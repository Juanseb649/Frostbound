using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Pruebas de Configuración → Control (exec ControlsChecks.X).
public static class ControlsChecks
{
    private static Gamepad Pad
    {
        get
        {
            foreach (Gamepad g in Gamepad.all) if (g.name.StartsWith("PruebaCoop")) return g;
            return InputSystem.AddDevice<Gamepad>("PruebaCoop");
        }
    }

    private static Keyboard Kb { get { foreach (var d in InputSystem.devices) if (d is Keyboard k && d.name.StartsWith("PruebaTeclado")) return k; return InputSystem.AddDevice<Keyboard>("PruebaTeclado"); } }

    public static string Open() { OptionsPanel.Open(); return Describe(); }
    public static string Describe() => OptionsPanel.IsOpen ? Object.FindAnyObjectByType<OptionsPanel>().Describe() : "cerrado";
    public static string Close() { var p = Object.FindAnyObjectByType<OptionsPanel>(); if (p != null) p.Close(); return "cerrado"; }

    public static string UsePad() { InputMap.Preferred = InputMap.Device.Gamepad; return Describe(); }
    public static string UseKeyboard() { InputMap.Preferred = InputMap.Device.KeyboardMouse; return Describe(); }
    public static string Reset() { InputMap.ResetDefaults(InputMap.Device.Gamepad); InputMap.ResetDefaults(InputMap.Device.KeyboardMouse); return "restablecido"; }

    public static string CaptureAbility() { Object.FindAnyObjectByType<OptionsPanel>().TestBeginCapture(GameAction.Ability); return "esperando"; }

    public static string PressLB() { InputSystem.QueueStateEvent(Pad, new GamepadState().WithButton(GamepadButton.LeftShoulder)); return "LB"; }
    public static string ReleasePad() { InputSystem.QueueStateEvent(Pad, new GamepadState()); return "suelto"; }
    public static string PressG() { InputSystem.QueueStateEvent(Kb, new KeyboardState(Key.G)); return "G"; }
    public static string ReleaseKeys() { InputSystem.QueueStateEvent(Kb, new KeyboardState()); return "suelto"; }

    public static string Bindings()
    {
        return "dispositivo=" + InputMap.Preferred + " habilidad=" + InputMap.KeyName(InputMap.KeyOf(GameAction.Ability)) + "/" + InputMap.PadName(InputMap.PadOf(GameAction.Ability))
            + " poción=" + InputMap.KeyName(InputMap.KeyOf(GameAction.QuickPotion)) + "/" + InputMap.PadName(InputMap.PadOf(GameAction.QuickPotion))
            + " mapa=" + InputMap.KeyName(InputMap.KeyOf(GameAction.Map)) + "/" + InputMap.PadName(InputMap.PadOf(GameAction.Map));
    }

    public static string P1Pad()
    {
        var p1 = Players.Get(0);
        return "mando_j1=" + (PlayerController.PadOf(p1) != null ? PlayerController.PadOf(p1).name : "-") + " modo_mando=" + (p1 != null && p1.IsPadPlayer) + " pos=" + (p1 != null ? p1.transform.position.ToString("F1") : "-");
    }

    public static string StickRight() { InputSystem.QueueStateEvent(Pad, new GamepadState { leftStick = new Vector2(1f, 0f) }); return "stick"; }

    public static string HudKeys()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var h in Object.FindObjectsByType<GameHUD>())
        {
            int glyphs = h.GetComponentsInChildren<RectTransform>(true).Count(t => t.name == "TeclaGlifo");
            sb.Append(h.name).Append(" glifos=").Append(glyphs).Append(" | ");
        }
        return sb.ToString();
    }

    public static string PauseLegend()
    {
        var pm = Object.FindAnyObjectByType<PauseMenu>();
        if (pm == null) return "sin pausa";
        pm.Pause();
        var legend = pm.transform.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "Controles");
        string s = legend == null ? "sin leyenda" : "leyenda=" + string.Join(",", legend.Cast<Transform>().Select(c => c.childCount > 0 ? c.GetChild(0).name.Replace("Glifo_", "") : "-"));
        pm.Resume();
        return s;
    }

    public static string InputDebug()
    {
        var kb = Keyboard.current;
        var m = Mouse.current;
        return "kb=" + (kb != null ? kb.name : "-") + " anyKey=" + (kb != null && kb.anyKey.isPressed) + " g=" + (kb != null && kb.gKey.isPressed)
            + " mouse=" + (m != null ? m.name + " L=" + m.leftButton.isPressed + " R=" + m.rightButton.isPressed : "-") + " held=" + InputMap.AnyKeyHeld();
    }

    public static string SaveFolder()
    {
        string f = SaveSystem.Folder;
        string list = System.IO.Directory.Exists(f) ? string.Join(",", System.IO.Directory.GetFiles(f).Select(System.IO.Path.GetFileName)) : "no existe";
        return f + " -> " + list;
    }
}
