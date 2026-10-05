using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ventana de intercambio con dos rejillas: a la izquierda el arcón (60 casillas) o la tienda de Tico,
// a la derecha la mochila del héroe. Clic en un objeto: guardarlo / sacarlo (arcón) o vender / comprar
// (tienda, con un segundo clic para confirmar). También se guardan y sacan monedas del arcón.
public class TradeWindow : MonoBehaviour
{
    private enum Mode { Stash, Shop }

    public static bool IsOpen => _instance != null && _instance._open;
    private static TradeWindow _instance;

    private Mode _mode;
    private bool _open;
    private UISkin _skin;
    private CanvasGroup _group;
    private RectTransform _leftGrid, _rightGrid;
    private TextMeshProUGUI _title, _leftLabel, _leftCoins, _rightCoins, _infoName, _infoText;
    private RectTransform _coinButtons;
    private readonly List<ItemSlotView> _left = new List<ItemSlotView>(), _right = new List<ItemSlotView>();
    private Equipment _player;
    private Wallet _wallet;
    private ItemSlotView _armed;
    private bool _dirty;

    // Existencias de la tienda (se renuevan cuando el héroe sube de nivel).
    private static readonly List<ItemStack> Stock = new List<ItemStack>();
    private static int _stockLevel = -1;

    private const float Cell = 50f, Gap = 4f;
    private const int Cols = 10;

    public static void OpenStash(NPCInteractable npc) => Ensure().Open(Mode.Stash);
    public static void OpenShop(NPCInteractable npc, bool selling) => Ensure().Open(Mode.Shop);

    private static TradeWindow Ensure()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("VentanaIntercambio", typeof(RectTransform));
        _instance = go.AddComponent<TradeWindow>();
        _instance.Build();
        return _instance;
    }

    void OnDestroy()
    {
        if (_open) GameplayInput.Unblock();
        if (_instance == this) _instance = null;
        Unhook();
    }

    // ---------- Abrir y cerrar ----------

    private void Open(Mode mode)
    {
        _player = FindAnyObjectByType<Equipment>();
        if (_player == null) return;
        _wallet = Wallet.Of(_player.gameObject);
        _mode = mode;
        if (mode == Mode.Shop) RefreshStock();
        Unhook();
        _player.Inventory.Changed += MarkDirty;
        _wallet.Changed += OnCoins;
        Stash.Changed += MarkDirty;
        if (!_open) GameplayInput.Block();
        _open = true;
        _group.alpha = 1f;
        _group.blocksRaycasts = true;
        _group.interactable = true;
        _armed = null;
        _title.text = mode == Mode.Stash ? "ARCÓN DE LA TABERNA" : "TIENDA DE TICO";
        _leftLabel.text = mode == Mode.Stash ? "ARCÓN  ·  60 CASILLAS" : "A LA VENTA";
        _coinButtons.gameObject.SetActive(mode == Mode.Stash);
        SetInfo("", mode == Mode.Stash
            ? "Clic en un objeto de la mochila para guardarlo; clic en el arcón para sacarlo."
            : "Clic en un objeto de la mochila para venderlo; clic en la tienda para comprar. Un segundo clic confirma.", FrostboundUI.Muted);
        Refresh();
    }

    private void Close()
    {
        if (!_open) return;
        _open = false;
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        GameplayInput.Unblock();
        Unhook();
        if (GameSession.Instance != null) GameSession.Instance.SaveNow();
    }

    private void Unhook()
    {
        if (_player != null) _player.Inventory.Changed -= MarkDirty;
        if (_wallet != null) _wallet.Changed -= OnCoins;
        Stash.Changed -= MarkDirty;
    }

    private void MarkDirty() => _dirty = true;
    private void OnCoins(int c) => _dirty = true;

    void Update()
    {
        if (!_open) return;
        if (UICancel.Pressed()) { Close(); return; }
        if (_dirty) Refresh();
    }

    // ---------- Tienda ----------

    private void RefreshStock()
    {
        CharacterStats stats = _player.Stats;
        int level = stats != null ? stats.level : 1;
        if (level == _stockLevel && Stock.Count > 0) return;
        _stockLevel = level;
        Stock.Clear();
        GameDatabase gdb = GameDatabase.Instance;
        ItemDatabase db = gdb != null ? gdb.items : null;
        if (db == null) return;
        foreach (ItemDefinition it in db.items)
            if (it != null && it.IsConsumable && (it.heal > 0f || it.restoreMana > 0f)) Stock.Add(new ItemStack(it, 1));
        int seed = SeedUtil.Combine(GameSession.Instance != null ? GameSession.Instance.worldSeed : 12345, SeedUtil.FromString("tienda"), level);
        var rng = new DeterministicRng(seed);
        for (int tries = 0; tries < 200 && Stock.Count < 14; tries++)
            foreach (ItemStack s in LootRoller.Roll(db, level, 1, true, 0f, rng))
                if (s != null && s.item != null && s.item.IsEquippable && Stock.Count < 14) Stock.Add(s);
    }

    // ---------- Acciones ----------

    private void OnLeft(ItemSlotView v)
    {
        if (_mode == Mode.Stash)
        {
            ItemStack s = Stash.Items.Get(v.index);
            if (s == null) return;
            if (_player.Inventory.FreeSlots <= 0 && !_player.Inventory.HasSpaceFor(s.item, s.quantity)) { Toast("La mochila está llena", FrostboundUI.Negative); return; }
            ItemStack taken = Stash.Items.RemoveAt(v.index);
            if (taken != null && !_player.Inventory.AddStack(taken)) Stash.Items.AddStack(taken);
            return;
        }
        if (v.index < 0 || v.index >= Stock.Count) return;
        ItemStack item = Stock[v.index];
        int price = ItemPricing.BuyPrice(item);
        if (_armed != v) { Arm(v, "Clic otra vez para comprar por " + price + " monedas", FrostboundUI.Gold); return; }
        _armed = null;
        if (_wallet.Coins < price) { Toast("Te faltan " + (price - _wallet.Coins) + " monedas", FrostboundUI.Negative); return; }
        if (!_player.Inventory.HasSpaceFor(item.item, 1)) { Toast("La mochila está llena", FrostboundUI.Negative); return; }
        _wallet.Spend(price);
        bool consumable = item.item.IsConsumable;
        _player.Inventory.AddStack(consumable ? new ItemStack(item.item, 1) : item);
        if (!consumable) Stock.RemoveAt(v.index);
        Toast("Compraste " + item.DisplayName + " por " + price + " monedas", FrostboundUI.Positive);
        _dirty = true;
    }

    private void OnRight(ItemSlotView v)
    {
        ItemStack s = _player.Inventory.Get(v.index);
        if (s == null) return;
        if (_mode == Mode.Stash)
        {
            if (!Stash.Items.HasSpaceFor(s.item, s.quantity) && Stash.Items.FreeSlots <= 0) { Toast("El arcón está lleno", FrostboundUI.Negative); return; }
            ItemStack moved = _player.Inventory.RemoveAt(v.index);
            if (moved != null && !Stash.Items.AddStack(moved)) _player.Inventory.AddStack(moved);
            return;
        }
        int price = ItemPricing.SellPrice(s);
        if (_armed != v) { Arm(v, "Clic otra vez para vender por " + price + " monedas", FrostboundUI.Gold); return; }
        _armed = null;
        ItemStack sold = _player.Inventory.RemoveAt(v.index);
        if (sold == null) return;
        _wallet.Add(price);
        Toast("Vendiste " + sold.DisplayName + " por " + price + " monedas", FrostboundUI.Positive);
    }

    private void Arm(ItemSlotView v, string text, Color color)
    {
        if (_armed != null) _armed.Selected = false;
        _armed = v;
        v.Selected = true;
        v.Refresh();
        SetInfo(NameOf(v), text, color);
    }

    private void Toast(string text, Color color) => Notifications.Show(text, color);

    private void Coins(bool deposit, int amount)
    {
        if (deposit) Stash.DepositCoins(_wallet, amount < 0 ? _wallet.Coins : amount);
        else Stash.WithdrawCoins(_wallet, amount < 0 ? Stash.Coins : amount);
        _dirty = true;
    }

    // ---------- Mostrar ----------

    private void Refresh()
    {
        _dirty = false;
        if (_player == null) return;
        int leftCount = _mode == Mode.Stash ? Stash.Capacity : Mathf.Max(Stock.Count, 20);
        EnsureCells(_left, _leftGrid, leftCount, OnLeft);
        for (int i = 0; i < _left.Count; i++)
        {
            ItemStack s = null;
            if (i < leftCount) s = _mode == Mode.Stash ? Stash.Items.Get(i) : (i < Stock.Count ? Stock[i] : null);
            _left[i].gameObject.SetActive(i < leftCount);
            Show(_left[i], s);
        }
        Inventory inv = _player.Inventory;
        EnsureCells(_right, _rightGrid, inv.Capacity, OnRight);
        for (int i = 0; i < _right.Count; i++) Show(_right[i], i < inv.Capacity ? inv.Get(i) : null);
        _rightCoins.text = Fmt(_wallet.Coins) + " monedas";
        _leftCoins.text = _mode == Mode.Stash ? "Guardadas: " + Fmt(Stash.Coins) + " monedas   ·   " + Stash.Items.UsedSlots + "/" + Stash.Capacity : "";
    }

    private static string Fmt(int n) => n.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-ES"));

    private void Show(ItemSlotView v, ItemStack s)
    {
        v.Selected = v == _armed && s != null;
        v.Show(s != null ? s.item : null, s != null ? s.quantity : 0, true);
        v.ShowState(s);
    }

    private ItemStack StackOf(ItemSlotView v)
    {
        if (v == null || v.index < 0) return null;
        if (_left.Contains(v)) return _mode == Mode.Stash ? Stash.Items.Get(v.index) : (v.index < Stock.Count ? Stock[v.index] : null);
        return _player != null ? _player.Inventory.Get(v.index) : null;
    }

    private string NameOf(ItemSlotView v)
    {
        ItemStack s = StackOf(v);
        return s == null ? "" : "<color=" + FrostboundUI.RichHex(s.DisplayColor) + ">" + s.DisplayName + "</color>";
    }

    private void OnHover(ItemSlotView v, bool on)
    {
        if (!on || _armed != null) return;
        ItemStack s = StackOf(v);
        if (s == null) return;
        bool left = _left.Contains(v);
        string detail;
        if (_mode == Mode.Shop) detail = left ? "Precio: " + ItemPricing.BuyPrice(s) + " monedas" : "Te paga: " + ItemPricing.SellPrice(s) + " monedas";
        else detail = left ? "Clic para sacarlo a la mochila" : "Clic para guardarlo en el arcón";
        if (s.item.IsWeapon) detail += "   ·   Daño " + s.BaseDamage.ToString("0.#") + (s.itemLevel > 0 ? "   ·   Nivel " + s.itemLevel : "   ·   Arma inicial");
        if (s.item.armor > 0) detail += "   ·   Armadura " + s.item.armor;
        SetInfo(NameOf(v), detail, FrostboundUI.Body);
    }

    private void SetInfo(string name, string text, Color color)
    {
        _infoName.text = name;
        _infoText.text = text;
        _infoText.color = color;
    }

    // ---------- Construcción ----------

    private void Build()
    {
        _skin = GameDatabase.Instance != null ? GameDatabase.Instance.uiSkin : null;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(FrostboundUI.ReferenceWidth, FrostboundUI.ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;

        Image dim = UIFactory.Img(transform, "Velo", null, FrostboundUI.Dim);
        UIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        float panelW = Cols * Cell + (Cols - 1) * Gap + 40f, panelH = 6 * Cell + 5 * Gap + 110f;
        float w = panelW * 2f + 60f, h = panelH + 170f;
        RectTransform win = UIFactory.Node(transform, "Ventana");
        UIFactory.Anchored(win, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
        win.pivot = new Vector2(0.5f, 0.5f);
        Image frame = UIFactory.Frame(win, "Marco", Round(20), FrostboundUI.Bg, FrostboundUI.Border, out _);
        UIFactory.Stretch(frame.rectTransform);
        frame.raycastTarget = true;

        _title = UIFactory.Text(win, "Titulo", "", Font(0), 28f, FrostboundUI.Title, TextAlignmentOptions.TopLeft, 3f);
        UIFactory.TopLeft(_title.rectTransform, 28f, 18f, 700f, 40f);
        Button close = UIFactory.FramedButton(win, "Cerrar", _skin, Round(10), "Cerrar  ·  Esc", 14f, FrostboundUI.Surface, FrostboundUI.Border, FrostboundUI.Text, out _, out _);
        UIFactory.TopLeft((RectTransform)close.transform, w - 28f - 160f, 18f, 160f, 40f);
        close.onClick.AddListener(Close);

        RectTransform left = Panel(win, "Izquierda", 20f, 70f, panelW, panelH, out _leftLabel);
        RectTransform right = Panel(win, "Derecha", 40f + panelW, 70f, panelW, panelH, out TextMeshProUGUI rightLabel);
        rightLabel.text = "MOCHILA";
        _leftGrid = Grid(left);
        _rightGrid = Grid(right);

        _leftCoins = UIFactory.Text(left, "Monedas", "", Font(2), 14f, FrostboundUI.Gold, TextAlignmentOptions.TopLeft);
        UIFactory.TopLeft(_leftCoins.rectTransform, 20f, panelH - 34f, panelW - 40f, 20f);
        _rightCoins = UIFactory.Text(right, "Monedas", "", Font(2), 16f, FrostboundUI.Gold, TextAlignmentOptions.TopLeft);
        UIFactory.TopLeft(_rightCoins.rectTransform, 20f, panelH - 36f, panelW - 40f, 22f);

        _coinButtons = UIFactory.Node(win, "BotonesMonedas");
        UIFactory.TopLeft(_coinButtons, 20f, 80f + panelH, w - 40f, 44f);
        var buttons = new (string label, bool deposit, int amount)[]
        {
            ("Guardar 10", true, 10), ("Guardar 100", true, 100), ("Guardar todas", true, -1),
            ("Sacar 10", false, 10), ("Sacar 100", false, 100), ("Sacar todas", false, -1)
        };
        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            Button btn = UIFactory.FramedButton(_coinButtons, "Btn_" + i, _skin, Round(10), b.label, 12f, FrostboundUI.Surface, FrostboundUI.Border, FrostboundUI.Text, out _, out _);
            UIFactory.TopLeft((RectTransform)btn.transform, i * 150f + (i >= 3 ? 40f : 0f), 0f, 140f, 40f);
            btn.onClick.AddListener(() => Coins(b.deposit, b.amount));
        }

        _infoName = UIFactory.Text(win, "InfoNombre", "", Font(1), 16f, FrostboundUI.Title, TextAlignmentOptions.TopLeft);
        UIFactory.TopLeft(_infoName.rectTransform, 28f, h - 64f, w - 56f, 22f);
        _infoText = UIFactory.Text(win, "InfoTexto", "", Font(2), 14f, FrostboundUI.Muted, TextAlignmentOptions.TopLeft);
        UIFactory.TopLeft(_infoText.rectTransform, 28f, h - 38f, w - 56f, 22f);
    }

    private Sprite Round(int r)
    {
        if (_skin == null) return null;
        return r >= 20 ? _skin.round20 : r >= 12 ? _skin.round12 : _skin.round10;
    }

    private TMP_FontAsset Font(int kind)
    {
        if (_skin == null) return null;
        return kind == 0 ? _skin.cinzel800 : kind == 1 ? _skin.nunito800 : _skin.nunito700;
    }

    private RectTransform Panel(RectTransform parent, string name, float x, float y, float w, float h, out TextMeshProUGUI label)
    {
        Image frame = UIFactory.Frame(parent, name, Round(20), FrostboundUI.Surface, FrostboundUI.Border, out _);
        frame.raycastTarget = true;
        UIFactory.TopLeft(frame.rectTransform, x, y, w, h);
        label = UIFactory.Text(frame.rectTransform, "Etiqueta", "", Font(1), 12f, FrostboundUI.Muted, TextAlignmentOptions.TopLeft, 4f);
        UIFactory.TopLeft(label.rectTransform, 20f, 16f, w - 40f, 18f);
        return frame.rectTransform;
    }

    private RectTransform Grid(RectTransform panel)
    {
        RectTransform g = UIFactory.Node(panel, "Rejilla");
        UIFactory.TopLeft(g, 20f, 46f, Cols * Cell + (Cols - 1) * Gap, 6 * Cell + 5 * Gap);
        var gl = g.gameObject.AddComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(Cell, Cell);
        gl.spacing = new Vector2(Gap, Gap);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = Cols;
        return g;
    }

    private void EnsureCells(List<ItemSlotView> list, RectTransform grid, int count, System.Action<ItemSlotView> onClick)
    {
        while (list.Count < count)
        {
            ItemSlotView v = CreateSlotView(grid, "Casilla_" + list.Count, onClick);
            v.index = list.Count;
            list.Add(v);
        }
    }

    private ItemSlotView CreateSlotView(Transform parent, string name, System.Action<ItemSlotView> onClick)
    {
        Image frame = UIFactory.Frame(parent, name, Round(12), FrostboundUI.Surface, FrostboundUI.Border, out Image fill);
        var v = frame.gameObject.AddComponent<ItemSlotView>();
        v.border = frame;
        v.fill = fill;
        v.button = UIFactory.MakeButton(frame);
        v.icon = UIFactory.Img(frame.transform, "Icono", null, Color.white);
        RectTransform irt = UIFactory.Anchored(v.icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Cell * 0.72f, Cell * 0.72f));
        irt.pivot = new Vector2(0.5f, 0.5f);
        v.icon.preserveAspect = true;
        v.quantity = UIFactory.Text(frame.transform, "Cantidad", "", Font(1), 12f, FrostboundUI.White, TextAlignmentOptions.BottomRight);
        UIFactory.Stretch(v.quantity.rectTransform, 5f);
        v.quantity.outlineWidth = 0.25f;
        v.quantity.outlineColor = new Color32(10, 18, 34, 255);
        v.button.onClick.AddListener(() => onClick(v));
        v.Action = onClick;
        v.Hovered = OnHover;
        v.Show(null, 0, true);
        return v;
    }
}
