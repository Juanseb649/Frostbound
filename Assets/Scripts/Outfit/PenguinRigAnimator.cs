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

    private Bone _spine, _head, _flipL, _flipR, _footL, _footR, _hips;
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

    private bool _armed;
    private int _comboStep;
    private Vector3 _tipRest = Vector3.up;
    private Quaternion _spinApplied = Quaternion.identity, _lastWritten = Quaternion.identity;
    private bool _spinning;
    private Transform _wrist;
    private Quaternion _wristRestLocal = Quaternion.identity;
    // Rotación de reposo de cada mano, guardada al iniciar (antes de que la muñeca la gire).
    private readonly System.Collections.Generic.Dictionary<Transform, Quaternion> _handRest = new System.Collections.Generic.Dictionary<Transform, Quaternion>();
    private Vector3 _tipLocal = Vector3.up;
    private Vector3 _comboArm, _bladeDir, _bladeVel, _comboBlade, _comboMotion;
    private Vector3 _edgeLocal = Vector3.right;
    private float _twist;
    private float _comboArmWeight;

    [Tooltip("Rigidez del resorte de la muñeca (más alto = la hoja sigue a la mano más pegada).")]
    public float wristStiffness = 20f;
    [Tooltip("Amortiguación de la muñeca (menos de 1 = la hoja latiguea un poco al frenar).")]
    public float wristDamping = 0.55f;
    [Tooltip("Giro máximo de la muñeca (grados).")]
    public float maxWristAngle = 100f;
    [Tooltip("Distancia mínima entre la hoja y el cuerpo o la cabeza (m).")]
    public float bladeClearance = 0.06f;
    [Tooltip("Rapidez con que la cabeza sigue su pose (más bajo = más retraso y suavidad).")]
    public float headFollow = 14f;
    private Quaternion _headPose = Quaternion.identity;
    private bool _headInit;

    [Header("Combo de espada")]
    [Tooltip("Cuánto baja la cadera al agacharse en el segundo tajo (m).")]
    public float crouchDepth = 0.09f;
    [Tooltip("Segundos para volver a la guardia si no llega el siguiente golpe.")]
    public float comboRecover = 0.32f;
    private WeaponGrip _grip = WeaponGrip.OneHand;
    private float _attackTime = 1f;
    private float _attackDuration;

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
        _hips = Find("Hips");
        _head = Find("Head");
        CacheBodyCapsule();
        _flipL = Find("Flipper_L");
        _flipR = Find("Flipper_R");
        _footL = Find("Foot_L");
        _footR = Find("Foot_R");
        Bone handL = Find("Anchor_Hand_L");
        Bone handR = Find("Anchor_Hand_R");
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Anchor_Hand") && !_handRest.ContainsKey(t)) _handRest[t] = t.localRotation;
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

    // Los derivados (PenguinLocomotionAnimator) pueden quedarse solo con la locomoción.
    protected virtual bool AllowsIdleActions => true;

    public virtual void PlayAction(IdleAction action)
    {
        if (action == IdleAction.None) return;
        _action = action;
        _actionTime = 0f;
        _actionSide = Random.value < 0.5f ? 1f : -1f;
    }

    // Pose de las aletas al llevar un arma (WeaponHolder la fija al equipar).
    public virtual void SetWeaponPose(bool armed, WeaponGrip grip, Vector3 tipRest)
    {
        _tipRest = tipRest.sqrMagnitude > 0.001f ? tipRest.normalized : Vector3.up;
        SetWeaponPose(armed, grip);
    }

    public virtual void SetWeaponPose(bool armed, WeaponGrip grip)
    {
        _armed = armed;
        _grip = grip;
    }

    // Golpe o disparo: la duración es la de un ataque completo.
    // Combo de espada estilo Prince of Persia: 1 tajo derecha→izquierda, 2 tajo izquierda→derecha agachado, 3 giro de 360°.
    public virtual void PlaySwordCombo(int step, float duration)
    {
        PlayAttack(duration);
        _comboStep = Mathf.Clamp(step, 1, 3);
    }

    public int ComboStep => _comboStep;

    public virtual void PlayAttack(float duration)
    {
        _comboStep = 0;
        _attackDuration = Mathf.Max(0.12f, duration);
        _attackTime = 0f;
        _action = IdleAction.None;
        _idleTime = 0f;
        _nextActionAt = idleActionDelay;
    }

    public bool IsAttacking => _attackTime < _attackDuration;

    // Capa extra de pose para los derivados. Ángulos en grados; extraCrouch en metros.
    protected virtual void ModifyPose(ref float swingL, ref float swingR, ref float outL, ref float outR,
        ref float spineYaw, ref float headPitch, ref float headRoll, ref float lean, ref float extraCrouch) { }

    protected static float SmoothStep01(float a, float b, float x) => Smooth(a, b, x);

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
            if (enableIdleActions && AllowsIdleActions && _action == IdleAction.None && _idleTime >= _nextActionAt)
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
        if (_attackTime < _attackDuration + (_comboStep > 0 ? comboRecover : 0f)) _attackTime += dt;
        else if (_comboStep > 0 && _attackTime >= _attackDuration + comboRecover) _comboStep = 0;

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

        // Combo de espada: se calcula primero porque el giro de 360° rota todo el pingüino.
        Combo combo = _comboStep > 0 ? ComboPose() : default;
        ApplySpin(combo.spin);

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
                    bool useLeft = _actionSide > 0f || _armed;
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

        if (_armed || IsAttacking) WeaponPose(ref swingL, ref swingR, ref outL, ref outR, ref spineYaw, ref headPitch);

        float lean = 0f;
        if (_comboStep > 0 && combo.weight > 0.001f)
        {
            float cw = combo.weight;
            // El torso sigue a la hoja (giro de cadera y hombros) y la cabeza mira al frente.
            float twist = Mathf.Clamp(Mathf.DeltaAngle(0f, combo.yaw) * 0.38f, -48f, 48f);
            spineYaw += (twist + combo.spineYaw) * cw;
            // La cabeza acompaña el tajo (mira un poco hacia donde va la hoja) y se inclina con el giro.
            headYaw += twist * 0.25f * cw;
            headRoll += -twist * 0.12f * cw;
            headPitch += combo.lean * 0.3f * cw;
            lean = combo.lean * cw;
            Quaternion spineD = Quaternion.AngleAxis(spineYaw, Vector3.up);
            Vector3 armTarget = Quaternion.Inverse(spineD) * Dir(combo.yaw, combo.armElevation);
            _comboBlade = Dir(combo.yaw, combo.elevation);
            _comboMotion = Dir(combo.nextYaw, combo.nextElevation) - _comboBlade;

            // Aleta del arma: apunta a la trayectoria del tajo (la mano va delante, la hoja la sigue con la muñeca).
            float side = _flipR != null ? Side(_flipR) : 1f;
            Quaternion baseR = Quaternion.AngleAxis(-swingR, Vector3.right) * Quaternion.AngleAxis(outR * side, Vector3.forward);
            Quaternion fullR = Quaternion.FromToRotation(baseR * _tipRest, armTarget);
            saluteR = Quaternion.Slerp(Quaternion.identity, fullR, cw);

            if (_grip == WeaponGrip.TwoHand)
                saluteL = Quaternion.Slerp(Quaternion.identity, fullR, cw * 0.65f);
            else if (_flipL != null)
            {
                // Aleta libre: contrapeso hacia el lado contrario de la hoja, extendida para equilibrar el giro.
                float sideL = Side(_flipL);
                Quaternion baseL = Quaternion.AngleAxis(-swingL, Vector3.right) * Quaternion.AngleAxis(outL * sideL, Vector3.forward);
                Vector3 restL = (_handRestL - _flipL.restPos).normalized;
                if (restL.sqrMagnitude < 0.5f) restL = new Vector3(sideL * 0.4f, -1f, 0f).normalized;
                // Hoja a la derecha → brazo libre atrás a la izquierda; hoja a la izquierda → brazo libre delante.
                float leftYaw = Mathf.Clamp(-90f - 0.4f * combo.yaw, -150f, -25f);
                Vector3 leftTarget = Quaternion.Inverse(spineD) * Dir(leftYaw, combo.leftElevation);
                Quaternion fullL = Quaternion.FromToRotation(baseL * restL, leftTarget);
                saluteL = Quaternion.Slerp(Quaternion.identity, fullL, cw * 0.75f);
            }
            _comboArm = armTarget;
            _comboArmWeight = cw;
        }
        else _comboArmWeight = 0f;

        float extraCrouch = 0f;
        ModifyPose(ref swingL, ref swingR, ref outL, ref outR, ref spineYaw, ref headPitch, ref headRoll, ref lean, ref extraCrouch);
        if (_hips != null)
            _hips.t.position = transform.TransformPoint(_hips.restPos + Vector3.down * (crouchDepth * combo.crouch * combo.weight + extraCrouch));

        Quaternion spineDelta = Quaternion.AngleAxis(spineYaw, Vector3.up);
        SetRot(_spine, spineDelta * Quaternion.Euler(lean, 0f, 0f));
        // La cabeza sigue su pose con un poco de retraso: los giros se ven más suaves y con peso.
        Quaternion headTarget = spineDelta * Quaternion.Euler(headPitch, headYaw, headRoll);
        float hk = Time.deltaTime > 0f && Application.isPlaying ? 1f - Mathf.Exp(-headFollow * Time.deltaTime) : 1f;
        _headPose = _headInit ? Quaternion.Slerp(_headPose, headTarget, hk) : headTarget;
        _headInit = true;
        SetRot(_head, _headPose);

        SetFlipper(_flipL, spineDelta, saluteL, outL, swingL);
        SetFlipper(_flipR, spineDelta, saluteR, outR, swingR);
        SetFoot(_footL, footOffL, pitchL);
        SetFoot(_footR, footOffR, pitchR);
        AvoidHand();
        ApplyWrist(spineYaw);
    }

    // Aletas al sujetar el arma y al atacar. Ángulos en grados: swing > 0 lleva la aleta hacia delante, out > 0 la abre.
    private void WeaponPose(ref float swingL, ref float swingR, ref float outL, ref float outR, ref float spineYaw, ref float headPitch)
    {
        float hold = _armed ? 1f : 0f;
        switch (_grip)
        {
            case WeaponGrip.OneHand:
            case WeaponGrip.Throwing:
                swingR = Mathf.Lerp(swingR, swingR * 0.4f + 30f, hold);
                outR += 10f * hold;
                break;
            case WeaponGrip.TwoHand:
                swingR = Mathf.Lerp(swingR, swingR * 0.3f + 50f, hold);
                swingL = Mathf.Lerp(swingL, swingL * 0.3f + 50f, hold);
                outR -= 6f * hold;
                outL -= 6f * hold;
                break;
            case WeaponGrip.Polearm:
                swingR = Mathf.Lerp(swingR, swingR * 0.4f + 35f, hold);
                swingL = Mathf.Lerp(swingL, swingL * 0.5f + 20f, hold);
                break;
            case WeaponGrip.Bow:
                swingL = Mathf.Lerp(swingL, swingL * 0.3f + 60f, hold);
                outL += 10f * hold;
                swingR = Mathf.Lerp(swingR, swingR * 0.5f + 25f, hold);
                break;
            case WeaponGrip.Crossbow:
                swingR = Mathf.Lerp(swingR, swingR * 0.2f + 65f, hold);
                swingL = Mathf.Lerp(swingL, swingL * 0.2f + 55f, hold);
                break;
        }

        if (!IsAttacking || _comboStep > 0) return;
        float t = Mathf.Clamp01(_attackTime / _attackDuration);
        float wind = Smooth(0f, 0.35f, t) * (1f - Smooth(0.35f, 0.55f, t));
        float strike = Smooth(0.35f, 0.55f, t) * (1f - Smooth(0.7f, 1f, t));
        WeaponGrip style = _armed ? _grip : WeaponGrip.OneHand;
        switch (style)
        {
            case WeaponGrip.TwoHand:
                swingR += 95f * wind - 20f * strike;
                swingL += 95f * wind - 20f * strike;
                headPitch += -6f * wind + 10f * strike;
                break;
            case WeaponGrip.Polearm:
                swingR += -25f * wind + 55f * strike;
                swingL += -15f * wind + 45f * strike;
                headPitch += 6f * strike;
                break;
            case WeaponGrip.Bow:
                float draw = Smooth(0f, 0.55f, t) * (1f - Smooth(0.6f, 0.7f, t));
                swingR += -45f * draw;
                outR += 20f * draw;
                break;
            case WeaponGrip.Crossbow:
                swingR -= 15f * strike;
                swingL -= 15f * strike;
                headPitch -= 4f * strike;
                break;
            case WeaponGrip.Throwing:
                swingR += -80f * wind + 110f * strike;
                outR += 25f * wind;
                spineYaw += -12f * wind + 18f * strike;
                break;
            default:
                swingR += -60f * wind + 75f * strike;
                outR += 35f * wind - 10f * strike;
                spineYaw += -14f * wind + 20f * strike;
                break;
        }
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

    // ---------- Combo de espada ----------

    private struct Combo
    {
        public float yaw, elevation, armElevation, nextYaw, nextElevation, weight, crouch, lean, spineYaw, spin, leftElevation;
    }

    // Trayectorias (tiempo 0–1, yaw, elevación de la hoja, elevación del brazo), sacadas de las curvas de animación del Príncipe.
    // La hoja nunca se detiene: entre tajos el brazo sube y la hoja queda horizontal detrás, lista para caer en el siguiente corte.
    private static readonly Vector4[] Path1 =
    {
        new Vector4(0f, 25f, 45f, 45f), new Vector4(0.16f, 80f, 48f, 38f), new Vector4(0.3f, 125f, 10f, 26f),
        new Vector4(0.42f, 45f, 2f, 12f), new Vector4(0.5f, -40f, -4f, -2f), new Vector4(0.6f, -110f, 4f, 8f),
        new Vector4(0.78f, -140f, 22f, 36f), new Vector4(1f, -150f, 16f, 42f)
    };

    private static readonly Vector4[] Path2 =
    {
        new Vector4(0f, -150f, 16f, 42f), new Vector4(0.14f, -140f, 10f, 30f), new Vector4(0.26f, -120f, -2f, 6f),
        new Vector4(0.38f, -25f, -10f, -8f), new Vector4(0.48f, 60f, -6f, -4f), new Vector4(0.58f, 118f, 2f, 4f),
        new Vector4(0.78f, 118f, 10f, 20f), new Vector4(1f, 100f, 12f, 16f)
    };

    // Giro: el brazo sube por encima de la cabeza y la hoja, horizontal, da la vuelta allí arriba;
    // al final baja en un corte horizontal a la altura del torso y vuelve a la guardia.
    private static readonly Vector4[] Path3 =
    {
        new Vector4(0f, 100f, 12f, 16f), new Vector4(0.1f, 70f, 28f, 55f), new Vector4(0.2f, 20f, 18f, 80f),
        new Vector4(0.35f, 100f, 14f, 84f), new Vector4(0.5f, 175f, 14f, 82f), new Vector4(0.62f, 130f, 26f, 62f),
        new Vector4(0.72f, 92f, 6f, 6f), new Vector4(0.8f, 85f, 4f, 4f),
        new Vector4(0.9f, 55f, 32f, 32f), new Vector4(1f, 25f, 48f, 48f)
    };

    // Catmull-Rom sobre (yaw, elevación de la hoja, elevación del brazo): curva suave que pasa por todas las claves.
    private static Vector3 Sample(Vector4[] keys, float t)
    {
        int n = keys.Length;
        if (t <= keys[0].x) return new Vector3(keys[0].y, keys[0].z, keys[0].w);
        if (t >= keys[n - 1].x) return new Vector3(keys[n - 1].y, keys[n - 1].z, keys[n - 1].w);
        int i = 0;
        while (i < n - 2 && t > keys[i + 1].x) i++;
        Vector4 k0 = keys[Mathf.Max(0, i - 1)], k1 = keys[i], k2 = keys[i + 1], k3 = keys[Mathf.Min(n - 1, i + 2)];
        float u = Mathf.InverseLerp(k1.x, k2.x, t);
        Vector3 a0 = new Vector3(k0.y, k0.z, k0.w), a1 = new Vector3(k1.y, k1.z, k1.w), a2 = new Vector3(k2.y, k2.z, k2.w), a3 = new Vector3(k3.y, k3.z, k3.w);
        float u2 = u * u, u3 = u2 * u;
        return 0.5f * (2f * a1 + (-a0 + a2) * u + (2f * a0 - 5f * a1 + 4f * a2 - a3) * u2 + (-a0 + 3f * a1 - 3f * a2 + a3) * u3);
    }

    // Dirección: yaw desde el frente (+ a la derecha) y elevación, en grados.
    private static Vector3 Dir(float yaw, float elevation)
    {
        float y = yaw * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(y) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(y) * Mathf.Cos(e));
    }

    private static float Cut(float a, float b, float x)
    {
        float t = Mathf.Clamp01(Mathf.InverseLerp(a, b, x));
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    private Combo ComboPose()
    {
        var c = new Combo();
        float d = Mathf.Max(0.01f, _attackDuration);
        float t = Mathf.Clamp01(_attackTime / d);
        // Si no llega el siguiente golpe, vuelve a la guardia.
        float recover = 1f - Smooth(d, d + comboRecover, _attackTime);
        Vector3 p;
        Vector4[] path;
        switch (_comboStep)
        {
            case 1:
                // Tajo de derecha a izquierda con el paso: la hoja sube sobre el hombro derecho y barre horizontal.
                path = Path1;
                p = Sample(Path1, t);
                c.weight = Smooth(0f, 0.14f, t) * recover;
                c.lean = 5f + 8f * Bump(0.3f, 0.75f, t);
                c.crouch = 0.2f + 0.3f * Bump(0.3f, 0.8f, t);
                c.leftElevation = -10f;
                break;
            case 2:
                // Arco inverso de izquierda a derecha: rodillas dobladas, centro de gravedad bajo.
                path = Path2;
                p = Sample(Path2, t);
                c.weight = recover;
                c.lean = 12f + 6f * Bump(0.25f, 0.7f, t);
                c.crouch = Mathf.Lerp(0.5f, 1f, Smooth(0f, 0.28f, t)) * (1f - 0.35f * Smooth(0.72f, 1f, t));
                c.leftElevation = -18f;
                break;
            default:
                // Giro completo con el brazo extendido a la altura del torso; la hoja remata subiendo a la guardia.
                path = Path3;
                p = Sample(Path3, t);
                float turn = Cut(0.1f, 0.8f, t);
                c.spin = 360f * turn;
                c.weight = recover;
                c.lean = 8f;
                float air = Mathf.Sin(Mathf.Clamp01((t - 0.12f) / 0.66f) * Mathf.PI);
                c.crouch = Mathf.Lerp(0.65f, 0.15f, air) + 0.55f * Bump(0.78f, 1f, t);
                c.spineYaw = -8f * air;
                c.leftElevation = 5f * air - 10f;
                break;
        }
        c.yaw = p.x;
        c.elevation = p.y;
        c.armElevation = p.z;
        // Un poco más adelante en la trayectoria: da la dirección del movimiento (el filo va por delante).
        Vector3 next = Sample(path, Mathf.Min(1f, t + 0.04f));
        c.nextYaw = next.x;
        c.nextElevation = next.y;
        return c;
    }

    // Muñeca: la hoja sigue su propia trayectoria (con un resorte: se retrasa en los cortes rápidos y latiguea al frenar)
    // y gira sobre su eje para que el filo vaya por delante del movimiento, como en los tajos del Príncipe.
    private void ApplyWrist(float spineYaw)
    {
        if (_wrist == null) return;
        _wrist.localRotation = _wristRestLocal;
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        if (_comboArmWeight <= 0.001f)
        {
            _bladeDir = Vector3.zero;
            _twist = 0f;
            AvoidBody();
            return;
        }
        Vector3 target = _comboBlade;
        if (_bladeDir.sqrMagnitude < 0.01f)
        {
            _bladeDir = target;
            _bladeVel = Vector3.zero;
        }
        float w = wristStiffness, z = wristDamping;
        Vector3 acc = w * w * (target - _bladeDir) - 2f * z * w * _bladeVel;
        _bladeVel += acc * dt;
        _bladeDir += _bladeVel * dt;
        Vector3 blade = _bladeDir.sqrMagnitude > 0.0001f ? _bladeDir.normalized : target;

        // 1) Punta hacia la trayectoria de la hoja.
        Vector3 currentTip = _wrist.rotation * _tipLocal;
        Vector3 desired = transform.rotation * blade;
        Quaternion q = Quaternion.FromToRotation(currentTip, desired);
        q = Quaternion.RotateTowards(Quaternion.identity, q, maxWristAngle);
        _wrist.rotation = Quaternion.Slerp(Quaternion.identity, q, _comboArmWeight) * _wrist.rotation;

        // 2) Filo hacia donde se mueve la hoja (no de plano).
        Vector3 tip = _wrist.rotation * _tipLocal;
        Vector3 edge = Vector3.ProjectOnPlane(_wrist.rotation * _edgeLocal, tip);
        Vector3 motion = Vector3.ProjectOnPlane(transform.rotation * _comboMotion, tip);
        float target01 = Mathf.Clamp01(motion.magnitude / 0.04f);
        float angle = 0f;
        if (edge.sqrMagnitude > 0.0001f && motion.sqrMagnitude > 0.000001f)
        {
            angle = Vector3.SignedAngle(edge, motion, tip);
            // Los dos filos cortan: se gira hacia el más cercano para no dar vueltas de más.
            if (angle > 90f) angle -= 180f;
            else if (angle < -90f) angle += 180f;
        }
        _twist = Mathf.LerpAngle(_twist, angle * target01, 1f - Mathf.Exp(-16f * dt));
        _wrist.rotation = Quaternion.AngleAxis(_twist * _comboArmWeight, tip) * _wrist.rotation;
        AvoidBody();
    }

    // ---------- La hoja no atraviesa al pingüino ----------

    private Vector3 _bodyCenterLocal;
    private float _bodyRadius, _bodyHalf, _bladeLength;
    private bool _hasBody;

    public void SetBladeLength(float meters) => _bladeLength = meters;

    // Cápsula del cuerpo (cabeza incluida) tomada del modelo en reposo, relativa a la cadera para que siga al agacharse.
    private static readonly string[] IgnoredParts = { "Flipper", "Foot", "Gauntlet", "Sleeve", "Wraps", "Weapon", "Offhand", "Cape", "Cloak", "Kilt", "Coat" };

    private void CacheBodyCapsule() => RefreshBodyCapsule();

    // Cuerpo + ropa rígida (casco, peto, hombreras): la hoja pasa rozando por fuera de todo eso.
    // Equipment la vuelve a calcular al cambiar de ropa.
    public void RefreshBodyCapsule()
    {
        _hasBody = false;
        if (_hips == null) return;
        Bounds b = default;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            bool body = r.name.StartsWith("Penguin_Body");
            bool gear = r is SkinnedMeshRenderer && r.name.StartsWith("Outfit_");
            if (!body && !gear) continue;
            bool skip = false;
            foreach (string p in IgnoredParts) if (gear && r.name.Contains(p)) { skip = true; break; }
            if (skip) continue;
            if (!_hasBody) { b = r.bounds; _hasBody = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!_hasBody) return;
        _bodyCenterLocal = _hips.t.InverseTransformPoint(b.center);
        // Radio según la parte más ancha (hombreras incluidas): la hoja pasa cerca pero por fuera.
        _bodyRadius = Mathf.Max(b.extents.x, b.extents.z) * 0.82f;
        _bodyHalf = Mathf.Max(0f, b.extents.y - _bodyRadius);
    }

    // Si la hoja entra en el cuerpo o en la cabeza, la muñeca la gira lo justo para que pase rozando por fuera.
    private void AvoidBody()
    {
        if (_wrist == null || !_hasBody || _bladeLength <= 0.05f) return;
        Vector3 up = transform.up;
        Vector3 c = _hips.t.TransformPoint(_bodyCenterLocal);
        Vector3 a = c - up * _bodyHalf, b = c + up * _bodyHalf;
        float r = _bodyRadius + bladeClearance;
        Vector3 hand = _wrist.position;
        for (int it = 0; it < 4; it++)
        {
            Vector3 dir = _wrist.rotation * _tipLocal;
            Vector3 p0 = hand + dir * (_bladeLength * 0.2f);
            Vector3 p1 = hand + dir * _bladeLength;
            ClosestPoints(p0, p1, a, b, out Vector3 onBlade, out Vector3 onBody);
            Vector3 away = onBlade - onBody;
            float d = away.magnitude;
            if (d >= r) break;
            if (d < 0.0001f) away = transform.right;
            Vector3 target = onBody + away.normalized * r;
            _wrist.rotation = Quaternion.FromToRotation(onBlade - hand, target - hand) * _wrist.rotation;
        }
    }

    // La mano del arma tampoco entra en el cuerpo ni en la cabeza: la aleta se abre lo justo.
    private void AvoidHand()
    {
        if (_wrist == null || _flipR == null || !_hasBody || _comboArmWeight <= 0.001f) return;
        Vector3 up = transform.up;
        Vector3 c = _hips.t.TransformPoint(_bodyCenterLocal);
        Vector3 a = c - up * _bodyHalf, b = c + up * _bodyHalf;
        float r = _bodyRadius + bladeClearance * 1.5f;
        Vector3 pivot = _flipR.t.position;
        for (int it = 0; it < 3; it++)
        {
            Vector3 hand = _wrist.position;
            ClosestPoints(hand, hand, a, b, out _, out Vector3 onBody);
            Vector3 away = hand - onBody;
            float d = away.magnitude;
            if (d >= r) break;
            if (d < 0.0001f) away = transform.right;
            Vector3 target = onBody + away.normalized * r;
            Quaternion q = Quaternion.FromToRotation(hand - pivot, target - pivot);
            _flipR.t.rotation = Quaternion.Slerp(Quaternion.identity, q, _comboArmWeight) * _flipR.t.rotation;
        }
    }

    // Puntos más cercanos entre los segmentos p1-q1 y p2-q2.
    private static void ClosestPoints(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
    {
        Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        float s, t;
        if (a <= 1e-6f && e <= 1e-6f) { c1 = p1; c2 = p2; return; }
        if (a <= 1e-6f) { s = 0f; t = Mathf.Clamp01(f / e); }
        else
        {
            float cc = Vector3.Dot(d1, r);
            if (e <= 1e-6f) { t = 0f; s = Mathf.Clamp01(-cc / a); }
            else
            {
                float bb = Vector3.Dot(d1, d2);
                float denom = a * e - bb * bb;
                s = denom > 1e-6f ? Mathf.Clamp01((bb * f - cc * e) / denom) : 0f;
                t = (bb * s + f) / e;
                if (t < 0f) { t = 0f; s = Mathf.Clamp01(-cc / a); }
                else if (t > 1f) { t = 1f; s = Mathf.Clamp01((bb - cc) / a); }
            }
        }
        c1 = p1 + d1 * s;
        c2 = p2 + d2 * t;
    }

    // La mano del arma: WeaponHolder la registra para girar la muñeca.
    public void SetWrist(Transform hand, Vector3 tipInHand, Vector3 edgeInHand)
    {
        _edgeLocal = edgeInHand.sqrMagnitude > 0.001f ? edgeInHand.normalized : Vector3.right;
        SetWrist(hand, tipInHand);
    }

    public void SetWrist(Transform hand, Vector3 tipInHand)
    {
        if (_wrist != null && _wrist != hand) _wrist.localRotation = _wristRestLocal;
        _wrist = hand;
        _tipLocal = tipInHand.sqrMagnitude > 0.001f ? tipInHand.normalized : Vector3.up;
        // Siempre la rotación de reposo guardada al iniciar: si se tomara la actual, un arma re-equipada
        // (al romperse o repararse) heredaría el giro de muñeca de ese momento y quedaría torcida.
        if (hand != null)
        {
            Init();
            if (!_handRest.TryGetValue(hand, out Quaternion rest))
            {
                rest = hand.localRotation;
                _handRest[hand] = rest;
            }
            _wristRestLocal = rest;
            hand.localRotation = rest;
        }
    }

    // Gira el modelo del pingüino sobre su eje (el giro de 360°). Otros scripts pueden tocar la rotación local
    // del modelo cada frame (PenguinAnimator); si nadie la tocó, se quita el giro que pusimos el frame anterior.
    private void ApplySpin(float yaw)
    {
        if (Mathf.Abs(yaw) < 0.01f && !_spinning) return;
        Quaternion current = transform.localRotation;
        if (_spinning && Quaternion.Angle(current, _lastWritten) < 0.01f) current *= Quaternion.Inverse(_spinApplied);
        _spinApplied = Quaternion.Euler(0f, yaw, 0f);
        transform.localRotation = current * _spinApplied;
        _lastWritten = transform.localRotation;
        _spinning = Mathf.Abs(yaw) >= 0.01f;
        if (!_spinning) _spinApplied = Quaternion.identity;
    }

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
