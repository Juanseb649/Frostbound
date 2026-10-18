using System.Text;
using UnityEngine;

// Comprobaciones de las misiones por datos. En Play (sin partida: las etapas viven en memoria):
// exec QuestChecks.Data · exec QuestChecks.Flow
public static class QuestChecks
{
    public static string Data()
    {
        var sb = new StringBuilder("misiones=" + QuestLog.All.Count);
        foreach (QuestDefinition q in QuestLog.All)
        {
            sb.Append(" | ").Append(q.chapter).Append('.').Append(q.order).Append(' ').Append(q.id).Append(" etapas=").Append(q.DoneStage);
            if (!string.IsNullOrEmpty(q.requiresQuest) && QuestLog.Get(q.requiresQuest) == null) sb.Append(" ¡requisito desconocido ").Append(q.requiresQuest).Append('!');
            QuestStage last = q.Stage(q.DoneStage);
            if (last != null && last.objective.Length > 0) sb.Append(" ¡la última etapa tiene objetivo!");
        }
        return sb.ToString();
    }

    // Recorre la campaña entera con avisos simulados, desde el final del capítulo I.
    public static string Flow()
    {
        var sb = new StringBuilder();
        CharacterStats stats = Players.Primary != null ? Players.Primary.Stats : null;
        int xp0 = stats != null ? stats.experience + stats.level * 100000 : 0;
        Wallet wallet = Players.Primary != null ? Wallet.Of(Players.Primary.gameObject) : null;
        int coins0 = wallet != null ? wallet.Coins : 0;

        foreach (QuestDefinition q in QuestLog.All) QuestLog.SetStage(q.id, 0);
        QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelTalked);
        Step(sb, "cap.I");
        QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelDone);
        QuestLog.SetStage(QuestLog.Ascent, QuestLog.AscentFoothillsDone);
        Step(sb, "escalinata");
        QuestLog.SetStage(QuestLog.Ascent, QuestLog.AscentRocco);
        Step(sb, "rocco");

        QuestLog.Report(QuestObjective.Llegar, "bosque_entrada"); Step(sb, "bosque");
        QuestLog.Report(QuestObjective.Interactuar, "pista_huellas");
        QuestLog.Report(QuestObjective.Interactuar, "raiz_1"); // no cuenta todavía
        QuestLog.Report(QuestObjective.Interactuar, "pista_bufanda"); Step(sb, "2 pistas");
        QuestLog.Report(QuestObjective.Interactuar, "pista_dibujo"); Step(sb, "3 pistas");
        for (int i = 1; i <= 3; i++) QuestLog.Report(QuestObjective.Interactuar, "raiz_" + i);
        Step(sb, "raices");
        QuestLog.Report(QuestObjective.Llegar, "claro_savia");
        QuestLog.Report(QuestObjective.Jefe, "Madre Savia"); Step(sb, "jefa");
        QuestLog.Report(QuestObjective.Interactuar, "capullo_pipo");
        QuestLog.Report(QuestObjective.Hablar, "steve"); Step(sb, "pipo");

        QuestLog.Report(QuestObjective.Llegar, "cumbre_entrada");
        for (int i = 1; i <= 3; i++) QuestLog.Report(QuestObjective.Interactuar, "altar_" + i);
        QuestLog.Report(QuestObjective.Llegar, "camara_sello");
        QuestLog.Report(QuestObjective.Jefe, "Bobby"); Step(sb, "bobby");
        QuestLog.Report(QuestObjective.Jefe, "Corazón del Frost");
        QuestLog.Report(QuestObjective.Interactuar, "sello"); Step(sb, "sello");
        QuestLog.Report(QuestObjective.Hablar, "steve"); Step(sb, "fin");

        int done = 0;
        foreach (QuestDefinition q in QuestLog.All) if (QuestLog.Done(q.id)) done++;
        sb.Append(" || cumplidas=").Append(done).Append('/').Append(QuestLog.All.Count);
        if (stats != null) sb.Append(" xp+=").Append(stats.experience + stats.level * 100000 - xp0 > 0 ? "sí" : "no").Append(" nivel=").Append(stats.level);
        if (wallet != null) sb.Append(" monedas+=").Append(wallet.Coins - coins0);
        return sb.ToString();
    }

    private static void Step(StringBuilder sb, string label)
    {
        string t = QuestLog.Tracked();
        sb.Append(label).Append(": ").Append(t.Length == 0 ? "-" : t + "@" + QuestLog.Stage(t) + " '" + QuestLog.Objective(t, QuestLog.Stage(t)) + "' " + QuestLog.Detail(t)).Append(" | ");
    }

    // Steve: ofrece la primera misión, luego comenta la etapa; tras Rocco comenta el capítulo II.
    public static string Steve()
    {
        var steve = GameObject.Find(MapSystem.SteveName);
        QuestGiver g = steve != null ? steve.GetComponent<QuestGiver>() : null;
        if (g == null) return "sin Steve";
        var sb = new StringBuilder();
        foreach (QuestDefinition q in QuestLog.All) QuestLog.SetStage(q.id, 0);
        sb.Append("inicio novedad=").Append(g.HasNews);
        g.SkipIntro();
        sb.Append(" | tras hablar: ").Append(QuestLog.Tracked()).Append(" novedad=").Append(g.HasNews);
        QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelDone);
        sb.Append(" | castillo hecho novedad=").Append(g.HasNews);
        g.SkipIntro();
        sb.Append(" -> ").Append(QuestLog.Tracked()).Append('@').Append(QuestLog.Stage(QuestLog.Ascent));
        QuestLog.SetStage(QuestLog.Ascent, QuestLog.AscentRocco);
        sb.Append(" | tras Rocco: ").Append(QuestLog.Tracked()).Append(" novedad=").Append(g.HasNews);
        steve.GetComponent<NPCInteractable>().Choose(NPCOption.Hablar);
        return sb.ToString();
    }

    // Pipo ya rescatado: aparece junto a Steve; hablar con Steve cumple "la_madre_savia".
    public static string PipoHome()
    {
        foreach (QuestDefinition q in QuestLog.All) QuestLog.SetStage(q.id, 0);
        QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelDone);
        QuestLog.SetStage(QuestLog.Ascent, QuestLog.AscentRocco);
        QuestLog.SetStage("rastro_de_pipo", 3);
        QuestLog.SetStage("savia_negra", 2);
        QuestLog.SetStage(MadreSaviaEncounter.Quest, MadreSaviaEncounter.StageHome);
        return "etapa=" + QuestLog.Stage(MadreSaviaEncounter.Quest);
    }

    public static string PipoState()
    {
        GameObject pipo = GameObject.Find("NPC_Pipo");
        var steve = GameObject.Find(MapSystem.SteveName);
        string s = "pipo=" + (pipo != null) + (pipo != null && steve != null ? " a " + Vector3.Distance(pipo.transform.position, steve.transform.position).ToString("0.0") + " m de Steve" : "");
        if (steve != null) steve.GetComponent<NPCInteractable>().Choose(NPCOption.Hablar);
        string t = QuestLog.Tracked();
        return s + " | tras hablar con Steve: " + MadreSaviaEncounter.Quest + "@" + QuestLog.Stage(MadreSaviaEncounter.Quest) + " sigue=" + t + "@" + QuestLog.Stage(t);
    }
}
