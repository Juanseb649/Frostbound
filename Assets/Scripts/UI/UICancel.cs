using UnityEngine.InputSystem;

// Esc en teclado o el botón B/Círculo del mando.
public static class UICancel
{
    public static bool Pressed()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) return true;
        return false;
    }
}
