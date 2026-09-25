using UnityEngine.EventSystems;

// Estado compartido de la entrada: los menús bloquean el control del héroe mientras están abiertos.
public static class GameplayInput
{
    private static int _blockers;

    public static bool Blocked => _blockers > 0;

    public static void Block() => _blockers++;

    public static void Unblock()
    {
        if (_blockers > 0) _blockers--;
    }

    public static bool PointerOverUI => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => _blockers = 0;
}
