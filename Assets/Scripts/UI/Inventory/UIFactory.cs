using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Construye la interfaz del juego por código con los mismos tokens del menú (FrostboundUI).
// Las posiciones siguen el lienzo de referencia de 1440 × 900: (x, y) desde la esquina superior izquierda.
public static class UIFactory
{
    public static RectTransform Node(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static RectTransform TopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    public static RectTransform Anchored(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    public static Image Img(Transform parent, string name, Sprite sprite, Color color)
    {
        RectTransform rt = Node(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        img.preserveAspect = img.type == Image.Type.Simple && sprite != null;
        img.raycastTarget = false;
        return img;
    }

    // Caja con borde de 2 px: la raíz es el borde y "Fill" el relleno.
    public static Image Frame(Transform parent, string name, Sprite round, Color fill, Color border, out Image fillImage)
    {
        Image b = Img(parent, name, round, border);
        b.preserveAspect = false;
        fillImage = Img(b.transform, "Fill", round, fill);
        fillImage.preserveAspect = false;
        Stretch(fillImage.rectTransform, 2f);
        return b;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.TopLeft, float trackingPx = 0f, bool upper = false)
    {
        RectTransform rt = Node(parent, name);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.text = text;
        t.alignment = align;
        t.characterSpacing = trackingPx > 0f ? FrostboundUI.Tracking(trackingPx, size) : 0f;
        t.fontStyle = upper ? FontStyles.UpperCase : FontStyles.Normal;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    public static Button MakeButton(Image target)
    {
        target.raycastTarget = true;
        var b = target.gameObject.AddComponent<Button>();
        b.targetGraphic = target;
        b.transition = Selectable.Transition.None;
        return b;
    }

    // Botón con borde que se ilumina en hover o foco (UIButtonFx del menú).
    public static Button FramedButton(Transform parent, string name, UISkin skin, Sprite round, string label, float fontSize,
        Color fill, Color border, Color textColor, out TextMeshProUGUI text, out Image fillImage)
    {
        Image frame = Frame(parent, name, round, fill, border, out fillImage);
        Button b = MakeButton(frame);
        text = Text(frame.transform, "Label", label, skin.nunito800, fontSize, textColor, TextAlignmentOptions.Center, 1.5f, true);
        Stretch(text.rectTransform);
        var fx = frame.gameObject.AddComponent<UIButtonFx>();
        fx.border = frame;
        fx.normalBorder = border;
        fx.activeBorder = FrostboundUI.Ice;
        return b;
    }

    // Barra horizontal: devuelve el relleno; su ancho se controla con SetFill.
    public static RectTransform Bar(Transform parent, string name, Sprite shape, Color background, Color fill, out RectTransform root)
    {
        Image bg = Img(parent, name, shape, background);
        bg.preserveAspect = false;
        root = bg.rectTransform;
        Image f = Img(bg.transform, "Fill", shape, fill);
        f.preserveAspect = false;
        RectTransform frt = f.rectTransform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.pivot = new Vector2(0f, 0.5f);
        frt.offsetMin = Vector2.zero;
        frt.offsetMax = Vector2.zero;
        return frt;
    }

    public static void SetFill(RectTransform fill, float value)
    {
        value = Mathf.Clamp01(value);
        fill.anchorMax = new Vector2(value, 1f);
        fill.gameObject.SetActive(value > 0.001f);
    }

    public static void SetLineHeight(TMP_Text t, float cssLineHeight)
    {
        if (t.font == null) return;
        var face = t.font.faceInfo;
        float natural = face.lineHeight / face.pointSize;
        t.lineSpacing = (cssLineHeight - natural) * 100f;
    }
}
