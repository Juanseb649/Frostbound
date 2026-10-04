using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// HUD del juego: vida, maná, nivel, barra de experiencia, aviso de puntos sin repartir,
// cartel de subida de nivel y avisos (objetos recogidos, mensajes).
public class GameHUD : MonoBehaviour
{
    public UISkin skin;
    public Equipment player;
    public InventoryScreen inventoryScreen;
    [Tooltip("Muestra las teclas de depuración (X experiencia, G objeto).")]
    public bool showDebugHints = true;
    [Tooltip("Tamaño de las estatuas con los orbes respecto al diseño original (1 = grande como en el diseño).")]
    [Range(0.3f, 1f)] public float orbStatueScale = 0.46f;

    private CharacterStats _stats;
    private RectTransform _hpFill, _mpFill, _xpFill;
    private TextMeshProUGUI _hpText, _mpText, _xpText, _levelText, _pointsText;
    private RectTransform _pointsChip;
    private Image _hpOrb, _mpOrb;
    private PotionBelt _belt;
    private readonly Image[] _beltIcons = new Image[PotionBelt.Size];
    private readonly TextMeshProUGUI[] _beltCounts = new TextMeshProUGUI[PotionBelt.Size];
    private readonly float[] _beltFlash = new float[PotionBelt.Size];
    private CanvasGroup _toast;
    private TextMeshProUGUI _toastTitle, _toastSub;
    private float _toastTime = -1f;
    private RectTransform _feed;
    private readonly List<(TextMeshProUGUI text, float time)> _feedLines = new List<(TextMeshProUGUI, float)>();

    private const float ToastDuration = 3f;
    private const float FeedDuration = 3.5f;
    private const int FeedMax = 5;

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<Equipment>();
        if (player == null || skin == null)
        {
            Debug.LogWarning("[GameHUD] Falta el jugador (Equipment) o el UISkin.");
            enabled = false;
            return;
        }
        _stats = player.Stats;
        _belt = player.GetComponent<PotionBelt>();
        if (_belt == null) _belt = player.gameObject.AddComponent<PotionBelt>();
        _belt.Failed += OnBeltFailed;
        _belt.Used += OnBeltUsed;
        Build();
        _stats.LeveledUp += OnLevelUp;
        _stats.ExperienceGained += OnExperienceGained;
        player.Inventory.ItemAdded += OnItemAdded;
        Notifications.Register(this);
    }

    private void OnExperienceGained(int amount) => ShowMessage("+" + amount + " XP", FrostboundUI.XpBar);

    private void OnBeltFailed(string msg) => ShowMessage(msg, FrostboundUI.Negative);
    private void OnBeltUsed(int slot) => _beltFlash[slot] = Time.unscaledTime;

    void OnDestroy()
    {
        if (_belt != null)
        {
            _belt.Failed -= OnBeltFailed;
            _belt.Used -= OnBeltUsed;
        }
        Notifications.Unregister(this);
        if (_stats != null)
        {
            _stats.LeveledUp -= OnLevelUp;
            _stats.ExperienceGained -= OnExperienceGained;
        }
        if (player != null && player.Inventory != null) player.Inventory.ItemAdded -= OnItemAdded;
    }

    void Update()
    {
        if (_stats == null) return;

        float hp = _stats.currentHealth / Mathf.Max(1f, _stats.MaxHealth);
        float mp = _stats.currentMana / Mathf.Max(1f, _stats.MaxMana);
        if (_hpOrb != null)
        {
            _hpOrb.fillAmount = Mathf.MoveTowards(_hpOrb.fillAmount, Mathf.Clamp01(hp), Time.unscaledDeltaTime * 1.5f);
            _mpOrb.fillAmount = Mathf.MoveTowards(_mpOrb.fillAmount, Mathf.Clamp01(mp), Time.unscaledDeltaTime * 1.5f);
            UpdateBelt();
        }
        else
        {
            UIFactory.SetFill(_hpFill, hp);
            UIFactory.SetFill(_mpFill, mp);
        }
        UIFactory.SetFill(_xpFill, _stats.LevelProgress);
        _hpText.text = Mathf.CeilToInt(_stats.currentHealth) + " / " + Mathf.CeilToInt(_stats.MaxHealth);
        _mpText.text = Mathf.FloorToInt(_stats.currentMana) + " / " + Mathf.CeilToInt(_stats.MaxMana);
        _levelText.text = _stats.level.ToString();
        if (_hpOrb != null) _xpText.text = _stats.IsMaxLevel ? "NIVEL MÁXIMO" : _stats.experience + " / " + _stats.ExperienceToNextLevel() + " XP";
        if (_hpOrb == null) _xpText.text = _stats.IsMaxLevel ? "NIVEL MÁXIMO" : "EXPERIENCIA  " + _stats.experience + " / " + _stats.ExperienceToNextLevel();

        bool points = _stats.pointsAvailable > 0 && (inventoryScreen == null || !inventoryScreen.IsOpen);
        _pointsChip.gameObject.SetActive(points);
        if (points)
        {
            _pointsText.text = "+" + _stats.pointsAvailable + (_stats.pointsAvailable == 1 ? " PUNTO" : " PUNTOS") + "  ·  C";
            float s = 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.035f;
            _pointsChip.localScale = new Vector3(s, s, 1f);
        }

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.qKey.wasPressedThisFrame && !GameplayInput.Blocked)
        {
            if (!player.UseFirstHealing(out string msg) && msg.Length > 0) ShowMessage(msg, FrostboundUI.Negative);
        }

        UpdateToast();
        UpdateFeed();
    }

    // ----- Avisos -----

    private void OnLevelUp(int level)
    {
        _toastTitle.text = "¡NIVEL " + level + "!";
        _toastSub.text = "+" + _stats.pointsPerLevel + " puntos de atributo  ·  pulsa C para repartirlos";
        _toastTime = 0f;
    }

    private void OnItemAdded(ItemDefinition item, int amount)
    {
        string qty = amount > 1 ? "  ×" + amount : "";
        ItemStack s = player != null && player.Inventory != null ? player.Inventory.LastAddedStack : null;
        if (s != null && s.item == item) ShowMessage("+ " + s.DisplayName + qty, s.DisplayColor);
        else ShowMessage("+ " + item.displayName + qty, FrostboundUI.Rarity(item.rarity));
    }

    public void ShowMessage(string message, Color color)
    {
        if (_feed == null) return;
        TextMeshProUGUI t = UIFactory.Text(_feed, "Line", message, skin.nunito800, 16f, color, TextAlignmentOptions.Right);
        t.rectTransform.sizeDelta = new Vector2(420f, 24f);
        t.outlineWidth = 0.18f;
        t.outlineColor = new Color32(10, 18, 34, 255);
        _feedLines.Add((t, Time.unscaledTime));
        while (_feedLines.Count > FeedMax)
        {
            Destroy(_feedLines[0].text.gameObject);
            _feedLines.RemoveAt(0);
        }
    }

    private void UpdateFeed()
    {
        for (int i = _feedLines.Count - 1; i >= 0; i--)
        {
            float age = Time.unscaledTime - _feedLines[i].time;
            if (age > FeedDuration)
            {
                Destroy(_feedLines[i].text.gameObject);
                _feedLines.RemoveAt(i);
                continue;
            }
            _feedLines[i].text.alpha = Mathf.Clamp01((FeedDuration - age) / 0.6f);
        }
    }

    private void UpdateToast()
    {
        if (_toastTime < 0f)
        {
            _toast.alpha = 0f;
            return;
        }
        _toastTime += Time.unscaledDeltaTime;
        float t = _toastTime;
        _toast.alpha = t < 0.2f ? t / 0.2f : Mathf.Clamp01((ToastDuration - t) / 0.6f);
        float s = t < 0.2f ? Mathf.Lerp(1.15f, 1f, t / 0.2f) : 1f;
        _toast.transform.localScale = new Vector3(s, s, 1f);
        if (t > ToastDuration) _toastTime = -1f;
    }

    // ----- Construcción -----

    private void UpdateBelt()
    {
        for (int i = 0; i < PotionBelt.Size; i++)
        {
            ItemDefinition item = _belt.slots[i];
            int n = _belt.Count(i);
            _beltIcons[i].sprite = item != null ? item.icon : null;
            _beltIcons[i].enabled = item != null && item.icon != null;
            _beltIcons[i].color = n > 0 ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            _beltCounts[i].text = item != null ? n.ToString() : "";
            float f = Mathf.Clamp01(1f - (Time.unscaledTime - _beltFlash[i]) / 0.35f);
            float sc = 1f + f * 0.18f;
            _beltIcons[i].rectTransform.localScale = new Vector3(sc, sc, 1f);
        }
    }

    // HUD de orbes: estatua del caballero con el orbe de vida (izquierda), estatua del mago con el de maná (derecha)
    // y en el centro la barra de piedra con las 6 pociones de acceso rápido.
    private const float DesignScale = FrostboundUI.ReferenceWidth / 1920f;

    private Image OrbSide(RectTransform root, bool life)
    {
        float w = 1100f * 0.62f * DesignScale * orbStatueScale, h = 900f * 0.62f * DesignScale * orbStatueScale;
        RectTransform side = UIFactory.Node(root, life ? "OrbeVida" : "OrbeMana");
        // Pegadas a los extremos de la barra de pociones, alineadas con ella.
        side.anchorMin = side.anchorMax = new Vector2(0.5f, 0f);
        side.pivot = new Vector2(life ? 1f : 0f, 0f);
        float barEdge = (1264f - 960f) * DesignScale - 6f;
        side.anchoredPosition = new Vector2(life ? -barEdge : barEdge, 0f);
        side.sizeDelta = new Vector2(w, h);

        Image statue = UIFactory.Img(side, "Estatua", life ? skin.hudStatueLife : skin.hudStatueMana, Color.white);
        statue.preserveAspect = false;
        UIFactory.Stretch(statue.rectTransform);

        float cx = life ? 770f : 330f, cy = 570f, r = 270f;
        Vector2 aMin = new Vector2((cx - r) / 1100f, 1f - (cy + r) / 900f);
        Vector2 aMax = new Vector2((cx + r) / 1100f, 1f - (cy - r) / 900f);

        Image back = UIFactory.Img(side, "OrbeFondo", skin.hudOrbBack, Color.white);
        back.preserveAspect = false;
        back.rectTransform.anchorMin = aMin; back.rectTransform.anchorMax = aMax;
        back.rectTransform.offsetMin = back.rectTransform.offsetMax = Vector2.zero;

        Image fill = UIFactory.Img(side, "OrbeLiquido", life ? skin.hudOrbFillLife : skin.hudOrbFillMana, Color.white);
        fill.preserveAspect = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.fillAmount = 1f;
        fill.rectTransform.anchorMin = aMin; fill.rectTransform.anchorMax = aMax;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

        Image frame = UIFactory.Img(side, "Marco", life ? skin.hudOrbFrameLife : skin.hudOrbFrameMana, Color.white);
        frame.preserveAspect = false;
        UIFactory.Stretch(frame.rectTransform);

        Color c = life ? new Color32(255, 225, 220, 255) : new Color32(214, 230, 255, 255);
        TextMeshProUGUI value = UIFactory.Text(side, "Valor", "", skin.cinzel800, Mathf.Max(11f, 17f * orbStatueScale * 1.4f), c, TextAlignmentOptions.Center);
        value.rectTransform.anchorMin = new Vector2(aMin.x, aMin.y + (aMax.y - aMin.y) * 0.34f);
        value.rectTransform.anchorMax = new Vector2(aMax.x, aMin.y + (aMax.y - aMin.y) * 0.5f);
        value.rectTransform.offsetMin = value.rectTransform.offsetMax = Vector2.zero;
        value.outlineWidth = 0.22f;
        value.outlineColor = new Color32(5, 6, 8, 255);
        TextMeshProUGUI cap = UIFactory.Text(side, "Titulo", life ? "VIDA" : "MANÁ", skin.cinzel600, Mathf.Max(7f, 10f * orbStatueScale * 1.4f), c, TextAlignmentOptions.Center, 4f);
        cap.rectTransform.anchorMin = new Vector2(aMin.x, aMin.y + (aMax.y - aMin.y) * 0.26f);
        cap.rectTransform.anchorMax = new Vector2(aMax.x, aMin.y + (aMax.y - aMin.y) * 0.36f);
        cap.rectTransform.offsetMin = cap.rectTransform.offsetMax = Vector2.zero;
        cap.outlineWidth = 0.25f;
        cap.outlineColor = new Color32(5, 6, 8, 255);
        if (life) _hpText = value; else _mpText = value;
        return fill;
    }

    private static void Anchor(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        // Coordenadas de la barra de diseño (1920 × 230, origen arriba a la izquierda).
        rt.anchorMin = new Vector2(x0 / 1920f, 1f - y1 / 230f);
        rt.anchorMax = new Vector2(x1 / 1920f, 1f - y0 / 230f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private void BuildOrbHud(RectTransform root)
    {
        _hpOrb = OrbSide(root, true);
        _mpOrb = OrbSide(root, false);

        RectTransform bar = UIFactory.Node(root, "BarraPociones");
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(1920f * DesignScale, 230f * DesignScale);
        Image barImg = UIFactory.Img(bar, "Piedra", skin.hudBar, Color.white);
        barImg.preserveAspect = false;
        UIFactory.Stretch(barImg.rectTransform);

        for (int i = 0; i < PotionBelt.Size; i++)
        {
            int slot = i;
            float x = 709f + i * 86f;
            Image hit = UIFactory.Img(bar, "Pocion_" + (i + 1), null, new Color(0f, 0f, 0f, 0f));
            hit.raycastTarget = true;
            Anchor(hit.rectTransform, x, 110f, x + 72f, 182f);
            Button b = hit.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => _belt.Use(slot));
            Image icon = UIFactory.Img(hit.transform, "Icono", null, Color.white);
            UIFactory.Stretch(icon.rectTransform, 5f);
            _beltIcons[i] = icon;
            TextMeshProUGUI key = UIFactory.Text(hit.transform, "Tecla", (i + 1).ToString(), skin.cinzel800, 10f, new Color32(207, 198, 184, 255), TextAlignmentOptions.TopLeft);
            UIFactory.Stretch(key.rectTransform, 3f);
            key.outlineWidth = 0.25f; key.outlineColor = new Color32(5, 6, 8, 255);
            TextMeshProUGUI count = UIFactory.Text(hit.transform, "Cantidad", "", skin.cinzel800, 12f, Color.white, TextAlignmentOptions.BottomRight);
            UIFactory.Stretch(count.rectTransform, 3f);
            count.outlineWidth = 0.25f; count.outlineColor = new Color32(5, 6, 8, 255);
            _beltCounts[i] = count;
        }

        _xpFill = UIFactory.Bar(bar, "Experiencia", skin.pill, new Color(0.03f, 0.04f, 0.05f, 1f), FrostboundUI.XpBar, out RectTransform xp);
        Anchor(xp, 709f, 192f, 1211f, 202f);
        _levelText = UIFactory.Text(bar, "Nivel", "1", skin.cinzel800, 14f, FrostboundUI.ChipGoldText, TextAlignmentOptions.Center);
        Anchor(_levelText.rectTransform, 670f, 186f, 706f, 208f);
        _xpText = UIFactory.Text(bar, "ExperienciaTexto", "", skin.nunito800, 9f, FrostboundUI.Muted, TextAlignmentOptions.Center, 1.5f);
        Anchor(_xpText.rectTransform, 1214f, 186f, 1300f, 208f);
        _xpText.alignment = TextAlignmentOptions.Left;

        Image chip = UIFactory.Frame(bar, "PointsChip", skin.pill, FrostboundUI.ChipGoldBg, FrostboundUI.Gold, out _);
        _pointsChip = chip.rectTransform;
        _pointsChip.anchorMin = _pointsChip.anchorMax = new Vector2(0.5f, 1f);
        _pointsChip.pivot = new Vector2(0.5f, 0f);
        _pointsChip.anchoredPosition = new Vector2(0f, 8f);
        _pointsChip.sizeDelta = new Vector2(200f, 30f);
        _pointsText = UIFactory.Text(chip.transform, "Label", "", skin.nunito800, 13f, FrostboundUI.ChipGoldText, TextAlignmentOptions.Center, 1.5f);
        UIFactory.Stretch(_pointsText.rectTransform);
        Button chipButton = UIFactory.MakeButton(chip);
        chipButton.onClick.AddListener(() => { if (inventoryScreen != null) inventoryScreen.Open(); });
    }

    private void Build()
    {
        var root = (RectTransform)transform;
        if (skin.HasOrbHud)
        {
            BuildOrbHud(root);
            BuildCommon(root);
            return;
        }

        // Barra inferior.
        RectTransform bottom = UIFactory.Anchored(UIFactory.Node(root, "BottomBar"), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(760f, 150f));

        _hpFill = UIFactory.Bar(bottom, "HealthBar", skin.pill, FrostboundUI.Surface, FrostboundUI.HealthBar, out RectTransform hp);
        UIFactory.TopLeft(hp, 60f, 86f, 250f, 22f);
        _hpText = BarText(hp);
        Label(bottom, "VIDA", 64f, 66f);

        _mpFill = UIFactory.Bar(bottom, "ManaBar", skin.pill, FrostboundUI.Surface, FrostboundUI.ManaBar, out RectTransform mp);
        UIFactory.TopLeft(mp, 450f, 86f, 250f, 22f);
        _mpText = BarText(mp);
        TextMeshProUGUI mpLabel = Label(bottom, "MANÁ", 450f, 66f);
        mpLabel.alignment = TextAlignmentOptions.TopRight;
        mpLabel.rectTransform.sizeDelta = new Vector2(246f, 16f);

        Image badge = UIFactory.Frame(bottom, "LevelBadge", skin.circle, FrostboundUI.Bg, FrostboundUI.Gold, out _);
        badge.preserveAspect = false;
        UIFactory.TopLeft(badge.rectTransform, 344f, 64f, 72f, 72f);
        badge.transform.Find("Fill").GetComponent<RectTransform>().offsetMin = new Vector2(3f, 3f);
        badge.transform.Find("Fill").GetComponent<RectTransform>().offsetMax = new Vector2(-3f, -3f);
        _levelText = UIFactory.Text(badge.transform, "Level", "1", skin.cinzel800, 30f, FrostboundUI.Title, TextAlignmentOptions.Center);
        UIFactory.Stretch(_levelText.rectTransform);
        TextMeshProUGUI nv = UIFactory.Text(badge.transform, "Caption", "NIVEL", skin.nunito800, 9f, FrostboundUI.Gold, TextAlignmentOptions.Center, 2f);
        UIFactory.TopLeft(nv.rectTransform, 0f, 10f, 72f, 12f);

        _xpFill = UIFactory.Bar(bottom, "XpBar", skin.pill, FrostboundUI.Surface, FrostboundUI.XpBar, out RectTransform xp);
        UIFactory.TopLeft(xp, 60f, 142f, 640f, 8f);
        _xpText = UIFactory.Text(bottom, "XpText", "", skin.nunito800, 11f, FrostboundUI.Muted, TextAlignmentOptions.Center, 2f);
        UIFactory.TopLeft(_xpText.rectTransform, 60f, 124f, 640f, 16f);

        // Aviso de puntos sin repartir (clic para abrir el personaje).
        Image chip = UIFactory.Frame(bottom, "PointsChip", skin.pill, FrostboundUI.ChipGoldBg, FrostboundUI.Gold, out _);
        _pointsChip = chip.rectTransform;
        UIFactory.TopLeft(_pointsChip, 280f, 18f, 200f, 32f);
        _pointsChip.pivot = new Vector2(0.5f, 0.5f);
        _pointsChip.anchoredPosition += new Vector2(100f, -16f);
        _pointsText = UIFactory.Text(chip.transform, "Label", "", skin.nunito800, 13f, FrostboundUI.ChipGoldText, TextAlignmentOptions.Center, 1.5f);
        UIFactory.Stretch(_pointsText.rectTransform);
        Button chipButton = UIFactory.MakeButton(chip);
        chipButton.onClick.AddListener(() => { if (inventoryScreen != null) inventoryScreen.Open(); });
        BuildCommon(root);
    }

    private void BuildCommon(RectTransform root)
    {
        // Cartel de subida de nivel.
        RectTransform toast = UIFactory.Anchored(UIFactory.Node(root, "LevelUpToast"), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 110f));
        toast.pivot = new Vector2(0.5f, 0.5f);
        _toast = toast.gameObject.AddComponent<CanvasGroup>();
        _toast.alpha = 0f;
        _toast.blocksRaycasts = false;
        _toastTitle = UIFactory.Text(toast, "Title", "", skin.cinzel800, 52f, FrostboundUI.Title, TextAlignmentOptions.Center, 4f);
        UIFactory.TopLeft(_toastTitle.rectTransform, 0f, 0f, 900f, 64f);
        _toastTitle.outlineWidth = 0.15f;
        _toastTitle.outlineColor = new Color32(10, 18, 34, 255);
        _toastSub = UIFactory.Text(toast, "Sub", "", skin.nunito800, 18f, FrostboundUI.ChipGoldText, TextAlignmentOptions.Center, 1f);
        UIFactory.TopLeft(_toastSub.rectTransform, 0f, 70f, 900f, 28f);
        _toastSub.outlineWidth = 0.2f;
        _toastSub.outlineColor = new Color32(10, 18, 34, 255);

        // Avisos (abajo a la derecha).
        _feed = UIFactory.Anchored(UIFactory.Node(root, "Feed"), new Vector2(1f, 0f), new Vector2(-32f, skin.HasOrbHud ? 60f + 420f * orbStatueScale : 40f), new Vector2(420f, 160f));
        var vl = _feed.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment = TextAnchor.LowerRight;
        vl.spacing = 4f;
        vl.childControlHeight = false;
        vl.childControlWidth = false;
        vl.childForceExpandHeight = false;
        vl.childForceExpandWidth = false;

        // Teclas.
        string hints = "Espacio  Rodar     I  Inventario     C  Personaje     1–6  Pociones     Q  Poción de vida";
        if (showDebugHints && player.GetComponent<ProgressionDebug>() != null) hints += "\n<color=#5B6F8A>Depuración:  X  +experiencia     Mayús+X  subir nivel     G  objeto al azar</color>";
        TextMeshProUGUI hint = UIFactory.Text(root, "Hints", hints, skin.nunito700, 13f, FrostboundUI.Muted);
        UIFactory.Anchored(hint.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(700f, 44f));
        hint.outlineWidth = 0.2f;
        hint.outlineColor = new Color32(10, 18, 34, 255);
    }

    private TextMeshProUGUI BarText(RectTransform bar)
    {
        TextMeshProUGUI t = UIFactory.Text(bar, "Value", "", skin.nunito800, 13f, FrostboundUI.White, TextAlignmentOptions.Center);
        UIFactory.Stretch(t.rectTransform);
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(10, 18, 34, 255);
        return t;
    }

    private TextMeshProUGUI Label(RectTransform parent, string text, float x, float y)
    {
        TextMeshProUGUI t = UIFactory.Text(parent, "Label_" + text, text, skin.nunito800, 11f, FrostboundUI.Muted, TextAlignmentOptions.TopLeft, 3f);
        UIFactory.TopLeft(t.rectTransform, x, y, 246f, 16f);
        return t;
    }
}
