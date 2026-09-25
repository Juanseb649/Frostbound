using TMPro;
using UnityEngine;

// Fuentes, sprites e iconos que usa la interfaz del juego (HUD e inventario).
[CreateAssetMenu(menuName = "Frostbound/UI Skin", fileName = "UISkin")]
public class UISkin : ScriptableObject
{
    [Header("Fuentes")]
    public TMP_FontAsset cinzel600;
    public TMP_FontAsset cinzel800;
    public TMP_FontAsset nunito500;
    public TMP_FontAsset nunito700;
    public TMP_FontAsset nunito800;

    [Header("Formas")]
    public Sprite round10;
    public Sprite round12;
    public Sprite round16;
    public Sprite round20;
    public Sprite pill;
    public Sprite circle;
    public Sprite diamond;

    [Header("Atributos")]
    public Sprite iconStrength;
    public Sprite iconMana;
    public Sprite iconAgility;
    public Sprite iconHealth;

    [Header("Ranuras vacías")]
    public Sprite slotHead;
    public Sprite slotChest;
    public Sprite slotFeet;
    public Sprite slotWeapon;
    public Sprite slotOffhand;
    public Sprite slotAmulet;

    public Sprite StatIcon(StatType type)
    {
        switch (type)
        {
            case StatType.Strength: return iconStrength;
            case StatType.Mana: return iconMana;
            case StatType.Agility: return iconAgility;
            default: return iconHealth;
        }
    }

    public Sprite SlotIcon(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Head: return slotHead;
            case EquipSlot.Chest: return slotChest;
            case EquipSlot.Feet: return slotFeet;
            case EquipSlot.Weapon: return slotWeapon;
            case EquipSlot.Offhand: return slotOffhand;
            default: return slotAmulet;
        }
    }

    public static Color StatColor(StatType type)
    {
        switch (type)
        {
            case StatType.Strength: return FrostboundUI.Hex("#F08A5D");
            case StatType.Mana: return FrostboundUI.Hex("#6FA8FF");
            case StatType.Agility: return FrostboundUI.Hex("#7FDB94");
            default: return FrostboundUI.Hex("#F0646A");
        }
    }
}
