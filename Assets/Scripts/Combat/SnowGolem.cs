using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Gólem de nieve del mago: tres bloques de nieve apilados con gorro de lana, bufanda, nariz de carámbano y botones
// de hielo. Sigue al héroe, lanza bolas de nieve heladas a los corruptos cercanos y los atrae hacia él.
// Tiene su propia vida; al morir se desmorona en ragdoll (cada bloque con Rigidbody unido por articulaciones).
public class SnowGolem : MonoBehaviour
{
    public const int MaxAlive = 2;
    public const float Lifetime = 45f;

    private static readonly List<SnowGolem> All = new List<SnowGolem>();
    private static Material _snow, _coal, _ice, _wood, _wool, _scarf;

    public Damageable Health { get; private set; }
    public bool Alive => Health != null && !Health.IsDead && !_melting;

    private Transform _owner;
    private NavMeshAgent _agent;
    private float _damage, _readyAt, _bornAt, _repathAt;
    private float _throwT = 1f;
    private bool _melting;
    private Transform _model, _sway, _base, _body, _head, _armL, _armR;
    private Damageable _target;
    private readonly Collider[] _overlap = new Collider[32];
    private float _followSide;

    public static IReadOnlyList<SnowGolem> Instances => All;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
        _snow = null;
    }

    // Gólem vivo que un corrupto prefiere atacar: el más cercano dentro de su vista, si está más cerca que el héroe.
    public static SnowGolem PickFor(Vector3 enemy, Vector3 player, float sight, SnowGolem current)
    {
        SnowGolem best = null;
        float bestD = float.MaxValue;
        float toPlayer = (player - enemy).sqrMagnitude;
        foreach (SnowGolem g in All)
        {
            if (g == null || !g.Alive) continue;
            float d = (g.transform.position - enemy).sqrMagnitude;
            if (d > sight * sight) continue;
            float bias = g == current ? 0.6f : 1f;
            if (d * bias < bestD) { bestD = d * bias; best = g; }
        }
        if (best == null) return null;
        return bestD <= toPlayer * (best == current ? 1.4f : 0.85f) ? best : null;
    }

    public static SnowGolem Summon(GameObject owner, Vector3 position, float health, float damage)
    {
        int alive = 0;
        SnowGolem oldest = null;
        foreach (SnowGolem g in All)
        {
            if (g == null || !g.Alive) continue;
            alive++;
            if (oldest == null || g._bornAt < oldest._bornAt) oldest = g;
        }
        if (alive >= MaxAlive && oldest != null) oldest.Melt();

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas)) position = hit.position;
        // Se monta desactivado: así la vida (Damageable) arranca ya con el modelo como parte visual.
        var go = new GameObject("Golem_de_Nieve");
        go.SetActive(false);
        go.transform.position = position;
        go.transform.rotation = owner != null ? Quaternion.Euler(0f, owner.transform.eulerAngles.y, 0f) : Quaternion.identity;
        SnowGolem golem = go.AddComponent<SnowGolem>();
        golem.Setup(owner != null ? owner.transform : null, health, damage);
        go.SetActive(true);
        golem.Health.ResetHealth();
        golem.StartCoroutine(golem.Rise());
        return golem;
    }

    private void Setup(Transform owner, float health, float damage)
    {
        _owner = owner;
        _damage = damage;
        _bornAt = Time.time;
        _followSide = All.Count % 2 == 0 ? 1f : -1f;
        BuildModel();

        var col = gameObject.AddComponent<CapsuleCollider>();
        col.radius = 0.42f;
        col.height = 1.9f;
        col.center = new Vector3(0f, 0.95f, 0f);

        _agent = gameObject.AddComponent<NavMeshAgent>();
        _agent.radius = 0.4f;
        _agent.height = 1.9f;
        _agent.speed = 4.2f;
        _agent.acceleration = 14f;
        _agent.angularSpeed = 540f;
        _agent.stoppingDistance = 0.6f;

        Health = gameObject.AddComponent<Damageable>();
        Health.displayName = "Gólem de nieve";
        Health.maxHealth = health;
        Health.popupHeight = 2.1f;
        Health.visual = _model;
        Health.friendly = true;
        Health.Died += OnDied;
    }

    // ---------- Modelo ----------

    private static void EnsureMaterials()
    {
        if (_snow != null) return;
        _snow = Mat("Nieve compacta", SnowTexture(), Color.white, 0.15f);
        _coal = Mat("Carbón", null, new Color(0.08f, 0.08f, 0.1f), 0.3f);
        _ice = MenuRuins.Lit("Hielo del gólem", new Color(0.45f, 0.78f, 1f), 0.9f, 0f, new Color(0.2f, 0.55f, 1.1f));
        _wood = Mat("Rama", null, new Color(0.36f, 0.22f, 0.12f), 0.1f);
        _wool = Mat("Lana", WoolTexture(new Color(0.2f, 0.36f, 0.72f), new Color(0.85f, 0.9f, 1f)), Color.white, 0.05f);
        _scarf = Mat("Bufanda", WoolTexture(new Color(0.72f, 0.16f, 0.18f), new Color(0.95f, 0.85f, 0.6f)), Color.white, 0.05f);
    }

    private static Material Mat(string name, Texture2D tex, Color color, float smooth)
    {
        Material m = MenuRuins.Lit(name, color, smooth);
        if (tex != null) m.SetTexture("_BaseMap", tex);
        return m;
    }

    // Textura de nieve en píxeles (16×16, sin suavizado): blancos y azules muy claros con motas.
    private static Texture2D SnowTexture()
    {
        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "Nieve_Pixel" };
        var rng = new System.Random(77);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                double r = rng.NextDouble();
                Color c = r < 0.55 ? new Color(0.97f, 0.98f, 1f) : r < 0.8 ? new Color(0.9f, 0.94f, 0.98f) : r < 0.95 ? new Color(0.82f, 0.88f, 0.95f) : new Color(0.74f, 0.82f, 0.92f);
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        return tex;
    }

    private static Texture2D WoolTexture(Color a, Color b)
    {
        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "Lana_Pixel" };
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                tex.SetPixel(x, y, (y / 2) % 2 == 0 ? a : Color.Lerp(a, b, ((x + y) % 3 == 0) ? 0.7f : 0.15f));
        tex.Apply();
        return tex;
    }

    private Transform Cube(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    private Transform Part(string name, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(_sway, false);
        t.localPosition = pos;
        return t;
    }

    private void BuildModel()
    {
        EnsureMaterials();
        _model = new GameObject("Modelo").transform;
        _model.SetParent(transform, false);
        _sway = new GameObject("Balanceo").transform;
        _sway.SetParent(_model, false);

        // Base: el bloque más grande, un poco ladeado; botones de hielo.
        _base = Part("Base", new Vector3(0f, 0.4f, 0f));
        Cube(_base, "Bloque", Vector3.zero, new Vector3(0.8f, 0.8f, 0.74f), _snow, new Vector3(0f, 4f, 0f));
        Cube(_base, "Boton", new Vector3(0f, 0.12f, 0.38f), new Vector3(0.11f, 0.11f, 0.06f), _ice, new Vector3(0f, 0f, 45f));
        Cube(_base, "Boton", new Vector3(0f, -0.14f, 0.38f), new Vector3(0.11f, 0.11f, 0.06f), _ice, new Vector3(0f, 0f, 45f));

        // Cuerpo: con la bufanda y los brazos de rama.
        _body = Part("Cuerpo", new Vector3(0f, 1.1f, 0f));
        Cube(_body, "Bloque", Vector3.zero, new Vector3(0.64f, 0.62f, 0.6f), _snow, new Vector3(0f, -6f, 0f));
        Cube(_body, "Boton", new Vector3(0f, 0.08f, 0.31f), new Vector3(0.09f, 0.09f, 0.05f), _ice, new Vector3(0f, 0f, 45f));
        Cube(_body, "Boton", new Vector3(0f, -0.14f, 0.31f), new Vector3(0.09f, 0.09f, 0.05f), _ice, new Vector3(0f, 0f, 45f));
        Cube(_body, "Bufanda", new Vector3(0f, 0.32f, 0f), new Vector3(0.7f, 0.12f, 0.66f), _scarf);
        Cube(_body, "Bufanda_Cola", new Vector3(0.18f, 0.12f, 0.34f), new Vector3(0.14f, 0.36f, 0.05f), _scarf, new Vector3(0f, 0f, -8f));

        _armL = Part("Brazo_L", new Vector3(-0.32f, 0.12f, 0f));
        _armR = Part("Brazo_R", new Vector3(0.32f, 0.12f, 0f));
        foreach (Transform arm in new[] { _armL, _armR })
        {
            float side = arm == _armR ? 1f : -1f;
            arm.localRotation = Quaternion.Euler(0f, 0f, side * -55f);
            Cube(arm, "Rama", new Vector3(0f, 0.32f, 0f), new Vector3(0.07f, 0.64f, 0.07f), _wood);
            Cube(arm, "Ramita", new Vector3(side * 0.06f, 0.5f, 0f), new Vector3(0.05f, 0.22f, 0.05f), _wood, new Vector3(0f, 0f, side * -35f));
            Cube(arm, "Ramita", new Vector3(-side * 0.05f, 0.62f, 0f), new Vector3(0.04f, 0.16f, 0.04f), _wood, new Vector3(0f, 0f, side * 30f));
        }

        // Cabeza: cara de carbón con sonrisa, nariz de carámbano y gorro de lana con pompón de nieve.
        _head = Part("Cabeza", new Vector3(0f, 1.66f, 0f));
        Cube(_head, "Bloque", Vector3.zero, new Vector3(0.52f, 0.5f, 0.5f), _snow, new Vector3(0f, 3f, 0f));
        Cube(_head, "Ojo", new Vector3(-0.12f, 0.08f, 0.255f), new Vector3(0.08f, 0.1f, 0.03f), _coal);
        Cube(_head, "Ojo", new Vector3(0.12f, 0.08f, 0.255f), new Vector3(0.08f, 0.1f, 0.03f), _coal);
        for (int i = 0; i < 5; i++)
        {
            float x = (i - 2) * 0.065f;
            float y = -0.12f + Mathf.Abs(i - 2) * 0.025f;
            Cube(_head, "Sonrisa", new Vector3(x, y, 0.255f), new Vector3(0.045f, 0.045f, 0.03f), _coal);
        }
        var nose = new GameObject("Nariz");
        nose.transform.SetParent(_head, false);
        nose.transform.localPosition = new Vector3(0f, -0.01f, 0.36f);
        nose.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        nose.transform.localScale = new Vector3(0.07f, 0.24f, 0.07f);
        nose.AddComponent<MeshFilter>().sharedMesh = MenuRuins.Octahedron();
        nose.AddComponent<MeshRenderer>().sharedMaterial = _ice;
        Cube(_head, "Gorro", new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.16f, 0.48f), _wool);
        Cube(_head, "Gorro_Borde", new Vector3(0f, 0.23f, 0f), new Vector3(0.56f, 0.07f, 0.54f), _wool);
        Cube(_head, "Pompon", new Vector3(0.04f, 0.45f, 0f), new Vector3(0.14f, 0.14f, 0.14f), _snow, new Vector3(0f, 30f, 12f));
    }

    private IEnumerator Rise()
    {
        GroundImpact.Spawn(transform.position, 1.6f, 1.2f);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
            _model.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, k), Mathf.Lerp(0.05f, 1f, k), Mathf.Lerp(0.6f, 1f, k));
            yield return null;
        }
        _model.localScale = Vector3.one;
    }

    // ---------- Comportamiento ----------

    void Update()
    {
        if (!Alive) return;
        if (Time.time - _bornAt > Lifetime) { Melt(); return; }
        if (_owner == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) _owner = p.transform;
        }
        AnimateIdle();
        if (_agent == null || !_agent.isOnNavMesh) return;
        _agent.speed = 4.2f * Health.SpeedMultiplier;
        _agent.isStopped = Health.IsFrozen;

        if (_target == null || _target.IsDead || (_target.transform.position - transform.position).sqrMagnitude > 16f * 16f) _target = FindTarget(14f);
        if (_target != null)
        {
            Vector3 to = _target.transform.position - transform.position;
            to.y = 0f;
            float d = to.magnitude;
            if (d > 11f) Move(_target.transform.position);
            else
            {
                _agent.ResetPath();
                if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 400f * Time.deltaTime);
                if (Time.time >= _readyAt && Vector3.Angle(transform.forward, to) < 25f) StartCoroutine(Throw(_target));
            }
            return;
        }
        if (_owner == null) return;
        Vector3 back = _owner.position - _owner.forward * 2.2f + _owner.right * 1.6f * _followSide;
        if ((transform.position - _owner.position).sqrMagnitude > 3.8f * 3.8f && Time.time >= _repathAt) Move(back);
        if ((transform.position - _owner.position).sqrMagnitude > 40f * 40f && NavMesh.SamplePosition(back, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            _agent.Warp(hit.position);
    }

    private void Move(Vector3 p)
    {
        _repathAt = Time.time + 0.4f;
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) _agent.SetDestination(hit.position);
    }

    private Damageable FindTarget(float radius)
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, radius, _overlap, ~0, QueryTriggerInteraction.Ignore);
        Damageable best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            Damageable d = _overlap[i].GetComponentInParent<Damageable>();
            if (d == null || d.IsDead || d.friendly || d.GetComponent<EnemyBrain>() == null) continue;
            float dist = (d.transform.position - transform.position).sqrMagnitude;
            if (dist < bestD) { bestD = dist; best = d; }
        }
        return best;
    }

    private IEnumerator Throw(Damageable target)
    {
        _readyAt = Time.time + 1.15f;
        _throwT = 0f;
        yield return new WaitForSeconds(0.22f);
        if (!Alive || target == null || target.IsDead) yield break;
        Vector3 origin = _armR.position + _armR.up * 0.6f;
        Vector3 aim = target.PopupPoint - Vector3.up * 0.7f;
        Projectile ball = Projectile.Launch(null, origin, aim - origin, 17f, 16f, gameObject, (hit, point) =>
        {
            if (hit == null || hit.IsDead || hit.friendly) return;
            hit.TakeHit(new DamageInfo { amount = _damage * Random.Range(0.9f, 1.1f), type = DamageType.Frost, source = gameObject, direction = hit.transform.position - transform.position });
            hit.ApplySlow(0.3f, 1.5f);
            GroundImpact.Spawn(new Vector3(point.x, hit.transform.position.y, point.z), 0.6f, 0.5f);
        });
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Bola_de_Nieve";
        Destroy(sphere.GetComponent<Collider>());
        sphere.transform.SetParent(ball.transform, false);
        sphere.transform.localScale = Vector3.one * 0.24f;
        sphere.GetComponent<Renderer>().sharedMaterial = _snow;
    }

    // Balanceo al andar, respiración y lanzamiento con el brazo de rama.
    private void AnimateIdle()
    {
        float speed = _agent != null ? _agent.velocity.magnitude : 0f;
        float t = Time.time * 7f;
        float walk = Mathf.Clamp01(speed / 3f);
        _sway.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t) * 6f * walk);
        _head.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.7f) * 3f, Mathf.Sin(Time.time * 0.9f) * 8f, 0f);
        _armL.localRotation = Quaternion.Euler(Mathf.Sin(t) * 25f * walk, 0f, 55f);
        if (_throwT < 1f)
        {
            _throwT += Time.deltaTime / 0.45f;
            float k = _throwT < 0.45f ? -Mathf.Sin(_throwT / 0.45f * Mathf.PI * 0.5f) * 80f : Mathf.Lerp(-80f, 70f, Mathf.SmoothStep(0f, 1f, (_throwT - 0.45f) / 0.3f)) * (1f - Mathf.SmoothStep(0.8f, 1f, _throwT));
            _armR.localRotation = Quaternion.Euler(-k, 0f, -55f + Mathf.Abs(k) * 0.3f);
        }
        else _armR.localRotation = Quaternion.Euler(-Mathf.Sin(t) * 25f * walk, 0f, -55f);
    }

    // ---------- Muerte ----------

    // Se acaba el tiempo o se invoca uno nuevo: se derrite y se hunde.
    public void Melt()
    {
        if (_melting || Health == null || Health.IsDead) return;
        _melting = true;
        StartCoroutine(MeltRoutine());
    }

    private IEnumerator MeltRoutine()
    {
        if (_agent != null) _agent.enabled = false;
        Vector3 start = _model.localScale;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            float k = t / 1.2f;
            _model.localScale = new Vector3(start.x * (1f + k * 0.4f), start.y * (1f - k), start.z * (1f + k * 0.4f));
            yield return null;
        }
        Destroy(gameObject);
    }

    // Ragdoll: cada bloque se suelta con su Rigidbody, unido a su vecino con una articulación que cede con el golpe.
    private void OnDied()
    {
        StopAllCoroutines();
        if (_agent != null) _agent.enabled = false;
        foreach (Collider c in GetComponents<Collider>()) c.enabled = false;
        Vector3 dir = transform.forward * -1f;
        Rigidbody baseRb = Physical(_base, new Vector3(0.8f, 0.8f, 0.74f), 3f);
        Rigidbody bodyRb = Physical(_body, new Vector3(0.64f, 0.62f, 0.6f), 2f);
        Rigidbody headRb = Physical(_head, new Vector3(0.52f, 0.5f, 0.5f), 1.2f);
        Rigidbody armL = Physical(_armL, new Vector3(0.08f, 0.7f, 0.08f), 0.2f, new Vector3(0f, 0.32f, 0f));
        Rigidbody armR = Physical(_armR, new Vector3(0.08f, 0.7f, 0.08f), 0.2f, new Vector3(0f, 0.32f, 0f));
        Joint(bodyRb, baseRb, new Vector3(0f, -0.31f, 0f), 260f);
        Joint(headRb, bodyRb, new Vector3(0f, -0.25f, 0f), 180f);
        Joint(armL, bodyRb, Vector3.zero, 90f);
        Joint(armR, bodyRb, Vector3.zero, 90f);
        Vector3 push = (dir + Vector3.up * 0.4f).normalized;
        headRb.AddForce(push * 3.5f + Random.insideUnitSphere, ForceMode.Impulse);
        bodyRb.AddForce(push * 2.5f, ForceMode.Impulse);
        bodyRb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
        armL.AddForce(Random.insideUnitSphere * 0.6f + Vector3.up * 0.8f, ForceMode.Impulse);
        armR.AddForce(Random.insideUnitSphere * 0.6f + Vector3.up * 0.8f, ForceMode.Impulse);
        GroundImpact.Spawn(transform.position, 1.4f, 0.9f);
        StartCoroutine(Crumble(new[] { _base, _body, _head, _armL, _armR }));
    }

    private Rigidbody Physical(Transform part, Vector3 size, float mass, Vector3 center = default)
    {
        part.SetParent(null, true);
        var box = part.gameObject.AddComponent<BoxCollider>();
        box.size = size;
        box.center = center;
        var rb = part.gameObject.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.3f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        return rb;
    }

    private static void Joint(Rigidbody a, Rigidbody b, Vector3 anchor, float breakForce)
    {
        var j = a.gameObject.AddComponent<CharacterJoint>();
        j.connectedBody = b;
        j.anchor = anchor;
        j.breakForce = breakForce;
        j.enableProjection = true;
    }

    private IEnumerator Crumble(Transform[] parts)
    {
        yield return new WaitForSeconds(4.5f);
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            foreach (Transform p in parts)
                if (p != null) p.localScale = Vector3.Lerp(p.localScale, Vector3.zero, Time.deltaTime * 4f);
            yield return null;
        }
        foreach (Transform p in parts) if (p != null) Destroy(p.gameObject);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_base != null && _base.parent == null) Destroy(_base.gameObject);
    }
}
