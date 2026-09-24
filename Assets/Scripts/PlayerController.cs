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

    private Rigidbody _rb;
    private Camera _camera;
    private CharacterStats _stats;
    private Vector3 _moveDirection;
    private Vector3? _clickTarget;
    private float _stuckTimer;
    private Vector3 _lastPosition;
    private readonly RaycastHit[] _hits = new RaycastHit[16];

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _stats = GetComponent<CharacterStats>();
        _rb.useGravity = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _camera = Camera.main;
        _lastPosition = _rb.position;
    }

    void Update()
    {
        Vector2 input = Vector2.ClampMagnitude(ReadKeyboardInput(), 1f);

        if (_clickTarget.HasValue && FlatOffset(_clickTarget.Value).sqrMagnitude < stopDistance * stopDistance)
            _clickTarget = null;

        if (input.sqrMagnitude > 0.01f)
        {
            _clickTarget = null;
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (_camera != null)
            {
                forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up).normalized;
            }
            _moveDirection = (forward * input.y + right * input.x).normalized;
        }
        else if (ReadClickTarget())
        {
            _moveDirection = FlatOffset(_clickTarget.Value).normalized;
        }
        else
        {
            _moveDirection = Vector3.zero;
        }

        if (_moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
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
        velocity.y = _rb.linearVelocity.y;
        _rb.linearVelocity = velocity;

        if (_clickTarget.HasValue)
        {
            _stuckTimer = actualSpeed < speed * 0.2f ? _stuckTimer + Time.fixedDeltaTime : 0f;
            if (_stuckTimer > stuckTimeout)
            {
                _clickTarget = null;
                _stuckTimer = 0f;
            }
        }
        else
        {
            _stuckTimer = 0f;
        }
    }

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

    private bool ReadClickTarget()
    {
        if (!clickToMove) return false;

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed && _camera != null)
        {
            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            int count = Physics.RaycastNonAlloc(ray, _hits, 200f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(transform)) continue;
                if (_hits[i].distance >= best) continue;
                best = _hits[i].distance;
                _clickTarget = _hits[i].point;
            }
        }
        return _clickTarget.HasValue;
    }
}
