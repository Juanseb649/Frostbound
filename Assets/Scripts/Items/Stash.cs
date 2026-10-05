using UnityEngine;

// El arcón de la taberna: 60 casillas y monedas guardadas. Vive con la partida (no con la escena):
// se carga al empezar o cargar una partida y se guarda con ella.
public static class Stash
{
    public const int Capacity = 60;
    private static Inventory _inventory;
    private static int _coins;

    public static int Coins => _coins;
    public static event System.Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _inventory = null;
        _coins = 0;
        Changed = null;
    }

    public static Inventory Items
    {
        get
        {
            if (_inventory != null) return _inventory;
            var go = new GameObject("Arcon");
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            go.hideFlags = Application.isPlaying ? HideFlags.HideInHierarchy : HideFlags.HideAndDontSave;
            go.SetActive(false);
            _inventory = go.AddComponent<Inventory>();
            _inventory.capacity = Capacity;
            go.SetActive(true);
            _inventory.Changed += () => Changed?.Invoke();
            return _inventory;
        }
    }

    public static void DepositCoins(Wallet w, int amount)
    {
        if (w == null) return;
        amount = Mathf.Min(amount, w.Coins);
        if (amount <= 0 || !w.Spend(amount)) return;
        _coins += amount;
        Changed?.Invoke();
    }

    public static void WithdrawCoins(Wallet w, int amount)
    {
        if (w == null) return;
        amount = Mathf.Min(amount, _coins);
        if (amount <= 0) return;
        _coins -= amount;
        w.Add(amount);
        Changed?.Invoke();
    }

    public static void Load(SaveData data)
    {
        Inventory inv = Items;
        inv.Clear();
        _coins = data != null ? Mathf.Max(0, data.stashCoins) : 0;
        ItemDatabase db = GameDatabase.Instance != null ? GameDatabase.Instance.items : null;
        if (data != null && db != null)
            foreach (SavedStack d in data.stash)
            {
                ItemStack s = d.ToStack(db);
                if (s != null && !inv.PutAt(d.index, s)) inv.AddStack(s);
            }
        Changed?.Invoke();
    }

    public static void Capture(SaveData data)
    {
        if (data == null) return;
        data.stashCoins = _coins;
        data.stash.Clear();
        if (_inventory == null) return;
        for (int i = 0; i < _inventory.Capacity; i++)
        {
            ItemStack s = _inventory.Get(i);
            if (s == null || s.item == null) continue;
            SavedStack d = SavedStack.From(s);
            d.index = i;
            data.stash.Add(d);
        }
    }
}
