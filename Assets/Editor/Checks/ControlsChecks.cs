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

    public static string HoldW() { InputSystem.QueueStateEvent(Kb, new KeyboardState(Key.W)); return "W"; }

    public static string Positions()
    {
        return string.Join(" | ", Players.All.Select(p => p.name + " " + p.transform.position.ToString("F1") + " pad=" + p.IsPadPlayer + " teclado=" + PlayerController.UsesKeyboard(p) + " activo=" + p.enabled));
    }

    // Quita los dispositivos virtuales de las pruebas (si no, siguen conectados en el editor y confunden al jugador).
    public static string RemoveTestDevices()
    {
        int n = 0;
        foreach (var d in InputSystem.devices.ToArray())
            if (d.name.StartsWith("PruebaCoop") || d.name.StartsWith("PruebaTeclado") || d.name.StartsWith("PruebaRaton")) { InputSystem.RemoveDevice(d); n++; }
        return "quitados=" + n;
    }

    public static string PressEnter() { InputSystem.QueueStateEvent(Kb, new KeyboardState(Key.Enter)); return "Intro"; }
    public static string PressY() { InputSystem.QueueStateEvent(Pad, new GamepadState().WithButton(GamepadButton.North)); return "Y"; }
    public static string PressRB() { InputSystem.QueueStateEvent(Pad, new GamepadState().WithButton(GamepadButton.RightShoulder)); return "RB"; }
    public static string LoadSlot1()
    {
        var menu = Object.FindAnyObjectByType<FrostMenu>();
        menu.LoadSlot(0);
        return "cargando ranura 1";
    }

    public static string Inventory()
    {
        var inv = Object.FindAnyObjectByType<InventoryScreen>();
        var sel = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
        return "abierto=" + inv.IsOpen + " pestaña=" + inv.Current + " seleccion=" + (sel != null ? sel.name : "-") + " puntos=" + inv.Owner.Stats.pointsAvailable;
    }

    public static string GivePoints() { var st = Players.Get(0).Stats; st.pointsAvailable += 5; return "puntos=" + st.pointsAvailable; }
    public static string PressA() { InputSystem.QueueStateEvent(Pad, new GamepadState().WithButton(GamepadButton.South)); return "A"; }

    public static string Debug2()
    {
        var sb = new System.Text.StringBuilder("bloqueado=" + GameplayInput.Blocked + " pausa=" + PauseMenu.IsPaused + " opciones=" + OptionsPanel.IsOpen + " ts=" + Time.timeScale);
        foreach (var p in Players.All)
        {
            var rb = p.GetComponent<Rigidbody>();
            var g = PlayerController.PadOf(p);
            sb.Append(" | ").Append(p.name).Append(" activo=").Append(p.enabled).Append(" cinem=").Append(rb.isKinematic)
              .Append(" mando=").Append(g != null ? g.name : "-").Append(" stick=").Append(g != null ? g.leftStick.ReadValue().ToString("F1") : "-")
              .Append(" vel=").Append(rb.linearVelocity.ToString("F1")).Append(" velMax=").Append(p.Stats.MoveSpeed.ToString("F1"));
        }
        return sb.ToString();
    }

    public static string OpenPauseOptions()
    {
        var pm = Object.FindAnyObjectByType<PauseMenu>();
        pm.Pause();
        OptionsPanel.Open();
        return "pausa+opciones";
    }

    public static string ClosePauseOptions()
    {
        var p = Object.FindAnyObjectByType<OptionsPanel>(); if (p != null) p.Close();
        Object.FindAnyObjectByType<PauseMenu>().Resume();
        return "cerrado";
    }

    private static Mouse TestMouse { get { foreach (var d in InputSystem.devices) if (d is Mouse m && d.name.StartsWith("PruebaRaton")) return m; return InputSystem.AddDevice<Mouse>("PruebaRaton"); } }
    public static string HoldRightMouse() { InputSystem.QueueStateEvent(TestMouse, new MouseState().WithButton(MouseButton.Right)); return "clic derecho pegado"; }
    public static string ReleaseMouse() { InputSystem.QueueStateEvent(TestMouse, new MouseState()); return "suelto"; }
    public static string PressI() { InputSystem.QueueStateEvent(Kb, new KeyboardState(Key.I)); return "I"; }
    public static string PressB() { InputSystem.QueueStateEvent(Pad, new GamepadState().WithButton(GamepadButton.East)); return "B"; }
    public static string UIDevices()
    {
        var m = UnityEngine.EventSystems.EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        var devs = m.actionsAsset.devices;
        return devs.HasValue ? "ui=" + string.Join(",", devs.Value.Select(d => d.name)) : "ui=todos";
    }
}
