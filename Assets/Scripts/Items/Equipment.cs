using System;
using System.Collections.Generic;
using UnityEngine;

// Ranuras de equipo del héroe. Cada pieza (casco, torso, pies, arma...) se equipa por separado,
// suma sus atributos a CharacterStats y muestra su modelo con PenguinOutfit.
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(Inventory))]
public class Equipment : MonoBehaviour
{
    public static readonly EquipSlot[] Slots =
        { EquipSlot.Head, EquipSlot.Chest, EquipSlot.Feet, EquipSlot.Weapon, EquipSlot.Offhand, EquipSlot.Amulet };

    // Orden en que se visten los modelos: lo que va encima se pone al final.
    private static readonly EquipSlot[] VisualOrder =
        { EquipSlot.Chest, EquipSlot.Feet, EquipSlot.Head, EquipSlot.Offhand, EquipSlot.Weapon, EquipSlot.Amulet };

    [Tooltip("Si la clase tiene objetos iniciales, se equipan al empezar.")]
    public bool giveClassStartingItems = true;
    public PenguinOutfit outfit;

    private readonly Dictionary<EquipSlot, ItemDefinition> _equipped = new Dictionary<EquipSlot, ItemDefinition>();
    private CharacterStats _stats;
    private Inventory _inventory;
    private bool _started;

    public event Action Changed;
    public event Action<ItemDefinition> ItemUsed;

    public Inventory Inventory => _inventory;
    public CharacterStats Stats => _stats;

    void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _inventory = GetComponent<Inventory>();
        if (outfit == null) outfit = GetComponentInChildren<PenguinOutfit>();
        if (UsesItems())
        {
            // El equipo lo controla este componente, no la ropa fija de la clase.
            if (outfit != null) outfit.useClassOutfit = false;
            HeroGearEquipper gear = GetComponentInChildren<HeroGearEquipper>();
            if (gear != null) gear.startingClass = "";
        }
    }

    void Start()
    {
        _started = true;
        if (UsesItems()) GiveStartingItems();
        Apply();
    }

    private bool UsesItems()
    {
        CharacterClass cls = _stats != null ? _stats.characterClass : null;
        return giveClassStartingItems && cls != null && cls.startingItems != null && cls.startingItems.Length > 0;
    }

    private void GiveStartingItems()
    {
        foreach (ItemDefinition item in _stats.characterClass.startingItems)
        {
            if (item == null) continue;
            if (item.IsEquippable && Get(item.equipSlot) == null) _equipped[item.equipSlot] = item;
            else _inventory.Add(item);
        }
    }

    public ItemDefinition Get(EquipSlot slot) => _equipped.TryGetValue(slot, out ItemDefinition item) ? item : null;

    public bool IsEquipped(ItemDefinition item) => item != null && item.IsEquippable && Get(item.equipSlot) == item;

    public int PiecesEquipped(ArmorSet set)
    {
        if (set == null) return 0;
        int n = 0;
        foreach (ItemDefinition item in _equipped.Values)
            if (item != null && item.armorSet == set) n++;
        return n;
    }

    // ----- Acciones -----

    // Acción principal de una casilla de la mochila: equipar o usar.
    public bool UseOrEquip(int inventoryIndex, out string message)
    {
        ItemStack s = _inventory.Get(inventoryIndex);
        message = "";
        if (s == null) return false;
        if (s.item.IsEquippable) return EquipFromInventory(inventoryIndex, out message);
        if (s.item.IsConsumable) return Use(inventoryIndex, out message);
        message = "No se puede usar";
        return false;
    }

    public bool EquipFromInventory(int inventoryIndex, out string message)
    {
        ItemStack s = _inventory.Get(inventoryIndex);
        message = "";
        if (s == null) return false;
        if (!s.item.CanBeEquippedBy(_stats, out message)) return false;

        EquipSlot slot = s.item.equipSlot;
        ItemDefinition previous = Get(slot);
        _inventory.RemoveAt(inventoryIndex, 1);
        _equipped[slot] = s.item;
        // Lo que llevaba puesto ocupa la casilla que quedó libre.
        if (previous != null && !_inventory.PutAt(inventoryIndex, previous)) _inventory.Add(previous);
        Apply();
        return true;
    }

    public bool Unequip(EquipSlot slot, out string message)
    {
        message = "";
        ItemDefinition item = Get(slot);
        if (item == null) return false;
        if (!_inventory.TryAdd(item))
        {
            message = "La mochila está llena";
            return false;
        }
        _equipped.Remove(slot);
        Apply();
        return true;
    }

    public bool Use(int inventoryIndex, out string message)
    {
        ItemStack s = _inventory.Get(inventoryIndex);
        message = "";
        if (s == null || !s.item.IsConsumable) return false;
        bool needsHealth = s.item.heal > 0f && _stats.currentHealth < _stats.MaxHealth;
        bool needsMana = s.item.restoreMana > 0f && _stats.currentMana < _stats.MaxMana;
        if (!needsHealth && !needsMana)
        {
            message = s.item.heal > 0f ? "Tu vida ya está llena" : "Tu maná ya está lleno";
            return false;
        }
        if (s.item.heal > 0f) _stats.Heal(s.item.heal);
        if (s.item.restoreMana > 0f) _stats.RestoreMana(s.item.restoreMana);
        _inventory.RemoveAt(inventoryIndex, 1);
        ItemUsed?.Invoke(s.item);
        return true;
    }

    // Usa el primer consumible que cure vida (para una tecla rápida).
    public bool UseFirstHealing(out string message)
    {
        message = "No tienes pociones de vida";
        for (int i = 0; i < _inventory.Capacity; i++)
        {
            ItemStack s = _inventory.Get(i);
            if (s != null && s.item.IsConsumable && s.item.heal > 0f) return Use(i, out message);
        }
        return false;
    }

    // Equipa directamente sin pasar por la mochila (carga de partida, recompensas).
    public void EquipDirect(ItemDefinition item)
    {
        if (item == null || !item.IsEquippable) return;
        _equipped[item.equipSlot] = item;
        if (_started) Apply();
    }

    // ----- Estadísticas y modelo -----

    public void Apply()
    {
        Recalculate();
        RefreshVisuals();
        Changed?.Invoke();
    }

    private void Recalculate()
    {
        var bonus = new StatBlock();
        int armor = 0;
        float damage = 0f;
        var sets = new HashSet<ArmorSet>();

        foreach (ItemDefinition item in _equipped.Values)
        {
            if (item == null) continue;
            bonus = bonus + item.StatBonus;
            armor += item.armor;
            damage += item.damage;
            if (item.armorSet != null) sets.Add(item.armorSet);
        }

        foreach (ArmorSet set in sets)
        {
            int count = PiecesEquipped(set);
            foreach (ArmorSet.Bonus b in set.bonuses)
            {
                if (count < b.piecesRequired) continue;
                foreach (StatModifier m in b.stats) bonus.Add(m.stat, m.value);
                armor += b.armor;
            }
        }

        _stats.SetEquipmentBonus(bonus, armor, damage);
    }

    private void RefreshVisuals()
    {
        if (outfit == null) return;
        outfit.UnequipAll();
        foreach (EquipSlot slot in VisualOrder)
        {
            ItemDefinition item = Get(slot);
            if (item != null && item.outfit != null) outfit.Equip(item.outfit);
        }
    }
}
