using System.Collections.Generic;
using UnityEngine;

// El Corazón del Frost, jefe final: un cristal gigante que late en el centro de la cámara del sello. No se mueve.
// 1. Dispara esquirlas y llama a oleadas de cristalizados.
// 2. (66 %) Se cubre con un escudo que alimentan tres cristales alrededor de la cámara: hay que romperlos.
// 3. (33 %) Expulsa el hielo en anillos que crecen desde el centro: hay que rodar para cruzarlos.
// Se monta sobre un EnemyBrain (barra de jefe, daño, botín y misiones) con el pingüino oculto.
[RequireComponent(typeof(BossController))]
public class FrostHeartBoss : MonoBehaviour
{
    private enum Phase { None, Waves, Shield, Rings }

    private BossController _boss;
    private EnemyBrain _brain;
    private Phase _phase = Phase.None;
    private float _nextWave, _nextRing;
    private Transform _core;
    private Renderer _shield;
    private readonly List<FeedingCrystal> _feeders = new List<FeedingCrystal>();
    private readonly List<EnemyBrain> _spawned = new List<EnemyBrain>();
    private static Material _heart, _shell, _shieldMat;

    public float ChamberRadius = 18f;
    public bool Shielded => _phase == Phase.Shield && _feeders.Exists(f => f != null && !f.Broken);

    void Start()
    {
        _boss = GetComponent<BossController>();
        _brain = GetComponent<EnemyBrain>();
        Body();
        SetPhase(Phase.Waves);
        _nextWave = Time.time + 8f;
    }

    void OnDestroy()
    {
        foreach (FeedingCrystal f in _feeders) if (f != null) Destroy(f.gameObject);
        foreach (EnemyBrain b in _spawned) if (b != null) Destroy(b.gameObject);
    }

    void Update()
    {
        // Late.
        float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * 2.4f)), 8f);
        if (_core != null) _core.localScale = Vector3.one * (1f + beat * 0.08f);
        if (_boss.Health.IsDead) return;
        _spawned.RemoveAll(b => b == null || b.Current == EnemyBrain.State.Dead);

        float hp = _boss.Health.Health / Mathf.Max(1f, _boss.Health.maxHealth);
        Phase want = hp > 0.66f ? Phase.Waves : hp > 0.33f ? Phase.Shield : Phase.Rings;
        // El escudo aguanta mientras quede un cristal que lo alimente.
        if (_phase == Phase.Shield && want == Phase.Rings && Shielded) want = Phase.Shield;
        if (want != _phase) SetPhase(want);

        bool shielded = Shielded;
        _boss.Health.invulnerable = shielded;
        if (_shield != null) _shield.enabled = shielded;
        if (_phase == Phase.Shield && !shielded && _boss.Health.healthFloor > 0f) _boss.Health.healthFloor = 0f;

        if (!_boss.Engaged) return;
        if (_phase != Phase.Rings && Time.time >= _nextWave && _spawned.Count < BossTuning.MaxWaveAdds)
        {
            _nextWave = Time.time + (_phase == Phase.Waves ? 16f : 20f);
            Wave(_phase == Phase.Waves ? 2 : 1);
        }
        if (_phase == Phase.Rings && Time.time >= _nextRing)
        {
            _nextRing = Time.time + BossTuning.RingEvery;
            IceRing.Spawn(transform.position, ChamberRadius, 3f, BossTuning.HeartRing(_brain.Level));
        }
    }

    private void SetPhase(Phase p)
    {
        Phase old = _phase;
        _phase = p;
        EnemyDefinition d = _brain.definition;
        d.ranged = true;
        d.moveSpeed = 0f;
        d.attackRange = 26f;
        d.sightRange = 30f;
        d.preferredDistance = new Vector2(0f, 26f);
        d.projectileSpeed = p == Phase.Rings ? 16f : 12f;
        d.windup = 0.7f;
        d.cooldown = p == Phase.Rings ? 2.6f : 2f;
        d.hitsPerAttack = 2;
        if (old == Phase.None) return;
        AudioManager.Play3D(AudioId.Boss_Grunt1, transform.position, 0.6f, 3f, 45f);
        if (p == Phase.Shield)
        {
            BonfireBanner.Show("EL CORAZÓN SE PROTEGE", new Color(0.55f, 0.8f, 1f));
            Notifications.Show("Rompe los tres cristales que alimentan su escudo.", FrostboundUI.Gold);
            // Que no pase de fase sin romper el escudo.
            _boss.Health.healthFloor = _boss.Health.maxHealth * 0.34f;
            Feeders();
        }
        else if (p == Phase.Rings)
        {
            BonfireBanner.Show("EL CORAZÓN SE DESBORDA", new Color(0.55f, 0.8f, 1f));
            Notifications.Show("Anillos de hielo: rueda para cruzarlos.", FrostboundUI.Gold);
            _nextRing = Time.time + 1.5f;
        }
    }

    private void Wave(int count)
    {
        GameDatabase db = GameDatabase.Instance;
        if (db == null) return;
        EnemyDefinition def = db.FindEnemy("Corrupt_Melee");
        if (def == null || def.prefab == null) return;
        for (int i = 0; i < count; i++)
        {
            Vector3 at = transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * (ChamberRadius * 0.7f);
            if (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
            GameObject go = Instantiate(def.prefab, at, Quaternion.identity);
            EnemyBrain b = go.GetComponent<EnemyBrain>();
            b.Setup(def, null, Mathf.Max(1, _brain.Level - 2), false, new Color(0.72f, 0.86f, 1f));
            b.leashOverride = 60f;
            b.Alert();
            _spawned.Add(b);
        }
        if (WorldHUD.Instance != null) WorldHUD.Instance.Popup(transform.position + Vector3.up * 6f, "¡Hijos del hielo, despertad!", new Color(0.55f, 0.85f, 1f), 1.1f);
    }

    private void Feeders()
    {
        for (int i = 0; i < 3; i++)
        {
            float a = (i / 3f) * Mathf.PI * 2f + 0.5f;
            Vector3 at = transform.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (ChamberRadius * 0.72f);
            if (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
            _feeders.Add(FeedingCrystal.Spawn(at, transform, BossTuning.HeartFeeder(_brain.Level)));
        }
    }

    // ---------- Aspecto ----------

    // Oculta al pingüino y monta el cristal: un núcleo que late, un caparazón de esquirlas y el escudo.
    private void Body()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        NameTag tag = GetComponent<NameTag>();
        if (tag != null) tag.enabled = false;
        if (_heart == null)
        {
            _heart = MenuRuins.Lit("Corazón del Frost", new Color(0.35f, 0.65f, 1f), 0.95f, 0f, new Color(0.6f, 1.4f, 3.2f));
            _shell = MenuRuins.Lit("Caparazón del Corazón", new Color(0.3f, 0.48f, 0.8f), 0.9f, 0f, new Color(0.05f, 0.12f, 0.3f));
            _shieldMat = MenuRuins.Transparent("Escudo del Corazón", MenuRuins.Soft(32), new Color(0.6f, 0.85f, 1f, 0.35f), true);
        }
        Mesh octa = MenuRuins.Octahedron();
        float inv = 1f / Mathf.Max(0.01f, transform.lossyScale.x);
        var root = new GameObject("Cristal_Corazon").transform;
        root.SetParent(transform, false);
        root.localScale = Vector3.one * inv;
        _core = new GameObject("Nucleo").transform;
        _core.SetParent(root, false);
        _core.localPosition = Vector3.up * 3.2f;
        var core = new GameObject("Latido");
        core.transform.SetParent(_core, false);
        core.transform.localScale = new Vector3(1.6f, 2.6f, 1.6f);
        core.AddComponent<MeshFilter>().sharedMesh = octa;
        core.AddComponent<MeshRenderer>().sharedMaterial = _heart;
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * 360f;
            var s = new GameObject("Esquirla");
            s.transform.SetParent(root, false);
            s.transform.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 1.4f + (i % 3) * 0.5f, 1.6f);
            s.transform.localRotation = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(25f + (i % 2) * 15f, 0f, 0f);
            s.transform.localScale = new Vector3(0.7f, 2.4f + (i % 3) * 0.6f, 0.7f);
            s.AddComponent<MeshFilter>().sharedMesh = octa;
            s.AddComponent<MeshRenderer>().sharedMaterial = _shell;
        }
        var shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(shield.GetComponent<Collider>());
        shield.name = "Escudo";
        shield.transform.SetParent(root, false);
        shield.transform.localPosition = Vector3.up * 2.8f;
        shield.transform.localScale = new Vector3(6.5f, 7f, 6.5f);
        _shield = shield.GetComponent<Renderer>();
        _shield.sharedMaterial = _shieldMat;
        _shield.enabled = false;
        var light = new GameObject("Luz_Corazon").AddComponent<Light>();
        light.transform.SetParent(_core, false);
        light.type = LightType.Point;
        light.color = new Color(0.5f, 0.75f, 1f);
        light.range = 26f;
        light.intensity = 4f;
        light.shadows = LightShadows.None;
        // Más fácil de golpear: el cuerpo ocupa casi todo el cristal.
        CapsuleCollider col = GetComponent<CapsuleCollider>();
        if (col != null)
        {
            col.radius = 2.2f * inv;
            col.height = 6f * inv;
            col.center = new Vector3(0f, 3f * inv, 0f);
        }
    }

    // Al caer: el cristal se rompe en pedazos que salen despedidos.
    public void Shatter()
    {
        Transform root = transform.Find("Cristal_Corazon");
        if (root == null) return;
        foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>())
        {
            if (r.name == "Escudo") { r.enabled = false; continue; }
            r.transform.SetParent(null, true);
            Rigidbody rb = r.gameObject.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.AddExplosionForce(900f, transform.position + Vector3.up * 2f, 12f, 0.6f);
            r.gameObject.AddComponent<SelfDestructQuiet>().seconds = 6f;
        }
        Light l = root.GetComponentInChildren<Light>();
        if (l != null) l.enabled = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _heart = null;
        _shell = null;
        _shieldMat = null;
    }
}

// Cristal que alimenta el escudo del Corazón: se rompe a golpes; un haz de luz lo une al Corazón mientras vive.
public class FeedingCrystal : MonoBehaviour
{
    private Damageable _dmg;
    private LineRenderer _beam;
    private Transform _heart;
    public bool Broken => _dmg == null || _dmg.IsDead;

    public static FeedingCrystal Spawn(Vector3 at, Transform heart, float health)
    {
        var go = new GameObject("Cristal_Alimentador");
        go.transform.position = at;
        Mesh octa = MenuRuins.Octahedron();
        Material m = MenuRuins.Lit("Cristal alimentador", new Color(0.45f, 0.75f, 1f), 0.9f, 0f, new Color(0.4f, 1f, 2.2f));
        for (int i = 0; i < 3; i++)
        {
            var s = new GameObject("Esquirla");
            s.transform.SetParent(go.transform, false);
            s.transform.localPosition = new Vector3((i - 1) * 0.4f, 1.2f + (i == 1 ? 0.4f : 0f), 0f);
            s.transform.localRotation = Quaternion.Euler(0f, i * 40f, (i - 1) * -15f);
            s.transform.localScale = new Vector3(0.6f, i == 1 ? 2.4f : 1.6f, 0.6f);
            s.AddComponent<MeshFilter>().sharedMesh = octa;
            s.AddComponent<MeshRenderer>().sharedMaterial = m;
        }
        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.9f;
        col.height = 3f;
        col.center = Vector3.up * 1.5f;
        var f = go.AddComponent<FeedingCrystal>();
        f._heart = heart;
        f._dmg = go.AddComponent<Damageable>();
        f._dmg.displayName = "Cristal que alimenta";
        f._dmg.maxHealth = health;
        f._dmg.ResetHealth();
        f._dmg.Died += f.OnBroken;
        f._beam = go.AddComponent<LineRenderer>();
        f._beam.positionCount = 2;
        f._beam.widthMultiplier = 0.18f;
        f._beam.sharedMaterial = MenuRuins.Transparent("Haz del Corazón", MenuRuins.Soft(16), new Color(0.55f, 0.85f, 1f, 0.8f), true);
        return f;
    }

    void Update()
    {
        if (_beam == null || _heart == null) return;
        _beam.SetPosition(0, transform.position + Vector3.up * 1.6f);
        _beam.SetPosition(1, _heart.position + Vector3.up * 3.2f);
    }

    private void OnBroken()
    {
        if (_beam != null) _beam.enabled = false;
        if (WorldHUD.Instance != null) WorldHUD.Instance.Popup(transform.position + Vector3.up * 2f, "¡Cristal roto!", new Color(0.6f, 0.9f, 1f), 1.1f);
        AudioManager.Play3D(AudioId.Corrupt_Death, transform.position, 0.6f, 2f, 30f, 0.1f);
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
        Destroy(gameObject, 0.5f);
    }
}

// Anillo de hielo que crece desde el centro: golpea a quien lo toque, salvo que esté rodando.
public class IceRing : MonoBehaviour
{
    private float _maxRadius, _seconds, _damage, _born;
    private readonly HashSet<PlayerController> _hit = new HashSet<PlayerController>();
    private readonly List<Transform> _segments = new List<Transform>();
    private static Material _mat;
    private const int Segments = 40;

    public static IceRing Spawn(Vector3 center, float maxRadius, float seconds, float damage)
    {
        var go = new GameObject("Anillo_Hielo");
        go.transform.position = center + Vector3.up * 0.3f;
        var ring = go.AddComponent<IceRing>();
        ring._maxRadius = maxRadius;
        ring._seconds = seconds;
        ring._damage = damage;
        ring._born = Time.time;
        if (_mat == null) _mat = MenuRuins.Lit("Anillo de hielo", new Color(0.6f, 0.85f, 1f), 0.9f, 0f, new Color(0.4f, 0.9f, 1.8f));
        Mesh octa = MenuRuins.Octahedron();
        for (int i = 0; i < Segments; i++)
        {
            var s = new GameObject("Tramo");
            s.transform.SetParent(go.transform, false);
            s.AddComponent<MeshFilter>().sharedMesh = octa;
            var r = s.AddComponent<MeshRenderer>();
            r.sharedMaterial = _mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring._segments.Add(s.transform);
        }
        AudioManager.Play3D(AudioId.Boss_Grunt1, center, 0.5f, 3f, 40f);
        return ring;
    }

    void Update()
    {
        float k = (Time.time - _born) / _seconds;
        if (k >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        float radius = Mathf.Lerp(1.5f, _maxRadius, k);
        for (int i = 0; i < _segments.Count; i++)
        {
            float a = i / (float)Segments * Mathf.PI * 2f;
            _segments[i].localPosition = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
            _segments[i].localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            _segments[i].localScale = new Vector3(radius * 2f * Mathf.PI / Segments * 0.9f, 0.6f, 0.35f);
        }
        foreach (PlayerController pc in Players.All)
        {
            if (pc == null || pc.Stats == null || pc.Stats.IsDead || _hit.Contains(pc)) continue;
            Vector3 d = pc.transform.position - transform.position;
            d.y = 0f;
            if (Mathf.Abs(d.magnitude - radius) > 0.7f) continue;
            PlayerDodge dodge = pc.GetComponent<PlayerDodge>();
            if (dodge != null && dodge.IsRolling) continue;
            _hit.Add(pc);
            EnemyBrain.DamagePlayer(pc.Stats, _damage, d);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _mat = null;
}

// Desaparece sin aviso al cabo de unos segundos (pedazos del Corazón).
public class SelfDestructQuiet : MonoBehaviour
{
    public float seconds = 5f;
    void Start() => Destroy(gameObject, seconds);
}
