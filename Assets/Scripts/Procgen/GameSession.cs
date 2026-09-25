using UnityEngine;

// Estado global de la partida: vive entre escenas y guarda el seed del mundo.
public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    [Header("Héroe elegido")]
    public CharacterClass SelectedClass;
    public string PlumageId = "";
    public string HeroName = "";

    // Crea la sesión si todavía no existe (por ejemplo, al abrir el menú por primera vez).
    public static GameSession Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("GameSession");
        return go.AddComponent<GameSession>();
    }

    public void SetHero(CharacterClass cls, string plumageId, string heroName)
    {
        SelectedClass = cls;
        PlumageId = plumageId;
        HeroName = heroName;
    }

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
