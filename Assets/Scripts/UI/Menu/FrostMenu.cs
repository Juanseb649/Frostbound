using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menús de inicio (guía "Frostbound · Menús de inicio"): pantalla de título sobre las ruinas del Frostspire,
// selección de personaje (las partidas guardadas) y creación del héroe. Todo se construye por código;
// el decorado 3D (MenuRuins) y el ambiente de noche helada (NightFrostAtmosphere) van detrás.
public class FrostMenu : MonoBehaviour
{
    private enum Screen { Title, Heroes, Create, Options, Credits }

    public const string Version = "v0.3 · Prealfa";

    private static readonly Dictionary<string, string> ShortNames = new Dictionary<string, string>
    {
        { "Knight", "Caballero" }, { "Mage", "Mago" }, { "Ninja", "Shinobi" }, { "Viking", "Bárbaro" }
    };

    private Screen _screen = Screen.Title;
    private Camera _cam;
    private Vector3 _camPos, _camLook;
    private float _camFov = 38f;
    private RectTransform _canvas;
    private readonly Dictionary<Screen, CanvasGroup> _groups = new Dictionary<Screen, CanvasGroup>();
    private bool _busy;

    // Héroe sobre el pedestal.
    private Transform _pivot;
    private GameObject _actor;
    private PreviewDrag _drag;

    // Título.
    private FrostButton _play, _continue, _options, _credits, _quit;
    private GameObject _lastHeroCard;
    private TextMeshProUGUI _lastHeroName, _lastHeroDetail;

    // Selección de personaje.
    private readonly List<(FrostButton button, TextMeshProUGUI level, TextMeshProUGUI name, TextMeshProUGUI detail, Image ring, GameObject free, GameObject used)> _slots
        = new List<(FrostButton, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI, Image, GameObject, GameObject)>();
    private TextMeshProUGUI _heroCount, _heroName, _heroDetail;
    private readonly Dictionary<string, TextMeshProUGUI> _summary = new Dictionary<string, TextMeshProUGUI>();
    private FrostButton _delete, _playHero, _newHero;
    private int _selectedSlot = -1;
    private readonly SaveData[] _saves = new SaveData[SaveSystem.SlotCount];
    private CanvasGroup _confirm;
    private TextMeshProUGUI _confirmText;

    // Crear héroe.
    private readonly List<FrostButton> _classCards = new List<FrostButton>();
    private TextMeshProUGUI _classKicker, _className, _roleChip, _attrChip, _description, _colorLabel;
    private readonly List<TextMeshProUGUI> _gear = new List<TextMeshProUGUI>();
    private readonly List<(Button button, Image ring, Image halo)> _swatches = new List<(Button, Image, Image)>();
    private TMP_InputField _nameField;
    private int _classIndex, _plumageIndex, _targetSlot = -1;

    private GameDatabase Db => GameDatabase.Instance;

    // ---------- Arranque ----------

    void Start()
    {
        GameSession.Ensure();
        Time.timeScale = 1f;
        _cam = Camera.main;
        if (GetComponent<NightFrostAtmosphere>() == null) gameObject.AddComponent<NightFrostAtmosphere>();
        if (FindAnyObjectByType<MenuRuins>() == null) new GameObject("Ruinas del Frostspire").AddComponent<MenuRuins>();
        Shot(Screen.Title, true);
        BuildCanvas();
        BuildTitle();
        BuildHeroes();
        BuildCreate();
        BuildStub(Screen.Options);
        BuildStub(Screen.Credits);
        BuildConfirm();
        BuildActor();
        foreach (var g in _groups) SetGroup(g.Value, g.Key == Screen.Title);
        RefreshTitle();
        Select(_play.button);
    }

    void Update()
    {
        if (_cam != null)
        {
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3.2f);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, _camPos, k);
            Quaternion look = Quaternion.LookRotation(_camLook - _cam.transform.position);
            _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, look, k);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _camFov, k);
        }
        if (_busy || SaveSlotsPanel.IsOpen || OptionsPanel.IsOpen) return;
        if (_nameField != null && _nameField.isFocused) return;
        if (UICancel.Pressed())
        {
            if (_confirm != null && _confirm.interactable) CloseConfirm();
            else if (_screen != Screen.Title) Go(Screen.Title);
        }
    }

    // Encuadre de cámara de cada pantalla.
    private void Shot(Screen s, bool instant = false)
    {
        bool hero = s == Screen.Heroes || s == Screen.Create;
        _camPos = hero ? new Vector3(0f, 1.55f, -4.7f) : new Vector3(0f, 2.5f, -11.8f);
        _camLook = hero ? new Vector3(0f, 1.22f, 0f) : new Vector3(0f, 2.3f, 2f);
        _camFov = hero ? 34f : 38f;
        if (instant && _cam != null)
        {
            _cam.transform.position = _camPos;
            _cam.transform.LookAt(_camLook);
            _cam.fieldOfView = _camFov;
        }
    }

    // ---------- Navegación entre pantallas ----------

    private void Go(Screen to)
    {
        if (_busy || to == _screen) return;
        StartCoroutine(Swap(to));
    }

    private IEnumerator Swap(Screen to)
    {
        _busy = true;
        CanvasGroup from = _groups[_screen], next = _groups[to];
        from.interactable = false;
        next.gameObject.SetActive(true);
        next.alpha = 0f;
        Shot(to);
        if (to == Screen.Heroes) OpenHeroes();
        if (to == Screen.Create) OpenCreate();
        if (to == Screen.Title) RefreshTitle();
        _actor.SetActive(to == Screen.Heroes ? _selectedSlot >= 0 : to == Screen.Create);
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.3f;
            from.alpha = 1f - k;
            next.alpha = k;
            yield return null;
        }
        SetGroup(from, false);
        SetGroup(next, true);
        _screen = to;
        _busy = false;
        switch (to)
        {
            case Screen.Title: Select(_play.button); break;
            case Screen.Heroes: Select(_playHero.button.interactable ? _playHero.button : _newHero.button); break;
            case Screen.Create: Select(_classCards[_classIndex].button); break;
            default: Select(_groups[to].GetComponentInChildren<Button>()); break;
        }
    }

    private static void SetGroup(CanvasGroup g, bool on)
    {
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
        g.gameObject.SetActive(on);
    }

    private static void Select(Selectable s)
    {
        if (s == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    // ---------- Construcción común ----------

    private void BuildCanvas()
    {
        var go = new GameObject("Menú Frostbound", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.layer = 5;
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        _canvas = (RectTransform)go.transform;
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    private RectTransform Screenroot(Screen s)
    {
        RectTransform rt = FrostUI.Stretch(FrostUI.Node(_canvas, "Pantalla_" + s));
        _groups[s] = rt.gameObject.AddComponent<CanvasGroup>();
        return rt;
    }

    private static RectTransform TopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private static RectTransform TopRight(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private FrostButton BackButton(RectTransform root, Screen back)
    {
        FrostButton b = FrostButton.Create(root, "Volver", "Volver", new Vector2(170f, 58f), FrostButton.Style.Normal, 24f);
        TopLeft((RectTransform)b.transform, 56f, 42f, 170f, 58f);
        b.label.rectTransform.offsetMin = new Vector2(52f, 0f);
        b.label.alignment = TextAlignmentOptions.MidlineLeft;
        b.icon = FrostUI.Img(b.border.transform, "Flecha", FrostUI.Icon("Back"), Color.white);
        FrostUI.Place(b.icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(22f, 22f));
        b.icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        b.button.onClick.AddListener(() => Go(back));
        b.Refresh();
        return b;
    }

    private void Header(RectTransform root, string kicker, string title)
    {
        TextMeshProUGUI k = FrostUI.Kicker(root, "Antetitulo", kicker, 20f, FrostUI.Ice);
        k.alignment = TextAlignmentOptions.Center;
        FrostUI.Place(k.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(900f, 30f));
        TextMeshProUGUI t = FrostUI.Title(root, "Titulo", title, 86f);
        FrostUI.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(1300f, 110f));
    }

    // ---------- Título ----------

    private void BuildTitle()
    {
        RectTransform root = Screenroot(Screen.Title);
        TextMeshProUGUI title = FrostUI.Title(root, "Frostbound", "FROSTBOUND", 168f, TextAlignmentOptions.Center, 0.24f);
        title.fontStyle = FontStyles.Italic;
        FrostUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1400f, 200f));
        TextMeshProUGUI sub = FrostUI.Title(root, "Subtitulo", "LAS RUINAS DEL FROSTSPIRE", 30f, TextAlignmentOptions.Center, 0.18f);
        sub.characterSpacing = 30f;
        FrostUI.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(1200f, 44f));

        string[] labels = { "Nueva partida", "Continuar", "Opciones", "Créditos", "Salir" };
        var buttons = new FrostButton[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            FrostButton b = FrostButton.Create(root, labels[i], labels[i], new Vector2(400f, 62f), FrostButton.Style.Normal, 30f, true);
            b.iceOnFocus = true;
            TopLeft((RectTransform)b.transform, 112f, 372f + i * 84f, 400f, 62f);
            buttons[i] = b;
        }
        _play = buttons[0]; _continue = buttons[1]; _options = buttons[2]; _credits = buttons[3]; _quit = buttons[4];
        _play.button.onClick.AddListener(() => { _targetSlot = -1; Go(Screen.Create); });
        _continue.button.onClick.AddListener(() => Go(Screen.Heroes));
        _options.button.onClick.AddListener(() => OptionsPanel.Open(() => Select(_options.button)));
        _credits.button.onClick.AddListener(() => Go(Screen.Credits));
        _quit.button.onClick.AddListener(Quit);

        Image card = FrostUI.Panel(root, "UltimoHeroe", out _, 16f);
        RectTransform crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 0f);
        crt.anchoredPosition = new Vector2(-64f, 52f);
        crt.sizeDelta = new Vector2(470f, 112f);
        Image shield = FrostUI.Img(card.transform, "Escudo", FrostUI.Icon("Shield"), FrostUI.Ice);
        FrostUI.Place(shield.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(46f, 46f));
        TextMeshProUGUI k = FrostUI.Kicker(card.transform, "Etiqueta", "Último héroe", 14f);
        TopLeft(k.rectTransform, 92f, 16f, 360f, 20f);
        _lastHeroName = FrostUI.Text(card.transform, "Nombre", "", 28f, Color.white, TextAlignmentOptions.MidlineLeft, FrostUI.Display);
        TopLeft(_lastHeroName.rectTransform, 92f, 36f, 360f, 36f);
        _lastHeroDetail = FrostUI.Text(card.transform, "Detalle", "", 17f, FrostUI.Ice, TextAlignmentOptions.MidlineLeft);
        TopLeft(_lastHeroDetail.rectTransform, 92f, 74f, 370f, 24f);
        _lastHeroCard = card.gameObject;

        TextMeshProUGUI version = FrostUI.Text(root, "Version", Version, 16f, FrostUI.Secondary);
        FrostUI.Place(version.rectTransform, new Vector2(0f, 0f), new Vector2(112f, 40f), new Vector2(300f, 24f));
    }

    private void RefreshTitle()
    {
        LoadSaves();
        int last = LastSlot();
        bool any = last >= 0;
        _continue.button.interactable = any;
        _lastHeroCard.SetActive(any);
        if (any)
        {
            SaveData d = _saves[last];
            _lastHeroName.text = d.heroName;
            _lastHeroDetail.text = ClassDisplay(d) + " · Nivel " + d.level + " · " + SceneIds.Title(ZoneTravel.ZoneOf(d));
        }
    }

    private void Load(int slot)
    {
        if (GameSession.Ensure().LoadGame(slot)) StartCoroutine(FadeAndLoad(ZoneTravel.ZoneOf(GameSession.Instance.Current)));
    }

    private IEnumerator FadeAndLoad(string scene = SceneIds.Village)
    {
        _busy = true;
        foreach (var g in _groups.Values) g.interactable = false;
        if (!Application.CanStreamedLevelBeLoaded(scene)) scene = SceneIds.Village;
        LoadingScreen.Load(scene);
        yield break;
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Partidas ----------

    private void LoadSaves()
    {
        for (int i = 0; i < _saves.Length; i++) _saves[i] = SaveSystem.Exists(i) ? SaveSystem.Load(i) : null;
    }

    private int LastSlot()
    {
        int best = -1;
        System.DateTime bestTime = System.DateTime.MinValue;
        for (int i = 0; i < _saves.Length; i++)
        {
            if (_saves[i] == null) continue;
            System.DateTime.TryParse(_saves[i].savedUtc, out System.DateTime t);
            if (best < 0 || t > bestTime) { best = i; bestTime = t; }
        }
        return best;
    }

    private CharacterClass ClassOf(SaveData d) => Db != null && d != null ? Db.FindClass(d.classId) : null;

    private string ClassDisplay(SaveData d)
    {
        CharacterClass c = ClassOf(d);
        return c != null ? c.displayName : d.classId;
    }

    private static string ShortName(CharacterClass c) => c != null && ShortNames.TryGetValue(c.className, out string s) ? s : c != null ? c.displayName : "";

    private static string Place(SaveData d)
    {
        if (string.IsNullOrEmpty(d.lastBonfire)) return "El poblado";
        if (d.lastBonfire == Bonfire.CastleId) return "El castillo";
        if (d.lastBonfire.StartsWith(StairsLayout.BonfirePrefix)) return "La Escalinata " + d.lastBonfire.Substring(StairsLayout.BonfirePrefix.Length);
        if (d.lastBonfire.StartsWith(MountainLayout.BonfirePrefix)) return "Las Faldas " + d.lastBonfire.Substring(MountainLayout.BonfirePrefix.Length);
        return "Hoguera " + d.lastBonfire.Replace("hoguera_", "");
    }

    private static string Played(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        return m >= 60 ? (m / 60) + " h " + (m % 60) + " min" : m + " min";
    }

    // ---------- Selección de personaje ----------

    private void BuildHeroes()
    {
        RectTransform root = Screenroot(Screen.Heroes);
        Header(root, "Tus héroes", "Selección de personaje");
        BackButton(root, Screen.Title);
        DragArea(root);

        Image summary = FrostUI.Panel(root, "Resumen", out _, 18f);
        TopLeft(summary.rectTransform, 62f, 316f, 344f, 352f);
        TextMeshProUGUI sk = FrostUI.Kicker(summary.transform, "Titulo", "Resumen", 15f);
        TopLeft(sk.rectTransform, 26f, 26f, 300f, 22f);
        var rows = new (string key, Color color)[]
        {
            ("Vida", FrostUI.Hex("#F07A6E")), ("Maná", FrostUI.Hex("#7FB0FF")), ("Fuerza", Color.white), ("Agilidad", Color.white),
            ("", Color.white), ("Oro", FrostUI.Gold), ("Tiempo jugado", Color.white), ("Última hoguera", Color.white)
        };
        float y = 62f;
        foreach (var r in rows)
        {
            if (r.key == "")
            {
                Image line = FrostUI.Img(summary.transform, "Separador", null, FrostUI.BorderDim);
                TopLeft(line.rectTransform, 26f, y + 4f, 292f, 2f);
                y += 14f;
                continue;
            }
            TextMeshProUGUI label = FrostUI.Text(summary.transform, r.key, r.key, 19f, Color.white);
            TopLeft(label.rectTransform, 26f, y, 200f, 30f);
            TextMeshProUGUI value = FrostUI.Text(summary.transform, r.key + "_Valor", "", 19f, r.color, TextAlignmentOptions.MidlineRight, FrostUI.Display);
            TopLeft(value.rectTransform, 140f, y, 178f, 30f);
            _summary[r.key] = value;
            y += 36f;
        }

        Image list = FrostUI.Panel(root, "Heroes", out _, 18f);
        TopRight(list.rectTransform, 46f, 206f, 412f, 772f);
        _heroCount = FrostUI.Kicker(list.transform, "Cuenta", "", 15f);
        TopLeft(_heroCount.rectTransform, 26f, 26f, 360f, 22f);
        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            int slot = i;
            FrostButton card = FrostButton.Create(list.transform, "Heroe_" + i, "", new Vector2(360f, 116f), FrostButton.Style.Normal, 1f);
            TopLeft((RectTransform)card.transform, 24f, 62f + i * 138f, 360f, 116f);
            card.label.text = "";
            GameObject used = new GameObject("Ocupada", typeof(RectTransform));
            used.transform.SetParent(card.border.transform, false);
            FrostUI.Stretch((RectTransform)used.transform);
            Image ring = FrostUI.Img(used.transform, "Aro", FrostUI.Skin != null ? FrostUI.Skin.circle : null, FrostUI.Border);
            FrostUI.Place(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(72f, 72f));
            Image inner = FrostUI.Img(ring.transform, "Fondo", FrostUI.Skin != null ? FrostUI.Skin.circle : null, FrostUI.PanelSolid);
            FrostUI.Stretch(inner.rectTransform, 3f);
            TextMeshProUGUI lvl = FrostUI.Text(ring.transform, "Nivel", "", 28f, Color.white, TextAlignmentOptions.Center, FrostUI.Display);
            FrostUI.Stretch(lvl.rectTransform);
            TextMeshProUGUI name = FrostUI.Text(used.transform, "Nombre", "", 28f, Color.white, TextAlignmentOptions.MidlineLeft, FrostUI.Display);
            TopLeft(name.rectTransform, 108f, 24f, 240f, 38f);
            TextMeshProUGUI detail = FrostUI.Text(used.transform, "Detalle", "", 17f, FrostUI.Ice, TextAlignmentOptions.MidlineLeft);
            TopLeft(detail.rectTransform, 108f, 62f, 240f, 26f);

            GameObject free = new GameObject("Libre", typeof(RectTransform));
            free.transform.SetParent(card.transform, false);
            FrostUI.Stretch((RectTransform)free.transform);
            Image dash = FrostUI.Img(free.transform, "Discontinuo", FrostUI.Dashed, FrostUI.Border);
            FrostUI.Stretch(dash.rectTransform);
            Image plus = FrostUI.Img(free.transform, "Mas", FrostUI.Icon("Plus"), FrostUI.Ice);
            FrostUI.Place(plus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-92f, 0f), new Vector2(26f, 26f));
            plus.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            TextMeshProUGUI ft = FrostUI.Text(free.transform, "Texto", "Ranura libre", 26f, FrostUI.Ice, TextAlignmentOptions.Center, FrostUI.Display);
            FrostUI.Stretch(ft.rectTransform);
            ft.rectTransform.offsetMin = new Vector2(30f, 0f);

            card.button.onClick.AddListener(() => OnSlot(slot));
            _slots.Add((card, lvl, name, detail, ring, free, used));
        }

        _heroName = FrostUI.Title(root, "NombreHeroe", "", 70f, TextAlignmentOptions.Center, 0.24f);
        FrostUI.Place(_heroName.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 186f), new Vector2(1000f, 90f));
        _heroDetail = FrostUI.Text(root, "DetalleHeroe", "", 21f, Color.white, TextAlignmentOptions.Center);
        FrostUI.Place(_heroDetail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1000f, 32f));

        _delete = FrostButton.Create(root, "Eliminar", "Eliminar", new Vector2(240f, 64f), FrostButton.Style.Danger, 28f);
        FrostUI.Place((RectTransform)_delete.transform, new Vector2(0.5f, 0f), new Vector2(-342f, 46f), new Vector2(240f, 64f));
        _playHero = FrostButton.Create(root, "Jugar", "Jugar", new Vector2(350f, 70f), FrostButton.Style.Primary, 32f);
        FrostUI.Place((RectTransform)_playHero.transform, new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(350f, 70f));
        _newHero = FrostButton.Create(root, "CrearNuevo", "Crear nuevo", new Vector2(240f, 64f), FrostButton.Style.Normal, 28f);
        FrostUI.Place((RectTransform)_newHero.transform, new Vector2(0.5f, 0f), new Vector2(342f, 46f), new Vector2(240f, 64f));
        _delete.button.onClick.AddListener(AskDelete);
        _playHero.button.onClick.AddListener(() => { if (_selectedSlot >= 0) Load(_selectedSlot); });
        _newHero.button.onClick.AddListener(() => { _targetSlot = FirstFree(); Go(Screen.Create); });
    }

    private void OpenHeroes()
    {
        LoadSaves();
        if (_selectedSlot < 0 || _saves[_selectedSlot] == null) _selectedSlot = LastSlot();
        RefreshHeroes();
    }

    private void RefreshHeroes()
    {
        int used = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            SaveData d = _saves[i];
            bool has = d != null;
            if (has) used++;
            s.used.SetActive(has);
            s.free.SetActive(!has);
            s.button.border.enabled = has;
            s.button.fill.enabled = has;
            s.button.selectedLook = has && i == _selectedSlot;
            if (has)
            {
                s.level.text = d.level.ToString();
                s.name.text = d.heroName;
                s.detail.text = ShortName(ClassOf(d)) + " · " + SceneIds.Title(ZoneTravel.ZoneOf(d));
                s.ring.color = i == _selectedSlot ? FrostUI.Ice : FrostUI.Border;
            }
            s.button.Refresh();
        }
        _heroCount.text = ("Héroes · " + used + " de " + SaveSystem.SlotCount).ToUpperInvariant();

        SaveData sel = _selectedSlot >= 0 ? _saves[_selectedSlot] : null;
        _delete.button.interactable = sel != null;
        _playHero.button.interactable = sel != null;
        _newHero.button.interactable = FirstFree() >= 0;
        _heroName.text = sel != null ? sel.heroName : "";
        _heroDetail.text = sel != null ? ClassDisplay(sel) + " · Nivel " + sel.level + " · " + SceneIds.Title(ZoneTravel.ZoneOf(sel)) : "No tienes héroes todavía";
        Summary(sel);
        if (_actor != null) _actor.SetActive(sel != null && _screen != Screen.Create);
        if (sel != null)
        {
            CharacterClass cls = ClassOf(sel);
            Dress(cls, Db != null && Db.palette != null ? Db.palette.ColorOf(sel.plumageId) : Color.white);
        }
    }

    private void Summary(SaveData d)
    {
        CharacterClass c = ClassOf(d);
        string Dash = "—";
        if (d == null)
        {
            foreach (var v in _summary.Values) v.text = Dash;
            return;
        }
        int hp = c != null ? c.baseHealth + d.allocated.health : d.allocated.health;
        int mp = c != null ? c.baseMana + d.allocated.mana : d.allocated.mana;
        float maxHp = 40f + hp * 8f, maxMp = 20f + mp * 5f;
        _summary["Vida"].text = Mathf.CeilToInt(d.health > 0f ? d.health : maxHp).ToString();
        _summary["Maná"].text = Mathf.CeilToInt(d.mana >= 0f ? d.mana : maxMp).ToString();
        _summary["Fuerza"].text = ((c != null ? c.baseStrength : 0) + d.allocated.strength).ToString();
        _summary["Agilidad"].text = ((c != null ? c.baseAgility : 0) + d.allocated.agility).ToString();
        _summary["Oro"].text = (d.coins + d.stashCoins).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-ES"));
        _summary["Tiempo jugado"].text = Played(d.playSeconds);
        _summary["Última hoguera"].text = Place(d);
    }

    private void OnSlot(int slot)
    {
        if (_saves[slot] == null) { _targetSlot = slot; Go(Screen.Create); return; }
        _selectedSlot = slot;
        RefreshHeroes();
    }

    private int FirstFree()
    {
        for (int i = 0; i < SaveSystem.SlotCount; i++) if (!SaveSystem.Exists(i)) return i;
        return -1;
    }

    // Confirmación al eliminar (no se puede deshacer).
    private void BuildConfirm()
    {
        RectTransform root = FrostUI.Stretch(FrostUI.Node(_canvas, "Confirmar"));
        _confirm = root.gameObject.AddComponent<CanvasGroup>();
        Image dim = FrostUI.Img(root, "Velo", null, new Color(0.01f, 0.02f, 0.05f, 0.78f));
        FrostUI.Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        Image box = FrostUI.Panel(root, "Caja", out _, 18f, FrostUI.Danger);
        FrostUI.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 260f));
        box.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        TextMeshProUGUI title = FrostUI.Title(box.transform, "Titulo", "¿Eliminar héroe?", 46f);
        TopLeft(title.rectTransform, 0f, 26f, 620f, 60f);
        _confirmText = FrostUI.Text(box.transform, "Texto", "", 20f, FrostUI.Secondary, TextAlignmentOptions.Center);
        _confirmText.textWrappingMode = TextWrappingModes.Normal;
        TopLeft(_confirmText.rectTransform, 40f, 92f, 540f, 60f);
        FrostButton cancel = FrostButton.Create(box.transform, "Cancelar", "Cancelar", new Vector2(220f, 60f), FrostButton.Style.Normal, 26f);
        TopLeft((RectTransform)cancel.transform, 80f, 172f, 220f, 60f);
        cancel.button.onClick.AddListener(CloseConfirm);
        FrostButton ok = FrostButton.Create(box.transform, "Eliminar", "Eliminar", new Vector2(220f, 60f), FrostButton.Style.Danger, 26f);
        TopLeft((RectTransform)ok.transform, 320f, 172f, 220f, 60f);
        ok.button.onClick.AddListener(ConfirmDelete);
        SetGroup(_confirm, false);
    }

    private void AskDelete()
    {
        if (_selectedSlot < 0 || _saves[_selectedSlot] == null) return;
        _confirmText.text = _saves[_selectedSlot].heroName + " y todo su progreso se perderán. Esta acción no se puede deshacer.";
        SetGroup(_confirm, true);
        _groups[Screen.Heroes].interactable = false;
        Select(_confirm.GetComponentInChildren<Button>());
    }

    private void CloseConfirm()
    {
        SetGroup(_confirm, false);
        _groups[Screen.Heroes].interactable = true;
        Select(_delete.button.interactable ? _delete.button : _newHero.button);
    }

    private void ConfirmDelete()
    {
        SaveSystem.Delete(_selectedSlot);
        _selectedSlot = -1;
        LoadSaves();
        _selectedSlot = LastSlot();
        CloseConfirm();
        RefreshHeroes();
    }

    // ---------- Crear héroe ----------

    private void BuildCreate()
    {
        RectTransform root = Screenroot(Screen.Create);
        Header(root, "Nueva partida", "Crea tu héroe");
        BackButton(root, Screen.Title);
        DragArea(root);

        List<CharacterClass> classes = Db != null ? Db.classes : new List<CharacterClass>();
        for (int i = 0; i < classes.Count; i++)
        {
            int index = i;
            CharacterClass c = classes[i];
            FrostButton card = FrostButton.Create(root, "Clase_" + c.className, "", new Vector2(432f, 114f), FrostButton.Style.Normal, 1f);
            TopLeft((RectTransform)card.transform, 60f, 222f + i * 134f, 432f, 114f);
            Image icon = FrostUI.Img(card.border.transform, "Icono", FrostUI.Icon(c.className), FrostUI.Ice);
            FrostUI.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(62f, 62f));
            TextMeshProUGUI n = FrostUI.Text(card.border.transform, "Nombre", ShortName(c), 32f, Color.white, TextAlignmentOptions.MidlineLeft, FrostUI.Display);
            TopLeft(n.rectTransform, 106f, 22f, 300f, 40f);
            TextMeshProUGUI r = FrostUI.Text(card.border.transform, "Rol", c.role, 18f, FrostUI.Ice, TextAlignmentOptions.MidlineLeft);
            TopLeft(r.rectTransform, 106f, 66f, 310f, 26f);
            card.button.onClick.AddListener(() => SelectClass(index));
            _classCards.Add(card);
        }

        Image panel = FrostUI.Panel(root, "Detalle", out _, 18f);
        TopRight(panel.rectTransform, 46f, 190f, 452f, 846f);
        Transform p = panel.transform;
        _classKicker = FrostUI.Kicker(p, "Clase", "", 16f);
        TopLeft(_classKicker.rectTransform, 30f, 30f, 400f, 24f);
        _className = FrostUI.Title(p, "Nombre", "", 50f, TextAlignmentOptions.MidlineLeft);
        TopLeft(_className.rectTransform, 28f, 58f, 410f, 70f);
        _roleChip = Chip(p, "Rol", 30f, 140f, FrostUI.Ice, FrostUI.Hex("#13284A"), FrostUI.Border);
        _attrChip = Chip(p, "Atributo", 30f, 140f, FrostUI.Gold, FrostUI.Hex("#3A2E14"), FrostUI.Hex("#B88A2E"));
        _description = FrostUI.Text(p, "Descripcion", "", 18f, Color.white, TextAlignmentOptions.TopLeft, FrostUI.BodyLight);
        _description.textWrappingMode = TextWrappingModes.Normal;
        TopLeft(_description.rectTransform, 30f, 194f, 396f, 110f);
        TextMeshProUGUI gk = FrostUI.Kicker(p, "Equipo", "Equipo inicial", 15f);
        TopLeft(gk.rectTransform, 30f, 310f, 400f, 22f);
        for (int i = 0; i < 4; i++)
        {
            float gx = 30f + (i % 2) * 204f, gy = 342f + (i / 2) * 30f;
            Image dot = FrostUI.Img(p, "Rombo", FrostUI.Diamond(true), Color.white);
            TopLeft(dot.rectTransform, gx, gy + 8f, 12f, 12f);
            TextMeshProUGUI g = FrostUI.Text(p, "Pieza", "", 17f, Color.white);
            TopLeft(g.rectTransform, gx + 20f, gy, 184f, 28f);
            _gear.Add(g);
        }
        _colorLabel = FrostUI.Kicker(p, "Color", "", 15f);
        TopLeft(_colorLabel.rectTransform, 30f, 420f, 400f, 22f);
        PlumagePalette palette = Db != null ? Db.palette : null;
        int colors = palette != null ? Mathf.Min(8, palette.entries.Count) : 0;
        for (int i = 0; i < colors; i++)
        {
            int index = i;
            RectTransform sw = FrostUI.Node(p, "Color_" + i);
            TopLeft(sw, 30f + i * 50f, 452f, 44f, 44f);
            Image halo = FrostUI.Img(sw, "Halo", FrostUI.Glow, FrostUI.Ice);
            FrostUI.Stretch(halo.rectTransform, -10f);
            Image ring = FrostUI.Img(sw, "Aro", FrostUI.Skin != null ? FrostUI.Skin.circle : null, FrostUI.BorderDim);
            FrostUI.Stretch(ring.rectTransform);
            ring.raycastTarget = true;
            Image fill = FrostUI.Img(ring.transform, "Color", FrostUI.Skin != null ? FrostUI.Skin.circle : null, palette.entries[i].color);
            FrostUI.Stretch(fill.rectTransform, 4f);
            Button b = sw.gameObject.AddComponent<Button>();
            b.targetGraphic = ring;
            b.onClick.AddListener(() => SelectPlumage(index));
            _swatches.Add((b, ring, halo));
        }
        TextMeshProUGUI nk = FrostUI.Kicker(p, "NombreEtiqueta", "Nombre del héroe", 15f);
        TopLeft(nk.rectTransform, 30f, 520f, 400f, 22f);
        _nameField = InputField(p, 30f, 552f, 392f, 58f);

        FrostButton start = FrostButton.Create(p, "Comenzar", "Comenzar aventura", new Vector2(392f, 70f), FrostButton.Style.Primary, 30f);
        TopLeft((RectTransform)start.transform, 30f, 748f, 392f, 70f);
        start.button.onClick.AddListener(StartAdventure);

        RectTransform hint = FrostUI.Node(root, "Pista");
        FrostUI.Place(hint, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(520f, 36f));
        Image rot = FrostUI.Img(hint, "Girar", FrostUI.Icon("Rotate"), Color.white);
        FrostUI.Place(rot.rectTransform, new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(24f, 24f));
        TextMeshProUGUI ht = FrostUI.Text(hint, "Texto", "Arrastra para girar al héroe", 20f, Color.white, TextAlignmentOptions.MidlineLeft);
        TopLeft(ht.rectTransform, 120f, 4f, 400f, 30f);
    }

    private TextMeshProUGUI Chip(Transform parent, string name, float x, float y, Color text, Color fill, Color border)
    {
        Image b = FrostUI.Panel(parent, name, out _, 10f, border, fill);
        TopLeft(b.rectTransform, x, y, 160f, 38f);
        TextMeshProUGUI t = FrostUI.Text(b.transform, "Texto", "", 16f, text, TextAlignmentOptions.Center);
        FrostUI.Stretch(t.rectTransform);
        return t;
    }

    private static void FitChip(TextMeshProUGUI chip, float x)
    {
        var rt = (RectTransform)chip.transform.parent;
        float w = chip.GetPreferredValues(chip.text).x + 30f;
        rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
        rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
    }

    private TMP_InputField InputField(Transform parent, float x, float y, float w, float h)
    {
        Image frame = FrostUI.Panel(parent, "CampoNombre", out Image fill, 12f, FrostUI.Border, new Color(0.01f, 0.03f, 0.06f, 0.95f));
        TopLeft(frame.rectTransform, x, y, w, h);
        frame.raycastTarget = true;
        RectTransform area = FrostUI.Stretch(FrostUI.Node(frame.transform, "Area"), 0f);
        area.offsetMin = new Vector2(20f, 6f);
        area.offsetMax = new Vector2(-20f, -6f);
        area.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI placeholder = FrostUI.Text(area, "Placeholder", "Escribe un nombre", 21f, FrostUI.Disabled, TextAlignmentOptions.MidlineLeft, FrostUI.Display);
        FrostUI.Stretch(placeholder.rectTransform);
        TextMeshProUGUI text = FrostUI.Text(area, "Text", "", 21f, Color.white, TextAlignmentOptions.MidlineLeft, FrostUI.Display);
        FrostUI.Stretch(text.rectTransform);
        var field = frame.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = area;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.characterLimit = 18;
        field.targetGraphic = frame;
        field.caretColor = FrostUI.Ice;
        field.selectionColor = new Color(0.36f, 0.6f, 0.85f, 0.5f);
        field.onSelect.AddListener(_ => frame.color = FrostUI.Ice);
        field.onDeselect.AddListener(_ => frame.color = FrostUI.Border);
        return field;
    }

    private void OpenCreate()
    {
        if (_targetSlot < 0) _targetSlot = FirstFree();
        if (_nameField != null) _nameField.text = "";
        SelectClass(_classIndex);
    }

    private void SelectClass(int index)
    {
        List<CharacterClass> classes = Db != null ? Db.classes : null;
        if (classes == null || index < 0 || index >= classes.Count) return;
        _classIndex = index;
        CharacterClass c = classes[index];
        for (int i = 0; i < _classCards.Count; i++)
        {
            _classCards[i].selectedLook = i == index;
            _classCards[i].Refresh();
        }
        _classKicker.text = c.className.ToUpperInvariant();
        _className.text = c.displayName;
        _roleChip.text = c.role;
        _attrChip.text = "Atributo principal: " + c.primaryAttribute;
        Canvas.ForceUpdateCanvases();
        FitChip(_roleChip, 30f);
        FitChip(_attrChip, 30f + ((RectTransform)_roleChip.transform.parent).sizeDelta.x + 10f);
        _description.text = c.description;
        for (int i = 0; i < _gear.Count; i++)
        {
            bool has = c.startingGear != null && i < c.startingGear.Length;
            _gear[i].text = has ? c.startingGear[i] : "";
        }
        PlumagePalette palette = Db.palette;
        _plumageIndex = Mathf.Max(0, palette.IndexOf(c.defaultPlumageId));
        SelectPlumage(_plumageIndex);
        if (_drag != null) _drag.ResetView();
    }

    private void SelectPlumage(int index)
    {
        PlumagePalette palette = Db.palette;
        if (index < 0 || index >= palette.entries.Count) return;
        _plumageIndex = index;
        for (int i = 0; i < _swatches.Count; i++)
        {
            bool on = i == index;
            _swatches[i].ring.color = on ? Color.white : FrostUI.BorderDim;
            _swatches[i].halo.enabled = on;
            _swatches[i].halo.color = new Color(palette.entries[i].color.r, palette.entries[i].color.g, palette.entries[i].color.b, 0.8f);
        }
        _colorLabel.text = ("Color del plumaje · " + palette.entries[index].displayName).ToUpperInvariant();
        Dress(Db.classes[_classIndex], palette.entries[index].color);
    }

    public void StartAdventure()
    {
        CharacterClass cls = Db.classes[_classIndex];
        string heroName = _nameField != null ? _nameField.text.Trim() : "";
        if (string.IsNullOrEmpty(heroName)) heroName = cls.displayName;
        string plumage = Db.palette.entries[_plumageIndex].id;
        int slot = _targetSlot >= 0 && !SaveSystem.Exists(_targetSlot) ? _targetSlot : FirstFree();
        if (slot >= 0)
        {
            GameSession.Ensure().StartNewGame(slot, cls, plumage, heroName);
            StartCoroutine(FadeAndLoad());
            return;
        }
        // Las cuatro ranuras están llenas: hay que elegir cuál sobrescribir.
        SaveSlotsPanel.Open(SaveSlotsPanel.Mode.New, s =>
        {
            GameSession.Ensure().StartNewGame(s, cls, plumage, heroName);
            StartCoroutine(FadeAndLoad());
        }, null);
    }

    // ---------- Accesos para las comprobaciones del editor ----------

    public void ShowCreate(int slot = -1) { _targetSlot = slot; Go(Screen.Create); }
    public void ShowHeroes() => Go(Screen.Heroes);
    public void LoadSlot(int slot) => Load(slot);
    public void AskDeleteSlot(int slot) { LoadSaves(); _selectedSlot = slot; RefreshHeroes(); AskDelete(); }
    public string State()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("pantalla=").Append(_screen);
        sb.Append(" fuente=").Append(FrostUI.Display != null ? FrostUI.Display.name : "null");
        sb.Append(" heroe=").Append(_actor != null && _actor.activeSelf ? _actor.name : "oculto");
        sb.Append(" clases=").Append(_classCards.Count).Append(" colores=").Append(_swatches.Count);
        sb.Append(" ultimo=").Append(_lastHeroCard.activeSelf ? _lastHeroName.text : "-");
        sb.Append(" heroes='").Append(_heroCount.text).Append("' nombre='").Append(_heroName.text).Append("'");
        sb.Append(" vida=").Append(_summary["Vida"].text).Append(" oro=").Append(_summary["Oro"].text);
        sb.Append(" clase='").Append(_className.text).Append("' chip='").Append(_attrChip.text).Append("'");
        sb.Append(" niebla=").Append(RenderSettings.fog).Append(" luz=").Append(RenderSettings.sun != null ? RenderSettings.sun.name : "-");
        sb.Append(" volumen=").Append(FindAnyObjectByType<UnityEngine.Rendering.Volume>() != null);
        return sb.ToString();
    }

    public bool ConfirmOpen => _confirm != null && _confirm.interactable;

    // ---------- Opciones y créditos ----------

    private void BuildStub(Screen s)
    {
        RectTransform root = Screenroot(s);
        Header(root, s == Screen.Options ? "Ajustes" : "Frostbound", s == Screen.Options ? "Opciones" : "Créditos");
        BackButton(root, Screen.Title);
        Image box = FrostUI.Panel(root, "Caja", out _, 18f);
        FrostUI.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(760f, 420f));
        box.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        if (s == Screen.Credits)
        {
            TextMeshProUGUI t = FrostUI.Text(box.transform, "Texto",
                "<size=30><font=\"" + (FrostUI.Display != null ? FrostUI.Display.name : "") + "\">Frostbound</font></size>\n\nDiseño y desarrollo: Sebastián\nProgramación asistida: Claude\n\nTipografías: Lilita One, Nunito y Cinzel (SIL Open Font License)\nHecho con Unity",
                20f, Color.white, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.Normal;
            FrostUI.Stretch(t.rectTransform, 40f);
            return;
        }
        TextMeshProUGUI vl = FrostUI.Text(box.transform, "Volumen", "Volumen general", 22f, Color.white);
        TopLeft(vl.rectTransform, 50f, 60f, 300f, 34f);
        Slider slider = MakeSlider(box.transform, 340f, 66f, 360f);
        slider.value = GameSettings.Volume;
        slider.onValueChanged.AddListener(v => { GameSettings.Volume = v; GameSettings.Save(); });
        TextMeshProUGUI fl = FrostUI.Text(box.transform, "Pantalla", "Pantalla completa", 22f, Color.white);
        TopLeft(fl.rectTransform, 50f, 140f, 300f, 34f);
        FrostButton full = FrostButton.Create(box.transform, "PantallaCompleta", GameSettings.Fullscreen ? "Sí" : "No", new Vector2(160f, 54f), FrostButton.Style.Normal, 24f);
        TopLeft((RectTransform)full.transform, 340f, 130f, 160f, 54f);
        full.button.onClick.AddListener(() =>
        {
            GameSettings.Fullscreen = !GameSettings.Fullscreen;
            GameSettings.Save();
            full.label.text = GameSettings.Fullscreen ? "Sí" : "No";
        });
    }

    private Slider MakeSlider(Transform parent, float x, float y, float w)
    {
        RectTransform root = FrostUI.Node(parent, "Deslizador");
        TopLeft(root, x, y, w, 24f);
        Image bg = FrostUI.Img(root, "Fondo", FrostUI.Skin != null ? FrostUI.Skin.pill : null, FrostUI.PanelSolid);
        FrostUI.Stretch(bg.rectTransform);
        RectTransform fillArea = FrostUI.Stretch(FrostUI.Node(root, "Area"), 3f);
        Image fill = FrostUI.Img(fillArea, "Relleno", FrostUI.Skin != null ? FrostUI.Skin.pill : null, FrostUI.Ice);
        RectTransform handleArea = FrostUI.Stretch(FrostUI.Node(root, "Mango"), 0f);
        Image handle = FrostUI.Img(handleArea, "Bola", FrostUI.Skin != null ? FrostUI.Skin.circle : null, Color.white);
        handle.rectTransform.sizeDelta = new Vector2(30f, 30f);
        handle.raycastTarget = true;
        var s = root.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        s.minValue = 0f;
        s.maxValue = 1f;
        return s;
    }

    // ---------- Héroe en el pedestal ----------

    private void BuildActor()
    {
        _pivot = new GameObject("Héroe en el pedestal").transform;
        _pivot.position = MenuRuins.PedestalTop;
        _pivot.rotation = Quaternion.Euler(0f, 180f, 0f);
        GameObject prefab = Db != null ? Db.penguinPrefab : null;
        _actor = prefab != null ? Instantiate(prefab, _pivot, false) : new GameObject("Sin pingüino");
        _actor.transform.localPosition = Vector3.zero;
        _actor.transform.localRotation = Quaternion.identity;
        foreach (MonoBehaviour m in _actor.GetComponentsInChildren<MonoBehaviour>())
            if (m is PlayerController || m is PlayerCombat) m.enabled = false;
        _actor.SetActive(false);
    }

    private void Dress(CharacterClass cls, Color plumage)
    {
        if (_actor == null) return;
        var outfit = _actor.GetComponent<PenguinOutfit>();
        if (outfit != null)
        {
            outfit.useClassOutfit = false;
            outfit.UnequipAll();
            // Igual que en el juego: cada pieza inicial de armadura pone su prenda; si no hay, el traje de la clase.
            bool any = false;
            if (cls != null && cls.startingItems != null)
                foreach (ItemDefinition item in cls.startingItems)
                    if (item != null && item.IsEquippable && item.weaponModel == null && item.outfit != null) { outfit.Equip(item.outfit); any = true; }
            if (!any && cls != null && cls.startingOutfit != null)
                foreach (OutfitItem item in cls.startingOutfit) outfit.Equip(item);
        }
        var look = _actor.GetComponent<PenguinAppearance>();
        if (look != null) look.SetPlumage(plumage);
        ArmActor(cls);
    }

    // Las armas de la clase van en las aletas igual que en el juego (WeaponHolder), no las piezas fijas del traje.
    private void ArmActor(CharacterClass cls)
    {
        foreach (SkinnedMeshRenderer r in _actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (r.name.Contains("_Weapon_") || r.name.Contains("_Offhand_")) r.enabled = false;
        PenguinRigAnimator rig = _actor.GetComponentInChildren<PenguinRigAnimator>();
        if (rig == null) return;
        WeaponHolder holder = rig.GetComponent<WeaponHolder>();
        if (holder == null) holder = rig.gameObject.AddComponent<WeaponHolder>();
        if (_carry != null) _carry.Release();
        _carry = null;
        ItemDefinition main = null, off = null;
        if (cls != null && cls.startingItems != null)
            foreach (ItemDefinition item in cls.startingItems)
            {
                if (item == null || !item.IsEquippable) continue;
                if (item.equipSlot == EquipSlot.Weapon && main == null) main = item;
                else if (item.equipSlot == EquipSlot.Offhand && off == null && item.weaponModel != null) off = item;
            }
        holder.Show(main, off);
        // El shinobi lleva la katana envainada a la espalda, como en el juego.
        _carry = WeaponCarry.For(cls, main, holder, rig);
    }

    private WeaponCarry _carry;

    // Zona central invisible: arrastrar gira al héroe.
    private void DragArea(RectTransform root)
    {
        Image area = FrostUI.Img(root, "ZonaGiro", null, new Color(0f, 0f, 0f, 0f));
        FrostUI.Place(area.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(760f, 640f));
        area.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        area.raycastTarget = true;
        area.transform.SetAsFirstSibling();
        var drag = area.gameObject.AddComponent<PreviewDrag>();
        drag.idleAmplitude = 12f;
        _drags.Add(drag);
    }

    private readonly List<PreviewDrag> _drags = new List<PreviewDrag>();

    void LateUpdate()
    {
        if (_pivot == null) return;
        foreach (PreviewDrag d in _drags)
        {
            bool active = d.isActiveAndEnabled;
            d.target = active ? _pivot : null;
            if (active) _drag = d;
        }
    }
}
