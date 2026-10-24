using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Estilo de los menús de Frostbound (guía /d2r-ui, "gótico de escarcha"): títulos en Cinzel con letra de oro viejo
// (degradado, contorno bronce y sombra abismo), etiquetas y botones en Cinzel en mayúsculas espaciadas, texto en
// Nunito, paneles noche con borde y tarjetas con brillo. La letra helada sigue disponible (IceMaterial).
public static class FrostUI
{
    // Los colores viven en FrostboundUI (un solo origen); aquí solo se les da el nombre que usan los menús.
    public static Color IceTop => Color.white;
    public static Color IceBottom => FrostboundUI.IceDeep;
    public static Color Outline => FrostboundUI.OutlineNight;
    public static Color InnerOutline => FrostboundUI.InnerOutline;
    public static Color PanelColor => FrostboundUI.MenuPanel;
    public static Color PanelSolid => FrostboundUI.MenuPanelSolid;
    public static Color Border => FrostboundUI.MenuBorder;
    public static Color BorderDim => FrostboundUI.Border;
    public static Color Ice => FrostboundUI.Ice;
    public static Color Secondary => FrostboundUI.Muted;
    public static Color Gold => FrostboundUI.MenuGold;
    public static Color Danger => FrostboundUI.Danger;
    public static Color TextColor => Color.white;
    public static Color Disabled => FrostboundUI.MenuDisabled;
    public static Color Label => FrostboundUI.MenuLabel;

    public static Color Hex(string h) => FrostboundUI.Hex(h);

    private static UISkin _skin;
    public static UISkin Skin => _skin != null ? _skin : (_skin = GameDatabase.Instance != null ? GameDatabase.Instance.uiSkin : null);
    // Títulos: Cinzel ExtraBold. Etiquetas y botones: Cinzel SemiBold. Si faltan, Lilita o Nunito.
    public static TMP_FontAsset Display => Skin == null ? null : Skin.cinzel800 != null ? Skin.cinzel800 : Skin.lilita != null ? Skin.lilita : Skin.nunito800;
    public static TMP_FontAsset Heading => Skin == null ? null : Skin.cinzel600 != null ? Skin.cinzel600 : Display;
    public static TMP_FontAsset Body => Skin != null ? Skin.nunito800 : null;
    public static TMP_FontAsset BodyLight => Skin != null ? Skin.nunito700 : null;

    // ---------- Materiales de texto ----------

    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

    // Letra helada: contorno azul por dentro y sombra (underlay) noche por fuera.
    public static Material IceMaterial(TMP_FontAsset font, float outline = 0.22f, float shadow = 0.55f)
    {
        string key = (font != null ? font.name : "") + "_hielo_" + outline + "_" + shadow;
        if (Materials.TryGetValue(key, out Material m) && m != null) return m;
        m = new Material(font.material) { name = key };
        m.EnableKeyword(ShaderUtilities.Keyword_Outline);
        m.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
        m.SetColor(ShaderUtilities.ID_OutlineColor, InnerOutline);
        m.SetFloat(ShaderUtilities.ID_FaceDilate, outline * 0.6f);
        m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        m.SetColor(ShaderUtilities.ID_UnderlayColor, Outline);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, Mathf.Min(1f, outline + shadow));
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.15f);
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.55f);
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.05f);
        Materials[key] = m;
        return m;
    }

    // Letra de oro viejo: contorno bronce oscuro por dentro y sombra abismo por fuera (títulos).
    public static Material GoldMaterial(TMP_FontAsset font, float outline = 0.18f, float shadow = 0.45f)
    {
        string key = (font != null ? font.name : "") + "_oro_" + outline + "_" + shadow;
        if (Materials.TryGetValue(key, out Material m) && m != null) return m;
        m = new Material(font.material) { name = key };
        m.EnableKeyword(ShaderUtilities.Keyword_Outline);
        m.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
        m.SetColor(ShaderUtilities.ID_OutlineColor, FrostboundUI.OldGoldDeep);
        m.SetFloat(ShaderUtilities.ID_FaceDilate, outline * 0.5f);
        m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        m.SetColor(ShaderUtilities.ID_UnderlayColor, FrostboundUI.Abyss);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, Mathf.Min(1f, outline + shadow));
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.1f);
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.45f);
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.25f);
        Materials[key] = m;
        return m;
    }

    // Texto normal blanco con contorno azul noche.
    public static Material BodyMaterial(TMP_FontAsset font)
    {
        string key = (font != null ? font.name : "") + "_cuerpo";
        if (Materials.TryGetValue(key, out Material m) && m != null) return m;
        m = new Material(font.material) { name = key };
        m.EnableKeyword(ShaderUtilities.Keyword_Outline);
        m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);
        m.SetColor(ShaderUtilities.ID_OutlineColor, Outline);
        m.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
        Materials[key] = m;
        return m;
    }

    // ---------- Construcción ----------

    public static RectTransform Node(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
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

    public static Image Img(Transform parent, string name, Sprite sprite, Color color, bool sliced = true)
    {
        RectTransform rt = Node(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = sliced && sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, TMP_FontAsset font = null, float tracking = 0f)
    {
        RectTransform rt = Node(parent, name);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font != null ? font : Body;
        if (t.font != null) t.fontSharedMaterial = BodyMaterial(t.font);
        t.fontSize = size;
        t.color = color;
        t.text = text;
        t.alignment = align;
        t.characterSpacing = tracking;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    // Título con letra de oro viejo (degradado vertical de oro claro a oro viejo).
    public static TextMeshProUGUI Title(Transform parent, string name, string text, float size, TextAlignmentOptions align = TextAlignmentOptions.Center, float outline = 0.22f)
    {
        TextMeshProUGUI t = Text(parent, name, text, size, Color.white, align, Display);
        if (t.font != null) t.fontSharedMaterial = GoldMaterial(t.font, outline * 0.8f);
        t.enableVertexGradient = true;
        t.colorGradient = new VertexGradient(FrostboundUI.OldGoldLight, FrostboundUI.OldGoldLight, FrostboundUI.OldGold, FrostboundUI.OldGold);
        t.characterSpacing = FrostboundUI.Tracking(size * 0.04f, size);
        return t;
    }

    // Etiqueta pequeña en mayúsculas espaciadas ("TÍTULO PEQUEÑO · ETIQUETA").
    public static TextMeshProUGUI Kicker(Transform parent, string name, string text, float size = 15f, Color? color = null)
    {
        TextMeshProUGUI t = Text(parent, name, text.ToUpperInvariant(), size, color ?? FrostboundUI.Ash, TextAlignmentOptions.MidlineLeft, Heading, 22f);
        return t;
    }

    // Panel con marco de hierro forjado (FrostFrames). Devuelve el marco (raíz); "fill" es la placa de dentro, teñida
    // con el color de relleno. "radius" se conserva por compatibilidad y elige el grosor del marco: grande para
    // paneles, medio para cajas y fino para chips. Un color de borde tiñe el hierro (oro en chips, sangre en avisos).
    public static Image Panel(Transform parent, string name, out Image fill, float radius = 16f, Color? border = null, Color? fillColor = null)
    {
        bool large = radius >= 16f, medium = radius >= 11f;
        Sprite frame = large ? FrostFrames.Panel : medium ? FrostFrames.Button(FrostFrames.Metal.Iron) : FrostFrames.Slot;
        float inset = large ? 11f : medium ? 7f : 5f;
        Image b = Img(parent, name, frame, border.HasValue ? Tint(border.Value) : Color.white);
        fill = Img(b.transform, "Relleno", FrostFrames.Plate, fillColor ?? PanelColor);
        Stretch(fill.rectTransform, inset);
        fill.transform.SetAsFirstSibling();
        return b;
    }

    // Los bordes de antes eran colores planos; sobre el hierro se aclaran para teñirlo sin oscurecerlo del todo.
    private static Color Tint(Color c) => Color.Lerp(c, Color.white, 0.35f);

    // ---------- Sprites generados ----------

    private static Sprite _glow, _diamondFilled, _diamondOutline, _dashed, _soft;
    private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();

    // Halo suave de rectángulo redondeado (9-slice) para el brillo de botones y tarjetas.
    public static Sprite Glow
    {
        get
        {
            if (_glow != null) return _glow;
            const int n = 64, r = 24;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Brillo" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - n * 0.5f) - (n * 0.5f - r)), dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - n * 0.5f) - (n * 0.5f - r));
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            _glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _glow;
        }
    }

    // Degradado vertical suave (para franjas oscuras y nieblas de la interfaz).
    public static Sprite SoftFade
    {
        get
        {
            if (_soft != null) return _soft;
            const int n = 64;
            var tex = new Texture2D(4, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Degradado" };
            for (int y = 0; y < n; y++)
            {
                float a = Mathf.Sin((y + 0.5f) / n * Mathf.PI);
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _soft = Sprite.Create(tex, new Rect(0, 0, 4, n), new Vector2(0.5f, 0.5f));
            return _soft;
        }
    }

    public static Sprite Diamond(bool filled)
    {
        if (filled && _diamondFilled != null) return _diamondFilled;
        if (!filled && _diamondOutline != null) return _diamondOutline;
        Sprite s = Draw(48, (u, v) =>
        {
            float d = (Mathf.Abs(u) + Mathf.Abs(v)) * 0.7071f - 0.55f;
            return filled ? d : Mathf.Abs(d + 0.07f) - 0.07f;
        });
        if (filled) _diamondFilled = s; else _diamondOutline = s;
        return s;
    }

    // Borde discontinuo (tarjeta de "Ranura libre").
    public static Sprite Dashed
    {
        get
        {
            if (_dashed != null) return _dashed;
            const int n = 64, r = 14, w = 3;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Discontinuo" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    bool ring = d > r - w && d <= r;
                    bool dash = ((x / 6) + (y / 6)) % 2 == 0;
                    px[y * n + x] = ring && dash ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            _dashed = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            return _dashed;
        }
    }

    // Iconos de línea de las clases y del menú (escudo, sombrero de mago, shuriken, hacha, flecha, girar).
    public static Sprite Icon(string id)
    {
        if (Icons.TryGetValue(id, out Sprite s) && s != null) return s;
        System.Func<float, float, float> f;
        const float t = 0.075f;
        switch (id)
        {
            case "Knight":
                f = (u, v) =>
                {
                    // Escudo: rectángulo con punta abajo, contorno y una cruz.
                    float shield = Mathf.Max(Mathf.Abs(u) - 0.62f + Mathf.Max(0f, -v - 0.1f) * 0.75f, Mathf.Max(v - 0.72f, -v - 0.86f + Mathf.Abs(u) * 0.35f));
                    float outline = Mathf.Abs(shield) - t;
                    float cross = Mathf.Min(Mathf.Max(Mathf.Abs(u) - t, Mathf.Abs(v - 0.05f) - 0.38f), Mathf.Max(Mathf.Abs(v - 0.05f) - t, Mathf.Abs(u) - 0.32f));
                    return Mathf.Min(outline, cross);
                };
                break;
            case "Mage":
                f = (u, v) =>
                {
                    float hat = Mathf.Max(Mathf.Abs(u) * 1.25f + v * 0.62f - 0.5f, -v - 0.42f);
                    float brim = Mathf.Max(Mathf.Abs(u) - 0.85f, Mathf.Abs(v + 0.52f) - 0.06f);
                    float star = Mathf.Abs(u) + Mathf.Abs(v + 0.05f) - 0.16f;
                    return Mathf.Min(Mathf.Min(Mathf.Abs(hat) - t, brim), Mathf.Abs(star) - t * 0.6f);
                };
                break;
            case "Ninja":
                f = (u, v) =>
                {
                    // Shuriken de cuatro puntas con centro hueco.
                    float a = Mathf.Abs(u), b = Mathf.Abs(v);
                    float star = Mathf.Min(Mathf.Max(a * 3.2f + b * 0.6f - 0.95f, b - 0.95f), Mathf.Max(b * 3.2f + a * 0.6f - 0.95f, a - 0.95f));
                    float hole = Mathf.Sqrt(u * u + v * v) - 0.12f;
                    return Mathf.Max(Mathf.Min(Mathf.Abs(star) - t, Mathf.Abs(hole) - t * 0.8f), -10f);
                };
                break;
            case "Viking":
                f = (u, v) =>
                {
                    // Hacha: mango vertical y hoja curva a la derecha.
                    float handle = Mathf.Max(Mathf.Abs(u + 0.35f) - t, Mathf.Abs(v) - 0.88f);
                    float blade = Mathf.Max(Mathf.Sqrt((u + 0.6f) * (u + 0.6f) + (v - 0.35f) * (v - 0.35f)) - 0.95f, Mathf.Max(-(u + 0.35f), Mathf.Abs(v - 0.35f) - 0.55f));
                    return Mathf.Min(handle, Mathf.Abs(blade) - t);
                };
                break;
            case "Back":
                f = (u, v) => Mathf.Max(Mathf.Abs(Mathf.Abs(v) * 0.9f + u * 0.9f) - t * 1.4f, Mathf.Max(-u - 0.4f, u - 0.35f));
                break;
            case "Shield":
                f = (u, v) =>
                {
                    float shield = Mathf.Max(Mathf.Abs(u) - 0.62f + Mathf.Max(0f, -v - 0.1f) * 0.75f, Mathf.Max(v - 0.72f, -v - 0.86f + Mathf.Abs(u) * 0.35f));
                    float cross = Mathf.Min(Mathf.Max(Mathf.Abs(u) - t, Mathf.Abs(v - 0.05f) - 0.36f), Mathf.Max(Mathf.Abs(v - 0.05f) - t, Mathf.Abs(u) - 0.3f));
                    return Mathf.Min(Mathf.Abs(shield) - t, cross);
                };
                break;
            case "Rotate":
                f = (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v);
                    float ring = Mathf.Abs(r - 0.6f) - t;
                    float gap = Mathf.Max(u - 0.05f, v - 0.05f);
                    float arrow = Mathf.Max(Mathf.Abs(u - 0.6f) * 1.2f + (v - 0.05f) * 0.9f - 0.25f, -(v - 0.05f) - 0.02f);
                    return Mathf.Min(Mathf.Max(ring, -gap), arrow);
                };
                break;
            case "Golem":
                f = (u, v) =>
                {
                    // Muñeco de nieve: tres bloques apilados (contorno) con brazos de rama.
                    float b1 = Mathf.Abs(Mathf.Max(Mathf.Abs(u) - 0.42f, Mathf.Abs(v + 0.55f) - 0.3f)) - t;
                    float b2 = Mathf.Abs(Mathf.Max(Mathf.Abs(u) - 0.32f, Mathf.Abs(v + 0.02f) - 0.23f)) - t;
                    float b3 = Mathf.Abs(Mathf.Max(Mathf.Abs(u) - 0.25f, Mathf.Abs(v - 0.47f) - 0.2f)) - t;
                    float arms = Mathf.Max(Mathf.Abs(Mathf.Abs(u) - 0.55f + (v - 0.05f) * 0.6f) - t * 0.8f, Mathf.Abs(Mathf.Abs(u) - 0.55f) - 0.22f);
                    return Mathf.Min(Mathf.Min(b1, b2), Mathf.Min(b3, arms));
                };
                break;
            case "Plus":
                f = (u, v) => Mathf.Min(Mathf.Max(Mathf.Abs(u) - t * 1.2f, Mathf.Abs(v) - 0.6f), Mathf.Max(Mathf.Abs(v) - t * 1.2f, Mathf.Abs(u) - 0.6f));
                break;
            default:
                f = (u, v) => Mathf.Sqrt(u * u + v * v) - 0.6f;
                break;
        }
        s = Draw(96, f);
        Icons[id] = s;
        return s;
    }

    private static Sprite Draw(int n, System.Func<float, float, float> distance)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        float aa = 2.2f / n;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(0.5f - distance(u, v) / aa);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }
}
