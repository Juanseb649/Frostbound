using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

// Capítulo II · El Bosque Corrupto. Construye con la seed de la partida el sendero entre pinos negros: terreno,
// árboles secos, setas y charcos de savia, campamentos, hogueras, las tres pistas de Pipo, las tres raíces corruptas,
// el corazón del bosque y los pasos (vuelta a la Escalinata y subida a la cumbre). Luego hornea el NavMesh.
[DefaultExecutionOrder(-900)]
public class ForestGenerator : MonoBehaviour
{
    public static ForestGenerator Instance { get; private set; }

    [Header("Piezas")]
    public GameObject pinePrefab;
    public GameObject bonfirePrefab;

    [Header("Materiales")]
    public Material snowMaterial;
    public Material stoneMaterial;

    [Tooltip("Seed para probar la escena sin pasar por el menú.")]
    public int fallbackSeed = 12345;

    public int Seed { get; private set; }
    public ForestLayout.Result Layout { get; private set; }
    public Transform HeartCenter { get; private set; }
    public bool Built { get; private set; }

    public const string EntryMarker = "bosque_entrada";
    public const string HeartMarker = "claro_savia";

    private Material _path, _snow, _rock, _bark, _deadBark, _sap, _vein, _mushStem, _mushCap, _stone, _scarf, _cloth;
    private readonly Dictionary<Material, Material> _darkPine = new Dictionary<Material, Material>();
    private Transform _root;
    private Transform _player;
    private Rigidbody _playerRb;
    private CharacterStats _playerStats;
    private Vector3 _lastValid;
    private bool _hasValid;
    private readonly List<(Light light, float baseIntensity, float phase)> _pulses = new List<(Light, float, float)>();

    void Awake()
    {
        Instance = this;
        Seed = GameSession.Instance != null && GameSession.Instance.worldSeed != 0 ? GameSession.Instance.worldSeed : fallbackSeed;
        Layout = ForestLayout.Generate(Seed);
        Build();
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

    void Update()
    {
        float t = Time.time;
        foreach (var p in _pulses)
            if (p.light != null) p.light.intensity = p.baseIntensity * (0.7f + 0.3f * Mathf.Sin(t * 1.3f + p.phase));
    }

    // El héroe no puede meterse en la maleza: si cruza el borde vuelve al último punto válido.
    void FixedUpdate()
    {
        if (_player == null) return;
        if (_playerStats != null && _playerStats.IsDead) return;
        if (_playerRb != null && _playerRb.isKinematic) return;
        Vector3 p = _player.position;
        float e = Layout.Edge(p.x, p.z);
        if (e < -0.2f)
        {
            _lastValid = p;
            _hasValid = true;
            return;
        }
        if (e < 0.6f || !_hasValid) return;
        Vector3 back = _lastValid;
        back.y = Mathf.Max(back.y, Layout.Height(back.x, back.z) + 0.05f);
        if (_playerRb != null)
        {
            _playerRb.position = back;
            Vector3 v = _playerRb.linearVelocity;
            _playerRb.linearVelocity = new Vector3(0f, Mathf.Min(0f, v.y), 0f);
        }
        _player.position = back;
    }

    // ---------- Construcción ----------

    private void Build()
    {
        _root = new GameObject("Bosque_Generado").transform;
        _root.SetParent(transform, false);
        Materials();

        var terrain = new GameObject("Terreno");
        terrain.transform.SetParent(_root, false);
        Mesh mesh = MountainMeshes.Terrain(Layout.Height, Layout.Edge, -ForestLayout.HalfWidth, ForestLayout.HalfWidth,
            -ForestLayout.Margin, ForestLayout.Length + ForestLayout.Margin, "Terreno_Bosque");
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = terrain.AddComponent<MeshRenderer>();
        // Nieve sucia con relieve; el sendero, pisado y más oscuro (hojas de pino y savia).
        Material relief = SnowSurface.For(10f, "Nieve del bosque");
        if (relief != null) relief.SetColor("_BaseColor", new Color(0.84f, 0.87f, 0.88f));
        Material trodden = SnowSurface.For(10f, "Sendero del bosque");
        if (trodden != null) trodden.SetColor("_BaseColor", new Color(0.6f, 0.57f, 0.54f));
        mr.sharedMaterials = new[] { trodden != null ? trodden : _path, relief != null ? relief : _snow, _rock };
        if (mesh.tangents == null || mesh.tangents.Length == 0) mesh.RecalculateTangents();
        terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
        terrain.isStatic = true;

        Props();
        Clearings();
        Gates();
        BakeNavMesh();
    }

    private void Materials()
    {
        _snow = snowMaterial != null ? snowMaterial : MenuRuins.Lit("Nieve", new Color(0.8f, 0.84f, 0.82f), 0.2f);
        _path = MenuRuins.Lit("Sendero del bosque", new Color(0.45f, 0.45f, 0.43f), 0.1f);
        _rock = MenuRuins.Lit("Roca musgosa", new Color(0.24f, 0.28f, 0.26f), 0.12f);
        _stone = stoneMaterial != null ? stoneMaterial : MenuRuins.Lit("Piedra", new Color(0.45f, 0.47f, 0.5f), 0.1f);
        _bark = MenuRuins.Lit("Corteza", new Color(0.2f, 0.15f, 0.12f), 0.08f);
        _deadBark = MenuRuins.Lit("Corteza seca", new Color(0.13f, 0.11f, 0.12f), 0.05f);
        _sap = MenuRuins.Lit("Savia negra", new Color(0.05f, 0.03f, 0.06f), 0.7f);
        _vein = MenuRuins.Lit("Vena de savia", new Color(0.25f, 0.9f, 0.3f), 0.6f, 0f, new Color(0.6f, 2.2f, 0.5f));
        _mushStem = MenuRuins.Lit("Pie de seta", new Color(0.75f, 0.72f, 0.65f), 0.2f);
        _mushCap = MenuRuins.Lit("Seta de savia", new Color(0.4f, 0.85f, 0.55f), 0.5f, 0f, new Color(0.3f, 1.1f, 0.55f));
        _scarf = MenuRuins.Lit("Bufanda de Pipo", new Color(0.78f, 0.12f, 0.12f), 0.15f);
        _cloth = MenuRuins.Lit("Banderín verde", new Color(0.2f, 0.34f, 0.22f), 0.1f);
        _cloth.SetFloat("_Cull", 0f);
        _scarf.SetFloat("_Cull", 0f);
    }

    private Transform Group(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(_root, false);
        return t;
    }

    private void Props()
    {
        Transform forest = Group("Pinos_Negros");
        if (pinePrefab != null)
            foreach (var s in Layout.pines)
            {
                GameObject pine = Instantiate(pinePrefab, s.position, Quaternion.Euler(0f, s.yaw, 0f), forest);
                pine.transform.localScale = Vector3.one * s.scale;
                Darken(pine);
                // Solo los pinos del borde chocan: los de dentro del bosque no los alcanza nadie.
                if (Layout.Edge(s.position.x, s.position.z) > 14f)
                    foreach (Collider c in pine.GetComponentsInChildren<Collider>()) Destroy(c);
            }

        Transform dead = Group("Arboles_Secos");
        foreach (var s in Layout.deadTrees) DeadTree(dead, s.position, s.yaw, s.scale, true);

        Transform rocks = Group("Rocas");
        var rockMeshes = new Mesh[5];
        for (int i = 0; i < rockMeshes.Length; i++) rockMeshes[i] = MountainMeshes.Rock(SeedUtil.Combine(Seed, 1700 + i), 0.5f + i * 0.07f);
        int n = 0;
        foreach (var s in Layout.rocks)
        {
            var go = new GameObject("Roca");
            go.transform.SetParent(rocks, false);
            go.transform.SetPositionAndRotation(s.position, Quaternion.Euler(0f, s.yaw, 0f));
            go.transform.localScale = new Vector3(s.scale * 1.3f, s.scale, s.scale);
            Mesh m = rockMeshes[n++ % rockMeshes.Length];
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = _rock;
            go.AddComponent<MeshCollider>().sharedMesh = m;
            go.isStatic = true;
        }

        Transform mush = Group("Setas");
        foreach (var s in Layout.mushrooms) Mushroom(mush, s);

        Transform pools = Group("Charcos_Savia");
        foreach (var s in Layout.pools) Pool(pools, s.position, s.scale, s.yaw);
    }

    // Copia oscura (verde casi negro) de cada material del pino: el bosque corrupto.
    private void Darken(GameObject pine)
    {
        foreach (Renderer r in pine.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material src = mats[i];
                if (src == null) continue;
                if (!_darkPine.TryGetValue(src, out Material dark))
                {
                    dark = new Material(src) { name = src.name + " (corrupto)", enableInstancing = true };
                    Color c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.white;
                    // La nieve de las ramas queda gris; las hojas, verde negruzco.
                    bool snowy = c.r > 0.75f && c.g > 0.75f && c.b > 0.75f;
                    Color to = snowy ? new Color(0.72f, 0.76f, 0.78f) : new Color(c.r * 0.22f, c.g * 0.34f, c.b * 0.26f);
                    if (dark.HasProperty("_BaseColor")) dark.SetColor("_BaseColor", to);
                    _darkPine[src] = dark;
                }
                mats[i] = dark;
            }
            r.sharedMaterials = mats;
        }
    }

    // Árbol seco: tronco que se afina y ramas torcidas (cilindros), sin hojas.
    private void DeadTree(Transform parent, Vector3 at, float yaw, float scale, bool collider)
    {
        var go = new GameObject("Arbol_Seco");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = Vector3.one * scale;
        var rng = new DeterministicRng(SeedUtil.Combine(Seed, Mathf.RoundToInt(at.x * 13f + at.z * 7f)));
        float h = rng.Range(4.5f, 6.5f);
        Limb(go.transform, Vector3.zero, new Vector3(rng.Range(-6f, 6f), 0f, rng.Range(-6f, 6f)), 0.32f, h);
        int branches = rng.NextInt(3, 6);
        for (int i = 0; i < branches; i++)
        {
            float y = h * rng.Range(0.45f, 0.9f);
            float a = rng.Range(0f, 360f);
            Limb(go.transform, new Vector3(0f, y, 0f), new Vector3(rng.Range(35f, 70f), a, 0f), 0.1f, rng.Range(1.2f, 2.4f));
        }
        if (!collider) return;
        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.35f;
        col.height = h;
        col.center = new Vector3(0f, h * 0.5f, 0f);
    }

    private void Limb(Transform parent, Vector3 basePos, Vector3 euler, float radius, float length)
    {
        var pivot = new GameObject("Rama").transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = basePos;
        pivot.localRotation = Quaternion.Euler(euler);
        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(c.GetComponent<Collider>());
        c.transform.SetParent(pivot, false);
        c.transform.localPosition = new Vector3(0f, length * 0.5f, 0f);
        c.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        c.GetComponent<Renderer>().sharedMaterial = _deadBark;
    }

    private void Mushroom(Transform parent, ForestLayout.Spot s)
    {
        var go = new GameObject("Seta");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(s.position, Quaternion.Euler(0f, s.yaw, 0f));
        go.transform.localScale = Vector3.one * s.scale;
        var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(stem.GetComponent<Collider>());
        stem.transform.SetParent(go.transform, false);
        stem.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        stem.transform.localScale = new Vector3(0.07f, 0.12f, 0.07f);
        stem.GetComponent<Renderer>().sharedMaterial = _mushStem;
        var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(cap.GetComponent<Collider>());
        cap.transform.SetParent(go.transform, false);
        cap.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        cap.transform.localScale = new Vector3(0.26f, 0.1f, 0.26f);
        cap.GetComponent<Renderer>().sharedMaterial = _mushCap;
    }

    private GameObject Pool(Transform parent, Vector3 at, float size, float yaw)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(p.GetComponent<Collider>());
        p.name = "Charco_Savia";
        p.transform.SetParent(parent, false);
        p.transform.SetPositionAndRotation(at + Vector3.up * 0.03f, Quaternion.Euler(0f, yaw, 0f));
        p.transform.localScale = new Vector3(size, 0.02f, size * 0.7f);
        p.GetComponent<Renderer>().sharedMaterial = _sap;
        return p;
    }

    private Light GlowLight(Transform parent, Vector3 at, Color color, float range, float intensity, bool pulse)
    {
        var go = new GameObject("Luz_Savia");
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        if (pulse) _pulses.Add((l, intensity, at.z * 0.11f));
        return l;
    }

    // ---------- Claros ----------

    private void Clearings()
    {
        GameDatabase db = GameDatabase.Instance;
        Transform camps = Group("Campamentos");
        Transform fires = Group("Hogueras");
        Transform clues = Group("Pistas");
        Transform roots = Group("Raices");
        foreach (ForestLayout.Clearing c in Layout.clearings)
        {
            float progress = c.center.z / ForestLayout.Length;
            Vector3 ground = Layout.Ground(c.center.x, c.center.z);
            switch (c.kind)
            {
                case ForestLayout.ClearingKind.Entry:
                    Marker(_root, EntryMarker, QuestObjective.Llegar, ground, 14f, "El Bosque Corrupto: los pinos sangran savia negra");
                    break;
                case ForestLayout.ClearingKind.Camp:
                    if (Camp(camps, db, "bosque_" + (c.index + 1), ground, c.radius * 0.55f, progress) != null)
                        GlowLight(_root, ground + Vector3.up * 3f, new Color(0.45f, 1f, 0.5f), 14f, 1.6f, true);
                    break;
                case ForestLayout.ClearingKind.Bonfire:
                    Bonfire(fires, ground, ForestLayout.BonfirePrefix + (c.index + 1));
                    break;
                case ForestLayout.ClearingKind.Clue:
                    Clue(clues, c, ground);
                    break;
                case ForestLayout.ClearingKind.Root:
                    {
                        EnemyCamp guard = Camp(camps, db, "bosque_raiz_" + (c.index + 1), ground + new Vector3(0f, 0f, -3f), c.radius * 0.5f, progress);
                        if (guard != null) guard.groupSize = new Vector2Int(5, 7);
                        Root(roots, c, ground);
                        break;
                    }
                case ForestLayout.ClearingKind.Heart:
                    Heart(c, ground);
                    break;
            }
        }
    }

    private EnemyCamp Camp(Transform parent, GameDatabase db, string id, Vector3 at, float radius, float progress)
    {
        if (db == null) return null;
        var go = new GameObject("Campamento_" + id);
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        EnemyCamp camp = go.AddComponent<EnemyCamp>();
        camp.campId = id;
        camp.melee = db.FindEnemy("Corrupt_Melee");
        camp.ranged = db.FindEnemy("Corrupt_Ranged");
        var elites = new List<EnemyDefinition>();
        foreach (string e in new[] { "Corrupt_Knight", "Corrupt_Mage", "Corrupt_Ninja", "Corrupt_Viking" })
        {
            EnemyDefinition d = db.FindEnemy(e);
            if (d != null) elites.Add(d);
        }
        camp.elites = elites.ToArray();
        camp.eliteMinPlayerLevel = 1;
        camp.eliteChance = Mathf.Lerp(0.3f, 0.65f, progress);
        camp.palette = db.palette;
        camp.groupSize = new Vector2Int(5, 8);
        var variants = new List<EnemyDefinition>();
        foreach (string e in ForestEnemies)
        {
            EnemyDefinition d = db.FindEnemy(e);
            if (d != null) variants.Add(d);
        }
        if (variants.Count == 0)
        {
            EnemyDefinition knight = db.FindEnemy("Corrupt_Knight");
            if (knight != null) variants.Add(knight);
        }
        camp.meleeVariants = variants.ToArray();
        camp.variantChance = 0.55f;
        camp.rangedFraction = 0.3f;
        int lvl = 8 + Mathf.RoundToInt(progress * 4f);
        camp.monsterLevel = new Vector2Int(lvl, lvl + 1);
        camp.championChance = 0.1f;
        camp.radius = Mathf.Max(5f, radius);
        camp.activateDistance = 36f;
        return camp;
    }

    // Enemigos propios del bosque (capítulo II); si aún no existen, salen caballeros corruptos.
    public static readonly string[] ForestEnemies = { "Corrupt_Woodcutter", "Corrupt_Sapspitter", "Corrupt_Stalker" };

    private void Bonfire(Transform parent, Vector3 at, string id)
    {
        if (bonfirePrefab == null) return;
        GameObject go = Instantiate(bonfirePrefab, at, Quaternion.identity, parent);
        go.name = id;
        go.transform.localScale = Vector3.one * 0.9f;
        Bonfire b = go.GetComponent<Bonfire>();
        if (b == null) b = go.AddComponent<Bonfire>();
        b.id = id;
        b.displayName = "Hoguera del bosque";
    }

    private QuestMarker Marker(Transform parent, string id, QuestObjective type, Vector3 at, float radius, string message,
        string displayName = "", string action = "")
    {
        var go = new GameObject("Marca_" + id);
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        QuestMarker m = go.AddComponent<QuestMarker>();
        m.id = id;
        m.type = type;
        m.radius = radius;
        m.message = message;
        if (displayName.Length > 0) m.displayName = displayName;
        if (action.Length > 0) m.actionLabel = action;
        return m;
    }

    // Pista de Pipo: huellas, la bufanda o el dibujo, con un brillo cálido para que se vea entre la niebla.
    private void Clue(Transform parent, ForestLayout.Clearing c, Vector3 ground)
    {
        var (id, name, text) = ForestLayout.Clues[Mathf.Clamp(c.index, 0, ForestLayout.Clues.Length - 1)];
        QuestMarker m = Marker(parent, id, QuestObjective.Interactuar, ground, 3f, text, name, "Examinar");
        Transform t = m.transform;
        var col = m.gameObject.AddComponent<CapsuleCollider>();
        col.radius = 0.8f;
        col.height = 1.4f;
        col.center = new Vector3(0f, 0.7f, 0f);
        Material dark = MenuRuins.Lit("Huella", new Color(0.3f, 0.32f, 0.36f), 0.1f);
        switch (c.index)
        {
            case 0:
                for (int i = 0; i < 9; i++)
                {
                    float z = -3f + i * 0.7f;
                    Flat(t, new Vector3((i & 1) == 0 ? -0.15f : 0.15f, 0.03f, z), new Vector3(0.18f, 0.01f, 0.26f), dark);
                    if (i % 2 == 0) Flat(t, new Vector3(1.1f + ((i & 2) == 0 ? -0.3f : 0.3f), 0.03f, z + 0.3f), new Vector3(0.45f, 0.01f, 0.6f), dark);
                }
                break;
            case 1:
                DeadTree(t, new Vector3(0.8f, 0f, 0.4f) + ground, 30f, 0.8f, false);
                var scarf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(scarf.GetComponent<Collider>());
                scarf.transform.SetParent(t, false);
                scarf.transform.localPosition = new Vector3(0.8f, 1.6f, 0.4f);
                scarf.transform.localRotation = Quaternion.Euler(0f, 30f, 70f);
                scarf.transform.localScale = new Vector3(1.1f, 0.18f, 0.04f);
                scarf.GetComponent<Renderer>().sharedMaterial = _scarf;
                break;
            default:
                for (int i = 0; i < 10; i++)
                {
                    float a = i / 10f * Mathf.PI * 2f;
                    Flat(t, new Vector3(Mathf.Sin(a) * 1.3f, 0.03f, Mathf.Cos(a) * 1.3f), new Vector3(0.1f, 0.01f, 0.5f), dark, a * Mathf.Rad2Deg + 90f);
                }
                Flat(t, new Vector3(0f, 0.03f, -0.2f), new Vector3(0.12f, 0.01f, 1.4f), dark, 35f);
                Flat(t, new Vector3(0.25f, 0.03f, 0.2f), new Vector3(0.12f, 0.01f, 0.9f), dark, -40f);
                break;
        }
        GlowLight(t, ground + Vector3.up * 1.2f, new Color(1f, 0.82f, 0.5f), 5f, 1.4f, false);
        m.Activated += () => { foreach (Light l in t.GetComponentsInChildren<Light>()) l.enabled = false; };
        if (m.Used) foreach (Light l in t.GetComponentsInChildren<Light>()) l.enabled = false;
    }

    private void Flat(Transform parent, Vector3 local, Vector3 size, Material mat, float yaw = 0f)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localPosition = local;
        q.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        q.transform.localScale = size;
        q.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // Raíz corrupta: brota del suelo con venas de savia; al arrancarla se seca y se hunde.
    private void Root(Transform parent, ForestLayout.Clearing c, Vector3 ground)
    {
        QuestMarker m = Marker(parent, "raiz_" + (c.index + 1), QuestObjective.Interactuar, ground, 3f,
            "La raíz cruje y se seca. La savia deja de subir por este lado del bosque.", "Raíz corrupta", "Arrancar");
        var col = m.gameObject.AddComponent<CapsuleCollider>();
        col.radius = 1.2f;
        col.height = 3f;
        col.center = new Vector3(0f, 1.5f, 0f);
        var root = m.gameObject.AddComponent<ForestRoot>();
        var rng = new DeterministicRng(SeedUtil.Combine(Seed, 3100 + c.index));
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * 360f + rng.Range(-20f, 20f);
            var pivot = new GameObject("Raiz").transform;
            pivot.SetParent(m.transform, false);
            pivot.localRotation = Quaternion.Euler(rng.Range(30f, 60f), a, 0f);
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(trunk.GetComponent<Collider>());
            trunk.transform.SetParent(pivot, false);
            float len = rng.Range(1.6f, 2.8f);
            trunk.transform.localPosition = new Vector3(0f, len * 0.5f, 0f);
            trunk.transform.localScale = new Vector3(0.35f, len * 0.5f, 0.35f);
            trunk.GetComponent<Renderer>().sharedMaterial = _bark;
            var vein = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(vein.GetComponent<Collider>());
            vein.transform.SetParent(pivot, false);
            vein.transform.localPosition = new Vector3(0f, len * 0.5f, 0.15f);
            vein.transform.localScale = new Vector3(0.06f, len * 0.45f, 0.06f);
            vein.GetComponent<Renderer>().sharedMaterial = _vein;
            root.parts.Add(pivot);
            root.veins.Add(vein.GetComponent<Renderer>());
        }
        root.light = GlowLight(m.transform, ground + Vector3.up * 1.5f, new Color(0.4f, 1f, 0.45f), 9f, 2.4f, true);
        root.pool = Pool(m.transform, ground, 4.5f, rng.Range(0f, 360f));
        root.Bind(m);
    }

    // Corazón del bosque: claro hundido con un árbol madre retorcido, charcos de savia y anillo de árboles secos.
    private void Heart(ForestLayout.Clearing c, Vector3 ground)
    {
        Transform heart = Group("Corazon_Bosque");
        HeartCenter = heart;
        heart.position = ground;
        Marker(heart, HeartMarker, QuestObjective.Llegar, ground, c.radius * 0.8f, "El corazón del bosque. Alguien canta entre los árboles...");
        Pool(heart, ground + new Vector3(0f, 0f, 6f), 9f, 0f);
        var rng = new DeterministicRng(SeedUtil.Combine(Seed, 3300));
        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.PI * 2f;
            Vector3 p = ground + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (c.radius + 1.5f);
            if (Mathf.Cos(a) < -0.85f) continue; // la entrada (al sur)
            DeadTree(heart, new Vector3(p.x, Layout.Height(p.x, p.z), p.z), rng.Range(0f, 360f), rng.Range(1.4f, 2f), true);
        }
        // Árbol madre: un tronco enorme con venas de savia, al fondo del claro.
        Vector3 mother = ground + new Vector3(0f, 0f, c.radius - 4f);
        var trunk = new GameObject("Arbol_Madre").transform;
        trunk.SetParent(heart, false);
        trunk.position = mother;
        for (int i = 0; i < 5; i++)
        {
            float a = i * 72f;
            var pivot = new GameObject("Tronco").transform;
            pivot.SetParent(trunk, false);
            pivot.localRotation = Quaternion.Euler(8f, a, 0f);
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(cyl.GetComponent<Collider>());
            cyl.transform.SetParent(pivot, false);
            cyl.transform.localPosition = new Vector3(0f, 6f, 0.6f);
            cyl.transform.localScale = new Vector3(1.6f, 7f, 1.6f);
            cyl.GetComponent<Renderer>().sharedMaterial = _deadBark;
            var vein = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(vein.GetComponent<Collider>());
            vein.transform.SetParent(pivot, false);
            vein.transform.localPosition = new Vector3(0f, 5f, 1.35f);
            vein.transform.localScale = new Vector3(0.18f, 5f, 0.18f);
            vein.GetComponent<Renderer>().sharedMaterial = _vein;
        }
        var tcol = trunk.gameObject.AddComponent<CapsuleCollider>();
        tcol.radius = 2.2f;
        tcol.height = 14f;
        tcol.center = new Vector3(0f, 7f, 0f);
        GlowLight(heart, mother + Vector3.up * 4f + Vector3.back * 3f, new Color(0.45f, 1f, 0.4f), 26f, 3.2f, true);
    }

    private void Gates()
    {
        Transform gates = Group("Pasos");
        ZoneGate back = ZoneGate.Build(gates, "Paso_Escalinata", Layout.backGate, 180f + Layout.PathYaw(3.5f), 8f, _stone, _cloth);
        back.targetScene = SceneIds.Stairs;
        back.arrival = ForestLayout.ArrivalFromForest;
        back.label = "Volver a la Escalinata";

        ZoneGate onward = ZoneGate.Build(gates, "Paso_Cumbre", Layout.onwardGate, Layout.PathYaw(ForestLayout.Length - 3.5f), 8f, _stone, _cloth);
        onward.targetScene = SceneIds.Summit;
        onward.arrival = ForestLayout.ArrivalFromForest;
        onward.label = "Grietas de la cumbre";
        onward.canPass = () => QuestLog.Done("la_madre_savia");
        onward.blockedMessage = "La savia cierra las grietas de la cumbre. Libera antes a Pipo.";
        ZoneArrival.Create(gates, ForestLayout.ArrivalFromSummit, Layout.onwardGate + Vector3.back * 6f + Vector3.up * 0.1f, 180f);
        ZoneArrival.Create(gates, ForestLayout.ArrivalFromStairs, Layout.entry + Vector3.up * 0.1f, Layout.PathYaw(14f));
    }

    private void BuildMap()
    {
        var map = new GameObject("Mapa").AddComponent<MapSystem>();
        map.transform.SetParent(transform, false);
        float half = ForestLayout.Length * 0.5f + 16f;
        Vector3 center = new Vector3(Layout.CenterX(ForestLayout.Length * 0.5f), 0f, ForestLayout.Length * 0.5f);
        var marks = new List<MapSystem.Marker>
        {
            MapSystem.Mark(MapSystem.MarkerKind.Exit, Layout.backGate, "Volver a la Escalinata"),
            MapSystem.Mark(MapSystem.MarkerKind.Exit, Layout.onwardGate, "Grietas de la cumbre"),
            MapSystem.Mark(MapSystem.MarkerKind.Throne, Layout.Find(ForestLayout.ClearingKind.Heart).center, "Corazón del bosque"),
        };
        map.SetupZone("bosque", "El Bosque Corrupto", center, half, 160, 18f, marks, Objective, Layout.entry);
    }

    // El objetivo del mapa: la próxima pista, raíz o el corazón, según la misión en curso.
    private Vector3? Objective()
    {
        foreach (QuestMarker m in GetComponentsInChildren<QuestMarker>())
            if (!m.Used && QuestLog.Expects(m.type, m.id)) return m.transform.position;
        return Layout.onwardGate;
    }

    private void BakeNavMesh()
    {
        NavMeshSurface surface = gameObject.GetComponent<NavMeshSurface>();
        if (surface == null) surface = gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();
    }

    // ---------- Héroe y ambiente ----------

    private void PlacePlayer()
    {
        GameObject p = GameObject.Find("Player");
        if (p == null) return;
        _player = p.transform;
        _playerRb = p.GetComponent<Rigidbody>();
        _playerStats = p.GetComponent<CharacterStats>();
        Quaternion rot = Quaternion.Euler(0f, Layout.PathYaw(14f), 0f);
        Vector3 pos = Layout.entry + Vector3.up * 0.1f;
        p.transform.SetPositionAndRotation(pos, rot);
        if (_playerRb != null) { _playerRb.position = pos; _playerRb.rotation = rot; }
        _lastValid = pos;
        _hasValid = true;
        PlayerRespawn respawn = p.GetComponent<PlayerRespawn>();
        ZoneArrival entry = ZoneArrival.Find(ForestLayout.ArrivalFromStairs);
        if (respawn != null && entry != null) respawn.respawnPoint = entry.transform;
        Camera cam = Camera.main;
        CameraFollow follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
        if (follow != null)
        {
            follow.target = p.transform;
            cam.transform.position = pos + follow.offset;
            cam.transform.LookAt(pos + Vector3.up * follow.lookHeight);
        }
    }

    private void Atmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.24f, 0.3f, 0.29f);
        RenderSettings.fogDensity = 0.011f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.36f, 0.42f, 0.4f);
        RenderSettings.ambientEquatorColor = new Color(0.26f, 0.3f, 0.28f);
        RenderSettings.ambientGroundColor = new Color(0.14f, 0.15f, 0.15f);
        foreach (Light l in FindObjectsByType<Light>())
            if (l.type == LightType.Directional)
            {
                l.intensity *= 0.55f;
                l.color = new Color(0.78f, 0.86f, 0.8f);
            }

        Camera cam = Camera.main;
        if (cam == null) return;
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, 400f);
        // Esporas de savia que flotan (verdes, brillan un poco) y alguna pavesa de nieve sucia.
        var go = new GameObject("Esporas");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 2f, 12f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 8f;
        main.startSpeed = 0.15f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.004f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 1f, 0.5f, 0.8f), new Color(0.9f, 1f, 0.8f, 0.5f));
        main.prewarm = true;
        var em = ps.emission;
        em.rateOverTime = 70f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(34f, 6f, 28f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.2f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = MenuRuins.Transparent("Espora", MenuRuins.Soft(32), new Color(0.7f, 1f, 0.6f, 0.9f), true);
        ps.Play();
    }
}
