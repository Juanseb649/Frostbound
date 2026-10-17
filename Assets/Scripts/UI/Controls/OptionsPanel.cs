using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Configuración (menú principal y pausa): pestaña General (volumen y pantalla) y pestaña Control.
// Control → Periférico preferido: el jugador 1 elige teclado y ratón o mando de Xbox, y debajo reasigna
// las teclas o los botones de ese periférico. Los iconos del mando son los de Xbox, como en Minecraft Dungeons.
public class OptionsPanel : MonoBehaviour
{
    public static bool IsOpen => _instance != null;
    private static OptionsPanel _instance;

    private enum Tab { General, Control }

    private Action _onClose;
    private Tab _tab = Tab.Control;
    private RectTransform _content, _general, _control, _grid;
    private FrostButton _tabGeneral, _tabControl, _kbCard, _padCard, _reset, _back;
    private TextMeshProUGUI _status, _gridTitle, _help, _volume;
    private readonly List<(GameAction action, FrostButton button, RectTransform glyphSlot)> _rows = new List<(GameAction, FrostButton, RectTransform)>();

    // Reasignación en curso.
    private GameAction? _capturing;
    private bool _waitRelease;
    private float _captureUntil;

    private const float CaptureSeconds = 6f;

    public static OptionsPanel Open(Action onClose = null)
    {
        if (_instance != null) return _instance;
        var go = new GameObject("Configuracion", typeof(RectTransform));
        _instance = go.AddComponent<OptionsPanel>();
        _instance._onClose = onClose;
        GameplayInput.Block();
        return _instance;
    }

    // Mientras está abierta, los botones de detrás no se pueden elegir (la cruceta no se escapa del panel).
    private readonly List<Selectable> _muted = new List<Selectable>();
    private RectTransform _panel;

    void Awake()
    {
        foreach (Selectable sel in Selectable.allSelectablesArray)
            if (sel != null && sel.interactable) { sel.interactable = false; _muted.Add(sel); }
        Build();
        ShowTab(Tab.Control);
        AudioManager.PlayUI(AudioId.UI_OpenMenu, 0.6f);
        InputMap.Changed += OnMapChanged;
    }

    void OnDestroy()
    {
        InputMap.Changed -= OnMapChanged;
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
        foreach (Selectable sel in _muted) if (sel != null) sel.interactable = true;
        _muted.Clear();
        if (_instance == this) { _instance = null; GameplayInput.Unblock(); }
    }

    public void Close()
    {
        Action cb = _onClose;
        UIDissolve.Ghost(_panel);
        foreach (Selectable sel in _muted) if (sel != null) sel.interactable = true;
        _muted.Clear();
        Destroy(gameObject);
        _instance = null;
        GameplayInput.Unblock();
        cb?.Invoke();
    }

    private void OnMapChanged()
    {
        if (_capturing == null) RefreshControl();
    }

    // ---------- Construcción ----------

    private void Build()
    {
        gameObject.layer = 5;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        ScreenAdapter.Match(scaler);
        gameObject.AddComponent<GraphicRaycaster>();
        var root = (RectTransform)transform;

        Image dim = FrostUI.Img(root, "Velo", null, new Color(0.01f, 0.02f, 0.05f, 0.82f));
        FrostUI.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Image panel = FrostUI.Panel(root, "Panel", out _, 18f);
        _panel = panel.rectTransform;
        FrostUI.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440f, 940f));
        panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        Transform p = panel.transform;

        TextMeshProUGUI kicker = FrostUI.Kicker(p, "Antetitulo", "Ajustes", 17f, FrostUI.Ice);
        kicker.alignment = TextAlignmentOptions.Center;
        FrostUI.Place(kicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(600f, 26f));
        kicker.rectTransform.pivot = new Vector2(0.5f, 1f);
        TextMeshProUGUI title = FrostUI.Title(p, "Titulo", "CONFIGURACIÓN", 62f, TextAlignmentOptions.Center, 0.22f);
        FrostUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(900f, 76f));
        title.rectTransform.pivot = new Vector2(0.5f, 1f);

        _tabGeneral = MakeButton(p, "General", new Vector2(220f, 52f), 24f);
        FrostUI.Place((RectTransform)_tabGeneral.transform, new Vector2(0.5f, 1f), new Vector2(-120f, -140f), new Vector2(220f, 52f));
        ((RectTransform)_tabGeneral.transform).pivot = new Vector2(0.5f, 1f);
        _tabGeneral.button.onClick.AddListener(() => ShowTab(Tab.General));
        _tabControl = MakeButton(p, "Control", new Vector2(220f, 52f), 24f);
        FrostUI.Place((RectTransform)_tabControl.transform, new Vector2(0.5f, 1f), new Vector2(120f, -140f), new Vector2(220f, 52f));
        ((RectTransform)_tabControl.transform).pivot = new Vector2(0.5f, 1f);
        _tabControl.button.onClick.AddListener(() => ShowTab(Tab.Control));

        _content = FrostUI.Node(p, "Contenido");
        _content.anchorMin = new Vector2(0f, 0f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.offsetMin = new Vector2(50f, 110f);
        _content.offsetMax = new Vector2(-50f, -210f);

        BuildGeneral();
        BuildControl();

        _help = FrostUI.Text(p, "Ayuda", "", 17f, FrostUI.Label, TextAlignmentOptions.MidlineLeft, FrostUI.BodyLight);
        FrostUI.Place(_help.rectTransform, new Vector2(0f, 0f), new Vector2(50f, 40f), new Vector2(860f, 50f));
        _help.textWrappingMode = TextWrappingModes.Normal;

        _reset = MakeButton(p, "Restablecer", new Vector2(230f, 58f), 24f);
        FrostUI.Place((RectTransform)_reset.transform, new Vector2(1f, 0f), new Vector2(-290f, 32f), new Vector2(230f, 58f));
        _reset.button.onClick.AddListener(() => { InputMap.ResetDefaults(InputMap.Preferred); SetHelp("Asignación restablecida.", FrostUI.Ice); });
        _back = MakeButton(p, "Volver", new Vector2(220f, 58f), 24f, FrostButton.Style.Primary);
        FrostUI.Place((RectTransform)_back.transform, new Vector2(1f, 0f), new Vector2(-50f, 32f), new Vector2(220f, 58f));
        _back.button.onClick.AddListener(Close);
    }

    private static FrostButton MakeButton(Transform parent, string text, Vector2 size, float font, FrostButton.Style style = FrostButton.Style.Normal)
    {
        return FrostButton.Create(parent, text, text, size, style, font);
    }

    // General: volumen, modo de pantalla, resolución (de 1280×720 a 4K, con las ultrawide 21:9 y 32:9) y HUD en ultrawide.
    private List<DisplaySettings.Res> _resList;
    private int _resIndex, _modeIndex;
    private TextMeshProUGUI _resText, _modeText, _hudText;

    private void BuildGeneral()
    {
        _general = FrostUI.Stretch(FrostUI.Node(_content, "General"));
        _resList = DisplaySettings.Resolutions();
        DisplaySettings.Res cur = DisplaySettings.Current;
        _resIndex = Mathf.Max(0, _resList.FindIndex(r => r.w == cur.w && r.h == cur.h));
        _modeIndex = Mathf.Max(0, System.Array.IndexOf(DisplaySettings.Modes, DisplaySettings.Mode));

        _volume = Selector(_general, "Volumen general", 0, () => ChangeVolume(-0.1f), () => ChangeVolume(0.1f));
        _modeText = Selector(_general, "Modo de pantalla", 1, () => { _modeIndex = (_modeIndex + DisplaySettings.Modes.Length - 1) % DisplaySettings.Modes.Length; RefreshGeneral(); },
                                                             () => { _modeIndex = (_modeIndex + 1) % DisplaySettings.Modes.Length; RefreshGeneral(); });
        _resText = Selector(_general, "Resolución", 2, () => { _resIndex = Mathf.Max(0, _resIndex - 1); RefreshGeneral(); },
                                                       () => { _resIndex = Mathf.Min(_resList.Count - 1, _resIndex + 1); RefreshGeneral(); });
        _hudText = Selector(_general, "HUD en ultrawide", 3, () => { DisplaySettings.HudCentered = !DisplaySettings.HudCentered; RefreshGeneral(); },
                                                             () => { DisplaySettings.HudCentered = !DisplaySettings.HudCentered; RefreshGeneral(); });

        FrostButton apply = MakeButton(_general, "Aplicar pantalla", new Vector2(300f, 58f), 24f, FrostButton.Style.Primary);
        FrostUI.Place((RectTransform)apply.transform, new Vector2(0f, 1f), new Vector2(560f, -4f * 90f - 40f), new Vector2(300f, 58f));
        apply.button.onClick.AddListener(() =>
        {
            DisplaySettings.Res r = _resList[_resIndex];
            DisplaySettings.Apply(r, DisplaySettings.Modes[_modeIndex]);
            SetHelp("Pantalla: " + r + " (" + r.AspectName + ") · " + DisplaySettings.ModeName(DisplaySettings.Modes[_modeIndex]) + "."
                    + (Application.isEditor ? " En el editor no cambia; se aplica en el juego compilado." : ""), FrostUI.Ice);
        });
        RefreshGeneral();
    }

    // Fila con nombre y un valor entre dos flechas (se maneja con ratón, teclado o mando).
    private TextMeshProUGUI Selector(Transform parent, string label, int row, Action left, Action right)
    {
        float y = -row * 90f - 40f;
        TextMeshProUGUI l = FrostUI.Text(parent, label, label, 26f, Color.white);
        FrostUI.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(160f, y), new Vector2(380f, 54f));
        FrostButton minus = MakeButton(parent, "◄ " + label, new Vector2(64f, 54f), 24f);
        minus.label.text = "<";
        FrostUI.Place((RectTransform)minus.transform, new Vector2(0f, 1f), new Vector2(560f, y), new Vector2(64f, 54f));
        TextMeshProUGUI value = FrostUI.Text(parent, label + "_Valor", "", 24f, FrostUI.Ice, TextAlignmentOptions.Center);
        FrostUI.Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(634f, y), new Vector2(420f, 54f));
        FrostButton plus = MakeButton(parent, "► " + label, new Vector2(64f, 54f), 24f);
        plus.label.text = ">";
        FrostUI.Place((RectTransform)plus.transform, new Vector2(0f, 1f), new Vector2(1064f, y), new Vector2(64f, 54f));
        minus.button.onClick.AddListener(() => left());
        plus.button.onClick.AddListener(() => right());
        return value;
    }

    private void ChangeVolume(float delta)
    {
        GameSettings.Volume = Mathf.Clamp01(Mathf.Round((GameSettings.Volume + delta) * 10f) / 10f);
        GameSettings.Save();
        RefreshGeneral();
    }

    private void RefreshGeneral()
    {
        _volume.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + " %";
        if (_resList == null || _resList.Count == 0) return;
        DisplaySettings.Res r = _resList[_resIndex];
        DisplaySettings.Res n = DisplaySettings.Native;
        string tag = r.w == n.w && r.h == n.h ? "  <size=17><color=#7FDB94>nativa</color></size>"
                   : (n.w > 0 && (r.w > n.w || r.h > n.h)) ? "  <size=17><color=#F07A6E>mayor que tu monitor</color></size>" : "";
        _resText.text = r + "  <size=18><color=#9FB3C8>" + r.AspectName + "</color></size>" + tag;
        _modeText.text = DisplaySettings.ModeName(DisplaySettings.Modes[_modeIndex]);
        _hudText.text = DisplaySettings.HudCentered ? "Centrado 16:9" : "Toda la pantalla";
    }

    private void BuildControl()
    {
        _control = FrostUI.Stretch(FrostUI.Node(_content, "Control"));
        TextMeshProUGUI kicker = FrostUI.Kicker(_control, "Periferico", "Periférico preferido · jugador 1", 18f, FrostUI.Ice);
        FrostUI.Place(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(700f, 28f));

        _kbCard = MakeButton(_control, "Teclado y ratón", new Vector2(420f, 74f), 26f);
        FrostUI.Place((RectTransform)_kbCard.transform, new Vector2(0f, 1f), new Vector2(0f, -36f), new Vector2(420f, 74f));
        _kbCard.label.rectTransform.offsetMin = new Vector2(120f, 0f);
        _kbCard.label.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform kbIcons = FrostUI.Node(_kbCard.border.transform, "Iconos");
        FrostUI.Place(kbIcons, new Vector2(0f, 0.5f), new Vector2(18f, -20f), new Vector2(90f, 40f));
        Place(InputGlyph.Key(kbIcons, (int)UnityEngine.InputSystem.Key.W, 36f), new Vector2(0f, 0.5f), new Vector2(0f, 0f));
        Place(InputGlyph.Key(kbIcons, InputMap.MouseLeft, 38f), new Vector2(0f, 0.5f), new Vector2(48f, 0f));
        _kbCard.button.onClick.AddListener(() => SetDevice(InputMap.Device.KeyboardMouse));

        _padCard = MakeButton(_control, "Mando de Xbox", new Vector2(420f, 74f), 26f);
        FrostUI.Place((RectTransform)_padCard.transform, new Vector2(0f, 1f), new Vector2(440f, -36f), new Vector2(420f, 74f));
        _padCard.label.rectTransform.offsetMin = new Vector2(120f, 0f);
        _padCard.label.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform padIcons = FrostUI.Node(_padCard.border.transform, "Iconos");
        FrostUI.Place(padIcons, new Vector2(0f, 0.5f), new Vector2(18f, -20f), new Vector2(90f, 40f));
        Place(InputGlyph.Pad(padIcons, (int)UnityEngine.InputSystem.LowLevel.GamepadButton.South, 38f), new Vector2(0f, 0.5f), new Vector2(0f, 0f));
        Place(InputGlyph.Pad(padIcons, (int)UnityEngine.InputSystem.LowLevel.GamepadButton.West, 38f), new Vector2(0f, 0.5f), new Vector2(46f, 0f));
        _padCard.button.onClick.AddListener(() => SetDevice(InputMap.Device.Gamepad));

        _status = FrostUI.Text(_control, "Estado", "", 17f, FrostUI.Secondary, TextAlignmentOptions.MidlineLeft, FrostUI.BodyLight);
        FrostUI.Place(_status.rectTransform, new Vector2(0f, 1f), new Vector2(880f, -48f), new Vector2(460f, 50f));
        _status.textWrappingMode = TextWrappingModes.Normal;

        _gridTitle = FrostUI.Kicker(_control, "Asignacion", "", 18f, FrostUI.Ice);
        FrostUI.Place(_gridTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -132f), new Vector2(900f, 28f));
        _grid = FrostUI.Node(_control, "Lista");
        _grid.anchorMin = new Vector2(0f, 0f);
        _grid.anchorMax = new Vector2(1f, 1f);
        _grid.offsetMin = new Vector2(0f, 0f);
        _grid.offsetMax = new Vector2(0f, -168f);
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
    }

    // ---------- Pestañas ----------

    private void ShowTab(Tab t)
    {
        CancelCapture();
        _tab = t;
        _general.gameObject.SetActive(t == Tab.General);
        _control.gameObject.SetActive(t == Tab.Control);
        _reset.gameObject.SetActive(t == Tab.Control);
        _tabGeneral.selectedLook = t == Tab.General; _tabGeneral.Refresh();
        _tabControl.selectedLook = t == Tab.Control; _tabControl.Refresh();
        if (t == Tab.Control) RefreshControl();
        else SetHelp("", FrostUI.Label);
        Select(t == Tab.Control ? _tabControl.button : _tabGeneral.button);
    }

    private void SetDevice(InputMap.Device d)
    {
        InputMap.Preferred = d;
        RefreshControl();
        SetHelp(d == InputMap.Device.Gamepad
            ? "El jugador 1 jugará con el mando. El jugador 2 necesitará otro mando para unirse."
            : "El jugador 1 jugará con teclado y ratón. Cualquier mando puede unir al jugador 2 con " + InputMap.PadName(InputMap.PadOf(GameAction.Pause)) + ".", FrostUI.Ice);
    }

    private void RefreshControl()
    {
        if (_control == null) return;
        InputMap.Device d = InputMap.Preferred;
        _kbCard.selectedLook = d == InputMap.Device.KeyboardMouse; _kbCard.Refresh();
        _padCard.selectedLook = d == InputMap.Device.Gamepad; _padCard.Refresh();
        int pads = Gamepad.all.Count;
        _status.text = pads == 0 ? "No hay ningún mando conectado." : pads == 1 ? "Mando conectado: " + Gamepad.all[0].displayName : pads + " mandos conectados.";
        _gridTitle.text = d == InputMap.Device.Gamepad ? "ASIGNACIÓN DE BOTONES · MANDO DE XBOX" : "ASIGNACIÓN DE TECLAS · TECLADO Y RATÓN";
        BuildRows(d);
        if (_capturing == null && _tab == Tab.Control && string.IsNullOrEmpty(_help.text))
            SetHelp(d == InputMap.Device.Gamepad ? "Elige una acción con A y pulsa el botón nuevo. Moverse es siempre el stick izquierdo."
                                                 : "Elige una acción y pulsa la tecla o el botón del ratón nuevo. Esc cancela.", FrostUI.Label);
    }

    private void BuildRows(InputMap.Device d)
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        GameAction? keep = null;
        foreach (var r in _rows) if (r.button != null && r.button.gameObject == selected) keep = r.action;
        foreach (var r in _rows) if (r.button != null) Destroy(r.button.gameObject);
        _rows.Clear();

        var actions = new List<GameAction>();
        foreach (GameAction a in InputMap.All)
        {
            if (d == InputMap.Device.Gamepad && !InputMap.PadRebindable(a) && a != GameAction.MoveUp) continue;
            actions.Add(a);
        }
        int perColumn = Mathf.CeilToInt(actions.Count / 2f);
        // La altura de fila se ajusta al hueco de la lista: con 19 acciones las de 50 px se metían bajo los botones.
        Canvas.ForceUpdateCanvases();
        float space = _grid.rect.height > 1f ? _grid.rect.height : 452f;
        float gap = 4f, rowH = Mathf.Min(50f, space / perColumn - gap), colW = 650f;
        for (int i = 0; i < actions.Count; i++)
        {
            GameAction a = actions[i];
            int col = i / perColumn, row = i % perColumn;
            bool fixedMove = d == InputMap.Device.Gamepad && !InputMap.PadRebindable(a);
            string name = fixedMove ? "Moverse" : InputMap.ActionName(a);
            FrostButton b = FrostButton.Create(_grid, "Accion_" + a, name, new Vector2(colW, rowH), FrostButton.Style.Normal, 21f);
            FrostUI.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(col * (colW + 20f), -row * (rowH + gap)), new Vector2(colW, rowH));
            b.label.alignment = TextAlignmentOptions.MidlineLeft;
            b.label.rectTransform.offsetMin = new Vector2(20f, 0f);
            b.label.font = FrostUI.Body;
            RectTransform slot = FrostUI.Node(b.border.transform, "Icono");
            slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(1f, 0.5f);
            slot.anchoredPosition = new Vector2(-14f, 0f);
            slot.sizeDelta = new Vector2(200f, rowH - 10f);
            RectTransform g = InputGlyph.ForAction(slot, a, d, rowH - 12f);
            g.anchorMin = g.anchorMax = g.pivot = new Vector2(1f, 0.5f);
            g.anchoredPosition = Vector2.zero;
            if (fixedMove)
            {
                b.button.interactable = false;
                b.label.text = "Moverse  <size=15><color=#9FB3C8>(stick izquierdo, fijo)</color></size>";
            }
            else b.button.onClick.AddListener(() => BeginCapture(a));
            _rows.Add((a, b, slot));
        }
        if (keep.HasValue)
            foreach (var r in _rows) if (r.action == keep.Value) Select(r.button.button);
    }

    // ---------- Reasignar ----------

    private void BeginCapture(GameAction a)
    {
        if (Time.unscaledTime < _cooldownUntil) return;
        // Mientras espera, la cruceta y A no navegan ni pulsan botones del panel.
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
        _capturing = a;
        _waitRelease = true;
        _captureUntil = Time.unscaledTime + CaptureSeconds;
        foreach (var r in _rows)
        {
            if (r.action != a) continue;
            foreach (Transform c in r.glyphSlot) Destroy(c.gameObject);
            TextMeshProUGUI t = FrostUI.Text(r.glyphSlot, "Esperando", InputMap.Preferred == InputMap.Device.Gamepad ? "Pulsa un botón…" : "Pulsa una tecla…", 19f, FrostUI.Gold, TextAlignmentOptions.MidlineRight);
            FrostUI.Stretch(t.rectTransform);
        }
        SetHelp(InputMap.Preferred == InputMap.Device.Gamepad
            ? "Pulsa el botón nuevo para «" + InputMap.ActionName(a) + "». Se cancela solo en 6 s."
            : "Pulsa la tecla o el botón del ratón nuevo para «" + InputMap.ActionName(a) + "». Esc cancela.", FrostUI.Gold);
    }

    private void CancelCapture()
    {
        if (_capturing == null) return;
        _capturing = null;
        _cooldownUntil = Time.unscaledTime + 0.25f;
        RefreshControl();
    }

    private void UpdateCapture()
    {
        GameAction a = _capturing.Value;
        bool pad = InputMap.Preferred == InputMap.Device.Gamepad;
        // Lo que abrió la reasignación (A o el clic) no se asigna solo
        // (solo cuentan pulsaciones nuevas, y no en el primer instante).
        if (_waitRelease)
        {
            if (Time.unscaledTime < _captureUntil - CaptureSeconds + 0.2f) return;
            _waitRelease = false;
        }
        Keyboard kb = Keyboard.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) || Time.unscaledTime > _captureUntil)
        {
            CancelCapture();
            SetHelp("Reasignación cancelada.", FrostUI.Secondary);
            return;
        }
        int code = pad ? InputMap.PadButtonPressed() : InputMap.KeyPressedNow();
        if (pad ? code == InputMap.NoPad : code == InputMap.None) return;
        GameAction? swapped = pad ? InputMap.SetPad(a, code) : InputMap.SetKey(a, code);
        _capturing = null;
        RefreshControl();
        string name = pad ? InputMap.PadName(code) : InputMap.KeyName(code);
        SetHelp("«" + InputMap.ActionName(a) + "» ahora es " + name + "."
                + (swapped.HasValue ? " «" + InputMap.ActionName(swapped.Value) + "» recibió la asignación anterior." : ""), FrostUI.Ice);
        // Que la tecla/botón recién pulsado no active nada más en este fotograma.
        _waitRelease = false;
        _cooldownUntil = Time.unscaledTime + (!pad && code < 0 ? 0.8f : 0.3f);
    }

    private float _cooldownUntil;

    void Update()
    {
        if (_capturing != null) { UpdateCapture(); return; }
        if (Time.unscaledTime < _cooldownUntil) return;
        if (EventSystem.current != null && !EventSystem.current.sendNavigationEvents) EventSystem.current.sendNavigationEvents = true;
        if (UICancel.Pressed()) { Close(); return; }
        Gamepad g = Gamepad.current;
        if (g != null && (g.leftShoulder.wasPressedThisFrame || g.rightShoulder.wasPressedThisFrame))
            ShowTab(_tab == Tab.General ? Tab.Control : Tab.General);
        if (_tab == Tab.Control && Time.frameCount % 30 == 0)
        {
            int pads = Gamepad.all.Count;
            _status.text = pads == 0 ? "No hay ningún mando conectado." : pads == 1 ? "Mando conectado: " + Gamepad.all[0].displayName : pads + " mandos conectados.";
        }
    }

    private void SetHelp(string text, Color color)
    {
        if (_help == null) return;
        _help.text = text;
        _help.color = color;
    }

    private static void Select(Selectable s)
    {
        if (s == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    // ---------- Para las comprobaciones ----------

    public string Describe()
    {
        var sb = new System.Text.StringBuilder("pestaña=" + _tab + " periferico=" + InputMap.Preferred + " filas=" + _rows.Count);
        foreach (var r in _rows)
        {
            string glyph = r.glyphSlot.childCount > 0 ? r.glyphSlot.GetChild(0).name : "-";
            sb.Append(" | ").Append(r.action).Append("=").Append(glyph);
        }
        return sb.ToString();
    }

    public void TestBeginCapture(GameAction a) => BeginCapture(a);
}
