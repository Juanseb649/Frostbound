using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Cinturón de 6 accesos rápidos para consumibles (teclas 1–6). Cada casilla guarda un tipo de objeto;
// la cantidad sale de la mochila. Los consumibles nuevos ocupan la primera casilla libre.
[RequireComponent(typeof(Equipment))]
public class PotionBelt : MonoBehaviour
{
    public const int Size = 6;
    public ItemDefinition[] slots = new ItemDefinition[Size];

    public event Action Changed;
    public event Action<int> Used;
    public event Action<string> Failed;

    private Equipment _eq;

    void Awake()
    {
        _eq = GetComponent<Equipment>();
        if (slots == null || slots.Length != Size) Array.Resize(ref slots, Size);
    }

    void Start()
    {
        if (_eq.Inventory != null) _eq.Inventory.Changed += AutoAssign;
        AutoAssign();
    }

    void OnDestroy()
    {
        if (_eq != null && _eq.Inventory != null) _eq.Inventory.Changed -= AutoAssign;
    }

    public int Count(int slot)
    {
        ItemDefinition item = slot >= 0 && slot < Size ? slots[slot] : null;
        if (item == null || _eq == null || _eq.Inventory == null) return 0;
        int n = 0;
        Inventory inv = _eq.Inventory;
        for (int i = 0; i < inv.Capacity; i++)
        {
            ItemStack s = inv.Get(i);
            if (s != null && s.item == item) n += s.quantity;
        }
        return n;
    }

    public void Restore(ItemDefinition[] saved)
    {
        for (int i = 0; i < Size; i++) slots[i] = saved != null && i < saved.Length ? saved[i] : null;
        Changed?.Invoke();
    }

    public void Assign(int slot, ItemDefinition item)
    {
        if (slot < 0 || slot >= Size) return;
        for (int i = 0; i < Size; i++) if (slots[i] == item) slots[i] = null;
        slots[slot] = item;
        Changed?.Invoke();
    }

    public bool Use(int slot)
    {
        ItemDefinition item = slot >= 0 && slot < Size ? slots[slot] : null;
        if (item == null) return false;
        Inventory inv = _eq.Inventory;
        for (int i = 0; i < inv.Capacity; i++)
        {
            ItemStack s = inv.Get(i);
            if (s == null || s.item != item) continue;
            if (_eq.Use(i, out string msg))
            {
                Used?.Invoke(slot);
                Changed?.Invoke();
                return true;
            }
            if (!string.IsNullOrEmpty(msg)) Failed?.Invoke(msg);
            return false;
        }
        Failed?.Invoke("No te quedan: " + item.displayName);
        return false;
    }

    private void AutoAssign()
    {
        Inventory inv = _eq != null ? _eq.Inventory : null;
        if (inv == null) return;
        bool changed = false;
        for (int i = 0; i < inv.Capacity; i++)
        {
            ItemStack s = inv.Get(i);
            if (s == null || !s.item.IsConsumable || Array.IndexOf(slots, s.item) >= 0) continue;
            int free = Array.IndexOf(slots, null);
            if (free < 0) break;
            slots[free] = s.item;
            changed = true;
        }
        if (changed) Changed?.Invoke();
        else Changed?.Invoke();
    }

    void Update()
    {
        if (GameplayInput.Blocked) return;
        if (WorldHUD.Instance != null && WorldHUD.Instance.MenuOpen) return;
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        for (int i = 0; i < Size; i++)
            if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Use(i);
    }
}
