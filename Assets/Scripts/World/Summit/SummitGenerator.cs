using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

// Capítulo III · La Cumbre del Frostspire. Construye con la seed de la partida la grieta de hielo que sube a la cámara
// del sello: paredes de hielo, agujas, cristales del Frost, pingüinos congelados, campamentos, hogueras, los tres
// altares (encenderlos abre el muro de hielo) y la cámara con el Corazón del Frost. Luego hornea el NavMesh.
[DefaultExecutionOrder(-900)]
public class SummitGenerator : MonoBehaviour
{
    public static SummitGenerator Instance { get; private set; }

    [Header("Piezas")]
    public GameObject bonfirePrefab;
    [Tooltip("Pingüino con esqueleto (pingüinos congelados, Bobby y los NPC de la zona).")]
    public GameObject npcPrefab;

    [Header("Materiales")]
    public Material snowMaterial;
    public Material stoneMaterial;

    public int fallbackSeed = 12345;

    public int Seed { get; private set; }
    public SummitLayout.Result Layout { get; private set; }
    public Transform ChamberCenter { get; private set; }
    public SealEncounter Encounter { get; private set; }
    public bool Built { get; private set; }

    public const string AscentQuest = "el_ascenso_final";
    public const string SealQuest = "el_sello";
    public const string EntryMarker = "cumbre_entrada";
    public const string ChamberMarker = "camara_sello";

    private Material _path, _snow, _ice, _iceDeep, _crystal, _stone, _rune, _fire, _cloth;
    private Transform _root;
    private Transform _player;
    private Rigidbody _playerRb;
    private CharacterStats _playerStats;
    private Vector3 _lastValid;
    private bool _hasValid;
    private GameObject _barrier;
    private readonly List<(Light light, float baseIntensity, float phase)> _pulses = new List<(Light, float, float)>();

    void Awake()
    {
        Instance = this;
        Seed = GameSession.Instance != null && GameSession.Instance.worldSeed != 0 ? GameSession.Instance.worldSeed : fallbackSeed;
        Layout = SummitLayout.Generate(Seed);
        Build();
        PlacePlayer();
        Atmosphere();
        BuildMap();
        AdventureHUD.Ensure();
        Built = true;
    }

    void Start()
    {
        QuestLog.Changed += OnQuest;
        RefreshBarrier();
    }

    void OnDestroy()
    {
        QuestLog.Changed -= OnQuest;
        if (Instance == this) Instance = null;
    }

    private void OnQuest(string id, int stage) => RefreshBarrier();

    // El muro de hielo se funde cuando arden los tres altares.
    public bool BarrierOpen => QuestLog.Done(AscentQuest);

    private void RefreshBarrier()
    {
        if (_barrier == null || !BarrierOpen || !_barrier.activeSelf) return;
        _barrier.SetActive(false);
        BonfireBanner.Show("EL MURO DE HIELO SE FUNDE", new Color(0.55f, 0.8f, 1f));
    }

    void Update()
    {
        float t = Time.time;
        foreach (var p in _pulses)
            if (p.light != null) p.light.intensity = p.baseIntensity * (0.75f + 0.5f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 2.1f + p.phase)), 10f));
    }

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
        if (e < 0.5f || !_hasValid) return;
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
        _root = new GameObject("Cumbre_Generada").transform;
        _root.SetParent(transform, false);
        Materials();

        var terrain = new GameObject("Terreno");
        terrain.transform.SetParent(_root, false);
        Mesh mesh = MountainMeshes.Terrain(Layout.Height, Layout.Edge, -SummitLayout.HalfWidth, SummitLayout.HalfWidth,
            -SummitLayout.Margin, SummitLayout.Length + SummitLayout.Margin, "Terreno_Cumbre");
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = terrain.AddComponent<MeshRenderer>();
        Material relief = SnowSurface.For(10f, "Nieve de la cumbre");
        if (relief != null) relief.SetColor("_BaseColor", new Color(0.86f, 0.92f, 1f));
        Material trodden = SnowSurface.For(10f, "Hielo pisado");
        if (trodden != null) trodden.SetColor("_BaseColor", new Color(0.7f, 0.8f, 0.92f));
        mr.sharedMaterials = new[] { trodden != null ? trodden : _path, relief != null ? relief : _snow, _ice };
        if (mesh.tangents == null || mesh.tangents.Length == 0) mesh.RecalculateTangents();
        terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
        terrain.isStatic = true;

        Props();
        Clearings();
        Barrier();
        Gates();
        BakeNavMesh();
    }

    private void Materials()
    {
        _snow = snowMaterial != null ? snowMaterial : MenuRuins.Lit("Nieve", new Color(0.9f, 0.95f, 1f), 0.2f);
        _path = MenuRuins.Lit("Hielo pisado", new Color(0.7f, 0.8f, 0.92f), 0.4f);
        _ice = MenuRuins.Lit("Pared de hielo", new Color(0.42f, 0.62f, 0.85f), 0.85f);
        _iceDeep = MenuRuins.Lit("Hielo profundo", new Color(0.25f, 0.42f, 0.72f), 0.9f, 0f, new Color(0.03f, 0.08f, 0.18f));
        _crystal = QuartzMaterial.Get();
        _stone = stoneMaterial != null ? stoneMaterial : MenuRuins.Lit("Piedra", new Color(0.5f, 0.52f, 0.58f), 0.1f);
        _rune = MenuRuins.Lit("Runa del sello", new Color(0.5f, 0.75f, 1f), 0.6f, 0f, new Color(0.4f, 1f, 2.4f));
        _fire = MenuRuins.Lit("Fuego azul", new Color(0.55f, 0.85f, 1f), 0.4f, 0f, new Color(0.8f, 1.8f, 3f));
        _cloth = MenuRuins.Lit("Banderín azul", new Color(0.18f, 0.32f, 0.62f), 0.1f);
        _cloth.SetFloat("_Cull", 0f);
    }

    private Transform Group(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(_root, false);
        return t;
    }

    private void Props()
    {
        Mesh octa = MenuRuins.Octahedron();
        Transform spires = Group("Agujas_Hielo");
        foreach (var s in Layout.spires)
        {
            var go = new GameObject("Aguja");
            go.transform.SetParent(spires, false);
            go.transform.SetPositionAndRotation(s.position + Vector3.up * s.scale, Quaternion.Euler(Random.Range(-8f, 8f), s.yaw, Random.Range(-8f, 8f)));
            go.transform.localScale = new Vector3(s.scale * 0.5f, s.scale * 2.2f, s.scale * 0.5f);
            go.AddComponent<MeshFilter>().sharedMesh = octa;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _iceDeep;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Transform crystals = Group("Cristales_Frost");
        foreach (var s in Layout.crystals) Crystal(crystals, s.position, s.yaw, s.scale);

        Transform rocks = Group("Rocas");
        var rockMeshes = new Mesh[4];
        for (int i = 0; i < rockMeshes.Length; i++) rockMeshes[i] = MountainMeshes.Rock(SeedUtil.Combine(Seed, 2700 + i), 0.6f + i * 0.06f);
        int n = 0;
        foreach (var s in Layout.rocks)
        {
            var go = new GameObject("Roca");
            go.transform.SetParent(rocks, false);
            go.transform.SetPositionAndRotation(s.position, Quaternion.Euler(0f, s.yaw, 0f));
            go.transform.localScale = new Vector3(s.scale * 1.2f, s.scale, s.scale);
            Mesh m = rockMeshes[n++ % rockMeshes.Length];
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = _ice;
            go.AddComponent<MeshCollider>().sharedMesh = m;
            go.isStatic = true;
        }

        Transform frozen = Group("Congelados");
        foreach (var s in Layout.frozen) Frozen(frozen, s);
    }

    private void Crystal(Transform parent, Vector3 at, float yaw, float scale)
    {
        var go = new GameObject("Cristal");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        Mesh octa = MenuRuins.Octahedron();
        for (int i = 0; i < 3; i++)
        {
            var shard = new GameObject("Esquirla");
            shard.transform.SetParent(go.transform, false);
            float h = scale * (i == 0 ? 2.2f : 1.2f);
            shard.transform.localPosition = new Vector3((i - 1) * 0.35f * scale, h * 0.35f, (i == 1 ? 0.2f : -0.1f) * scale);
            shard.transform.localRotation = Quaternion.Euler((i - 1) * 18f, i * 40f, (i - 1) * -14f);
            shard.transform.localScale = new Vector3(0.45f * scale, h, 0.45f * scale);
            shard.AddComponent<MeshFilter>().sharedMesh = octa;
            shard.AddComponent<MeshRenderer>().sharedMaterial = _crystal;
        }
        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.4f * scale;
        col.height = 1.6f * scale;
        col.center = new Vector3(0f, col.height * 0.5f, 0f);
    }

    // Un pingüino atrapado en un bloque de hielo: los que subieron antes y se quedaron aquí.
    private void Frozen(Transform parent, SummitLayout.Spot s)
    {
        var go = new GameObject("Pinguino_Congelado");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(s.position, Quaternion.Euler(0f, s.yaw, 0f));
        if (npcPrefab != null)
        {
            GameObject p = Instantiate(npcPrefab, go.transform);
            p.transform.localPosition = Vector3.up * 0.1f;
            p.transform.localRotation = Quaternion.Euler(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
            foreach (Collider c in p.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (MonoBehaviour b in p.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
            foreach (Renderer r in p.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = _iceDeep;
                r.sharedMaterials = mats;
            }
        }
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "Bloque";
        block.transform.SetParent(go.transform, false);
        block.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        block.transform.localScale = new Vector3(1.3f, 1.6f, 1.2f) * s.scale;
        block.transform.localRotation = Quaternion.Euler(0f, 12f, 3f);
        Material glass = MenuRuins.Transparent("Bloque de hielo", MenuRuins.Soft(8), new Color(0.65f, 0.85f, 1f, 0.35f), false);
        block.GetComponent<Renderer>().sharedMaterial = glass;
    }

    // ---------- Claros ----------

    private void Clearings()
    {
        GameDatabase db = GameDatabase.Instance;
        Transform camps = Group("Campamentos");
        Transform fires = Group("Hogueras");
        Transform altars = Group("Altares");
        foreach (SummitLayout.Clearing c in Layout.clearings)
        {
            float progress = c.center.z / SummitLayout.Length;
            Vector3 ground = Layout.Ground(c.center.x, c.center.z);
            switch (c.kind)
            {
                case SummitLayout.ClearingKind.Entry:
                    Marker(_root, EntryMarker, QuestObjective.Llegar, ground, 14f, "La Cumbre del Frostspire: el aire corta como el cristal");
                    break;
                case SummitLayout.ClearingKind.Camp:
                    if (Camp(camps, db, "cumbre_" + (c.index + 1), ground, c.radius * 0.55f, progress) != null) PulseLight(ground + Vector3.up * 3f, 16f, 2.2f);
                    break;
                case SummitLayout.ClearingKind.Bonfire:
                    Bonfire(fires, ground, SummitLayout.BonfirePrefix + (c.index + 1));
                    break;
                case SummitLayout.ClearingKind.Altar:
                    Altar(altars, c, ground);
                    break;
                case SummitLayout.ClearingKind.Chamber:
                    Chamber(c, ground);
                    break;
            }
        }
    }

    // Enemigos propios de la cumbre (capítulo III); si aún no existen, élites del capítulo I.
    public static readonly string[] SummitEnemies = { "Corrupt_Crystallized", "Corrupt_FrostSpawn" };

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
        camp.eliteChance = Mathf.Lerp(0.45f, 0.8f, progress);
        camp.palette = db.palette;
        camp.groupSize = new Vector2Int(5, 8);
        var variants = new List<EnemyDefinition>();
        foreach (string e in SummitEnemies)
        {
            EnemyDefinition d = db.FindEnemy(e);
            if (d != null) variants.Add(d);
        }
        if (variants.Count == 0)
            foreach (string e in new[] { "Corrupt_Knight", "Corrupt_Viking" })
            {
                EnemyDefinition d = db.FindEnemy(e);
                if (d != null) variants.Add(d);
            }
        camp.meleeVariants = variants.ToArray();
        camp.variantChance = 0.6f;
        camp.rangedFraction = 0.25f;
        int lvl = 14 + Mathf.RoundToInt(progress * 4f);
        camp.monsterLevel = new Vector2Int(lvl, lvl + 1);
        camp.championChance = 0.12f;
        camp.radius = Mathf.Max(5f, radius);
        camp.activateDistance = 34f;
        return camp;
    }

    private Light PulseLight(Vector3 at, float range, float intensity)
    {
        var go = new GameObject("Luz_Frost");
        go.transform.SetParent(_root, false);
        go.transform.position = at;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.45f, 0.72f, 1f);
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        _pulses.Add((l, intensity, at.z * 0.13f));
        return l;
    }

    private void Bonfire(Transform parent, Vector3 at, string id)
    {
        if (bonfirePrefab == null) return;
        GameObject go = Instantiate(bonfirePrefab, at, Quaternion.identity, parent);
        go.name = id;
        go.transform.localScale = Vector3.one * 0.9f;
        Bonfire b = go.GetComponent<Bonfire>();
        if (b == null) b = go.AddComponent<Bonfire>();
        b.id = id;
        b.displayName = "Hoguera de la cumbre";
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

    // Altar del sello: pedestal de piedra con un brasero apagado; al encenderlo arde con fuego azul.
    private void Altar(Transform parent, SummitLayout.Clearing c, Vector3 ground)
    {
        QuestMarker m = Marker(parent, "altar_" + (c.index + 1), QuestObjective.Interactuar, ground, 3f,
            "El altar arde con fuego azul. Algo, muy arriba, se estremece.", "Altar del sello", "Encender");
        m.notYetMessage = "Un altar antiguo, frío. Sin saber para qué sirve, no te atreves a tocarlo.";
        Transform t = m.transform;
        var baseStone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseStone.transform.SetParent(t, false);
        baseStone.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        baseStone.transform.localScale = new Vector3(2.4f, 0.25f, 2.4f);
        baseStone.GetComponent<Renderer>().sharedMaterial = _stone;
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillar.transform.SetParent(t, false);
        pillar.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        pillar.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
        pillar.GetComponent<Renderer>().sharedMaterial = _stone;
        var bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(bowl.GetComponent<Collider>());
        bowl.transform.SetParent(t, false);
        bowl.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        bowl.transform.localScale = new Vector3(1.1f, 0.1f, 1.1f);
        bowl.GetComponent<Renderer>().sharedMaterial = _stone;
        for (int i = 0; i < 4; i++)
        {
            var rune = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(rune.GetComponent<Collider>());
            rune.transform.SetParent(t, false);
            float a = i * 90f;
            rune.transform.localRotation = Quaternion.Euler(0f, a, 0f);
            rune.transform.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.95f, -0.41f);
            rune.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            rune.GetComponent<Renderer>().sharedMaterial = _rune;
        }
        // Llama (apagada hasta que se enciende).
        var flame = new GameObject("Llama");
        flame.transform.SetParent(t, false);
        flame.transform.localPosition = new Vector3(0f, 1.9f, 0f);
        Mesh octa = MenuRuins.Octahedron();
        for (int i = 0; i < 3; i++)
        {
            var f = new GameObject("Lengua");
            f.transform.SetParent(flame.transform, false);
            f.transform.localPosition = new Vector3((i - 1) * 0.18f, 0.1f * i, 0f);
            f.transform.localScale = new Vector3(0.25f, 0.7f - i * 0.12f, 0.25f);
            f.AddComponent<MeshFilter>().sharedMesh = octa;
            f.AddComponent<MeshRenderer>().sharedMaterial = _fire;
        }
        var l = flame.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.5f, 0.8f, 1f);
        l.range = 12f;
        l.intensity = 3f;
        l.shadows = LightShadows.None;
        flame.SetActive(m.Used);
        m.Activated += () => flame.SetActive(true);
    }

    // Muro de hielo delante de la cámara del sello: no deja pasar hasta que arden los tres altares.
    private void Barrier()
    {
        Vector3 at = Layout.barrier;
        float yaw = Layout.PathYaw(at.z);
        _barrier = new GameObject("Muro_Hielo");
        _barrier.transform.SetParent(_root, false);
        _barrier.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.SetParent(_barrier.transform, false);
        wall.transform.localPosition = new Vector3(0f, 3f, 0f);
        wall.transform.localScale = new Vector3(18f, 6f, 1.6f);
        wall.GetComponent<Renderer>().sharedMaterial = _iceDeep;
        var obstacle = _barrier.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
        obstacle.center = new Vector3(0f, 3f, 0f);
        obstacle.size = new Vector3(18f, 6f, 1.6f);
        obstacle.carving = true;
        Mesh octa = MenuRuins.Octahedron();
        for (int i = 0; i < 9; i++)
        {
            var spike = new GameObject("Pincho");
            spike.transform.SetParent(_barrier.transform, false);
            spike.transform.localPosition = new Vector3(-8f + i * 2f, 6f + (i % 2) * 0.8f, 0f);
            spike.transform.localScale = new Vector3(0.7f, 2.2f + (i % 3) * 0.6f, 0.7f);
            spike.AddComponent<MeshFilter>().sharedMesh = octa;
            spike.AddComponent<MeshRenderer>().sharedMaterial = _crystal;
        }
        if (BarrierOpen) _barrier.SetActive(false);
    }

    // Cámara del sello: explanada circular con un anillo de runas, columnas rotas y el Corazón del Frost en el centro.
    private void Chamber(SummitLayout.Clearing c, Vector3 ground)
    {
        Transform ch = Group("Camara_Sello");
        ch.position = ground;
        ChamberCenter = ch;
        Marker(ch, ChamberMarker, QuestObjective.Llegar, ground + Vector3.back * (c.radius * 0.4f), c.radius * 0.7f,
            "La cámara del sello. En el centro late algo enorme bajo el hielo.");
        // Anillo de runas en el suelo.
        int runes = 24;
        for (int i = 0; i < runes; i++)
        {
            float a = i / (float)runes * Mathf.PI * 2f;
            var r = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(r.GetComponent<Collider>());
            r.transform.SetParent(ch, false);
            r.transform.position = ground + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 9f + Vector3.up * 0.03f;
            r.transform.rotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            r.transform.localScale = new Vector3(0.25f, 0.02f, 1.2f);
            r.GetComponent<Renderer>().sharedMaterial = _rune;
        }
        // Columnas rotas alrededor.
        var rng = new DeterministicRng(SeedUtil.Combine(Seed, 4400));
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f;
            if (Mathf.Cos(a) < -0.85f) continue; // la entrada (al sur)
            Vector3 p = ground + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (c.radius - 1.5f);
            float h = rng.Chance(0.4f) ? rng.Range(5f, 7f) : rng.Range(1.5f, 3f);
            var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            col.transform.SetParent(ch, false);
            col.transform.position = new Vector3(p.x, Layout.Height(p.x, p.z) + h * 0.5f - 0.1f, p.z);
            col.transform.localScale = new Vector3(1.4f, h * 0.5f, 1.4f);
            col.GetComponent<Renderer>().sharedMaterial = _ice;
            if (i % 3 == 0) Crystal(ch, p + (ground - p).normalized * 1.8f, rng.Range(0f, 360f), 1.4f);
        }
        PulseLight(ground + Vector3.up * 6f, 34f, 3.5f);

        // Bobby, el Corazón del Frost y el sello.
        var enc = new GameObject("Encuentro_Sello");
        enc.transform.SetParent(ch, false);
        enc.transform.position = ground;
        Encounter = enc.AddComponent<SealEncounter>();
        Encounter.npcPrefab = npcPrefab;
        Encounter.radius = c.radius - 3f;
    }

    private void Gates()
    {
        Transform gates = Group("Pasos");
        ZoneGate back = ZoneGate.Build(gates, "Paso_Bosque", Layout.backGate, 180f + Layout.PathYaw(3.5f), 8f, _stone, _cloth);
        back.targetScene = SceneIds.Forest;
        back.arrival = ForestLayout.ArrivalFromSummit;
        back.label = "Bajar al Bosque Corrupto";
        ZoneArrival.Create(gates, SummitLayout.ArrivalFromForest, Layout.entry + Vector3.up * 0.1f, Layout.PathYaw(14f));
    }

    private void BuildMap()
    {
        var map = new GameObject("Mapa").AddComponent<MapSystem>();
        map.transform.SetParent(transform, false);
        float half = SummitLayout.Length * 0.5f + 16f;
        Vector3 center = new Vector3(Layout.CenterX(SummitLayout.Length * 0.5f), 0f, SummitLayout.Length * 0.5f);
        var marks = new List<MapSystem.Marker>
        {
            MapSystem.Mark(MapSystem.MarkerKind.Exit, Layout.backGate, "Bajar al Bosque Corrupto"),
            MapSystem.Mark(MapSystem.MarkerKind.Throne, Layout.Find(SummitLayout.ClearingKind.Chamber).center, "Cámara del sello"),
        };
        map.SetupZone("cumbre", "La Cumbre del Frostspire", center, half, 160, 18f, marks, Objective, Layout.entry);
    }

    private Vector3? Objective()
    {
        foreach (QuestMarker m in GetComponentsInChildren<QuestMarker>())
            if (!m.Used && QuestLog.Expects(m.type, m.id)) return m.transform.position;
        return Layout.Find(SummitLayout.ClearingKind.Chamber).center;
    }

    private void BakeNavMesh()
    {
        NavMeshSurface surface = gameObject.GetComponent<NavMeshSurface>();
        if (surface == null) surface = gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        // El muro se talla aparte (NavMeshObstacle): fuera del horneado para que, al fundirse, quede camino.
        bool wasActive = _barrier != null && _barrier.activeSelf;
        if (_barrier != null) _barrier.SetActive(false);
        surface.BuildNavMesh();
        if (_barrier != null) _barrier.SetActive(wasActive);
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
        ZoneArrival entry = ZoneArrival.Find(SummitLayout.ArrivalFromForest);
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
        RenderSettings.fogColor = new Color(0.42f, 0.52f, 0.7f);
        RenderSettings.fogDensity = 0.009f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.45f, 0.55f, 0.75f);
        RenderSettings.ambientEquatorColor = new Color(0.35f, 0.42f, 0.58f);
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.24f, 0.34f);
        foreach (Light l in FindObjectsByType<Light>())
            if (l.type == LightType.Directional)
            {
                l.intensity *= 0.7f;
                l.color = new Color(0.75f, 0.84f, 1f);
            }

        Camera cam = Camera.main;
        if (cam == null) return;
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, 500f);
        var go = new GameObject("Ventisca");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 6f, 10f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 5f;
        main.startSpeed = 1.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.maxParticles = 1800;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.05f;
        main.startColor = new Color(1f, 1f, 1f, 0.9f);
        main.prewarm = true;
        var em = ps.emission;
        em.rateOverTime = 320f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(36f, 1f, 30f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        vel.y = new ParticleSystem.MinMaxCurve(-1.6f, -1f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.3f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = MenuRuins.Transparent("Copo", MenuRuins.Soft(32), new Color(1f, 1f, 1f, 0.9f), false);
        ps.Play();
    }
}
