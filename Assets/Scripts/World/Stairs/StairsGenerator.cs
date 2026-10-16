using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

// Construye La Escalinata (capítulo I): terrazas de piedra con hordas, tramos de escalera con peldaños,
// barandillas sobre el precipicio, el reino en miniatura en el valle de abajo y la explanada de Rocco Vendaval.
[DefaultExecutionOrder(-900)]
public class StairsGenerator : MonoBehaviour
{
    public static StairsGenerator Instance { get; private set; }

    [Header("Piezas")]
    public GameObject pinePrefab;
    public GameObject bonfirePrefab;

    [Header("Materiales")]
    public Material snowMaterial;
    public Material stoneMaterial;
    public int fallbackSeed = 12345;

    public int Seed { get; private set; }
    public bool Built { get; private set; }
    public RoccoEncounter Encounter { get; private set; }

    private Material _stone, _stoneDark, _snow, _rock, _wood, _roof, _window, _crystal;
    private MeshKit _kit;
    private Transform _root;
    private DeterministicRng _rng;

    void Awake()
    {
        Instance = this;
        Seed = GameSession.Instance != null && GameSession.Instance.worldSeed != 0 ? GameSession.Instance.worldSeed : fallbackSeed;
        _rng = new DeterministicRng(SeedUtil.Combine(Seed, SeedUtil.FromString("escalinata")));
        _root = new GameObject("Escalinata_Generada").transform;
        _root.SetParent(transform, false);
        Materials();
        _kit = new MeshKit(_root, null, null, _stone);
        Terraces();
        Flights();
        Arena();
        _kit.Combine("Escalinata");
        Kingdom();
        Camps();
        Gates();
        BakeNavMesh();
        PlacePlayer();
        Atmosphere();
        BuildMap();
        AdventureHUD.Ensure();
        Built = true;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Materials()
    {
        _stone = stoneMaterial != null ? stoneMaterial : MenuRuins.Lit("Piedra", new Color(0.55f, 0.57f, 0.62f), 0.1f);
        _stoneDark = MenuRuins.Lit("Piedra oscura", new Color(0.32f, 0.35f, 0.42f), 0.12f);
        _rock = MenuRuins.Lit("Roca de la cara norte", new Color(0.3f, 0.34f, 0.42f), 0.15f);
        _snow = snowMaterial != null ? snowMaterial : MenuRuins.Lit("Nieve", new Color(0.93f, 0.96f, 1f), 0.2f);
        _wood = MenuRuins.Lit("Madera del reino", new Color(0.45f, 0.3f, 0.2f), 0.1f);
        _roof = MenuRuins.Lit("Tejados del reino", new Color(0.32f, 0.42f, 0.62f), 0.15f);
        _window = MenuRuins.Lit("Ventanas encendidas", new Color(1f, 0.8f, 0.45f), 0.3f, 0f, new Color(2.2f, 1.5f, 0.6f));
        _crystal = MenuRuins.Lit("Cristal del Frost", new Color(0.32f, 0.62f, 0.95f), 0.9f, 0f, new Color(0.25f, 0.7f, 1.6f));
    }

    // ---------- Terrazas ----------

    private void Terraces()
    {
        for (int i = 0; i < StairsLayout.Terraces; i++)
        {
            Vector3 c = StairsLayout.TerraceCenter(i);
            float depth = StairsLayout.TerraceDepth;
            float hw = StairsLayout.HalfWidth;
            // Bloque de roca que baja hasta el valle y se mete por debajo de la terraza siguiente.
            float bottom = StairsLayout.KingdomDepth - 10f;
            float south = c.z - depth * 0.5f - StairsLayout.TerraceSpacing;
            float north = c.z + depth * 0.5f;
            _kit.Box(new Vector3(0f, (c.y + bottom) * 0.5f - 0.3f, (south + north) * 0.5f), Vector3.zero, new Vector3(hw * 2f + 4f, c.y - bottom - 0.6f, north - south), _rock);
            // Suelo de losas con nieve en los bordes.
            _kit.Box(new Vector3(0f, c.y - 0.3f, c.z), Vector3.zero, new Vector3(hw * 2f, 0.6f, depth), _stone, true);
            float gap = StairsLayout.TerraceSpacing - depth;
            _kit.Box(new Vector3(0f, c.y - 0.25f, c.z - depth * 0.5f - gap * 0.5f), Vector3.zero, new Vector3(hw * 2f, 0.5f, gap), _rock, false);
            for (float x = -hw + 2f; x < hw - 1f; x += _rng.Range(5f, 9f))
                _kit.Box(new Vector3(x, c.y + 0.02f, c.z + _rng.Range(-2.5f, 2.5f)), new Vector3(0f, _rng.Range(0f, 90f), 0f), new Vector3(_rng.Range(1.5f, 3f), 0.06f, _rng.Range(1f, 2f)), _snow);
            // Pared del fondo (al sur), con el hueco por donde sale el tramo que sube a la siguiente terraza.
            Vector3 up, upTo;
            float upWidth = StairsLayout.FlightWidth;
            if (i < StairsLayout.Terraces - 1) StairsLayout.Flight(i + 1, out up, out upTo);
            else { StairsLayout.FinalFlight(out up, out upTo); upWidth = 5f; }
            float g0 = up.x - upWidth * 0.5f - 0.5f, g1 = up.x + upWidth * 0.5f + 0.5f;
            float wz = c.z - depth * 0.5f - 0.4f;
            foreach (var (x0, x1) in new[] { (-hw, g0), (g1, hw) })
            {
                if (x1 - x0 < 0.3f) continue;
                _kit.Box(new Vector3((x0 + x1) * 0.5f, c.y + 1.2f, wz), Vector3.zero, new Vector3(x1 - x0, 2.4f, 0.8f), _rock, true);
            }
            // Barandilla sobre el precipicio (al norte), con hueco donde baja la escalera.
            Railing(i);
            // Extremos este y oeste: pilares de roca altos.
            foreach (float s in new[] { -1f, 1f })
            {
                _kit.Box(new Vector3(s * (hw + 1.2f), c.y + 3f, c.z - 2f), new Vector3(0f, 0f, s * 4f), new Vector3(2.4f, 8f, depth + 8f), _rock, true);
                _kit.Box(new Vector3(s * (hw + 1.2f), c.y + 7.2f, c.z - 2f), Vector3.zero, new Vector3(2.6f, 0.4f, depth + 8.2f), _snow);
            }
            // Antorchas de cristal del Frost en las esquinas de cada terraza.
            foreach (float s in new[] { -1f, 1f }) Crystal(new Vector3(s * (hw - 1.5f), c.y, c.z - depth * 0.5f + 0.8f));
        }
    }

    private void Railing(int i)
    {
        Vector3 c = StairsLayout.TerraceCenter(i);
        float z = c.z + StairsLayout.TerraceDepth * 0.5f - 0.25f;
        float hw0 = StairsLayout.HalfWidth;
        float gapMin = hw0 + 10f, gapMax = hw0 + 10f;
        if (i > 0)
        {
            StairsLayout.Flight(i, out Vector3 a, out Vector3 b);
            gapMin = b.x - StairsLayout.FlightWidth * 0.5f - 0.4f;
            gapMax = b.x + StairsLayout.FlightWidth * 0.5f + 0.4f;
        }
        // El tramo que baja desde esta terraza a la siguiente también deja un hueco en la pared del fondo (al sur).
        float hw = StairsLayout.HalfWidth;
        for (float x = -hw + 0.6f; x <= hw - 0.6f; x += 2f)
        {
            if (x > gapMin && x < gapMax) continue;
            _kit.Box(new Vector3(x, c.y + 0.45f, z), Vector3.zero, new Vector3(0.35f, 0.9f, 0.35f), _stoneDark);
        }
        // Pasamanos y colisión alta (invisible) para no caer.
        Segment(-hw, Mathf.Min(gapMin, hw), c.y, z);
        if (gapMax < hw) Segment(gapMax, hw, c.y, z);
    }

    private void Segment(float x0, float x1, float y, float z)
    {
        if (x1 - x0 < 0.3f) return;
        _kit.Box(new Vector3((x0 + x1) * 0.5f, y + 0.95f, z), Vector3.zero, new Vector3(x1 - x0, 0.15f, 0.45f), _stone);
        _kit.ColliderBox(new Vector3((x0 + x1) * 0.5f, y + 1.2f, z), Vector3.zero, new Vector3(x1 - x0, 2.4f, 0.5f));
    }

    private void Crystal(Vector3 at)
    {
        Mesh octa = MenuRuins.Octahedron();
        _kit.Mesh(octa, at + Vector3.up * 0.9f, new Vector3(0f, 20f, 8f), new Vector3(0.4f, 1.8f, 0.4f), _crystal);
        _kit.Mesh(octa, at + new Vector3(0.3f, 0.5f, 0.1f), new Vector3(20f, 0f, -25f), new Vector3(0.25f, 1f, 0.25f), _crystal);
        var go = new GameObject("Luz_Frost");
        go.transform.SetParent(_root, false);
        go.transform.position = at + Vector3.up * 1.6f;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.45f, 0.72f, 1f);
        l.range = 9f;
        l.intensity = 1.8f;
        l.shadows = LightShadows.None;
    }

    // ---------- Escaleras ----------

    private void Flights()
    {
        for (int i = 1; i < StairsLayout.Terraces; i++)
        {
            StairsLayout.Flight(i, out Vector3 a, out Vector3 b);
            CurvedStair(a, b);
        }
        StairsLayout.FinalFlight(out Vector3 fa, out Vector3 fb);
        Stair(fa, fb, 5f);
    }

    // Tramo: rampa invisible para caminar, peldaños de piedra encima, muretes a los lados.
    private void Stair(Vector3 a, Vector3 b, float width = StairsLayout.FlightWidth)
    {
        Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
        float run = flat.magnitude;
        float rise = b.y - a.y;
        Quaternion yaw = Quaternion.LookRotation(flat.normalized);
        float pitch = -Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
        float length = Mathf.Sqrt(run * run + rise * rise);
        Vector3 mid = (a + b) * 0.5f;
        Vector3 euler = (yaw * Quaternion.Euler(pitch, 0f, 0f)).eulerAngles;
        _kit.ColliderBox(mid - Vector3.up * 0.15f, euler, new Vector3(width, 0.3f, length + 0.6f));
        // Muretes laterales (con colisión) y apoyo de roca bajo el tramo.
        Vector3 right = yaw * Vector3.right;
        foreach (float s in new[] { -1f, 1f })
        {
            _kit.Box(mid + right * s * (width * 0.5f + 0.2f) + Vector3.up * 0.5f, euler, new Vector3(0.4f, 1f, length + 0.4f), _stoneDark, false);
            _kit.ColliderBox(mid + right * s * (width * 0.5f + 0.25f) + Vector3.up * 1.1f, euler, new Vector3(0.5f, 2.4f, length + 0.4f));
        }
        _kit.Box(new Vector3(mid.x, (Mathf.Min(a.y, b.y) + StairsLayout.KingdomDepth) * 0.5f, mid.z), yaw.eulerAngles, new Vector3(width + 1f, Mathf.Min(a.y, b.y) - StairsLayout.KingdomDepth, run * 0.9f), _rock);
        int steps = Mathf.Max(2, Mathf.RoundToInt(rise / 0.28f));
        for (int k = 0; k < steps; k++)
        {
            float t0 = k / (float)steps, t1 = (k + 1) / (float)steps;
            Vector3 p0 = Vector3.Lerp(a, b, t0), p1 = Vector3.Lerp(a, b, t1);
            float top = p1.y;
            Vector3 center = new Vector3((p0.x + p1.x) * 0.5f, (top + p0.y - 0.35f) * 0.5f, (p0.z + p1.z) * 0.5f);
            _kit.Box(center, yaw.eulerAngles, new Vector3(width, top - p0.y + 0.35f, run / steps + 0.02f), (k & 1) == 0 ? _stone : _stoneDark);
        }
    }

    // Tramo curvo: se arma con segmentos cortos (rampa invisible + muretes con colisión que se solapan) y peldaños
    // que siguen la curva. Los extremos van rectos hacia el sur para encajar con los huecos de muro y barandilla.
    private void CurvedStair(Vector3 a, Vector3 b, float width = StairsLayout.FlightWidth)
    {
        const int segments = 14;
        for (int j = 0; j < segments; j++)
        {
            Vector3 p0 = StairsLayout.FlightPoint(a, b, j / (float)segments);
            Vector3 p1 = StairsLayout.FlightPoint(a, b, (j + 1) / (float)segments);
            Vector3 flat = new Vector3(p1.x - p0.x, 0f, p1.z - p0.z);
            float run = flat.magnitude, rise = p1.y - p0.y;
            Quaternion yaw = Quaternion.LookRotation(flat.normalized);
            float pitch = -Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(run * run + rise * rise);
            Vector3 mid = (p0 + p1) * 0.5f;
            Vector3 euler = (yaw * Quaternion.Euler(pitch, 0f, 0f)).eulerAngles;
            Vector3 right = yaw * Vector3.right;
            foreach (float s in new[] { -1f, 1f })
            {
                _kit.Box(mid + right * s * (width * 0.5f + 0.2f) + Vector3.up * 0.5f, euler, new Vector3(0.4f, 1f, length + 0.35f), _stoneDark, false);
                _kit.ColliderBox(mid + right * s * (width * 0.5f + 0.25f) + Vector3.up * 1.1f, euler, new Vector3(0.5f, 2.4f, length + 0.7f));
            }
            float low = Mathf.Min(p0.y, p1.y);
            _kit.Box(new Vector3(mid.x, (low + StairsLayout.KingdomDepth) * 0.5f, mid.z), yaw.eulerAngles, new Vector3(width + 1f, low - StairsLayout.KingdomDepth, run + 0.2f), _rock);
        }
        RampSurface(a, b, width + 0.3f);
        float totalRise = b.y - a.y;
        int steps = Mathf.Max(2, Mathf.RoundToInt(totalRise / 0.28f));
        for (int k = 0; k < steps; k++)
        {
            Vector3 p0 = StairsLayout.FlightPoint(a, b, k / (float)steps), p1 = StairsLayout.FlightPoint(a, b, (k + 1) / (float)steps);
            Vector3 flat = new Vector3(p1.x - p0.x, 0f, p1.z - p0.z);
            Quaternion yaw = Quaternion.LookRotation(flat.normalized);
            float top = p1.y;
            Vector3 center = new Vector3((p0.x + p1.x) * 0.5f, (top + p0.y - 0.35f) * 0.5f, (p0.z + p1.z) * 0.5f);
            _kit.Box(center, yaw.eulerAngles, new Vector3(width, top - p0.y + 0.35f, flat.magnitude + 0.12f), (k & 1) == 0 ? _stone : _stoneDark);
        }
    }

    // Superficie continua del tramo curvo (una sola malla): sin juntas ni aristas en las que se enganche el héroe.
    // Empieza y termina un poco metida dentro del suelo de cada terraza.
    private void RampSurface(Vector3 a, Vector3 b, float width)
    {
        const int n = 48;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int k = 0; k <= n + 2; k++)
        {
            float t = (k - 1) / (float)n;
            Vector3 p, fwd;
            if (t < 0f) { fwd = Vector3.back; p = a + Vector3.forward * 0.6f + Vector3.down * 0.05f; }
            else if (t > 1f) { fwd = Vector3.back; p = b + Vector3.back * 0.6f + Vector3.down * 0.05f; }
            else
            {
                p = StairsLayout.FlightPoint(a, b, t);
                Vector3 q = StairsLayout.FlightPoint(a, b, Mathf.Min(1f, t + 0.01f)) - StairsLayout.FlightPoint(a, b, Mathf.Max(0f, t - 0.01f));
                q.y = 0f;
                fwd = q.sqrMagnitude > 1e-6f ? q.normalized : Vector3.back;
            }
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            verts.Add(p - right * width * 0.5f);
            verts.Add(p + right * width * 0.5f);
            if (k > 0)
            {
                int i0 = (k - 1) * 2;
                tris.AddRange(new[] { i0, i0 + 2, i0 + 1, i0 + 1, i0 + 2, i0 + 3 });
            }
        }
        var mesh = new Mesh { name = "Rampa_Tramo" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        if (mesh.normals.Length > 0 && mesh.normals[0].y < 0f)
        {
            for (int i = 0; i < tris.Count; i += 3) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
        }
        var go = new GameObject("Rampa_Tramo");
        go.transform.SetParent(_kit.colliders, false);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    // ---------- Explanada de Rocco ----------

    private void Arena()
    {
        Vector3 c = StairsLayout.ArenaCenter;
        float r = StairsLayout.ArenaRadius;
        _kit.Cylinder(new Vector3(c.x, c.y - 0.4f, c.z), r * 2f, 0.8f, _stone, false);
        var floor = new GameObject("Suelo_Explanada");
        floor.transform.SetParent(_kit.colliders, false);
        floor.transform.position = new Vector3(c.x, c.y - 0.4f, c.z);
        var cap = floor.AddComponent<MeshCollider>();
        cap.sharedMesh = DiscMesh(r, 32);
        floor.transform.position = new Vector3(c.x, c.y, c.z);
        _kit.Cylinder(new Vector3(c.x, (c.y + StairsLayout.KingdomDepth) * 0.5f, c.z), r * 1.9f, c.y - StairsLayout.KingdomDepth, _rock, false);
        _kit.Cylinder(new Vector3(c.x, c.y + 0.01f, c.z), r * 1.2f, 0.04f, _snow, false);
        // Borde: pilares rotos, cristales y el vacío alrededor (pared invisible).
        int n = 20;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            Vector3 p = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (r - 0.8f);
            if (Mathf.Cos(a) > 0.92f) continue;
            float h = (i % 3 == 0) ? _rng.Range(2.5f, 4.5f) : _rng.Range(0.6f, 1.4f);
            _kit.Box(p + Vector3.up * h * 0.5f, new Vector3(0f, a * Mathf.Rad2Deg, _rng.Range(-4f, 4f)), new Vector3(0.9f, h, 0.9f), _stoneDark, true);
            if (i % 4 == 2) Crystal(p + (c - p).normalized * 1.2f);
        }
        for (int i = 0; i < 48; i++)
        {
            float a0 = i / 48f * Mathf.PI * 2f;
            if (Mathf.Cos(a0) > 0.95f) continue;
            Vector3 p = c + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * (r + 0.2f);
            _kit.ColliderBox(p + Vector3.up * 1.4f, new Vector3(0f, a0 * Mathf.Rad2Deg, 0f), new Vector3(2.1f, 2.8f, 0.5f));
        }
        var go = new GameObject("Encuentro_Rocco");
        go.transform.SetParent(_root, false);
        go.transform.position = c;
        Encounter = go.AddComponent<RoccoEncounter>();
    }

    private static Mesh DiscMesh(float radius, int sides)
    {
        var v = new List<Vector3> { Vector3.zero };
        var t = new List<int>();
        for (int i = 0; i <= sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            v.Add(new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius));
            if (i > 0) { t.Add(0); t.Add(i); t.Add(i + 1); }
        }
        var m = new Mesh { name = "Disco" };
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        return m;
    }

    // ---------- El reino, abajo ----------

    // Valle nevado con el poblado, su castillo, bosques y ventanas encendidas: se ve cada vez más lejos al subir.
    private void Kingdom()
    {
        Transform k = new GameObject("Reino").transform;
        k.SetParent(_root, false);
        var kit = new MeshKit(k, null, null, _stone);
        float y = StairsLayout.KingdomDepth;
        kit.Box(new Vector3(0f, y - 1f, 300f), Vector3.zero, new Vector3(900f, 2f, 700f), _snow);
        Vector3 town = new Vector3(_rng.Range(-30f, 30f), y, 190f);
        Mesh roofMesh = MountainMeshes.Peak(SeedUtil.Combine(Seed, 51));
        for (int i = 0; i < 46; i++)
        {
            float a = _rng.Range(0f, Mathf.PI * 2f), d = Mathf.Sqrt(_rng.Next01()) * 42f;
            Vector3 p = town + new Vector3(Mathf.Sin(a) * d, 0f, Mathf.Cos(a) * d * 0.8f);
            float w = _rng.Range(4f, 7f), dd = _rng.Range(4f, 6f), h = _rng.Range(3f, 5f);
            float rot = _rng.Range(0f, 90f);
            kit.Box(p + Vector3.up * h * 0.5f, new Vector3(0f, rot, 0f), new Vector3(w, h, dd), _wood);
            kit.Mesh(roofMesh, p + Vector3.up * h, new Vector3(0f, rot, 0f), new Vector3(w * 1.5f, h * 0.9f, dd * 1.5f), _roof);
            if (i % 2 == 0) kit.Box(p + Vector3.up * h * 0.5f + Quaternion.Euler(0f, rot, 0f) * Vector3.forward * (dd * 0.5f + 0.05f), new Vector3(0f, rot, 0f), new Vector3(1f, 1.2f, 0.1f), _window);
        }
        // Muralla y castillo del reino.
        for (int i = 0; i < 36; i++)
        {
            float a = i / 36f * Mathf.PI * 2f;
            Vector3 p = town + new Vector3(Mathf.Sin(a) * 52f, 2f, Mathf.Cos(a) * 44f);
            kit.Box(p, new Vector3(0f, a * Mathf.Rad2Deg + 90f, 0f), new Vector3(9.5f, 4f, 1.6f), _stone);
            if (i % 6 == 0) { kit.Cylinder(p + Vector3.up * 2f, 4f, 9f, _stone); kit.Mesh(roofMesh, p + Vector3.up * 6.5f, Vector3.zero, new Vector3(6f, 6f, 6f), _roof); }
        }
        Vector3 keep = town + new Vector3(_rng.Chance(0.5f) ? 70f : -70f, 6f, 40f);
        kit.Box(keep + Vector3.up * 4f, Vector3.zero, new Vector3(26f, 12f, 18f), _stone);
        foreach (Vector3 o in new[] { new Vector3(-13f, 0f, -9f), new Vector3(13f, 0f, -9f), new Vector3(-13f, 0f, 9f), new Vector3(13f, 0f, 9f) })
        {
            kit.Cylinder(keep + o + Vector3.up * 9f, 6f, 22f, _stone);
            kit.Mesh(roofMesh, keep + o + Vector3.up * 20f, Vector3.zero, new Vector3(8f, 12f, 8f), _roof);
        }
        kit.Cylinder(keep + Vector3.up * 16f, 7f, 30f, _stone);
        kit.Mesh(roofMesh, keep + Vector3.up * 31f, Vector3.zero, new Vector3(10f, 18f, 10f), _roof);
        kit.Combine("Reino");
        if (pinePrefab != null)
            for (int i = 0; i < 260; i++)
            {
                Vector3 p = new Vector3(_rng.Range(-300f, 300f), y, _rng.Range(60f, 520f));
                if ((p - town).magnitude < 60f) continue;
                GameObject pine = Instantiate(pinePrefab, p, Quaternion.Euler(0f, _rng.Range(0f, 360f), 0f), k);
                pine.transform.localScale = Vector3.one * _rng.Range(2.5f, 4.5f);
                foreach (Collider c in pine.GetComponentsInChildren<Collider>()) DestroyImmediate(c);
                foreach (Transform t in pine.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
            }
        for (int i = 0; i < 10; i++)
        {
            var go = new GameObject("Luz_Reino");
            go.transform.SetParent(k, false);
            go.transform.position = town + new Vector3(_rng.Range(-35f, 35f), 6f, _rng.Range(-30f, 30f));
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.75f, 0.4f);
            l.range = 30f;
            l.intensity = 2.5f;
            l.shadows = LightShadows.None;
        }
        // Cumbres lejanas a los lados del valle.
        for (int i = 0; i < 10; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            var peak = new GameObject("Cumbre");
            peak.transform.SetParent(k, false);
            peak.transform.position = new Vector3(side * _rng.Range(160f, 320f), y - 5f, _rng.Range(80f, 520f));
            float s = _rng.Range(90f, 170f);
            peak.transform.localScale = new Vector3(s * 1.5f, s, s * 1.5f);
            peak.AddComponent<MeshFilter>().sharedMesh = MountainMeshes.Peak(SeedUtil.Combine(Seed, 600 + i));
            var r = peak.AddComponent<MeshRenderer>();
            r.sharedMaterial = _snow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // ---------- Hordas, hogueras y pasos ----------

    private void Camps()
    {
        GameDatabase db = GameDatabase.Instance;
        for (int i = 1; i < StairsLayout.Terraces; i++)
        {
            Vector3 c = StairsLayout.TerraceCenter(i) + new Vector3(_rng.Range(-8f, 8f), 0f, 0f);
            if (i == 3 || i == StairsLayout.Terraces - 1)
            {
                Bonfire(StairsLayout.TerraceCenter(i) + new Vector3(-StairsLayout.FlightSide(i) * 15f, 0f, -1f), StairsLayout.BonfirePrefix + (i == 3 ? 1 : 2));
                if (i == StairsLayout.Terraces - 1) continue;
            }
            if (db == null) continue;
            var go = new GameObject("Horda_Terraza_" + i);
            go.transform.SetParent(_root, false);
            go.transform.position = c;
            EnemyCamp camp = go.AddComponent<EnemyCamp>();
            camp.campId = "escalinata_" + i;
            camp.melee = db.FindEnemy("Corrupt_Melee");
            camp.ranged = db.FindEnemy("Corrupt_Ranged");
            EnemyDefinition knight = db.FindEnemy("Corrupt_Knight");
            if (knight != null) { camp.meleeVariants = new[] { knight }; camp.variantChance = 0.45f; }
            var elites = new List<EnemyDefinition>();
            foreach (string e in new[] { "Corrupt_Knight", "Corrupt_Mage", "Corrupt_Ninja", "Corrupt_Viking" })
            {
                EnemyDefinition d = db.FindEnemy(e);
                if (d != null) elites.Add(d);
            }
            camp.elites = elites.ToArray();
            camp.eliteChance = 0.35f + i * 0.08f;
            camp.eliteMinPlayerLevel = 1;
            camp.palette = db.palette;
            camp.groupSize = new Vector2Int(5, 8);
            camp.rangedFraction = 0.3f;
            camp.monsterLevel = new Vector2Int(6 + i / 2, 7 + i / 2);
            camp.radius = 7f;
            camp.activateDistance = 26f;
        }
    }

    private void Bonfire(Vector3 at, string id)
    {
        if (bonfirePrefab == null) return;
        GameObject go = Instantiate(bonfirePrefab, at, Quaternion.Euler(0f, 0f, 0f), _root);
        go.name = id;
        go.transform.localScale = Vector3.one * 0.9f;
        Bonfire b = go.GetComponent<Bonfire>();
        if (b == null) b = go.AddComponent<Bonfire>();
        b.id = id;
        b.displayName = "Hoguera de la Escalinata";
    }

    private void Gates()
    {
        Material cloth = MenuRuins.Lit("Banderín", new Color(0.62f, 0.16f, 0.18f), 0.1f);
        cloth.SetFloat("_Cull", 0f);
        ZoneGate down = ZoneGate.Build(_root, "Paso_Faldas", StairsLayout.BottomGate, -90f, 6f, _stone, cloth);
        down.targetScene = SceneIds.Foothills;
        down.arrival = StairsLayout.ArrivalFromStairs;
        down.label = "Bajar a Las Faldas";
        down.gameObject.layer = 2;

        ZoneGate onward = ZoneGate.Build(_root, "Paso_Bosque", StairsLayout.ArenaGate, 180f, 6f, _stone, cloth);
        onward.targetScene = "";
        onward.label = "Camino del Bosque Corrupto";
        onward.canPass = () => QuestLog.Stage(QuestLog.Ascent) >= QuestLog.AscentRocco;
        onward.blockedMessage = "Rocco Vendaval cierra el camino. Derrótalo primero.";
        onward.gameObject.layer = 2;

        ZoneArrival.Create(_root, StairsLayout.ArrivalFromFoothills, StairsLayout.Entry, 90f);
    }

    private void BakeNavMesh()
    {
        NavMeshSurface surface = gameObject.GetComponent<NavMeshSurface>();
        if (surface == null) surface = gameObject.AddComponent<NavMeshSurface>();
        // Solo la montaña (terrazas, escaleras y explanada): el reino de abajo queda fuera del horneado.
        surface.collectObjects = CollectObjects.Volume;
        Vector3 top = StairsLayout.ArenaCenter;
        surface.center = transform.InverseTransformPoint(new Vector3(0f, (top.y + 2f) * 0.5f, top.z * 0.5f + 4f));
        surface.size = new Vector3(StairsLayout.HalfWidth * 2f + 8f, top.y + 14f, -top.z + StairsLayout.ArenaRadius + 18f);
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~(1 << 2);
        surface.BuildNavMesh();
    }

    private void PlacePlayer()
    {
        GameObject p = GameObject.Find("Player");
        if (p == null) return;
        Quaternion rot = Quaternion.Euler(0f, 90f, 0f);
        p.transform.SetPositionAndRotation(StairsLayout.Entry, rot);
        Rigidbody rb = p.GetComponent<Rigidbody>();
        if (rb != null) { rb.position = StairsLayout.Entry; rb.rotation = rot; }
        PlayerRespawn respawn = p.GetComponent<PlayerRespawn>();
        ZoneArrival entry = ZoneArrival.Find(StairsLayout.ArrivalFromFoothills);
        if (respawn != null && entry != null) respawn.respawnPoint = entry.transform;
        Camera cam = Camera.main;
        CameraFollow follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
        if (follow != null)
        {
            follow.target = p.transform;
            cam.transform.position = StairsLayout.Entry + follow.offset;
            cam.transform.LookAt(StairsLayout.Entry + Vector3.up * follow.lookHeight);
        }
    }

    // Mapa de La Escalinata: terrazas, la bajada a Las Faldas y la explanada de Rocco.
    private void BuildMap()
    {
        var map = new GameObject("Mapa").AddComponent<MapSystem>();
        map.transform.SetParent(transform, false);
        Vector3 top = StairsLayout.ArenaCenter;
        float half = Mathf.Max(StairsLayout.HalfWidth + 10f, (-top.z + StairsLayout.ArenaRadius + 14f) * 0.5f);
        Vector3 center = new Vector3(0f, 0f, (top.z - StairsLayout.ArenaRadius + 8f) * 0.5f);
        map.SetupZone("escalinata", "La Escalinata", center, half, 96, 14f, new[]
        {
            MapSystem.Mark(MapSystem.MarkerKind.Exit, StairsLayout.BottomGate, "Bajar a Las Faldas"),
            MapSystem.Mark(MapSystem.MarkerKind.Throne, StairsLayout.ArenaCenter, "Explanada de Rocco"),
        }, () => QuestLog.Stage(QuestLog.Ascent) >= QuestLog.AscentRocco ? StairsLayout.ArenaGate : StairsLayout.ArenaCenter, StairsLayout.Entry);
    }

    private void Atmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.66f, 0.74f, 0.86f);
        RenderSettings.fogDensity = 0.0032f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.66f, 0.8f);
        RenderSettings.ambientEquatorColor = new Color(0.46f, 0.52f, 0.64f);
        RenderSettings.ambientGroundColor = new Color(0.28f, 0.3f, 0.38f);
        Camera cam = Camera.main;
        if (cam != null) cam.farClipPlane = Mathf.Max(cam.farClipPlane, 900f);
    }
}
