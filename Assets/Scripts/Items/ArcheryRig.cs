using System.Collections.Generic;
using UnityEngine;

// Lo que se ve al disparar con arco: el arco se coloca vertical frente al blanco, la cuerda (una línea entre las palas)
// se estira con la aleta que tensa y, al soltar, vibra con un resorte; la flecha va encajada en la cuerda y desaparece
// al salir disparada. La usan el héroe y los arqueros corruptos (va junto al PenguinRigAnimator).
[DefaultExecutionOrder(230)]
public class ArcheryRig : MonoBehaviour
{
    [Tooltip("Inclinación lateral del arco al apuntar (grados).")]
    public float cant = 12f;
    [Tooltip("Cuánto se puede tensar la cuerda (m, a escala del arco).")]
    public float maxDraw = 0.32f;
    public float stringStiffness = 900f;
    [Range(0f, 1f)] public float stringDamping = 0.1f;
    public Color stringColor = new Color(0.85f, 0.82f, 0.74f);

    private PenguinRigAnimator _rig;
    private Transform _bow, _handR;
    private Quaternion _bowRestLocal;
    private Vector3 _topLocal, _botLocal;
    private LineRenderer _string;
    private Transform _arrow;
    private float _arrowBack, _arrowFront;
    private Vector3 _offset, _offsetVel, _lastOffset;
    private bool _wasNocked;
    private static Material _stringMaterial;
    private static readonly Dictionary<Mesh, (Mesh mesh, int stringIndex, Vector3 top, Vector3 bottom)> Stripped = new Dictionary<Mesh, (Mesh, int, Vector3, Vector3)>();

    public bool Ready => _bow != null;
    public Transform Arrow => _arrow;
    public Vector3 LaunchPoint { get; private set; }
    public Vector3 LaunchDirection { get; private set; }
    public float ArrowLength => _arrowFront + _arrowBack;

    public string DebugState() => "top " + _topLocal.ToString("F3") + " bot " + _botLocal.ToString("F3") + " off " + _offset.ToString("F3") + " vel " + _offsetVel.ToString("F2")
        + " arrow " + _arrowBack.ToString("F2") + "/" + _arrowFront.ToString("F2") + " mano " + (_handR != null ? _handR.position.ToString("F2") : "-") + " arco " + (_bow != null ? _bow.position.ToString("F2") : "-");

    public void Setup(Transform bowModel, GameObject arrowPrefab, Transform rightHand, Color? tint = null)
    {
        Clear();
        _rig = GetComponent<PenguinRigAnimator>();
        _bow = bowModel;
        _handR = rightHand;
        if (_bow == null) return;
        _bowRestLocal = _bow.localRotation;
        if (tint.HasValue) stringColor = tint.Value;

        // La cuerda del modelo se quita (se dibuja aparte para poder tensarla).
        bool found = false;
        foreach (MeshFilter mf in _bow.GetComponentsInChildren<MeshFilter>(true))
        {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null) continue;
            var data = Strip(mf.sharedMesh, mr.sharedMaterials);
            if (data.stringIndex < 0) continue;
            var mats = new List<Material>(mr.sharedMaterials);
            if (data.stringIndex < mats.Count) mats.RemoveAt(data.stringIndex);
            mf.sharedMesh = data.mesh;
            mr.sharedMaterials = mats.ToArray();
            _topLocal = _bow.InverseTransformPoint(mf.transform.TransformPoint(data.top));
            _botLocal = _bow.InverseTransformPoint(mf.transform.TransformPoint(data.bottom));
            found = true;
        }
        if (!found)
        {
            Bounds b = new Bounds();
            foreach (MeshFilter mf in _bow.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) b = mf.sharedMesh.bounds;
            _topLocal = new Vector3(0f, b.max.y, b.max.z);
            _botLocal = new Vector3(0f, b.min.y, b.max.z);
        }

        if (_stringMaterial == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null) _stringMaterial = new Material(sh);
        }
        var sgo = new GameObject("Cuerda");
        sgo.transform.SetParent(transform, false);
        _string = sgo.AddComponent<LineRenderer>();
        _string.sharedMaterial = _stringMaterial;
        _string.positionCount = 3;
        _string.useWorldSpace = true;
        _string.widthMultiplier = 0.009f * _bow.lossyScale.y;
        _string.startColor = _string.endColor = stringColor;
        _string.numCapVertices = 1;
        _string.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (arrowPrefab != null)
        {
            GameObject a = Instantiate(arrowPrefab, transform);
            a.name = "Flecha_Encajada";
            foreach (Collider c in a.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Rigidbody rb in a.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            foreach (MonoBehaviour m in a.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(m);
            _arrow = a.transform;
            float ps = Mathf.Max(0.0001f, transform.lossyScale.y);
            _arrow.localScale = Vector3.one / ps;
            Bounds ab = new Bounds();
            bool any = false;
            foreach (MeshFilter mf in a.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) { ab = any ? Enc(ab, mf.sharedMesh.bounds) : mf.sharedMesh.bounds; any = true; }
            float s = a.transform.lossyScale.y;
            _arrowBack = any ? -ab.min.y * s : 0.35f * s;
            _arrowFront = any ? ab.max.y * s : 0.38f * s;
            a.SetActive(false);
        }
        _offset = _offsetVel = _lastOffset = Vector3.zero;
        if (_rig != null) _rig.BowReleased += OnReleased;
    }

    private static Bounds Enc(Bounds a, Bounds b) { a.Encapsulate(b); return a; }

    public void Clear()
    {
        if (_rig != null) _rig.BowReleased -= OnReleased;
        if (_string != null) Destroy(_string.gameObject);
        if (_arrow != null) Destroy(_arrow.gameObject);
        _string = null;
        _arrow = null;
        _bow = null;
    }

    void OnDestroy() => Clear();

    // Copia de la malla sin la parte de la cuerda (y los extremos de la cuerda, que son las puntas de las palas).
    private static (Mesh mesh, int stringIndex, Vector3 top, Vector3 bottom) Strip(Mesh source, Material[] mats)
    {
        if (Stripped.TryGetValue(source, out var cached)) return cached;
        if (!source.isReadable) return (source, -1, Vector3.zero, Vector3.zero);
        int index = -1;
        for (int i = 0; i < mats.Length && i < source.subMeshCount; i++)
            if (mats[i] != null && mats[i].name.Contains("String")) { index = i; break; }
        if (index < 0) return (source, -1, Vector3.zero, Vector3.zero);
        Vector3[] v = source.vertices;
        int[] tris = source.GetTriangles(index);
        if (tris.Length == 0) return (source, -1, Vector3.zero, Vector3.zero);
        Vector3 top = new Vector3(0f, float.MinValue, 0f), bottom = new Vector3(0f, float.MaxValue, 0f);
        foreach (int t in tris)
        {
            if (v[t].y > top.y) top = v[t];
            if (v[t].y < bottom.y) bottom = v[t];
        }
        Mesh copy = Object.Instantiate(source);
        copy.name = source.name + "_SinCuerda";
        var keep = new List<int[]>();
        for (int i = 0; i < source.subMeshCount; i++) if (i != index) keep.Add(source.GetTriangles(i));
        copy.subMeshCount = keep.Count;
        for (int i = 0; i < keep.Count; i++) copy.SetTriangles(keep[i], i);
        var result = (copy, index, top, bottom);
        Stripped[source] = result;
        return result;
    }

    private void OnReleased()
    {
        if (_arrow != null) _arrow.gameObject.SetActive(false);
        // La cuerda sale con la velocidad que llevaba la mano: vibra hacia delante y vuelve.
        _offsetVel = -_offset * 28f;
    }

    void LateUpdate()
    {
        if (_bow == null || _rig == null) return;
        float dt = Time.deltaTime;
        float aim = _rig.BowAimWeight;
        Vector3 fwd = _rig.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();

        // El arco, vertical y algo inclinado, con la cuerda hacia el arquero.
        Quaternion rest = _bow.parent != null ? _bow.parent.rotation * _bowRestLocal : _bowRestLocal;
        Vector3 up = Quaternion.AngleAxis(-cant, fwd) * Vector3.up;
        Quaternion aimRot = Quaternion.LookRotation(-fwd, up);
        _bow.rotation = Quaternion.Slerp(rest, aimRot, aim);

        Vector3 top = _bow.TransformPoint(_topLocal), bot = _bow.TransformPoint(_botLocal);
        Vector3 mid = (top + bot) * 0.5f;
        Vector3 grip = _bow.position;
        float scale = _bow.lossyScale.y;

        if (_rig.BowNocked && _handR != null)
        {
            // La aleta lleva la cuerda: se tensa lo que la mano se aleja hacia atrás sobre la línea de tiro.
            Vector3 back = (mid - grip).sqrMagnitude > 1e-6f ? (mid - grip).normalized : -fwd;
            Vector3 toHand = _handR.position - mid;
            float pull = Mathf.Clamp(Vector3.Dot(toHand, back), 0f, maxDraw * scale);
            pull = Mathf.Max(pull, maxDraw * scale * _rig.BowDraw01 * 0.85f);
            Vector3 onLine = mid + back * pull;
            Vector3 target = Vector3.Lerp(onLine, _handR.position, 0.22f);
            Vector3 newOffset = target - mid;
            _offsetVel = dt > 0f ? (newOffset - _lastOffset) / dt : Vector3.zero;
            _offset = newOffset;
            if (_arrow != null && !_arrow.gameObject.activeSelf) _arrow.gameObject.SetActive(true);
        }
        else if (dt > 0f)
        {
            // Cuerda suelta: resorte amortiguado (vibra al soltar la flecha).
            float k = stringStiffness, c = 2f * stringDamping * Mathf.Sqrt(k);
            Vector3 acc = -k * _offset - c * _offsetVel;
            _offsetVel += acc * dt;
            _offset += _offsetVel * dt;
            if (_arrow != null && _arrow.gameObject.activeSelf && !_rig.BowNocked) _arrow.gameObject.SetActive(false);
        }
        _lastOffset = _offset;
        Vector3 nock = mid + _offset;

        if (_string != null)
        {
            _string.SetPosition(0, top);
            _string.SetPosition(1, nock);
            _string.SetPosition(2, bot);
        }

        // Flecha: la cola en la cuerda, apoyada en el arco, apuntando al blanco.
        Vector3 dir = grip - nock;
        if (dir.sqrMagnitude < 0.0025f) dir = fwd;
        dir.Normalize();
        dir = Vector3.Slerp(dir, fwd, 0.35f).normalized;
        if (_arrow != null && _arrow.gameObject.activeSelf)
        {
            _arrow.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            _arrow.position = nock + dir * _arrowBack;
        }
        if (_rig.BowNocked || _arrow == null || LaunchPoint == Vector3.zero)
        {
            LaunchPoint = nock + dir * (_arrowBack + _arrowFront);
            LaunchDirection = dir;
        }
    }
}
