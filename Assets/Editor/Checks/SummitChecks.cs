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

    // Cristalizado (resiste lo físico, débil al fuego) e Hijo del Frost (se parte en dos al morir).
    public static string Bestiary()
    {
        EnemyCamp camp = Object.FindAnyObjectByType<EnemyCamp>();
        GameObject p = Players.PrimaryObject;
        GameDatabase db = GameDatabase.Instance;
        if (camp == null || p == null || db == null) return "sin campamento o héroe";
        var sb = new StringBuilder();
        EnemyDefinition crys = db.FindEnemy(SummitCreature.CrystallizedId), spawn = db.FindEnemy(SummitCreature.FrostSpawnId);
        if (crys == null || spawn == null) return "faltan definiciones";
        EnemyBrain c = camp.SpawnMember(crys, p.transform.position + p.transform.forward * 6f - p.transform.right * 2f, 15, Color.gray);
        EnemyBrain h = camp.SpawnMember(spawn, p.transform.position + p.transform.forward * 6f + p.transform.right * 2f, 15, Color.gray);
        Damageable cd = c.GetComponent<Damageable>();
        float h0 = cd.Health;
        cd.TakeHit(new DamageInfo { amount = 100f, type = DamageType.Physical, source = p });
        float physical = h0 - cd.Health;
        h0 = cd.Health;
        cd.TakeHit(new DamageInfo { amount = 100f, type = DamageType.Fire, source = p });
        float fire = h0 - cd.Health;
        sb.Append("cristalizado: 100 físico→").Append(physical.ToString("0")).Append(" 100 fuego→").Append(fire.ToString("0"));
        int before = camp.Members.Count;
        h.GetComponent<Damageable>().TakeHit(new DamageInfo { amount = 99999f, type = DamageType.Physical, source = p });
        sb.Append(" | hijo del Frost muerto: miembros ").Append(before).Append("→").Append(camp.Members.Count);
        int shards = 0;
        foreach (EnemyBrain b in camp.Members) if (b != null && b.definition != null && b.definition.id == SummitCreature.ShardId) shards++;
        sb.Append(" esquirlas=").Append(shards);
        return sb.ToString();
    }

    // ---------- Final (C-9 y C-10) ----------

    private static SealEncounter Enc => Gen.Encounter;

    // Altares encendidos y en la cámara: Bobby aparece.
    public static string ToBobby()
    {
        StartChapter();
        QuestLog.SetStage(SummitGenerator.AscentQuest, QuestLog.Get(SummitGenerator.AscentQuest).DoneStage);
        QuestLog.SetStage(SealEncounter.Quest, SealEncounter.StageBobby);
        return "bobby=" + (Enc.Bobby != null ? Enc.Bobby.title : "no") + " misión=" + QuestLog.Objective(SealEncounter.Quest, QuestLog.Stage(SealEncounter.Quest));
    }

    // Bobby al 10 %: se rinde (discurso) y despierta el Corazón.
    public static string BobbyGivesUp()
    {
        BossController b = Enc.Bobby;
        if (b == null) return "sin Bobby";
        b.Health.TakeHit(new DamageInfo { amount = b.Health.maxHealth, type = DamageType.Physical, source = Players.PrimaryObject });
        return "vida=" + Mathf.RoundToInt(b.Health.Health) + " hablando=" + b.Speaking;
    }

    public static string State()
    {
        var sb = new StringBuilder();
        BossController b = Enc.Bobby, h = Enc.Heart;
        sb.Append("bobby=").Append(b == null ? "-" : (b.Spared ? "rendido" : b.Speaking ? "hablando" : "pelea"));
        if (h != null)
        {
            FrostHeartBoss fh = h.GetComponent<FrostHeartBoss>();
            sb.Append(" corazón=").Append(Mathf.RoundToInt(h.Health.Health)).Append('/').Append(Mathf.RoundToInt(h.Health.maxHealth))
              .Append(h.Health.IsDead ? " roto" : "").Append(fh != null && fh.Shielded ? " escudo" : "");
        }
        sb.Append(" alimentadores=").Append(Object.FindObjectsByType<FeedingCrystal>().Length)
          .Append(" anillos=").Append(Object.FindObjectsByType<IceRing>().Length)
          .Append(" sello=").Append(Enc.Seal != null);
        string t = QuestLog.Tracked();
        sb.Append(" | ").Append(t).Append('@').Append(QuestLog.Stage(t)).Append(" '").Append(QuestLog.Objective(t, QuestLog.Stage(t))).Append("'");
        return sb.ToString();
    }

    public static string HeartTo50() => HeartTo(0.5f);
    public static string HeartTo20() => HeartTo(0.2f);

    private static string HeartTo(float k)
    {
        BossController h = Enc.Heart;
        if (h == null) return "sin corazón";
        h.Health.invulnerable = false;
        float floor = h.Health.healthFloor;
        h.Health.healthFloor = 0f;
        h.Health.TakeHit(new DamageInfo { amount = h.Health.Health - h.Health.maxHealth * k, type = DamageType.Fire, source = Players.PrimaryObject });
        h.Health.healthFloor = floor;
        return State();
    }

    public static string BreakFeeders()
    {
        int n = 0;
        foreach (FeedingCrystal f in Object.FindObjectsByType<FeedingCrystal>())
        {
            Damageable d = f.GetComponent<Damageable>();
            d.TakeHit(new DamageInfo { amount = d.maxHealth + 1f, type = DamageType.Physical, source = Players.PrimaryObject });
            n++;
        }
        return "rotos=" + n + " · " + State();
    }

    public static string KillHeart()
    {
        BossController h = Enc.Heart;
        if (h == null) return "sin corazón";
        h.Health.invulnerable = false;
        h.Health.healthFloor = 0f;
        h.Health.TakeHit(new DamageInfo { amount = h.Health.Health + 1f, type = DamageType.Fire, source = Players.PrimaryObject });
        return State();
    }

    public static string UseSeal()
    {
        if (Enc.Seal == null) return "sin sello";
        bool ok = Enc.Seal.Use();
        return "sello usado=" + ok + " final=" + EndingSequence.Running + " · " + State();
    }

    public static string GoSeal() => Go(Gen.Layout.Find(SummitLayout.ClearingKind.Chamber).center + Vector3.back * 3f);
}
