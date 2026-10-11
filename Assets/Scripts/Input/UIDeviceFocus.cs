using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

// Mientras un jugador usa una ventana suya (la mochila), la interfaz solo escucha sus dispositivos:
// el otro jugador no puede moverse por ella ni hacer clic en sus objetos.
public static class UIDeviceFocus
{
    private static InputSystemUIInputModule _module;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _module = null;

    private static InputSystemUIInputModule Module
    {
        get
        {
            EventSystem es = EventSystem.current;
            if (es == null) return null;
            if (_module == null || _module.gameObject != es.gameObject) _module = es.GetComponent<InputSystemUIInputModule>();
            return _module;
        }
    }

    // Dispositivos con los que juega este héroe: su mando, o el teclado y el ratón.
    public static InputDevice[] DevicesOf(Component hero)
    {
        var list = new List<InputDevice>();
        Gamepad pad = PlayerController.PadOf(hero);
        if (pad != null && !PlayerController.UsesKeyboard(hero)) list.Add(pad);
        else
            foreach (InputDevice d in InputSystem.devices)
                if (d is Keyboard || d is Mouse) list.Add(d);
        return list.ToArray();
    }

    public static void FocusOn(Component hero)
    {
        InputSystemUIInputModule m = Module;
        if (m == null || m.actionsAsset == null || hero == null) return;
        // Con un solo héroe no hace falta limitar nada.
        if (Players.Count < 2) { Release(); return; }
        m.actionsAsset.devices = DevicesOf(hero);
    }

    public static void Release()
    {
        InputSystemUIInputModule m = Module;
        if (m != null && m.actionsAsset != null) m.actionsAsset.devices = null;
    }
}
