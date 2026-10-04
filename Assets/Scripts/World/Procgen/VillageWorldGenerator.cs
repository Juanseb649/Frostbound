using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;

// Construye el exterior del poblado con la seed de la partida antes de que nada más arranque:
// bosque, montones de nieve, posición de los campamentos y hogueras. Luego vuelve a hornear el NavMesh.
[DefaultExecutionOrder(-900)]
public class VillageWorldGenerator : MonoBehaviour
{
    [Header("Piezas")]
    public GameObject pinePrefab;
    public GameObject moundPrefab;
    public GameObject bonfirePrefab;

    [Header("Escena")]
    [Tooltip("Bancos y aldeanos que se reúnen alrededor de la hoguera del castillo (se mueven con ella).")]
    public Transform bonfireCamp;
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

        Bonfire castle = SpawnBonfire(root, Layout.castleBonfire, Bonfire.CastleId, "Hoguera del castillo");
        if (bonfireCamp != null && castle != null)
        {
            bonfireCamp.SetPositionAndRotation(castle.transform.position, castle.transform.rotation);
            foreach (VillagerNPC v in bonfireCamp.GetComponentsInChildren<VillagerNPC>(true))
                if (v.mood == VillagerMood.Huddle) v.focusPoint = castle.transform;
        }
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

        if (navMesh != null) navMesh.BuildNavMesh();
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
