using UnityEngine;

// Estado global de la partida: vive entre escenas y guarda el seed del mundo.
public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    [Header("Mundo")]
    [Tooltip("Seed maestro de esta partida. Define montaña, castillos y spawn.")]
    public int worldSeed;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Llamar al pulsar "Nueva Partida" en el menú.
    public void NewGame()
    {
        worldSeed = Random.Range(1, int.MaxValue);
        Debug.Log("[GameSession] Nueva partida creada. Seed del mundo: " + worldSeed);
    }

    // Para partidas compartidas por seed o pruebas repetibles.
    public void NewGameWithSeed(int seed)
    {
        worldSeed = seed;
        Debug.Log("[GameSession] Partida con seed fijo: " + worldSeed);
    }

    // Seed determinista de un piso concreto de un castillo.
    public int FloorSeed(string dungeonId, int floorIndex)
    {
        return SeedUtil.Combine(worldSeed, SeedUtil.FromString(dungeonId), floorIndex);
    }

    // Seed de la estructura de la montaña (zonas, orden, spawn base).
    public int MountainSeed => SeedUtil.Combine(worldSeed, SeedUtil.FromString("montana"));
}
