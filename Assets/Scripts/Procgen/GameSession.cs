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

    // ----- Partidas guardadas -----

    public int CurrentSlot => Current != null ? Current.slot : -1;
    public SaveData Current { get; private set; }
    public bool PendingRestore { get; private set; }

    private Equipment _player;
    private float _playSeconds;

    public void StartNewGame(int slot, CharacterClass cls, string plumageId, string heroName)
    {
        SetHero(cls, plumageId, heroName);
        NewGame();
        Current = new SaveData
        {
            slot = slot,
            worldSeed = worldSeed,
            heroName = heroName,
            classId = cls != null ? cls.name : "",
            plumageId = plumageId,
            lastBonfire = ""
        };
        _playSeconds = 0f;
        PendingRestore = false;
        SaveSystem.Save(Current);
    }

    public bool LoadGame(int slot)
    {
        SaveData data = SaveSystem.Load(slot);
        if (data == null) return false;
        GameDatabase db = GameDatabase.Instance;
        CharacterClass cls = db != null ? db.FindClass(data.classId) : null;
        if (cls == null)
        {
            Debug.LogWarning("[GameSession] La ranura " + (slot + 1) + " usa una clase que no existe: " + data.classId);
            return false;
        }
        SetHero(cls, data.plumageId, data.heroName);
        worldSeed = data.worldSeed;
        Current = data;
        _playSeconds = data.playSeconds;
        PendingRestore = true;
        Debug.Log("[GameSession] Partida " + (slot + 1) + " cargada. Seed del mundo: " + worldSeed);
        return true;
    }

    public void EndGame()
    {
        SaveNow();
        Current = null;
        _player = null;
        PendingRestore = false;
    }

    public string LastBonfire => Current != null ? Current.lastBonfire ?? "" : "";

    public bool IsBonfireLit(string id) => Current != null && Current.litBonfires.Contains(id);

    public void LightBonfire(string id)
    {
        if (Current == null || string.IsNullOrEmpty(id)) return;
        if (!Current.litBonfires.Contains(id)) Current.litBonfires.Add(id);
        Current.lastBonfire = id;
        SaveNow();
    }

    public void SetLastBonfire(string id)
    {
        if (Current == null) return;
        Current.lastBonfire = id ?? "";
    }

    public void RegisterPlayer(Equipment player) => _player = player;

    public void UnregisterPlayer(Equipment player)
    {
        if (_player == player) _player = null;
    }

    void Update()
    {
        if (_player != null) _playSeconds += Time.unscaledDeltaTime;
    }

    void OnApplicationQuit() => SaveNow();

    public bool SaveNow()
    {
        if (Current == null) return false;
        if (_player != null) Capture(_player, Current);
        Current.playSeconds = _playSeconds;
        Current.worldSeed = worldSeed;
        return SaveSystem.Save(Current);
    }

    public static void Capture(Equipment eq, SaveData data)
    {
        CharacterStats stats = eq.Stats;
        data.level = stats.level;
        data.experience = stats.experience;
        data.pointsAvailable = stats.pointsAvailable;
        data.allocated = stats.allocated;
        data.health = stats.IsDead ? stats.MaxHealth : stats.currentHealth;
        data.mana = stats.currentMana;

        data.inventory.Clear();
        Inventory inv = eq.Inventory;
        for (int i = 0; i < inv.Capacity; i++)
        {
            ItemStack s = inv.Get(i);
            if (s == null || s.item == null) continue;
            SavedStack d = SavedStack.From(s);
            d.index = i;
            data.inventory.Add(d);
        }

        data.equipment.Clear();
        foreach (EquipSlot slot in Equipment.Slots)
        {
            ItemStack s = eq.GetStack(slot);
            if (s == null || s.item == null) continue;
            SavedStack d = SavedStack.From(s);
            d.slot = slot;
            data.equipment.Add(d);
        }

        data.belt.Clear();
        PotionBelt belt = eq.GetComponent<PotionBelt>();
        if (belt != null)
            foreach (ItemDefinition item in belt.slots) data.belt.Add(item != null ? item.id : "");
    }

    // Lo llama Equipment.Start: devuelve true si cargó el héroe de la partida (y no hay que darle el equipo inicial).
    public bool RestorePlayer(Equipment eq)
    {
        if (!PendingRestore || Current == null) return false;
        PendingRestore = false;
        ItemDatabase db = GameDatabase.Instance != null ? GameDatabase.Instance.items : null;
        SaveData data = Current;

        CharacterStats stats = eq.Stats;
        stats.level = Mathf.Max(1, data.level);
        stats.experience = Mathf.Max(0, data.experience);
        stats.pointsAvailable = Mathf.Max(0, data.pointsAvailable);
        stats.allocated = data.allocated;

        foreach (SavedStack d in data.inventory)
        {
            ItemStack s = d.ToStack(db);
            if (s != null && !eq.Inventory.PutAt(d.index, s)) eq.Inventory.AddStack(s);
        }
        foreach (SavedStack d in data.equipment)
        {
            ItemStack s = d.ToStack(db);
            if (s != null) eq.SetEquippedStack(d.slot, s);
        }
        PotionBelt belt = eq.GetComponent<PotionBelt>();
        if (belt != null && db != null)
        {
            var slots = new ItemDefinition[PotionBelt.Size];
            for (int i = 0; i < slots.Length && i < data.belt.Count; i++)
                slots[i] = string.IsNullOrEmpty(data.belt[i]) ? null : db.Find(data.belt[i]);
            belt.Restore(slots);
        }
        return true;
    }

    // Después de aplicar el equipo (la vida máxima ya incluye sus bonificaciones).
    public void FinishRestore(Equipment eq)
    {
        if (Current == null) return;
        CharacterStats stats = eq.Stats;
        stats.currentHealth = Current.health > 0f ? Mathf.Min(stats.MaxHealth, Current.health) : stats.MaxHealth;
        stats.currentMana = Current.mana >= 0f ? Mathf.Min(stats.MaxMana, Current.mana) : stats.MaxMana;
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
