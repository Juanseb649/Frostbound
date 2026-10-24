using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Botón de los menús con el kit "gótico de escarcha" (FrostFrames). Estados: normal (marco de hierro, texto hueso),
// al pasar o enfocar (el hierro se tiñe de oro, brillo dorado y texto oro claro), pulsado (se hunde y oscurece) y
// desactivado (apagado). El principal lleva marco de oro viejo, como "Jugar"; el de peligro, marco de sangre.
public class FrostButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    public enum Style { Normal, Primary, Danger }

    public Button button;
    public Image border, fill, glow, shine, icon;
    public TextMeshProUGUI label;
    public Style style = Style.Normal;
    public bool iceOnFocus;
    public bool selectedLook;
    public Sprite iconIdle, iconFocus;

    private bool _hover, _focus, _down;
    private static readonly Color HoverFill = new Color(0.1f, 0.14f, 0.22f, 0.96f);
    private static readonly Color DangerFill = new Color(0.2f, 0.06f, 0.07f, 0.95f);
    private static readonly Color DisabledFrame = new Color(0.45f, 0.47f, 0.52f, 0.8f);

    public void OnPointerEnter(PointerEventData e)
    {
        _hover = true;
        Refresh();
        if (button == null || button.interactable) AudioManager.PlayUI(AudioId.UI_Hover, 0.45f, 0.06f);
    }
    public void OnPointerExit(PointerEventData e) { _hover = false; _down = false; Refresh(); }
    public void OnPointerDown(PointerEventData e)
    {
        _down = true;
        Refresh();
        if (button == null || button.interactable) AudioManager.PlayUI(AudioId.UI_Click, 0.75f, 0.04f);
    }
    public void OnPointerUp(PointerEventData e) { _down = false; Refresh(); }
    public void OnSelect(BaseEventData e)
    {
        _focus = true;
        Refresh();
        if (button == null || button.interactable) AudioManager.PlayUI(AudioId.UI_Hover, 0.4f, 0.06f);
    }
    public void OnDeselect(BaseEventData e) { _focus = false; Refresh(); }
    // Con mando o teclado no hay "pointer down": el clic suena al confirmar.
    public void OnSubmit(BaseEventData e)
    {
        if (button == null || button.interactable) AudioManager.PlayUI(AudioId.UI_Click, 0.75f, 0.04f);
    }

    void OnEnable() => Refresh();

    private bool _lastInteractable = true;

    // Efectos del marco (shader Frostbound/UI Frost Frame): un destello al activarse que se repite cada pocos
    // segundos mientras sigue activo, y escarcha mientras está activo.
    // Si el shader no está, el botón se queda estático.
    private static readonly int RectId = Shader.PropertyToID("_Rect");
    private static readonly int SheenId = Shader.PropertyToID("_SheenPos");
    private static readonly int FrostId = Shader.PropertyToID("_Frost");
    private const float SheenSeconds = 0.55f, SheenRepeat = 2.4f, FrostSeconds = 0.25f;
    private static Shader _fxShader;
    private Material _fx;
    private float _sheenStart = -10f, _frost;
    private bool _wasActive;
    private readonly Vector3[] _corners = new Vector3[4];

    private void SetupFx()
    {
        if (_fxShader == null) _fxShader = Resources.Load<Shader>("Shaders/UIFrostFrame");
        if (_fxShader == null || border == null) return;
        _fx = new Material(_fxShader) { name = "Marco con destello" };
        border.material = _fx;
    }

    void OnDestroy()
    {
        if (_fx != null) Destroy(_fx);
    }

    private void UpdateFx(bool on)
    {
        if (_fx == null) return;
        bool active = on && (_hover || _focus);
        float now = Time.unscaledTime;
        if (active && !_wasActive) _sheenStart = now;
        _wasActive = active;
        // uGUI agrupa la geometría por Canvas: el shader recibe posiciones en el espacio del Canvas raíz, así que
        // el rectángulo del botón se pasa en ese mismo espacio.
        Canvas root = border.canvas != null ? border.canvas.rootCanvas : null;
        if (root != null)
        {
            border.rectTransform.GetWorldCorners(_corners);
            Vector3 a = root.transform.InverseTransformPoint(_corners[0]), b = root.transform.InverseTransformPoint(_corners[2]);
            _fx.SetVector(RectId, new Vector4(a.x, a.y, b.x - a.x, b.y - a.y));
        }
        float t = (active ? Mathf.Repeat(now - _sheenStart, SheenRepeat) : now - _sheenStart) / SheenSeconds;
        _fx.SetFloat(SheenId, t <= 1f ? Mathf.Lerp(-0.4f, 1.4f, t) : -1f);
        _frost = Mathf.MoveTowards(_frost, active ? 1f : 0f, Time.unscaledDeltaTime / FrostSeconds);
        _fx.SetFloat(FrostId, _frost);
    }

    void Update()
    {
        bool on = button == null || button.interactable;
        UpdateFx(on);
        if (on != _lastInteractable) Refresh();
        float target = _down ? 0.98f : (_hover || _focus) && on ? 1.01f : 1f;
        float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 0.6f);
        transform.localScale = new Vector3(s, s, 1f);
    }

    public void Refresh()
    {
        bool on = button == null || button.interactable;
        _lastInteractable = on;
        bool active = (_hover || _focus || selectedLook) && on;
        bool primary = style == Style.Primary;
        Color b, f, t;
        if (!on)
        {
            b = DisabledFrame; f = new Color(0.05f, 0.07f, 0.11f, 0.85f); t = FrostUI.Disabled;
        }
        else if (style == Style.Danger)
        {
            b = active ? Color.white : new Color(0.8f, 0.8f, 0.8f); f = DangerFill; t = active ? Color.white : FrostboundUI.Bone;
        }
        else
        {
            // El hierro se tiñe de oro al pasar; el marco de oro del principal se aviva.
            b = primary ? (active ? Color.white : new Color(0.85f, 0.85f, 0.85f)) : active ? FrostboundUI.OldGoldLight : Color.white;
            f = _down ? new Color(0.03f, 0.05f, 0.08f, 0.97f) : active ? HoverFill : primary ? FrostboundUI.Slate : FrostUI.PanelColor;
            t = active || primary ? FrostboundUI.OldGoldLight : FrostboundUI.Bone;
        }
        if (border != null) border.color = b;
        if (fill != null) fill.color = f;
        if (shine != null) shine.enabled = false;
        if (glow != null)
        {
            glow.enabled = on && (active || primary);
            Color g = FrostboundUI.OldGold;
            glow.color = new Color(g.r, g.g, g.b, active ? 0.4f : 0.18f);
        }
        if (label != null)
        {
            label.color = t;
            label.fontSharedMaterial = FrostUI.BodyMaterial(label.font);
        }
        if (icon != null)
        {
            icon.color = t;
            if (iconFocus != null && iconIdle != null) icon.sprite = active ? iconFocus : iconIdle;
        }
    }

    // ---------- Fábrica ----------

    public static FrostButton Create(Transform parent, string name, string text, Vector2 size, Style style, float fontSize = 26f, bool diamond = false)
    {
        RectTransform root = FrostUI.Node(parent, name);
        root.sizeDelta = size;
        Image glow = FrostUI.Img(root, "Brillo", FrostUI.Glow, Color.white);
        FrostUI.Stretch(glow.rectTransform, -16f);
        FrostFrames.Metal metal = style == Style.Primary ? FrostFrames.Metal.Gold : style == Style.Danger ? FrostFrames.Metal.Blood : FrostFrames.Metal.Iron;
        Image border = FrostUI.Img(root, "Borde", FrostFrames.Button(metal), Color.white);
        FrostUI.Stretch(border.rectTransform);
        border.raycastTarget = true;
        Image fill = FrostUI.Img(border.transform, "Relleno", FrostFrames.Plate, FrostUI.PanelColor);
        FrostUI.Stretch(fill.rectTransform, 7f);
        Image shine = FrostUI.Img(fill.transform, "Reflejo", FrostUI.SoftFade, new Color(1f, 1f, 1f, 0.45f));
        shine.rectTransform.anchorMin = new Vector2(0.02f, 0.5f);
        shine.rectTransform.anchorMax = new Vector2(0.98f, 1.1f);
        shine.rectTransform.offsetMin = shine.rectTransform.offsetMax = Vector2.zero;

        var b = root.gameObject.AddComponent<FrostButton>();
        b.button = root.gameObject.AddComponent<Button>();
        b.button.targetGraphic = border;
        b.button.transition = Selectable.Transition.None;
        b.border = border;
        b.fill = fill;
        b.glow = glow;
        b.shine = shine;
        b.style = style;
        b.SetupFx();

        float left = 0f;
        if (diamond)
        {
            b.icon = FrostUI.Img(border.transform, "Rombo", FrostUI.Diamond(false), Color.white);
            FrostUI.Place(b.icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(18f, 18f));
            b.icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            b.iconIdle = FrostUI.Diamond(false);
            b.iconFocus = FrostUI.Diamond(true);
            left = 54f;
        }
        // Cinzel en mayúsculas espaciadas; los textos largos se achican para caber en el botón.
        float labelSize = fontSize * 0.82f;
        b.label = FrostUI.Text(border.transform, "Texto", text.ToUpperInvariant(), labelSize, Color.white,
            diamond ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center, FrostUI.Heading, FrostboundUI.Tracking(labelSize * 0.08f, labelSize));
        b.label.enableAutoSizing = true;
        b.label.fontSizeMin = 12f;
        b.label.fontSizeMax = labelSize;
        FrostUI.Stretch(b.label.rectTransform);
        b.label.rectTransform.offsetMin = new Vector2(diamond ? left : 10f, 0f);
        b.label.rectTransform.offsetMax = new Vector2(-10f, 2f);
        b.Refresh();
        return b;
    }
}
