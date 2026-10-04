using System;
using System.Collections.Generic;
using UnityEngine;

// Mochila estilo Minecraft Dungeons: casillas iguales (un objeto por casilla, sin formas como en Diablo),
// mucha capacidad y los objetos apilables (pociones, materiales) se agrupan.
public class Inventory : MonoBehaviour
{
    [Tooltip("Casillas de la mochila.")]
    [Min(1)] public int capacity = 60;

    [SerializeField] private List<ItemStack> slots = new List<ItemStack>();

    public event Action Changed;
    public event Action<ItemDefinition, int> ItemAdded;
    // Casilla que se está añadiendo mientras se dispara ItemAdded (para mostrar su nombre y color de botín).
    public ItemStack LastAddedStack { get; private set; }

    public int Capacity => slots.Count;

    void Awake() => EnsureSize();

    void OnValidate() => EnsureSize();

    private void EnsureSize()
    {
        while (slots.Count < capacity) slots.Add(null);
        while (slots.Count > capacity && IsEmptySlot(slots.Count - 1)) slots.RemoveAt(slots.Count - 1);
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null && slots[i].IsEmpty) slots[i] = null;
    }

    private bool IsEmptySlot(int i) => slots[i] == null || slots[i].IsEmpty;

    public ItemStack Get(int index) => index >= 0 && index < slots.Count && !IsEmptySlot(index) ? slots[index] : null;

    public int UsedSlots
    {
        get
        {
            int n = 0;
            for (int i = 0; i < slots.Count; i++) if (!IsEmptySlot(i)) n++;
            return n;
        }
    }

    public int FreeSlots => slots.Count - UsedSlots;

    public int Count(ItemDefinition item)
    {
        int n = 0;
        foreach (ItemStack s in slots) if (s != null && s.item == item) n += s.quantity;
        return n;
    }

    public bool HasSpaceFor(ItemDefinition item, int quantity = 1)
    {
        if (item == null) return false;
        int room = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (IsEmptySlot(i)) room += item.maxStack;
            else if (slots[i].item == item) room += slots[i].SpaceLeft;
            if (room >= quantity) return true;
        }
        return false;
    }

    // Devuelve la cantidad que no cupo.
    public int Add(ItemDefinition item, int quantity = 1)
    {
        if (item == null || quantity <= 0) return quantity;
        EnsureSize();
        int left = quantity;

        if (item.IsStackable)
            for (int i = 0; i < slots.Count && left > 0; i++)
            {
                if (IsEmptySlot(i) || slots[i].item != item) continue;
                int n = Mathf.Min(left, slots[i].SpaceLeft);
                slots[i].quantity += n;
                left -= n;
            }

        for (int i = 0; i < slots.Count && left > 0; i++)
        {
            if (!IsEmptySlot(i)) continue;
            int n = Mathf.Min(left, item.maxStack);
            slots[i] = new ItemStack(item, n);
            left -= n;
        }

        int added = quantity - left;
        if (added > 0)
        {
            ItemAdded?.Invoke(item, added);
            Changed?.Invoke();
        }
        return left;
    }

    // Añade una casilla conservando su estado (durabilidad, runas). Devuelve false si no cupo.
    public bool AddStack(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty) return true;
        if (!stack.HasInstanceData && stack.item.IsStackable) return Add(stack.item, stack.quantity) <= 0;
        EnsureSize();
        for (int i = 0; i < slots.Count; i++)
        {
            if (!IsEmptySlot(i)) continue;
            stack.EnsureInstance();
            slots[i] = stack;
            LastAddedStack = stack;
            ItemAdded?.Invoke(stack.item, stack.quantity);
            LastAddedStack = null;
            Changed?.Invoke();
            return true;
        }
        return false;
    }

    public bool PutAt(int index, ItemStack stack)
    {
        if (stack == null || index < 0 || index >= slots.Count || !IsEmptySlot(index)) return false;
        slots[index] = stack;
        Changed?.Invoke();
        return true;
    }

    // Avisa a la interfaz cuando cambia el estado de un objeto (durabilidad, runas).
    public void NotifyChanged() => Changed?.Invoke();

    public bool TryAdd(ItemDefinition item, int quantity = 1)
    {
        if (!HasSpaceFor(item, quantity)) return false;
        Add(item, quantity);
        return true;
    }

    public ItemStack RemoveAt(int index, int quantity = int.MaxValue)
    {
        ItemStack s = Get(index);
        if (s == null) return null;
        int n = Mathf.Min(quantity, s.quantity);
        if (n >= s.quantity)
        {
            slots[index] = null;
            Changed?.Invoke();
            return s;
        }
        s.quantity -= n;
        Changed?.Invoke();
        return new ItemStack(s.item, n);
    }

    public bool Remove(ItemDefinition item, int quantity = 1)
    {
        if (Count(item) < quantity) return false;
        for (int i = slots.Count - 1; i >= 0 && quantity > 0; i--)
        {
            if (IsEmptySlot(i) || slots[i].item != item) continue;
            int n = Mathf.Min(quantity, slots[i].quantity);
            slots[i].quantity -= n;
            quantity -= n;
            if (slots[i].quantity <= 0) slots[i] = null;
        }
        Changed?.Invoke();
        return true;
    }

    // Coloca un objeto en una casilla concreta si está libre (usado al quitarse equipo).
    public bool PutAt(int index, ItemDefinition item)
    {
        if (index < 0 || index >= slots.Count || !IsEmptySlot(index)) return false;
        slots[index] = new ItemStack(item, 1);
        Changed?.Invoke();
        return true;
    }

    public void Swap(int a, int b)
    {
        if (a == b || a < 0 || b < 0 || a >= slots.Count || b >= slots.Count) return;
        ItemStack sa = Get(a), sb = Get(b);
        if (sa != null && sb != null && sa.item == sb.item && sa.item.IsStackable)
        {
            int n = Mathf.Min(sa.quantity, sb.SpaceLeft);
            sb.quantity += n;
            sa.quantity -= n;
            if (sa.quantity <= 0) slots[a] = null;
        }
        else
        {
            slots[a] = sb;
            slots[b] = sa;
        }
        Changed?.Invoke();
    }

    // Agrupa pilas y ordena: armas, armadura (casco, torso, pies...), consumibles, materiales; rareza de mayor a menor.
    public void Sort()
    {
        var items = new List<ItemStack>();
        foreach (ItemStack s in slots) if (s != null && !s.IsEmpty) items.Add(s);
        for (int i = 0; i < slots.Count; i++) slots[i] = null;

        items.Sort((x, y) =>
        {
            int c = Order(x.item.category).CompareTo(Order(y.item.category));
            if (c != 0) return c;
            c = ((int)x.item.equipSlot).CompareTo((int)y.item.equipSlot);
            if (c != 0) return c;
            c = ((int)y.item.rarity).CompareTo((int)x.item.rarity);
            if (c != 0) return c;
            return string.Compare(x.item.displayName, y.item.displayName, StringComparison.CurrentCulture);
        });

        var merged = new List<ItemStack>();
        foreach (ItemStack s in items)
        {
            if (s.HasInstanceData || !s.item.IsStackable)
            {
                merged.Add(s);
                continue;
            }
            int left = s.quantity;
            foreach (ItemStack m in merged)
            {
                if (left <= 0) break;
                if (m.item != s.item || m.SpaceLeft <= 0 || m.HasInstanceData) continue;
                int n = Mathf.Min(left, m.SpaceLeft);
                m.quantity += n;
                left -= n;
            }
            if (left > 0) merged.Add(new ItemStack(s.item, left));
        }
        for (int i = 0; i < merged.Count && i < slots.Count; i++) slots[i] = merged[i];
        Changed?.Invoke();
    }

    private static int Order(ItemCategory c)
    {
        switch (c)
        {
            case ItemCategory.Weapon: return 0;
            case ItemCategory.Armor: return 1;
            case ItemCategory.Consumable: return 2;
            case ItemCategory.Rune: return 3;
            case ItemCategory.Material: return 4;
            default: return 5;
        }
    }

    // Para mejoras futuras de mochila.
    public void Expand(int extraSlots)
    {
        capacity += Mathf.Max(0, extraSlots);
        EnsureSize();
        Changed?.Invoke();
    }

    public void Clear()
    {
        for (int i = 0; i < slots.Count; i++) slots[i] = null;
        Changed?.Invoke();
    }
}
