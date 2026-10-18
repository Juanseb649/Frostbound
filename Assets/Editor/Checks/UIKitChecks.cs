using System.IO;
using UnityEngine;

// Comprobaciones del kit de UI (FrostFrames). Por el bridge: exec UIKitChecks.DumpAtlas
public static class UIKitChecks
{
    // Contraste WCAG de los pares de texto y fondo principales (≥ 7 texto principal, ≥ 4,5 secundario).
    public static string Contrast()
    {
        var pairs = new (string name, Color fg, Color bg, float min)[]
        {
            ("Hueso sobre pizarra", FrostboundUI.Bone, FrostboundUI.Slate, 7f),
            ("Hueso sobre noche", FrostboundUI.Bone, FrostboundUI.Bg, 7f),
            ("Blanco sobre panel", Color.white, FrostboundUI.MenuPanelSolid, 7f),
            ("Oro claro sobre pizarra", FrostboundUI.OldGoldLight, FrostboundUI.Slate, 4.5f),
            ("Oro viejo sobre noche", FrostboundUI.OldGold, FrostboundUI.Bg, 4.5f),
            ("Ceniza sobre noche", FrostboundUI.Ash, FrostboundUI.Bg, 4.5f),
            ("Etiqueta sobre panel", FrostboundUI.MenuLabel, FrostboundUI.MenuPanelSolid, 4.5f),
            ("Apagado sobre panel", FrostboundUI.Muted, FrostboundUI.MenuPanelSolid, 4.5f),
        };
        var sb = new System.Text.StringBuilder();
        foreach (var p in pairs)
        {
            float r = Ratio(p.fg, p.bg);
            sb.Append(p.name).Append(' ').Append(r.ToString("0.0")).Append(r >= p.min ? " ok" : " BAJO").Append(" | ");
        }
        return sb.ToString();
    }

    private static float Ratio(Color a, Color b)
    {
        float la = Luminance(a), lb = Luminance(b);
        return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
    }

    private static float Luminance(Color c)
    {
        float Ch(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        return 0.2126f * Ch(c.r) + 0.7152f * Ch(c.g) + 0.0722f * Ch(c.b);
    }

    // Abre Configuración en la pestaña General (accesibilidad incluida).
    public static string OpenGeneral()
    {
        OptionsPanel.Open();
        var p = Object.FindAnyObjectByType<OptionsPanel>();
        var tabType = typeof(OptionsPanel).GetNestedType("Tab", System.Reflection.BindingFlags.NonPublic);
        typeof(OptionsPanel).GetMethod("ShowTab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(p, new object[] { System.Enum.Parse(tabType, "General") });
        return "general abierta";
    }

    public static string TextLarge() { AccessibilitySettings.TextStep = 2; return "texto " + AccessibilitySettings.TextScale; }
    public static string TextNormal() { AccessibilitySettings.TextStep = 0; return "texto " + AccessibilitySettings.TextScale; }

    // Rendimiento de la UI: exec UIKitChecks.PerfStart → esperar → exec UIKitChecks.PerfResult.
    public static string PerfStart() { UIPerfProbe.Begin(120); return "sonda en marcha (120 fotogramas)"; }
    public static string PerfResult() => UIPerfProbe.Running ? "aún midiendo" : UIPerfProbe.Last;

    // Imágenes y textos que reciben clics sin necesitarlo (sin Selectable ni manejador en ellos ni en sus padres).
    public static string RaycastAudit()
    {
        int total = 0, needless = 0;
        var sample = new System.Collections.Generic.List<string>();
        foreach (var g in Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsInactive.Exclude))
        {
            if (!g.raycastTarget) continue;
            total++;
            bool useful = g.GetComponentInParent<UnityEngine.UI.Selectable>() != null
                || g.GetComponentInParent<UnityEngine.EventSystems.IEventSystemHandler>() != null
                || g.GetComponent<UnityEngine.UI.Image>() is var img && img != null && img.color.a > 0.3f && g.rectTransform.rect.width > 1000f;
            if (useful) continue;
            needless++;
            if (sample.Count < 8) sample.Add(g.transform.parent != null ? g.transform.parent.name + "/" + g.name : g.name);
        }
        return "raycast activos=" + total + " innecesarios=" + needless + " ej: " + string.Join(", ", sample);
    }

    // Dónde cae la mansión modelada (para comprobar ejes y escala del FBX): límites en el mundo.
    public static string ManorBounds()
    {
        var m = GameObject.Find("Mansión modelada");
        if (m == null) return "sin mansión modelada";
        Bounds b = default;
        bool first = true;
        int renderers = 0;
        foreach (var r in m.GetComponentsInChildren<Renderer>())
        {
            renderers++;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        var vela = m.transform.Find("Mansion_Vela");
        string candle = vela != null ? vela.GetComponent<Renderer>().bounds.center.ToString("F1") : "-";
        return "renderers=" + renderers + " min=" + b.min.ToString("F1") + " max=" + b.max.ToString("F1") + " vela=" + candle + " (esperado ≈ 9.4, 5.6, 16.4)";
    }

    // Guarda la textura de marcos en FrostboundBridge/ui_atlas.png y resume el alfa de la placa.
    public static string DumpAtlas()
    {
        Texture2D tex = FrostFrames.AtlasTexture;
        string file = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "FrostboundBridge", "ui_atlas.png");
        File.WriteAllBytes(file, tex.EncodeToPNG());
        Rect r = FrostFrames.Plate.textureRect;
        Color center = tex.GetPixel((int)r.center.x, (int)r.center.y);
        return file + " placa_centro=" + center;
    }
}
