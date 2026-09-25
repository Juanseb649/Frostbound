using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemCategory { Armor, Weapon, Consumable, Material, Misc }

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

    [Header("Consumible")]
    [Min(0)] public float heal;
    [Min(0)] public float restoreMana;

    public bool IsEquippable => equipSlot != EquipSlot.None;
    public bool IsStackable => maxStack > 1;
    public bool IsConsumable => category == ItemCategory.Consumable;

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
        return true;
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
