using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Ataque del héroe con el arma equipada: cuerpo a cuerpo (arco delante del pingüino) o a distancia (proyectil).
// Clic derecho: atacar hacia el cursor (Espacio es rodar, PlayerDodge). Clic izquierdo sobre un objetivo: acercarse y atacar (PlayerController).
// Cada golpe gasta durabilidad; las runas grabadas aplican fuego, veneno, escarcha, cadena, robo de vida o empuje.
[RequireComponent(typeof(Equipment))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Sin arma")]
    public float unarmedRange = 1.4f;
    public float unarmedAttackSpeed = 1.4f;

    [Header("Ritmo")]
    [Tooltip("Velocidad de ataque extra por punto de Agilidad (0.01 = +1 %).")]
    public float agilityAttackSpeed = 0.01f;
    [Tooltip("Momento del golpe dentro de la animación (0–1).")]
    [Range(0.1f, 0.9f)] public float hitMoment = 0.45f;

    [Header("Combo de espada (estilo Prince of Persia)")]
    [Tooltip("Segundos tras un tajo para encadenar el siguiente.")]
    public float comboWindow = 0.45f;
    [Tooltip("Multiplicador de daño de cada tajo: derecha→izquierda, izquierda→derecha, giro de 360°.")]
    public float[] comboDamage = { 1f, 1.15f, 1.5f };
    [Tooltip("Duración de cada tajo respecto a un ataque normal.")]
    public float[] comboTiming = { 0.75f, 0.85f, 1.35f };
    [Tooltip("Probabilidad de crítico en el giro final (muy baja).")]
    [Range(0f, 0.2f)] public float spinCritChance = 0.03f;
    public float spinCritMultiplier = 2.5f;

    [Header("Combo pesado (mandobles, hachas largas, mazos)")]
    [Tooltip("Daño de cada golpe: tajo vertical, barrido, salto y aplastamiento.")]
    public float[] heavyDamage = { 1.3f, 1.1f, 1.75f };
    [Tooltip("Duración de cada golpe respecto a un ataque normal (lentos: el arma pesa).")]
    public float[] heavyTiming = { 1.1f, 1.0f, 1.35f };
    public float heavyComboWindow = 0.6f;

    [Header("Arco")]
    [Tooltip("Momento en que se suelta la flecha dentro del disparo (0–1).")]
    [Range(0.4f, 0.85f)] public float bowRelease = 0.64f;

    [Header("Proyectiles")]
    public float projectileSpeed = 24f;
    [Tooltip("Proyectil por defecto de los arcos si el objeto no trae uno.")]
    public GameObject defaultArrow;

    [Tooltip("Material de los iconos de los objetos tirados que no tienen modelo 3D.")]
    public Material lootSpriteMaterial;

    [Header("Durabilidad")]
    public float wearPerMeleeHit = 1f;
    public float wearPerShot = 0.5f;

    private Equipment _eq;
    private CharacterStats _stats;
    private PlayerController _pc;
    private PenguinRigAnimator _rig;
    private WeaponHolder _holder;
    private PlayerDodge _dodge;
    private Camera _cam;

    private float _readyAt;
    private bool _pending;
    private float _hitAt;
    private Vector3 _aimDir = Vector3.forward;
    private bool _lowWarned;
    private int _comboStep;
    private float _comboDeadline;
    private float _attackMultiplier = 1f;
    private bool _spinAttack;
    private int _heavyStep;
    private float _trailOn, _trailOff;
    private readonly List<Damageable> _scratch = new List<Damageable>();
    private readonly Collider[] _overlap = new Collider[48];

    public bool IsAttacking => _pending || Time.time < _readyAt;

    void Awake()
    {
        _eq = GetComponent<Equipment>();
        _stats = GetComponent<CharacterStats>();
        _pc = GetComponent<PlayerController>();
        _rig = GetComponentInChildren<PenguinRigAnimator>();
        _holder = GetComponentInChildren<WeaponHolder>();
        _dodge = GetComponent<PlayerDodge>();
        if (lootSpriteMaterial != null) WorldItem.DefaultSpriteMaterial = lootSpriteMaterial;
    }

    void OnEnable()
    {
        if (_eq != null) _eq.WeaponBroke += OnWeaponBroke;
    }

    void OnDisable()
    {
        if (_eq != null) _eq.WeaponBroke -= OnWeaponBroke;
    }

    void Update()
    {
        if (_holder == null) _holder = GetComponentInChildren<WeaponHolder>();
        if (_cam == null) _cam = Camera.main;

        if (_holder != null) _holder.SetTrail(Time.time >= _trailOn && Time.time < _trailOff);
        if (_comboStep > 0 && Time.time > _comboDeadline && Time.time > _readyAt + comboWindow) _comboStep = 0;
        if (_heavyStep > 0 && Time.time > _comboDeadline && Time.time > _readyAt + heavyComboWindow) _heavyStep = 0;

        if (_pending && Time.time >= _hitAt)
        {
            _pending = false;
            ResolveAttack();
        }

        if (GameplayInput.Blocked || _stats.IsDead || (_dodge != null && _dodge.IsRolling)) return;
        Mouse mouse = Mouse.current;
        bool wants = mouse != null && mouse.rightButton.isPressed && !GameplayInput.PointerOverUI;
        if (wants && CursorPoint(out Vector3 p)) TryAttack(p);
    }

    // ----- Datos del arma -----

    public ItemStack Weapon => _eq != null ? _eq.MainWeapon : null;

    private bool HasWeapon => Weapon != null && Weapon.item.IsWeapon;

    public float Range
    {
        get
        {
            if (!HasWeapon) return unarmedRange;
            return Weapon.item.WeaponInfo.range;
        }
    }

    public bool IsRanged => HasWeapon && Weapon.item.IsRanged;

    public int ComboStep => _comboStep;

    // Armas de una mano cuerpo a cuerpo: usan el combo de tres tajos.
    public bool UsesSwordCombo
    {
        get
        {
            if (!HasWeapon) return false;
            switch (Weapon.item.weaponType)
            {
                case WeaponType.Shuriken:
                    return false;
                default:
                    // Todas las armas de una mano cuerpo a cuerpo usan el combo del Príncipe.
                    return Weapon.item.Handling == WeaponHandling.OneHanded && !Weapon.item.IsRanged;
            }
        }
    }

    // Armas de dos manos con agarre a dos aletas: combo pesado.
    public bool UsesHeavyCombo => HasWeapon && !Weapon.item.IsRanged && Weapon.item.Handling == WeaponHandling.TwoHanded
                                  && Weapon.item.WeaponInfo.grip == WeaponGrip.TwoHand;

    public bool UsesBow => HasWeapon && Weapon.item.WeaponInfo.grip == WeaponGrip.Bow;

    public float AttacksPerSecond
    {
        get
        {
            float baseSpeed = HasWeapon ? Weapon.item.attackSpeed : unarmedAttackSpeed;
            float swift = _eq.RunePower(RuneEffect.Swiftness);
            return baseSpeed * (1f + _stats.Agility * agilityAttackSpeed) * (1f + swift) * (1f + _eq.AffixSum(AffixKind.AttackSpeed));
        }
    }

    // ----- Ataque -----

    public bool TryAttack(Vector3 targetPoint)
    {
        if (Time.time < _readyAt || _pending) return false;
        Vector3 dir = targetPoint - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
        _aimDir = dir.normalized;

        float duration = 1f / Mathf.Max(0.2f, AttacksPerSecond);
        if (UsesSwordCombo) return StartComboStep(duration);
        if (UsesHeavyCombo) return StartHeavyStep(duration);

        _comboStep = 0;
        _heavyStep = 0;
        if (UsesBow)
        {
            // Coger flecha, colocarla, tensar y soltar: la flecha sale cuando la aleta suelta la cuerda.
            float shot = Mathf.Max(0.55f, duration);
            _attackMultiplier = 1f;
            _spinAttack = false;
            _readyAt = Time.time + shot;
            _pending = true;
            _hitAt = Time.time + shot * bowRelease;
            if (_pc != null) _pc.FaceAndHold(_aimDir, shot * 0.95f);
            if (_rig != null) _rig.PlayBowShot(shot, bowRelease);
            return true;
        }
        _attackMultiplier = 1f;
        _spinAttack = false;
        _readyAt = Time.time + duration;
        _pending = true;
        _hitAt = Time.time + duration * hitMoment;
        if (_pc != null) _pc.FaceAndHold(_aimDir, duration * 0.85f);
        if (_rig != null) _rig.PlayAttack(duration * 0.95f);
        return true;
    }

    // Tres tajos encadenados: 1) derecha→izquierda con un paso, 2) izquierda→derecha agachado, 3) giro de 360°.
    private bool StartComboStep(float baseDuration)
    {
        int step = Time.time <= _comboDeadline && _comboStep > 0 && _comboStep < 3 ? _comboStep + 1 : 1;
        _comboStep = step;
        _heavyStep = 0;
        int i = step - 1;
        float[] minimum = { 0.4f, 0.45f, 0.78f };
        float duration = Mathf.Max(minimum[i], baseDuration * comboTiming[i]);
        float[] hit = { 0.48f, 0.45f, 0.72f };
        float[] step_m = { 0.45f, 0.25f, 0.3f };

        _attackMultiplier = comboDamage[i];
        _spinAttack = step == 3;
        _pending = true;
        _hitAt = Time.time + duration * hit[i];
        // Se puede encadenar un poco antes de que termine la animación: el siguiente tajo nace de la inercia del anterior.
        _readyAt = Time.time + duration * (step == 3 ? 1f : 0.9f);
        _comboDeadline = step == 3 ? 0f : Time.time + duration + comboWindow;
        _trailOn = Time.time + duration * (step == 3 ? 0.12f : 0.26f);
        _trailOff = Time.time + duration * (step == 3 ? 0.84f : 0.62f);

        if (_pc != null)
        {
            _pc.FaceAndHold(_aimDir, duration * 0.9f);
            _pc.Lunge(_aimDir, step_m[i], duration * 0.2f, duration * 0.35f);
        }
        if (_rig != null) _rig.PlaySwordCombo(step, duration);
        return true;
    }

    // Tres golpes pesados: 1) tajo vertical contra el suelo, 2) barrido horizontal con todo el cuerpo, 3) salto y aplastamiento.
    private bool StartHeavyStep(float baseDuration)
    {
        int step = Time.time <= _comboDeadline && _heavyStep > 0 && _heavyStep < 3 ? _heavyStep + 1 : 1;
        _heavyStep = step;
        _comboStep = 0;
        int i = step - 1;
        float[] minimum = { 0.95f, 0.9f, 1.25f };
        float duration = Mathf.Max(minimum[i], baseDuration * heavyTiming[i]);
        float[] hit = { 0.65f, 0.6f, 0.68f };
        float[] stepMeters = { 0.4f, 0.55f, 1.3f };
        float[] stepDelay = { 0.5f, 0.42f, 0.22f };

        _attackMultiplier = heavyDamage[i];
        _spinAttack = false;
        _pending = true;
        _hitAt = Time.time + duration * hit[i];
        _readyAt = Time.time + duration * (step == 3 ? 1f : 0.92f);
        _comboDeadline = step == 3 ? 0f : Time.time + duration + heavyComboWindow;
        _trailOn = Time.time + duration * (step == 2 ? 0.46f : 0.5f);
        _trailOff = Time.time + duration * (step == 2 ? 0.78f : 0.7f);

        if (_pc != null)
        {
            _pc.FaceAndHold(_aimDir, duration * 0.95f);
            _pc.Lunge(_aimDir, stepMeters[i], duration * stepDelay[i], duration * (step == 3 ? 0.42f : 0.2f));
        }
        if (_rig != null) _rig.PlayHeavyCombo(step, duration);
        return true;
    }

    private float _brokenNoticeAt;

    private void ResolveAttack()
    {
        ItemStack weapon = Weapon;
        // Arma rota: la animación se hace igual, pero no hace daño ni aplica runas.
        if (weapon != null && weapon.IsBroken)
        {
            if (Time.time > _brokenNoticeAt)
            {
                _brokenNoticeAt = Time.time + 3f;
                Notifications.Show(weapon.item.displayName + " está rota: no hace daño. Llévala al herrero.", FrostboundUI.Negative);
            }
            return;
        }
        if (IsRanged) Shoot(weapon);
        else Melee(weapon);
    }

    private void Melee(ItemStack weapon)
    {
        if (_heavyStep > 0 && UsesHeavyCombo)
        {
            HeavyMelee();
            return;
        }
        float range = Range;
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        // Los tajos horizontales barren un arco ancho; el giro de 360° alcanza todo alrededor.
        float arc = _spinAttack ? 181f : _comboStep > 0 ? 85f : 70f;
        Vector3 center = _spinAttack ? origin : origin + _aimDir * range * 0.5f;
        float radius = _spinAttack ? range + 0.4f : range * 0.75f;
        int n = Physics.OverlapSphereNonAlloc(center, radius, _overlap, ~0, QueryTriggerInteraction.Ignore);
        _scratch.Clear();
        for (int i = 0; i < n; i++)
        {
            Damageable d = _overlap[i].GetComponentInParent<Damageable>();
            if (d == null || d.IsDead || _scratch.Contains(d) || d.transform.IsChildOf(transform)) continue;
            Vector3 to = d.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > range + 0.6f) continue;
            if (to.sqrMagnitude > 0.04f && Vector3.Angle(_aimDir, to) > arc) continue;
            _scratch.Add(d);
        }
        bool crit = _spinAttack && _scratch.Count > 0 && Random.value < spinCritChance;
        foreach (Damageable d in _scratch) Strike(d, _attackMultiplier * (crit ? spinCritMultiplier : 1f), true, crit);
        if (crit) StartCoroutine(HitStop());
        if (_scratch.Count > 0) _eq.WearWeapon(wearPerMeleeHit);
    }

    // Golpe pesado: el tajo vertical alcanza lejos y estrecho, el barrido muy ancho y el aplastamiento todo alrededor.
    // Siempre hay impacto contra el suelo (anillo de nieve, temblor de cámara); si golpea a alguien, la pantalla se congela un instante.
    private void HeavyMelee()
    {
        float range = Range;
        int step = Mathf.Clamp(_heavyStep, 1, 3);
        float[] arcs = { 32f, 115f, 181f };
        float[] reach = { range + 0.6f, range + 0.2f, range + 1.1f };
        float[] shake = { 0.22f, 0.12f, 0.36f };
        float arc = arcs[step - 1], maxDist = reach[step - 1];
        Vector3 impact = step == 2 ? transform.position + _aimDir * range * 0.6f : transform.position + _aimDir * (step == 3 ? 1.2f : range * 0.85f);
        int n = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.6f, maxDist + 0.4f, _overlap, ~0, QueryTriggerInteraction.Ignore);
        _scratch.Clear();
        for (int i = 0; i < n; i++)
        {
            Damageable d = _overlap[i].GetComponentInParent<Damageable>();
            if (d == null || d.IsDead || _scratch.Contains(d) || d.transform.IsChildOf(transform)) continue;
            Vector3 to = d.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > maxDist + 0.4f) continue;
            if (step == 1)
            {
                // Línea estrecha delante: lo que queda bajo el arma al caer.
                float along = Vector3.Dot(to, _aimDir), side = Vector3.Cross(_aimDir, to).magnitude;
                if (along < -0.3f || side > 0.95f) continue;
            }
            else if (step == 3)
            {
                if ((d.transform.position - impact).magnitude > maxDist) continue;
            }
            else if (to.sqrMagnitude > 0.04f && Vector3.Angle(_aimDir, to) > arc) continue;
            _scratch.Add(d);
        }
        foreach (Damageable d in _scratch)
        {
            Strike(d, _attackMultiplier, true);
            // Los golpes pesados empujan.
            UnityEngine.AI.NavMeshAgent agent = d.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                Vector3 push = d.transform.position - (step == 3 ? impact : transform.position);
                push.y = 0f;
                agent.Move(push.normalized * (step == 3 ? 1.1f : 0.6f));
            }
        }
        if (step != 2) GroundImpact.Spawn(new Vector3(impact.x, transform.position.y, impact.z), step == 3 ? 2.6f : 1.4f, step == 3 ? 1.5f : 1f);
        CameraFollow.Shake(shake[step - 1] + (_scratch.Count > 0 ? 0.08f : 0f), step == 3 ? 0.4f : 0.28f);
        if (_scratch.Count > 0)
        {
            StartCoroutine(HitStop(step == 3 ? 0.11f : 0.07f));
            _eq.WearWeapon(wearPerMeleeHit);
        }
    }

    private void Shoot(ItemStack weapon)
    {
        ItemDefinition item = weapon.item;
        GameObject model = item.projectileModel != null ? item.projectileModel : (item.weaponType == WeaponType.ThrowingKunai ? item.weaponModel : defaultArrow);
        Vector3 origin = (_holder != null ? _holder.MuzzlePosition : transform.position + Vector3.up * 0.7f) + _aimDir * 0.3f;
        origin.y = Mathf.Max(origin.y, transform.position.y + 0.6f);
        bool spin = item.weaponType == WeaponType.ThrowingKunai;
        Projectile.Launch(model, origin, _aimDir, projectileSpeed, item.WeaponInfo.range, gameObject,
            (target, point) => Strike(target, 1f, true), spin);
        _eq.WearWeapon(wearPerShot);
    }

    // Daño del golpe más los efectos de las runas del arma.
    public void Strike(Damageable target, float multiplier, bool allowChain) => Strike(target, multiplier, allowChain, false);

    public void Strike(Damageable target, float multiplier, bool allowChain, bool critical)
    {
        if (target == null || target.IsDead) return;
        ItemStack weapon = Weapon;
        float spread = weapon != null ? weapon.item.damageSpread : 0.1f;
        float damage = _stats.Damage * Random.Range(1f - spread, 1f + spread) * multiplier;
        Vector3 dir = target.transform.position - transform.position;
        target.TakeHit(new DamageInfo { amount = damage, type = DamageType.Physical, source = gameObject, direction = dir, critical = critical });

        if (weapon == null || weapon.IsBroken) return;
        float steal = weapon.AffixSum(AffixKind.Lifesteal);
        if (steal > 0f) _stats.Heal(damage * steal);
        float frost = weapon.AffixSum(AffixKind.FrostChance);
        if (frost > 0f && Random.value < frost) target.ApplySlow(0.35f, 2f);
        foreach (ItemDefinition rune in weapon.runes)
        {
            if (rune == null) continue;
            switch (rune.runeEffect)
            {
                case RuneEffect.Fire:
                    target.ApplyBurn(rune.runePower, rune.runeDuration);
                    break;
                case RuneEffect.Poison:
                    target.ApplyPoison(rune.runePower, rune.runeDuration);
                    break;
                case RuneEffect.Frost:
                    target.ApplySlow(rune.runePower, rune.runeDuration);
                    if (Random.value < rune.runeChance) target.Freeze(1.5f);
                    break;
                case RuneEffect.Chain:
                    if (allowChain && Random.value < rune.runeChance) Chain(target, damage * rune.runePower);
                    break;
                case RuneEffect.Lifesteal:
                    _stats.Heal(damage * rune.runePower);
                    break;
                case RuneEffect.Knockback:
                    if (Random.value < rune.runeChance) target.Knockback(dir, rune.runePower);
                    break;
            }
        }
    }

    // Rayo que salta del objetivo a los 3 enemigos más cercanos.
    private void Chain(Damageable from, float damage)
    {
        int n = Physics.OverlapSphereNonAlloc(from.transform.position, 6f, _overlap, ~0, QueryTriggerInteraction.Ignore);
        var targets = new List<Damageable>();
        for (int i = 0; i < n; i++)
        {
            Damageable d = _overlap[i].GetComponentInParent<Damageable>();
            if (d == null || d == from || d.IsDead || targets.Contains(d)) continue;
            targets.Add(d);
        }
        targets.Sort((a, b) => (a.transform.position - from.transform.position).sqrMagnitude
            .CompareTo((b.transform.position - from.transform.position).sqrMagnitude));
        Vector3 prev = from.PopupPoint - Vector3.up * 0.5f;
        Color c = WeaponCatalog.DamageColor(DamageType.Lightning);
        for (int i = 0; i < targets.Count && i < 3; i++)
        {
            Vector3 p = targets[i].PopupPoint - Vector3.up * 0.5f;
            LightningArc.Spawn(prev, p, c);
            targets[i].TakeHit(new DamageInfo { amount = damage, type = DamageType.Lightning, source = gameObject });
            prev = p;
        }
        if (targets.Count == 0) LightningArc.Spawn(from.PopupPoint, from.PopupPoint + Vector3.up * 1.5f + Random.insideUnitSphere * 0.4f, c, 0.15f);
    }

    // Golpe crítico: el tiempo casi se detiene un instante para que se sienta el impacto.
    private System.Collections.IEnumerator HitStop(float seconds = 0.08f)
    {
        float previous = Time.timeScale;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(seconds);
        Time.timeScale = previous <= 0.05f ? 1f : previous;
    }

    // ----- Utilidades -----

    private bool CursorPoint(out Vector3 point)
    {
        point = transform.position + transform.forward;
        Mouse mouse = Mouse.current;
        if (mouse == null || _cam == null) return true;
        Ray ray = _cam.ScreenPointToRay(mouse.position.ReadValue());
        var ground = new Plane(Vector3.up, transform.position);
        if (ground.Raycast(ray, out float enter)) point = ray.GetPoint(enter);
        return true;
    }

    private void OnWeaponBroke(ItemStack weapon)
    {
        Notifications.Show("¡" + weapon.item.displayName + " se rompió! Se guardó en la mochila: llévala al herrero.", FrostboundUI.Negative);
    }

    void LateUpdate()
    {
        ItemStack w = Weapon;
        if (w == null || !w.item.HasDurability) return;
        if (!_lowWarned && w.Durability01 <= 0.2f && !w.IsBroken)
        {
            _lowWarned = true;
            Notifications.Show(w.item.displayName + " está a punto de romperse", FrostboundUI.Gold);
        }
        if (w.Durability01 > 0.2f) _lowWarned = false;
    }

}
