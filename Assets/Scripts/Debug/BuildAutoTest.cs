using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

// Autoprueba de la build de desarrollo (sin intervención). Solo actúa si el ejecutable se lanza con
// «-autotest <carpeta>» (lo hace ReleaseBuilder.AutoTest*): mide la UI con UIPerfProbe en el título, en la partida
// y con el inventario abierto, saca capturas, abre y cierra la pausa, y escribe informe.txt con los resultados y las
// excepciones que hayan salido. Después cierra el juego. Solo existe en el editor y en builds de desarrollo.
public class BuildAutoTest : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private const int Frames = 300;
    private string _dir;
    private int _width, _height;
    private readonly StringBuilder _report = new StringBuilder();
    private int _errors;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isEditor) return;
        string[] args = System.Environment.GetCommandLineArgs();
        string dir = Arg(args, "-autotest");
        if (dir == null) return;
        var go = new GameObject("Autoprueba de la build");
        DontDestroyOnLoad(go);
        var t = go.AddComponent<BuildAutoTest>();
        t._dir = Path.GetFullPath(dir);
        int.TryParse(Arg(args, "-screen-width"), out t._width);
        int.TryParse(Arg(args, "-screen-height"), out t._height);
        t._bisect = System.Array.IndexOf(args, "-autotest-bisect") >= 0;
    }

    private static string Arg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
        return null;
    }

    void OnEnable() => Application.logMessageReceived += OnLog;
    void OnDisable() => Application.logMessageReceived -= OnLog;

    private void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        _errors++;
        if (_errors <= 10) _report.AppendLine("  [" + type + "] " + message.Split('\n')[0]);
    }

    IEnumerator Start()
    {
        Directory.CreateDirectory(_dir);
        // DisplaySettings aplica la resolución guardada al arrancar: se vuelve a poner la pedida, en ventana.
        if (_width > 0 && _height > 0) Screen.SetResolution(_width, _height, FullScreenMode.Windowed);
        yield return Wait(4f);
        _report.AppendLine("Resolución pedida " + _width + "×" + _height + " · real " + Screen.width + "×" + Screen.height + " · versión " + Application.version);

        yield return Measure("titulo");
        yield return PadTour();

        int slot = LastSlot();
        FrostMenu menu = FindAnyObjectByType<FrostMenu>();
        if (slot < 0 || menu == null)
        {
            _report.AppendLine("Sin partidas guardadas (o sin menú): solo se probó el título.");
        }
        else
        {
            menu.LoadSlot(slot);
            yield return Wait(2f);
            for (float t = 0f; t < 60f && LoadingScreen.Active; t += Time.unscaledDeltaTime) yield return null;
            yield return Wait(3f);
            yield return Measure("partida");
            if (_bisect) yield return Bisect();

            InventoryScreen inv = FindAnyObjectByType<InventoryScreen>();
            if (inv != null)
            {
                inv.OpenTab(InventoryScreen.Tab.Inventory);
                yield return Wait(1f);
                yield return Measure("inventario");
                inv.Close();
                yield return Wait(1f);
            }
            yield return CoopTour();
            PauseMenu pause = FindAnyObjectByType<PauseMenu>();
            if (pause != null)
            {
                pause.Pause();
                yield return Wait(1f);
                Shot("pausa");
                pause.Resume();
                yield return Wait(1f);
            }
        }

        _report.AppendLine("Errores y excepciones: " + _errors);
        File.WriteAllText(Path.Combine(_dir, "informe.txt"), _report.ToString());
        yield return Wait(0.5f);
        Application.Quit();
    }

    // Con «-autotest-bisect»: apaga por turnos cada lienzo de UI de la partida y mide sin él, para saber cuánta
    // basura (GC.Alloc) pone cada uno. «sin UI» apaga todos a la vez: lo que queda es del resto del juego.
    private bool _bisect;

    private IEnumerator Bisect()
    {
        var canvases = new System.Collections.Generic.List<Canvas>();
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            if (c.isRootCanvas && c.enabled) canvases.Add(c);
        foreach (Canvas c in canvases)
        {
            GameObject owner = c.gameObject;
            owner.SetActive(false);
            yield return Wait(0.5f);
            yield return Measure("sin " + owner.name, false);
            owner.SetActive(true);
            yield return Wait(0.5f);
        }
        foreach (Canvas c in canvases) c.gameObject.SetActive(false);
        yield return Wait(0.5f);
        yield return Measure("sin UI", false);
        foreach (Canvas c in canvases) c.gameObject.SetActive(true);
        yield return Wait(1f);
    }

    private IEnumerator Measure(string name, bool shot = true)
    {
        if (!shot) { yield return MeasureOnly(name); yield break; }
        Shot(name);
        yield return Wait(0.5f);
        UIPerfProbe.Begin(Frames);
        while (UIPerfProbe.Running) yield return null;
        _report.AppendLine(name + ": " + UIPerfProbe.Last);
    }

    // Recorrido del título con un mando virtual: anota qué botón tiene el foco tras cada pulsación.
    // (En el editor no sirve: Unity solo pasa la entrada al juego si la ventana del editor tiene el foco.)
    private IEnumerator PadTour()
    {
        var pad = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>("MandoAutoprueba");
        var line = new StringBuilder("mando: inicio=" + Focus());
        // Solo flechas por el título (sin A: podría caer en SALIR y cerrar el juego). Luego se pone el foco en
        // OPCIONES a mano y se prueban A (abre), flechas dentro y B (cierra).
        var steps = new (string name, UnityEngine.InputSystem.LowLevel.GamepadButton b)[]
        {
            ("abajo", UnityEngine.InputSystem.LowLevel.GamepadButton.DpadDown),
            ("derecha", UnityEngine.InputSystem.LowLevel.GamepadButton.DpadRight),
            ("derecha", UnityEngine.InputSystem.LowLevel.GamepadButton.DpadRight),
            ("izquierda", UnityEngine.InputSystem.LowLevel.GamepadButton.DpadLeft),
            ("arriba", UnityEngine.InputSystem.LowLevel.GamepadButton.DpadUp),
            ("@opciones", default),
            ("A", UnityEngine.InputSystem.LowLevel.GamepadButton.South),
            ("abajo", UnityEngine.InputSystem.LowLevel.GamepadButton.DpadDown),
            ("abajo", UnityEngine.InputSystem.LowLevel.GamepadButton.DpadDown),
            ("B", UnityEngine.InputSystem.LowLevel.GamepadButton.East),
        };
        foreach (var s in steps)
        {
            if (s.name == "@opciones")
            {
                GameObject opt = GameObject.Find("Opciones");
                if (opt != null && UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(opt);
                line.Append(" · [foco puesto en ").Append(Focus()).Append("]");
                continue;
            }
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(s.b));
            yield return Wait(0.15f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.LowLevel.GamepadState());
            yield return Wait(0.45f);
            line.Append(" · ").Append(s.name).Append("→").Append(Focus());
            if (s.name == "A") line.Append(OptionsPanel.IsOpen ? " (opciones abiertas)" : " (opciones NO abiertas)");
            Flush(line.ToString());
        }
        line.Append(OptionsPanel.IsOpen ? " · opciones siguen abiertas" : " · opciones cerradas");
        _report.AppendLine(line.ToString());
        UnityEngine.InputSystem.InputSystem.RemoveDevice(pad);
        yield return Wait(0.5f);
    }

    // Co-op: entra un jugador 2 con un mando virtual (como CoopChecks.Join), se capturan los dos HUD y sale.
    // ReleaseBuilder respalda y restaura las partidas, porque al salir el compañero se guarda en la partida.
    private IEnumerator CoopTour()
    {
        CoopManager cm = CoopManager.Instance;
        GameDatabase db = GameDatabase.Instance;
        if (cm == null || db == null || db.classes.Count < 2) { _report.AppendLine("co-op: sin CoopManager o clases"); yield break; }
        var pad = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>("MandoCoop");
        CharacterClass cls = db.classes.Find(c => c.className == "Mage") ?? db.classes[1];
        PlayerController p2 = cm.Spawn(pad, new CompanionData { classId = cls.name, plumageId = db.palette.entries[2].id, heroName = "Pingu Dos" }, false);
        yield return Wait(2f);
        int huds = FindObjectsByType<GameHUD>(FindObjectsInactive.Exclude).Length;
        _report.AppendLine("co-op: jugador 2 " + (p2 != null ? "entró" : "NO entró") + " · HUD=" + huds);
        Shot("coop");
        yield return Wait(0.5f);
        cm.Leave();
        UnityEngine.InputSystem.InputSystem.RemoveDevice(pad);
        yield return Wait(1f);
    }

    // Escribe el informe tal como va (más una línea en curso), por si el juego se cierra antes de terminar.
    private void Flush(string pending = null)
    {
        File.WriteAllText(Path.Combine(_dir, "informe.txt"), _report + (pending != null ? pending + " …\n" : ""));
    }

    private static string Focus()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        GameObject go = es != null ? es.currentSelectedGameObject : null;
        return go != null ? go.name : "(ninguno)";
    }

    private IEnumerator MeasureOnly(string name)
    {
        UIPerfProbe.Begin(Frames);
        while (UIPerfProbe.Running) yield return null;
        _report.AppendLine(name + ": " + UIPerfProbe.Last);
    }

    private void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));

    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    // La ranura guardada más reciente (como el botón Jugar del título).
    private static int LastSlot()
    {
        int best = -1;
        System.DateTime bestTime = System.DateTime.MinValue;
        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            if (!SaveSystem.Exists(i)) continue;
            SaveData d = SaveSystem.Load(i);
            if (d == null) continue;
            System.DateTime.TryParse(d.savedUtc, out System.DateTime t);
            if (best < 0 || t > bestTime) { best = i; bestTime = t; }
        }
        return best;
    }
#endif
}
