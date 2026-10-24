using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Auditoría de la IA de los corruptos en el poblado (01_Village), por el bridge:
// exec AIChecks.StandAtCamp → wait 15 → exec AIChecks.Report   (héroe quieto junto al primer grupo del camino)
// exec AIChecks.WalkRoad → wait 30 → exec AIChecks.Report      (héroe recorre el camino a la ciudadela sin pararse)
public static class AIChecks
{
    private static float _hp0, _t0;
    private static int _hits;
    private static readonly HashSet<EnemyBrain> _hooked = new HashSet<EnemyBrain>();
    private static readonly HashSet<EnemyBrain> _engaged = new HashSet<EnemyBrain>();

    private static VillageWorldGenerator Gen => Object.FindAnyObjectByType<VillageWorldGenerator>();

    private static void Begin()
    {
        foreach (QuestDefinition q in QuestLog.All) QuestLog.SetStage(q.id, 0);
        QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelTalked);
        CharacterStats st = Players.Primary.Stats;
        st.invulnerable = false;
        st.baseMaxHealth += 100000f;
        st.currentHealth = st.MaxHealth;
        _hp0 = st.currentHealth;
        _t0 = Time.time;
        _hits = 0;
        _hooked.Clear();
        _engaged.Clear();
    }

    // Engancha a los corruptos que vayan apareciendo para contar golpes y quién llegó a pelear.
    public static string Hook()
    {
        int n = 0;
        foreach (EnemyBrain b in Object.FindObjectsByType<EnemyBrain>())
        {
            if (_hooked.Contains(b)) continue;
            _hooked.Add(b);
            b.HitPlayerEvent += _ => _hits++;
            n++;
        }
        foreach (EnemyBrain b in _hooked)
            if (b != null && b.Current != EnemyBrain.State.Idle) _engaged.Add(b);
        return "enganchados+" + n;
    }

    private static void Teleport(Vector3 p)
    {
        PlayerPersistence mover = Object.FindAnyObjectByType<PlayerPersistence>();
        if (mover != null) mover.MoveTo(p, Quaternion.identity);
    }

    public static string StandAtCamp()
    {
        Begin();
        EnemyCamp camp = Gen.RoadCamps[0];
        Vector3 at = camp.transform.position + (VillageLayout.CitadelRoadPoint(Gen.Layout, 0.3f, out _) - camp.transform.position).normalized * 7f;
        Teleport(at + Vector3.up * 0.1f);
        return "quieto a 7 m del grupo 1 en " + at.ToString("0");
    }

    public static string WalkRoad()
    {
        Begin();
        Vector3 a = VillageLayout.CitadelRoadPoint(Gen.Layout, 0.12f, out _);
        Vector3 b = VillageLayout.CitadelRoadPoint(Gen.Layout, 0.97f, out _);
        Teleport(a + Vector3.up * 0.1f);
        Players.Primary.MoveTo(b);
        return "andando por el camino de " + a.ToString("0") + " a " + b.ToString("0");
    }

    public static string Report()
    {
        Hook();
        CharacterStats st = Players.Primary.Stats;
        var states = new Dictionary<EnemyBrain.State, int>();
        int alive = 0;
        float nearest = float.MaxValue;
        foreach (EnemyBrain b in Object.FindObjectsByType<EnemyBrain>())
        {
            if (b.Current == EnemyBrain.State.Dead) continue;
            alive++;
            states[b.Current] = states.TryGetValue(b.Current, out int c) ? c + 1 : 1;
            nearest = Mathf.Min(nearest, Vector3.Distance(b.transform.position, Players.Primary.transform.position));
        }
        var sb = new StringBuilder();
        sb.Append(Mathf.RoundToInt(Time.time - _t0)).Append(" s · daño=").Append(Mathf.RoundToInt(_hp0 - st.currentHealth)).Append(" golpes=").Append(_hits)
          .Append(" · enemigos que llegaron a pelear=").Append(_engaged.Count).Append(" · vivos=").Append(alive).Append(" estados:");
        foreach (var kv in states) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
        sb.Append(" · más cercano a ").Append(nearest.ToString("0.0")).Append(" m · héroe en ").Append(Players.Primary.transform.position.ToString("0"));
        sb.Append(" · velocidad héroe=").Append(st.MoveSpeed.ToString("0.0"));
        return sb.ToString();
    }

    // ---------- Dificultad ----------

    public static string Easy() { Difficulty.SetForTesting(Difficulty.Level.Facil); return "fácil"; }
    public static string Normal() { Difficulty.SetForTesting(Difficulty.Level.Normal); return "normal"; }
    public static string Hard() { Difficulty.SetForTesting(Difficulty.Level.Dificil); return "difícil"; }

    // Un corrupto recién creado con la dificultad de ahora: su vida y sus tiempos efectivos.
    public static string Sample()
    {
        EnemyCamp camp = Gen.RoadCamps[0];
        EnemyDefinition def = GameDatabase.Instance.FindEnemy("Corrupt_Melee");
        EnemyBrain b = camp.SpawnMember(def, camp.transform.position, 2, Color.gray);
        Difficulty.Tuning t = Difficulty.Now;
        string r = t.name + ": vida=" + Mathf.RoundToInt(b.GetComponent<Damageable>().maxHealth) + " daño×" + t.damage + " vista=" + (def.sightRange * t.sight).ToString("0.0")
            + " m reacción=" + t.reaction + " s aviso=" + (def.windup * t.windup).ToString("0.00") + " s espera=" + (def.cooldown * t.cooldown).ToString("0.00")
            + " s a la vez=" + t.attackers + " correa=" + (25f * t.leash).ToString("0");
        Object.Destroy(b.gameObject);
        return r;
    }

    // ---------- Encuentros obligatorios ----------

    public static string GateState()
    {
        var g = CitadelGate.Instance;
        if (g == null) return "sin sello";
        var L = Gen.Layout;
        var path = new UnityEngine.AI.NavMeshPath();
        UnityEngine.AI.NavMesh.SamplePosition(VillageLayout.CitadelRoadPoint(L, 0.95f, out _), out var a, 3f, UnityEngine.AI.NavMesh.AllAreas);
        UnityEngine.AI.NavMesh.SamplePosition(L.castle.TownCenter, out var b, 6f, UnityEngine.AI.NavMesh.AllAreas);
        UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position, UnityEngine.AI.NavMesh.AllAreas, path);
        var sb = new StringBuilder("encuentros=" + g.Cleared + "/" + g.Required + " ciudadela abierta=" + CitadelGate.CitadelOpen
            + " · fin del camino→plaza de la ciudadela=" + path.status + " · asedio=" + (CitadelSiege.Instance != null ? CitadelSiege.Instance.Current.ToString() : "-")
            + " · etapa=" + QuestLog.Stage(QuestLog.Citadel) + " · |");
        foreach (EnemyCamp c in g.camps) sb.Append(' ').Append(c.campId).Append(CitadelGate.Done(c) ? "✓" : "·").Append(" vivos=").Append(c.Members.Count);
        return sb.ToString();
    }

    // Despierta el grupo y lo mata entero (como si el héroe lo hubiera derrotado).
    public static string KillCamp1() => KillCamp(0);
    public static string KillCamp2() => KillCamp(1);
    public static string KillCamp3() => KillCamp(2);

    private static string KillCamp(int i)
    {
        EnemyCamp camp = Gen.RoadCamps[i];
        if (camp.Members.Count == 0) camp.Spawn();
        var members = new List<EnemyBrain>(camp.Members);
        foreach (EnemyBrain b in members)
            b.GetComponent<Damageable>().TakeHit(new DamageInfo { amount = 99999f, type = DamageType.Physical, source = Players.PrimaryObject });
        return camp.campId + " derrotado (" + members.Count + ")";
    }

    // Lleva al héroe junto al hielo de la puerta y lo manda a la plaza (no debe poder entrar).
    public static string TryEnterCitadel()
    {
        var L = Gen.Layout;
        Vector3 toVillage = new Vector3(-L.castle.Gate.x, 0f, -L.castle.Gate.z).normalized;
        Teleport(L.castle.Gate + toVillage * 6f + Vector3.up * 0.1f);
        Players.Primary.MoveTo(L.castle.TownCenter);
        return "intentando entrar en la ciudadela";
    }

    public static string InCitadel()
    {
        var L = Gen.Layout;
        Vector3 d = Players.Primary.transform.position - L.castle.TownCenter;
        d.y = 0f;
        return "a " + d.magnitude.ToString("0") + " m de la plaza de la ciudadela · asedio=" + (CitadelSiege.Instance != null ? CitadelSiege.Instance.Current.ToString() : "-");
    }

    public static string Attackers() => "atacantes cuerpo a cuerpo contados=" + EnemyBrain.MeleeAttackers + " / máximo " + EnemyBrain.MaxMeleeAttackers;
}
