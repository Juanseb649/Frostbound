using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;

// Construye el exterior del poblado con la seed de la partida antes de que nada más arranque:
// castillo gótico, bosque, montones de nieve, posición de los campamentos y hogueras repartidas por la zona.
// Luego vuelve a hornear el NavMesh.
[DefaultExecutionOrder(-900)]
public class VillageWorldGenerator : MonoBehaviour
{
    [Header("Piezas")]
    public GameObject pinePrefab;
    public GameObject moundPrefab;
    public GameObject bonfirePrefab;

    public CastleBuilder castlePrefab;

    [Header("Escena")]
    [Tooltip("Suelo de la zona: se agranda para que quepa el castillo.")]
    public Transform ground;
    public NavMeshSurface navMesh;
    [Tooltip("Seed para probar la escena sin pasar por el menú.")]
    public int fallbackSeed = 12345;

    public int Seed { get; private set; }
    public VillageLayout.Result Layout { get; private set; }

    void Awake()
    {
        Seed = GameSession.Instance != null && GameSession.Instance.worldSeed != 0 ? GameSession.Instance.worldSeed : fallbackSeed;
        Generate();
    }

    private void Generate()
    {
        List<EnemyCamp> camps = FindObjectsByType<EnemyCamp>().OrderBy(c => c.campId).ToList();
        Layout = VillageLayout.Generate(Seed, camps.Select(c => c.transform.position).ToList());

        for (int i = 0; i < camps.Count && i < Layout.camps.Count; i++)
            camps[i].transform.position = Layout.camps[i];
        // Más caballeros corruptos en las hordas.
        EnemyDefinition knight = GameDatabase.Instance != null ? GameDatabase.Instance.FindEnemy("Corrupt_Knight") : null;
        if (knight != null)
            foreach (EnemyCamp c in camps) { c.meleeVariants = new[] { knight }; c.variantChance = 0.3f; }

        Transform root = new GameObject("Exterior_Generado").transform;
        root.SetParent(transform, false);

        if (ground != null)
        {
            float scale = VillageLayout.GroundVisualHalfSize * 2f / 10f;
            ground.localScale = new Vector3(scale, ground.localScale.y, scale);
            ApplyDetailedSnow(ground.GetComponent<Renderer>(), VillageLayout.GroundVisualHalfSize * 2f);
        }

        if (castlePrefab != null)
        {
            CastleBuilder castle = Instantiate(castlePrefab, root);
            castle.name = "Castillo";
            castle.Build(Layout.castle, Layout.town);
            BuildCastleGameplay(root, castle);
            BuildTrailhead(root, castle.stone);
        }

        SpawnBonfire(root, Layout.castleBonfire, Bonfire.CastleId, "Hoguera del castillo");
        for (int i = 0; i < Layout.bonfires.Count; i++)
            SpawnBonfire(root, Layout.bonfires[i], VillageLayout.BonfireId(i), "Hoguera");

        Transform forest = new GameObject("Bosque").transform;
        forest.SetParent(root, false);
        if (pinePrefab != null)
            foreach (VillageLayout.Spot s in Layout.pines)
            {
                GameObject pine = Instantiate(pinePrefab, s.position, Quaternion.Euler(0f, s.yaw, 0f), forest);
                pine.transform.localScale = Vector3.one * s.scale;
            }

        BuildBorder(root);

        Transform snow = new GameObject("Montones_Nieve").transform;
        snow.SetParent(root, false);
        if (moundPrefab != null)
            foreach (VillageLayout.Spot s in Layout.mounds)
            {
                GameObject mound = Instantiate(moundPrefab, s.position, Quaternion.Euler(0f, s.yaw, 0f), snow);
                mound.transform.localScale = new Vector3(s.scale * 1.4f, s.scale * 0.6f, s.scale);
            }

        if (navMesh != null)
        {
            // El volumen del NavMesh cubre toda la zona (el castillo y su poblado quedan lejos del centro).
            float size = VillageLayout.GroundHalfSize * 2f + 10f;
            navMesh.center = navMesh.transform.InverseTransformPoint(new Vector3(0f, 12f, 0f));
            navMesh.size = new Vector3(size, 44f, size);
            navMesh.BuildNavMesh();
        }

        FallenWarriors.Scatter(root, Seed, FallenSpots());

        var map = new GameObject("Mapa").AddComponent<MapSystem>();
        map.transform.SetParent(transform, false);
        map.Setup(Layout);
        AdventureHUD.Ensure();

        // La tabernera guarda el arcón del héroe; Tico compra y vende.
        foreach (NPCInteractable npc in FindObjectsByType<NPCInteractable>())
        {
            if (npc.displayName == "Tabernera Mora") npc.options = new List<NPCOption> { NPCOption.Hablar, NPCOption.Arcon };
            if (npc.displayName == "Mercader Tico") npc.options = new List<NPCOption> { NPCOption.Hablar, NPCOption.Comprar, NPCOption.Vender };
            if (npc.displayName == "Anciano Pingo")
            {
                npc.role = "Mago";
                npc.options = new List<NPCOption> { NPCOption.Hablar, NPCOption.Comprar };
            }
        }

        GameObject steve = GameObject.Find(MapSystem.SteveName);
        if (steve != null && steve.GetComponent<NPCInteractable>() != null && steve.GetComponent<QuestGiver>() == null) steve.AddComponent<QuestGiver>();
    }

    // Puerta sellada, interior (se genera al entrar), asedio de la ciudadela y la guardia del poblado en ruinas.
    private void BuildCastleGameplay(Transform root, CastleBuilder castle)
    {
        var entrance = new GameObject("Puerta_Castillo").AddComponent<CastleEntrance>();
        entrance.transform.SetParent(root, true);
        entrance.Setup(Layout.castle, castle.ice);

        CastleInterior interior = new GameObject("Interior_Castillo").AddComponent<CastleInterior>();
        interior.transform.SetParent(transform, false);
        interior.Seed = Seed;
        interior.stone = castle.stone; interior.trim = castle.trim; interior.roof = castle.roof; interior.glass = castle.glass;
        interior.gold = castle.gold; interior.ice = castle.ice; interior.door = castle.door; interior.lampGlow = castle.lampGlow;
        interior.cone = castle.cone; interior.prism = castle.prism;
        interior.ExitPoint = Layout.castle.Door + Quaternion.Euler(0f, Layout.castle.yaw, 0f) * Vector3.forward * 3.5f;
        interior.ExitRotation = Quaternion.Euler(0f, Layout.castle.yaw, 0f);

        CitadelSiege siege = new GameObject("Asedio_Ciudadela").AddComponent<CitadelSiege>();
        siege.transform.SetParent(root, true);
        siege.Setup(Layout.castle, Layout.town, Seed);

        GameDatabase db = GameDatabase.Instance;
        if (db == null) return;
        var guardGo = new GameObject("Campamento_Ciudadela");
        guardGo.transform.SetParent(root, true);
        guardGo.transform.position = Layout.castle.ToWorld(Layout.town.camp[0]);
        EnemyCamp guard = guardGo.AddComponent<EnemyCamp>();
        guard.campId = "ciudadela";
        guard.melee = db.FindEnemy("Corrupt_Melee");
        guard.ranged = db.FindEnemy("Corrupt_Ranged");
        guard.palette = db.palette;
        guard.groupSize = new Vector2Int(4, 6);
        guard.rangedFraction = 0.35f;
        guard.monsterLevel = new Vector2Int(2, 3);
        guard.radius = 10f;
        guard.activateDistance = 45f;
    }

    // Inicio del sendero del Frostspire al final del camino del norte: arco de piedra con banderines, mojones
    // a lo largo del camino y las cumbres de la montaña al fondo. Se abre cuando Steve te manda a la montaña.
    private void BuildTrailhead(Transform root, Material stone)
    {
        Transform t = new GameObject("Sendero_Frostspire").transform;
        t.SetParent(root, false);
        Material cloth = MenuRuins.Lit("Banderín rojo", new Color(0.62f, 0.16f, 0.18f), 0.1f);
        cloth.SetFloat("_Cull", 0f);
        Vector3 top = BuildTrailStairs(t, stone);
        ZoneGate gate = ZoneGate.Build(t, "Paso_Montana", top + new Vector3(0f, 0f, 4f), 0f, 8f, stone, cloth);
        gate.targetScene = SceneIds.Foothills;
        gate.arrival = MountainLayout.ArrivalFromVillage;
        gate.label = "Sendero del Frostspire";
        gate.canPass = () => QuestLog.Stage(QuestLog.Ascent) >= QuestLog.AscentTold;
        gate.blockedMessage = "Una ventisca cierra el sendero de la montaña. Quizá Steve sepa algo.";
        gate.onPass = () =>
        {
            if (QuestLog.Stage(QuestLog.Ascent) < QuestLog.AscentFoothills) QuestLog.SetStage(QuestLog.Ascent, QuestLog.AscentFoothills);
        };
        ZoneArrival.Create(t, MountainLayout.ArrivalFromFoothills, top + new Vector3(0f, 0.1f, -0.5f), 180f);
        // Llegada del pergamino de regreso: la plaza, donde empieza la partida.
        ZoneArrival.Create(root, TownPortal.ArrivalId, new Vector3(0f, 0.1f, -9f), 0f);

        var rng = new DeterministicRng(SeedUtil.Combine(Seed, SeedUtil.FromString("sendero_norte")));
        Mesh rock = MountainMeshes.Rock(SeedUtil.Combine(Seed, 77), 0.6f);
        for (float z = 48f; z < VillageLayout.Trailhead.z - TrailRun - 6f; z += rng.Range(24f, 34f))
        {
            float side = rng.Chance(0.5f) ? 1f : -1f;
            var cairn = new GameObject("Mojon");
            cairn.transform.SetParent(t, false);
            cairn.transform.position = new Vector3(side * 5.6f, 0f, z);
            float y = 0f;
            for (int i = 0; i < 3; i++)
            {
                float size = 0.75f - i * 0.18f;
                var piece = new GameObject("Piedra");
                piece.transform.SetParent(cairn.transform, false);
                piece.transform.localPosition = new Vector3(0f, y + size * 0.3f, 0f);
                piece.transform.localRotation = Quaternion.Euler(0f, i * 47f + z, 0f);
                piece.transform.localScale = new Vector3(size * 1.3f, size * 0.75f, size * 1.1f);
                piece.AddComponent<MeshFilter>().sharedMesh = rock;
                piece.AddComponent<MeshRenderer>().sharedMaterial = stone;
                y += size * 0.55f;
            }
        }

        Material snow = ground != null && ground.GetComponent<Renderer>() != null ? ground.GetComponent<Renderer>().sharedMaterial : null;
        if (snow == null) return;
        // El Frostspire: la cumbre mayor se apoya justo detrás de la plataforma del paso (la escalera sube a su ladera)
        // y las demás quedan detrás del acantilado. Todas son sólidas: no se pueden atravesar.
        float wall = VillageLayout.Trailhead.z + TrailBack;
        bool sceneMountain = PlaceSceneFrostspire(wall);
        for (int i = 0; i < 7; i++)
        {
            bool main = i == 3;
            // Con el Frostspire de la escena, las cumbres generadas solo rellenan los extremos.
            if (sceneMountain && i >= 2 && i <= 4) continue;
            float x = main ? 0f : (i - 3) * rng.Range(80f, 105f) + rng.Range(-15f, 15f);
            float s = main ? 170f : rng.Range(70f, 120f);
            Quaternion rot = Quaternion.Euler(0f, main ? 0f : rng.Range(0f, 360f), 0f);
            Vector3 scale = new Vector3(s * 1.5f, s, s * 1.5f);
            Mesh mesh = MountainMeshes.Peak(SeedUtil.Combine(Seed, 300 + i));
            float front = main ? FrontAtCenter(mesh, rot, scale) : FrontMost(mesh, rot, scale);
            // Distancia del centro al pie de la ladera (a la altura del suelo la ladera se mete ~4 m).
            float z = main ? wall - 3f + front : wall + rng.Range(8f, 40f) + front;
            var peak = new GameObject(main ? "Frostspire" : "Cumbre_Frostspire");
            peak.transform.SetParent(t, false);
            peak.transform.SetPositionAndRotation(new Vector3(x, -6f, z), rot);
            peak.transform.localScale = scale;
            peak.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = peak.AddComponent<MeshRenderer>();
            r.sharedMaterial = snow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            peak.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
    }

    // Dónde yacen los guerreros caídos: al pie de la escalera del Frostspire y en su rellano, ante el castillo,
    // junto a algunas hogueras y repartidos por el bosque.
    private List<Vector3> FallenSpots()
    {
        var rng = new DeterministicRng(SeedUtil.Combine(Seed, SeedUtil.FromString("caidos_sitios")));
        var spots = new List<Vector3>();
        Vector3 foot = TrailPoint(0f);
        for (int i = 0; i < 4; i++) spots.Add(foot + new Vector3(rng.Range(-7f, 7f), 0f, rng.Range(-9f, -3f)));
        spots.Add(TrailPoint(0.5f) + Vector3.up * 0.5f);
        Vector3 castle = Layout.castle.Foot;
        Vector3 toVillage = (-castle).normalized;
        for (int i = 0; i < 3; i++) spots.Add(castle + toVillage * rng.Range(6f, 14f) + new Vector3(rng.Range(-6f, 6f), 0f, rng.Range(-6f, 6f)));
        foreach (VillageLayout.Spot b in Layout.bonfires)
            if (rng.Chance(0.6f)) spots.Add(b.position + new Vector3(rng.Range(-5f, 5f), 0f, rng.Range(-5f, 5f)));
        for (int tries = 0, added = 0; tries < 200 && added < 6; tries++)
        {
            Vector3 p = VillageLayout.Polar(rng.Range(0f, 360f), rng.Range(90f, 260f));
            if (VillageLayout.OnRoad(p) || Mathf.Abs(p.x) > VillageLayout.PlayableHalfSize - 12f || Mathf.Abs(p.z) > VillageLayout.PlayableHalfSize - 12f) continue;
            bool nearCamp = false;
            foreach (Vector3 c in Layout.camps) if ((c - p).sqrMagnitude < 18f * 18f) nearCamp = true;
            if (nearCamp) continue;
            spots.Add(p);
            added++;
        }
        return spots;
    }

    // El Frostspire de la escena (tres conos con la cima nevada): la cumbre central se apoya detrás de la plataforma
    // del paso, de modo que la escalera sube a su ladera. Todos sus picos reciben colisión.
    private bool PlaceSceneFrostspire(float wall)
    {
        GameObject fs = GameObject.Find("The_Frostspire");
        if (fs == null) return false;
        Renderer main = null;
        foreach (Renderer r in fs.GetComponentsInChildren<Renderer>())
            if (r.name == "Pico" && (main == null || r.bounds.size.x > main.bounds.size.x)) main = r;
        if (main == null) return false;
        Bounds b = main.bounds;
        // A la altura del suelo la ladera ya se metió hacia dentro: radio / alto × lo enterrado.
        float buried = Mathf.Max(0f, -b.min.y);
        float frontAtGround = b.min.z + buried * (b.extents.z / Mathf.Max(1f, b.size.y));
        Vector3 shift = new Vector3(-b.center.x, 0f, wall + 0.5f - frontAtGround);
        fs.transform.position += shift;
        foreach (MeshFilter mf in fs.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        GameObject oldExit = GameObject.Find("Salida_HaciaFoothills");
        if (oldExit != null) oldExit.SetActive(false);
        return true;
    }

    // Base de la cumbre (anillo inferior, ya girado y escalado).
    private static List<Vector3> BaseRing(Mesh mesh, Quaternion rot, Vector3 scale)
    {
        var ring = new List<Vector3>();
        foreach (Vector3 v in mesh.vertices)
            if (v.y < 0.06f)
            {
                Vector3 w = rot * Vector3.Scale(v, scale);
                w.y = 0f;
                if (!ring.Exists(o => (o - w).sqrMagnitude < 0.01f)) ring.Add(w);
            }
        ring.Sort((a, b) => Mathf.Atan2(a.x, a.z).CompareTo(Mathf.Atan2(b.x, b.z)));
        return ring;
    }

    // Cuánto sobresale la base hacia el sur (−z) en el peor punto.
    private static float FrontMost(Mesh mesh, Quaternion rot, Vector3 scale)
    {
        float m = 0f;
        foreach (Vector3 w in BaseRing(mesh, rot, scale)) m = Mathf.Max(m, -w.z);
        return m;
    }

    // Cuánto sobresale la base hacia el sur justo en x = 0 (donde llega la escalera).
    private static float FrontAtCenter(Mesh mesh, Quaternion rot, Vector3 scale)
    {
        List<Vector3> ring = BaseRing(mesh, rot, scale);
        float best = 0f;
        for (int i = 0; i < ring.Count; i++)
        {
            Vector3 a = ring[i], b = ring[(i + 1) % ring.Count];
            if ((a.x <= 0f) == (b.x <= 0f) || Mathf.Approximately(a.x, b.x)) continue;
            float k = -a.x / (b.x - a.x);
            float z = Mathf.Lerp(a.z, b.z, k);
            if (z < 0f) best = Mathf.Max(best, -z);
        }
        return best > 0f ? best : FrontMost(mesh, rot, scale);
    }

    public const float TrailRise = 8f;      // altura de la plataforma del paso (en la ladera del Frostspire)
    public const float TrailBack = 12f;     // fondo de la plataforma desde el Trailhead
    public const float TrailRun = 30f;      // fondo (en z) que ocupa la escalera curva
    public const float TrailHalfWidth = 3f;
    public const float TrailStartX = -8f;  // la escalera arranca a un lado del camino y se curva hasta el centro

    // Escalinata de piedra al pie de la montaña: sube del camino a una plataforma donde está el arco del sendero.
    // Detrás, un acantilado sólido (rocas + colisión) impide atravesar la montaña. Devuelve el centro de la plataforma.
    private Vector3 BuildTrailStairs(Transform t, Material stone)
    {
        Vector3 head = VillageLayout.Trailhead;
        float z0 = head.z - TrailRun, z1 = head.z;          // la escalera ocupa [z0, z1]
        float platBack = head.z + TrailBack, platHalf = TrailHalfWidth + 4f;
        Transform stairs = new GameObject("Escalinata_Sendero").transform;
        stairs.SetParent(t, false);

        BuildCurvedFlight(stairs, stone);

        // Plataforma del paso, con murete alrededor (menos por donde llega la escalera).
        float platDepth = platBack - z1;
        Block(stairs, "Plataforma", new Vector3(0f, TrailRise * 0.5f, z1 + platDepth * 0.5f), new Vector3(platHalf * 2f, TrailRise, platDepth), stone, true);
        for (int side = -1; side <= 1; side += 2)
        {
            Block(stairs, "Murete", new Vector3(side * (platHalf - 0.4f), TrailRise + 0.6f, z1 + platDepth * 0.5f), new Vector3(0.8f, 1.2f, platDepth), stone, true);
            float w = platHalf - TrailHalfWidth - 0.8f;
            Block(stairs, "Murete", new Vector3(side * (TrailHalfWidth + 0.8f + w * 0.5f), TrailRise + 0.6f, z1 + 0.4f), new Vector3(w, 1.2f, 0.8f), stone, true);
        }

        // Acantilado: la ladera de la montaña es sólida de lado a lado del mundo.
        float half = VillageLayout.GroundHalfSize;
        var cliff = new GameObject("Acantilado_Frostspire");
        cliff.transform.SetParent(stairs, false);
        cliff.transform.position = new Vector3(0f, 20f, platBack + (half + 10f - platBack) * 0.5f);
        BoxCollider cc = cliff.AddComponent<BoxCollider>();
        cc.size = new Vector3(half * 2f + 20f, 44f, half + 10f - platBack);
        var rng = new DeterministicRng(SeedUtil.Combine(Seed, SeedUtil.FromString("acantilado")));
        Mesh[] rocks = { MountainMeshes.Rock(SeedUtil.Combine(Seed, 91), 0.9f), MountainMeshes.Rock(SeedUtil.Combine(Seed, 92), 0.8f), MountainMeshes.Rock(SeedUtil.Combine(Seed, 93), 1f) };
        for (float x = -half; x <= half; x += rng.Range(11f, 16f))
        {
            bool nearGate = Mathf.Abs(x) < platHalf + 10f;
            float sx = rng.Range(9f, 14f), sy = nearGate ? rng.Range(16f, 22f) : rng.Range(10f, 20f);
            var rock = new GameObject("Roca_Acantilado");
            rock.transform.SetParent(stairs, false);
            rock.transform.SetPositionAndRotation(new Vector3(x, sy * 0.35f, platBack + rng.Range(4f, 9f)), Quaternion.Euler(rng.Range(-8f, 8f), rng.Range(0f, 360f), rng.Range(-8f, 8f)));
            rock.transform.localScale = new Vector3(sx, sy, rng.Range(8f, 12f));
            rock.AddComponent<MeshFilter>().sharedMesh = rocks[rng.NextInt(0, rocks.Length)];
            rock.AddComponent<MeshRenderer>().sharedMaterial = stone;
        }
        // Rocas que enmarcan la plataforma.
        for (int side = -1; side <= 1; side += 2)
        {
            var rock = new GameObject("Roca_Paso");
            rock.transform.SetParent(stairs, false);
            rock.transform.SetPositionAndRotation(new Vector3(side * (platHalf + 4f), TrailRise * 0.6f, z1 + 4f), Quaternion.Euler(0f, rng.Range(0f, 360f), 0f));
            rock.transform.localScale = new Vector3(8f, TrailRise * 2.2f, 10f);
            rock.AddComponent<MeshFilter>().sharedMesh = rocks[side > 0 ? 0 : 1];
            rock.AddComponent<MeshRenderer>().sharedMaterial = stone;
            rock.AddComponent<MeshCollider>().sharedMesh = rock.GetComponent<MeshFilter>().sharedMesh;
        }
        return new Vector3(0f, TrailRise, z1 + 2.5f);
    }


    // ---------- Escalera curva del sendero ----------
    // Sube en S desde el camino hasta la plataforma: un tramo, un rellano a media altura y otro tramo.
    // El héroe pisa una superficie lisa (una malla) y los peldaños, macizos hasta el suelo, son solo visuales.
    private static Vector3 TrailBezier(float t)
    {
        Vector3 head = VillageLayout.Trailhead;
        Vector3 p0 = new Vector3(TrailStartX, 0f, head.z - TrailRun), p3 = new Vector3(0f, 0f, head.z);
        Vector3 p1 = p0 + Vector3.forward * TrailRun * 0.45f, p2 = p3 - Vector3.forward * TrailRun * 0.45f;
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }

    // Altura según la fracción del recorrido: sube, rellano, sube.
    private static float TrailHeight(float f)
    {
        const float landA = 0.42f, landB = 0.58f;
        if (f < landA) return Mathf.SmoothStep(0f, 1f, f / landA) * 0.5f * TrailRise * 0.92f + f / landA * 0.04f * TrailRise;
        if (f < landB) return TrailRise * 0.5f;
        float k = (f - landB) / (1f - landB);
        return TrailRise * 0.5f + TrailRise * 0.5f * (0.92f * Mathf.SmoothStep(0f, 1f, k) + 0.08f * k);
    }

    public static Vector3 TrailPoint(float t)
    {
        const int n = 64;
        float done = 0f, total = 0f;
        Vector3 prev = TrailBezier(0f);
        for (int i = 1; i <= n; i++)
        {
            float ti = i / (float)n;
            Vector3 cur = TrailBezier(ti);
            float d = Vector3.Distance(prev, cur);
            if (ti <= t) done += d;
            else if (ti - 1f / n < t) done += d * (t - (ti - 1f / n)) * n;
            total += d;
            prev = cur;
        }
        Vector3 p = TrailBezier(t);
        p.y = TrailHeight(total > 0f ? done / total : t);
        return p;
    }

    private static Vector3 TrailForward(float t)
    {
        Vector3 d = TrailBezier(Mathf.Min(1f, t + 0.01f)) - TrailBezier(Mathf.Max(0f, t - 0.01f));
        d.y = 0f;
        return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
    }

    private void BuildCurvedFlight(Transform parent, Material stone)
    {
        Material dark = new Material(stone) { name = "Piedra escalera oscura" };
        if (dark.HasProperty("_BaseColor")) dark.SetColor("_BaseColor", stone.GetColor("_BaseColor") * 0.78f);
        float w = TrailHalfWidth * 2f;

        // Superficie para caminar.
        const int n = 72;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int k = 0; k <= n + 2; k++)
        {
            float t = (k - 1) / (float)n;
            Vector3 p, fwd;
            if (t < 0f) { fwd = TrailForward(0f); p = TrailPoint(0f) - fwd * 0.8f + Vector3.down * 0.06f; }
            else if (t > 1f) { fwd = Vector3.forward; p = TrailPoint(1f) + fwd * 0.8f; }
            else { p = TrailPoint(t); fwd = TrailForward(t); }
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            verts.Add(p - right * (w * 0.5f + 0.2f));
            verts.Add(p + right * (w * 0.5f + 0.2f));
            if (k > 0) { int i0 = (k - 1) * 2; tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0 + 1, i0 + 3, i0 + 2 }); }
        }
        var mesh = new Mesh { name = "Rampa_Sendero" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        if (mesh.normals[0].y < 0f)
        {
            for (int i = 0; i < tris.Count; i += 3) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
        }
        var walk = new GameObject("Rampa_Sendero");
        walk.transform.SetParent(parent, false);
        walk.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        walk.AddComponent<MeshCollider>().sharedMesh = mesh;

        // Peldaños (y losas en el rellano), macizos hasta el suelo.
        const int steps = 60;
        float lastTop = -1f;
        for (int k = 0; k < steps; k++)
        {
            float t0 = k / (float)steps, t1 = (k + 1) / (float)steps;
            Vector3 a = TrailPoint(t0), b = TrailPoint(t1);
            Vector3 fwd = TrailForward((t0 + t1) * 0.5f);
            float top = Mathf.Max(a.y, b.y);
            // Peldaño de 25 cm como mucho: si el tramo apenas sube, se une al anterior (rellano).
            if (lastTop >= 0f && top - lastTop < 0.12f) top = lastTop;
            lastTop = top;
            Vector3 mid = (a + b) * 0.5f;
            float depth = new Vector2(b.x - a.x, b.z - a.z).magnitude + 0.18f;
            GameObject step = Block(parent, "Peldano", new Vector3(mid.x, top * 0.5f - 0.05f, mid.z), new Vector3(w, top + 0.1f, depth), (k & 1) == 0 ? stone : dark, false);
            step.transform.rotation = Quaternion.LookRotation(fwd);
        }

        // Muretes a ambos lados (con colisión), siguiendo la curva, y faroles de hielo en el rellano.
        const int segs = 30;
        for (int j = 0; j < segs; j++)
        {
            Vector3 a = TrailPoint(j / (float)segs), b = TrailPoint((j + 1) / (float)segs);
            Vector3 d = b - a;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            Quaternion rot = Quaternion.LookRotation(d.normalized, Vector3.up);
            Vector3 right = Vector3.Cross(Vector3.up, flat.normalized).normalized;
            Vector3 mid = (a + b) * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 c = mid + right * side * (w * 0.5f + 0.35f);
                GameObject wall = Block(parent, "Murete_Escalera", Vector3.zero, new Vector3(0.7f, 1.1f, d.magnitude + 0.45f), dark, true);
                wall.transform.SetPositionAndRotation(c + Vector3.up * 0.55f, rot);
                // Zócalo que baja hasta el suelo por fuera del murete.
                Block(parent, "Zocalo_Escalera", new Vector3(c.x, Mathf.Min(a.y, b.y) * 0.5f, c.z), new Vector3(0.7f, Mathf.Min(a.y, b.y) + 0.05f, flat.magnitude + 0.45f), stone, false)
                    .transform.rotation = Quaternion.LookRotation(flat.normalized);
            }
        }
        foreach (float f in new[] { 0.43f, 0.57f })
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 p = TrailPoint(f);
                Vector3 right = Vector3.Cross(Vector3.up, TrailForward(f)).normalized;
                Vector3 at = p + right * side * (w * 0.5f + 0.35f) + Vector3.up * 1.1f;
                Block(parent, "Poste_Farol", at + Vector3.up * 0.5f, new Vector3(0.35f, 1f, 0.35f), dark, false);
                var lamp = new GameObject("Farol_Hielo");
                lamp.transform.SetParent(parent, false);
                lamp.transform.position = at + Vector3.up * 1.3f;
                Light l = lamp.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.55f, 0.78f, 1f);
                l.range = 8f;
                l.intensity = 1.6f;
                l.shadows = LightShadows.None;
            }
    }

    // Nieve con detalle: variación grande cada ~60 m (ventisqueros azulados) y grano/ondas de viento cada ~4 m.
    public const float SnowMacroMeters = 60f, SnowDetailMeters = 4f;

    public static void ApplyDetailedSnow(Renderer r, float meters)
    {
        if (r == null) return;
        Material m = Resources.Load<Material>("Frostbound/NieveDetallada");
        if (m == null) return;
        var inst = new Material(m) { name = "Nieve del poblado" };
        inst.SetTextureScale("_BaseMap", Vector2.one * (meters / SnowMacroMeters));
        inst.SetTextureScale("_DetailAlbedoMap", Vector2.one * (meters / SnowDetailMeters));
        r.sharedMaterial = inst;
    }

    private static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collide)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collide) Destroy(go.GetComponent<Collider>());
        return go;
    }

    // Borde del mundo: hileras de pinos (sin colisión, agrupados en lotes estáticos) y paredes invisibles detrás.
    private void BuildBorder(Transform root)
    {
        Transform border = new GameObject("Borde_Bosque").transform;
        border.SetParent(root, false);
        if (pinePrefab != null)
        {
            foreach (VillageLayout.Spot s in Layout.border)
            {
                GameObject pine = Instantiate(pinePrefab, s.position, Quaternion.Euler(0f, s.yaw, 0f), border);
                pine.transform.localScale = Vector3.one * s.scale;
                foreach (Collider c in pine.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            StaticBatchingUtility.Combine(border.gameObject);
        }
        float d = VillageLayout.PlayableHalfSize + 1f, len = d * 2f + 4f;
        for (int side = 0; side < 4; side++)
        {
            var wall = new GameObject("Limite_Mundo");
            wall.transform.SetParent(root, false);
            bool alongX = side % 2 == 0;
            float sign = side < 2 ? 1f : -1f;
            wall.transform.position = alongX ? new Vector3(0f, 10f, sign * (d + 1f)) : new Vector3(sign * (d + 1f), 10f, 0f);
            BoxCollider c = wall.AddComponent<BoxCollider>();
            c.size = alongX ? new Vector3(len, 30f, 2f) : new Vector3(2f, 30f, len);
        }
    }

    private Bonfire SpawnBonfire(Transform parent, VillageLayout.Spot spot, string id, string label)
    {
        if (bonfirePrefab == null) return null;
        GameObject go = Instantiate(bonfirePrefab, spot.position, Quaternion.Euler(0f, spot.yaw, 0f), parent);
        go.name = id;
        go.transform.localScale = Vector3.one * spot.scale;
        Bonfire b = go.GetComponent<Bonfire>();
        if (b == null) b = go.AddComponent<Bonfire>();
        b.id = id;
        b.displayName = label;
        return b;
    }
}
