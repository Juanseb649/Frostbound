using UnityEngine;

// Balanceo de todo el cuerpo del pingüino (rebote, contoneo, inclinación, respiración y temblor).
// Lo usan el héroe y los NPC; el esqueleto (aletas, patas, cabeza) lo anima PenguinRigAnimator.
public class PenguinBodySway : MonoBehaviour
{
    [Header("Modelo")]
    public Transform model;
    [Tooltip("Velocidad (m/s) que cuenta como moverse al 100 %. Si hay CharacterStats se usa su velocidad.")]
    public float referenceSpeed = 6f;

    [Header("Al moverse")]
    public float bobFrequency = 14f;
    public float bobAmplitude = 0.12f;
    [Tooltip("Rebote por paso (siempre hacia arriba) en vez de onda continua.")]
    public bool stepBounce;
    public float rollAngle = 6f;
    public float leanAngle = 8f;

    [Header("Quieto")]
    public float idleFrequency = 1.2f;
    public float idleAmplitude = 0.025f;

    [Header("Miedo (NPC)")]
    [Range(0f, 1f)] public float fear;
    [Tooltip("Multiplica el miedo: los NPC se calman cuando el héroe está cerca.")]
    [Range(0f, 1f)] public float calm = 1f;
    public float shiverAmplitude = 0.02f;
    public float shiverFrequency = 32f;
    public float hunchAngle = 6f;

    private CharacterStats _stats;
    private Vector3 _basePos;
    private Quaternion _baseRot;
    private Vector3 _lastPos;
    private float _speed01;
    private float _phase;

    void Awake()
    {
        if (model == null)
        {
            Transform found = transform.Find("Penguin");
            if (found == null && transform.childCount > 0) found = transform.GetChild(0);
            model = found;
        }
        _stats = GetComponent<CharacterStats>();
        _lastPos = transform.position;
        _phase = Random.value * 100f;
    }

    private bool _captured;

    private void CaptureBase()
    {
        _basePos = model.localPosition;
        _baseRot = model.localRotation;
        _captured = true;
    }

    public void Configure(float bobFreq, float bobAmp, float roll, float lean, bool perStep, float idleFreq, float idleAmp)
    {
        bobFrequency = bobFreq;
        bobAmplitude = bobAmp;
        rollAngle = roll;
        leanAngle = lean;
        stepBounce = perStep;
        idleFrequency = idleFreq;
        idleAmplitude = idleAmp;
    }

    void LateUpdate()
    {
        if (model == null) return;
        if (!_captured) CaptureBase();

        float dt = Time.deltaTime;
        Vector3 delta = transform.position - _lastPos;
        _lastPos = transform.position;
        delta.y = 0f;
        float reference = _stats != null ? _stats.MoveSpeed : referenceSpeed;
        float target = dt > 0f ? Mathf.Clamp01(delta.magnitude / dt / Mathf.Max(0.01f, reference)) : 0f;
        _speed01 = Mathf.MoveTowards(_speed01, target, dt * 6f);
        float speed = _speed01;

        float t = Time.time + _phase;
        float motion = Mathf.Max(speed, 0.2f);
        float wave = Mathf.Sin(t * bobFrequency * motion * 0.5f);
        float bob = (stepBounce ? Mathf.Abs(wave) : Mathf.Sin(t * bobFrequency * motion)) * bobAmplitude * speed;
        float roll = wave * rollAngle * speed;
        float lean = leanAngle * speed;
        float idle = Mathf.Sin(t * idleFrequency) * idleAmplitude * (1f - speed);

        float shiver = fear * calm * (1f - speed);
        float jitterX = 0f, jitterRoll = 0f;
        if (shiver > 0.001f)
        {
            jitterX = (Mathf.PerlinNoise(t * shiverFrequency, 0f) - 0.5f) * 2f * shiverAmplitude * shiver;
            jitterRoll = (Mathf.PerlinNoise(0f, t * shiverFrequency) - 0.5f) * 6f * shiver;
        }
        float hunch = hunchAngle * fear * calm * (1f - speed);

        model.localPosition = _basePos + new Vector3(jitterX, bob + idle, 0f);
        model.localRotation = _baseRot * Quaternion.Euler(lean + hunch, 0f, roll + jitterRoll);
    }
}
