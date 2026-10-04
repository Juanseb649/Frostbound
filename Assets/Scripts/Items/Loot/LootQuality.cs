using System;
using UnityEngine;

// Calidad de un objeto que cae (spec §5.2). None = objeto fijo (equipo inicial, tienda, recompensas).
public enum LootQuality { None, Worn, Normal, Superior, Magic, Rare, Set, Unique }

public enum AffixKind { Strength, Mana, Agility, Health, Armor, Damage, AttackSpeed, Lifesteal, FrostChance, MagicFind }

// Un afijo ya tirado sobre un objeto concreto.
[Serializable]
public class RolledAffix
{
    public string id;
    public AffixKind kind;
    public float value;

    public RolledAffix(string id, AffixKind kind, float value)
    {
        this.id = id;
        this.kind = kind;
        this.value = value;
    }

    public string Describe()
    {
        int n = Mathf.RoundToInt(value);
        string sign = value >= 0f ? "+" : "";
        switch (kind)
        {
            case AffixKind.Strength: return sign + n + " de Fuerza";
            case AffixKind.Mana: return sign + n + " de Maná";
            case AffixKind.Agility: return sign + n + " de Agilidad";
            case AffixKind.Health: return sign + n + " de Salud";
            case AffixKind.Armor: return sign + n + " de armadura";
            case AffixKind.Damage: return sign + value.ToString("0.#") + " de daño";
            case AffixKind.AttackSpeed: return sign + Mathf.RoundToInt(value * 100f) + " % de velocidad de ataque";
            case AffixKind.Lifesteal: return "Roba el " + Mathf.RoundToInt(value * 100f) + " % del daño como vida";
            case AffixKind.FrostChance: return Mathf.RoundToInt(value * 100f) + " % de prob. de ralentizar con escarcha";
            case AffixKind.MagicFind: return sign + Mathf.RoundToInt(value) + " % de hallazgo mágico";
            default: return "";
        }
    }
}

public static class LootQualityInfo
{
    public static readonly Color Worn = FrostboundUI.Hex("#8A97A8");
    public static readonly Color Normal = FrostboundUI.Hex("#E8F1FA");
    public static readonly Color Magic = FrostboundUI.Hex("#6FA8FF");
    public static readonly Color Rare = FrostboundUI.Hex("#F3D48A");
    public static readonly Color Set = FrostboundUI.Hex("#6FD08A");
    public static readonly Color Unique = FrostboundUI.Hex("#E0B44A");
    public static readonly Color Rune = FrostboundUI.Hex("#7FD8FF");

    public static Color Color(LootQuality q)
    {
        switch (q)
        {
            case LootQuality.Worn: return Worn;
            case LootQuality.Magic: return Magic;
            case LootQuality.Rare: return Rare;
            case LootQuality.Set: return Set;
            case LootQuality.Unique: return Unique;
            default: return Normal;
        }
    }

    public static string Name(LootQuality q)
    {
        switch (q)
        {
            case LootQuality.Worn: return "Gastado";
            case LootQuality.Normal: return "Normal";
            case LootQuality.Superior: return "Superior";
            case LootQuality.Magic: return "Mágico";
            case LootQuality.Rare: return "Raro";
            case LootQuality.Set: return "De conjunto";
            case LootQuality.Unique: return "Único";
            default: return "";
        }
    }

    // Con haz de luz al caer (spec §5.7).
    public static bool HasBeam(ItemStack s)
    {
        if (s == null || s.item == null) return false;
        return s.quality >= LootQuality.Rare || (s.quality == LootQuality.None && s.item.rarity == ItemRarity.Unique);
    }
}
