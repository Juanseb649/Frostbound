using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// IA de los pingüinos corruptos por el Frost (spec §4.2):
// Idle → Alerta (chillido que despierta al grupo) → Perseguir → Atacar (con aviso) → Recuperarse → Morir.
// Los de cuerpo a cuerpo rodean al jugador en puestos; los arqueros mantienen la distancia.
// Nunca entran en una zona segura (campamento, claro de Steve): si el jugador se refugia, vuelven a su campamento.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Damageable))]
public class EnemyBrain : MonoBehaviour
{
    public enum State { Idle, Chase, Windup, Recover, Return, Dead }

    public EnemyDefinition definition;
    [Tooltip("Parte visual (el pingüino): se congela y estalla al morir.")]
    public Transform model;

    public const int MaxMeleeAttackers = 4;
    private const float Leash = 25f;
    private const float AlertRadius = 12f;

    public State Current { get; private set; } = State.Idle;
    public int Level { get; private set; } = 1;
    public bool Champion { get; private set; }
    public EnemyCamp Camp { get; private set; }
    public int Slot { get; set; }
    public bool IsDead => Current == State.Dead;

    // Ajustes para oleadas y jefes.
    [System.NonSerialized] public float leashOverride;
    [System.NonSerialized] public float damageMultiplier = 1f;
    [System.NonSerialized] public bool ignoreSafeZone;
    [System.NonSerialized] public bool paused;
    public event System.Action<EnemyBrain> Killed;
    public event System.Action<CharacterStats> HitPlayerEvent;
    private float LeashDistance => leashOverride > 0f ? leashOverride : Leash;

    private static readonly List<EnemyBrain> Active = new List<EnemyBrain>();
    private static int _meleeAttackers;
    private static Transform _player;
    private static CharacterStats _playerStats;
    private static PlayerDodge _playerDodge;

    private NavMeshAgent _agent;
    private Damageable _dmg;
    private PenguinLocomotionAnimator _rig;
    private bool _struck;
    private const float StrikeLead = 0.1f;
    private Vector3 _home;
    private float _stateUntil, _readyAt, _retreatAt, _wanderAt, _frenzyUntil;
    private int _frenzyStacks;
    private bool _countedAttacker;
    private int _hitsLeft;
    private Color _plumage = Color.gray;
    private DamageInfo _lastHit;
    private readonly List<Renderer> _eyes = new List<Renderer>();
    private MaterialPropertyBlock _eyeBlock;
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private static readonly Color EyeIdle = new Color(0.62f, 0.73f, 1f) * 0.6f;
    private static readonly Color EyeCharge = new Color(0.12f, 0.31f, 0.82f) * 4f;

    public static IReadOnlyList<EnemyBrain> All => Active;
    public Color Plumage => _plumage;
    public static Transform Player => FindPlayer() ? _player : null;

    // Teletransporte (el ninja maldito aparece a la espalda del héroe).
    public void Blink(Vector3 to)
    {
        if (_agent != null && _agent.isOnNavMesh) _agent.Warp(to);
        else transform.position = to;
    }

    // Golpe inmediato para habilidades de jefe (no pasa por la preparación normal).
    public void ForceAttack()
    {
        if (Current == State.Dead || paused) return;
        StartAttack(1f);
    }

    public void Setup(EnemyDefinition def, EnemyCamp camp, int level, bool champion, Color plumage)
    {
        definition = def;
        Camp = camp;
        Level = Mathf.Max(1, level);
        Champion = champion;
        _home = transform.position;
        _plumage = plumage;

        _dmg = GetComponent<Damageable>();
        _dmg.displayName = def.displayName;
        _dmg.maxHealth = def.HealthAt(Level) * (champion ? 2.5f : 1f);
        _dmg.ResetHealth();

        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = def.moveSpeed;
        _agent.stoppingDistance = def.ranged ? 0.5f : Mathf.Max(0.2f, def.attackRange * 0.6f);
        _agent.angularSpeed = 720f;
        _agent.acceleration = 24f;

        PenguinOutfit outfit = GetComponentInChildren<PenguinOutfit>();
        if (outfit != null && def.gear != null)
        {
            outfit.useClassOutfit = false;
            outfit.startingItems.Clear();
            outfit.Equip(def.gear);
        }
        _eyes.Clear();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            if (r.name.StartsWith("Eyes")) _eyes.Add(r);
        SetEyes(EyeIdle);
        PenguinAppearance look = GetComponentInChildren<PenguinAppearance>();
        if (look != null) look.SetPlumage(plumage);
        _dmg.RefreshBaseColors();
        if (champion)
        {
            transform.localScale *= 1.15f;
            NameTag tag = GetComponent<NameTag>();
            if (tag != null) tag.displayName = "Campeón " + def.displayName.ToLowerInvariant();
        }
    }

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _dmg = GetComponent<Damageable>();
        if (model == null && transform.childCount > 0) model = transform.GetChild(0);
        _rig = GetComponentInChildren<PenguinLocomotionAnimator>();
    }

    void OnEnable()
    {
        Active.Add(this);
        _dmg.Damaged += OnDamaged;
        _dmg.Died += OnDied;
    }

    void OnDisable()
    {
        Active.Remove(this);
        ReleaseAttackerSlot();
        _dmg.Damaged -= OnDamaged;
        _dmg.Died -= OnDied;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active.Clear();
        _meleeAttackers = 0;
        _player = null;
        _playerStats = null;
        _playerDodge = null;
    }

    private static bool FindPlayer()
    {
        if (_player != null) return true;
        GameObject p = GameObject.Find("Player");
        if (p == null) return false;
        _player = p.transform;
        _playerStats = p.GetComponent<CharacterStats>();
        _playerDodge = p.GetComponent<PlayerDodge>();
        return true;
    }

    // ---------- Bucle ----------

    void Update()
    {
        if (definition == null || Current == State.Dead || !_agent.isOnNavMesh) return;
        if (paused)
        {
            _agent.isStopped = true;
            return;
        }
        if (!FindPlayer()) return;

        float frenzy = Time.time < _frenzyUntil ? 1f + 0.15f * _frenzyStacks : 1f;
        if (Time.time >= _frenzyUntil) _frenzyStacks = 0;
        float mult = _dmg.SpeedMultiplier * frenzy;
        _agent.speed = definition.moveSpeed * mult;
        _agent.isStopped = _dmg.IsFrozen;
        if (_dmg.IsFrozen) return;

        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        bool playerGone = _playerStats == null || _playerStats.IsDead || SafeZone.Contains(_player.position, 1.5f);

        switch (Current)
        {
            case State.Idle:
                if (!playerGone && dist < definition.sightRange) Alert();
                else Wander();
                break;

            case State.Chase:
                if (playerGone || (transform.position - _home).magnitude > LeashDistance || (!ignoreSafeZone && SafeZone.Contains(transform.position, 2f)))
                {
                    GoHome();
                    break;
                }
                if (definition.ranged) ChaseRanged(dist, toPlayer, mult);
                else ChaseMelee(dist, toPlayer, mult);
                break;

            case State.Windup:
                Face(toPlayer);
                if (!_struck && !definition.ranged && Time.time >= _stateUntil - StrikeLead)
                {
                    _struck = true;
                    if (_rig != null) _rig.PlayBruteStrike();
                }
                if (Time.time >= _stateUntil) ResolveAttack(dist, toPlayer);
                break;

            case State.Recover:
                Face(toPlayer);
                if (Time.time >= _stateUntil)
                {
                    ReleaseAttackerSlot();
                    Current = playerGone ? State.Return : State.Chase;
                }
                break;

            case State.Return:
                if ((transform.position - _home).magnitude < 1.5f)
                {
                    Current = State.Idle;
                    _dmg.Heal(_dmg.maxHealth);
                }
                else if (!playerGone && dist < definition.sightRange * 0.7f && !SafeZone.Contains(transform.position, 2f)
                         && (transform.position - _home).magnitude < LeashDistance * 0.6f)
                    Alert();
                break;
        }
    }

    private void Wander()
    {
        if (Time.time < _wanderAt) return;
        _wanderAt = Time.time + Random.Range(4f, 9f);
        Vector2 r = Random.insideUnitCircle * 4f;
        Vector3 p = _home + new Vector3(r.x, 0f, r.y);
        if (!SafeZone.Contains(p, 2f) && NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            _agent.SetDestination(hit.position);
    }

    private void ChaseMelee(float dist, Vector3 toPlayer, float mult)
    {
        bool canAttack = Time.time >= _readyAt && (_countedAttacker || _meleeAttackers < MaxMeleeAttackers);
        if (dist <= definition.attackRange && canAttack)
        {
            StartAttack(mult);
            return;
        }
        // Rodear: cada uno va a su puesto alrededor del jugador; si no le toca atacar, espera un poco más lejos.
        float ring = canAttack ? 1.3f : 3f;
        float angle = Slot * 60f + Time.time * 8f;
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * ring;
        Vector3 target = dist > 5f ? _player.position : _player.position + offset;
        SetDestination(target);
        if (dist < 2.5f) Face(toPlayer);
    }

    private void ChaseRanged(float dist, Vector3 toPlayer, float mult)
    {
        Vector2 pref = definition.preferredDistance;
        if (dist < 4f && Time.time >= _retreatAt)
        {
            _retreatAt = Time.time + 4f;
            SetDestination(transform.position - toPlayer.normalized * 3f);
            return;
        }
        if (dist > pref.y || !HasLineOfSight())
        {
            SetDestination(_player.position);
            return;
        }
        if (dist < pref.x) SetDestination(transform.position - toPlayer.normalized * 1.5f);
        else _agent.ResetPath();
        Face(toPlayer);
        if (Time.time >= _readyAt && dist <= definition.attackRange) StartAttack(mult);
    }

    private void SetDestination(Vector3 p)
    {
        if (SafeZone.Contains(p, 2f)) return;
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) _agent.SetDestination(hit.position);
    }

    private void Face(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.01f) return;
        Quaternion target = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 540f * Time.deltaTime);
    }

    private bool HasLineOfSight()
    {
        Vector3 from = transform.position + Vector3.up * 0.7f;
        Vector3 to = _player.position + Vector3.up * 0.7f;
        if (Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform.IsChildOf(_player) || hit.collider.GetComponentInParent<EnemyBrain>() != null;
        return true;
    }

    // ---------- Ataque ----------

    private void StartAttack(float speedMult)
    {
        if (!definition.ranged && !_countedAttacker)
        {
            _countedAttacker = true;
            _meleeAttackers++;
        }
        _agent.ResetPath();
        Current = State.Windup;
        float windup = definition.windup / Mathf.Max(0.5f, speedMult);
        _stateUntil = Time.time + windup;
        _hitsLeft = definition.hitsPerAttack;
        _struck = false;
        if (!definition.ranged && _rig != null) _rig.PlayBruteWindup(windup);
        SetEyes(EyeCharge);
    }

    private void ResolveAttack(float dist, Vector3 toPlayer)
    {
        if (definition.ranged) Shoot();
        else if (definition.areaRadius > 0f ? dist <= definition.areaRadius : dist <= definition.attackRange + 0.5f)
            HitPlayer(toPlayer);

        _hitsLeft--;
        if (_hitsLeft > 0)
        {
            _stateUntil = Time.time + 0.25f;
            _struck = false;
            if (!definition.ranged && _rig != null) _rig.PlayBruteWindup(0.25f, _rig.NextSwing);
            return;
        }
        float frenzy = Time.time < _frenzyUntil ? 1f + 0.15f * _frenzyStacks : 1f;
        _readyAt = Time.time + definition.cooldown / frenzy;
        Current = State.Recover;
        _stateUntil = Time.time + 0.35f;
        SetEyes(EyeIdle);
    }

    // Aviso del ataque: los ojos pasan de blanquecinos a cobalto intenso.
    private void SetEyes(Color emission)
    {
        if (_eyes.Count == 0) return;
        if (_eyeBlock == null) _eyeBlock = new MaterialPropertyBlock();
        foreach (Renderer r in _eyes)
        {
            if (r == null) continue;
            for (int m = 0; m < r.sharedMaterials.Length; m++)
            {
                r.GetPropertyBlock(_eyeBlock, m);
                _eyeBlock.SetColor(EmissionId, emission);
                r.SetPropertyBlock(_eyeBlock, m);
            }
        }
    }

    // ¿El golpe final vino de un arma con runa de hielo grabada?
    private static bool FrostWeapon(GameObject source)
    {
        if (source == null) return false;
        Equipment eq = source.GetComponent<Equipment>();
        ItemStack w = eq != null ? eq.MainWeapon : null;
        if (w == null || w.IsBroken || w.runes == null) return false;
        foreach (ItemDefinition r in w.runes)
            if (r != null && r.runeEffect == RuneEffect.Frost) return true;
        return false;
    }

    private float RollDamage()
    {
        return Random.Range(definition.damage.x, definition.damage.y) * definition.DamageScaleAt(Level) * (Champion ? 1.3f : 1f) * damageMultiplier;
    }

    private void HitPlayer(Vector3 toPlayer)
    {
        if (_playerStats == null || _playerStats.IsDead) return;
        if (_playerDodge != null && _playerDodge.IsRolling) return;
        DamagePlayer(_playerStats, RollDamage(), toPlayer);
        HitPlayerEvent?.Invoke(_playerStats);
    }

    // Vuelve a fijar el punto al que regresa (las oleadas lo ponen en la plaza).
    public void SetHome(Vector3 home) => _home = home;

    public static void DamagePlayer(CharacterStats stats, float amount, Vector3 direction = default)
    {
        if (stats == null || stats.IsDead || stats.invulnerable) return;
        if (direction.sqrMagnitude > 0.001f) stats.lastHitDirection = direction.normalized;
        float before = stats.currentHealth;
        stats.TakeDamage(amount);
        float taken = before - stats.currentHealth;
        if (taken > 0f && WorldHUD.Instance != null)
            WorldHUD.Instance.Popup(stats.transform.position + Vector3.up * 1.7f + Random.insideUnitSphere * 0.15f,
                Mathf.CeilToInt(taken).ToString(), FrostboundUI.HealthBar, 0.9f);
    }

    private void Shoot()
    {
        Vector3 origin = transform.position + Vector3.up * 0.75f + transform.forward * 0.45f;
        Vector3 aim = _player.position + Vector3.up * 0.7f;
        Rigidbody rb = _player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            aim += v * Mathf.Clamp((aim - origin).magnitude / definition.projectileSpeed, 0f, 0.6f) * 0.6f;
        }
        EnemyProjectile.Launch(origin, aim - origin, definition.projectileSpeed, definition.attackRange + 4f,
            RollDamage(), definition.projectileMaterial, gameObject);
    }

    private void ReleaseAttackerSlot()
    {
        if (!_countedAttacker) return;
        _countedAttacker = false;
        _meleeAttackers = Mathf.Max(0, _meleeAttackers - 1);
    }

    // ---------- Grupo ----------

    public void Alert()
    {
        if (Current == State.Dead) return;
        if (Current == State.Idle || Current == State.Return)
        {
            Current = State.Chase;
            if (Camp != null) Camp.AlertAll(this);
            else
                foreach (EnemyBrain b in Active)
                    if (b != this && (b.transform.position - transform.position).sqrMagnitude < AlertRadius * AlertRadius) b.WakeUp();
        }
    }

    public void WakeUp()
    {
        if (Current == State.Idle || Current == State.Return) Current = State.Chase;
    }

    public void Frenzy()
    {
        if (Current == State.Dead) return;
        _frenzyStacks = Mathf.Min(2, _frenzyStacks + 1);
        _frenzyUntil = Time.time + 3f;
        WakeUp();
    }

    public void GoHome()
    {
        ReleaseAttackerSlot();
        Current = State.Return;
        if (_agent.isOnNavMesh) _agent.SetDestination(_home);
    }

    private void OnDamaged(DamageInfo info)
    {
        if (Current == State.Dead) return;
        _lastHit = info;
        if (Current == State.Idle || Current == State.Return) Alert();
        if (!info.periodic && info.direction.sqrMagnitude > 0.01f && _agent.isOnNavMesh)
        {
            Vector3 push = info.direction;
            push.y = 0f;
            _agent.Move(push.normalized * (info.critical ? 0.6f : 0.25f));
        }
    }

    private void OnDied()
    {
        ReleaseAttackerSlot();
        Current = State.Dead;
        if (_agent.isOnNavMesh) _agent.ResetPath();
        _agent.enabled = false;
        SetEyes(Color.black);
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

        if (FindPlayer() && _playerStats != null)
            _playerStats.AddExperience(Mathf.RoundToInt(definition.ExperienceAt(Level) * (Champion ? 2.5f : 1f)));
        if (Camp != null) Camp.OnMemberDied(this);
        else
            foreach (EnemyBrain b in Active)
                if (b != this && (b.transform.position - transform.position).sqrMagnitude < 36f) b.Frenzy();

        Killed?.Invoke(this);
        Vector3 center = transform.position + Vector3.up * 0.6f;
        EnemyLoot.Drop(this, center);
        // Estallido de hielo solo si lo mató un arma con runa de hielo; si no, el cuerpo cae con física.
        // Los jefes no estallan ni se hunden: su cuerpo se queda donde cayó.
        bool boss = GetComponent<BossController>() != null;
        if (!boss && FrostWeapon(_lastHit.source)) IceShatter.Play(this, model != null ? model : transform, center, _plumage);
        else EnemyPhysicsDeath.Play(this, _lastHit.direction, boss);
    }
}
