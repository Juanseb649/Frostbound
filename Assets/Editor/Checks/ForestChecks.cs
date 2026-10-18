using System.Text;
using UnityEngine;
using UnityEngine.AI;

// Comprobaciones en Play del Bosque Corrupto (04_BosqueCorrupto). Por el bridge: exec ForestChecks.Info
public static class ForestChecks
{
    private static ForestGenerator Gen => ForestGenerator.Instance;

    public static string Info()
    {
        if (Gen == null) return "sin bosque";
        var sb = new StringBuilder();
        var l = Gen.Layout;
        sb.Append("pinos=").Append(l.pines.Count).Append(" secos=").Append(l.deadTrees.Count).Append(" setas=").Append(l.mushrooms.Count)
          .Append(" charcos=").Append(l.pools.Count).Append(" claros=").Append(l.clearings.Count);
        sb.Append(" campamentos=").Append(Object.FindObjectsByType<EnemyCamp>().Length);
        sb.Append(" marcas=");
        foreach (QuestMarker m in Gen.GetComponentsInChildren<QuestMarker>()) sb.Append(m.id).Append(m.Used ? "(✓) " : " ");
        // ¿Se puede ir andando de la entrada al corazón?
        var path = new NavMeshPath();
        Vector3 a = l.entry, b = l.Find(ForestLayout.ClearingKind.Heart).center;
        NavMesh.SamplePosition(a, out NavMeshHit ha, 4f, NavMesh.AllAreas);
        NavMesh.SamplePosition(b, out NavMeshHit hb, 6f, NavMesh.AllAreas);
        bool ok = NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path);
        sb.Append("| camino entrada→corazón=").Append(ok ? path.status.ToString() : "no");
        sb.Append(" | luces=").Append(Object.FindObjectsByType<Light>().Length);
        return sb.ToString();
    }

    // Lleva al héroe a un claro: exec ForestChecks.GoClue → pista 1, GoRoot, GoHeart, GoEntry.
    public static string GoEntry() => Go(Gen.Layout.entry);
    public static string GoClue() => Go(Near(ForestLayout.ClearingKind.Clue));
    public static string GoRoot() => Go(Near(ForestLayout.ClearingKind.Root));
    public static string GoHeart() => Go(Gen.Layout.Find(ForestLayout.ClearingKind.Heart).center + Vector3.back * 10f);
    public static string GoCamp() => Go(Near(ForestLayout.ClearingKind.Camp));

    private static Vector3 Near(ForestLayout.ClearingKind kind)
    {
        foreach (var c in Gen.Layout.All(kind)) return c.center + Vector3.back * 4f;
        return Gen.Layout.entry;
    }

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

    // Usa la primera marca de un tipo (pista o raíz) que espere una misión.
    public static string UseNext()
    {
        foreach (QuestMarker m in Gen.GetComponentsInChildren<QuestMarker>())
            if (!m.Used && m.type == QuestObjective.Interactuar && QuestLog.Expects(m.type, m.id))
            {
                m.Use();
                string t = QuestLog.Tracked();
                return m.id + " usada → " + t + "@" + QuestLog.Stage(t) + " " + QuestLog.Detail(t);
            }
        return "ninguna marca esperada";
    }

    // Prepara el capítulo II como recién empezado (sin partida: en memoria).
    public static string StartChapter()
    {
        QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelDone);
        QuestLog.SetStage(QuestLog.Ascent, QuestLog.AscentRocco);
        string t = QuestLog.Tracked();
        return t + "@" + QuestLog.Stage(t);
    }

    // Viaje real por las puertas: en la Escalinata, pone al héroe sobre el paso del bosque (tras vencer a Rocco).
    public static string WalkToForestGate()
    {
        QuestLog.SetStage(QuestLog.Ascent, QuestLog.AscentRocco);
        GameObject gate = GameObject.Find("Paso_Bosque");
        GameObject p = Players.PrimaryObject;
        if (gate == null || p == null) return "sin paso o sin héroe";
        Rigidbody rb = p.GetComponent<Rigidbody>();
        Vector3 at = gate.transform.position + Vector3.up * 0.2f;
        if (rb != null) rb.position = at;
        p.transform.position = at;
        return "en el paso " + at.ToString("0");
    }

    // En el bosque: pone al héroe sobre el paso de vuelta a la Escalinata.
    public static string WalkToStairsGate()
    {
        GameObject gate = GameObject.Find("Paso_Escalinata");
        GameObject p = Players.PrimaryObject;
        if (gate == null || p == null) return "sin paso o sin héroe";
        Rigidbody rb = p.GetComponent<Rigidbody>();
        Vector3 at = gate.transform.position + Vector3.up * 0.2f;
        if (rb != null) rb.position = at;
        p.transform.position = at;
        return "en el paso " + at.ToString("0");
    }

    public static string Where()
    {
        GameObject p = Players.PrimaryObject;
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " héroe=" + (p != null ? p.transform.position.ToString("0") : "-");
    }

    // Los tres corruptos del bosque delante del héroe (en el primer campamento).
    public static string Bestiary()
    {
        EnemyCamp camp = Object.FindAnyObjectByType<EnemyCamp>();
        GameObject p = Players.PrimaryObject;
        if (camp == null || p == null || GameDatabase.Instance == null) return "sin campamento o héroe";
        var sb = new StringBuilder();
        int i = 0;
        foreach (string id in ForestGenerator.ForestEnemies)
        {
            EnemyDefinition def = GameDatabase.Instance.FindEnemy(id);
            if (def == null) { sb.Append(id).Append("=¡falta! "); continue; }
            Vector3 at = p.transform.position + p.transform.forward * 7f + p.transform.right * (i++ - 1) * 3f;
            EnemyBrain b = camp.SpawnMember(def, at, 9, Color.gray);
            ForestCreature fc = b != null ? b.GetComponent<ForestCreature>() : null;
            sb.Append(def.displayName).Append(fc != null ? "(" + fc.kind + ") " : "(sin conducta) ");
            if (b != null) b.Alert();
        }
        return sb.ToString();
    }

    public static string Puddles() => "charcos de veneno=" + Object.FindObjectsByType<PoisonPuddle>().Length
        + " envenenado=" + (Players.Primary != null && Players.Primary.GetComponent<PlayerAfflictions>() != null && Players.Primary.GetComponent<PlayerAfflictions>().Poisoned);

    public static string Invulnerable()
    {
        if (Players.Primary != null && Players.Primary.Stats != null) Players.Primary.Stats.invulnerable = true;
        return "invulnerable";
    }

    // Capítulo II hasta el claro de la Madre Savia (etapa 2: aparece).
    public static string ToBoss()
    {
        QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelDone);
        QuestLog.SetStage(QuestLog.Ascent, QuestLog.AscentRocco);
        QuestLog.SetStage("rastro_de_pipo", 3);
        QuestLog.SetStage("savia_negra", 2);
        QuestLog.SetStage(MadreSaviaEncounter.Quest, MadreSaviaEncounter.StageFight);
        var e = Gen.Encounter;
        return "jefa=" + (e != null && e.Boss != null ? e.Boss.title : "no") + " pipo=" + (e != null && e.Pipo != null);
    }

    // Deja a la jefa al 40 % (fase de raíces) o al 20 % (lluvia).
    public static string BossTo40() => BossTo(0.4f);
    public static string BossTo20() => BossTo(0.2f);

    private static string BossTo(float k)
    {
        var b = Gen.Encounter != null ? Gen.Encounter.Boss : null;
        if (b == null) return "sin jefa";
        b.Health.TakeHit(new DamageInfo { amount = b.Health.Health - b.Health.maxHealth * k, type = DamageType.Physical, source = Players.PrimaryObject });
        return "vida=" + Mathf.RoundToInt(b.Health.Health) + "/" + Mathf.RoundToInt(b.Health.maxHealth) + " avisos=" + Object.FindObjectsByType<GroundWarning>().Length;
    }

    public static string KillBoss()
    {
        var b = Gen.Encounter != null ? Gen.Encounter.Boss : null;
        if (b == null) return "sin jefa";
        b.Health.healthFloor = 0f;
        b.Health.TakeHit(new DamageInfo { amount = b.Health.Health + 10f, type = DamageType.Physical, source = Players.PrimaryObject });
        string t = QuestLog.Tracked();
        return "muerta=" + b.Health.IsDead + " → " + t + "@" + QuestLog.Stage(t) + " '" + QuestLog.Objective(t, QuestLog.Stage(t)) + "'";
    }

    public static string FreePipo()
    {
        foreach (QuestMarker m in Gen.GetComponentsInChildren<QuestMarker>())
            if (m.id == MadreSaviaEncounter.CocoonMarker)
            {
                bool ok = m.Use();
                string t = QuestLog.Tracked();
                return "capullo usado=" + ok + " marca=" + m.Used + " → " + t + "@" + QuestLog.Stage(t) + " '" + QuestLog.Objective(t, QuestLog.Stage(t)) + "'";
            }
        return "sin capullo";
    }

    public static string GoCocoon() => Go(Gen.Encounter.cocoonAt + Vector3.back * 3f);
}
