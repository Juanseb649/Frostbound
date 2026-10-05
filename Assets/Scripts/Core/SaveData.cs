using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int slot;
    public string createdUtc = "";
    public string savedUtc = "";
    public float playSeconds;

    public int worldSeed;
    public string heroName = "";
    public string classId = "";
    public string plumageId = "";

    public int level = 1;
    public int experience;
    public int pointsAvailable;
    public StatBlock allocated;
    public float health = -1f;
    public float mana = -1f;

    public List<SavedStack> inventory = new List<SavedStack>();
    public List<SavedStack> equipment = new List<SavedStack>();
    public List<string> belt = new List<string>();

    public List<string> litBonfires = new List<string>();
    public string lastBonfire = "";
    public string zone = "";

    public List<SavedQuest> quests = new List<SavedQuest>();
    public int coins;
    public int stashCoins;
    public List<SavedStack> stash = new List<SavedStack>();
    public List<SavedFog> explored = new List<SavedFog>();

    public bool HasProgress => level > 1 || experience > 0 || inventory.Count > 0;
}

[Serializable]
public class SavedQuest
{
    public string id = "";
    public int stage;
}

// Niebla de guerra de un mapa: bits de las celdas exploradas, en base64.
[Serializable]
public class SavedFog
{
    public string area = "";
    public int size;
    public string bits = "";
}

[Serializable]
public class SavedStack
{
    public int index;
    public EquipSlot slot;
    public string id = "";
    public int quantity = 1;
    public float durability = -1f;
    public List<string> runes = new List<string>();
    public LootQuality quality;
    public List<SavedAffix> affixes = new List<SavedAffix>();
    public string rareName = "";
    public int itemLevel;

    public static SavedStack From(ItemStack s)
    {
        var d = new SavedStack
        {
            id = s.item != null ? s.item.id : "",
            quantity = s.quantity,
            durability = s.durability,
            quality = s.quality,
            rareName = s.rareName ?? "",
            itemLevel = s.itemLevel
        };
        if (s.runes != null) foreach (ItemDefinition r in s.runes) if (r != null) d.runes.Add(r.id);
        if (s.affixes != null) foreach (RolledAffix a in s.affixes) d.affixes.Add(new SavedAffix { id = a.id, kind = a.kind, value = a.value });
        return d;
    }

    public ItemStack ToStack(ItemDatabase db)
    {
        ItemDefinition item = db != null ? db.Find(id) : null;
        if (item == null) return null;
        // Partidas antiguas sin nivel: lo que vino del botín cuenta como nivel 1.
        int level = itemLevel > 0 ? itemLevel : (quality != LootQuality.None ? 1 : 0);
        var s = new ItemStack(item, Math.Max(1, quantity)) { quality = quality, rareName = rareName ?? "", itemLevel = level };
        if (item.HasDurability && durability >= 0f) s.durability = durability;
        s.runes = new List<ItemDefinition>();
        foreach (string r in runes)
        {
            ItemDefinition rune = db.Find(r);
            if (rune != null) s.runes.Add(rune);
        }
        s.affixes = new List<RolledAffix>();
        foreach (SavedAffix a in affixes) s.affixes.Add(new RolledAffix(a.id, a.kind, a.value));
        return s;
    }
}

[Serializable]
public class SavedAffix
{
    public string id = "";
    public AffixKind kind;
    public float value;
}
