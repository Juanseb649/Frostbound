using System;
using System.Collections.Generic;

// Una casilla: el objeto, cuántos hay y, en las armas, su estado propio (durabilidad y runas grabadas).
[Serializable]
public class ItemStack
{
    public ItemDefinition item;
    public int quantity;
    [UnityEngine.Tooltip("Durabilidad actual. -1 = nueva (se llena al usarla por primera vez).")]
    public float durability = -1f;
    public List<ItemDefinition> runes = new List<ItemDefinition>();
    [UnityEngine.Tooltip("Calidad con la que cayó (botín). None = objeto fijo.")]
    public LootQuality quality = LootQuality.None;
    public List<RolledAffix> affixes = new List<RolledAffix>();
    [UnityEngine.Tooltip("Nombre generado de los objetos raros.")]
    public string rareName = "";
    [UnityEngine.Tooltip("Nivel del enemigo que lo soltó. 0 = arma inicial o fija (pega menos).")]
    public int itemLevel;

    // El daño de un arma crece con el nivel del enemigo que la soltó; las iniciales pegan un 30 % menos.
    public static float LevelMultiplier(int level) => level <= 0 ? 0.7f : 1f + 0.15f * (level - 1);
    public float BaseDamage => item != null ? item.damage * (item.IsWeapon ? LevelMultiplier(itemLevel) : 1f) : 0f;

    public ItemStack(ItemDefinition item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
        EnsureInstance();
    }

    public bool IsEmpty => item == null || quantity <= 0;
    public int SpaceLeft => item == null ? 0 : item.maxStack - quantity;

    // Estado propio: no se apila con otras copias del mismo objeto.
    public bool HasInstanceData => item != null && (item.HasDurability || item.runeSlots > 0 || quality != LootQuality.None);

    public string DisplayName
    {
        get
        {
            if (item == null) return "";
            switch (quality)
            {
                case LootQuality.Rare: return string.IsNullOrEmpty(rareName) ? item.displayName : rareName;
                case LootQuality.Superior: return item.displayName + " superior";
                case LootQuality.Worn: return item.displayName + " (desgastado)";
                case LootQuality.Magic:
                    string pre = "", suf = "";
                    foreach (RolledAffix a in affixes)
                    {
                        if (LootRoller.IsPrefix(a.id)) pre = " " + LootRoller.AffixLabel(a.id);
                        else suf = " " + LootRoller.AffixLabel(a.id);
                    }
                    return item.displayName + pre + suf;
                default: return item.displayName;
            }
        }
    }

    public UnityEngine.Color DisplayColor
    {
        get
        {
            if (item == null) return UnityEngine.Color.white;
            if (quality != LootQuality.None && quality != LootQuality.Normal) return LootQualityInfo.Color(quality);
            if (item.IsRune) return LootQualityInfo.Rune;
            return FrostboundUI.Rarity(item.rarity);
        }
    }

    public float AffixSum(AffixKind kind)
    {
        float total = 0f;
        if (affixes == null) return 0f;
        foreach (RolledAffix a in affixes) if (a.kind == kind) total += a.value;
        return total;
    }

    public void EnsureInstance()
    {
        if (runes == null) runes = new List<ItemDefinition>();
        if (affixes == null) affixes = new List<RolledAffix>();
        if (item != null && item.HasDurability && durability < 0f) durability = item.maxDurability;
    }

    public float MaxDurability => item != null ? item.maxDurability : 0f;
    public float Durability01 => item != null && item.HasDurability ? UnityEngine.Mathf.Clamp01(durability / item.maxDurability) : 1f;
    public bool IsBroken => item != null && item.HasDurability && durability <= 0f;
    public int FreeRuneSlots => item == null ? 0 : UnityEngine.Mathf.Max(0, item.runeSlots - runes.Count);

    // Gasta durabilidad. Devuelve true si el arma se acaba de romper.
    public bool Wear(float amount)
    {
        if (item == null || !item.HasDurability || durability <= 0f) return false;
        EnsureInstance();
        durability = UnityEngine.Mathf.Max(0f, durability - amount);
        return durability <= 0f;
    }

    public void Repair()
    {
        if (item != null && item.HasDurability) durability = item.maxDurability;
    }

    public ItemStack Clone()
    {
        var c = new ItemStack(item, quantity) { durability = durability, quality = quality, rareName = rareName, itemLevel = itemLevel };
        c.affixes = new List<RolledAffix>();
        if (affixes != null) foreach (RolledAffix a in affixes) c.affixes.Add(new RolledAffix(a.id, a.kind, a.value));
        c.runes = new List<ItemDefinition>(runes ?? new List<ItemDefinition>());
        return c;
    }
}
