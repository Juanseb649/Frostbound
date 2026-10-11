using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Botón de los menús. Estados de la guía: normal (noche con borde azul), al pasar (borde de hielo y brillo),
// pulsado (se hunde), desactivado (apagado). Los principales y los que tienen "hielo al enfocar" se vuelven
// de hielo (relleno claro, borde blanco y texto noche) como "Jugar".
public class FrostButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
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
    private static readonly Color IceFill = new Color(0.82f, 0.93f, 0.99f);
    private static readonly Color HoverFill = new Color(0.08f, 0.14f, 0.24f, 0.95f);
    private static readonly Color DangerFill = new Color(0.23f, 0.06f, 0.07f, 0.95f);

    public void OnPointerEnter(PointerEventData e) { _hover = true; Refresh(); }
    public void OnPointerExit(PointerEventData e) { _hover = false; _down = false; Refresh(); }
    public void OnPointerDown(PointerEventData e) { _down = true; Refresh(); }
    public void OnPointerUp(PointerEventData e) { _down = false; Refresh(); }
    public void OnSelect(BaseEventData e) { _focus = true; Refresh(); }
    public void OnDeselect(BaseEventData e) { _focus = false; Refresh(); }

    void OnEnable() => Refresh();

    private bool _lastInteractable = true;

    void Update()
    {
        bool on = button == null || button.interactable;
        if (on != _lastInteractable) Refresh();
        float target = _down ? 0.97f : (_hover || _focus) && on ? 1.02f : 1f;
        float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 0.6f);
        transform.localScale = new Vector3(s, s, 1f);
    }

    public void Refresh()
    {
        bool on = button == null || button.interactable;
        _lastInteractable = on;
        bool active = (_hover || _focus || selectedLook) && on;
        bool ice = on && (style == Style.Primary || (iceOnFocus && active));
        Color b, f, t;
        if (!on)
        {
            b = FrostUI.BorderDim; f = new Color(0.05f, 0.08f, 0.14f, 0.85f); t = FrostUI.Disabled;
        }
        else if (ice)
        {
            b = Color.white; f = _down ? new Color(0.68f, 0.84f, 0.95f) : IceFill; t = FrostUI.Outline;
        }
        else if (style == Style.Danger)
        {
            b = active ? new Color(1f, 0.55f, 0.5f) : new Color(0.75f, 0.27f, 0.22f); f = DangerFill; t = Color.white;
        }
        else
        {
            b = active ? FrostUI.Ice : FrostUI.Border; f = _down ? new Color(0.03f, 0.05f, 0.1f, 0.95f) : active ? HoverFill : FrostUI.PanelColor; t = Color.white;
        }
        if (border != null) border.color = b;
        if (fill != null) fill.color = f;
        if (shine != null) shine.enabled = ice;
        if (glow != null)
        {
            glow.enabled = on && (active || style == Style.Primary);
            glow.color = new Color(FrostUI.Ice.r, FrostUI.Ice.g, FrostUI.Ice.b, ice ? 0.55f : 0.35f);
        }
        if (label != null)
        {
            label.color = t;
            label.fontSharedMaterial = ice ? DarkMaterial(label.font) : FrostUI.BodyMaterial(label.font);
        }
        if (icon != null)
        {
            icon.color = t;
            if (iconFocus != null && iconIdle != null) icon.sprite = active ? iconFocus : iconIdle;
        }
    }

    private static Material _dark;

    // Texto noche sobre hielo, sin contorno.
    private static Material DarkMaterial(TMP_FontAsset font)
    {
        if (_dark != null && _dark.mainTexture == font.material.mainTexture) return _dark;
        _dark = new Material(font.material) { name = font.name + "_sobre_hielo" };
        return _dark;
    }

    // ---------- Fábrica ----------

    public static FrostButton Create(Transform parent, string name, string text, Vector2 size, Style style, float fontSize = 26f, bool diamond = false)
    {
        RectTransform root = FrostUI.Node(parent, name);
        root.sizeDelta = size;
        Image glow = FrostUI.Img(root, "Brillo", FrostUI.Glow, Color.white);
        FrostUI.Stretch(glow.rectTransform, -16f);
        Image border = FrostUI.Img(root, "Borde", FrostUI.Skin != null ? FrostUI.Skin.round16 : null, FrostUI.Border);
        FrostUI.Stretch(border.rectTransform);
        border.raycastTarget = true;
        Image fill = FrostUI.Img(border.transform, "Relleno", FrostUI.Skin != null ? FrostUI.Skin.round16 : null, FrostUI.PanelColor);
        FrostUI.Stretch(fill.rectTransform, 2.5f);
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
        b.label = FrostUI.Text(border.transform, "Texto", text, fontSize, Color.white,
            diamond ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center, FrostUI.Display);
        FrostUI.Stretch(b.label.rectTransform);
        b.label.rectTransform.offsetMin = new Vector2(diamond ? left : 10f, 0f);
        b.label.rectTransform.offsetMax = new Vector2(-10f, 2f);
        b.Refresh();
        return b;
    }
}
