using UnityEngine;

// Viaje entre zonas: guarda la partida con la zona de destino y abre la pantalla de carga.
// "arrival" dice en qué punto de llegada (ZoneArrival) aparece el héroe en la zona nueva.
public static class ZoneTravel
{
    public static string PendingArrival { get; private set; } = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => PendingArrival = "";

    public static void Go(string scene, string arrival)
    {
        if (LoadingScreen.Active) return;
        PendingArrival = arrival ?? "";
        GameSession s = GameSession.Instance;
        if (s != null && s.Current != null)
        {
            s.SaveNow();
            s.Current.zone = scene;
            s.SaveNow();
        }
        LoadingScreen.Load(scene);
    }

    // Lo llama PlayerPersistence al empezar la zona. Devuelve el punto de llegada pendiente, si existe.
    public static ZoneArrival TakeArrival()
    {
        if (string.IsNullOrEmpty(PendingArrival)) return null;
        ZoneArrival a = ZoneArrival.Find(PendingArrival);
        PendingArrival = "";
        return a;
    }

    // Zona donde está el héroe de una partida guardada (las partidas viejas no la tienen: el poblado).
    public static string ZoneOf(SaveData d)
    {
        string z = d != null ? d.zone : "";
        return SceneIds.IsGameplay(z) ? z : SceneIds.Village;
    }
}
