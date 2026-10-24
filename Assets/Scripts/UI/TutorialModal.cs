using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// Tutorial «Cómo jugar»: ventanas por páginas que explican los controles y la meta del juego.
// Sale solo la primera vez que se entra a una zona jugable y se puede volver a ver desde Pausa → Cómo jugar.
// Cada página puede llevar una captura o un vídeo corto: Resources/Tutorial/<id> (imagen o VideoClip .mp4).
public class TutorialModal : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private const string PrefSeen = "tutorial_visto";
    private const float AutoDelay = 2f;

    private struct Page
    {
        public string id, title, body;
        public GameAction[] keys;
        public Page(string id, string title, string body, params GameAction[] keys) { this.id = id; this.title = title; this.body = body; this.keys = keys; }
    }

    private static readonly Page[] Pages =
    {
        new Page("bienvenida", "Bienvenido a Frostbound",
            "Eres un pingüino aventurero en un poblado helado. Tu meta es ascender al Frostspire, la montaña que domina el norte, y llegar hasta el Corazón del Frost en la cumbre."),
        new Page("moverse", "Moverse y rodar",
            "Camina con las teclas de dirección (o el stick del mando). Rueda para esquivar: es la mejor defensa contra los golpes fuertes.",
            GameAction.MoveUp, GameAction.MoveLeft, GameAction.MoveDown, GameAction.MoveRight, GameAction.Dodge),
        new Page("atacar", "Atacar",
            "Haz clic izquierdo para golpear hacia el cursor y mantenlo pulsado para encadenar el combo. Si haces clic sobre un enemigo lejano, tu héroe se acerca solo. Con mando, el ataque apunta al enemigo más cercano.",
            GameAction.Attack, GameAction.SwapWeapons),
        new Page("habilidad", "Habilidad de clase",
            "Cada clase tiene un poder especial que gasta maná. Su casilla está abajo a la izquierda, encima de tus monedas: se ilumina en dorado cuando está lista y muestra el coste y la recarga.",
            GameAction.Ability),
        new Page("pociones", "Vida, maná y pociones",
            "El orbe rojo es tu vida y el azul tu maná. El cinturón guarda hasta seis pociones: úsalas con la tecla de cada casilla o bebe vida al momento con la poción rápida.",
            GameAction.QuickPotion, GameAction.Belt1, GameAction.Belt2),
        new Page("botin", "Botín y monedas",
            "Los enemigos sueltan armas, armaduras, runas y monedas. Haz clic en un objeto del suelo o pulsa Interactuar cerca de él para recogerlo.",
            GameAction.Interact),
        new Page("inventario", "Inventario y personaje",
            "Equipa lo que encuentres para hacerte más fuerte. Al subir de nivel ganas puntos: repártelos en la ficha del personaje.",
            GameAction.Inventory, GameAction.Character),
        new Page("npc", "Habla con el poblado",
            "Acércate a un pingüino y pulsa Interactuar (o haz clic sobre él) para hablar, comerciar o aceptar misiones.",
            GameAction.Interact),
        new Page("mapa", "Misiones y mapa",
            "La misión activa aparece arriba a la derecha, debajo del minimapa, que marca a dónde ir. Ábrelo en grande con el mapa.",
            GameAction.Map),
        new Page("hogueras", "Hogueras",
            "Las hogueras se encienden solas al pasar cerca: guardan tu avance y, si caes en combate, vuelves a la encendida más cercana."),
        new Page("frostspire", "El camino al Frostspire",
            "Al principio una ventisca cierra el sendero de la montaña. Ayuda al poblado, termina «La ciudadela caída» y vuelve a hablar con Steve: él te abrirá el camino. Puedes volver a ver este tutorial desde la pausa.",
            GameAction.Pause),
    };

    private CanvasGroup _group;
    private RectTransform _panel, _mediaBox, _keys;
    private RawImage _media;
    private TextMeshProUGUI _kicker, _title, _body;
    private FrostButton _prev, _next, _skip;
    private Image[] _dots;
    private VideoPlayer _video;
    private RenderTexture _videoTarget;
    private int _page;
    private bool _froze, _waiting;
    private float _readyFor;
    private Action _onClose;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsOpen = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

    private static TutorialModal Ensure()
    {
        if (!SceneIds.IsGameplay(SceneManager.GetActiveScene().name) && GameObject.Find("Player") == null) return null;
        TutorialModal t = FindAnyObjectByType<TutorialModal>();
        if (t == null) t = new GameObject("Tutorial", typeof(RectTransform)).AddComponent<TutorialModal>();
        return t;
    }

    // Abre el tutorial desde el principio (por ejemplo desde la pausa). onClose se llama al cerrarlo.
    public static void Open(Action onClose = null)
    {
        TutorialModal t = Ensure();
        if (t == null) { onClose?.Invoke(); return; }
        t.Show(onClose);
    }

    void Awake()
    {
        Build();
        SetGroup(false);
        _waiting = PlayerPrefs.GetInt(PrefSeen, 0) == 0;
    }

    void OnDestroy()
    {
        if (IsOpen) Close(false);
        if (_videoTarget != null) _videoTarget.Release();
    }

    void Update()
    {
        if (IsOpen)
        {
            if (UICancel.Pressed()) Close(true);
            return;
        }
        if (!_waiting) return;
        // La primera vez: espera a que acaben la pantalla de carga, las charlas y cualquier otro menú.
        if (PauseMenu.IsPaused || PauseMenu.OtherMenuOpen() || PrimaryInDialogue())
        {
            _readyFor = 0f;
            return;
        }
        _readyFor += Time.unscaledDeltaTime;
        if (_readyFor >= AutoDelay) Show(null);
    }

    private static bool PrimaryInDialogue()
    {
        GameObject p = Players.PrimaryObject;
        PlayerController pc = p != null ? p.GetComponent<PlayerController>() : null;
        return pc != null && pc.InDialogue;
    }

    private void Show(Action onClose)
    {
        if (IsOpen) return;
        _waiting = false;
        PlayerPrefs.SetInt(PrefSeen, 1);
        PlayerPrefs.Save();
        _onClose = onClose;
        IsOpen = true;
        // Desde la pausa el juego ya está congelado; si sale solo, lo congela él.
        _froze = !PauseMenu.IsPaused;
        if (_froze)
        {
            Time.timeScale = 0f;
            GameplayInput.Block();
        }
        AudioManager.PlayUI(AudioId.UI_OpenMenu, 0.7f);
        SetGroup(true);
        ShowPage(0);
    }

    private void Close(bool sound)
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (_video != null) _video.Stop();
        if (sound) UIDissolve.Ghost(_panel);
        SetGroup(false);
        if (_froze)
        {
            Time.timeScale = 1f;
            GameplayInput.Unblock();
            _froze = false;
        }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        Action done = _onClose;
        _onClose = null;
        done?.Invoke();
    }

    private void ShowPage(int i)
    {
        _page = Mathf.Clamp(i, 0, Pages.Length - 1);
        Page p = Pages[_page];
        _kicker.text = "Cómo jugar · " + (_page + 1) + " / " + Pages.Length;
        _title.text = p.title;
        _body.text = p.body;
        bool hasMedia = LoadMedia(p.id);
        _mediaBox.gameObject.SetActive(hasMedia);
        // Sin captura, el texto sube y ocupa el centro de la ventana.
        _body.rectTransform.anchoredPosition = new Vector2(0f, hasMedia ? -490f : -190f);
        _keys.anchoredPosition = new Vector2(0f, hasMedia ? -610f : -330f);
        BuildKeys(p.keys);
        for (int d = 0; d < _dots.Length; d++) _dots[d].color = d == _page ? FrostUI.Gold : new Color(1f, 1f, 1f, 0.22f);
        _prev.button.interactable = _page > 0;
        bool last = _page == Pages.Length - 1;
        if (_next.label != null) _next.label.text = last ? "¡A jugar!" : "Siguiente";
        _skip.gameObject.SetActive(!last);
        Select(_next.button);
    }

    private void Next()
    {
        if (_page >= Pages.Length - 1) { Close(true); return; }
        AudioManager.PlayUI(AudioId.UI_Click, 0.6f);
        ShowPage(_page + 1);
    }

    private void Prev()
    {
        if (_page == 0) return;
        ShowPage(_page - 1);
    }

    // Captura o vídeo de la página, si existe en Resources/Tutorial.
    private bool LoadMedia(string id)
    {
        if (_video != null) _video.Stop();
        VideoClip clip = Resources.Load<VideoClip>("Tutorial/" + id);
        if (clip != null)
        {
            if (_video == null)
            {
                _video = gameObject.AddComponent<VideoPlayer>();
                _video.playOnAwake = false;
                _video.isLooping = true;
                _video.audioOutputMode = VideoAudioOutputMode.None;
                _video.renderMode = VideoRenderMode.RenderTexture;
                _video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            }
            int w = Mathf.Max(16, (int)clip.width), h = Mathf.Max(16, (int)clip.height);
            if (_videoTarget == null || _videoTarget.width != w || _videoTarget.height != h)
            {
                if (_videoTarget != null) _videoTarget.Release();
                _videoTarget = new RenderTexture(w, h, 0);
            }
            _video.targetTexture = _videoTarget;
            _video.clip = clip;
            _video.Play();
            Fit(_videoTarget, w, h);
            return true;
        }
        Texture2D tex = Resources.Load<Texture2D>("Tutorial/" + id);
        if (tex == null) return false;
        Fit(tex, tex.width, tex.height);
        return true;
    }

    private void Fit(Texture tex, int w, int h)
    {
        _media.texture = tex;
        Vector2 box = _mediaBox.sizeDelta - new Vector2(12f, 12f);
        float s = Mathf.Min(box.x / w, box.y / h);
        _media.rectTransform.sizeDelta = new Vector2(w * s, h * s);
    }

    // Teclas de la página con los iconos del periférico preferido, como la leyenda de la pausa.
    private void BuildKeys(GameAction[] actions)
    {
        var old = new System.Collections.Generic.List<Transform>();
        foreach (Transform c in _keys) old.Add(c);
        foreach (Transform c in old) { c.SetParent(null, false); Destroy(c.gameObject); }
        if (actions == null || actions.Length == 0) return;
        InputMap.Device d = InputMap.Preferred;
        const float cellW = 196f;
        float x0 = -(actions.Length - 1) * cellW * 0.5f;
        for (int i = 0; i < actions.Length; i++)
        {
            GameAction a = actions[i];
            RectTransform cell = FrostUI.Node(_keys, "Tecla_" + a);
            FrostUI.Place(cell, new Vector2(0.5f, 0.5f), new Vector2(x0 + i * cellW, 0f), new Vector2(cellW - 8f, 70f));
            RectTransform g = InputGlyph.ForAction(cell, a, d, 34f);
            g.anchorMin = g.anchorMax = g.pivot = new Vector2(0.5f, 1f);
            g.anchoredPosition = Vector2.zero;
            TextMeshProUGUI t = FrostUI.Text(cell, "Accion", InputMap.ActionName(a), 15f, FrostUI.Label, TextAlignmentOptions.Bottom, FrostUI.BodyLight);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.enableAutoSizing = true;
            t.fontSizeMin = 11f;
            t.fontSizeMax = 15f;
            FrostUI.Stretch(t.rectTransform);
            t.rectTransform.offsetMax = new Vector2(0f, -38f);
        }
    }

    private void SetGroup(bool on)
    {
        _group.alpha = on ? 1f : 0f;
        _group.interactable = on;
        _group.blocksRaycasts = on;
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
        canvas.sortingOrder = 950;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        ScreenAdapter.Match(scaler);
        gameObject.AddComponent<GraphicRaycaster>();
        RectTransform root = (RectTransform)transform;

        RectTransform main = FrostUI.Stretch(FrostUI.Node(root, "Tutorial"));
        _group = main.gameObject.AddComponent<CanvasGroup>();
        Image dim = FrostUI.Img(main, "Velo", null, new Color(0.01f, 0.02f, 0.05f, 0.78f));
        FrostUI.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Image panel = FrostUI.Panel(main, "Panel", out _, 18f);
        _panel = panel.rectTransform;
        FrostUI.Place(_panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 820f));
        Transform p = panel.transform;

        _kicker = FrostUI.Kicker(p, "Antetitulo", "Cómo jugar", 17f, FrostUI.Ice);
        _kicker.alignment = TextAlignmentOptions.Center;
        FrostUI.Place(_kicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(700f, 26f));
        _title = FrostUI.Title(p, "Titulo", "", 54f, TextAlignmentOptions.Center, 0.22f);
        FrostUI.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(960f, 70f));

        // Marco de la captura o el vídeo.
        Image box = FrostUI.Img(p, "Captura", null, new Color(0.02f, 0.04f, 0.08f, 0.9f));
        _mediaBox = box.rectTransform;
        FrostUI.Place(_mediaBox, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(760f, 336f));
        Image edge = FrostUI.Img(box.transform, "Borde", FrostUI.Dashed, new Color(FrostUI.Ice.r, FrostUI.Ice.g, FrostUI.Ice.b, 0.35f));
        FrostUI.Stretch(edge.rectTransform);
        RectTransform mediaRt = FrostUI.Node(box.transform, "Imagen");
        mediaRt.anchorMin = mediaRt.anchorMax = mediaRt.pivot = new Vector2(0.5f, 0.5f);
        mediaRt.anchoredPosition = Vector2.zero;
        _media = mediaRt.gameObject.AddComponent<RawImage>();
        _media.raycastTarget = false;

        _body = FrostUI.Text(p, "Texto", "", 23f, FrostUI.TextColor, TextAlignmentOptions.Top);
        _body.textWrappingMode = TextWrappingModes.Normal;
        FrostUI.Place(_body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -490f), new Vector2(880f, 120f));

        _keys = FrostUI.Node(p, "Teclas");
        FrostUI.Place(_keys, new Vector2(0.5f, 1f), new Vector2(0f, -610f), new Vector2(980f, 70f));

        // Puntos de página.
        RectTransform dots = FrostUI.Node(p, "Paginas");
        FrostUI.Place(dots, new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(600f, 12f));
        _dots = new Image[Pages.Length];
        for (int i = 0; i < Pages.Length; i++)
        {
            Image dot = FrostUI.Img(dots, "Punto_" + (i + 1), FrostUI.Diamond(true), Color.white);
            FrostUI.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - (Pages.Length - 1) * 0.5f) * 24f, 0f), new Vector2(12f, 12f));
            _dots[i] = dot;
        }

        _prev = FrostButton.Create(p, "Anterior", "Anterior", new Vector2(240f, 60f), FrostButton.Style.Normal, 24f);
        FrostUI.Place((RectTransform)_prev.transform, new Vector2(0.5f, 0f), new Vector2(-300f, 28f), new Vector2(240f, 60f));
        _prev.button.onClick.AddListener(Prev);
        _skip = FrostButton.Create(p, "Saltar", "Saltar tutorial", new Vector2(240f, 60f), FrostButton.Style.Normal, 24f);
        FrostUI.Place((RectTransform)_skip.transform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(240f, 60f));
        _skip.button.onClick.AddListener(() => Close(true));
        _next = FrostButton.Create(p, "Siguiente", "Siguiente", new Vector2(240f, 60f), FrostButton.Style.Primary, 26f);
        FrostUI.Place((RectTransform)_next.transform, new Vector2(0.5f, 0f), new Vector2(300f, 28f), new Vector2(240f, 60f));
        _next.button.onClick.AddListener(Next);
    }
}
