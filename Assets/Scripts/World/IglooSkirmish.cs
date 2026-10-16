using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Un grupo de iglús del bosque. O bien sus aldeanos ya están muertos, o bien la primera vez que el héroe se acerca
// los corruptos los atacan: los aldeanos pelean (con las mismas animaciones de golpe) pero siempre caen.
// Pasa una sola vez (se guarda en la partida); al volver, sus cuerpos siguen ahí.
public class IglooSkirmish : MonoBehaviour
{
    public string id;
    public bool fight;
    public int villagers = 3;
    public int seed;

    public const float TriggerDistance = 40f;
    private static readonly string[] Weapons = { "spear", "knife", "hammer", "sickle", "machete", "sword" };
    private static readonly string[] Names = { "Aldeano Tolo", "Aldeana Pía", "Aldeano Nuno", "Aldeana Brisa", "Aldeano Copo", "Aldeana Lía", "Aldeano Fermín" };

    public bool Done => QuestLog.Stage(id) >= 1;
    public int VillagersAlive { get; private set; }
    public int Bodies { get; private set; }
    public bool Fighting { get; private set; }

    private readonly List<EnemyBrain> _enemies = new List<EnemyBrain>();

    IEnumerator Start()
    {
        yield return null;
        if (!fight || Done)
        {
            SpawnBodies();
            yield break;
        }
        while (NearestPlayer() > TriggerDistance) yield return new WaitForSeconds(0.25f);
        QuestLog.SetStage(id, 1);
        yield return Fight();
    }

    private float NearestPlayer()
    {
        float best = float.MaxValue;
        foreach (PlayerController p in Players.All)
            if (p != null) best = Mathf.Min(best, Vector3.Distance(p.transform.position, transform.position));
        return best;
    }

    private Vector3 Spot(int i, float radius, DeterministicRng rng)
    {
        float a = (i + rng.Range(-0.3f, 0.3f)) / Mathf.Max(1, villagers) * Mathf.PI * 2f;
        Vector3 p = transform.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas)) p = hit.position;
        return p;
    }

    // Ya muertos: tendidos alrededor de los iglús.
    private void SpawnBodies()
    {
        var rng = new DeterministicRng(SeedUtil.Combine(seed, SeedUtil.FromString(id + "_cuerpos")));
        for (int i = 0; i < villagers; i++)
        {
            EnemyBrain v = MakeVillager(Spot(i, rng.Range(3f, 7f), rng), rng.Range(0f, 360f), rng, i);
            if (v == null) continue;
            PenguinRagdoll body = FallenWarriors.Lay(v, v.gameObject.name + " (muerto)", rng);
            if (body != null) { Bodies++; StartCoroutine(FallenWarriors.FreezeWhenSettled(body)); }
        }
    }

    private IEnumerator Fight()
    {
        Fighting = true;
        var rng = new DeterministicRng(SeedUtil.Combine(seed, SeedUtil.FromString(id + "_pelea")));
        var folk = new List<EnemyBrain>();
        for (int i = 0; i < villagers; i++)
        {
            EnemyBrain v = MakeVillager(Spot(i, 2.5f, rng), 0f, rng, i);
            if (v != null) folk.Add(v);
        }
        VillagersAlive = folk.Count;
        // Los corruptos llegan desde el bosque, del lado contrario al héroe.
        Vector3 away = transform.position - ClosestPlayerPos();
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
        string[] kinds = { "Corrupt_Melee", "Corrupt_Melee", "Corrupt_Knight", "Corrupt_Viking" };
        int count = Mathf.Max(2, folk.Count);
        for (int i = 0; i < count; i++)
        {
            EnemyDefinition def = EnemySpawner.Find(kinds[rng.NextInt(0, kinds.Length)]) ?? EnemySpawner.Find("Corrupt_Melee");
            Vector3 side = Vector3.Cross(Vector3.up, away) * ((i - (count - 1) * 0.5f) * 2.2f);
            EnemyBrain e = EnemySpawner.Spawn(def, transform.position + away * 9f + side, Quaternion.LookRotation(-away).eulerAngles.y, transform.parent, EnemySpawner.LevelForPlayer(), false, rng);
            if (e == null) continue;
            e.enabled = false;                      // de momento los mueve la escaramuza
            _enemies.Add(e);
        }
        var duels = new List<Coroutine>();
        for (int i = 0; i < folk.Count; i++)
        {
            float dies = rng.Range(5f, 12f) + i * 1.5f;
            duels.Add(StartCoroutine(Duel(folk[i], i, dies, rng.Range(0f, 0.6f))));
        }
        while (VillagersAlive > 0)
        {
            WakeIfProvoked();
            yield return null;
        }
        // Sin aldeanos en pie: los corruptos vuelven a ser enemigos normales (y van a por el héroe).
        foreach (EnemyBrain e in _enemies) Wake(e);
        Fighting = false;
    }

    private Vector3 ClosestPlayerPos()
    {
        Vector3 best = transform.position + Vector3.back * 30f;
        float d = float.MaxValue;
        foreach (PlayerController p in Players.All)
            if (p != null && (p.transform.position - transform.position).sqrMagnitude < d) { d = (p.transform.position - transform.position).sqrMagnitude; best = p.transform.position; }
        return best;
    }

    // Si el héroe se mete en la pelea (cerca o golpeando), ese corrupto pasa a pelear con él.
    private void WakeIfProvoked()
    {
        foreach (EnemyBrain e in _enemies)
        {
            if (e == null || e.enabled) continue;
            Damageable d = e.GetComponent<Damageable>();
            bool hurt = d != null && d.Health < d.maxHealth - 0.01f;
            bool close = false;
            foreach (PlayerController p in Players.All)
                if (p != null && Vector3.Distance(p.transform.position, e.transform.position) < 9f) close = true;
            if (hurt || close) Wake(e);
        }
    }

    private static void Wake(EnemyBrain e)
    {
        if (e == null || e.enabled || e.IsDead) return;
        NavMeshAgent a = e.GetComponent<NavMeshAgent>();
        if (a != null && a.isOnNavMesh) a.ResetPath();
        e.enabled = true;
    }

    private EnemyBrain FoeFor(int index)
    {
        for (int k = 0; k < _enemies.Count; k++)
        {
            EnemyBrain e = _enemies[(index + k) % _enemies.Count];
            if (e != null && !e.IsDead) return e;
        }
        return null;
    }

    // Un aldeano contra un corrupto: se acercan, se golpean por turnos y el aldeano cae a los dieAfter segundos.
    private IEnumerator Duel(EnemyBrain villager, int index, float dieAfter, float delay)
    {
        yield return new WaitForSeconds(delay);
        float end = Time.time + dieAfter;
        var vRig = villager.GetComponentInChildren<PenguinLocomotionAnimator>();
        float vNext = 0f, eNext = 0f;
        float orphanSince = -1f;
        EnemyBrain foe = FoeFor(index);
        while (Time.time < end)
        {
            if (foe == null || foe.IsDead || !foe.gameObject.activeInHierarchy) foe = FoeFor(index);
            if (foe == null)
            {
                if (orphanSince < 0f) orphanSince = Time.time;
                if (Time.time - orphanSince > 1.5f) break;   // malherido, cae igual
                yield return null;
                continue;
            }
            Vector3 vp = villager.transform.position, ep = foe.transform.position;
            Vector3 to = ep - vp; to.y = 0f;
            float dist = to.magnitude;
            if (dist > 0.01f) villager.transform.rotation = Quaternion.Slerp(villager.transform.rotation, Quaternion.LookRotation(to / dist), Time.deltaTime * 8f);
            if (dist > 1.5f)
            {
                Vector3 next = Vector3.MoveTowards(vp, ep, Time.deltaTime * 2.6f);
                if (NavMesh.SamplePosition(next, out NavMeshHit h, 1f, NavMesh.AllAreas)) next.y = h.position.y;
                villager.transform.position = next;
            }
            else if (Time.time >= vNext && vRig != null)
            {
                vNext = Time.time + Random.Range(0.9f, 1.4f);
                StartCoroutine(Swing(vRig, 0.3f));
            }
            // El corrupto (si la escaramuza aún lo controla) va a por el aldeano.
            if (!foe.enabled)
            {
                NavMeshAgent ag = foe.GetComponent<NavMeshAgent>();
                if (dist > 1.4f) { if (ag != null && ag.isOnNavMesh) ag.SetDestination(vp); }
                else
                {
                    if (ag != null && ag.isOnNavMesh) ag.ResetPath();
                    foe.transform.rotation = Quaternion.Slerp(foe.transform.rotation, Quaternion.LookRotation(-to.normalized), Time.deltaTime * 8f);
                    var eRig = foe.GetComponentInChildren<PenguinLocomotionAnimator>();
                    if (Time.time >= eNext && eRig != null)
                    {
                        eNext = Time.time + Random.Range(1.1f, 1.6f);
                        StartCoroutine(Swing(eRig, 0.4f));
                    }
                }
            }
            yield return null;
        }
        Vector3 hitDir = foe != null ? villager.transform.position - foe.transform.position : -villager.transform.forward;
        PenguinRagdoll body = FallenWarriors.Lay(villager, villager.gameObject.name + " (muerto)", hitDir);
        if (body != null) { Bodies++; StartCoroutine(FallenWarriors.FreezeWhenSettled(body)); }
        VillagersAlive--;
    }

    private static IEnumerator Swing(PenguinLocomotionAnimator rig, float windup)
    {
        rig.PlayBruteWindup(windup);
        yield return new WaitForSeconds(windup);
        if (rig != null) rig.PlayBruteStrike();
    }

    // Aldeano: el pingüino de los enemigos pero con plumaje sano, sin equipo corrupto y con una herramienta en la aleta.
    private EnemyBrain MakeVillager(Vector3 at, float yaw, DeterministicRng rng, int index)
    {
        EnemyDefinition baseDef = EnemySpawner.Find("Corrupt_Melee");
        if (baseDef == null || baseDef.prefab == null) return null;
        EnemyDefinition def = Instantiate(baseDef);
        def.gear = null;
        def.displayName = Names[(index + Mathf.Abs(seed)) % Names.Length];
        GameObject go = Instantiate(def.prefab, at, Quaternion.Euler(0f, yaw, 0f), transform);
        go.name = def.displayName;
        EnemyBrain brain = go.GetComponent<EnemyBrain>();
        PlumagePalette palette = GameDatabase.Instance != null ? GameDatabase.Instance.palette : null;
        Color plumage = palette != null && palette.entries != null && palette.entries.Count > 0
            ? palette.entries[rng.NextInt(0, palette.entries.Count)].color : new Color(0.15f, 0.35f, 0.75f);
        brain.Setup(def, null, 1, false, plumage);
        brain.enabled = false;
        Damageable d = go.GetComponent<Damageable>();
        if (d != null) d.enabled = false;
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        foreach (Collider c in go.GetComponents<Collider>()) c.enabled = false;
        NameTag tag = go.GetComponent<NameTag>();
        if (tag != null) { tag.displayName = def.displayName; tag.subtitle = "Aldeano"; }
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            if (r.name.StartsWith("Eyes")) { r.GetPropertyBlock(block); block.SetColor("_EmissionColor", Color.black); block.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.08f)); r.SetPropertyBlock(block); }
        ItemDatabase items = GameDatabase.Instance != null ? GameDatabase.Instance.items : null;
        ItemDefinition tool = items != null ? items.Find(Weapons[rng.NextInt(0, Weapons.Length)]) : null;
        if (tool != null) brain.EquipWeapon(tool);
        return brain;
    }
}
