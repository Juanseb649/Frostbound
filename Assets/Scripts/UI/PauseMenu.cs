using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pausa con Esc (o Start en el mando) en cualquier zona jugable: congela el juego y deja reanudar o volver al menú.
// Esc solo pausa si no hay otro menú abierto: el inventario, el mapa o una conversación se cierran primero.
[DefaultExecutionOrder(-1000)]
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    private CanvasGroup _group, _confirm;
    private FrostButton _resume, _quit, _settings;
    private RectTransform _panel;
    private RectTransform _legend;
    private TextMeshProUGUI _hero;
    private bool _leaving;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsPaused = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

    private static void Ensure()
    {
        if (!SceneIds.IsGameplay(SceneManager.GetActiveScene().name) && GameObject.Find("Player") == null) return;
        if (FindAnyObjectByType<PauseMenu>() != null) return;
        new GameObject("MenuPausa", typeof(RectTransform)).AddComponent<PauseMenu>();
    }

    void Awake()
    {
        Build();
        SetGroup(_group, false);
        SetGroup(_confirm, false);
    }

    void OnDestroy()
    {
        if (IsPaused) Unfreeze();
    }

    void Update()
    {
        if (_leaving) return;
        bool cancel = UICancel.Pressed();
        // START pausa desde el mando del jugador 1 o del jugador 2 (un mando libre con START une al jugador 2).
        // Pausa según Configuración → Control (Esc y el botón Menú por defecto), desde el jugador 1 o el 2.
        if (OptionsPanel.IsOpen) return;
        bool start = InputMap.KeyPressed(GameAction.Pause);
        GameObject p1 = Players.PrimaryObject;
        UnityEngine.InputSystem.Gamepad mine = p1 != null ? PlayerController.PadOf(p1.transform) : null;
        if (InputMap.PadPressed(mine, GameAction.Pause)) start = true;
        foreach (PlayerController pc in Players.All)
            if (pc != null && pc.playerIndex > 0 && InputMap.PadPressed(pc.pad, GameAction.Pause)) start = true;
        if (IsPaused)
        {
            if (cancel || start)
            {
                if (_confirm.interactable) CloseConfirm();
                else Resume();
            }
            return;
        }
        if (start && !OtherMenuOpen()) Pause();
    }

    public static bool OtherMenuOpen()
    {
        if (GameplayInput.Blocked || LoadingScreen.Active || TradeWindow.IsOpen || SaveSlotsPanel.IsOpen) return true;
        if (WorldHUD.Instance != null && WorldHUD.Instance.MenuOpen) return true;
        if (AdventureHUD.Instance != null && AdventureHUD.Instance.Map != null && AdventureHUD.Instance.Map.FullOpen) return true;
        GameObject p = GameObject.Find("Player");
        PlayerRespawn r = p != null ? p.GetComponent<PlayerRespawn>() : null;
        return r != null && r.IsDying;
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        GameplayInput.Block();
        GameSession s = GameSession.Instance;
        SaveData d = s != null ? s.Current : null;
        GameObject player = GameObject.Find("Player");
        CharacterStats stats = player != null ? player.GetComponent<CharacterStats>() : null;
        string name = d != null ? d.heroName : s != null ? s.HeroName : "";
        string level = stats != null ? "Nivel " + stats.level : "";
        _hero.text = string.Join(" · ", new[] { name, level, SceneIds.Title(SceneManager.GetActiveScene().name) }).Trim(' ', '·');
        BuildLegend();
        AudioManager.PlayUI(AudioId.UI_OpenMenu, 0.7f);
        SetGroup(_group, true);
        Select(_resume.button);
    }

    // Leyenda con los iconos del periférico preferido (teclas o botones de Xbox).
    private static readonly GameAction[] LegendActions = { GameAction.Attack, GameAction.Ability, GameAction.Dodge, GameAction.QuickPotion, GameAction.Interact, GameAction.Inventory, GameAction.Map, GameAction.Character };

    private void BuildLegend()
    {
        if (_legend == null) return;
        var old = new System.Collections.Generic.List<Transform>();
        foreach (Transform c in _legend) old.Add(c);
        foreach (Transform c in old) { c.SetParent(null, false); Destroy(c.gameObject); }
        InputMap.Device d = InputMap.Preferred;
        for (int i = 0; i < LegendActions.Length; i++)
        {
            GameAction a = LegendActions[i];
            int col = i % 2, row = i / 2;
            RectTransform cell = FrostUI.Node(_legend, "Leyenda_" + a);
            cell.anchorMin = cell.anchorMax = cell.pivot = new Vector2(0f, 1f);
            cell.anchoredPosition = new Vector2(20f + col * 300f, -row * 30f);
            cell.sizeDelta = new Vector2(290f, 28f);
            RectTransform g = InputGlyph.ForAction(cell, a, d, 26f);
            g.anchorMin = g.anchorMax = g.pivot = new Vector2(0f, 0.5f);
            g.anchoredPosition = Vector2.zero;
            TextMeshProUGUI t = FrostUI.Text(cell, "Texto", InputMap.ActionName(a), 16f, FrostUI.Label, TextAlignmentOptions.MidlineLeft, FrostUI.BodyLight);
            t.rectTransform.anchorMin = new Vector2(0f, 0f);
            t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.offsetMin = new Vector2(g.sizeDelta.x + 10f, 0f);
            t.rectTransform.offsetMax = Vector2.zero;
        }
    }

    public void Resume()
    {
        if (!IsPaused) return;
        UIDissolve.Ghost(_panel);
        SetGroup(_confirm, false);
        SetGroup(_group, false);
        Unfreeze();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void Unfreeze()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameplayInput.Unblock();
    }

    private void AskQuit()
    {
        SetGroup(_confirm, true);
        _group.interactable = false;
        Select(_confirm.GetComponentInChildren<Button>());
    }

    private void CloseConfirm()
    {
        SetGroup(_confirm, false);
        _group.interactable = true;
        Select(_quit.button);
    }

    public void QuitToMenu()
    {
        if (_leaving) return;
        StartCoroutine(Leave());
    }

    private IEnumerator Leave()
    {
        _leaving = true;
        GameSession s = GameSession.Instance;
        if (s != null) s.EndGame();
        _group.interactable = false;
        _confirm.interactable = false;
        yield return ScreenFade.Instance.Fade(1f, 0.4f);
        Unfreeze();
        SceneManager.LoadScene(SceneIds.MainMenu);
        yield return null;
        yield return ScreenFade.Instance.Fade(0f, 0.5f);
    }

    private static void SetGroup(CanvasGroup g, bool on)
    {
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }

    private static void Select(Selectable s)
    {
        if (s == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    private void Build()
    {
        gameObject.layer = 5;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        ScreenAdapter.Match(scaler);
        gameObject.AddComponent<GraphicRaycaster>();
        RectTransform root = (RectTransform)transform;

        RectTransform main = FrostUI.Stretch(FrostUI.Node(root, "Pausa"));
        _group = main.gameObject.AddComponent<CanvasGroup>();
        Image dim = FrostUI.Img(main, "Velo", null, new Color(0.01f, 0.02f, 0.05f, 0.72f));
        FrostUI.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Image panel = FrostUI.Panel(main, "Panel", out _, 18f);
        _panel = panel.rectTransform;
        FrostUI.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 640f));
        Transform p = panel.transform;
        TextMeshProUGUI kicker = FrostUI.Kicker(p, "Antetitulo", "Partida en pausa", 17f, FrostUI.Ice);
        kicker.alignment = TextAlignmentOptions.Center;
        FrostUI.Place(kicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(500f, 26f));
        TextMeshProUGUI title = FrostUI.Title(p, "Titulo", "PAUSA", 84f, TextAlignmentOptions.Center, 0.24f);
        FrostUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(520f, 104f));
        _hero = FrostUI.Text(p, "Heroe", "", 20f, FrostUI.Secondary, TextAlignmentOptions.Center);
        FrostUI.Place(_hero.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(520f, 30f));

        _resume = FrostButton.Create(p, "Reanudar", "Reanudar", new Vector2(400f, 66f), FrostButton.Style.Primary, 30f);
        FrostUI.Place((RectTransform)_resume.transform, new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(400f, 66f));
        _resume.button.onClick.AddListener(Resume);
        _settings = FrostButton.Create(p, "Configuracion", "Configuración", new Vector2(400f, 62f), FrostButton.Style.Normal, 28f);
        FrostUI.Place((RectTransform)_settings.transform, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(400f, 62f));
        _settings.button.onClick.AddListener(() => OptionsPanel.Open(() => { BuildLegend(); Select(_settings.button); }));
        _quit = FrostButton.Create(p, "SalirMenu", "Salir al menú", new Vector2(400f, 62f), FrostButton.Style.Normal, 28f);
        FrostUI.Place((RectTransform)_quit.transform, new Vector2(0.5f, 1f), new Vector2(0f, -382f), new Vector2(400f, 62f));
        _quit.button.onClick.AddListener(AskQuit);

        _legend = FrostUI.Node(p, "Controles");
        FrostUI.Place(_legend, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(600f, 120f));
        _legend.pivot = new Vector2(0.5f, 0f);

        RectTransform confirmRoot = FrostUI.Stretch(FrostUI.Node(root, "Confirmar"));
        _confirm = confirmRoot.gameObject.AddComponent<CanvasGroup>();
        Image cdim = FrostUI.Img(confirmRoot, "Velo", null, new Color(0.01f, 0.02f, 0.05f, 0.6f));
        FrostUI.Stretch(cdim.rectTransform);
        cdim.raycastTarget = true;
        Image box = FrostUI.Panel(confirmRoot, "Caja", out _, 18f);
        FrostUI.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 250f));
        TextMeshProUGUI ct = FrostUI.Title(box.transform, "Titulo", "¿Volver al menú?", 44f);
        FrostUI.Place(ct.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(560f, 60f));
        TextMeshProUGUI cx = FrostUI.Text(box.transform, "Texto", "Tu partida se guarda al salir. Volverás a la última hoguera encendida de esta zona.", 19f, FrostUI.Secondary, TextAlignmentOptions.Center);
        cx.textWrappingMode = TextWrappingModes.Normal;
        FrostUI.Place(cx.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(520f, 60f));
        FrostButton no = FrostButton.Create(box.transform, "Cancelar", "Seguir jugando", new Vector2(240f, 58f), FrostButton.Style.Normal, 24f);
        FrostUI.Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(-132f, 26f), new Vector2(240f, 58f));
        no.button.onClick.AddListener(CloseConfirm);
        FrostButton yes = FrostButton.Create(box.transform, "Salir", "Salir", new Vector2(240f, 58f), FrostButton.Style.Danger, 24f);
        FrostUI.Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(132f, 26f), new Vector2(240f, 58f));
        yes.button.onClick.AddListener(QuitToMenu);
    }
}
