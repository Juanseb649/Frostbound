using UnityEngine;

// Pone el modelo del arma en la aleta del pingüino (Anchor_Hand_R / Anchor_Hand_L) con la orientación
// de su tipo de agarre, y cambia la pose de las aletas. El arma sigue la animación de la aleta.
// Las orientaciones se definen en el espacio del pingüino (+Z adelante, +Y arriba) con el esqueleto en reposo.
public class WeaponHolder : MonoBehaviour
{
    public struct GripPose
    {
        public Vector3 offset;
        [Tooltip("Orientación del arma (su +Y es la punta) en el espacio del pingüino, ya compensada con la pose de la aleta armada.")]
        public Vector3 euler;
        [Tooltip("Giro sobre su propio eje largo.")]
        public float spin;

        public GripPose(float x, float y, float z, float pitch, float yaw, float roll, float spin = 0f)
        {
            offset = new Vector3(x, y, z);
            euler = new Vector3(pitch, yaw, roll);
            this.spin = spin;
        }
    }

    public string rightAnchor = "Anchor_Hand_R";
    public string leftAnchor = "Anchor_Hand_L";

    // La aleta armada ya va inclinada hacia delante (PenguinRigAnimator.WeaponPose), y el arma se inclina hacia atrás
    // con ella: por eso el cabeceo de cada agarre suma esa inclinación.
    // Una mano: hoja hacia delante y arriba, de canto (el filo mira al frente y la cara plana a los lados),
    // como se sujeta una espada en guardia. La curva de las cimitarras queda hacia atrás.
    private static readonly GripPose OneHandGrip = new GripPose(0f, 0.02f, 0.02f, 68f, 0f, -6f, -90f);
    private static readonly GripPose TwoHandGrip = new GripPose(0f, 0.03f, 0.03f, 82f, 0f, 38f);
    private static readonly GripPose PolearmGrip = new GripPose(0f, 0.02f, 0.03f, 50f, 0f, 4f);
    private static readonly GripPose BowGrip = new GripPose(0f, 0.02f, 0.02f, 62f, 0f, 0f);
    private static readonly GripPose CrossbowGrip = new GripPose(0f, 0.02f, 0.04f, 155f, 0f, 0f, 180f);
    private static readonly GripPose ThrowingGrip = new GripPose(0f, 0.02f, 0.02f, 68f, 0f, -6f, -90f);
    private static readonly GripPose OffhandWeaponGrip = new GripPose(0f, 0.02f, 0.02f, 35f, 0f, -8f, 90f);
    private static readonly GripPose ShieldGrip = new GripPose(0.06f, 0.05f, 0.02f, 0f, 90f, 0f);

    public WeaponGrip CurrentGrip { get; private set; } = WeaponGrip.OneHand;
    public bool Armed { get; private set; }
    public GameObject MainModel { get; private set; }
    // Hoja del arma como cápsula (solo para que la tela de la ropa la esquive; no choca con nada más).
    public CapsuleCollider BladeCollider { get; private set; }
    public float BladeLength { get; private set; }
    public GameObject OffhandModel { get; private set; }

    private Transform _handR, _handL;
    private Vector3 _lastTipRest = Vector3.up;
    private TrailRenderer _trail;
    private static Material _trailMaterial;
    private Matrix4x4 _restR, _restL;
    private bool _ready;
    private PenguinRigAnimator _rig;

    void Awake() => Init();

    private void Init()
    {
        if (_ready) return;
        _rig = GetComponent<PenguinRigAnimator>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == rightAnchor && _handR == null) _handR = t;
            if (t.name == leftAnchor && _handL == null) _handL = t;
        }
        // Pose de reposo de cada anclaje respecto al pingüino (antes de que la animación mueva nada).
        if (_handR != null) _restR = transform.worldToLocalMatrix * _handR.localToWorldMatrix;
        if (_handL != null) _restL = transform.worldToLocalMatrix * _handL.localToWorldMatrix;
        _ready = true;
    }

    public Transform RightHand { get { Init(); return _handR; } }
    public Transform LeftHand { get { Init(); return _handL; } }

    // Punto del mundo desde donde salen los proyectiles.
    public Vector3 MuzzlePosition
    {
        get
        {
            Init();
            Transform hand = CurrentGrip == WeaponGrip.Bow ? _handL : _handR;
            return hand != null ? hand.position : transform.position + Vector3.up * 0.7f;
        }
    }

    private ItemDefinition _shownMain, _shownOffhand;

    public void Show(ItemDefinition main, ItemDefinition offhand)
    {
        Init();
        // Mismas armas (por ejemplo, al romperse o repararse): no se vuelven a colocar.
        if (main == _shownMain && offhand == _shownOffhand && (main == null || MainModel != null)) return;
        _shownMain = main;
        _shownOffhand = offhand;
        Clear();

        Armed = main != null && main.weaponModel != null;
        CurrentGrip = Armed ? main.WeaponInfo.grip : WeaponGrip.OneHand;

        if (Armed)
        {
            bool left = CurrentGrip == WeaponGrip.Bow;
            MainModel = Attach(main.weaponModel, left, Pose(CurrentGrip), main.name);
        }

        if (offhand != null && offhand.weaponModel != null && (main == null || !main.UsesBothHands))
        {
            GripPose pose = offhand.IsWeapon ? OffhandWeaponGrip : ShieldGrip;
            OffhandModel = Attach(offhand.weaponModel, true, pose, offhand.name);
        }

        if (_rig != null)
        {
            _rig.SetWeaponPose(Armed, CurrentGrip, _lastTipRest);
            bool wrist = Armed && MainModel != null && CurrentGrip != WeaponGrip.Bow;
            _rig.SetWrist(wrist ? _handR : null,
                wrist ? MainModel.transform.localRotation * Vector3.up : Vector3.up,
                wrist ? MainModel.transform.localRotation * Vector3.right : Vector3.right);
            _rig.SetBladeLength(BladeLength);
        }
        if (MainModel != null && main.IsWeapon && !main.IsRanged) AddTrail(MainModel);
        BladeCollider = null;
        BladeLength = 0f;
        if (MainModel != null) AddBlade(MainModel);
    }

    // Estela del filo durante los tajos (se ve el arco del corte, como en Prince of Persia).
    public void SetTrail(bool on)
    {
        if (_trail == null) return;
        if (on && !_trail.emitting) _trail.Clear();
        _trail.emitting = on;
    }

    private void AddTrail(GameObject weapon)
    {
        float top = 0f;
        foreach (MeshFilter mf in weapon.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null) top = Mathf.Max(top, mf.sharedMesh.bounds.max.y);
        if (top < 0.15f) return;
        if (_trailMaterial == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) return;
            _trailMaterial = new Material(sh);
        }
        var tip = new GameObject("Estela");
        tip.transform.SetParent(weapon.transform, false);
        tip.transform.localPosition = new Vector3(0f, top * 0.62f, 0f);
        _trail = tip.AddComponent<TrailRenderer>();
        _trail.sharedMaterial = _trailMaterial;
        _trail.time = 0.16f;
        _trail.minVertexDistance = 0.02f;
        _trail.widthMultiplier = top * 0.7f;
        _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f));
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(0.9f, 0.97f, 1f), 0f), new GradientColorKey(new Color(0.6f, 0.85f, 1f), 1f) },
                  new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
        _trail.colorGradient = g;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.emitting = false;
    }

    private void AddBlade(GameObject weapon)
    {
        float top = 0f;
        foreach (MeshFilter mf in weapon.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null) top = Mathf.Max(top, mf.sharedMesh.bounds.max.y);
        if (top < 0.1f) return;
        BladeLength = top * weapon.transform.lossyScale.y;
        var go = new GameObject("Hoja_Colision");
        go.transform.SetParent(weapon.transform, false);
        go.layer = 2;
        var cap = go.AddComponent<CapsuleCollider>();
        cap.isTrigger = true;
        cap.direction = 1;
        cap.radius = 0.04f;
        cap.height = top;
        cap.center = new Vector3(0f, top * 0.5f, 0f);
        BladeCollider = cap;
    }

    public void Clear()
    {
        _trail = null;
        if (MainModel != null) DestroySafe(MainModel);
        if (OffhandModel != null) DestroySafe(OffhandModel);
        MainModel = OffhandModel = null;
    }

    private GripPose Pose(WeaponGrip g)
    {
        switch (g)
        {
            case WeaponGrip.TwoHand: return TwoHandGrip;
            case WeaponGrip.Polearm: return PolearmGrip;
            case WeaponGrip.Bow: return BowGrip;
            case WeaponGrip.Crossbow: return CrossbowGrip;
            case WeaponGrip.Throwing: return ThrowingGrip;
            case WeaponGrip.Offhand: return OffhandWeaponGrip;
            default: return OneHandGrip;
        }
    }

    private GameObject Attach(GameObject model, bool left, GripPose pose, string itemName)
    {
        Transform hand = left ? _handL : _handR;
        if (hand == null || model == null) return null;
        Matrix4x4 rest = left ? _restL : _restR;

        // El lado se deduce de la posición del anclaje: los ángulos de balanceo lateral se reflejan en la aleta izquierda.
        float side = rest.GetColumn(3).x >= 0f ? 1f : -1f;
        Vector3 offset = new Vector3(pose.offset.x * side, pose.offset.y, pose.offset.z);
        Vector3 euler = new Vector3(pose.euler.x, pose.euler.y * side, pose.euler.z * side);

        Vector3 restPos = rest.GetColumn(3);
        Quaternion rot = Quaternion.Euler(euler) * Quaternion.Euler(0f, pose.spin, 0f);
        if (!left || CurrentGrip == WeaponGrip.Bow) _lastTipRest = rot * Vector3.up;
        Matrix4x4 desired = Matrix4x4.TRS(restPos + offset, rot, Vector3.one);
        Matrix4x4 local = rest.inverse * desired;

        GameObject go = Instantiate(model, hand, false);
        go.name = "Arma_" + itemName;
        go.transform.localPosition = local.GetColumn(3);
        go.transform.localRotation = local.rotation;
        Vector3 s = rest.lossyScale;
        go.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, s.x), 1f / Mathf.Max(0.0001f, s.y), 1f / Mathf.Max(0.0001f, s.z));
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) DestroySafe(c);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = gameObject.layer;
        return go;
    }

    private static void DestroySafe(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
