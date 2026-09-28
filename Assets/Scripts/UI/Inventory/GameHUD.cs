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

    private CharacterStats _stats;
    private RectTransform _hpFill, _mpFill, _xpFill;
    private TextMeshProUGUI _hpText, _mpText, _xpText, _levelText, _pointsText;
    private RectTransform _pointsChip;
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
        Build();
        _stats.LeveledUp += OnLevelUp;
        _stats.ExperienceGained += OnExperienceGained;
        player.Inventory.ItemAdded += OnItemAdded;
        Notifications.Register(this);
    }

    private void OnExperienceGained(int amount) => ShowMessage("+" + amount + " XP", FrostboundUI.XpBar);

    void OnDestroy()
    {
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

        UIFactory.SetFill(_hpFill, _stats.currentHealth / Mathf.Max(1f, _stats.MaxHealth));
        UIFactory.SetFill(_mpFill, _stats.currentMana / Mathf.Max(1f, _stats.MaxMana));
        UIFactory.SetFill(_xpFill, _stats.LevelProgress);
        _hpText.text = Mathf.CeilToInt(_stats.currentHealth) + " / " + Mathf.CeilToInt(_stats.MaxHealth);
        _mpText.text = Mathf.FloorToInt(_stats.currentMana) + " / " + Mathf.CeilToInt(_stats.MaxMana);
        _levelText.text = _stats.level.ToString();
        _xpText.text = _stats.IsMaxLevel ? "NIVEL MÁXIMO" : "EXPERIENCIA  " + _stats.experience + " / " + _stats.ExperienceToNextLevel();

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
        ShowMessage("+ " + item.displayName + qty, FrostboundUI.Rarity(item.rarity));
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

    private void Build()
    {
        var root = (RectTransform)transform;

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
        _feed = UIFactory.Anchored(UIFactory.Node(root, "Feed"), new Vector2(1f, 0f), new Vector2(-32f, 40f), new Vector2(420f, 160f));
        var vl = _feed.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment = TextAnchor.LowerRight;
        vl.spacing = 4f;
        vl.childControlHeight = false;
        vl.childControlWidth = false;
        vl.childForceExpandHeight = false;
        vl.childForceExpandWidth = false;

        // Teclas.
        string hints = "Espacio  Rodar     I  Inventario     C  Personaje     Q  Poción de vida";
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
