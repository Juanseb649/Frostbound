using UnityEngine;

public class PenguinAnimator : MonoBehaviour
{
    [Header("Model")]
    public Transform model;

    [Header("Walk/Run")]
    [Tooltip("Que tan rápido bombea al caminar (alto = más acelerado).")]
    public float bobFrequency = 14f;
    [Tooltip("Altura del rebote al moverse (metros).")]
    public float bobAmplitude = 0.12f;
    [Tooltip("Balanceo lateral tipo waddle (grados).")]
    public float rollAngle = 6f;
    [Tooltip("Inclinación hacia adelante al correr (grados).")]
    public float leanAngle = 8f;

    [Header("Idle")]
    [Tooltip("Respiración sutil cuando está quieto.")]
    public float idleFrequency = 1.2f;
    public float idleAmplitude = 0.025f;

    private Rigidbody _rb;
    private PlayerController _controller;
    private CharacterStats _stats;
    private Vector3 _basePos;
    private Quaternion _baseRot;

    void Awake()
    {
        if (model == null)
        {
            Transform found = transform.Find("Penguin");
            if (found == null && transform.childCount > 0) found = transform.GetChild(0);
            model = found;
        }

        _rb = GetComponent<Rigidbody>();
        _controller = GetComponent<PlayerController>();
        _stats = GetComponent<CharacterStats>();

        if (model != null)
        {
            _basePos = model.localPosition;
            _baseRot = model.localRotation;
        }
    }

    void LateUpdate()
    {
        if (model == null) return;

        float speed = 0f;
        if (_rb != null)
            speed = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.z).magnitude;

        // Normaliza con la velocidad real de la clase (stats), no el campo fijo.
        float referenceSpeed = _controller != null ? _controller.moveSpeed : 6f;
        if (_stats != null) referenceSpeed = _stats.MoveSpeed;

        if (referenceSpeed > 0.01f) speed /= referenceSpeed;
        speed = Mathf.Clamp01(speed);

        float t = Time.time;
        float motion = Mathf.Max(speed, 0.2f);

        float bob = Mathf.Sin(t * bobFrequency * motion) * bobAmplitude * speed;
        float roll = Mathf.Sin(t * bobFrequency * motion * 0.5f) * rollAngle * speed;
        float lean = leanAngle * speed;
        float idle = Mathf.Sin(t * idleFrequency) * idleAmplitude * (1f - speed);

        Vector3 pos = _basePos;
        pos.y += bob + idle;
        model.localPosition = pos;

        model.localRotation = _baseRot * Quaternion.Euler(lean, 0f, roll);
    }
}