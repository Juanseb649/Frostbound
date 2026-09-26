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

    public ItemStack(ItemDefinition item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
        EnsureInstance();
    }

    public bool IsEmpty => item == null || quantity <= 0;
    public int SpaceLeft => item == null ? 0 : item.maxStack - quantity;

    // Estado propio: no se apila con otras copias del mismo objeto.
    public bool HasInstanceData => item != null && (item.HasDurability || item.runeSlots > 0);

    public void EnsureInstance()
    {
        if (runes == null) runes = new List<ItemDefinition>();
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
        var c = new ItemStack(item, quantity) { durability = durability };
        c.runes = new List<ItemDefinition>(runes ?? new List<ItemDefinition>());
        return c;
    }
}
