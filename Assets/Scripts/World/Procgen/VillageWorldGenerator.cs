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
        ZoneGate gate = ZoneGate.Build(t, "Paso_Montana", VillageLayout.Trailhead, 0f, 8f, stone, cloth);
        gate.targetScene = SceneIds.Foothills;
        gate.arrival = MountainLayout.ArrivalFromVillage;
        gate.label = "Sendero del Frostspire";
        gate.canPass = () => QuestLog.Stage(QuestLog.Ascent) >= QuestLog.AscentTold;
        gate.blockedMessage = "Una ventisca cierra el sendero de la montaña. Quizá Steve sepa algo.";
        gate.onPass = () =>
        {
            if (QuestLog.Stage(QuestLog.Ascent) < QuestLog.AscentFoothills) QuestLog.SetStage(QuestLog.Ascent, QuestLog.AscentFoothills);
        };
        ZoneArrival.Create(t, MountainLayout.ArrivalFromFoothills, VillageLayout.Trailhead + new Vector3(0f, 0.1f, -6f), 180f);
        // Llegada del pergamino de regreso: la plaza, donde empieza la partida.
        ZoneArrival.Create(root, TownPortal.ArrivalId, new Vector3(0f, 0.1f, -9f), 0f);

        var rng = new DeterministicRng(SeedUtil.Combine(Seed, SeedUtil.FromString("sendero_norte")));
        Mesh rock = MountainMeshes.Rock(SeedUtil.Combine(Seed, 77), 0.6f);
        for (float z = 48f; z < VillageLayout.Trailhead.z - 8f; z += rng.Range(24f, 34f))
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
        for (int i = 0; i < 7; i++)
        {
            float x = (i - 3) * rng.Range(48f, 62f) + rng.Range(-15f, 15f);
            float z = VillageLayout.GroundVisualHalfSize + rng.Range(40f, 140f) + (i == 3 ? 90f : 0f);
            float s = i == 3 ? 170f : rng.Range(70f, 120f);
            var peak = new GameObject("Cumbre_Frostspire");
            peak.transform.SetParent(t, false);
            peak.transform.SetPositionAndRotation(new Vector3(x, -6f, z), Quaternion.Euler(0f, rng.Range(0f, 360f), 0f));
            peak.transform.localScale = new Vector3(s * 1.5f, s, s * 1.5f);
            peak.AddComponent<MeshFilter>().sharedMesh = MountainMeshes.Peak(SeedUtil.Combine(Seed, 300 + i));
            var r = peak.AddComponent<MeshRenderer>();
            r.sharedMaterial = snow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
