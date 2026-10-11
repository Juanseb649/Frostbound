using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Habilidad de clase (tecla F o RB en el mando): gasta maná y tiene enfriamiento. El daño crece con el atributo Maná.
//   Caballero · Carga de escudo: embiste hacia el cursor, golpea y derriba a los que atraviesa.
//   Mago      · Gólem de nieve: levanta un muñeco de nieve que lanza bolas heladas (máx. 2, 45 s).
//   Shinobi   · Abanico de kunais: lanza cinco kunais en abanico que envenenan.
//   Bárbaro   · Grito de guerra: aturde y ralentiza alrededor y recupera vida.
[RequireComponent(typeof(CharacterStats))]
public class PlayerAbility : MonoBehaviour
{
    public enum Kind { ShieldCharge, SnowGolem, KunaiFan, WarCry }

    public struct Info
    {
        public string name, description, icon;
        public float cost, cooldown;
    }

    [Tooltip("Daño extra de la habilidad por punto de Maná (0.04 = +4 %).")]
    public float manaScaling = 0.04f;

    private CharacterStats _stats;
    private PlayerController _pc;
    private PlayerCombat _combat;
    private PlayerDodge _dodge;
    private PenguinRigAnimator _rig;
    private Camera _cam;
    private float _readyAt;
    private readonly Collider[] _overlap = new Collider[48];
    private readonly List<Damageable> _hit = new List<Damageable>();

    public Kind Current { get; private set; }
    public Info Data => Describe(Current);
    public float CooldownLeft => Mathf.Max(0f, _readyAt - Time.time);
    public float Cooldown01 => Data.cooldown > 0f ? CooldownLeft / Data.cooldown : 0f;
    public bool CanAfford => _stats != null && _stats.currentMana >= Data.cost;
    public event System.Action<string> Failed;

    public static Info Describe(Kind k)
    {
        switch (k)
        {
            case Kind.SnowGolem: return new Info { name = "Gólem de nieve", icon = "Golem", cost = 30f, cooldown = 10f, description = "Levanta un gólem de nieve que te sigue y lanza bolas heladas a los corruptos. Los atrae hacia él. Hasta 2 a la vez durante 45 s." };
            case Kind.KunaiFan: return new Info { name = "Abanico de kunais", icon = "Ninja", cost = 15f, cooldown = 4f, description = "Lanza cinco kunais en abanico hacia el cursor. Envenenan." };
            case Kind.WarCry: return new Info { name = "Grito de guerra", icon = "Viking", cost = 20f, cooldown = 9f, description = "Aturde y ralentiza a los que te rodean y recupera vida." };
            default: return new Info { name = "Carga de escudo", icon = "Knight", cost = 18f, cooldown = 6f, description = "Embiste hacia el cursor: golpea y derriba a los que atraviesas." };
        }
    }

    public static Kind ForClass(CharacterClass cls)
    {
        string id = cls != null ? cls.className : "";
        switch (id)
        {
            case "Mage": return Kind.SnowGolem;
            case "Ninja": return Kind.KunaiFan;
            case "Viking": return Kind.WarCry;
            default: return Kind.ShieldCharge;
        }
    }

    void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _pc = GetComponent<PlayerController>();
        _combat = GetComponent<PlayerCombat>();
        _dodge = GetComponent<PlayerDodge>();
        _rig = GetComponentInChildren<PenguinRigAnimator>();
    }

    void Start() => RefreshClass();

    public void RefreshClass()
    {
        CharacterClass cls = _stats.characterClass;
        if (cls == null && GameSession.Instance != null) cls = GameSession.Instance.SelectedClass;
        Current = ForClass(cls);
    }

    void Update()
    {
        if (_cam == null) _cam = Camera.main;
        RefreshClass();
        if (GameplayInput.Blocked || _stats.IsDead || PauseMenu.IsPaused) return;
        Keyboard kb = PlayerController.UsesKeyboard(this) ? Keyboard.current : null;
        Gamepad pad = PlayerController.PadOf(this);
        bool pressed = (kb != null && kb.fKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame);
        if (pressed) TryUse();
    }

    public bool TryUse()
    {
        if (_dodge != null && _dodge.IsRolling) return false;
        if (Time.time < _readyAt)
        {
            Failed?.Invoke(Data.name + " se está recargando");
            return false;
        }
        if (!_stats.SpendMana(Data.cost))
        {
            Failed?.Invoke("Necesitas " + Mathf.CeilToInt(Data.cost) + " de maná para " + Data.name);
            return false;
        }
        _readyAt = Time.time + Data.cooldown;
        Vector3 aim = AimDirection();
        switch (Current)
        {
            case Kind.SnowGolem: StartCoroutine(SummonGolem(aim)); break;
            case Kind.KunaiFan: KunaiFan(aim); break;
            case Kind.WarCry: StartCoroutine(WarCry()); break;
            default: StartCoroutine(ShieldCharge(aim)); break;
        }
        return true;
    }

    private float Power => _stats.Damage * (1f + _stats.Mana * manaScaling);

    private Vector3 AimDirection()
    {
        Vector3 dir = transform.forward;
        Mouse mouse = Mouse.current;
        PlayerController pc = GetComponent<PlayerController>();
        if (!PlayerController.UsesKeyboard(this) || (PlayerController.RecentP1Pad != null && PlayerController.PadOf(this) == PlayerController.RecentP1Pad))
            return pc != null ? pc.PadAimDirection() : dir;
        if (mouse != null && _cam != null)
        {
            Ray ray = _cam.ScreenPointToRay(mouse.position.ReadValue());
            var ground = new Plane(Vector3.up, transform.position);
            if (ground.Raycast(ray, out float enter)) dir = ray.GetPoint(enter) - transform.position;
        }
        dir.y = 0f;
        return dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
    }

    private void Hit(Damageable d, float multiplier, DamageType type)
    {
        Vector3 dir = d.transform.position - transform.position;
        d.TakeHit(new DamageInfo { amount = Power * multiplier * Random.Range(0.9f, 1.1f), type = type, source = gameObject, direction = dir });
    }

    private List<Damageable> Around(Vector3 center, float radius)
    {
        _hit.Clear();
        int n = Physics.OverlapSphereNonAlloc(center, radius, _overlap, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Damageable d = _overlap[i].GetComponentInParent<Damageable>();
            if (d == null || d.IsDead || _hit.Contains(d) || d.transform.IsChildOf(transform)) continue;
            SnowGolem golem = d.GetComponent<SnowGolem>();
            if (d.GetComponent<EnemyBrain>() == null && !d.trainingDummy && (golem == null || !golem.Hostile)) continue;
            _hit.Add(d);
        }
        return _hit;
    }

    // ---------- Caballero ----------

    private IEnumerator ShieldCharge(Vector3 dir)
    {
        const float distance = 7f, seconds = 0.32f;
        if (_pc != null)
        {
            _pc.FaceAndHold(dir, seconds + 0.15f);
            _pc.Lunge(dir, distance, 0f, seconds);
        }
        if (_rig != null) _rig.PlayHeavyCombo(1, seconds + 0.2f);
        var struck = new HashSet<Damageable>();
        for (float t = 0f; t < seconds + 0.05f; t += Time.deltaTime)
        {
            foreach (Damageable d in Around(transform.position + Vector3.up * 0.6f + dir * 0.8f, 1.6f))
            {
                if (!struck.Add(d)) continue;
                Hit(d, 1.6f, DamageType.Physical);
                d.Knockback(dir, 2.2f);
                d.ApplySlow(0.6f, 1.2f);
            }
            yield return null;
        }
        GroundImpact.Spawn(transform.position + dir * 0.8f, 1.8f, 1.1f);
        CameraFollow.Shake(struck.Count > 0 ? 0.3f : 0.18f, 0.3f);
    }

    // ---------- Mago ----------

    public int GolemsAlive
    {
        get
        {
            int n = 0;
            foreach (SnowGolem g in SnowGolem.Instances) if (g != null && g.Alive) n++;
            return n;
        }
    }

    private IEnumerator SummonGolem(Vector3 dir)
    {
        if (_pc != null) _pc.FaceAndHold(dir, 0.55f);
        if (_rig != null) _rig.PlayAttack(0.55f);
        yield return new WaitForSeconds(0.3f);
        float health = 60f + _stats.level * 14f + _stats.Mana * 4f;
        SnowGolem.Summon(gameObject, transform.position + dir * 2.2f, health, Power * 0.32f);
        CameraFollow.Shake(0.12f, 0.2f);
    }

    // ---------- Shinobi ----------

    private void KunaiFan(Vector3 dir)
    {
        if (_pc != null) _pc.FaceAndHold(dir, 0.35f);
        if (_rig != null) _rig.PlayAttack(0.35f);
        GameObject model = KunaiModel();
        Vector3 origin = transform.position + Vector3.up * 0.8f + dir * 0.4f;
        for (int i = -2; i <= 2; i++)
        {
            Vector3 d = Quaternion.Euler(0f, i * 11f, 0f) * dir;
            Projectile.Launch(model, origin, d, 26f, 14f, gameObject, (target, point) =>
            {
                if (target == null || target.IsDead) return;
                Hit(target, 0.85f, DamageType.Physical);
                target.ApplyPoison(Power * 0.12f, 4f);
            }, true);
        }
    }

    private static GameObject _kunai;

    private GameObject KunaiModel()
    {
        if (_kunai != null) return _kunai;
        ItemDatabase db = GameDatabase.Instance != null ? GameDatabase.Instance.items : null;
        if (db != null)
            foreach (ItemDefinition item in db.items)
                if (item != null && item.weaponType == WeaponType.ThrowingKunai && item.weaponModel != null) return _kunai = item.weaponModel;
        return _combat != null ? _combat.defaultArrow : null;
    }

    // ---------- Bárbaro ----------

    private IEnumerator WarCry()
    {
        if (_pc != null) _pc.FaceAndHold(transform.forward, 0.6f);
        if (_rig != null) _rig.PlayHeavyCombo(3, 0.6f);
        yield return new WaitForSeconds(0.3f);
        GroundImpact.Spawn(transform.position, 6f, 1.8f);
        CameraFollow.Shake(0.35f, 0.45f);
        foreach (Damageable d in Around(transform.position + Vector3.up * 0.6f, 6.2f))
        {
            Hit(d, 0.7f, DamageType.Physical);
            d.ApplySlow(0.55f, 4f);
            d.Freeze(0.8f);
            d.Knockback(d.transform.position - transform.position, 1.6f);
        }
        _stats.Heal(_stats.MaxHealth * 0.15f);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _kunai = null;
}
