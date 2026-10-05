using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Capa de HUD de la aventura: minimapa y mapa completo, panel de misión (debajo del minimapa),
// barra del jefe al estilo Souls (abajo, sobre el cinturón) y aviso de veneno.
public class AdventureHUD : MonoBehaviour
{
    public static AdventureHUD Instance { get; private set; }
    public Canvas Canvas { get; private set; }
    public MapUI Map { get; private set; }

    private UISkin _skin;
    private RectTransform _tracker;
    private TextMeshProUGUI _questTitle, _questObjective, _questDetail;
    private RectTransform _bossRoot, _bossFill, _bossTrail;
    private TextMeshProUGUI _bossName;
    private CanvasGroup _bossGroup;
    private TextMeshProUGUI _poison, _coins;
    private Wallet _wallet;
    private float _trail = 1f;
    private BossController _shownBoss;
    private PlayerAfflictions _afflictions;

    public static AdventureHUD Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("HUD_Aventura", typeof(RectTransform));
        return go.AddComponent<AdventureHUD>();
    }

    void Awake()
    {
        Instance = this;
        _skin = GameDatabase.Instance != null ? GameDatabase.Instance.uiSkin : null;
        Canvas = gameObject.AddComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Canvas.sortingOrder = 20;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(FrostboundUI.ReferenceWidth, FrostboundUI.ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;
        BuildTracker();
        BuildBossBar();
        Map = gameObject.AddComponent<MapUI>();
        Map.Build(transform, _skin);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildTracker()
    {
        _tracker = UIFactory.Node(transform, "Mision");
        UIFactory.Anchored(_tracker, new Vector2(1f, 1f), new Vector2(-18f, -262f), new Vector2(300f, 92f));
        Image band = UIFactory.Img(_tracker, "Franja", null, new Color(0.02f, 0.04f, 0.08f, 0.62f));
        UIFactory.Stretch(band.rectTransform);
        Image accent = UIFactory.Img(_tracker, "Acento", null, FrostboundUI.Gold);
        UIFactory.Anchored(accent.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(3f, 92f));
        _questTitle = UIFactory.Text(_tracker, "Titulo", "", _skin != null ? _skin.cinzel600 : null, 17f, FrostboundUI.Gold, TextAlignmentOptions.TopRight, 1f);
        UIFactory.Anchored(_questTitle.rectTransform, new Vector2(1f, 1f), new Vector2(-14f, -8f), new Vector2(280f, 22f));
        _questObjective = UIFactory.Text(_tracker, "Objetivo", "", _skin != null ? _skin.nunito700 : null, 14.5f, FrostboundUI.Text, TextAlignmentOptions.TopRight);
        _questObjective.textWrappingMode = TextWrappingModes.Normal;
        UIFactory.Anchored(_questObjective.rectTransform, new Vector2(1f, 1f), new Vector2(-14f, -32f), new Vector2(270f, 40f));
        _questDetail = UIFactory.Text(_tracker, "Detalle", "", _skin != null ? _skin.nunito800 : null, 13.5f, FrostboundUI.Negative, TextAlignmentOptions.TopRight);
        UIFactory.Anchored(_questDetail.rectTransform, new Vector2(1f, 1f), new Vector2(-14f, -70f), new Vector2(270f, 18f));

        _poison = UIFactory.Text(transform, "Veneno", "", _skin != null ? _skin.nunito800 : null, 15f, PlayerAfflictions.PoisonColor, TextAlignmentOptions.Center, 1.5f, true);
        UIFactory.Anchored(_poison.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 236f), new Vector2(300f, 22f));
        _coins = UIFactory.Text(transform, "Monedas", "", _skin != null ? _skin.nunito800 : null, 17f, new Color(1f, 0.82f, 0.32f), TextAlignmentOptions.BottomLeft, 1f);
        UIFactory.Anchored(_coins.rectTransform, new Vector2(0f, 0f), new Vector2(26f, 196f), new Vector2(260f, 24f));
        _coins.outlineWidth = 0.22f;
        _coins.outlineColor = new Color32(20, 12, 0, 220);
        _poison.outlineWidth = 0.2f;
        _poison.outlineColor = new Color32(0, 0, 0, 200);
    }

    private void BuildBossBar()
    {
        _bossRoot = UIFactory.Node(transform, "Jefe");
        UIFactory.Anchored(_bossRoot, new Vector2(0.5f, 0f), new Vector2(0f, 262f), new Vector2(760f, 46f));
        _bossGroup = _bossRoot.gameObject.AddComponent<CanvasGroup>();
        _bossName = UIFactory.Text(_bossRoot, "Nombre", "", _skin != null ? _skin.cinzel600 : null, 19f, new Color(0.93f, 0.9f, 0.84f), TextAlignmentOptions.BottomLeft, 1.5f);
        UIFactory.Anchored(_bossName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(760f, 26f));
        _bossName.outlineWidth = 0.2f;
        _bossName.outlineColor = new Color32(0, 0, 0, 230);
        Image back = UIFactory.Img(_bossRoot, "Fondo", null, new Color(0.03f, 0.02f, 0.02f, 0.9f));
        UIFactory.Anchored(back.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(760f, 12f));
        Image border = UIFactory.Img(back.transform, "Borde", null, new Color(0.45f, 0.38f, 0.28f, 0.9f));
        UIFactory.Stretch(border.rectTransform, -1.5f);
        border.transform.SetAsFirstSibling();
        _bossTrail = UIFactory.Bar(back.transform, "Rastro", null, new Color(0f, 0f, 0f, 0f), new Color(0.85f, 0.7f, 0.35f, 0.9f), out RectTransform trailRoot);
        UIFactory.Stretch(trailRoot, 1.5f);
        _bossFill = UIFactory.Bar(back.transform, "Vida", null, new Color(0f, 0f, 0f, 0f), new Color(0.62f, 0.09f, 0.08f), out RectTransform fillRoot);
        UIFactory.Stretch(fillRoot, 1.5f);
        _bossGroup.alpha = 0f;
    }

    public void SetVisible(bool visible)
    {
        Canvas.enabled = visible;
        if (!visible && Map != null) Map.Close();
    }

    void Update()
    {
        UpdateTracker();
        UpdateBoss();
        UpdatePoison();
        if (_wallet == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) _wallet = Wallet.Of(p);
        }
        if (_wallet != null) _coins.text = "● " + _wallet.Coins.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-ES")) + " monedas";
    }

    private void UpdateTracker()
    {
        int stage = QuestLog.Stage(QuestLog.Citadel);
        string objective = QuestLog.Objective(QuestLog.Citadel, stage);
        bool show = !string.IsNullOrEmpty(objective);
        _tracker.gameObject.SetActive(show);
        if (!show) return;
        _questTitle.text = QuestLog.Title(QuestLog.Citadel);
        CitadelSiege siege = CitadelSiege.Instance;
        string detail = "";
        if (stage == QuestLog.CitadelTalked && siege != null)
        {
            if (siege.Current == CitadelSiege.Phase.Waves) { objective = "Resiste el asedio en la plaza"; detail = "Oleada " + Mathf.Max(1, siege.Wave) + "/" + siege.WaveCount + " · quedan " + siege.Remaining; }
            else if (siege.Current == CitadelSiege.Phase.Boss) { objective = "Derrota a Gorvald, el bárbaro corrupto"; detail = "Cuidado: su hacha envenena"; }
        }
        _questObjective.text = objective;
        _questDetail.text = detail;
    }

    private void UpdateBoss()
    {
        BossController boss = BossController.Active;
        bool show = boss != null && boss.Engaged && !boss.Health.IsDead;
        _bossGroup.alpha = Mathf.MoveTowards(_bossGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 2.5f);
        if (boss == null) return;
        if (boss != _shownBoss)
        {
            _shownBoss = boss;
            _trail = 1f;
        }
        float hp = boss.Health.Health / Mathf.Max(1f, boss.Health.maxHealth);
        _bossName.text = boss.title;
        UIFactory.SetFill(_bossFill, hp);
        _trail = hp > _trail ? hp : Mathf.MoveTowards(_trail, hp, Time.deltaTime * 0.35f);
        UIFactory.SetFill(_bossTrail, _trail);
    }

    private void UpdatePoison()
    {
        if (_afflictions == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) _afflictions = p.GetComponent<PlayerAfflictions>();
        }
        bool on = _afflictions != null && _afflictions.Poisoned;
        _poison.text = on ? "Envenenado " + Mathf.CeilToInt(_afflictions.PoisonLeft) + "s" : "";
    }
}
