using System.Text;
using UnityEngine;
using UnityEngine.AI;

// Comprobaciones en Play de la Cumbre (05_Cumbre). Por el bridge: exec SummitChecks.Info
public static class SummitChecks
{
    private static SummitGenerator Gen => SummitGenerator.Instance;

    public static string Info()
    {
        if (Gen == null) return "sin cumbre";
        var l = Gen.Layout;
        var sb = new StringBuilder();
        sb.Append("agujas=").Append(l.spires.Count).Append(" cristales=").Append(l.crystals.Count).Append(" congelados=").Append(l.frozen.Count)
          .Append(" claros=").Append(l.clearings.Count).Append(" campamentos=").Append(Object.FindObjectsByType<EnemyCamp>().Length);
        sb.Append(" marcas=");
        foreach (QuestMarker m in Gen.GetComponentsInChildren<QuestMarker>()) sb.Append(m.id).Append(m.Used ? "(✓) " : " ");
        var path = new NavMeshPath();
        Vector3 b = l.Find(SummitLayout.ClearingKind.Chamber).center;
        NavMesh.SamplePosition(l.entry, out NavMeshHit ha, 4f, NavMesh.AllAreas);
        NavMesh.SamplePosition(b, out NavMeshHit hb, 6f, NavMesh.AllAreas);
        NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path);
        sb.Append("| entrada→cámara=").Append(path.status).Append(" muro=").Append(Gen.BarrierOpen ? "abierto" : "cerrado");
        return sb.ToString();
    }

    // Empieza el capítulo III (sin partida: en memoria).
    public static string StartChapter()
    {
        foreach (string id in new[] { QuestLog.Citadel, QuestLog.Ascent, "rastro_de_pipo", "savia_negra", "la_madre_savia" })
        {
            QuestDefinition q = QuestLog.Get(id);
            if (q != null) QuestLog.SetStage(id, q.DoneStage);
        }
        string t = QuestLog.Tracked();
        return t + "@" + QuestLog.Stage(t) + " '" + QuestLog.Objective(t, QuestLog.Stage(t)) + "'";
    }

    public static string LightAltars()
    {
        var sb = new StringBuilder();
        foreach (QuestMarker m in Gen.GetComponentsInChildren<QuestMarker>())
            if (m.id.StartsWith("altar_")) { m.Use(); sb.Append(m.id).Append(m.Used ? "✓ " : "✗ "); }
        string t = QuestLog.Tracked();
        return sb + "→ " + t + "@" + QuestLog.Stage(t) + " muro=" + (Gen.BarrierOpen ? "abierto" : "cerrado");
    }

    public static string GoEntry() => Go(Gen.Layout.entry);
    public static string GoAltar() { foreach (var c in Gen.Layout.All(SummitLayout.ClearingKind.Altar)) return Go(c.center + Vector3.back * 4f); return "sin altar"; }
    public static string GoBarrier() => Go(Gen.Layout.barrier + Vector3.back * 7f);
    public static string GoChamber() => Go(Gen.Layout.Find(SummitLayout.ClearingKind.Chamber).center + Vector3.back * 12f);

    private static string Go(Vector3 at)
    {
        GameObject p = Players.PrimaryObject;
        if (p == null) return "sin héroe";
        at = Gen.Layout.Ground(at.x, at.z) + Vector3.up * 0.1f;
        Rigidbody rb = p.GetComponent<Rigidbody>();
        if (rb != null) { rb.position = at; rb.linearVelocity = Vector3.zero; }
        p.transform.position = at;
        Camera cam = Camera.main;
        CameraFollow f = cam != null ? cam.GetComponent<CameraFollow>() : null;
        if (f != null) { cam.transform.position = at + f.offset; cam.transform.LookAt(at + Vector3.up * f.lookHeight); }
        return "héroe en " + at.ToString("0");
    }
}
