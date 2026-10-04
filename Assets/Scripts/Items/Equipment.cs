using System;
using System.Collections.Generic;
using UnityEngine;

// Ranuras de equipo del héroe. Cada pieza (casco, torso, pies, arma...) se equipa por separado,
// suma sus atributos a CharacterStats y muestra su modelo (ropa con PenguinOutfit, armas con WeaponHolder).
// Cada casilla guarda su propio estado: durabilidad y runas grabadas en el arma.
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
    public WeaponHolder weaponHolder;

    private readonly Dictionary<EquipSlot, ItemStack> _equipped = new Dictionary<EquipSlot, ItemStack>();
    private CharacterStats _stats;
    private Inventory _inventory;
    private bool _started;
    private OutfitCloth _cloth;

    public event Action Changed;
    public event Action<ItemDefinition> ItemUsed;
    public event Action<ItemStack> WeaponBroke;

    public Inventory Inventory => _inventory;
    public CharacterStats Stats => _stats;
    public ItemStack MainWeapon => GetStack(EquipSlot.Weapon);

    void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _inventory = GetComponent<Inventory>();
        if (outfit == null) outfit = GetComponentInChildren<PenguinOutfit>();
        if (weaponHolder == null) weaponHolder = GetComponentInChildren<WeaponHolder>();
        if (weaponHolder == null && outfit != null) weaponHolder = outfit.gameObject.AddComponent<WeaponHolder>();
        if (outfit != null)
        {
            _cloth = outfit.GetComponent<OutfitCloth>();
            if (_cloth == null) _cloth = outfit.gameObject.AddComponent<OutfitCloth>();
        }
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
        GameSession session = GameSession.Instance;
        bool restored = session != null && session.RestorePlayer(this);
        if (!restored && UsesItems()) GiveStartingItems();
        Apply();
        if (restored) session.FinishRestore(this);
    }

    public void SetEquippedStack(EquipSlot slot, ItemStack stack)
    {
        if (stack == null || stack.item == null) _equipped.Remove(slot);
        else _equipped[slot] = stack;
        if (_started) Apply();
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
            if (item.IsEquippable && Get(item.equipSlot) == null) _equipped[item.equipSlot] = new ItemStack(item, 1);
            else _inventory.Add(item);
        }
    }

    public ItemStack GetStack(EquipSlot slot) => _equipped.TryGetValue(slot, out ItemStack s) ? s : null;

    public ItemDefinition Get(EquipSlot slot)
    {
        ItemStack s = GetStack(slot);
        return s != null ? s.item : null;
    }

    public bool IsEquipped(ItemDefinition item) => item != null && item.IsEquippable && Get(item.equipSlot) == item;

    public int PiecesEquipped(ArmorSet set)
    {
        if (set == null) return 0;
        int n = 0;
        foreach (ItemStack s in _equipped.Values)
            if (s != null && s.item != null && s.item.armorSet == set) n++;
        return n;
    }

    // ----- Acciones -----

    // Acción principal de una casilla de la mochila: equipar, usar o grabar una runa.
    public bool UseOrEquip(int inventoryIndex, out string message)
    {
        ItemStack s = _inventory.Get(inventoryIndex);
        message = "";
        if (s == null) return false;
        if (s.item.IsEquippable) return EquipFromInventory(inventoryIndex, out message);
        if (s.item.IsConsumable) return Use(inventoryIndex, out message);
        if (s.item.IsRune) return EngraveRune(inventoryIndex, out message);
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
        // Qué hay que quitar: la misma ranura y, con armas a dos manos, la otra aleta.
        var displaced = new List<EquipSlot> { slot };
        if (s.item.UsesBothHands) displaced.Add(EquipSlot.Offhand);
        if (slot == EquipSlot.Offhand && Get(EquipSlot.Weapon) != null && Get(EquipSlot.Weapon).UsesBothHands) displaced.Add(EquipSlot.Weapon);

        int toStore = 0;
        foreach (EquipSlot d in displaced) if (GetStack(d) != null) toStore++;
        if (toStore > 1 && _inventory.FreeSlots < toStore - 1)
        {
            message = "La mochila está llena";
            return false;
        }

        ItemStack moving = _inventory.RemoveAt(inventoryIndex);
        bool usedFreedSlot = false;
        foreach (EquipSlot d in displaced)
        {
            ItemStack previous = GetStack(d);
            if (previous == null) continue;
            _equipped.Remove(d);
            // Lo primero que se quita ocupa la casilla que quedó libre.
            if (!usedFreedSlot && _inventory.PutAt(inventoryIndex, previous)) usedFreedSlot = true;
            else _inventory.AddStack(previous);
        }
        moving.EnsureInstance();
        _equipped[slot] = moving;
        Apply();
        return true;
    }

    public bool Unequip(EquipSlot slot, out string message)
    {
        message = "";
        ItemStack stack = GetStack(slot);
        if (stack == null) return false;
        if (_inventory.FreeSlots <= 0)
        {
            message = "La mochila está llena";
            return false;
        }
        _equipped.Remove(slot);
        _inventory.AddStack(stack);
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
        _equipped[item.equipSlot] = new ItemStack(item, 1);
        if (_started) Apply();
    }

    // ----- Runas -----

    // Hechizo de grabado: la runa de la mochila queda grabada en el arma equipada y cuesta maná.
    public bool EngraveRune(int inventoryIndex, out string message)
    {
        message = "";
        ItemStack rune = _inventory.Get(inventoryIndex);
        if (rune == null || !rune.item.IsRune) return false;
        ItemStack weapon = MainWeapon;
        if (weapon == null)
        {
            message = "Equipa un arma para grabar la runa";
            return false;
        }
        if (weapon.item.runeSlots <= 0)
        {
            message = weapon.item.displayName + " no admite runas";
            return false;
        }
        if (weapon.FreeRuneSlots <= 0)
        {
            message = "No quedan ranuras de runa en " + weapon.item.displayName;
            return false;
        }
        if (weapon.IsBroken)
        {
            message = "Repara el arma antes de grabarle runas";
            return false;
        }
        if (!_stats.SpendMana(rune.item.engraveManaCost))
        {
            message = "Necesitas " + rune.item.engraveManaCost + " de maná para el hechizo de grabado";
            return false;
        }
        weapon.runes.Add(rune.item);
        _inventory.RemoveAt(inventoryIndex, 1);
        message = "La runa " + WeaponCatalog.RuneName(rune.item.runeEffect) + " quedó grabada en " + weapon.item.displayName;
        Apply();
        return true;
    }

    public float RunePower(RuneEffect effect)
    {
        ItemStack w = MainWeapon;
        if (w == null || w.IsBroken) return 0f;
        float total = 0f;
        foreach (ItemDefinition r in w.runes)
            if (r != null && r.runeEffect == effect) total += r.runePower;
        return total;
    }

    // ----- Durabilidad -----

    public void WearWeapon(float amount)
    {
        ItemStack w = MainWeapon;
        if (w == null) return;
        bool broke = w.Wear(amount);
        if (broke)
        {
            Apply();
            WeaponBroke?.Invoke(w);
        }
        else Changed?.Invoke();
    }

    // Lo que hace el herrero: deja como nuevas las armas equipadas y las de la mochila.
    public int RepairAll()
    {
        int repaired = 0;
        foreach (ItemStack s in _equipped.Values)
            if (s != null && s.item.HasDurability && s.durability < s.item.maxDurability) { s.Repair(); repaired++; }
        for (int i = 0; i < _inventory.Capacity; i++)
        {
            ItemStack s = _inventory.Get(i);
            if (s != null && s.item.HasDurability && s.durability < s.item.maxDurability) { s.Repair(); repaired++; }
        }
        _inventory.NotifyChanged();
        Apply();
        return repaired;
    }

    // ----- Estadísticas y modelo -----

    // Suma de un afijo en todo el equipo puesto (sin contar lo roto).
    public float AffixSum(AffixKind kind)
    {
        float total = 0f;
        foreach (ItemStack s in _equipped.Values)
            if (s != null && s.item != null && !s.IsBroken) total += s.AffixSum(kind);
        return total;
    }

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

        foreach (ItemStack s in _equipped.Values)
        {
            if (s == null || s.item == null) continue;
            // Un objeto roto no da bonificaciones (como en Diablo II).
            if (s.IsBroken) continue;
            ItemDefinition item = s.item;
            bonus = bonus + item.StatBonus;
            armor += item.armor;
            float d = item.damage;
            if (item.equipSlot == EquipSlot.Weapon)
                foreach (ItemDefinition r in s.runes)
                    if (r != null && r.runeEffect == RuneEffect.Sharpness) d *= 1f + r.runePower;
            damage += d;
            if (item.armorSet != null) sets.Add(item.armorSet);

            // Afijos del botín (spec §5.5).
            bonus.Add(StatType.Strength, Mathf.RoundToInt(s.AffixSum(AffixKind.Strength)));
            bonus.Add(StatType.Mana, Mathf.RoundToInt(s.AffixSum(AffixKind.Mana)));
            bonus.Add(StatType.Agility, Mathf.RoundToInt(s.AffixSum(AffixKind.Agility)));
            bonus.Add(StatType.Health, Mathf.RoundToInt(s.AffixSum(AffixKind.Health)));
            armor += Mathf.RoundToInt(s.AffixSum(AffixKind.Armor));
            damage += s.AffixSum(AffixKind.Damage);
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
        if (outfit != null)
        {
            outfit.UnequipAll();
            foreach (EquipSlot slot in VisualOrder)
            {
                ItemDefinition item = Get(slot);
                if (item == null || item.weaponModel != null) continue;
                if (item.outfit != null) outfit.Equip(item.outfit);
            }
        }
        if (weaponHolder != null) weaponHolder.Show(Get(EquipSlot.Weapon), Get(EquipSlot.Offhand));
        // Capas y telas sueltas con física: esquivan el cuerpo y la hoja del arma.
        if (_cloth != null && Application.isPlaying) _cloth.Refresh(weaponHolder != null ? weaponHolder.BladeCollider : null);
        PenguinRigAnimator rig = outfit != null ? outfit.GetComponent<PenguinRigAnimator>() : null;
        if (rig != null) rig.RefreshBodyCapsule();
    }
}
