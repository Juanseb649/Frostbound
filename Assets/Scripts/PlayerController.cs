using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Tooltip("0 = jugador 1 (teclado y ratón), 1 = jugador 2 (mando).")]
    public int playerIndex;

    public CharacterStats Stats => _stats != null ? _stats : _stats = GetComponent<CharacterStats>();

    // Diálogo con un NPC: mientras el menú está abierto o el NPC habla, este héroe no se mueve, no rueda ni ataca.
    [System.NonSerialized] public float dialogueUntil;
    public bool InDialogue
    {
        get
        {
            WorldHUD hud = WorldHUD.Instance;
            if (hud != null && hud.MenuOpen && hud.MenuTalker == this) return true;
            if (TradeWindow.IsOpen && TradeWindow.Customer == gameObject) return true;
            return Time.time < dialogueUntil;
        }
    }

    public static bool Busy(Component c)
    {
        PlayerController pc = c != null ? c.GetComponent<PlayerController>() : null;
        return pc != null && pc.InDialogue;
    }

    public void LockForDialogue(float seconds) => dialogueUntil = Mathf.Max(dialogueUntil, Time.time + seconds);

    // Mando del jugador 2 (el jugador 1 usa teclado y ratón, o un mando que no esté reclamado).
    [System.NonSerialized] public Gamepad pad;
    // Jugador 2 con teclado y ratón (el jugador 1 pasa entonces al mando).
    [System.NonSerialized] public bool keyboardPlayer;
    public bool IsPadPlayer => playerIndex > 0 ? pad != null : PadOf(this) != null;

    // ¿El teclado lo tiene el jugador 2?
    public static bool KeyboardTakenBySecond
    {
        get
        {
            foreach (PlayerController p in Players.All) if (p != null && p.playerIndex > 0 && p.keyboardPlayer) return true;
            return false;
        }
    }

    public static bool Claimed(Gamepad g)
    {
        if (g == null) return false;
        foreach (PlayerController p in Players.All) if (p != null && p.playerIndex > 0 && p.pad == g) return true;
        return false;
    }

    // Mando del jugador 1: solo si su periférico preferido es el mando (Configuración → Control).
    public static Gamepad P1Pad
    {
        get
        {
            if (InputMap.Preferred != InputMap.Device.Gamepad && !KeyboardTakenBySecond) return null;
            Gamepad g = Gamepad.current;
            if (g != null && !Claimed(g)) return g;
            foreach (Gamepad other in Gamepad.all) if (!Claimed(other)) return other;
            return null;
        }
    }

    // Mando que maneja a este héroe (null = ninguno).
    public static Gamepad PadOf(Component c)
    {
        PlayerController pc = c != null ? c.GetComponent<PlayerController>() : null;
        if (pc != null && pc.playerIndex > 0) return pc.pad;
        return P1Pad;
    }

    // Compatibilidad: el mando que usa el jugador 1 (para no confundirlo con el que pulsa START para unirse).
    public static Gamepad RecentP1Pad => P1Pad;
    public static void NoteP1Pad(Gamepad g) { }

    public static bool UsesKeyboard(Component c)
    {
        PlayerController pc = c != null ? c.GetComponent<PlayerController>() : null;
        if (pc == null) return !KeyboardTakenBySecond;
        // El jugador 1 con mando no lee teclado ni ratón (un botón del ratón "pegado" lo dejaba atacando sin moverse).
        return pc.playerIndex == 0 ? !KeyboardTakenBySecond && P1Pad == null : pc.keyboardPlayer;
    }

    public static Vector2 Stick(Gamepad g)
    {
        if (g == null) return Vector2.zero;
        Vector2 v = g.leftStick.ReadValue();
        return v.sqrMagnitude < 0.04f ? Vector2.zero : Vector2.ClampMagnitude(v, 1f);
    }

    void OnEnable() => Players.Register(this);
    void OnDestroy() => Players.Unregister(this);

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
        if (InDialogue)
        {
            // Se salta el resto del diálogo con Esc o B.
            if (Time.time < dialogueUntil && UICancel.Pressed()) dialogueUntil = 0f;
            _moveDirection = Vector3.zero;
            _clickTarget = null;
            _attackTarget = null;
            return;
        }
        if (IsPadPlayer)
        {
            PadUpdate();
            return;
        }
        bool keys = UsesKeyboard(this);
        if (keys) UpdateHover();

        Vector2 input = keys ? Vector2.ClampMagnitude(ReadKeyboardInput(), 1f) : Vector2.zero;

        if (_clickTarget.HasValue && FlatOffset(_clickTarget.Value).sqrMagnitude < stopDistance * stopDistance)
            _clickTarget = null;

        if (keys && InputMap.KeyPressed(GameAction.Interact) && !GameplayInput.Blocked && !PickUpNearest()) TalkToNearest();

        if (Time.time < _holdUntil)
        {
            _moveDirection = Vector3.zero;
            if (keys) ReadClick();
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
        else if (keys && ReadClick())
        {
            _moveDirection = Steer(_clickTarget.Value);
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
        if (hud != null && hud.MenuOpen) CloseMenuIfAway(hud);
    }

    // El menú del NPC se cierra cuando ningún héroe sigue a su lado.
    private void CloseMenuIfAway(WorldHUD hud)
    {
        NPCInteractable npc = hud.MenuTarget;
        if (npc == null) return;
        float limit = npc.interactRange + menuCloseMargin;
        PlayerController near = Players.Nearest(npc.transform.position);
        Vector3 from = near != null ? near.transform.position : transform.position;
        Vector3 d = npc.transform.position - from;
        d.y = 0f;
        if (d.sqrMagnitude > limit * limit) hud.CloseMenu();
    }

    // Jugador 2: se mueve con el stick izquierdo; Y recoge o habla. Ataque, habilidad y esquiva los leen sus componentes.
    private void PadUpdate()
    {
        Gamepad g = PadOf(this);
        Vector2 input = GameplayInput.Blocked || (Stats != null && Stats.IsDead) ? Vector2.zero : Stick(g);
        bool interact = InputMap.PadPressed(g, GameAction.Interact) || (UsesKeyboard(this) && InputMap.KeyPressed(GameAction.Interact));
        if (interact && !GameplayInput.Blocked && !PickUpNearest()) TalkToNearest();

        if (Time.time < _holdUntil)
        {
            _moveDirection = Vector3.zero;
            return;
        }
        if (input.sqrMagnitude > 0f)
        {
            _clickTarget = null;
            _interactTarget = null;
            _pickupTarget = null;
            _attackTarget = null;
            Vector3 forward = Vector3.forward, right = Vector3.right;
            if (_camera != null)
            {
                forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up).normalized;
            }
            _moveDirection = (forward * input.y + right * input.x).normalized;
        }
        else if (_pickupTarget != null) _moveDirection = ApproachPickupTarget();
        else if (_interactTarget != null) _moveDirection = ApproachInteractTarget();
        else _moveDirection = Vector3.zero;

        if (_moveDirection.sqrMagnitude > 0.01f)
            _targetRotation = Quaternion.LookRotation(_moveDirection, Vector3.up);
        WorldHUD hud = WorldHUD.Instance;
        if (hud != null && hud.MenuOpen) CloseMenuIfAway(hud);
    }

    // Dirección en la que apunta el mando (stick) o, si está suelto, hacia donde mira.
    public Vector3 PadAimDirection()
    {
        Vector2 st = Stick(PadOf(this));
        if (st.sqrMagnitude > 0f && _camera != null)
        {
            Vector3 f = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
            Vector3 r = Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up).normalized;
            return (f * st.y + r * st.x).normalized;
        }
        Vector3 fw = transform.forward;
        fw.y = 0f;
        return fw.sqrMagnitude > 0.01f ? fw.normalized : Vector3.forward;
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
        velocity.y = SnapVertical(_rb.linearVelocity.y, speed);
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

    // Pegado al suelo: al subir una rampa o pasar una loma la física no puede lanzarlo por los aires.
    private CapsuleCollider _capsule;
    private float SnapVertical(float vy, float speed)
    {
        if (_capsule == null) _capsule = GetComponent<CapsuleCollider>();
        vy = Mathf.Min(vy, speed * 0.8f);
        if (vy <= -3f || _moveDirection.sqrMagnitude < 0.01f || _capsule == null) return vy;
        float feet = _capsule.bounds.min.y;
        Vector3 origin = new Vector3(_rb.position.x, feet + 0.3f, _rb.position.z);
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, 1.1f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (_hits[i].collider == null || _hits[i].collider.attachedRigidbody == _rb) continue;
            best = Mathf.Min(best, _hits[i].distance);
        }
        float gap = best - 0.3f;
        if (gap > 0.05f && gap < 0.8f) return -Mathf.Max(4f, speed);
        return vy;
    }

    // ---------- Navegación ----------

    // Dirección hacia un destino siguiendo el NavMesh: rodea casas, rocas y paredes en lugar de chocar contra ellas.
    // Sin NavMesh en la zona (o destino fuera de él) va en línea recta como antes.
    private NavMeshPath _path;
    private Vector3 _pathGoal = new Vector3(float.MaxValue, 0f, 0f);
    private float _pathAt;
    private int _corner;
    public bool UsingNavMesh { get; private set; }

    private Vector3 Steer(Vector3 goal)
    {
        Vector3 straight = FlatOffset(goal).normalized;
        if (_path == null) _path = new NavMeshPath();
        if ((goal - _pathGoal).sqrMagnitude > 0.36f || Time.time - _pathAt > 0.35f)
        {
            _pathAt = Time.time;
            _pathGoal = goal;
            _corner = 1;
            UsingNavMesh = false;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit from, 1.5f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(goal, out NavMeshHit to, 2.5f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _path)
                && _path.status != NavMeshPathStatus.PathInvalid && _path.corners.Length >= 2)
                UsingNavMesh = true;
        }
        if (!UsingNavMesh) return straight;
        Vector3[] corners = _path.corners;
        while (_corner < corners.Length - 1 && FlatOffset(corners[_corner]).sqrMagnitude < 0.36f) _corner++;
        if (_corner >= corners.Length) return straight;
        Vector3 dir = FlatOffset(corners[_corner]);
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : straight;
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
            if (WorldHUD.Instance != null) { WorldHUD.Instance.MenuTalker = this; WorldHUD.Instance.OpenMenu(_interactTarget); }
            _interactTarget = null;
            return Vector3.zero;
        }
        return Steer(_interactTarget.transform.position);
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
        return Steer(_pickupTarget.transform.position);
    }

    private void TryPickUp(WorldItem item)
    {
        if (item == null || !item.CanPickUp) return;
        string label = item.Label;
        if (item.TryPickUp(_inventory))
            Notifications.Show("Recogiste " + label, FrostboundUI.Muted);
        else
            Notifications.Show("La mochila está llena", FrostboundUI.Negative);
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
        return Steer(_attackTarget.transform.position);
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

    private Vector2 ReadKeyboardInput() => InputMap.KeyboardMove();

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
                if (enemy != null && enemy.friendly) enemy = null;
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
