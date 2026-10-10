using System.Collections.Generic;
using UnityEngine;

// Decorado 3D del menú: las ruinas del Frostspire de noche. Pedestal de tres escalones con luces en el borde,
// antorchas, columnas rotas, un arco, cristales de hielo que brillan, el cristal del Frost flotando, la luna,
// nieve cayendo y bruma. El héroe elegido aparece sobre el pedestal.
public class MenuRuins : MonoBehaviour
{
    public static readonly Vector3 PedestalTop = new Vector3(0f, 0.72f, 0f);

    private MeshKit _kit;
    private Material _stone, _stoneDark, _snow, _crystal, _flame, _dash, _frost, _moon, _iron;
    private Transform _frostCrystal;
    private Light _frostLight;
    private readonly List<(Transform t, Vector3 basePos, float speed, float phase)> _mist = new List<(Transform, Vector3, float, float)>();
    private static Mesh _octa;
    private System.Random _r = new System.Random(7);

    private float R(float a, float b) => a + (float)_r.NextDouble() * (b - a);

    void Awake() => Build();

    private void Build()
    {
        _stone = Lit("Piedra noche", new Color(0.2f, 0.24f, 0.31f), 0.15f);
        _stoneDark = Lit("Piedra oscura", new Color(0.11f, 0.14f, 0.19f), 0.12f);
        _snow = Lit("Nieve de noche", new Color(0.42f, 0.48f, 0.58f), 0.25f);
        _iron = Lit("Hierro", new Color(0.08f, 0.09f, 0.11f), 0.5f, 0.6f);
        _crystal = Lit("Cristal de hielo", new Color(0.12f, 0.3f, 0.85f), 0.9f, 0f, new Color(0.2f, 0.45f, 1.6f));
        _frost = Lit("Cristal del Frost", new Color(0.8f, 0.92f, 1f), 0.95f, 0f, new Color(2.6f, 3.2f, 4.2f));
        _flame = Lit("Llama", new Color(1f, 0.9f, 0.75f), 0.5f, 0f, new Color(4.5f, 3.2f, 1.8f));
        _dash = Lit("Luz del pedestal", new Color(0.8f, 0.95f, 1f), 0.5f, 0f, new Color(1.5f, 2.6f, 3.6f));
        _moon = Lit("Luna", Color.white, 0f, 0f, new Color(5f, 5.2f, 5.5f));

        _kit = new MeshKit(transform, null, null, _stone);

        _kit.Box(new Vector3(0f, -0.25f, 10f), Vector3.zero, new Vector3(120f, 0.5f, 120f), _snow, true);
        Pedestal();
        foreach (float side in new[] { -1f, 1f }) Torch(new Vector3(side * 2.9f, 0f, 0.5f));
        Arch(new Vector3(0f, 0f, 8.5f), 2.4f, 6.8f);
        Column(new Vector3(-4.6f, 0f, 3f), 6.2f, false);
        Column(new Vector3(4.4f, 0f, 2.4f), 5.4f, true);
        Column(new Vector3(-7.6f, 0f, 6.5f), 7.2f, true);
        Column(new Vector3(7.8f, 0f, 5.5f), 6.6f, false);
        Column(new Vector3(-2.6f, 0f, 5.2f), 3.2f, true);
        Column(new Vector3(10.5f, 0f, 1.5f), 6f, true);
        Column(new Vector3(-10.5f, 0f, 2f), 5f, false);
        for (int i = 0; i < 9; i++)
            _kit.Box(new Vector3(-9f + i * 2.3f + R(-0.4f, 0.4f), R(0.6f, 1.5f) * 0.5f, 11.5f + R(-0.6f, 0.6f)), new Vector3(0f, R(-8f, 8f), 0f), new Vector3(R(1.6f, 2.3f), R(0.6f, 1.9f), R(0.8f, 1.2f)), _stoneDark, true);
        // Montaña al fondo (silueta que se pierde en la niebla).
        _kit.Box(new Vector3(3f, 9f, 36f), new Vector3(0f, 45f, 0f), new Vector3(22f, 22f, 22f), _stoneDark, false);

        foreach (Vector3 c in new[] { new Vector3(-3.6f, 0f, -0.8f), new Vector3(3.4f, 0f, -1.4f), new Vector3(-5.6f, 0f, 1.6f), new Vector3(5.4f, 0f, 0.6f), new Vector3(-2.4f, 0f, -3.4f), new Vector3(6.6f, 0f, 3.6f) })
            Crystals(c, _r.Next(3, 6));
        for (int i = 0; i < 40; i++)
        {
            float a = R(0f, Mathf.PI * 2f), d = R(3.4f, 13f);
            Vector3 p = new Vector3(Mathf.Sin(a) * d, 0.08f, Mathf.Cos(a) * d * 0.8f + 2f);
            _kit.Box(p, new Vector3(R(0f, 40f), R(0f, 90f), R(0f, 40f)), Vector3.one * R(0.18f, 0.55f), _stoneDark, false);
        }
        _kit.Combine("Ruinas");

        // El cristal del Frost flota tras el pedestal y late.
        var fc = new GameObject("Cristal del Frost", typeof(MeshFilter), typeof(MeshRenderer));
        fc.transform.SetParent(transform, false);
        fc.transform.localPosition = new Vector3(1.6f, 3.4f, 4.5f);
        fc.transform.localScale = new Vector3(0.38f, 0.62f, 0.38f);
        fc.GetComponent<MeshFilter>().sharedMesh = Octahedron();
        fc.GetComponent<MeshRenderer>().sharedMaterial = _frost;
        _frostCrystal = fc.transform;
        _frostLight = PointLight(fc.transform.localPosition, new Color(0.55f, 0.75f, 1f), 9f, 3.2f, false);

        var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(moon.GetComponent<Collider>());
        moon.name = "Luna";
        moon.transform.SetParent(transform, false);
        moon.transform.localPosition = new Vector3(-7.5f, 12.5f, 30f);
        moon.transform.localScale = Vector3.one * 2.4f;
        moon.GetComponent<MeshRenderer>().sharedMaterial = _moon;

        // Foco frío desde arriba: el círculo de luz sobre el pedestal.
        var spotGo = new GameObject("Foco del pedestal");
        spotGo.transform.SetParent(transform, false);
        spotGo.transform.localPosition = new Vector3(0f, 7f, -1.2f);
        spotGo.transform.LookAt(transform.TransformPoint(PedestalTop));
        Light spot = spotGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(0.78f, 0.88f, 1f);
        spot.intensity = 9f;
        spot.range = 14f;
        spot.spotAngle = 42f;
        spot.innerSpotAngle = 20f;
        spot.shadows = LightShadows.Soft;

        PointLight(new Vector3(-3.8f, 0.8f, -0.6f), new Color(0.3f, 0.5f, 1f), 5f, 1.8f, false);
        PointLight(new Vector3(3.6f, 0.8f, -1.2f), new Color(0.3f, 0.5f, 1f), 5f, 1.8f, false);
        PointLight(new Vector3(0f, 1.2f, 8.5f), new Color(0.45f, 0.6f, 1f), 9f, 1.4f, false);

        Snow();
        Mist();
    }

    // ---------- Piezas ----------

    private void Pedestal()
    {
        float[] radius = { 2.4f, 1.85f, 1.35f };
        for (int i = 0; i < 3; i++)
            _kit.Cylinder(new Vector3(0f, 0.12f + i * 0.24f, 0f), radius[i] * 2f, 0.24f, i == 2 ? _stone : _stoneDark, true);
        for (int k = 0; k < 14; k++)
        {
            float a = k / 14f * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Sin(a) * 2.38f, 0.14f, Mathf.Cos(a) * 2.38f);
            _kit.Box(p, new Vector3(0f, a * Mathf.Rad2Deg, 0f), new Vector3(0.05f, 0.05f, 0.26f), _dash, false);
        }
        for (int k = 0; k < 8; k++)
        {
            float a = (k + 0.5f) / 8f * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Sin(a) * 1.82f, 0.38f, Mathf.Cos(a) * 1.82f);
            _kit.Box(p, new Vector3(0f, a * Mathf.Rad2Deg + 90f, 0f), new Vector3(0.05f, 0.04f, 0.18f), _dash, false);
        }
    }

    private void Torch(Vector3 p)
    {
        _kit.Cylinder(p + Vector3.up * 1.15f, 0.12f, 2.3f, _iron, true);
        _kit.Cylinder(p + Vector3.up * 2.32f, 0.6f, 0.12f, _iron);
        _kit.Cylinder(p + Vector3.up * 0.04f, 0.5f, 0.08f, _iron);
        _kit.Mesh(Octahedron(), p + Vector3.up * 2.62f, Vector3.zero, new Vector3(0.24f, 0.42f, 0.24f), _flame);
        PointLight(p + Vector3.up * 2.75f, new Color(1f, 0.72f, 0.42f), 8f, 3.6f, true);
    }

    private void Column(Vector3 p, float height, bool broken)
    {
        _kit.Box(p + Vector3.up * 0.25f, Vector3.zero, new Vector3(1.5f, 0.5f, 1.5f), _stoneDark, true);
        int drums = Mathf.Max(2, Mathf.RoundToInt(height / 0.9f));
        float h = (height - 0.5f) / drums;
        for (int i = 0; i < drums; i++)
        {
            if (broken && i == drums - 1) break;
            Vector3 off = new Vector3(R(-0.04f, 0.04f), 0f, R(-0.04f, 0.04f));
            _kit.Cylinder(p + off + Vector3.up * (0.5f + h * (i + 0.5f)), 1.05f - i * 0.01f, h - 0.04f, i % 2 == 0 ? _stone : _stoneDark, i == 0);
        }
        if (!broken) _kit.Box(p + Vector3.up * (height + 0.15f), Vector3.zero, new Vector3(1.6f, 0.3f, 1.6f), _stone);
        else _kit.Box(p + new Vector3(0.9f, 0.3f, -0.6f), new Vector3(R(-10f, 10f), R(0f, 90f), 88f), new Vector3(1f, h, 1f), _stoneDark);
    }

    private void Arch(Vector3 c, float half, float height)
    {
        foreach (float side in new[] { -1f, 1f }) Column(c + Vector3.right * side * half, height, false);
        float r = half, top = height + 0.3f;
        for (int k = 0; k <= 10; k++)
        {
            if (k == 7) continue;
            float a = Mathf.PI * k / 10f;
            Vector3 p = c + new Vector3(-Mathf.Cos(a) * r, top + Mathf.Sin(a) * r, 0f);
            _kit.Box(p, new Vector3(0f, 0f, a * Mathf.Rad2Deg - 90f), new Vector3(0.95f, 0.75f, 1.1f), k % 2 == 0 ? _stone : _stoneDark);
        }
    }

    private void Crystals(Vector3 p, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float h = R(0.5f, 1.4f);
            Vector3 off = new Vector3(R(-0.45f, 0.45f), h * 0.3f, R(-0.45f, 0.45f));
            _kit.Mesh(Octahedron(), p + off, new Vector3(R(-28f, 28f), R(0f, 90f), R(-28f, 28f)), new Vector3(h * 0.22f, h * 0.6f, h * 0.22f), _crystal);
        }
    }

    // ---------- Luces, nieve y bruma ----------

    private Light PointLight(Vector3 local, Color color, float range, float intensity, bool flicker)
    {
        var go = new GameObject("Luz");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        if (flicker)
        {
            FlickerLight f = go.AddComponent<FlickerLight>();
            f.baseIntensity = intensity;
            f.intensityVariation = intensity * 0.18f;
            f.speed = 6f;
        }
        return l;
    }

    private void Snow()
    {
        var go = new GameObject("Nieve");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 9f, 3f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 9f;
        main.startSpeed = 0.6f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.03f;
        main.startColor = new Color(0.85f, 0.92f, 1f, 0.8f);
        main.prewarm = true;
        var em = ps.emission;
        em.rateOverTime = 60f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(24f, 0.5f, 16f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.3f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Transparent("Copo", Soft(32), new Color(1f, 1f, 1f, 0.9f), false);
        main.startRotation = 0f;
        ps.Play();
    }

    // Bruma: planos grandes y suaves que derivan despacio entre las ruinas, y dos haces de luz de luna.
    private void Mist()
    {
        Material mist = Transparent("Bruma", Soft(64), new Color(0.55f, 0.68f, 0.9f, 0.13f), false);
        for (int i = 0; i < 7; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "Bruma";
            q.transform.SetParent(transform, false);
            Vector3 p = new Vector3(R(-9f, 9f), R(0.4f, 1.6f), R(1f, 12f));
            q.transform.localPosition = p;
            q.transform.localScale = new Vector3(R(8f, 14f), R(2f, 3.5f), 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = mist;
            q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mist.Add((q.transform, p, R(0.15f, 0.35f), R(0f, 6f)));
        }
        Material shaft = Transparent("Haz de luna", Soft(64), new Color(0.5f, 0.68f, 1f, 0.09f), true);
        foreach (float x in new[] { -1.2f, 1.6f })
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "Haz";
            q.transform.SetParent(transform, false);
            q.transform.localPosition = new Vector3(x, 5f, 7.5f);
            q.transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
            q.transform.localScale = new Vector3(2.4f, 12f, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = shaft;
            q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    void Update()
    {
        float t = Time.unscaledTime;
        if (_frostCrystal != null)
        {
            _frostCrystal.localPosition = new Vector3(1.6f, 3.4f + Mathf.Sin(t * 1.1f) * 0.12f, 4.5f);
            _frostCrystal.localRotation = Quaternion.Euler(0f, t * 24f, 0f);
            // Late: BOM... BOM...
            float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 2.2f)), 12f);
            if (_frostLight != null) _frostLight.intensity = 2.6f + beat * 2.4f;
        }
        foreach (var m in _mist)
        {
            if (m.t == null) continue;
            m.t.localPosition = m.basePos + new Vector3(Mathf.Sin(t * m.speed * 0.3f + m.phase) * 1.6f, 0f, 0f);
            Camera cam = Camera.main;
            if (cam != null) m.t.rotation = Quaternion.LookRotation(m.t.position - cam.transform.position);
        }
    }

    // ---------- Materiales y mallas ----------

    private static Material Lit(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        return m;
    }

    private static Material Transparent(string name, Texture tex, Color color, bool additive)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var m = new Material(sh) { name = name };
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        return m;
    }

    private static Texture2D Soft(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Suave" };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        return tex;
    }

    public static Mesh Octahedron()
    {
        if (_octa != null) return _octa;
        Vector3[] v = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        int[][] faces = { new[] { 0, 4, 3 }, new[] { 0, 3, 5 }, new[] { 0, 5, 2 }, new[] { 0, 2, 4 }, new[] { 1, 3, 4 }, new[] { 1, 5, 3 }, new[] { 1, 2, 5 }, new[] { 1, 4, 2 } };
        var verts = new List<Vector3>();
        var tris = new List<int>();
        foreach (int[] f in faces)
        {
            for (int i = 0; i < 3; i++) { verts.Add(v[f[i]] * 0.5f); tris.Add(verts.Count - 1); }
        }
        _octa = new Mesh { name = "Octaedro" };
        _octa.SetVertices(verts);
        _octa.SetTriangles(tris, 0);
        _octa.RecalculateNormals();
        _octa.RecalculateBounds();
        return _octa;
    }
}
