using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemCategory { Armor, Weapon, Consumable, Material, Misc, Rune }

// Cada pieza de armadura es un objeto independiente: se pueden combinar cascos, torsos y pies de sets distintos.
public enum EquipSlot { None, Head, Chest, Feet, Weapon, Offhand, Amulet }

public enum ItemRarity { Common, Uncommon, Rare, Epic, Unique }

[Serializable]
public struct StatModifier
{
    public StatType stat;
    public int value;

    public StatModifier(StatType stat, int value)
    {
        this.stat = stat;
        this.value = value;
    }
}

[CreateAssetMenu(menuName = "Frostbound/Objeto", fileName = "NuevoObjeto")]
public class ItemDefinition : ScriptableObject
{
    [Header("Info")]
    [Tooltip("Identificador único (para guardar partidas y buscar en la base de datos).")]
    public string id = "";
    public string displayName = "Objeto";
    [TextArea(2, 4)] public string description = "";
    public Sprite icon;
    public ItemCategory category = ItemCategory.Misc;
    public ItemRarity rarity = ItemRarity.Common;
    [Tooltip("1 = no se apila. Pociones y materiales se apilan.")]
    [Min(1)] public int maxStack = 1;

    [Header("Equipo")]
    public EquipSlot equipSlot = EquipSlot.None;
    [Min(1)] public int requiredLevel = 1;
    [Tooltip("Vacío = cualquier clase puede usarlo.")]
    public List<CharacterClass> allowedClasses = new List<CharacterClass>();
    public List<StatModifier> stats = new List<StatModifier>();
    [Min(0)] public int armor;
    [Min(0)] public float damage;
    [Tooltip("Modelo que se ve sobre el pingüino al equiparlo (opcional).")]
    public OutfitItem outfit;
    [Tooltip("Set al que pertenece la pieza (bonos por llevar varias piezas).")]
    public ArmorSet armorSet;

    [Header("Arma")]
    public WeaponType weaponType = WeaponType.None;
    [Tooltip("Ataques por segundo (antes de la Agilidad y las runas).")]
    [Min(0.1f)] public float attackSpeed = 1.2f;
    [Tooltip("Variación del daño: 0.15 = ±15 %.")]
    [Range(0f, 0.5f)] public float damageSpread = 0.15f;
    [Tooltip("Atributos mínimos para poder usarla (el kunai y la hoz piden Agilidad; el mazo, Fuerza).")]
    public List<StatModifier> requiredStats = new List<StatModifier>();
    [Tooltip("Durabilidad máxima. Cada golpe la gasta; a 0 el arma está rota hasta que la reparen.")]
    [Min(0)] public int maxDurability;
    [Tooltip("Ranuras donde se pueden grabar runas.")]
    [Range(0, 4)] public int runeSlots;
    [Tooltip("Modelo del arma: se sujeta con la aleta y es el que cae al suelo.")]
    public GameObject weaponModel;
    [Tooltip("Proyectil (flecha, virote, kunai). Vacío = usa el modelo del arma.")]
    public GameObject projectileModel;

    [Header("Runa")]
    public RuneEffect runeEffect = RuneEffect.None;
    [Tooltip("Fuego/Veneno: daño por segundo · Escarcha: ralentización (0.4 = 40 %) · Cadena: fracción del golpe · Vampiro/Filo/Celeridad: fracción · Empuje: metros.")]
    public float runePower;
    [Tooltip("Segundos que dura el efecto (fuego, veneno, escarcha).")]
    public float runeDuration;
    [Tooltip("Probabilidad por golpe (1 = siempre).")]
    [Range(0f, 1f)] public float runeChance = 1f;
    [Tooltip("Maná que cuesta el hechizo de grabado.")]
    [Min(0)] public int engraveManaCost = 15;

    [Header("Consumible")]
    [Min(0)] public float heal;
    [Min(0)] public float restoreMana;

    public bool IsEquippable => equipSlot != EquipSlot.None;
    public bool IsStackable => maxStack > 1;
    public bool IsConsumable => category == ItemCategory.Consumable;
    public bool IsWeapon => weaponType != WeaponType.None;
    public bool IsRune => category == ItemCategory.Rune && runeEffect != RuneEffect.None;
    public WeaponCatalog.Info WeaponInfo => WeaponCatalog.Get(weaponType);
    public WeaponHandling Handling => WeaponInfo.handling;
    public bool IsRanged => IsWeapon && WeaponInfo.projectile;
    public bool UsesBothHands => IsWeapon && equipSlot == EquipSlot.Weapon && WeaponCatalog.UsesBothHands(weaponType);
    public bool HasDurability => maxDurability > 0;

    public bool MeetsStatRequirements(CharacterStats who, out string reason)
    {
        reason = "";
        if (who == null || requiredStats == null) return true;
        foreach (StatModifier r in requiredStats)
        {
            if (r.value <= 0 || who.Get(r.stat) >= r.value) continue;
            reason = "Requiere " + r.value + " de " + StatBlock.DisplayName(r.stat);
            return false;
        }
        return true;
    }

    // Texto del efecto de una runa ("Incendia: 3 de daño de fuego por segundo durante 4 s").
    public static string RuneDescription(ItemDefinition rune)
    {
        if (rune == null) return "";
        string chance = rune.runeChance < 0.999f ? Mathf.RoundToInt(rune.runeChance * 100f) + " % de prob. de " : "";
        switch (rune.runeEffect)
        {
            case RuneEffect.Fire: return "Incendia: " + rune.runePower.ToString("0.#") + " de daño de fuego por segundo durante " + rune.runeDuration.ToString("0.#") + " s";
            case RuneEffect.Poison: return "Envenena: " + rune.runePower.ToString("0.#") + " de daño por segundo durante " + rune.runeDuration.ToString("0.#") + " s (se acumula ×3)";
            case RuneEffect.Frost: return "Congela: ralentiza un " + Mathf.RoundToInt(rune.runePower * 100f) + " % durante " + rune.runeDuration.ToString("0.#") + " s; " + Mathf.RoundToInt(rune.runeChance * 100f) + " % de dejarlo helado";
            case RuneEffect.Chain: return chance + "rayo en cadena a 3 enemigos cercanos (" + Mathf.RoundToInt(rune.runePower * 100f) + " % del golpe)";
            case RuneEffect.Lifesteal: return "Roba vida: recuperas el " + Mathf.RoundToInt(rune.runePower * 100f) + " % del daño";
            case RuneEffect.Sharpness: return "Filo: +" + Mathf.RoundToInt(rune.runePower * 100f) + " % de daño del arma";
            case RuneEffect.Swiftness: return "Celeridad: +" + Mathf.RoundToInt(rune.runePower * 100f) + " % de velocidad de ataque";
            case RuneEffect.Knockback: return chance + "empuja al enemigo " + rune.runePower.ToString("0.#") + " m";
            default: return "";
        }
    }

    public StatBlock StatBonus
    {
        get
        {
            var block = new StatBlock();
            foreach (StatModifier m in stats) block.Add(m.stat, m.value);
            return block;
        }
    }

    public bool CanBeEquippedBy(CharacterStats who, out string reason)
    {
        reason = "";
        if (!IsEquippable)
        {
            reason = "No se puede equipar";
            return false;
        }
        if (who != null && who.level < requiredLevel)
        {
            reason = "Requiere nivel " + requiredLevel;
            return false;
        }
        if (who != null && allowedClasses.Count > 0 && !allowedClasses.Contains(who.characterClass))
        {
            reason = "Tu clase no puede usarlo";
            return false;
        }
        return MeetsStatRequirements(who, out reason);
    }

    void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name;
        if (IsEquippable) maxStack = 1;
    }

    public static string SlotName(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Head: return "Casco";
            case EquipSlot.Chest: return "Torso";
            case EquipSlot.Feet: return "Pies";
            case EquipSlot.Weapon: return "Arma";
            case EquipSlot.Offhand: return "Mano secundaria";
            case EquipSlot.Amulet: return "Amuleto";
            default: return "";
        }
    }

    public static string CategoryName(ItemCategory category)
    {
        switch (category)
        {
            case ItemCategory.Armor: return "Armadura";
            case ItemCategory.Weapon: return "Arma";
            case ItemCategory.Consumable: return "Consumible";
            case ItemCategory.Material: return "Material";
            case ItemCategory.Rune: return "Runa";
            default: return "Objeto";
        }
    }

    public static string RarityName(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon: return "Poco común";
            case ItemRarity.Rare: return "Raro";
            case ItemRarity.Epic: return "Épico";
            case ItemRarity.Unique: return "Único";
            default: return "Común";
        }
    }
}
