using System;
using System.Collections.Generic;
using UnityEngine;

// Misiones y su etapa. Se guardan en la partida actual; sin partida (probando la escena) viven en memoria.
public static class QuestLog
{
    public const string Citadel = "ciudadela_caida";

    // Etapas de "La ciudadela caída".
    public const int CitadelNone = 0;
    public const int CitadelTalked = 1;      // Steve contó lo de la ciudadela: limpia el poblado del castillo.
    public const int CitadelBarbarian = 2;   // El bárbaro cayó: entra al castillo.
    public const int CitadelDone = 3;        // El ninja maldito cayó.

    public static event Action<string, int> Changed;

    private static readonly Dictionary<string, int> Memory = new Dictionary<string, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Memory.Clear();
        Changed = null;
    }

    public static int Stage(string id)
    {
        SaveData data = GameSession.Instance != null ? GameSession.Instance.Current : null;
        if (data != null)
        {
            foreach (SavedQuest q in data.quests) if (q.id == id) return q.stage;
            return 0;
        }
        return Memory.TryGetValue(id, out int s) ? s : 0;
    }

    public static void SetStage(string id, int stage)
    {
        if (Stage(id) == stage) return;
        SaveData data = GameSession.Instance != null ? GameSession.Instance.Current : null;
        if (data != null)
        {
            SavedQuest q = data.quests.Find(x => x.id == id);
            if (q == null) data.quests.Add(q = new SavedQuest { id = id });
            q.stage = stage;
        }
        else Memory[id] = stage;
        Changed?.Invoke(id, stage);
        if (GameSession.Instance != null) GameSession.Instance.SaveNow();
    }

    public static string Title(string id) => id == Citadel ? "La ciudadela caída" : id;

    // Texto del objetivo para el panel de misiones (vacío = no se muestra).
    public static string Objective(string id, int stage)
    {
        if (id != Citadel) return "";
        switch (stage)
        {
            case CitadelTalked: return "Limpia el poblado al pie del castillo de corruptos";
            case CitadelBarbarian: return "Entra al castillo y derrota al ninja maldito";
            case CitadelDone: return "";
            default: return "";
        }
    }

    public static bool Active(string id)
    {
        int s = Stage(id);
        return s > 0 && s < CitadelDone;
    }
}
