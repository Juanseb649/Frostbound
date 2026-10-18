using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

// Capítulo I · Las Faldas. Construye con la seed de la partida el valle que sube hacia el Frostspire:
// terreno, sendero, rocas, pinos, mojones, cristales del Frost, hogueras, campamentos de corruptos y los dos pasos
// (bajada al poblado y el paso helado de la cima). Luego hornea el NavMesh y coloca al héroe en la entrada.
[DefaultExecutionOrder(-900)]
public class MountainGenerator : MonoBehaviour
{
    public static MountainGenerator Instance { get; private set; }

    [Header("Piezas")]
    public GameObject pinePrefab;
    public GameObject moundPrefab;
    public GameObject bonfirePrefab;

    [Header("Materiales")]
    public Material snowMaterial;
    public Material stoneMaterial;

    [Tooltip("Seed para probar la escena sin pasar por el menú.")]
    public int fallbackSeed = 12345;

    public int Seed { get; private set; }
    public MountainLayout.Result Layout { get; private set; }
    public EnemyCamp SummitCamp { get; private set; }
    public bool Built { get; private set; }

    private Material _path, _snow, _rock, _crystal, _clothRed, _clothBlue, _stone;
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
        Layout = MountainLayout.Generate(Seed);
        Build();
        PlacePlayer();
        Atmosphere();
        BuildMap();
        var fallen = new List<Vector3>();
        var frng = new DeterministicRng(SeedUtil.Combine(Seed, SeedUtil.FromString("faldas_caidos")));
        for (int i = 0; i < 8; i++)
        {
            float z = Mathf.Lerp(25f, MountainLayout.Length - 25f, (i + frng.Range(0.1f, 0.9f)) / 8f);
            float x = Layout.CenterX(z) + frng.Range(-1f, 1f) * (Layout.HalfWidthAt(z) - 1.5f);
            fallen.Add(Layout.Ground(x, z));
        }
        FallenWarriors.Scatter(transform, Seed, fallen);
        AdventureHUD.Ensure();
        Built = true;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (QuestLog.Stage(QuestLog.Ascent) < QuestLog.AscentFoothills) QuestLog.SetStage(QuestLog.Ascent, QuestLog.AscentFoothills);
    }

    void Update()
    {
        float t = Time.time;
        foreach (var p in _pulses)
            if (p.light != null) p.light.intensity = p.baseIntensity * (0.75f + 0.5f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 2.1f + p.phase)), 10f));

        UpdateHordes();
        if (SummitCamp != null && SummitCamp.Cleared && QuestLog.Stage(QuestLog.Ascent) == QuestLog.AscentFoothills)
        {
            QuestLog.SetStage(QuestLog.Ascent, QuestLog.AscentFoothillsDone);
            BonfireBanner.Show("LAS FALDAS, SUPERADAS", BonfireBanner.Victory);
            Notifications.Show("Los guardianes del paso cayeron. El paso helado lleva a la Escalinata.", FrostboundUI.Gold);
        }
    }

    // ---------- Hordas ----------

    // Cada cierto tiempo baja por el sendero una horda que va directa a por el héroe.
    private float _nextHorde = 45f;
    private int _hordeCount;
    private readonly List<EnemyCamp> _hordes = new List<EnemyCamp>();

    private void UpdateHordes()
    {
        if (_player == null || Time.timeSinceLevelLoad < _nextHorde) return;
        _hordes.RemoveAll(h => h == null || h.Cleared);
        _nextHorde = Time.timeSinceLevelLoad + Random.Range(70f, 100f);
        if (_hordes.Count >= 2 || (_playerStats != null && _playerStats.IsDead)) return;
        float z = Mathf.Clamp(_player.position.z + 32f, 30f, MountainLayout.Length - 30f);
        if (Mathf.Abs(z - _player.position.z) < 15f) z = Mathf.Clamp(_player.position.z - 30f, 20f, MountainLayout.Length - 30f);
        Vector3 at = Layout.Ground(Layout.CenterX(z), z);
        EnemyCamp horde = Camp(transform, GameDatabase.Instance, "faldas_horda_" + (++_hordeCount), at, 4f, Mathf.Clamp01(z / MountainLayout.Length));
        if (horde == null) return;
        horde.groupSize = new Vector2Int(4, 7);
        horde.variantChance = 0.5f;
        horde.respawnSeconds = 99999f;
        horde.activateDistance = 999f;
        horde.Spawn();
        foreach (EnemyBrain b in horde.Members) b.leashOverride = 90f;
        horde.AlertAll(null);
        _hordes.Add(horde);
        BonfireBanner.Show("¡SE ACERCA UNA HORDA!", new Color(0.9f, 0.4f, 0.35f));
    }

    // El héroe no puede salir del sendero ni de los claros: si cruza el borde vuelve al último punto válido.
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
        _root = new GameObject("Faldas_Generadas").transform;
        _root.SetParent(transform, false);
        Materials();

        var terrain = new GameObject("Terreno");
        terrain.transform.SetParent(_root, false);
        Mesh mesh = MountainMeshes.Terrain(Layout);
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = terrain.AddComponent<MeshRenderer>();
        // Nieve con relieve (las UV del terreno van a 10 m por unidad); el sendero pisado, con el mismo grano más gris.
        Material relief = SnowSurface.For(10f, "Nieve de Las Faldas");
        Material trodden = SnowSurface.For(10f, "Sendero pisado");
        if (trodden != null) trodden.SetColor("_BaseColor", new Color(0.8f, 0.84f, 0.9f));
        mr.sharedMaterials = new[] { trodden != null ? trodden : _path, relief != null ? relief : _snow, _rock };
        if (mesh.tangents == null || mesh.tangents.Length == 0) mesh.RecalculateTangents();
        terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
        terrain.isStatic = true;

        Props();
        Clearings();
        Gates();
        Peaks();
        BakeNavMesh();
    }

    private void Materials()
    {
        _snow = snowMaterial != null ? snowMaterial : MenuRuins.Lit("Nieve", new Color(0.93f, 0.96f, 1f), 0.2f);
        _path = MenuRuins.Lit("Sendero pisado", new Color(0.74f, 0.79f, 0.86f), 0.12f);
        _rock = MenuRuins.Lit("Roca de la montaña", new Color(0.33f, 0.37f, 0.45f), 0.15f);
        _stone = stoneMaterial != null ? stoneMaterial : MenuRuins.Lit("Piedra", new Color(0.5f, 0.52f, 0.56f), 0.1f);
        _crystal = QuartzMaterial.Get();
        _clothRed = MenuRuins.Lit("Banderín rojo", new Color(0.62f, 0.16f, 0.18f), 0.1f);
        _clothBlue = MenuRuins.Lit("Banderín azul", new Color(0.18f, 0.32f, 0.62f), 0.1f);
        _clothRed.SetFloat("_Cull", 0f);
        _clothBlue.SetFloat("_Cull", 0f);
    }

    private void Props()
    {
        Transform forest = Group("Pinos");
        if (pinePrefab != null)
            foreach (var s in Layout.pines)
            {
                GameObject pine = Instantiate(pinePrefab, s.position, Quaternion.Euler(0f, s.yaw, 0f), forest);
                pine.transform.localScale = Vector3.one * s.scale;
            }

        Transform rocks = Group("Rocas");
        var rockMeshes = new Mesh[6];
        for (int i = 0; i < rockMeshes.Length; i++) rockMeshes[i] = MountainMeshes.Rock(SeedUtil.Combine(Seed, 700 + i), 0.55f + i * 0.06f);
        int n = 0;
        foreach (var s in Layout.rocks) Rock(rocks, rockMeshes[n++ % rockMeshes.Length], s, true);
        foreach (var s in Layout.stones) Rock(rocks, rockMeshes[n++ % rockMeshes.Length], s, false);

        Transform mounds = Group("Montones_Nieve");
        if (moundPrefab != null)
            foreach (var s in Layout.mounds)
            {
                GameObject m = Instantiate(moundPrefab, s.position, Quaternion.Euler(0f, s.yaw, 0f), mounds);
                m.transform.localScale = new Vector3(s.scale * 1.4f, s.scale * 0.6f, s.scale);
            }

        Transform cairns = Group("Mojones");
        foreach (var s in Layout.cairns) Cairn(cairns, rockMeshes, s);

        Transform crystals = Group("Cristales_Frost");
        foreach (var s in Layout.crystals) Crystal(crystals, s);
    }

    private Transform Group(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(_root, false);
        return t;
    }

    private void Rock(Transform parent, Mesh mesh, MountainLayout.Spot s, bool collider)
    {
        var go = new GameObject("Roca");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(s.position, Quaternion.Euler(0f, s.yaw, 0f));
        go.transform.localScale = new Vector3(s.scale * 1.2f, s.scale, s.scale);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = _rock;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.isStatic = true;
    }

    // Mojón: tres piedras apiladas con un banderín.
    private void Cairn(Transform parent, Mesh[] meshes, MountainLayout.Spot s)
    {
        var go = new GameObject("Mojon");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(s.position, Quaternion.Euler(0f, s.yaw, 0f));
        float y = 0f;
        for (int i = 0; i < 3; i++)
        {
            float size = (0.75f - i * 0.18f) * s.scale;
            var stone = new GameObject("Piedra");
            stone.transform.SetParent(go.transform, false);
            stone.transform.localPosition = new Vector3(0f, y + size * 0.3f, 0f);
            stone.transform.localRotation = Quaternion.Euler(0f, i * 47f, 0f);
            stone.transform.localScale = new Vector3(size * 1.3f, size * 0.75f, size * 1.1f);
            stone.AddComponent<MeshFilter>().sharedMesh = meshes[i % meshes.Length];
            stone.AddComponent<MeshRenderer>().sharedMaterial = _stone;
            y += size * 0.55f;
        }
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(pole.GetComponent<Collider>());
        pole.transform.SetParent(go.transform, false);
        pole.transform.localPosition = new Vector3(0f, y + 0.6f, 0f);
        pole.transform.localScale = new Vector3(0.05f, 0.7f, 0.05f);
        pole.GetComponent<Renderer>().sharedMaterial = _stone;
        var flag = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(flag.GetComponent<Collider>());
        flag.transform.SetParent(go.transform, false);
        flag.transform.localPosition = new Vector3(0.28f, y + 1.05f, 0f);
        flag.transform.localScale = new Vector3(0.5f, 0.32f, 1f);
        flag.GetComponent<Renderer>().sharedMaterial = (Mathf.RoundToInt(s.yaw) & 1) == 0 ? _clothRed : _clothBlue;
        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.45f * s.scale;
        col.height = y + 0.4f;
        col.center = new Vector3(0f, col.height * 0.5f, 0f);
    }

    private void Crystal(Transform parent, MountainLayout.Spot s)
    {
        var go = new GameObject("Cristal");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(s.position, Quaternion.Euler(0f, s.yaw, 0f));
        Mesh octa = MenuRuins.Octahedron();
        int shards = 3;
        for (int i = 0; i < shards; i++)
        {
            var shard = new GameObject("Esquirla");
            shard.transform.SetParent(go.transform, false);
            float h = s.scale * (i == 0 ? 2.2f : 1.2f);
            shard.transform.localPosition = new Vector3((i - 1) * 0.35f * s.scale, h * 0.35f, (i == 1 ? 0.2f : -0.1f) * s.scale);
            shard.transform.localRotation = Quaternion.Euler((i - 1) * 18f, i * 40f, (i - 1) * -14f);
            shard.transform.localScale = new Vector3(0.45f * s.scale, h, 0.45f * s.scale);
            shard.AddComponent<MeshFilter>().sharedMesh = octa;
            shard.AddComponent<MeshRenderer>().sharedMaterial = _crystal;
        }
        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.4f * s.scale;
        col.height = 1.6f * s.scale;
        col.center = new Vector3(0f, col.height * 0.5f, 0f);
    }

    private void Clearings()
    {
        GameDatabase db = GameDatabase.Instance;
        Transform camps = Group("Campamentos");
        Transform fires = Group("Hogueras");
        int campCount = 0;
        foreach (MountainLayout.Clearing c in Layout.clearings) if (c.kind == MountainLayout.ClearingKind.Camp) campCount++;

        foreach (MountainLayout.Clearing c in Layout.clearings)
        {
            float progress = c.center.z / MountainLayout.Length;
            switch (c.kind)
            {
                case MountainLayout.ClearingKind.Camp:
                    {
                        EnemyCamp camp = Camp(camps, db, "faldas_" + (c.index + 1), c.center, c.radius * 0.55f, progress);
                        if (camp != null)
                        {
                            camp.eliteChance = Mathf.Lerp(0.2f, 0.6f, c.index / Mathf.Max(1f, campCount - 1f));
                            CampLight(c.center);
                        }
                        break;
                    }
                case MountainLayout.ClearingKind.Bonfire:
                    {
                        if (bonfirePrefab == null) break;
                        Vector3 p = c.center;
                        p.y = Layout.Height(p.x, p.z);
                        float pathX = Layout.CenterX(p.z);
                        Vector3 toPath = new Vector3(pathX - p.x, 0f, 0f);
                        float yaw = toPath.sqrMagnitude > 1f ? Mathf.Atan2(toPath.x, 0f) * Mathf.Rad2Deg : 180f;
                        GameObject go = Instantiate(bonfirePrefab, p, Quaternion.Euler(0f, yaw, 0f), fires);
                        string id = MountainLayout.BonfirePrefix + (c.index + 1);
                        go.name = id;
                        go.transform.localScale = Vector3.one * 0.9f;
                        Bonfire b = go.GetComponent<Bonfire>();
                        if (b == null) b = go.AddComponent<Bonfire>();
                        b.id = id;
                        b.displayName = "Hoguera de las Faldas";
                        break;
                    }
                case MountainLayout.ClearingKind.Ruins:
                    Ruins(c);
                    break;
                case MountainLayout.ClearingKind.Summit:
                    {
                        SummitCamp = Camp(camps, db, "faldas_cima", c.center, c.radius * 0.5f, 1.05f);
                        if (SummitCamp != null)
                        {
                            SummitCamp.groupSize = new Vector2Int(6, 8);
                            SummitCamp.eliteChance = 1f;
                            SummitCamp.ignoreCurseGate = true;
                            SummitCamp.championChance = 0.35f;
                            SummitCamp.respawnSeconds = 600f;
                            CampLight(c.center);
                        }
                        break;
                    }
            }
        }
    }

    private EnemyCamp Camp(Transform parent, GameDatabase db, string id, Vector3 center, float radius, float progress)
    {
        if (db == null) return null;
        var go = new GameObject("Campamento_" + id);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(center.x, Layout.Height(center.x, center.z), center.z);
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
        camp.palette = db.palette;
        camp.groupSize = new Vector2Int(5, 8);
        EnemyDefinition knight = db.FindEnemy("Corrupt_Knight");
        if (knight != null) { camp.meleeVariants = new[] { knight }; camp.variantChance = 0.4f; }
        camp.rangedFraction = 0.3f;
        int lvl = 3 + Mathf.RoundToInt(progress * 3f);
        camp.monsterLevel = new Vector2Int(lvl, lvl + 1);
        camp.championChance = 0.08f;
        camp.radius = Mathf.Max(5f, radius);
        camp.activateDistance = 40f;
        return camp;
    }

    // Luz azul del Frost sobre el campamento: late como el corazón de la montaña.
    private void CampLight(Vector3 center)
    {
        var go = new GameObject("Luz_Frost");
        go.transform.SetParent(_root, false);
        go.transform.position = new Vector3(center.x, Layout.Height(center.x, center.z) + 3f, center.z);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.45f, 0.72f, 1f);
        l.range = 16f;
        l.intensity = 2.2f;
        l.shadows = LightShadows.None;
        _pulses.Add((l, l.intensity, center.z * 0.13f));
    }

    // Ruinas de una civilización antigua: columnas rotas en círculo y una losa con grabados.
    private void Ruins(MountainLayout.Clearing c)
    {
        Transform ruins = Group("Ruinas");
        var rng = new DeterministicRng(SeedUtil.Combine(Seed, SeedUtil.FromString("ruinas_faldas")));
        int columns = 7;
        for (int i = 0; i < columns; i++)
        {
            float a = (i + rng.Range(-0.2f, 0.2f)) / columns * Mathf.PI * 2f;
            float d = c.radius * 0.72f;
            float x = c.center.x + Mathf.Sin(a) * d, z = c.center.z + Mathf.Cos(a) * d;
            if (Layout.Edge(x, z) > -1f) continue;
            float h = rng.Chance(0.35f) ? rng.Range(3.2f, 4.2f) : rng.Range(0.8f, 2.4f);
            var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            col.name = "Columna";
            col.transform.SetParent(ruins, false);
            col.transform.position = new Vector3(x, Layout.Height(x, z) + h * 0.5f - 0.1f, z);
            col.transform.localScale = new Vector3(0.9f, h * 0.5f, 0.9f);
            col.transform.rotation = Quaternion.Euler(rng.Range(-4f, 4f), rng.Range(0f, 360f), rng.Range(-4f, 4f));
            col.GetComponent<Renderer>().sharedMaterial = _stone;
            if (h < 2.5f && rng.Chance(0.6f))
            {
                var fallen = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fallen.name = "Columna_Caida";
                fallen.transform.SetParent(ruins, false);
                Vector3 off = new Vector3(Mathf.Sin(a + 1.3f), 0f, Mathf.Cos(a + 1.3f)) * 1.8f;
                Vector3 fp = new Vector3(x, 0f, z) + off;
                if (Layout.Edge(fp.x, fp.z) < -1f)
                {
                    fallen.transform.position = new Vector3(fp.x, Layout.Height(fp.x, fp.z) + 0.35f, fp.z);
                    fallen.transform.rotation = Quaternion.Euler(90f, a * Mathf.Rad2Deg + 70f, 0f);
                    fallen.transform.localScale = new Vector3(0.8f, rng.Range(0.9f, 1.4f), 0.8f);
                    fallen.GetComponent<Renderer>().sharedMaterial = _stone;
                }
                else Destroy(fallen);
            }
        }
        var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = "Losa_Grabada";
        slab.transform.SetParent(ruins, false);
        slab.transform.position = new Vector3(c.center.x, Layout.Height(c.center.x, c.center.z) + 1.1f, c.center.z);
        slab.transform.localScale = new Vector3(2.4f, 2.4f, 0.5f);
        slab.transform.rotation = Quaternion.Euler(0f, 180f, rng.Range(-3f, 3f));
        slab.GetComponent<Renderer>().sharedMaterial = _stone;
        var rune = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(rune.GetComponent<Collider>());
        rune.name = "Grabado";
        rune.transform.SetParent(slab.transform, false);
        rune.transform.localPosition = new Vector3(0f, 0.08f, 0.51f);
        rune.transform.localRotation = Quaternion.Euler(0f, 180f, 45f);
        rune.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
        rune.GetComponent<Renderer>().sharedMaterial = _crystal;
        CampLight(c.center);
    }

    private void Gates()
    {
        Transform gates = Group("Pasos");
        ZoneGate down = ZoneGate.Build(gates, "Paso_Poblado", Layout.bottomGate, 180f + Layout.PathYaw(3.5f), 8f, _stone, _clothRed);
        down.targetScene = SceneIds.Village;
        down.arrival = MountainLayout.ArrivalFromFoothills;
        down.label = "Bajar al poblado";

        ZoneGate up = ZoneGate.Build(gates, "Paso_Helado", Layout.summitGate, Layout.PathYaw(MountainLayout.Length - 3.5f), 8f, _stone, _clothBlue);
        up.targetScene = SceneIds.Stairs;
        up.arrival = StairsLayout.ArrivalFromFoothills;
        up.label = "Paso helado";
        up.canPass = () => QuestLog.Stage(QuestLog.Ascent) >= QuestLog.AscentFoothillsDone;
        up.blockedMessage = "Los guardianes del paso helado no te dejan pasar. Derrótalos primero.";
        ZoneArrival.Create(gates, StairsLayout.ArrivalFromStairs, Layout.summitGate + Vector3.back * 6f + Vector3.up * 0.1f, 180f);

        float yaw = Layout.PathYaw(14f);
        ZoneArrival.Create(gates, MountainLayout.ArrivalFromVillage, Layout.entry + Vector3.up * 0.1f, yaw);
    }

    // Mapa de Las Faldas: la franja del sendero, con la bajada al poblado y el paso helado.
    private void BuildMap()
    {
        var map = new GameObject("Mapa").AddComponent<MapSystem>();
        map.transform.SetParent(transform, false);
        float half = MountainLayout.Length * 0.5f + 18f;
        Vector3 center = new Vector3(Layout.CenterX(MountainLayout.Length * 0.5f), 0f, MountainLayout.Length * 0.5f);
        map.SetupZone("faldas", "Las Faldas del Frostspire", center, half, 160, 20f, new[]
        {
            MapSystem.Mark(MapSystem.MarkerKind.Exit, Layout.bottomGate, "Bajar al poblado"),
            MapSystem.Mark(MapSystem.MarkerKind.Exit, Layout.summitGate, "Paso helado"),
        }, () => Layout.summitGate, Layout.entry);
    }

    // El Frostspire al fondo: cumbres enormes al norte (sin colisión).
    private void Peaks()
    {
        Transform peaks = Group("Cumbres");
        int i = 0;
        foreach (var s in Layout.peaks)
        {
            Mesh mesh = MountainMeshes.Peak(SeedUtil.Combine(Seed, 900 + i++));
            var go = new GameObject("Cumbre");
            go.transform.SetParent(peaks, false);
            go.transform.SetPositionAndRotation(s.position, Quaternion.Euler(0f, s.yaw, 0f));
            go.transform.localScale = new Vector3(s.scale * 1.6f, s.scale, s.scale * 1.6f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { _snow };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
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
        ZoneArrival entry = ZoneArrival.Find(MountainLayout.ArrivalFromVillage);
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
        RenderSettings.fogColor = new Color(0.72f, 0.8f, 0.9f);
        RenderSettings.fogDensity = 0.0065f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.82f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.56f, 0.66f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.33f, 0.4f);

        Camera cam = Camera.main;
        if (cam == null) return;
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, 700f);
        var go = new GameObject("Ventisca");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 6f, 10f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 6f;
        main.startSpeed = 1.2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.maxParticles = 1400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.05f;
        main.startColor = new Color(1f, 1f, 1f, 0.85f);
        main.prewarm = true;
        var em = ps.emission;
        em.rateOverTime = 220f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(36f, 1f, 30f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        vel.y = new ParticleSystem.MinMaxCurve(-1.4f, -0.8f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.25f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = MenuRuins.Transparent("Copo", MenuRuins.Soft(32), new Color(1f, 1f, 1f, 0.9f), false);
        ps.Play();
    }
}
