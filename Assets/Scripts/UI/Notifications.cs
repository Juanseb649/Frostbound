using UnityEngine;

// Punto único para los avisos en pantalla. Los dibuja el GameHUD activo; sin HUD, el aviso se descarta.
public static class Notifications
{
    private static GameHUD _hud;

    public static void Register(GameHUD hud) => _hud = hud;

    public static void Unregister(GameHUD hud)
    {
        if (_hud == hud) _hud = null;
    }

    public static void Show(string message, Color color)
    {
        if (_hud != null && !string.IsNullOrEmpty(message)) _hud.ShowMessage(message, color);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => _hud = null;
}
