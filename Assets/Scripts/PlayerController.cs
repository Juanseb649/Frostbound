using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float rotationSpeed = 12f;
    [Tooltip("Clic (o tocar) en el suelo para moverse, estilo Diablo. Mantener pulsado sigue al cursor.")]
    public bool clickToMove = true;
    [Tooltip("Distancia (m) a la que se considera que llegó al punto clicado.")]
    public float stopDistance = 0.2f;
    [Tooltip("Segundos empujando contra un obstáculo antes de cancelar el destino.")]
    public float stuckTimeout = 0.35f;

    [Header("Interacción")]
    [Tooltip("Tecla para hablar con el NPC más cercano.")]
    public Key interactKey = Key.E;
    [Tooltip("Se cierra el menú del NPC si el héroe se aleja más que su alcance más este margen.")]
    public float menuCloseMargin = 1.5f;

    private Rigidbody _rb;
    private Camera _camera;
    private CharacterStats _stats;
    private Vector3 _moveDirection;
    private Vector3? _clickTarget;
    private NPCInteractable _interactTarget;
    private WorldItem _pickupTarget;
    private Damageable _attackTarget;
    private PlayerCombat _combat;
    private Inventory _inventory;
    private float _holdUntil;
    private Vector3 _lungeVelocity;
    private float _lungeFrom, _lungeUntil;
    [Tooltip("Distancia a la que se recoge un objeto del suelo.")]
    public float pickupRange = 1.3f;
    private NPCInteractable _hovered;
    private bool _pressStartedOnUI;
    private float _stuckTimer;
    private Vector3 _lastPosition;
    private Quaternion _targetRotation;
    private readonly RaycastHit[] _hits = new RaycastHit[16];

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _stats = GetComponent<CharacterStats>();
        _rb.useGravity = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _rb.angularVelocity = Vector3.zero;
        _targetRotation = _rb.rotation;
        _camera = Camera.main;
        _lastPosition = _rb.position;
        _combat = GetComponent<PlayerCombat>();
        _inventory = GetComponent<Inventory>();
    }

    // El héroe mira hacia dir y se queda quieto un momento (golpes y disparos).
    public void FaceAndHold(Vector3 dir, float seconds)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) _targetRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        _holdUntil = Time.time + seconds;
        _clickTarget = null;
    }

    // Pequeño paso hacia delante durante un golpe (el tajo aprovecha la cadencia del paso).
    public void Lunge(Vector3 dir, float meters, float delay, float seconds)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f || seconds <= 0f) return;
        _lungeVelocity = dir.normalized * (meters / seconds);
        _lungeFrom = Time.time + delay;
        _lungeUntil = _lungeFrom + seconds;
    }

    // Caminar hasta un objeto del suelo y recogerlo.
    public void PickUp(WorldItem item)
    {
        if (item == null) return;
        _clickTarget = null;
        _interactTarget = null;
        _attackTarget = null;
        _pickupTarget = item;
    }

    void Update()
    {
        if (_camera == null) _camera = Camera.main;
        UpdateHover();

        Vector2 input = Vector2.ClampMagnitude(ReadKeyboardInput(), 1f);

        if (_clickTarget.HasValue && FlatOffset(_clickTarget.Value).sqrMagnitude < stopDistance * stopDistance)
            _clickTarget = null;

        Keyboard kb = Keyboard.current;
        if (kb != null && kb[interactKey].wasPressedThisFrame && !PickUpNearest()) TalkToNearest();

        if (Time.time < _holdUntil)
        {
            _moveDirection = Vector3.zero;
            ReadClick();
            return;
        }

        if (input.sqrMagnitude > 0.01f)
        {
            _clickTarget = null;
            _interactTarget = null;
            _pickupTarget = null;
            _attackTarget = null;
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (_camera != null)
            {
                forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up).normalized;
            }
            _moveDirection = (forward * input.y + right * input.x).normalized;
        }
        else if (ReadClick())
        {
            _moveDirection = FlatOffset(_clickTarget.Value).normalized;
        }
        else if (_attackTarget != null)
        {
            _moveDirection = ApproachAttackTarget();
        }
        else if (_pickupTarget != null)
        {
            _moveDirection = ApproachPickupTarget();
        }
        else if (_interactTarget != null)
        {
            _moveDirection = ApproachInteractTarget();
        }
        else
        {
            _moveDirection = Vector3.zero;
        }

        if (_moveDirection.sqrMagnitude > 0.01f)
            _targetRotation = Quaternion.LookRotation(_moveDirection, Vector3.up);

        WorldHUD hud = WorldHUD.Instance;
        if (hud != null && hud.MenuOpen)
        {
            NPCInteractable npc = hud.MenuTarget;
            float limit = npc.interactRange + menuCloseMargin;
            if (FlatOffset(npc.transform.position).sqrMagnitude > limit * limit) hud.CloseMenu();
        }
    }

    void FixedUpdate()
    {
        float speed = _stats != null ? _stats.MoveSpeed : moveSpeed;
        Vector3 moved = _rb.position - _lastPosition;
        moved.y = 0f;
        float actualSpeed = moved.magnitude / Time.fixedDeltaTime;
        _lastPosition = _rb.position;

        Vector3 velocity = _moveDirection * speed;
        if (Time.time >= _lungeFrom && Time.time < _lungeUntil) velocity = _lungeVelocity;
        velocity.y = _rb.linearVelocity.y;
        _rb.linearVelocity = velocity;

        // La rotación la controla solo el script: los choques no pueden dejarlo girando.
        _rb.angularVelocity = Vector3.zero;
        _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, _targetRotation, rotationSpeed * Time.fixedDeltaTime));

        if (_clickTarget.HasValue || _interactTarget != null || _pickupTarget != null || (_attackTarget != null && _moveDirection.sqrMagnitude > 0.01f))
        {
            _stuckTimer = actualSpeed < speed * 0.2f && _moveDirection.sqrMagnitude > 0.01f ? _stuckTimer + Time.fixedDeltaTime : 0f;
            if (_stuckTimer > stuckTimeout)
            {
                _clickTarget = null;
                _interactTarget = null;
                _pickupTarget = null;
                _attackTarget = null;
                _stuckTimer = 0f;
            }
        }
        else
        {
            _stuckTimer = 0f;
        }
    }

    // ---------- Interacción con NPC ----------

    public void MoveTo(Vector3 point)
    {
        _interactTarget = null;
        _pickupTarget = null;
        _attackTarget = null;
        _clickTarget = point;
    }

    public void Interact(NPCInteractable npc)
    {
        if (npc == null) return;
        _clickTarget = null;
        _interactTarget = npc;
        if (WorldHUD.Instance != null) WorldHUD.Instance.CloseMenu();
    }

    private Vector3 ApproachInteractTarget()
    {
        Vector3 offset = FlatOffset(_interactTarget.transform.position);
        float reach = _interactTarget.interactRange;
        if (offset.sqrMagnitude <= reach * reach)
        {
            _targetRotation = Quaternion.LookRotation(offset.sqrMagnitude > 0.001f ? offset.normalized : transform.forward, Vector3.up);
            if (WorldHUD.Instance != null) WorldHUD.Instance.OpenMenu(_interactTarget);
            _interactTarget = null;
            return Vector3.zero;
        }
        return offset.normalized;
    }

    private Vector3 ApproachPickupTarget()
    {
        if (_pickupTarget == null) return Vector3.zero;
        Vector3 offset = FlatOffset(_pickupTarget.transform.position);
        if (offset.sqrMagnitude <= pickupRange * pickupRange)
        {
            TryPickUp(_pickupTarget);
            _pickupTarget = null;
            return Vector3.zero;
        }
        return offset.normalized;
    }

    private void TryPickUp(WorldItem item)
    {
        if (item == null || !item.CanPickUp) return;
        string label = item.Label;
        if (item.TryPickUp(_inventory))
            FindHud()?.ShowMessage("Recogiste " + label, FrostboundUI.Muted);
        else
            FindHud()?.ShowMessage("La mochila está llena", FrostboundUI.Negative);
    }

    private GameHUD _hud;
    private GameHUD FindHud()
    {
        if (_hud == null) _hud = FindAnyObjectByType<GameHUD>();
        return _hud;
    }

    private bool PickUpNearest()
    {
        WorldItem best = null;
        float bestDist = float.MaxValue;
        foreach (WorldItem w in FindObjectsByType<WorldItem>())
        {
            float d = FlatOffset(w.transform.position).sqrMagnitude;
            if (d < 2.2f * 2.2f && d < bestDist && w.CanPickUp)
            {
                best = w;
                bestDist = d;
            }
        }
        if (best == null) return false;
        PickUp(best);
        return true;
    }

    // Clic izquierdo sobre un objetivo: acercarse hasta tenerlo al alcance del arma y atacar mientras se mantiene pulsado.
    private Vector3 ApproachAttackTarget()
    {
        Mouse mouse = Mouse.current;
        if (_attackTarget == null || _attackTarget.IsDead || _combat == null || mouse == null || !mouse.leftButton.isPressed)
        {
            _attackTarget = null;
            return Vector3.zero;
        }
        Vector3 offset = FlatOffset(_attackTarget.transform.position);
        float reach = _combat.Range + 0.3f;
        if (offset.sqrMagnitude <= reach * reach)
        {
            _combat.TryAttack(_attackTarget.transform.position);
            return Vector3.zero;
        }
        return offset.normalized;
    }

    private void TalkToNearest()
    {
        WorldHUD hud = WorldHUD.Instance;
        if (hud != null && hud.MenuOpen) return;
        NPCInteractable best = null;
        float bestDist = float.MaxValue;
        foreach (NPCInteractable npc in FindObjectsByType<NPCInteractable>())
        {
            float reach = npc.interactRange + 1.2f;
            float d = FlatOffset(npc.transform.position).sqrMagnitude;
            if (d < reach * reach && d < bestDist)
            {
                best = npc;
                bestDist = d;
            }
        }
        if (best != null) Interact(best);
    }

    private NPCInteractable NpcUnderCursor(Vector2 screen)
    {
        if (_camera == null) return null;
        Ray ray = _camera.ScreenPointToRay(screen);
        int count = Physics.RaycastNonAlloc(ray, _hits, 200f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        NPCInteractable found = null;
        for (int i = 0; i < count; i++)
        {
            if (_hits[i].transform.IsChildOf(transform) || _hits[i].distance >= best) continue;
            best = _hits[i].distance;
            found = _hits[i].collider.GetComponentInParent<NPCInteractable>();
        }
        return found;
    }

    private Collider ColliderUnderCursor(Vector2 screen)
    {
        if (_camera == null) return null;
        Ray ray = _camera.ScreenPointToRay(screen);
        int count = Physics.RaycastNonAlloc(ray, _hits, 200f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Collider found = null;
        for (int i = 0; i < count; i++)
        {
            if (_hits[i].transform.IsChildOf(transform) || _hits[i].distance >= best) continue;
            best = _hits[i].distance;
            found = _hits[i].collider;
        }
        return found;
    }

    private WorldItem _hoveredItem;

    private void UpdateHover()
    {
        Mouse mouse = Mouse.current;
        NPCInteractable now = null;
        WorldItem item = null;
        if (mouse != null && !WorldHUD.PointerOverUI())
        {
            now = NpcUnderCursor(mouse.position.ReadValue());
            Collider under = ColliderUnderCursor(mouse.position.ReadValue());
            item = under != null ? under.GetComponentInParent<WorldItem>() : null;
        }
        if (item != _hoveredItem && !WorldHUD.PointerOverUI())
        {
            if (_hoveredItem != null) _hoveredItem.Highlighted = false;
            _hoveredItem = item;
            if (_hoveredItem != null) _hoveredItem.Highlighted = true;
        }
        if (now == _hovered) return;
        SetHighlight(_hovered, false);
        _hovered = now;
        SetHighlight(_hovered, true);
    }

    private static void SetHighlight(NPCInteractable npc, bool on)
    {
        if (npc == null) return;
        NameTag tag = npc.GetComponent<NameTag>();
        if (tag != null) tag.Highlighted = on;
    }

    // ---------- Entrada ----------

    private Vector3 FlatOffset(Vector3 point)
    {
        Vector3 offset = point - transform.position;
        offset.y = 0f;
        return offset;
    }

    private Vector2 ReadKeyboardInput()
    {
        Vector2 input = Vector2.zero;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return input;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        return input;
    }

    // Devuelve true si hay un punto del suelo al que caminar.
    private bool ReadClick()
    {
        if (!clickToMove) return false;
        Mouse mouse = Mouse.current;
        if (mouse == null || _camera == null) return _clickTarget.HasValue;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            _pressStartedOnUI = WorldHUD.PointerOverUI();
            if (!_pressStartedOnUI)
            {
                NPCInteractable npc = NpcUnderCursor(mouse.position.ReadValue());
                if (npc != null)
                {
                    Interact(npc);
                    return false;
                }
                Collider under = ColliderUnderCursor(mouse.position.ReadValue());
                WorldItem item = under != null ? under.GetComponentInParent<WorldItem>() : null;
                if (item != null)
                {
                    PickUp(item);
                    return false;
                }
                Damageable enemy = under != null ? under.GetComponentInParent<Damageable>() : null;
                if (enemy != null && _combat != null)
                {
                    _clickTarget = null;
                    _interactTarget = null;
                    _pickupTarget = null;
                    _attackTarget = enemy;
                    return false;
                }
                _interactTarget = null;
                _pickupTarget = null;
                if (WorldHUD.Instance != null) WorldHUD.Instance.CloseMenu();
            }
        }

        if (mouse.leftButton.isPressed && !_pressStartedOnUI && _interactTarget == null && _pickupTarget == null && _attackTarget == null)
        {
            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            int count = Physics.RaycastNonAlloc(ray, _hits, 200f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(transform)) continue;
                if (_hits[i].collider.GetComponentInParent<NPCInteractable>() != null) continue;
                if (_hits[i].collider.GetComponentInParent<WorldItem>() != null) continue;
                if (_hits[i].distance >= best) continue;
                best = _hits[i].distance;
                _clickTarget = _hits[i].point;
            }
        }
        return _clickTarget.HasValue;
    }
}
