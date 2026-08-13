using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float rotationSpeed = 12f;
    [Tooltip("Clic (o tocar) en el suelo para moverse, estilo Diablo.")]
    public bool clickToMove = true;

    private Rigidbody _rb;
    private Camera _camera;
    private CharacterStats _stats;
    private Vector3 _moveDirection;
    private Vector3? _clickTarget;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _stats = GetComponent<CharacterStats>();
        _rb.useGravity = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _camera = Camera.main != null ? Camera.main : null;
    }

    void Update()
    {
        Vector2 input = ReadKeyboardInput();
        input = Vector2.ClampMagnitude(input, 1f);

        if (_clickTarget.HasValue && ((Vector2)(_clickTarget.Value - transform.position)).sqrMagnitude < 0.04f)
            _clickTarget = null;

        if (ReadClickTarget())
        {
            _moveDirection = (_clickTarget.Value - transform.position).normalized;
        }
        else if (input.sqrMagnitude > 0.01f)
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
        // La clase (stats) define la velocidad: Ninja corre más, Vikingo menos.
        float speed = _stats != null ? _stats.MoveSpeed : moveSpeed;
        Vector3 velocity = _moveDirection * speed;
        velocity.y = _rb.linearVelocity.y;
        _rb.linearVelocity = velocity;
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
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && _camera != null)
        {
            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            RaycastHit[] hits = Physics.RaycastAll(ray, 200f);
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                _clickTarget = hit.point;
                break;
            }
        }
        return _clickTarget.HasValue;
    }
}