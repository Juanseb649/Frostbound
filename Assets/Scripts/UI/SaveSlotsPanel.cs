using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Ventana de las 4 ranuras de partida. En modo Cargar se elige una partida guardada (y se puede borrar);
// en modo Nueva se elige dónde guardar la partida nueva, con confirmación si la ranura ya está ocupada.
public class SaveSlotsPanel : MonoBehaviour
{
    public enum Mode { Load, New }

    private Mode _mode;
    private Action<int> _onChosen;
    private Action _onClosed;
    private UISkin _skin;
    private RectTransform _list;
    private TextMeshProUGUI _hint;
    private int _confirmSlot = -1;
    private bool _confirmDelete;
    private Button _back;
    private bool _closing;

    public static bool IsOpen { get; private set; }

    public static SaveSlotsPanel Open(Mode mode, Action<int> onChosen, Action onClosed)
    {
        var go = new GameObject("SaveSlotsPanel", typeof(RectTransform));
        var panel = go.AddComponent<SaveSlotsPanel>();
        IsOpen = true;
        panel._mode = mode;
        panel._onChosen = onChosen;
        panel._onClosed = onClosed;
        panel._skin = GameDatabase.Instance != null ? GameDatabase.Instance.uiSkin : null;
        panel.Build();
        return panel;
    }

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(FrostboundUI.ReferenceWidth, FrostboundUI.ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        Image dim = UIFactory.Img(transform, "Velo", null, FrostboundUI.Dim);
        dim.raycastTarget = true;
        UIFactory.Stretch(dim.rectTransform);

        Image frame = UIFactory.Frame(transform, "Ventana", _skin != null ? _skin.round20 : null, FrostboundUI.Surface, FrostboundUI.Border, out _);
        UIFactory.Anchored(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 640f));
        RectTransform win = frame.rectTransform;

        var kicker = UIFactory.Text(win, "Kicker", _mode == Mode.Load ? "CONTINUAR" : "NUEVA PARTIDA", _skin != null ? _skin.nunito800 : null, 13f, FrostboundUI.Gold, TextAlignmentOptions.Center, 5f);
        UIFactory.TopLeft(kicker.rectTransform, 0f, 30f, 760f, 20f);
        var title = UIFactory.Text(win, "Titulo", _mode == Mode.Load ? "Cargar partida" : "Elige una ranura", _skin != null ? _skin.cinzel800 : null, 34f, FrostboundUI.Title, TextAlignmentOptions.Center, 3f);
        UIFactory.TopLeft(title.rectTransform, 0f, 54f, 760f, 44f);

        _list = UIFactory.Node(win, "Ranuras");
        UIFactory.TopLeft(_list, 40f, 118f, 680f, 420f);

        _hint = UIFactory.Text(win, "Ayuda", "", _skin != null ? _skin.nunito700 : null, 14f, FrostboundUI.Muted, TextAlignmentOptions.Center);
        UIFactory.TopLeft(_hint.rectTransform, 40f, 546f, 680f, 22f);

        _back = UIFactory.FramedButton(win, "Volver", _skin, _skin != null ? _skin.round10 : null, "Volver", 14f,
            FrostboundUI.Surface, FrostboundUI.Border, FrostboundUI.Text, out _, out _);
        UIFactory.TopLeft((RectTransform)_back.transform, 300f, 578f, 160f, 44f);
        _back.onClick.AddListener(Close);

        Refresh();
    }

    private void Refresh()
    {
        for (int i = _list.childCount - 1; i >= 0; i--) Destroy(_list.GetChild(i).gameObject);
        var buttons = new Button[SaveSystem.SlotCount];
        Button firstUsable = null;

        for (int slot = 0; slot < SaveSystem.SlotCount; slot++)
        {
            SaveData data = SaveSystem.Load(slot);
            bool confirming = _confirmSlot == slot;
            Color border = confirming ? FrostboundUI.Negative : FrostboundUI.Border;
            Button b = UIFactory.FramedButton(_list, "Ranura_" + (slot + 1), _skin, _skin != null ? _skin.round16 : null, "", 1f,
                data != null ? FrostboundUI.SurfaceSelected : FrostboundUI.Bg, border, FrostboundUI.Text, out TextMeshProUGUI label, out _);
            label.gameObject.SetActive(false);
            var rt = (RectTransform)b.transform;
            UIFactory.TopLeft(rt, 0f, slot * 104f, 680f, 92f);

            var number = UIFactory.Text(rt, "Numero", (slot + 1).ToString(), _skin != null ? _skin.cinzel800 : null, 34f,
                data != null ? FrostboundUI.Ice : FrostboundUI.Disabled, TextAlignmentOptions.Center);
            UIFactory.TopLeft(number.rectTransform, 12f, 22f, 56f, 48f);

            string line1, line2;
            if (confirming)
            {
                line1 = _confirmDelete ? "¿Borrar esta partida?" : "¿Sobrescribir esta partida?";
                line2 = "Pulsa otra vez para confirmar. Se perderá " + (data != null ? data.heroName : "") + ".";
            }
            else if (data != null)
            {
                CharacterClass cls = GameDatabase.Instance != null ? GameDatabase.Instance.FindClass(data.classId) : null;
                line1 = data.heroName;
                line2 = ClassLabel(cls, data) + "  ·  Nivel " + data.level + "  ·  " + FormatTime(data.playSeconds) + "  ·  " + FormatDate(data.savedUtc);
            }
            else
            {
                line1 = "Ranura vacía";
                line2 = _mode == Mode.New ? "Empezar aquí una partida nueva" : "Sin partida guardada";
            }
            var t1 = UIFactory.Text(rt, "Linea1", line1, _skin != null ? _skin.cinzel600 : null, 22f,
                confirming ? FrostboundUI.Negative : data != null ? FrostboundUI.Title : FrostboundUI.Muted, TextAlignmentOptions.Left);
            UIFactory.TopLeft(t1.rectTransform, 84f, 16f, 470f, 30f);
            var t2 = UIFactory.Text(rt, "Linea2", line2, _skin != null ? _skin.nunito700 : null, 14f, FrostboundUI.Body, TextAlignmentOptions.Left);
            UIFactory.TopLeft(t2.rectTransform, 84f, 52f, 520f, 22f);

            int captured = slot;
            bool usable = _mode == Mode.New || data != null;
            b.interactable = usable;
            b.onClick.AddListener(() => Choose(captured, data != null));
            buttons[slot] = b;
            if (usable && firstUsable == null) firstUsable = b;

            if (_mode == Mode.Load && data != null && !confirming)
            {
                Button del = UIFactory.FramedButton(rt, "Borrar", _skin, _skin != null ? _skin.round10 : null, "Borrar", 12f,
                    FrostboundUI.Bg, FrostboundUI.Border, FrostboundUI.Muted, out _, out _);
                UIFactory.TopLeft((RectTransform)del.transform, 568f, 26f, 96f, 40f);
                del.onClick.AddListener(() => AskDelete(captured));
                del.navigation = new Navigation { mode = Navigation.Mode.None };
            }
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i > 0 ? buttons[i - 1] : _back,
                selectOnDown = i < buttons.Length - 1 ? buttons[i + 1] : _back
            };
        }
        _back.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = buttons[buttons.Length - 1], selectOnDown = buttons[0] };

        _hint.text = _mode == Mode.Load ? "Elige una partida para continuar" : "Las ranuras ocupadas piden confirmación antes de sobrescribirse";
        Select(_confirmSlot >= 0 ? buttons[_confirmSlot] : firstUsable != null ? firstUsable : _back);
    }

    private void Choose(int slot, bool occupied)
    {
        if (_closing) return;
        if (_confirmSlot == slot)
        {
            if (_confirmDelete)
            {
                SaveSystem.Delete(slot);
                _confirmSlot = -1;
                _confirmDelete = false;
                Refresh();
                return;
            }
            Finish(slot);
            return;
        }
        if (_mode == Mode.New && occupied)
        {
            _confirmSlot = slot;
            _confirmDelete = false;
            Refresh();
            return;
        }
        Finish(slot);
    }

    private void AskDelete(int slot)
    {
        _confirmSlot = slot;
        _confirmDelete = true;
        Refresh();
    }

    private void Finish(int slot)
    {
        _closing = true;
        Destroy(gameObject);
        _onChosen?.Invoke(slot);
    }

    public void Close()
    {
        if (_closing) return;
        if (_confirmSlot >= 0)
        {
            _confirmSlot = -1;
            _confirmDelete = false;
            Refresh();
            return;
        }
        _closing = true;
        Destroy(gameObject);
        _onClosed?.Invoke();
    }

    void Update()
    {
        if (UICancel.Pressed()) Close();
    }

    void OnDestroy() => IsOpen = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsOpen = false;

    private static void Select(Selectable s)
    {
        if (s == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    private static string ClassLabel(CharacterClass cls, SaveData data)
    {
        if (cls == null) return data.classId;
        return !string.IsNullOrEmpty(cls.displayName) && cls.displayName != data.heroName ? cls.displayName : cls.className;
    }

    private static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        return (m / 60).ToString("00") + ":" + (m % 60).ToString("00") + " h";
    }

    private static string FormatDate(string utc)
    {
        return DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime d)
            ? d.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "";
    }
}
