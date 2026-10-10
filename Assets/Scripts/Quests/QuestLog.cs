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

    public const string Ascent = "ascenso_frostspire";

    // Etapas de "El ascenso".
    public const int AscentNone = 0;
    public const int AscentTold = 1;         // Steve te mandó a la montaña: toma el sendero del norte.
    public const int AscentFoothills = 2;    // Estás en Las Faldas: llega al paso helado.
    public const int AscentFoothillsDone = 3;

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

    public static string Title(string id) => id == Citadel ? "La ciudadela caída" : id == Ascent ? "El ascenso" : id;

    // Texto del objetivo para el panel de misiones (vacío = no se muestra).
    public static string Objective(string id, int stage)
    {
        if (id == Citadel)
            switch (stage)
            {
                case CitadelTalked: return "Limpia el poblado al pie del castillo de corruptos";
                case CitadelBarbarian: return "Entra al castillo y derrota al ninja maldito";
                default: return "";
            }
        if (id == Ascent)
            switch (stage)
            {
                case AscentTold: return "Toma el sendero del norte hacia la montaña";
                case AscentFoothills: return "Asciende por Las Faldas y vence a los guardianes del paso helado";
                default: return "";
            }
        return "";
    }

    // La misión que se muestra en pantalla: la primera con objetivo pendiente.
    public static string Tracked()
    {
        if (Objective(Citadel, Stage(Citadel)).Length > 0) return Citadel;
        if (Objective(Ascent, Stage(Ascent)).Length > 0) return Ascent;
        return "";
    }

    public static bool Active(string id)
    {
        int s = Stage(id);
        return s > 0 && s < CitadelDone;
    }
}
