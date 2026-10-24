using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Navegación de la UI con un mando virtual (exec PadChecks.X). Cada pulsación hay que soltarla con Release antes de la
// siguiente (como hace CoopChecks). Selected dice qué botón tiene el foco.
public static class PadChecks
{
    private static Gamepad Pad
    {
        get
        {
            foreach (Gamepad g in Gamepad.all) if (g.name.StartsWith("MandoPrueba")) return g;
            return InputSystem.AddDevice<Gamepad>("MandoPrueba");
        }
    }

    public static string Up() => Press(GamepadButton.DpadUp);
    public static string Down() => Press(GamepadButton.DpadDown);
    public static string Left() => Press(GamepadButton.DpadLeft);
    public static string Right() => Press(GamepadButton.DpadRight);
    public static string A() => Press(GamepadButton.South);
    public static string B() => Press(GamepadButton.East);
    public static string Start() => Press(GamepadButton.Start);
    public static string Select() => Press(GamepadButton.Select);

    private static string Press(GamepadButton b)
    {
        InputSystem.QueueStateEvent(Pad, new GamepadState().WithButton(b));
        return "pulsado " + b;
    }

    public static string Release()
    {
        InputSystem.QueueStateEvent(Pad, new GamepadState());
        return "suelto · foco=" + Focus();
    }

    public static string Selected() => "foco=" + Focus();

    private static string Focus()
    {
        GameObject go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (go == null) return "(ninguno)";
        Transform p = go.transform.parent;
        return (p != null ? p.name + "/" : "") + go.name;
    }

    public static string Remove()
    {
        foreach (Gamepad g in Gamepad.all) if (g.name.StartsWith("MandoPrueba")) { InputSystem.RemoveDevice(g); return "mando quitado"; }
        return "no había mando de prueba";
    }
}
