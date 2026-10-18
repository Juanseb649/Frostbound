using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Misiones y su etapa. Se guardan en la partida actual; sin partida (probando la escena) viven en memoria.
// Los textos, objetivos, diálogos y recompensas salen de los QuestDefinition de Resources/Quests.
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
    public const int AscentFoothillsDone = 3; // Guardianes del paso vencidos: sube la Escalinata.
    public const int AscentRocco = 4;         // Rocco Vendaval derrotado: fin del capítulo I.

    public static event Action<string, int> Changed;

    private static readonly Dictionary<string, int> Memory = new Dictionary<string, int>();
    private static readonly Dictionary<string, int> MemoryProgress = new Dictionary<string, int>();
    private static List<QuestDefinition> _all;
    private static Dictionary<string, QuestDefinition> _byId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Memory.Clear();
        MemoryProgress.Clear();
        Changed = null;
        _all = null;
        _byId = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Al entrar en una zona: las misiones que empiezan solas y cuyo requisito ya se cumplió (partidas viejas incluidas).
    private static void OnSceneLoaded(Scene s, LoadSceneMode m) => StartUnlocked();

    // Todas las misiones de la campaña, en orden.
    public static IReadOnlyList<QuestDefinition> All
    {
        get
        {
            if (_all == null)
            {
                _all = new List<QuestDefinition>(Resources.LoadAll<QuestDefinition>("Quests"));
                _all.Sort((a, b) => a.chapter != b.chapter ? a.chapter.CompareTo(b.chapter) : a.order.CompareTo(b.order));
                _byId = new Dictionary<string, QuestDefinition>();
                foreach (QuestDefinition q in _all) if (!string.IsNullOrEmpty(q.id)) _byId[q.id] = q;
            }
            return _all;
        }
    }

    public static QuestDefinition Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        _ = All;
        return _byId.TryGetValue(id, out QuestDefinition q) ? q : null;
    }

    private static SaveData Data => GameSession.Instance != null ? GameSession.Instance.Current : null;

    public static int Stage(string id)
    {
        SaveData data = Data;
        if (data != null)
        {
            foreach (SavedQuest q in data.quests) if (q.id == id) return q.stage;
            return 0;
        }
        return Memory.TryGetValue(id, out int s) ? s : 0;
    }

    // Cuánto lleva del objetivo de la etapa actual (enemigos muertos, pistas encontradas...).
    public static int Progress(string id)
    {
        SaveData data = Data;
        if (data != null)
        {
            foreach (SavedQuest q in data.quests) if (q.id == id) return q.progress;
            return 0;
        }
        return MemoryProgress.TryGetValue(id, out int p) ? p : 0;
    }

    private static void SetProgress(string id, int progress)
    {
        SaveData data = Data;
        if (data != null)
        {
            SavedQuest q = data.quests.Find(x => x.id == id);
            if (q == null) data.quests.Add(q = new SavedQuest { id = id });
            q.progress = progress;
        }
        else MemoryProgress[id] = progress;
    }

    public static void SetStage(string id, int stage)
    {
        int before = Stage(id);
        if (before == stage) return;
        SaveData data = Data;
        if (data != null)
        {
            SavedQuest q = data.quests.Find(x => x.id == id);
            if (q == null) data.quests.Add(q = new SavedQuest { id = id });
            q.stage = stage;
            q.progress = 0;
        }
        else
        {
            Memory[id] = stage;
            MemoryProgress[id] = 0;
        }

        QuestDefinition def = Get(id);
        if (def != null)
        {
            QuestStage st = def.Stage(stage);
            if (st != null && !string.IsNullOrEmpty(st.onReach)) Notifications.Show(st.onReach, FrostboundUI.Gold);
            if (stage >= def.DoneStage && before < def.DoneStage) Reward(def);
        }
        Changed?.Invoke(id, stage);
        StartUnlocked();
        if (GameSession.Instance != null) GameSession.Instance.SaveNow();
    }

    // Empieza una misión (etapa 1) con su aviso.
    public static void Begin(string id)
    {
        if (Stage(id) > 0) return;
        SetStage(id, 1);
        BonfireBanner.Show("NUEVA MISIÓN", BonfireBanner.Victory);
        Notifications.Show("Nueva misión: " + Title(id), FrostboundUI.Gold);
    }

    // Las misiones sin NPC empiezan solas en cuanto se cumple su requisito.
    private static void StartUnlocked()
    {
        foreach (QuestDefinition q in All)
            if (string.IsNullOrEmpty(q.giver) && !string.IsNullOrEmpty(q.requiresQuest) && Stage(q.id) == 0 && q.Unlocked)
                Begin(q.id);
    }

    private static void Reward(QuestDefinition def)
    {
        if (def.experience <= 0 && def.coins <= 0) return;
        foreach (PlayerController pc in Players.All)
        {
            if (pc == null) continue;
            if (def.experience > 0 && pc.Stats != null) pc.Stats.AddExperience(def.experience);
            if (def.coins > 0) Wallet.Of(pc.gameObject).Add(def.coins);
        }
        string what = def.experience > 0 && def.coins > 0 ? def.experience + " de experiencia y " + def.coins + " monedas"
            : def.experience > 0 ? def.experience + " de experiencia" : def.coins + " monedas";
        Notifications.Show("Misión cumplida: " + def.title + " · +" + what, FrostboundUI.Gold);
    }

    // Aviso de algo que pasó en el mundo: avanza el objetivo de cada misión activa que lo espere.
    public static void Report(QuestObjective type, string target, int amount = 1)
    {
        foreach (QuestDefinition q in All)
        {
            int stage = Stage(q.id);
            if (stage <= 0 || stage >= q.DoneStage) continue;
            QuestStage st = q.Stage(stage);
            if (st == null || st.type != type) continue;
            if (!Matches(st.target, target)) continue;
            int p = Progress(q.id) + amount;
            if (p >= st.count) SetStage(q.id, stage + 1);
            else
            {
                SetProgress(q.id, p);
                Changed?.Invoke(q.id, stage);
            }
        }
    }

    // Vacío = cualquiera; "pista" acepta "pista" y "pista_bufanda" (así cuentan varias del mismo tipo).
    private static bool Matches(string wanted, string got)
    {
        if (string.IsNullOrEmpty(wanted)) return true;
        if (string.IsNullOrEmpty(got)) return false;
        return string.Equals(wanted, got, StringComparison.OrdinalIgnoreCase)
            || got.StartsWith(wanted + "_", StringComparison.OrdinalIgnoreCase);
    }

    public static string Title(string id)
    {
        QuestDefinition q = Get(id);
        return q != null && !string.IsNullOrEmpty(q.title) ? q.title : id;
    }

    // Texto del objetivo para el panel de misiones (vacío = no se muestra).
    public static string Objective(string id, int stage)
    {
        QuestDefinition q = Get(id);
        QuestStage st = q != null && stage < q.DoneStage ? q.Stage(stage) : null;
        return st != null ? st.objective : "";
    }

    // Detalle bajo el objetivo: "2/3" si hay que repetirlo.
    public static string Detail(string id)
    {
        QuestDefinition q = Get(id);
        QuestStage st = q != null ? q.Stage(Stage(id)) : null;
        return st != null && st.count > 1 && Stage(id) < q.DoneStage ? Progress(id) + "/" + st.count : "";
    }

    // La misión que se muestra en pantalla: la primera con objetivo pendiente.
    public static string Tracked()
    {
        foreach (QuestDefinition q in All)
            if (Objective(q.id, Stage(q.id)).Length > 0) return q.id;
        return "";
    }

    public static bool Active(string id)
    {
        int s = Stage(id);
        QuestDefinition q = Get(id);
        return s > 0 && s < (q != null ? q.DoneStage : CitadelDone);
    }

    public static bool Done(string id)
    {
        QuestDefinition q = Get(id);
        return q != null && Stage(id) >= q.DoneStage;
    }
}
