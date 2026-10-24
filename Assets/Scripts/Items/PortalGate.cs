using System.Collections.Generic;
using UnityEngine;

// Portal azul de pie (pergamino de regreso). Lo crea TownPortal; al entrar el héroe, TownPortal decide a dónde va.
// Se construye en código: un óvalo translúcido que late, anillos que giran y una luz.
public class PortalGate : MonoBehaviour
{
    public static readonly Color Blue = new Color(0.35f, 0.65f, 1f);

    private const float Width = 1.3f;
    private const float Height = 2.4f;
    private const int Segments = 40;

    private static readonly List<PortalGate> All = new List<PortalGate>();

    public bool InVillage { get; private set; }

    private readonly List<LineRenderer> _rings = new List<LineRenderer>();
    private Material _fillMat;
    private Light _light;
    private float _readyAt;
    private bool _used;
    private float _born;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public static PortalGate Create(Vector3 position, float yaw, bool inVillage)
    {
        var go = new GameObject(inVillage ? "Portal_Regreso" : "Portal_Poblado");
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        PortalGate g = go.AddComponent<PortalGate>();
        g.InVillage = inVillage;
        g.Build();
        return g;
    }

    public static void CloseAll()
    {
        for (int i = All.Count - 1; i >= 0; i--)
            if (All[i] != null) Destroy(All[i].gameObject);
    }

    private void Build()
    {
        _born = Time.time;
        _readyAt = Time.time + 1.2f;
        Shader sh = Shader.Find("Sprites/Default");

        // Relleno: un óvalo vertical translúcido (abanico de triángulos, se ve por las dos caras).
        var fill = new GameObject("Velo");
        fill.transform.SetParent(transform, false);
        fill.transform.localPosition = Vector3.up * (Height * 0.5f + 0.05f);
        var verts = new Vector3[Segments + 1];
        var colors = new Color[Segments + 1];
        var tris = new int[Segments * 3];
        verts[0] = Vector3.zero;
        colors[0] = new Color(0.75f, 0.9f, 1f, 0.85f);
        for (int i = 0; i < Segments; i++)
        {
            float a = i / (float)Segments * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Sin(a) * Width * 0.5f, Mathf.Cos(a) * Height * 0.5f, 0f);
            colors[i + 1] = new Color(Blue.r, Blue.g, Blue.b, 0.35f);
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 1 < Segments ? i + 2 : 1;
        }
        var mesh = new Mesh { name = "Velo_Portal", vertices = verts, colors = colors, triangles = tris };
        mesh.RecalculateBounds();
        fill.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = fill.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        if (sh != null)
        {
            _fillMat = new Material(sh);
            mr.sharedMaterial = _fillMat;
        }

        // Anillos: el borde y dos óvalos interiores que giran en sentidos opuestos.
        for (int r = 0; r < 3; r++)
        {
            var go = new GameObject("Anillo_" + r);
            go.transform.SetParent(fill.transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            if (sh != null) lr.sharedMaterial = new Material(sh);
            lr.loop = true;
            lr.useWorldSpace = false;
            lr.positionCount = Segments;
            lr.widthMultiplier = r == 0 ? 0.09f : 0.04f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Color c = r == 0 ? new Color(0.8f, 0.93f, 1f, 1f) : new Color(Blue.r, Blue.g, Blue.b, 0.8f);
            lr.startColor = lr.endColor = c;
            _rings.Add(lr);
        }

        var lightGo = new GameObject("Luz");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.3f, 0.6f);
        _light = lightGo.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = Blue;
        _light.range = 6f;
        _light.intensity = 3f;
        _light.shadows = LightShadows.None;

        var trigger = gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.2f, 0f);
        trigger.size = new Vector3(Width, 2.4f, 0.9f);
    }

    void Update()
    {
        float t = Time.time - _born;
        float grow = Mathf.Lerp(0.05f, 1f, Mathf.Clamp01(t / 0.6f));
        transform.localScale = new Vector3(grow, Mathf.Lerp(0.2f, 1f, grow), 1f);
        for (int r = 0; r < _rings.Count; r++)
        {
            float k = r == 0 ? 1f : r == 1 ? 0.72f : 0.45f;
            float spin = r == 0 ? 0f : (r == 1 ? 1.6f : -2.3f) * t;
            float wob = r == 0 ? 0.02f : 0.06f;
            LineRenderer lr = _rings[r];
            for (int i = 0; i < Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                float w = 1f + Mathf.Sin(a * 5f + spin * 2f) * wob;
                lr.SetPosition(i, new Vector3(Mathf.Sin(a + spin) * Width * 0.5f * k * w, Mathf.Cos(a + spin) * Height * 0.5f * k * w, 0f));
            }
        }
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f);
        if (_light != null) _light.intensity = 2.5f + pulse * 1.5f;
        if (_fillMat != null) _fillMat.color = new Color(1f, 1f, 1f, 0.75f + pulse * 0.25f);
    }

    void OnTriggerEnter(Collider other) => TryEnter(other);
    void OnTriggerStay(Collider other) => TryEnter(other);

    private void TryEnter(Collider other)
    {
        if (_used || Time.time < _readyAt) return;
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;
        CharacterStats st = pc.GetComponent<CharacterStats>();
        if (st != null && st.IsDead) return;
        _used = true;
        AudioManager.Play3D("SFX/Portal_Open", transform.position, 0.8f, 0f, 3f, 30f, 0f);
        TownPortal.Enter(this);
    }
}
