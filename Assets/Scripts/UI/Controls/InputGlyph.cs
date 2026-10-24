using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Iconos de controles al estilo de Minecraft Dungeons: botones del mando de Xbox (A verde, B rojo, X azul, Y amarillo,
// LB/RB, LT/RT, cruceta, sticks, Vista y Menú) y teclas o botones del ratón como teclas dibujadas.
// Todo con piezas de UI (sin texturas nuevas), así sirve igual en el menú, la pausa y el HUD.
public static class InputGlyph
{
    public static Color GreenA => FrostboundUI.PadA;
    public static Color RedB => FrostboundUI.PadB;
    public static Color BlueX => FrostboundUI.PadX;
    public static Color YellowY => FrostboundUI.PadY;
    private static Color Body => FrostboundUI.PadBody;
    private static Color Rim => FrostboundUI.PadRim;
    private static Color Ink => FrostboundUI.PadInk;

    private static UISkin Skin => FrostUI.Skin;

    // Icono de la asignación actual de una acción para un periférico. size = alto en píxeles de UI.
    public static RectTransform ForAction(Transform parent, GameAction a, InputMap.Device device, float size)
    {
        if (device == InputMap.Device.Gamepad)
        {
            if (!InputMap.PadRebindable(a)) return Stick(parent, "L", size, false);
            return Pad(parent, InputMap.PadOf(a), size);
        }
        return Key(parent, InputMap.KeyOf(a), size);
    }

    // ---------- Mando ----------

    public static RectTransform Pad(Transform parent, int code, float size)
    {
        if (code == InputMap.NoPad) return Label(parent, "—", size);
        switch ((GamepadButton)code)
        {
            case GamepadButton.South: return Face(parent, "A", GreenA, size);
            case GamepadButton.East: return Face(parent, "B", RedB, size);
            case GamepadButton.West: return Face(parent, "X", BlueX, size);
            case GamepadButton.North: return Face(parent, "Y", YellowY, size);
            case GamepadButton.LeftShoulder: return Bumper(parent, "LB", size);
            case GamepadButton.RightShoulder: return Bumper(parent, "RB", size);
            case GamepadButton.LeftTrigger: return Trigger(parent, "LT", size);
            case GamepadButton.RightTrigger: return Trigger(parent, "RT", size);
            case GamepadButton.LeftStick: return Stick(parent, "L", size, true);
            case GamepadButton.RightStick: return Stick(parent, "R", size, true);
            case GamepadButton.DpadUp: return Dpad(parent, 0, size);
            case GamepadButton.DpadRight: return Dpad(parent, 1, size);
            case GamepadButton.DpadDown: return Dpad(parent, 2, size);
            case GamepadButton.DpadLeft: return Dpad(parent, 3, size);
            case GamepadButton.Select: return View(parent, size);
            case GamepadButton.Start: return Menu(parent, size);
            default: return Label(parent, InputMap.PadName(code), size);
        }
    }

    private static RectTransform Root(Transform parent, string name, float w, float h)
    {
        RectTransform rt = FrostUI.Node(parent, "Glifo_" + name);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private static Image Shape(Transform parent, string name, Sprite sprite, Color color, Vector2 size, Vector2 pos = default)
    {
        Image img = FrostUI.Img(parent, name, sprite, color);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.rectTransform.sizeDelta = size;
        img.rectTransform.anchoredPosition = pos;
        return img;
    }

    private static TextMeshProUGUI Letter(Transform parent, string text, float size, Color color)
    {
        TextMeshProUGUI t = FrostUI.Text(parent, "Letra", text, size, color, TextAlignmentOptions.Center, Skin != null ? Skin.nunito800 : null);
        FrostUI.Stretch(t.rectTransform);
        t.fontStyle = FontStyles.Bold;
        return t;
    }

    // A, B, X, Y: círculo de color con la letra blanca, como en el mando.
    private static RectTransform Face(Transform parent, string letter, Color color, float size)
    {
        RectTransform rt = Root(parent, letter, size, size);
        Shape(rt, "Borde", Skin != null ? Skin.circle : null, Ink, new Vector2(size, size));
        Shape(rt, "Boton", Skin != null ? Skin.circle : null, color, new Vector2(size * 0.86f, size * 0.86f));
        Shape(rt, "Brillo", Skin != null ? Skin.circle : null, new Color(1f, 1f, 1f, 0.18f), new Vector2(size * 0.5f, size * 0.3f), new Vector2(0f, size * 0.17f));
        TextMeshProUGUI t = Letter(rt, letter, size * 0.56f, Color.white);
        t.outlineWidth = 0.18f;
        t.outlineColor = new Color32(5, 8, 14, 255);
        return rt;
    }

    private static RectTransform Bumper(Transform parent, string text, float size)
    {
        RectTransform rt = Root(parent, text, size * 1.7f, size * 0.8f);
        Shape(rt, "Borde", Skin != null ? Skin.pill : null, Rim, rt.sizeDelta);
        Shape(rt, "Boton", Skin != null ? Skin.pill : null, Body, rt.sizeDelta - new Vector2(4f, 4f));
        Letter(rt, text, size * 0.42f, Color.white);
        return rt;
    }

    private static RectTransform Trigger(Transform parent, string text, float size)
    {
        RectTransform rt = Root(parent, text, size * 1.15f, size);
        Shape(rt, "Borde", Skin != null ? Skin.round12 : null, Rim, rt.sizeDelta);
        Shape(rt, "Boton", Skin != null ? Skin.round12 : null, Body, rt.sizeDelta - new Vector2(4f, 4f));
        Shape(rt, "Curva", Skin != null ? Skin.pill : null, Rim, new Vector2(size * 0.7f, 3f), new Vector2(0f, size * 0.3f));
        Letter(rt, text, size * 0.4f, Color.white).rectTransform.offsetMin = new Vector2(0f, -size * 0.08f);
        return rt;
    }

    private static RectTransform Stick(Transform parent, string side, float size, bool click)
    {
        RectTransform rt = Root(parent, "Stick" + side, size, size);
        Shape(rt, "Borde", Skin != null ? Skin.circle : null, Rim, new Vector2(size, size));
        Shape(rt, "Base", Skin != null ? Skin.circle : null, Body, new Vector2(size * 0.88f, size * 0.88f));
        Shape(rt, "Seta", Skin != null ? Skin.circle : null, FrostboundUI.PadStick, new Vector2(size * 0.62f, size * 0.62f));
        Letter(rt, click ? side + "S" : side, size * (click ? 0.34f : 0.44f), Color.white);
        if (click) Shape(rt, "Pulsar", Skin != null ? Skin.circle : null, FrostUI.Ice, new Vector2(size * 0.16f, size * 0.16f), new Vector2(0f, -size * 0.36f));
        return rt;
    }

    // Cruceta con el brazo pulsado en blanco. arm: 0 arriba, 1 derecha, 2 abajo, 3 izquierda.
    private static RectTransform Dpad(Transform parent, int arm, float size)
    {
        RectTransform rt = Root(parent, "Cruceta" + arm, size, size);
        float w = size * 0.36f;
        Shape(rt, "BordeV", null, Ink, new Vector2(w + 4f, size));
        Shape(rt, "BordeH", null, Ink, new Vector2(size, w + 4f));
        Shape(rt, "V", null, Body, new Vector2(w, size - 4f));
        Shape(rt, "H", null, Body, new Vector2(size - 4f, w));
        float d = (size - w) * 0.25f + w * 0.5f;
        Vector2 pos = arm == 0 ? new Vector2(0f, d) : arm == 1 ? new Vector2(d, 0f) : arm == 2 ? new Vector2(0f, -d) : new Vector2(-d, 0f);
        Vector2 sz = arm % 2 == 0 ? new Vector2(w - 2f, (size - w) * 0.5f - 2f) : new Vector2((size - w) * 0.5f - 2f, w - 2f);
        Shape(rt, "Pulsado", null, Color.white, sz, pos);
        return rt;
    }

    // Vista: dos ventanas superpuestas.
    private static RectTransform View(Transform parent, float size)
    {
        RectTransform rt = Root(parent, "Vista", size, size);
        Shape(rt, "Borde", Skin != null ? Skin.circle : null, Rim, new Vector2(size, size));
        Shape(rt, "Base", Skin != null ? Skin.circle : null, Body, new Vector2(size * 0.88f, size * 0.88f));
        float s = size * 0.3f;
        Shape(rt, "Atras", null, Color.white, new Vector2(s, s), new Vector2(-s * 0.25f, s * 0.25f));
        Shape(rt, "AtrasHueco", null, Body, new Vector2(s - 4f, s - 4f), new Vector2(-s * 0.25f, s * 0.25f));
        Shape(rt, "Delante", null, Color.white, new Vector2(s, s), new Vector2(s * 0.25f, -s * 0.25f));
        return rt;
    }

    // Menú: tres rayas.
    private static RectTransform Menu(Transform parent, float size)
    {
        RectTransform rt = Root(parent, "Menu", size, size);
        Shape(rt, "Borde", Skin != null ? Skin.circle : null, Rim, new Vector2(size, size));
        Shape(rt, "Base", Skin != null ? Skin.circle : null, Body, new Vector2(size * 0.88f, size * 0.88f));
        for (int i = -1; i <= 1; i++) Shape(rt, "Raya" + i, null, Color.white, new Vector2(size * 0.4f, Mathf.Max(2f, size * 0.07f)), new Vector2(0f, i * size * 0.15f));
        return rt;
    }

    // ---------- Teclado y ratón ----------

    public static RectTransform Key(Transform parent, int code, float size)
    {
        if (code == InputMap.None) return Label(parent, "—", size);
        if (code < 0) return MouseIcon(parent, code, size);
        string text = InputMap.KeyName(code);
        float w = Mathf.Max(size, size * 0.34f * text.Length + size * 0.5f);
        RectTransform rt = Root(parent, "Tecla", w, size);
        Shape(rt, "Sombra", Skin != null ? Skin.round10 : null, Ink, new Vector2(w, size), new Vector2(0f, -2f));
        Shape(rt, "Borde", Skin != null ? Skin.round10 : null, Rim, new Vector2(w, size - 2f), new Vector2(0f, 1f));
        Shape(rt, "Tapa", Skin != null ? Skin.round10 : null, Body, new Vector2(w - 4f, size - 6f), new Vector2(0f, 1f));
        TextMeshProUGUI t = Letter(rt, text, size * (text.Length > 3 ? 0.36f : 0.5f), Color.white);
        t.rectTransform.offsetMin = new Vector2(4f, 0f);
        t.rectTransform.offsetMax = new Vector2(-4f, 0f);
        return rt;
    }

    private static RectTransform MouseIcon(Transform parent, int code, float size)
    {
        RectTransform rt = Root(parent, "Raton", size * 0.75f, size);
        Shape(rt, "Borde", Skin != null ? Skin.pill : null, Rim, new Vector2(size * 0.7f, size));
        Shape(rt, "Cuerpo", Skin != null ? Skin.pill : null, Body, new Vector2(size * 0.7f - 4f, size - 4f));
        float bw = size * 0.33f - 3f, bh = size * 0.4f;
        if (code == InputMap.MouseMiddle) Shape(rt, "Rueda", Skin != null ? Skin.pill : null, Color.white, new Vector2(size * 0.1f, size * 0.24f), new Vector2(0f, size * 0.22f));
        else Shape(rt, "Boton", null, Color.white, new Vector2(bw, bh), new Vector2(code == InputMap.MouseLeft ? -bw * 0.5f - 1f : bw * 0.5f + 1f, size * 0.22f));
        Shape(rt, "Linea", null, Rim, new Vector2(2f, bh), new Vector2(0f, size * 0.22f));
        return rt;
    }

    private static RectTransform Label(Transform parent, string text, float size)
    {
        RectTransform rt = Root(parent, "Texto", size * 1.4f, size);
        Letter(rt, text, size * 0.45f, FrostUI.Secondary);
        return rt;
    }
}
