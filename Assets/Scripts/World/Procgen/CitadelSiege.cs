using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Mini nivel del poblado del castillo: al entrar en la plaza con la misión activa llegan oleadas de corruptos
// y, al final, baja del viaducto Gorvald, el bárbaro corrupto con el hacha envenenada.
public class CitadelSiege : MonoBehaviour
{
    public enum Phase { Idle, Waves, Boss, Done }

    public static readonly string[] BarbarianLines =
    {
        "...El hielo... se aparta de mis ojos. Por fin veo con claridad.",
        "Yo era Gorvald, guardián de esta ciudadela. Juré protegerla... y fui yo quien la quemó.",
        "Un ninja maldito bajó de la montaña con su tropa de élite. Sus palabras eran escarcha: nos corrompió uno a uno.",
        "Él sigue dentro del castillo, en la sala del trono. Entra, héroe... y libera a los que aún quedan."
    };

    private static readonly int[,] Waves = { { 5, 0 }, { 4, 3 }, { 6, 3 } };

    public Phase Current { get; private set; } = Phase.Idle;
    public int Wave { get; private set; }
    public int WaveCount => Waves.GetLength(0);
    public int Remaining => _alive.FindAll(b => b != null && !b.IsDead).Count;
    public static CitadelSiege Instance { get; private set; }
    public BossController Boss => _boss;

    private VillageLayout.CastleDesign _castle;
    private VillageLayout.CitadelTown _town;
    private Vector3 _center;
    private Transform _player;
    private CharacterStats _stats;
    private readonly List<EnemyBrain> _alive = new List<EnemyBrain>();
    private BossController _boss;
    private int _seed, _attempt;
    private Coroutine _run;

    public void Setup(VillageLayout.CastleDesign castle, VillageLayout.CitadelTown town, int seed)
    {
        _castle = castle;
        _town = town;
        _seed = seed;
        _center = castle.TownCenter;
        transform.position = _center;
        if (QuestLog.Stage(QuestLog.Citadel) >= QuestLog.CitadelBarbarian) Current = Phase.Done;
    }

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (_player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p == null) return;
            _player = p.transform;
            _stats = p.GetComponent<CharacterStats>();
        }
        if (Current == Phase.Done) return;

        if (Current == Phase.Idle)
        {
            if (QuestLog.Stage(QuestLog.Citadel) != QuestLog.CitadelTalked) return;
            Vector3 d = _player.position - _center;
            d.y = 0f;
            if (d.magnitude < VillageLayout.CitadelTown.ArenaRadius + 1f && _stats != null && !_stats.IsDead) _run = StartCoroutine(Run());
            return;
        }
        // El héroe cayó o huyó lejos: el asedio se reinicia.
        if (_stats != null && (_stats.IsDead || (_player.position - _center).magnitude > 75f)) Abort();
    }

    private IEnumerator Run()
    {
        Current = Phase.Waves;
        _attempt++;
        BonfireBanner.Show("¡EMBOSCADA!", BonfireBanner.Threat);
        Notifications.Show("Los corruptos rodean la plaza. ¡Resiste!", FrostboundUI.Negative);
        yield return new WaitForSeconds(1.6f);

        var rng = new DeterministicRng(SeedUtil.Combine(_seed, SeedUtil.FromString("asedio"), _attempt));
        int level = EnemySpawner.LevelForPlayer(0);
        EnemyDefinition melee = EnemySpawner.Find("Corrupt_Melee"), ranged = EnemySpawner.Find("Corrupt_Ranged");
        for (Wave = 1; Wave <= WaveCount; Wave++)
        {
            int m = Waves[Wave - 1, 0], r = Waves[Wave - 1, 1];
            for (int i = 0; i < m + r; i++)
            {
                Vector3 local = _town.spawnPoints[rng.NextInt(0, _town.spawnPoints.Count)] + new Vector3(rng.Range(-1.5f, 1.5f), 0f, rng.Range(-1.5f, 1.5f));
                EnemyBrain b = EnemySpawner.Spawn(i < m ? melee : ranged, _castle.ToWorld(local), rng.Range(0f, 360f), transform, level, rng.Chance(0.05f * Wave), rng);
                if (b == null) continue;
                b.leashOverride = 80f;
                b.SetHome(_center);
                b.Slot = i;
                b.Alert();
                _alive.Add(b);
                yield return new WaitForSeconds(0.18f);
            }
            while (Remaining > 0) yield return new WaitForSeconds(0.4f);
            _alive.Clear();
            if (Wave < WaveCount)
            {
                Notifications.Show("Oleada " + Wave + " superada. Vienen más...", FrostboundUI.Muted);
                yield return new WaitForSeconds(3.5f);
            }
        }

        Current = Phase.Boss;
        Notifications.Show("Algo enorme baja del viaducto...", FrostboundUI.Negative);
        yield return new WaitForSeconds(2f);
        EnemyDefinition viking = EnemySpawner.Find("Corrupt_Viking");
        EnemyBrain boss = EnemySpawner.Spawn(viking, _castle.ToWorld(_town.bossSpawn), _castle.yaw, transform, level + 2, false, rng);
        if (boss == null) yield break;
        _boss = BossController.Make(boss, "Gorvald, el Bárbaro Corrupto", "Guardián caído de la ciudadela", 6f, 1.3f, 1.4f, def =>
        {
            def.attackRange = 2.6f;
            def.areaRadius = 2.9f;
            def.windup = 0.85f;
            def.cooldown = 2.3f;
            def.sightRange = 40f;
            def.lootPicks = 5;
        });
        _boss.poisonDps = 4f + level * 0.6f;
        _boss.poisonSeconds = 5f;
        _boss.dyingLines = BarbarianLines;
        _boss.Defeated += OnBossDefeated;
        boss.SetHome(_center);
        BonfireBanner.Show("GORVALD, EL BÁRBARO CORRUPTO", BonfireBanner.Threat);
        boss.Alert();
    }

    private void OnBossDefeated(BossController b)
    {
        Current = Phase.Done;
        QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelBarbarian);
        BonfireBanner.Show("GRAN ENEMIGO DERRIBADO", BonfireBanner.Victory);
        Notifications.Show("El sello de hielo del castillo se ha roto.", FrostboundUI.Gold);
    }

    private void Abort()
    {
        if (_run != null) StopCoroutine(_run);
        _run = null;
        foreach (EnemyBrain b in _alive) if (b != null && !b.IsDead) Destroy(b.gameObject);
        _alive.Clear();
        if (_boss != null && !_boss.Health.IsDead && !_boss.Speaking) Destroy(_boss.gameObject);
        _boss = null;
        Wave = 0;
        Current = Phase.Idle;
    }

    // Para las comprobaciones: arranca el asedio aunque el héroe no esté en la plaza.
    public void ForceStart()
    {
        if (Current == Phase.Idle) _run = StartCoroutine(Run());
    }

    public void KillWave()
    {
        foreach (EnemyBrain b in _alive)
            if (b != null && !b.IsDead) b.GetComponent<Damageable>().Kill(_player != null ? _player.gameObject : null, Vector3.forward);
    }
}
