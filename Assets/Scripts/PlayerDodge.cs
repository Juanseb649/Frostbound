using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Rodar con Espacio (o A/Cruz del mando): el pingüino da una voltereta hacia donde se mueve
// (o hacia donde mira si está quieto), un poco más rápido que caminando y sin recibir daño mientras rueda.
[RequireComponent(typeof(Rigidbody))]
[DefaultExecutionOrder(1000)]
public class PlayerDodge : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Velocidad al rodar respecto a la de caminar (1.5 = 50 % más rápido).")]
    [Range(1f, 3f)] public float speedMultiplier = 1.5f;
    [Tooltip("Duración de la voltereta (segundos).")]
    [Range(0.2f, 1f)] public float duration = 0.45f;
    [Tooltip("Espera entre dos volteretas (segundos, contando desde que termina la anterior).")]
    [Range(0f, 2f)] public float cooldown = 0.35f;
    [Tooltip("No recibe daño mientras rueda.")]
    public bool invulnerable = true;

    [Header("Voltereta")]
    [Tooltip("Modelo que gira (vacío = el hijo \"Penguin\").")]
    public Transform model;
    [Tooltip("Altura del centro de giro (metros). Negativo = mitad del collider.")]
    public float pivotHeight = -1f;

    public bool IsRolling { get; private set; }

    private Rigidbody _rb;
    private PlayerController _controller;
    private CharacterStats _stats;
    private Camera _cam;
    private Vector3 _dir;
    private float _startedAt;
    private float _readyAt;
    private bool _controllerWasEnabled;

    // Para quitar el giro del frame anterior si nadie más reescribió la pose del modelo.
    private bool _applied;
    private Quaternion _baseRot, _lastRot;
    private Vector3 _basePos, _lastPos;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _controller = GetComponent<PlayerController>();
        _stats = GetComponent<CharacterStats>();
        if (model == null)
        {
            model = transform.Find("Penguin");
            if (model == null && transform.childCount > 0) model = transform.GetChild(0);
        }
        if (pivotHeight < 0f)
        {
            CapsuleCollider col = GetComponent<CapsuleCollider>();
            pivotHeight = col != null ? col.center.y : 0.5f;
        }
    }

    void OnDisable()
    {
        if (IsRolling) EndRoll();
    }

    void Update()
    {
        if (IsRolling)
        {
            if (Time.time - _startedAt >= duration) EndRoll();
            return;
        }
        if (GameplayInput.Blocked || Time.time < _readyAt || (_stats != null && _stats.IsDead)) return;

        bool pressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                       || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame
                           && (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null));
        if (pressed) StartRoll(RollDirection());
    }

    // Rueda por código (pruebas, tutoriales). Devuelve false si no puede rodar ahora.
    public bool TryRoll()
    {
        if (IsRolling || GameplayInput.Blocked || Time.time < _readyAt || (_stats != null && _stats.IsDead)) return false;
        StartRoll(RollDirection());
        return true;
    }

    void FixedUpdate()
    {
        if (!IsRolling) return;
        float speed = (_stats != null ? _stats.MoveSpeed : 6f) * speedMultiplier;
        Vector3 v = _dir * speed;
        v.y = _rb.linearVelocity.y;
        _rb.linearVelocity = v;
        _rb.angularVelocity = Vector3.zero;
        _rb.MoveRotation(Quaternion.LookRotation(_dir, Vector3.up));
    }

    void LateUpdate()
    {
        if (model == null) return;

        Quaternion rot = model.localRotation;
        Vector3 pos = model.localPosition;
        if (_applied && Quaternion.Angle(rot, _lastRot) < 0.01f && (pos - _lastPos).sqrMagnitude < 1e-8f)
        {
            rot = _baseRot;
            pos = _basePos;
        }

        if (!IsRolling)
        {
            if (_applied)
            {
                model.localRotation = rot;
                model.localPosition = pos;
                _applied = false;
            }
            return;
        }

        float t = Mathf.Clamp01((Time.time - _startedAt) / duration);
        float eased = t * t * (3f - 2f * t);
        Quaternion flip = Quaternion.AngleAxis(360f * eased, Vector3.right);
        Vector3 pivot = Vector3.up * pivotHeight;

        _baseRot = rot;
        _basePos = pos;
        model.localRotation = flip * rot;
        model.localPosition = pivot + flip * (pos - pivot);
        _lastRot = model.localRotation;
        _lastPos = model.localPosition;
        _applied = true;
    }

    private Vector3 RollDirection()
    {
        if (_cam == null) _cam = Camera.main;
        Vector2 input = Vector2.zero;
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
        }
        if (Gamepad.current != null && input.sqrMagnitude < 0.01f) input = Gamepad.current.leftStick.ReadValue();

        if (input.sqrMagnitude > 0.01f)
        {
            Vector3 forward = Vector3.forward, right = Vector3.right;
            if (_cam != null)
            {
                forward = Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(_cam.transform.right, Vector3.up).normalized;
            }
            return (forward * input.y + right * input.x).normalized;
        }

        Vector3 vel = _rb.linearVelocity;
        vel.y = 0f;
        if (vel.sqrMagnitude > 0.25f) return vel.normalized;
        Vector3 facing = transform.forward;
        facing.y = 0f;
        return facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward;
    }

    private void StartRoll(Vector3 dir)
    {
        _dir = dir;
        _startedAt = Time.time;
        IsRolling = true;
        if (_controller != null)
        {
            // Cancela el destino del clic y deja al héroe mirando hacia donde rodó.
            _controller.FaceAndHold(dir, 0f);
            _controllerWasEnabled = _controller.enabled;
            _controller.enabled = false;
        }
        if (invulnerable && _stats != null) _stats.invulnerable = true;
    }

    private void EndRoll()
    {
        IsRolling = false;
        _readyAt = Time.time + cooldown;
        if (_controller != null && _controllerWasEnabled && !GameplayInput.Blocked) _controller.enabled = true;
        if (_stats != null) _stats.invulnerable = false;
        Vector3 v = _rb.linearVelocity;
        _rb.linearVelocity = new Vector3(0f, v.y, 0f);
    }
}
