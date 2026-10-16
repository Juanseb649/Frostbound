using System.Collections.Generic;
using UnityEngine;

// Resolución, modo de pantalla y HUD en ultrawide. Se guarda en PlayerPrefs y se aplica al arrancar.
public static class DisplaySettings
{
    private const string KeyW = "display_width", KeyH = "display_height", KeyMode = "display_mode", KeyHud = "display_hud_centered";

    public struct Res
    {
        public int w, h;
        public Res(int w, int h) { this.w = w; this.h = h; }
        public float Aspect => h > 0 ? w / (float)h : 0f;
        public string AspectName
        {
            get
            {
                float a = Aspect;
                if (a > 3.3f) return "32:9";
                if (a > 2.3f) return "21:9";
                if (a > 1.7f) return "16:9";
                if (a > 1.55f) return "16:10";
                return a > 1.3f ? "4:3" : "5:4";
            }
        }
        public override string ToString() => w + " × " + h;
    }

    // De la más baja a 4K, más las ultrawide (21:9 y 32:9) y las que tenga el monitor.
    private static readonly Res[] Standard =
    {
        new Res(1280, 720), new Res(1280, 800), new Res(1366, 768), new Res(1440, 900), new Res(1600, 900),
        new Res(1680, 1050), new Res(1920, 1080), new Res(1920, 1200), new Res(2560, 1080), new Res(2560, 1440),
        new Res(2560, 1600), new Res(3440, 1440), new Res(3840, 1080), new Res(3840, 1600), new Res(3840, 2160),
        new Res(5120, 1440), new Res(5120, 2160)
    };

    public static List<Res> Resolutions()
    {
        var list = new List<Res>(Standard);
        foreach (Resolution r in Screen.resolutions)
            if (r.width >= 1024 && !list.Exists(x => x.w == r.width && x.h == r.height)) list.Add(new Res(r.width, r.height));
        Res native = Native;
        if (native.w > 0 && !list.Exists(x => x.w == native.w && x.h == native.h)) list.Add(native);
        list.Sort((a, b) => (a.w * a.h).CompareTo(b.w * b.h) != 0 ? (a.w * a.h).CompareTo(b.w * b.h) : a.w.CompareTo(b.w));
        return list;
    }

    public static Res Native => new Res(Display.main.systemWidth, Display.main.systemHeight);

    public static Res Current
    {
        get
        {
            int w = PlayerPrefs.GetInt(KeyW, 0), h = PlayerPrefs.GetInt(KeyH, 0);
            if (w > 0 && h > 0) return new Res(w, h);
            Res n = Native;
            return n.w > 0 ? n : new Res(Screen.width, Screen.height);
        }
    }

    public static FullScreenMode Mode
    {
        get => (FullScreenMode)PlayerPrefs.GetInt(KeyMode, (int)FullScreenMode.FullScreenWindow);
    }

    public static bool HudCentered
    {
        get => PlayerPrefs.GetInt(KeyHud, 0) == 1;
        set { PlayerPrefs.SetInt(KeyHud, value ? 1 : 0); PlayerPrefs.Save(); ScreenAdapter.Refresh(); }
    }

    public static string ModeName(FullScreenMode m)
    {
        switch (m)
        {
            case FullScreenMode.ExclusiveFullScreen: return "Pantalla completa";
            case FullScreenMode.FullScreenWindow: return "Sin bordes";
            default: return "Ventana";
        }
    }

    public static readonly FullScreenMode[] Modes = { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };

    public static void Apply(Res r, FullScreenMode mode)
    {
        PlayerPrefs.SetInt(KeyW, r.w);
        PlayerPrefs.SetInt(KeyH, r.h);
        PlayerPrefs.SetInt(KeyMode, (int)mode);
        PlayerPrefs.Save();
        if (!Application.isEditor) Screen.SetResolution(r.w, r.h, mode);
        ScreenAdapter.Refresh();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySaved()
    {
        if (Application.isEditor) return;
        int w = PlayerPrefs.GetInt(KeyW, 0), h = PlayerPrefs.GetInt(KeyH, 0);
        // Primera vez: la resolución nativa del monitor, sin bordes (así un ultrawide arranca a su tamaño real).
        Res r = w > 0 && h > 0 ? new Res(w, h) : Native;
        if (r.w > 0) Screen.SetResolution(r.w, r.h, Mode);
    }
}
