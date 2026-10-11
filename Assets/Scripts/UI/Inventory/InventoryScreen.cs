using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Ventana del héroe por pestañas, como en Diablo II: Personaje (C) con nivel, atributos y habilidad,
// Equipo con las piezas puestas y su resumen, e Inventario (I) con la mochila. Tab cambia de pestaña.
// Usa la tipografía y los colores del menú principal (FrostUI).
public class InventoryScreen : MonoBehaviour
{
    public UISkin skin;
    public Equipment player;

    public bool IsOpen { get; private set; }

    public enum Tab { Character, Equipment, Inventory }
    public Tab Current { get; private set; } = Tab.Inventory;

    private enum Filter { All, Armor, Weapons, Consumables, Other }

    private struct AttrRow
    {
        public TextMeshProUGUI value;
        public Button plus;
        public Image plusFill;
        public TextMeshProUGUI plusLabel;
    }

    private CharacterStats _stats;
    private Inventory _inventory;
    private CanvasGroup _group;
    private RectTransform _window;
    private bool _dirty = true;

    // Atributos
    private TextMeshProUGUI _heroName, _className, _levelText, _xpText, _pointsText, _derivedLabels, _derivedValues;
    private Image _pointsFill;
    private RectTransform _xpFill;
    private readonly AttrRow[] _rows = new AttrRow[4];

    // Equipo y detalle
    private readonly Dictionary<EquipSlot, ItemSlotView> _equipViews = new Dictionary<EquipSlot, ItemSlotView>();
    private TextMeshProUGUI _setText, _dName, _dMeta, _dBody, _dEmpty, _dPrimaryLabel, _dSecondaryLabel;
    private Button _dPrimary, _dSecondary;
    private Image _dPrimaryFill;

    // Mochila
    private readonly List<ItemSlotView> _cells = new List<ItemSlotView>();
    private RectTransform _grid;
    private ScrollRect _scroll;
    private TextMeshProUGUI _countText;
    private readonly List<(Filter filter, Image border, Image fill, TextMeshProUGUI label)> _tabs = new List<(Filter, Image, Image, TextMeshProUGUI)>();
    private Filter _filter = Filter.All;

    // Selección
    private ItemSlotView _selected;
    private ItemSlotView _hovered;
    private bool _confirmDiscard;

    // Pestañas
    private readonly List<(Tab tab, FrostButton button)> _tabButtons = new List<(Tab, FrostButton)>();
    private readonly Dictionary<Tab, List<RectTransform>> _pages = new Dictionary<Tab, List<RectTransform>>();
    private RectTransform _detailPanel;
    private TextMeshProUGUI _kicker;
    private Image _abilityIcon;
    private TextMeshProUGUI _abilityName, _abilityText, _abilityStats, _journeyValues, _summaryValues, _summarySets, _durability, _purseCoins, _purseInfo;

    private TMP_FontAsset DisplayFont => FrostUI.Display != null ? FrostUI.Display : DisplayFont;

    private const float WindowW = 988f;
    private const float WindowH = 910f;
    private const float Top = 152f;

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<Equipment>();
        if (player == null || skin == null)
        {
            Debug.LogWarning("[InventoryScreen] Falta el jugador (Equipment) o el UISkin.");
            enabled = false;
            return;
        }
        _stats = player.Stats;
        _inventory = player.Inventory;
        Build();
        _stats.StatsChanged += MarkDirty;
        _inventory.Changed += MarkDirty;
        player.Changed += MarkDirty;
        SetOpen(false);
    }

    void OnDestroy()
    {
        if (_stats != null) _stats.StatsChanged -= MarkDirty;
        if (_inventory != null) _inventory.Changed -= MarkDirty;
        if (player != null) player.Changed -= MarkDirty;
        if (IsOpen) GameplayInput.Unblock();
    }

    private void MarkDirty() => _dirty = true;

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        bool blocked = PauseMenu.IsPaused || LoadingScreen.Active
            || (IsOpen && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null);
        if (!blocked)
        {
            if (kb != null && kb.cKey.wasPressedThisFrame) Press(Tab.Character);
            else if (kb != null && kb.iKey.wasPressedThisFrame) Press(Tab.Inventory);
            else if ((kb != null && kb.tabKey.wasPressedThisFrame) || (IsOpen && pad != null && pad.rightShoulder.wasPressedThisFrame))
            {
                if (IsOpen) SetTab((Tab)(((int)Current + 1) % 3));
                else SetOpen(true);
            }
            else if (IsOpen && pad != null && pad.leftShoulder.wasPressedThisFrame) SetTab((Tab)(((int)Current + 2) % 3));
            else if (pad != null && pad.selectButton.wasPressedThisFrame) SetOpen(!IsOpen);
            else if (IsOpen && UICancel.Pressed()) SetOpen(false);
        }

        if (IsOpen && Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame && _selected != null) DoAction(_selected);
    }

    void LateUpdate()
    {
        if (!IsOpen) return;
        FitWindow();
        if (_dirty) Refresh();
    }

    public void Open() => OpenTab(Tab.Character);
    public void Close() => SetOpen(false);

    public void OpenTab(Tab tab)
    {
        SetTab(tab);
        SetOpen(true);
    }

    // La tecla de una pestaña la abre; si ya está abierta en esa pestaña, cierra la ventana.
    private void Press(Tab tab)
    {
        if (IsOpen && Current == tab) SetOpen(false);
        else OpenTab(tab);
    }

    public void SetTab(Tab tab)
    {
        Current = tab;
        if (_group == null) return;
        foreach (var page in _pages)
            foreach (RectTransform rt in page.Value) rt.gameObject.SetActive(page.Key == tab);
        bool detail = tab != Tab.Character;
        _detailPanel.gameObject.SetActive(detail);
        if (detail) UIFactory.TopLeft(_detailPanel, 32f, tab == Tab.Equipment ? Top + 396f : Top, 380f, 304f);
        foreach (var b in _tabButtons)
        {
            b.button.selectedLook = b.tab == tab;
            b.button.Refresh();
        }
        _kicker.text = (tab == Tab.Character ? "Personaje · C" : tab == Tab.Equipment ? "Equipo · Tab" : "Inventario · I").ToUpperInvariant();
        _hovered = null;
        Select(null);
        _dirty = true;
        if (IsOpen && EventSystem.current != null)
        {
            GameObject first = tab == Tab.Inventory && _cells.Count > 0 ? _cells[0].gameObject
                : tab == Tab.Equipment ? _equipViews[EquipSlot.Weapon].gameObject : null;
            EventSystem.current.SetSelectedGameObject(first);
        }
    }

    private void SetOpen(bool open)
    {
        if (_group == null) return;
        if (open == IsOpen && _group.alpha == (open ? 1f : 0f)) return;
        bool wasOpen = IsOpen;
        IsOpen = open;
        _group.alpha = open ? 1f : 0f;
        _group.interactable = open;
        _group.blocksRaycasts = open;
        if (open && !wasOpen) GameplayInput.Block();
        if (!open && wasOpen) GameplayInput.Unblock();
        if (open != wasOpen) FreezePlayer(open);

        if (open)
        {
            _dirty = true;
            SetTab(Current);
            Refresh();
        }
        else
        {
            _hovered = null;
            _confirmDiscard = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // Mientras la pantalla está abierta el héroe no se mueve ni habla con los NPC.
    private void FreezePlayer(bool frozen)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null) controller.enabled = !frozen;
        if (!frozen) return;
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        if (WorldHUD.Instance != null && WorldHUD.Instance.MenuOpen) WorldHUD.Instance.CloseMenu();
    }

    private void FitWindow()
    {
        var parent = (RectTransform)transform;
        float s = Mathf.Min(1f, parent.rect.width / (WindowW + 32f), parent.rect.height / (WindowH + 16f));
        _window.localScale = new Vector3(s, s, 1f);
    }

    // ----- Refresco -----

    private void Refresh()
    {
        _dirty = false;
        RefreshAttributes();
        RefreshEquipment();
        RefreshBackpack();
        RefreshDetail();
        RefreshPages();
    }

    private string Hex(Color c) => FrostboundUI.RichHex(c);

    // Paneles propios de cada pestaña: habilidad y viaje, resumen del equipo, monedas y cinturón.
    private void RefreshPages()
    {
        PlayerAbility ability = player.GetComponent<PlayerAbility>();
        if (ability != null)
        {
            PlayerAbility.Info info = ability.Data;
            _abilityIcon.sprite = FrostUI.Icon(info.icon);
            _abilityName.text = info.name;
            _abilityText.text = info.description;
            _abilityStats.text = "<color=" + Hex(FrostUI.Label) + ">Tecla F</color>     <color=#7FB0FF>" + Mathf.CeilToInt(info.cost) + " de maná</color>     "
                + "<color=" + Hex(FrostUI.Secondary) + ">Recarga " + info.cooldown.ToString("0.#") + " s</color>\n"
                + "<color=" + Hex(FrostUI.Secondary) + ">Potencia: +" + Mathf.RoundToInt(_stats.Mana * ability.manaScaling * 100f) + "% por tu Maná</color>";
        }

        GameSession session = GameSession.Instance;
        SaveData save = session != null ? session.Current : null;
        string quest = QuestLog.Tracked();
        string objective = quest.Length > 0 ? QuestLog.Objective(quest, QuestLog.Stage(quest)) : "Ninguna misión pendiente";
        int minutes = save != null ? Mathf.FloorToInt(save.playSeconds / 60f) : 0;
        _journeyValues.text =
            "<color=" + Hex(FrostUI.Secondary) + ">Zona</color>\n" + SceneIds.Title(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name) + "\n\n" +
            "<color=" + Hex(FrostUI.Secondary) + ">Misión</color>\n" + objective + "\n\n" +
            "<color=" + Hex(FrostUI.Secondary) + ">Hogueras encendidas</color>   " + (save != null ? save.litBonfires.Count : 0) +
            "     <color=" + Hex(FrostUI.Secondary) + ">Tiempo jugado</color>   " + (minutes >= 60 ? minutes / 60 + " h " + minutes % 60 + " min" : minutes + " min");

        PlayerCombat combat = player.GetComponent<PlayerCombat>();
        ItemStack weapon = player.MainWeapon;
        _summaryValues.text =
            "Daño<pos=230>" + _stats.Damage.ToString("0.#") + "\n" +
            "Ataques por segundo<pos=230>" + (combat != null ? combat.AttacksPerSecond.ToString("0.00") : "-") + "\n" +
            "Alcance<pos=230>" + (combat != null ? combat.Range.ToString("0.0") + " m" : "-") + "\n" +
            "Armadura<pos=230>" + _stats.Armor + "  <color=" + Hex(FrostUI.Secondary) + ">(-" + Mathf.RoundToInt(_stats.DamageReduction * 100f) + "%)</color>\n" +
            "Arma<pos=230>" + (weapon != null ? weapon.DisplayName : "Sin arma");

        var dur = new StringBuilder();
        foreach (EquipSlot slot in Equipment.Slots)
        {
            ItemStack st = player.GetStack(slot);
            if (st == null || st.item == null || !st.item.HasDurability) continue;
            int pct = Mathf.RoundToInt(st.Durability01 * 100f);
            Color c = st.IsBroken ? FrostboundUI.Negative : pct <= 20 ? FrostUI.Gold : FrostboundUI.Positive;
            dur.Append(st.item.displayName).Append("<pos=230><color=").Append(Hex(c)).Append(">").Append(st.IsBroken ? "Rota" : pct + "%").Append("</color>\n");
        }
        _durability.text = dur.Length > 0 ? dur.ToString() : "<color=" + Hex(FrostUI.Disabled) + ">Nada que se desgaste</color>";
        _summarySets.text = _setText.text;

        Wallet wallet = player.GetComponent<Wallet>();
        _purseCoins.text = (wallet != null ? wallet.Coins : 0).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-ES")) + " monedas";
        var belt = new StringBuilder();
        belt.Append("<color=").Append(Hex(FrostUI.Secondary)).Append(">Espacio</color>   ").Append(_inventory.UsedSlots).Append(" / ").Append(_inventory.Capacity).Append("\n\n");
        belt.Append("<color=").Append(Hex(FrostUI.Secondary)).Append(">Cinturón</color>\n");
        PotionBelt pb = player.GetComponent<PotionBelt>();
        bool any = false;
        if (pb != null)
            for (int i = 0; i < PotionBelt.Size; i++)
            {
                if (pb.slots[i] == null) continue;
                any = true;
                belt.Append(i + 1).Append("  ").Append(pb.slots[i].displayName).Append("  ×").Append(pb.Count(i)).Append("\n");
            }
        if (!any) belt.Append("<color=").Append(Hex(FrostUI.Disabled)).Append(">Vacío</color>\n");
        _purseInfo.text = belt.ToString();
    }

    private void RefreshAttributes()
    {
        GameSession session = GameSession.Instance;
        CharacterClass cls = _stats.characterClass;
        string hero = session != null && !string.IsNullOrEmpty(session.HeroName) ? session.HeroName : (cls != null ? cls.displayName : "Héroe");
        _heroName.text = hero;
        _className.text = cls != null ? (string.IsNullOrEmpty(cls.displayName) ? cls.className : cls.displayName) : "";
        _levelText.text = _stats.level.ToString();
        _xpText.text = _stats.IsMaxLevel ? "Nivel máximo" : _stats.experience + " / " + _stats.ExperienceToNextLevel() + " XP";
        UIFactory.SetFill(_xpFill, _stats.LevelProgress);

        int points = _stats.pointsAvailable;
        _pointsText.text = points + (points == 1 ? " PUNTO" : " PUNTOS");
        _pointsFill.color = points > 0 ? FrostboundUI.ChipGoldBg : FrostboundUI.SurfaceSelected;
        _pointsText.color = points > 0 ? FrostboundUI.ChipGoldText : FrostboundUI.Muted;

        for (int i = 0; i < 4; i++)
        {
            StatType type = StatBlock.All[i];
            int bonus = _stats.EquipmentBonus.Get(type);
            string bonusText = bonus != 0
                ? " <size=15><color=" + FrostboundUI.RichHex(bonus > 0 ? FrostboundUI.Positive : FrostboundUI.Negative) + ">" + (bonus > 0 ? "+" : "") + bonus + "</color></size>"
                : "";
            _rows[i].value.text = _stats.GetWithoutEquipment(type) + bonusText;
            bool can = points > 0;
            _rows[i].plus.interactable = can;
            _rows[i].plusFill.color = can ? FrostboundUI.Ice : FrostboundUI.Surface;
            _rows[i].plusLabel.color = can ? FrostboundUI.Bg : FrostboundUI.Disabled;
        }

        _derivedLabels.text = "Vida máxima\nManá máximo\nDaño\nArmadura\nVelocidad\nRegeneración de maná";
        _derivedValues.text =
            Mathf.CeilToInt(_stats.MaxHealth) + "\n" +
            Mathf.CeilToInt(_stats.MaxMana) + "\n" +
            _stats.Damage.ToString("0.#") + "\n" +
            _stats.Armor + "  <color=" + FrostboundUI.RichHex(FrostboundUI.Muted) + ">(-" + Mathf.RoundToInt(_stats.DamageReduction * 100f) + "% daño)</color>\n" +
            _stats.MoveSpeed.ToString("0.0") + " m/s\n" +
            _stats.ManaRegenPerSecond.ToString("0.0") + " /s";
    }

    private void RefreshEquipment()
    {
        var sets = new List<ArmorSet>();
        foreach (var pair in _equipViews)
        {
            ItemDefinition item = player.Get(pair.Key);
            pair.Value.Show(item, 1, true);
            pair.Value.ShowState(player.GetStack(pair.Key));
            if (item != null && item.armorSet != null && !sets.Contains(item.armorSet)) sets.Add(item.armorSet);
        }

        var sb = new StringBuilder();
        foreach (ArmorSet set in sets)
        {
            int n = player.PiecesEquipped(set);
            if (n < 2) continue;
            if (sb.Length > 0) sb.Append("\n");
            sb.Append("<color=").Append(FrostboundUI.RichHex(FrostUI.Gold)).Append(">").Append(set.displayName)
              .Append(" ").Append(n).Append("/").Append(set.PieceCount).Append("</color>");
            foreach (ArmorSet.Bonus b in set.bonuses)
                if (n >= b.piecesRequired) sb.Append("  ·  ").Append(ArmorSet.Describe(b));
        }
        _setText.text = sb.Length > 0 ? sb.ToString() : "<color=" + FrostboundUI.RichHex(FrostboundUI.Version) + ">Combina piezas del mismo set para ganar bonos.</color>";
    }

    private void RefreshBackpack()
    {
        int capacity = _inventory.Capacity;
        if (_grid.gameObject.activeInHierarchy) while (_cells.Count < capacity) _cells.Add(CreateCell(_cells.Count));
        if (_cells.Count < capacity) capacity = _cells.Count;

        int shown = 0;
        for (int i = 0; i < capacity; i++)
        {
            ItemStack s = _inventory.Get(i);
            bool visible = _filter == Filter.All || (s != null && Matches(s.item, _filter));
            if (!visible) continue;
            ItemSlotView cell = _cells[shown++];
            cell.gameObject.SetActive(true);
            cell.index = i;
            cell.Show(s != null ? s.item : null, s != null ? s.quantity : 0, s == null || MeetsRequirements(s.item));
            cell.ShowState(s);
        }
        for (int i = shown; i < _cells.Count; i++)
        {
            _cells[i].gameObject.SetActive(false);
            _cells[i].index = -1;
        }

        _countText.text = _inventory.UsedSlots + " / " + capacity;
        foreach (var tab in _tabs)
        {
            bool on = tab.filter == _filter;
            tab.fill.color = on ? FrostboundUI.SurfaceSelected : FrostboundUI.Surface;
            tab.border.GetComponent<UIButtonFx>().normalBorder = on ? FrostboundUI.Ice : FrostUI.BorderDim;
            tab.label.color = on ? FrostboundUI.Ice : FrostboundUI.Muted;
        }

        if (_selected != null && !_selected.IsEquipmentSlot && (_selected.index < 0 || _inventory.Get(_selected.index) == null)) Select(null);
        foreach (ItemSlotView cell in _cells) cell.Refresh();
    }

    private bool MeetsRequirements(ItemDefinition item) => !item.IsEquippable || item.CanBeEquippedBy(_stats, out _);

    private static bool Matches(ItemDefinition item, Filter f)
    {
        switch (f)
        {
            case Filter.Armor: return item.category == ItemCategory.Armor;
            case Filter.Weapons: return item.category == ItemCategory.Weapon;
            case Filter.Consumables: return item.category == ItemCategory.Consumable;
            case Filter.Other: return item.category == ItemCategory.Material || item.category == ItemCategory.Misc || item.category == ItemCategory.Rune;
            default: return true;
        }
    }

    // ----- Detalle del objeto -----

    private ItemDefinition ItemOf(ItemSlotView v)
    {
        if (v == null) return null;
        if (v.IsEquipmentSlot) return player.Get(v.slot);
        ItemStack s = _inventory.Get(v.index);
        return s != null ? s.item : null;
    }

    private ItemStack StackOf(ItemSlotView v)
    {
        if (v == null) return null;
        if (v.IsEquipmentSlot) return player.GetStack(v.slot);
        return _inventory.Get(v.index);
    }

    private void RefreshDetail()
    {
        ItemSlotView view = _hovered != null && ItemOf(_hovered) != null ? _hovered : _selected;
        ItemDefinition item = ItemOf(view);
        bool has = item != null;
        _dEmpty.gameObject.SetActive(!has);
        _dName.gameObject.SetActive(has);
        _dMeta.gameObject.SetActive(has);
        _dBody.gameObject.SetActive(has);

        bool actionable = has && view == _selected;
        _dPrimary.gameObject.SetActive(actionable);
        _dSecondary.gameObject.SetActive(actionable && !view.IsEquipmentSlot);
        if (!has) return;

        bool equipped = view.IsEquipmentSlot;
        ItemStack shown = StackOf(view);
        _dName.text = shown != null ? shown.DisplayName : item.displayName;
        _dName.color = shown != null ? shown.DisplayColor : FrostboundUI.Rarity(item.rarity);

        string kind = item.IsWeapon
            ? "Arma · " + WeaponCatalog.HandlingName(item.Handling) + " · " + item.WeaponInfo.name
            : item.IsEquippable ? ItemDefinition.SlotName(item.equipSlot) : ItemDefinition.CategoryName(item.category);
        string quality = shown != null && shown.quality != LootQuality.None ? LootQualityInfo.Name(shown.quality) : ItemDefinition.RarityName(item.rarity);
        if (shown != null && shown.quality == LootQuality.Rare) kind = item.displayName + "  ·  " + kind;
        _dMeta.text = quality + "  ·  " + kind + (equipped ? "  ·  Equipado" : "");

        _dBody.text = Describe(item, equipped ? null : player.Get(item.equipSlot), equipped, StackOf(view));

        if (actionable)
        {
            string primary = equipped ? "Quitar" : item.IsEquippable ? "Equipar" : item.IsConsumable ? "Usar" : item.IsRune ? "Grabar" : "";
            _dPrimary.gameObject.SetActive(primary.Length > 0);
            _dPrimaryLabel.text = primary;
            bool ok = equipped || !item.IsEquippable || item.CanBeEquippedBy(_stats, out _);
            if (item.IsRune) ok = player.MainWeapon != null && player.MainWeapon.FreeRuneSlots > 0 && !player.MainWeapon.IsBroken;
            _dPrimary.interactable = ok;
            _dPrimaryFill.color = ok ? FrostboundUI.Ice : FrostboundUI.Surface;
            _dPrimaryLabel.color = ok ? FrostboundUI.Bg : FrostboundUI.Disabled;
            _dSecondaryLabel.text = _confirmDiscard ? "¿Seguro?" : "Tirar";
            _dSecondaryLabel.color = _confirmDiscard ? FrostboundUI.Negative : FrostboundUI.Text;
        }
    }

    private string Describe(ItemDefinition item, ItemDefinition compareTo, bool equipped, ItemStack stack)
    {
        var sb = new StringBuilder();
        string pos = FrostboundUI.RichHex(FrostboundUI.Positive);
        string neg = FrostboundUI.RichHex(FrostboundUI.Negative);
        string muted = FrostboundUI.RichHex(FrostboundUI.Muted);
        string gold = FrostboundUI.RichHex(FrostUI.Gold);

        if (item.IsRune)
        {
            string rc = FrostboundUI.RichHex(WeaponCatalog.RuneColor(item.runeEffect));
            sb.Append("<color=").Append(rc).Append(">• ").Append(WeaponCatalog.RuneName(item.runeEffect)).Append("</color>  ")
              .Append(ItemDefinition.RuneDescription(item)).Append("\n");
            sb.Append("<size=13><color=").Append(muted).Append(">Hechizo de grabado: ").Append(item.engraveManaCost)
              .Append(" de maná. Queda grabada para siempre en el arma equipada.</color></size>\n");
            ItemStack w = player.MainWeapon;
            if (w == null) sb.Append("<size=13><color=").Append(neg).Append(">No tienes un arma equipada</color></size>\n");
            else sb.Append("<size=13><color=").Append(w.FreeRuneSlots > 0 ? muted : neg).Append(">").Append(w.item.displayName)
                   .Append(": ").Append(w.FreeRuneSlots).Append(" ranura(s) libre(s)</color></size>\n");
        }

        if (item.damage > 0f)
        {
            float myDmg = stack != null ? stack.BaseDamage : item.damage;
            ItemStack cmp = compareTo != null && player != null ? player.GetStack(compareTo.equipSlot) : null;
            float theirDmg = cmp != null ? cmp.BaseDamage : (compareTo != null ? compareTo.damage : 0f);
            sb.Append("Daño  <b>+").Append(myDmg.ToString("0.#")).Append("</b>").Append(Delta(myDmg - theirDmg, compareTo, null, pos, neg)).Append("\n");
            if (item.IsWeapon && stack != null)
                sb.Append("<size=13><color=").Append(muted).Append(">").Append(stack.itemLevel > 0 ? "Nivel del objeto " + stack.itemLevel : "Arma inicial").Append("</color></size>\n");
        }
        if (item.armor > 0) sb.Append("Armadura  <b>").Append(item.armor).Append("</b>").Append(Delta(item.armor - (compareTo != null ? compareTo.armor : 0), compareTo, item, pos, neg)).Append("\n");

        StatBlock mine = item.StatBonus;
        StatBlock other = compareTo != null ? compareTo.StatBonus : new StatBlock();
        foreach (StatType t in StatBlock.All)
        {
            int v = mine.Get(t);
            if (v == 0) continue;
            sb.Append("<color=").Append(v > 0 ? pos : neg).Append(">").Append(v > 0 ? "+" : "").Append(v).Append(" ").Append(StatBlock.DisplayName(t)).Append("</color>")
              .Append(Delta(v - other.Get(t), compareTo, item, pos, neg)).Append("\n");
        }
        if (stack != null && stack.affixes != null && stack.affixes.Count > 0)
        {
            string ac = FrostboundUI.RichHex(stack.quality == LootQuality.Worn ? FrostboundUI.Negative : LootQualityInfo.Magic);
            foreach (RolledAffix a in stack.affixes)
                sb.Append("<color=").Append(a.value < 0f ? neg : ac).Append(">").Append(a.Describe()).Append("</color>\n");
        }
        if (item.IsWeapon) DescribeWeapon(sb, item, stack, pos, neg, muted, gold);
        if (item.heal > 0f) sb.Append("Cura <b>").Append(item.heal.ToString("0")).Append("</b> de vida\n");
        if (item.restoreMana > 0f) sb.Append("Recupera <b>").Append(item.restoreMana.ToString("0")).Append("</b> de maná\n");

        if (item.IsEquippable && item.requiredLevel > 1)
        {
            bool ok = _stats.level >= item.requiredLevel;
            sb.Append("<color=").Append(ok ? muted : neg).Append(">Requiere nivel ").Append(item.requiredLevel).Append("</color>\n");
        }
        if (item.IsEquippable && item.allowedClasses.Count > 0 && !item.allowedClasses.Contains(_stats.characterClass))
            sb.Append("<color=").Append(neg).Append(">Tu clase no puede usarlo</color>\n");

        if (item.armorSet != null)
        {
            int n = player.PiecesEquipped(item.armorSet);
            sb.Append("<color=").Append(gold).Append(">").Append(item.armorSet.displayName).Append(" (").Append(n).Append("/").Append(item.armorSet.PieceCount).Append(")</color>\n");
            foreach (ArmorSet.Bonus b in item.armorSet.bonuses)
                sb.Append("<size=13><color=").Append(n >= b.piecesRequired ? pos : muted).Append(">  ").Append(b.piecesRequired).Append(" piezas: ").Append(ArmorSet.Describe(b)).Append("</color></size>\n");
        }

        if (!string.IsNullOrEmpty(item.description))
            sb.Append("<size=13><i><color=").Append(muted).Append(">").Append(item.description).Append("</color></i></size>");

        if (!equipped && item.IsEquippable && compareTo == null)
            sb.Append("\n<size=12><color=").Append(muted).Append(">Ranura vacía</color></size>");
        return sb.ToString().TrimEnd('\n');
    }

    private void DescribeWeapon(StringBuilder sb, ItemDefinition item, ItemStack stack, string pos, string neg, string muted, string gold)
    {
        WeaponCatalog.Info info = item.WeaponInfo;
        sb.Append("Velocidad  <b>").Append(item.attackSpeed.ToString("0.0#")).Append("</b>/s  ·  Alcance  <b>").Append(info.range.ToString("0.#")).Append(" m</b>")
          .Append(info.projectile ? " <size=12><color=" + muted + ">(proyectil)</color></size>" : "").Append("\n");
        sb.Append("<color=").Append(muted).Append(">Nivel  <b>").Append(item.requiredLevel).Append("</b></color>");
        if (item.HasDurability)
        {
            float cur = stack != null && stack.durability >= 0f ? stack.durability : item.maxDurability;
            bool broken = cur <= 0f;
            float r = cur / item.maxDurability;
            string col = broken ? neg : r <= 0.2f ? gold : muted;
            sb.Append("  ·  <color=").Append(col).Append(">Durabilidad  <b>").Append(Mathf.CeilToInt(cur)).Append("/").Append(item.maxDurability).Append("</b>")
              .Append(broken ? " ROTA" : "").Append("</color>");
        }
        sb.Append("\n");
        var reqs = new List<string>();
        foreach (StatModifier req in item.requiredStats)
        {
            if (req.value <= 0) continue;
            bool ok = _stats.Get(req.stat) >= req.value;
            reqs.Add("<color=" + (ok ? muted : neg) + ">" + req.value + " " + StatBlock.DisplayName(req.stat) + "</color>");
        }
        if (reqs.Count > 0) sb.Append("<color=").Append(muted).Append(">Requiere </color>").Append(string.Join("<color=" + muted + ">, </color>", reqs)).Append("\n");
        if (item.runeSlots > 0)
        {
            sb.Append("<color=").Append(gold).Append(">Runas</color> ");
            for (int i = 0; i < item.runeSlots; i++)
            {
                ItemDefinition rune = stack != null && i < stack.runes.Count ? stack.runes[i] : null;
                if (i > 0) sb.Append("  ");
                if (rune != null)
                    sb.Append("<color=").Append(FrostboundUI.RichHex(WeaponCatalog.RuneColor(rune.runeEffect))).Append(">• ")
                      .Append(WeaponCatalog.RuneName(rune.runeEffect)).Append("</color> <size=12>").Append(RuneShort(rune)).Append("</size>");
                else
                    sb.Append("<color=").Append(muted).Append(">• vacía</color>");
            }
            sb.Append("\n");
        }
    }

    private static string RuneShort(ItemDefinition r)
    {
        switch (r.runeEffect)
        {
            case RuneEffect.Fire: return r.runePower.ToString("0.#") + " fuego/s";
            case RuneEffect.Poison: return r.runePower.ToString("0.#") + " veneno/s";
            case RuneEffect.Frost: return "-" + Mathf.RoundToInt(r.runePower * 100f) + " % vel.";
            case RuneEffect.Chain: return Mathf.RoundToInt(r.runeChance * 100f) + " % rayo";
            case RuneEffect.Lifesteal: return Mathf.RoundToInt(r.runePower * 100f) + " % vida";
            case RuneEffect.Sharpness: return "+" + Mathf.RoundToInt(r.runePower * 100f) + " % daño";
            case RuneEffect.Swiftness: return "+" + Mathf.RoundToInt(r.runePower * 100f) + " % vel. ataque";
            case RuneEffect.Knockback: return "empuje";
            default: return "";
        }
    }

    private static string Delta(float d, ItemDefinition compareTo, ItemDefinition item, string pos, string neg)
    {
        if (compareTo == null || compareTo == item || Mathf.Abs(d) < 0.01f) return "";
        string sign = d > 0 ? "+" : "−";
        return "  <size=12><color=" + (d > 0 ? pos : neg) + ">(" + sign + Mathf.Abs(d).ToString("0.#") + ")</color></size>";
    }

    // ----- Interacción -----

    private void OnCellClicked(ItemSlotView v)
    {
        if (v == _selected && ItemOf(v) != null) DoAction(v);
        else Select(v);
    }

    private void OnCellHovered(ItemSlotView v, bool on)
    {
        if (on) _hovered = v;
        else if (_hovered == v) _hovered = null;
        RefreshDetail();
    }

    private void Select(ItemSlotView v)
    {
        if (_selected != null) _selected.Selected = false;
        _selected = v != null && ItemOf(v) != null ? v : null;
        if (_selected != null) _selected.Selected = true;
        _confirmDiscard = false;
        foreach (ItemSlotView c in _cells) c.Refresh();
        foreach (ItemSlotView c in _equipViews.Values) c.Refresh();
        RefreshDetail();
    }

    private void DoAction(ItemSlotView v)
    {
        ItemDefinition item = ItemOf(v);
        if (item == null) return;
        string message;
        bool ok;
        if (v.IsEquipmentSlot)
        {
            ok = player.Unequip(v.slot, out message);
            if (ok) Select(null);
        }
        else
        {
            ok = player.UseOrEquip(v.index, out message);
            if (ok && item.IsEquippable) Select(_equipViews[item.equipSlot]);
            if (ok && item.IsRune)
            {
                Select(_equipViews[EquipSlot.Weapon]);
                if (!string.IsNullOrEmpty(message)) Notifications.Show(message, FrostboundUI.Positive);
            }
        }
        if (!ok) Notifications.Show(message, FrostboundUI.Negative);
        _dirty = true;
    }

    private void OnPrimary()
    {
        if (_selected != null) DoAction(_selected);
    }

    private void OnSecondary()
    {
        if (_selected == null || _selected.IsEquipmentSlot) return;
        if (!_confirmDiscard)
        {
            _confirmDiscard = true;
            RefreshDetail();
            return;
        }
        ItemStack removed = _inventory.RemoveAt(_selected.index);
        if (removed != null)
        {
            // Cae delante del héroe con física y se puede volver a recoger.
            Transform t = player.transform;
            Vector3 origin = t.position + Vector3.up * 1.1f + t.forward * 0.5f;
            Vector3 velocity = t.forward * 2.2f + Vector3.up * 3f + Random.insideUnitSphere * 0.6f;
            WorldItem.Spawn(removed, origin, velocity, player.gameObject);
            Notifications.Show("Tiraste " + removed.item.displayName, FrostboundUI.Muted);
        }
        Select(null);
    }

    private void OnPlus(StatType type)
    {
        Keyboard kb = Keyboard.current;
        bool shift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        _stats.AddPoints(type, shift ? 5 : 1);
    }

    private void SetFilter(Filter f)
    {
        _filter = f;
        Select(null);
        _dirty = true;
        if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
    }

    // ----- Construcción -----

    private void Build()
    {
        var root = (RectTransform)transform;
        _group = gameObject.GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

        Image dim = UIFactory.Img(root, "Dim", null, new Color(0.01f, 0.02f, 0.05f, 0.78f));
        UIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        _window = UIFactory.Node(root, "Window");
        _window.anchorMin = _window.anchorMax = _window.pivot = new Vector2(0.5f, 0.5f);
        _window.sizeDelta = new Vector2(WindowW, WindowH);
        Image frame = FrostUI.Panel(_window, "Marco", out _, 20f);
        FrostUI.Stretch(frame.rectTransform);
        frame.raycastTarget = true;

        _kicker = FrostUI.Kicker(_window, "Kicker", "", 16f, FrostUI.Ice);
        UIFactory.TopLeft(_kicker.rectTransform, 34f, 20f, 600f, 22f);
        _heroName = FrostUI.Title(_window, "HeroName", "", 46f, TextAlignmentOptions.MidlineLeft, 0.22f);
        UIFactory.TopLeft(_heroName.rectTransform, 32f, 40f, 700f, 58f);

        FrostButton close = FrostButton.Create(_window, "BtnClose", "Cerrar · Esc", new Vector2(190f, 50f), FrostButton.Style.Normal, 20f);
        UIFactory.TopLeft((RectTransform)close.transform, WindowW - 32f - 190f, 30f, 190f, 50f);
        close.button.onClick.AddListener(Close);

        var tabs = new (Tab, string)[] { (Tab.Character, "Personaje"), (Tab.Equipment, "Equipo"), (Tab.Inventory, "Inventario") };
        for (int i = 0; i < tabs.Length; i++)
        {
            Tab t = tabs[i].Item1;
            FrostButton b = FrostButton.Create(_window, "Pestana_" + t, tabs[i].Item2, new Vector2(300f, 54f), FrostButton.Style.Normal, 26f);
            UIFactory.TopLeft((RectTransform)b.transform, 32f + i * 312f, 88f, 300f, 54f);
            b.button.onClick.AddListener(() => SetTab(t));
            _tabButtons.Add((t, b));
            _pages[t] = new List<RectTransform>();
        }

        TextMeshProUGUI help = FrostUI.Text(_window, "Help",
            "C personaje  ·  I inventario  ·  Tab cambia de pestaña  ·  Clic otra vez, doble clic o clic derecho: equipar / usar  ·  Mayús + «+»: 5 puntos",
            13f, FrostUI.Disabled, TextAlignmentOptions.Center, FrostUI.BodyLight);
        UIFactory.TopLeft(help.rectTransform, 32f, WindowH - 34f, WindowW - 64f, 20f);

        BuildAttributes();
        BuildAbilityPage();
        BuildEquipment();
        BuildSummary();
        BuildDetail();
        BuildPurse();
        BuildBackpack();
        while (_cells.Count < _inventory.Capacity) _cells.Add(CreateCell(_cells.Count));
        SetTab(Current);
    }

    private void BuildAbilityPage()
    {
        RectTransform p = Panel("HabilidadPanel", 436f, Top, 520f, 330f, "HABILIDAD DE CLASE");
        _pages[Tab.Character].Add(p);
        Image ring = UIFactory.Frame(p, "Aro", skin.round16, FrostUI.PanelSolid, FrostUI.Border, out _);
        UIFactory.TopLeft(ring.rectTransform, 24f, 56f, 92f, 92f);
        _abilityIcon = UIFactory.Img(ring.transform, "Icono", null, FrostUI.Ice);
        UIFactory.Stretch(_abilityIcon.rectTransform, 16f);
        _abilityName = FrostUI.Title(p, "Nombre", "", 34f, TextAlignmentOptions.MidlineLeft, 0.2f);
        UIFactory.TopLeft(_abilityName.rectTransform, 134f, 60f, 370f, 44f);
        _abilityText = FrostUI.Text(p, "Descripcion", "", 17f, Color.white, TextAlignmentOptions.TopLeft, FrostUI.BodyLight);
        _abilityText.textWrappingMode = TextWrappingModes.Normal;
        UIFactory.TopLeft(_abilityText.rectTransform, 134f, 106f, 362f, 90f);
        _abilityStats = FrostUI.Text(p, "Datos", "", 15f, Color.white, TextAlignmentOptions.TopLeft, FrostUI.Body);
        _abilityStats.textWrappingMode = TextWrappingModes.Normal;
        UIFactory.TopLeft(_abilityStats.rectTransform, 24f, 220f, 472f, 80f);
        UIFactory.SetLineHeight(_abilityStats, 1.6f);

        RectTransform j = Panel("ViajePanel", 436f, Top + 346f, 520f, 354f, "VIAJE");
        _pages[Tab.Character].Add(j);
        _journeyValues = FrostUI.Text(j, "Valores", "", 17f, Color.white, TextAlignmentOptions.TopLeft, FrostUI.Body);
        _journeyValues.textWrappingMode = TextWrappingModes.Normal;
        UIFactory.TopLeft(_journeyValues.rectTransform, 24f, 54f, 472f, 280f);
        UIFactory.SetLineHeight(_journeyValues, 1.35f);
    }

    private void BuildSummary()
    {
        RectTransform p = Panel("ResumenPanel", 436f, Top, 520f, 700f, "RESUMEN DEL EQUIPO");
        _pages[Tab.Equipment].Add(p);
        _summaryValues = FrostUI.Text(p, "Valores", "", 18f, Color.white, TextAlignmentOptions.TopLeft, FrostUI.Body);
        UIFactory.TopLeft(_summaryValues.rectTransform, 24f, 56f, 472f, 210f);
        _summaryValues.textWrappingMode = TextWrappingModes.Normal;
        UIFactory.SetLineHeight(_summaryValues, 1.6f);
        SectionLabel(p, "DURABILIDAD", 24f, 284f);
        _durability = FrostUI.Text(p, "Durabilidad", "", 16f, Color.white, TextAlignmentOptions.TopLeft, FrostUI.Body);
        UIFactory.TopLeft(_durability.rectTransform, 24f, 312f, 472f, 200f);
        UIFactory.SetLineHeight(_durability, 1.5f);
        SectionLabel(p, "BONOS DE SET", 24f, 530f);
        _summarySets = FrostUI.Text(p, "Sets", "", 15f, Color.white, TextAlignmentOptions.TopLeft, FrostUI.Body);
        _summarySets.textWrappingMode = TextWrappingModes.Normal;
        UIFactory.TopLeft(_summarySets.rectTransform, 24f, 558f, 472f, 120f);
    }

    private void BuildPurse()
    {
        RectTransform p = Panel("BolsaPanel", 32f, Top + 320f, 380f, 380f, "BOLSA");
        _pages[Tab.Inventory].Add(p);
        _purseCoins = FrostUI.Text(p, "Monedas", "", 30f, FrostUI.Gold, TextAlignmentOptions.MidlineLeft, DisplayFont);
        UIFactory.TopLeft(_purseCoins.rectTransform, 24f, 54f, 332f, 44f);
        _purseInfo = FrostUI.Text(p, "Info", "", 16f, Color.white, TextAlignmentOptions.TopLeft, FrostUI.Body);
        _purseInfo.textWrappingMode = TextWrappingModes.Normal;
        UIFactory.TopLeft(_purseInfo.rectTransform, 24f, 112f, 332f, 250f);
        UIFactory.SetLineHeight(_purseInfo, 1.45f);
    }

    private RectTransform Panel(string name, float x, float y, float w, float h, string title)
    {
        Image frame = FrostUI.Panel(_window, name, out _, 16f, FrostUI.BorderDim, FrostUI.Hex("#08101D"));
        frame.raycastTarget = true;
        UIFactory.TopLeft(frame.rectTransform, x, y, w, h);
        if (!string.IsNullOrEmpty(title)) SectionLabel(frame.rectTransform, title, 24f, 20f);
        return frame.rectTransform;
    }

    private TextMeshProUGUI SectionLabel(RectTransform parent, string text, float x, float y)
    {
        TextMeshProUGUI t = FrostUI.Kicker(parent, "Label_" + text, text, 14f, FrostUI.Label);
        UIFactory.TopLeft(t.rectTransform, x, y, 300f, 20f);
        return t;
    }

    private void BuildAttributes()
    {
        RectTransform p = Panel("AttributesPanel", 32f, Top, 380f, 700f, null);
        _pages[Tab.Character].Add(p);

        Image badge = UIFactory.Frame(p, "LevelBadge", skin.circle, FrostboundUI.Bg, FrostUI.Gold, out _);
        UIFactory.TopLeft(badge.rectTransform, 24f, 24f, 64f, 64f);
        _levelText = UIFactory.Text(badge.transform, "Level", "1", DisplayFont, 26f, FrostboundUI.Title, TextAlignmentOptions.Center);
        UIFactory.Stretch(_levelText.rectTransform);
        TextMeshProUGUI nv = UIFactory.Text(badge.transform, "Caption", "NIVEL", skin.nunito800, 8f, FrostUI.Gold, TextAlignmentOptions.Center, 2f);
        UIFactory.TopLeft(nv.rectTransform, 0f, 8f, 64f, 10f);

        _className = UIFactory.Text(p, "ClassName", "", DisplayFont, 17f, FrostboundUI.Title);
        UIFactory.TopLeft(_className.rectTransform, 104f, 30f, 252f, 24f);
        _className.overflowMode = TextOverflowModes.Ellipsis;
        _xpText = UIFactory.Text(p, "Xp", "", skin.nunito700, 13f, FrostboundUI.Muted);
        UIFactory.TopLeft(_xpText.rectTransform, 104f, 58f, 252f, 18f);
        _xpFill = UIFactory.Bar(p, "XpBar", skin.pill, FrostboundUI.Bg, FrostboundUI.XpBar, out RectTransform xp);
        UIFactory.TopLeft(xp, 24f, 104f, 332f, 10f);

        SectionLabel(p, "ATRIBUTOS", 24f, 138f);
        Image chip = UIFactory.Frame(p, "PointsChip", skin.pill, FrostboundUI.ChipGoldBg, FrostUI.BorderDim, out _pointsFill);
        UIFactory.TopLeft(chip.rectTransform, 356f - 132f, 132f, 132f, 28f);
        chip.color = new Color(0f, 0f, 0f, 0f);
        _pointsText = UIFactory.Text(chip.transform, "Label", "", skin.nunito800, 12f, FrostboundUI.ChipGoldText, TextAlignmentOptions.Center, 1.5f);
        UIFactory.Stretch(_pointsText.rectTransform);

        for (int i = 0; i < 4; i++)
        {
            StatType type = StatBlock.All[i];
            float y = 172f + i * 66f;
            Image row = UIFactory.Frame(p, "Row_" + type, skin.round12, FrostboundUI.Bg, FrostUI.BorderDim, out _);
            UIFactory.TopLeft(row.rectTransform, 24f, y, 332f, 58f);

            Image icon = UIFactory.Img(row.transform, "Icon", skin.StatIcon(type), Color.white);
            UIFactory.TopLeft(icon.rectTransform, 12f, 13f, 32f, 32f);
            TextMeshProUGUI name = UIFactory.Text(row.transform, "Name", StatBlock.DisplayName(type), skin.nunito800, 16f, FrostboundUI.Text);
            UIFactory.TopLeft(name.rectTransform, 54f, 8f, 150f, 22f);
            TextMeshProUGUI effect = UIFactory.Text(row.transform, "Effect", _stats.EffectDescription(type), skin.nunito700, 11f, FrostboundUI.Muted);
            UIFactory.TopLeft(effect.rectTransform, 54f, 32f, 180f, 16f);

            var r = new AttrRow();
            r.value = UIFactory.Text(row.transform, "Value", "", DisplayFont, 22f, FrostboundUI.Title, TextAlignmentOptions.Right);
            UIFactory.TopLeft(r.value.rectTransform, 190f, 12f, 74f, 34f);

            Image plus = UIFactory.Frame(row.transform, "BtnPlus", skin.round10, FrostboundUI.Ice, FrostUI.BorderDim, out r.plusFill);
            UIFactory.TopLeft(plus.rectTransform, 332f - 12f - 40f, 9f, 40f, 40f);
            r.plus = UIFactory.MakeButton(plus);
            var fx = plus.gameObject.AddComponent<UIButtonFx>();
            fx.border = plus;
            fx.normalBorder = FrostUI.BorderDim;
            fx.activeBorder = FrostboundUI.White;
            r.plusLabel = UIFactory.Text(plus.transform, "Label", "+", DisplayFont, 24f, FrostboundUI.Bg, TextAlignmentOptions.Center);
            UIFactory.Stretch(r.plusLabel.rectTransform);
            r.plus.onClick.AddListener(() => OnPlus(type));
            _rows[i] = r;
        }

        SectionLabel(p, "ESTADÍSTICAS", 24f, 452f);
        _derivedLabels = UIFactory.Text(p, "DerivedLabels", "", skin.nunito700, 14f, FrostboundUI.Body);
        UIFactory.TopLeft(_derivedLabels.rectTransform, 24f, 482f, 180f, 180f);
        UIFactory.SetLineHeight(_derivedLabels, 1.75f);
        _derivedValues = UIFactory.Text(p, "DerivedValues", "", skin.nunito800, 14f, FrostboundUI.Text, TextAlignmentOptions.TopRight);
        UIFactory.TopLeft(_derivedValues.rectTransform, 156f, 482f, 200f, 180f);
        UIFactory.SetLineHeight(_derivedValues, 1.75f);
    }

    private void BuildEquipment()
    {
        RectTransform p = Panel("EquipmentPanel", 32f, Top, 380f, 380f, "EQUIPO");
        _pages[Tab.Equipment].Add(p);
        AddEquipSlot(p, EquipSlot.Head, 146f, 52f);
        AddEquipSlot(p, EquipSlot.Amulet, 262f, 52f);
        AddEquipSlot(p, EquipSlot.Weapon, 30f, 148f);
        AddEquipSlot(p, EquipSlot.Chest, 146f, 148f);
        AddEquipSlot(p, EquipSlot.Offhand, 262f, 148f);
        AddEquipSlot(p, EquipSlot.Feet, 146f, 244f);

        _setText = UIFactory.Text(p, "SetBonus", "", skin.nunito700, 12f, FrostboundUI.Body);
        UIFactory.TopLeft(_setText.rectTransform, 24f, 340f, 332f, 34f);
        _setText.textWrappingMode = TextWrappingModes.Normal;
        _setText.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void AddEquipSlot(RectTransform parent, EquipSlot slot, float x, float y)
    {
        ItemSlotView v = CreateSlotView(parent, "Slot_" + slot, 88f, 58f);
        UIFactory.TopLeft((RectTransform)v.transform, x, y, 88f, 88f);
        v.slot = slot;
        v.placeholder.sprite = skin.SlotIcon(slot);
        v.caption = UIFactory.Text(v.transform, "Caption", ItemDefinition.SlotName(slot), skin.nunito800, 9f, FrostboundUI.Version, TextAlignmentOptions.Bottom, 1.5f, true);
        UIFactory.TopLeft(v.caption.rectTransform, 0f, 68f, 88f, 14f);
        _equipViews[slot] = v;
    }

    private void BuildDetail()
    {
        RectTransform p = Panel("DetailPanel", 32f, Top, 380f, 304f, null);
        _detailPanel = p;

        _dEmpty = UIFactory.Text(p, "Empty", "Selecciona un objeto para ver sus detalles.", skin.nunito700, 14f, FrostboundUI.Version, TextAlignmentOptions.Center);
        UIFactory.Stretch(_dEmpty.rectTransform, 24f);
        _dEmpty.textWrappingMode = TextWrappingModes.Normal;

        _dName = UIFactory.Text(p, "Name", "", DisplayFont, 20f, FrostboundUI.Title);
        UIFactory.TopLeft(_dName.rectTransform, 22f, 18f, 336f, 28f);
        _dName.overflowMode = TextOverflowModes.Ellipsis;
        _dMeta = UIFactory.Text(p, "Meta", "", skin.nunito700, 13f, FrostboundUI.Muted);
        UIFactory.TopLeft(_dMeta.rectTransform, 22f, 48f, 336f, 18f);
        _dBody = UIFactory.Text(p, "Body", "", skin.nunito700, 14f, FrostboundUI.Body);
        UIFactory.TopLeft(_dBody.rectTransform, 22f, 76f, 336f, 156f);
        _dBody.textWrappingMode = TextWrappingModes.Normal;
        _dBody.overflowMode = TextOverflowModes.Ellipsis;
        _dBody.enableAutoSizing = true;
        _dBody.fontSizeMin = 10.5f;
        _dBody.fontSizeMax = 14f;
        UIFactory.SetLineHeight(_dBody, 1.4f);

        _dPrimary = UIFactory.FramedButton(p, "BtnPrimary", skin, skin.round12, "Equipar", 15f, FrostboundUI.Ice, FrostboundUI.White, FrostboundUI.Bg, out _dPrimaryLabel, out _dPrimaryFill);
        UIFactory.TopLeft((RectTransform)_dPrimary.transform, 22f, 242f, 196f, 44f);
        _dPrimary.onClick.AddListener(OnPrimary);
        _dSecondary = UIFactory.FramedButton(p, "BtnSecondary", skin, skin.round12, "Tirar", 14f, FrostUI.PanelSolid, FrostUI.BorderDim, FrostboundUI.Text, out _dSecondaryLabel, out _);
        UIFactory.TopLeft((RectTransform)_dSecondary.transform, 230f, 242f, 128f, 44f);
        _dSecondary.onClick.AddListener(OnSecondary);
    }

    private void BuildBackpack()
    {
        RectTransform p = Panel("BackpackPanel", 436f, Top, 520f, 700f, "MOCHILA");
        _pages[Tab.Inventory].Add(p);

        Button sort = UIFactory.FramedButton(p, "BtnSort", skin, skin.round10, "Ordenar", 12f, FrostUI.PanelSolid, FrostUI.BorderDim, FrostboundUI.Text, out _, out _);
        UIFactory.TopLeft((RectTransform)sort.transform, 520f - 24f - 112f, 14f, 112f, 34f);
        sort.onClick.AddListener(() => { _inventory.Sort(); Select(null); });
        _countText = UIFactory.Text(p, "Count", "", skin.nunito800, 14f, FrostboundUI.Muted, TextAlignmentOptions.Right);
        UIFactory.TopLeft(_countText.rectTransform, 520f - 24f - 112f - 12f - 120f, 21f, 120f, 20f);

        var tabs = new (Filter, string)[] { (Filter.All, "Todo"), (Filter.Armor, "Armadura"), (Filter.Weapons, "Armas"), (Filter.Consumables, "Pociones"), (Filter.Other, "Otros") };
        for (int i = 0; i < tabs.Length; i++)
        {
            Filter f = tabs[i].Item1;
            Button b = UIFactory.FramedButton(p, "Tab_" + f, skin, skin.pill, tabs[i].Item2, 12f, FrostUI.PanelSolid, FrostUI.BorderDim, FrostboundUI.Muted, out TextMeshProUGUI label, out Image fill);
            UIFactory.TopLeft((RectTransform)b.transform, 24f + i * 96f, 60f, 88f, 32f);
            b.onClick.AddListener(() => SetFilter(f));
            _tabs.Add((f, (Image)b.targetGraphic, fill, label));
        }

        Image viewport = UIFactory.Img(p, "Viewport", null, new Color(0f, 0f, 0f, 0f));
        UIFactory.TopLeft(viewport.rectTransform, 12f, 106f, 496f, 580f);
        viewport.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        _grid = UIFactory.Node(viewport.transform, "Grid");
        _grid.anchorMin = new Vector2(0f, 1f);
        _grid.anchorMax = new Vector2(1f, 1f);
        _grid.pivot = new Vector2(0.5f, 1f);
        _grid.anchoredPosition = Vector2.zero;
        _grid.sizeDelta = Vector2.zero;
        var gl = _grid.gameObject.AddComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(76f, 76f);
        gl.spacing = new Vector2(8f, 8f);
        gl.padding = new RectOffset(0, 0, 6, 6);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = 6;
        gl.childAlignment = TextAnchor.UpperCenter;
        var fitter = _grid.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scroll = viewport.gameObject.AddComponent<ScrollRect>();
        _scroll.viewport = viewport.rectTransform;
        _scroll.content = _grid;
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 40f;
    }

    private ItemSlotView CreateCell(int index)
    {
        ItemSlotView v = CreateSlotView(_grid, "Cell_" + index, 76f, 52f);
        Destroy(v.placeholder.gameObject);
        v.placeholder = null;
        v.index = index;
        return v;
    }

    private ItemSlotView CreateSlotView(Transform parent, string name, float size, float iconSize)
    {
        Image frame = UIFactory.Frame(parent, name, skin.round12, FrostUI.PanelSolid, FrostUI.BorderDim, out Image fill);
        ((RectTransform)frame.transform).sizeDelta = new Vector2(size, size);
        var v = frame.gameObject.AddComponent<ItemSlotView>();
        v.border = frame;
        v.fill = fill;
        v.button = UIFactory.MakeButton(frame);

        v.placeholder = UIFactory.Img(frame.transform, "Placeholder", null, new Color(FrostUI.BorderDim.r, FrostUI.BorderDim.g, FrostUI.BorderDim.b, 0.9f));
        UIFactory.Anchored(v.placeholder.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(iconSize * 0.8f, iconSize * 0.8f)).pivot = new Vector2(0.5f, 0.5f);
        v.placeholder.preserveAspect = true;

        v.icon = UIFactory.Img(frame.transform, "Icon", null, Color.white);
        RectTransform irt = UIFactory.Anchored(v.icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));
        irt.pivot = new Vector2(0.5f, 0.5f);
        v.icon.preserveAspect = true;

        v.quantity = UIFactory.Text(frame.transform, "Qty", "", skin.nunito800, 14f, FrostboundUI.White, TextAlignmentOptions.BottomRight);
        UIFactory.Stretch(v.quantity.rectTransform, 7f);
        v.quantity.outlineWidth = 0.25f;
        v.quantity.outlineColor = new Color32(10, 18, 34, 255);

        v.button.onClick.AddListener(() => OnCellClicked(v));
        v.Action = DoAction;
        v.Hovered = OnCellHovered;
        v.Show(null, 0, true);
        return v;
    }
}
