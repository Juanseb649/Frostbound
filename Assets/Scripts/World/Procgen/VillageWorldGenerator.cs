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
