using UnityEngine;

// Animación procedural sobre el esqueleto del pingüino (aletas, patas, columna y cabeza).
// Estados: caminar, reposo y acciones de reposo (buscar con la aleta en la frente, golpetear la pata).
// Todas las poses se definen en el espacio del pingüino (+Z adelante, +Y arriba). El lado de cada aleta se detecta solo.
[DefaultExecutionOrder(200)]
public class PenguinRigAnimator : MonoBehaviour
{
    public enum IdleAction { None, Search, FootTap, Shrug }

    [Tooltip("Velocidad (m/s) que se considera caminar al 100%.")]
    public float referenceSpeed = 4f;

    [Header("Caminar")]
    [Tooltip("Pasos por segundo a velocidad máxima.")]
    public float stepRate = 2.4f;
    public float strideLength = 0.07f;
    [Tooltip("Altura (m) a la que se levanta cada pata al dar el paso.")]
    [Range(0f, 0.15f)] public float stepHeight = 0.05f;
    public float footPitch = 18f;
    public float flipperSwing = 24f;
    public float flipperOut = 10f;
    public float spineTwist = 5f;
    public float headBob = 3f;

    [Header("Reposo")]
    public float breathFrequency = 1.2f;
    public float idleFlipperSway = 4f;

    [Header("Acciones en reposo")]
    public bool enableIdleActions = true;
    [Tooltip("Segundos quieto antes de la primera acción.")]
    public float idleActionDelay = 10f;
    [Tooltip("Pausa aleatoria entre acciones (mín, máx).")]
    public Vector2 idleActionInterval = new Vector2(8f, 14f);

    public IdleAction CurrentAction => _action;

    private class Bone
    {
        public Transform t;
        public Quaternion restRot;
        public Vector3 restPos;
    }

    private Bone _spine, _head, _flipL, _flipR, _footL, _footR;
    private Vector3 _handRestL, _handRestR;
    private Transform _mover;
    private Vector3 _lastPos;
    private bool _ready;

    private float _speed01;
    private float _walkPhase;
    private float _idleTime;
    private float _nextActionAt;
    private IdleAction _action = IdleAction.None;
    private float _actionTime;
    private float _actionWeight;
    private float _actionSide = 1f;
    private float _seed;

    void Awake()
    {
        Init();
        _mover = transform.parent != null ? transform.parent : transform;
        _lastPos = _mover.position;
        _seed = Random.value * 100f;
        _nextActionAt = idleActionDelay;
    }

    public void Init()
    {
        if (_ready) return;
        _spine = Find("Spine");
        _head = Find("Head");
        _flipL = Find("Flipper_L");
        _flipR = Find("Flipper_R");
        _footL = Find("Foot_L");
        _footR = Find("Foot_R");
        Bone handL = Find("Anchor_Hand_L");
        Bone handR = Find("Anchor_Hand_R");
        _handRestL = handL != null ? handL.restPos : Vector3.zero;
        _handRestR = handR != null ? handR.restPos : Vector3.zero;
        _ready = true;
    }

    private Bone Find(string boneName)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name != boneName) continue;
            return new Bone
            {
                t = t,
                restRot = Quaternion.Inverse(transform.rotation) * t.rotation,
                restPos = transform.InverseTransformPoint(t.position)
            };
        }
        return null;
    }

    // ---------- API para otros sistemas (emotes, NPC, cinemáticas) ----------

    public void PlayAction(IdleAction action)
    {
        if (action == IdleAction.None) return;
        _action = action;
        _actionTime = 0f;
        _actionSide = Random.value < 0.5f ? 1f : -1f;
    }

    public static float ActionDuration(IdleAction action)
    {
        switch (action)
        {
            case IdleAction.Search: return 4.6f;
            case IdleAction.FootTap: return 2.6f;
            case IdleAction.Shrug: return 1.6f;
            default: return 0f;
        }
    }

    // ---------- Ciclo ----------

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        Vector3 delta = _mover.position - _lastPos;
        _lastPos = _mover.position;
        delta.y = 0f;
        float speed = dt > 0f ? delta.magnitude / dt : 0f;
        float target = Mathf.Clamp01(speed / Mathf.Max(0.01f, referenceSpeed));
        _speed01 = Mathf.MoveTowards(_speed01, target, dt * 6f);

        bool moving = _speed01 > 0.05f;
        if (moving)
        {
            _walkPhase += dt * Mathf.PI * 2f * stepRate * Mathf.Lerp(0.5f, 1f, _speed01) * 0.5f;
            _idleTime = 0f;
            _nextActionAt = idleActionDelay;
            if (_action != IdleAction.None) _action = IdleAction.None;
        }
        else
        {
            _idleTime += dt;
            if (enableIdleActions && _action == IdleAction.None && _idleTime >= _nextActionAt)
            {
                float roll = Random.value;
                PlayAction(roll < 0.65f ? IdleAction.Search : roll < 0.85f ? IdleAction.FootTap : IdleAction.Shrug);
                _nextActionAt = _idleTime + ActionDuration(_action) + Random.Range(idleActionInterval.x, idleActionInterval.y);
            }
        }

        if (_action != IdleAction.None)
        {
            _actionTime += dt;
            if (_actionTime >= ActionDuration(_action)) _action = IdleAction.None;
        }
        _actionWeight = Mathf.MoveTowards(_actionWeight, _action != IdleAction.None ? 1f : 0f, dt * (moving ? 8f : 4f));

        ApplyPose(Time.time + _seed);
    }

    // Permite posar el esqueleto desde el editor (capturas de prueba).
    public void DebugPose(float walkPhase, float walk01, IdleAction action, float actionTime, float side = 1f)
    {
        Init();
        _walkPhase = walkPhase;
        _speed01 = walk01;
        _action = action;
        _actionTime = actionTime;
        _actionWeight = action != IdleAction.None ? 1f : 0f;
        _actionSide = side;
        ApplyPose(0f);
    }

    // ---------- Poses ----------

    private void ApplyPose(float time)
    {
        if (!_ready) return;
        float walk = Mathf.SmoothStep(0f, 1f, _speed01);
        float idle = 1f - walk;
        float s = Mathf.Sin(_walkPhase);
        float c = Mathf.Cos(_walkPhase);
        float breath = Mathf.Sin(time * breathFrequency * Mathf.PI * 2f * 0.5f);

        // Caminar: patas alternadas que avanzan, se levantan y apuntan la punta.
        Vector3 footOffL = new Vector3(0f, stepHeight * Mathf.Max(0f, c), strideLength * s) * walk;
        Vector3 footOffR = new Vector3(0f, stepHeight * Mathf.Max(0f, -c), -strideLength * s) * walk;
        float pitchL = -footPitch * Mathf.Max(0f, c) * walk;
        float pitchR = -footPitch * Mathf.Max(0f, -c) * walk;

        // Aletas: balanceo contrario a la pata del mismo lado + un poco abiertas al caminar.
        float swingL = -flipperSwing * s * walk;
        float swingR = flipperSwing * s * walk;
        float outL = flipperOut * walk + idleFlipperSway * idle * (0.5f + 0.5f * breath);
        float outR = outL;

        float spineYaw = spineTwist * s * walk;
        float headYaw = -spineYaw * 0.6f;
        float headPitch = Mathf.Sin(_walkPhase * 2f) * headBob * walk - breath * 1.2f * idle;
        float headRoll = 0f;

        Quaternion saluteL = Quaternion.identity, saluteR = Quaternion.identity;
        float w = _actionWeight * idle;

        if (w > 0.001f)
        {
            float t = _actionTime;
            switch (_action)
            {
                case IdleAction.Search:
                {
                    float raise = Smooth(0f, 0.45f, t) * (1f - Smooth(3.4f, 3.9f, t));
                    float look = Smooth(0.35f, 0.6f, t) * (1f - Smooth(3.2f, 3.6f, t));
                    float yaw = Mathf.Sin(Mathf.Clamp01((t - 0.5f) / 2.8f) * Mathf.PI * 2f) * 42f * look * _actionSide;
                    headYaw += yaw * w;
                    headPitch += -6f * look * w;
                    spineYaw += yaw * 0.3f * w;
                    bool useLeft = _actionSide > 0f;
                    Quaternion salute = useLeft ? SalutePose(_flipL, _handRestL) : SalutePose(_flipR, _handRestR);
                    Quaternion follow = Quaternion.AngleAxis(yaw * 0.5f, Vector3.up);
                    Quaternion pose = Quaternion.Slerp(Quaternion.identity, follow * salute, raise * w);
                    if (useLeft) saluteL = pose; else saluteR = pose;

                    float shrug = Bump(3.8f, 4.6f, t) * w;
                    outL += 28f * shrug;
                    outR += 28f * shrug;
                    headRoll += 6f * shrug * _actionSide;
                    break;
                }
                case IdleAction.FootTap:
                {
                    float env = Smooth(0f, 0.2f, t) * (1f - Smooth(2.3f, 2.6f, t));
                    float tap = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2f * 2.6f)) * env * w;
                    if (_actionSide > 0f) pitchL += -24f * tap; else pitchR += -24f * tap;
                    headRoll += 5f * env * w * _actionSide;
                    headYaw += 10f * env * w * _actionSide;
                    float hands = 18f * env * w;
                    outL += hands;
                    outR += hands;
                    break;
                }
                case IdleAction.Shrug:
                {
                    float shrug = Bump(0f, 1.6f, t) * w;
                    outL += 35f * shrug;
                    outR += 35f * shrug;
                    headPitch += 5f * shrug;
                    headRoll += 8f * shrug * _actionSide;
                    break;
                }
            }
        }

        Quaternion spineDelta = Quaternion.AngleAxis(spineYaw, Vector3.up);
        SetRot(_spine, spineDelta);
        SetRot(_head, spineDelta * Quaternion.Euler(headPitch, headYaw, headRoll));

        SetFlipper(_flipL, spineDelta, saluteL, outL, swingL);
        SetFlipper(_flipR, spineDelta, saluteR, outR, swingR);
        SetFoot(_footL, footOffL, pitchL);
        SetFoot(_footR, footOffR, pitchR);
    }

    // Rotación que lleva la punta de la aleta a la frente (visera para mirar a lo lejos).
    private Quaternion SalutePose(Bone flipper, Vector3 handRest)
    {
        if (flipper == null || _head == null) return Quaternion.identity;
        float side = Side(flipper);
        Vector3 restDir = (handRest - flipper.restPos).normalized;
        if (restDir.sqrMagnitude < 0.5f) restDir = new Vector3(side * 0.4f, -1f, 0f).normalized;
        Vector3 brow = _head.restPos + new Vector3(side * 0.13f, 0.13f, 0.24f);
        Vector3 targetDir = (brow - flipper.restPos).normalized;
        return Quaternion.FromToRotation(restDir, targetDir);
    }

    private static float Side(Bone b) => b.restPos.x >= 0f ? 1f : -1f;

    private void SetFlipper(Bone b, Quaternion spineDelta, Quaternion extra, float outAngle, float swing)
    {
        if (b == null) return;
        Quaternion raise = Quaternion.AngleAxis(outAngle * Side(b), Vector3.forward);
        Quaternion pitch = Quaternion.AngleAxis(-swing, Vector3.right);
        SetRot(b, spineDelta * extra * pitch * raise);
    }

    private void SetFoot(Bone b, Vector3 offset, float pitch)
    {
        if (b == null) return;
        b.t.position = transform.TransformPoint(b.restPos + offset);
        SetRot(b, Quaternion.AngleAxis(pitch, Vector3.right));
    }

    private void SetRot(Bone b, Quaternion deltaInRoot)
    {
        if (b == null) return;
        b.t.rotation = transform.rotation * deltaInRoot * b.restRot;
    }

    private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

    private static float Bump(float a, float b, float x)
    {
        if (x <= a || x >= b) return 0f;
        return Mathf.Sin((x - a) / (b - a) * Mathf.PI);
    }
}
