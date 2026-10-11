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
    public bool Hostile { get; private set; }
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
        _csnow = null;
        _projCorrupt = null;
    }

    // Gólem vivo que un corrupto prefiere atacar: el más cercano dentro de su vista, si está más cerca que el héroe.
    public static SnowGolem PickFor(Vector3 enemy, Vector3 player, float sight, SnowGolem current)
    {
        SnowGolem best = null;
        float bestD = float.MaxValue;
        float toPlayer = (player - enemy).sqrMagnitude;
        foreach (SnowGolem g in All)
        {
            if (g == null || !g.Alive || g.Hostile) continue;
            float d = (g.transform.position - enemy).sqrMagnitude;
            if (d > sight * sight) continue;
            float bias = g == current ? 0.6f : 1f;
            if (d * bias < bestD) { bestD = d * bias; best = g; }
        }
        if (best == null) return null;
        return bestD <= toPlayer * (best == current ? 1.4f : 0.85f) ? best : null;
    }

    // hostile = gólem corrupto (lo invocan Rocco Vendaval y los chamanes): ataca al héroe y a sus gólems.
    public static SnowGolem Summon(GameObject owner, Vector3 position, float health, float damage, bool hostile = false, int maxAlive = MaxAlive)
    {
        int alive = 0;
        SnowGolem oldest = null;
        foreach (SnowGolem g in All)
        {
            if (g == null || !g.Alive || g.Hostile != hostile) continue;
            if (hostile && (g._owner == null || owner == null || g._owner != owner.transform)) continue;
            alive++;
            if (oldest == null || g._bornAt < oldest._bornAt) oldest = g;
        }
        if (alive >= maxAlive && oldest != null) oldest.Melt();

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas)) position = hit.position;
        // Se monta desactivado: así la vida (Damageable) arranca ya con el modelo como parte visual.
        var go = new GameObject("Golem_de_Nieve");
        go.SetActive(false);
        go.transform.position = position;
        go.transform.rotation = owner != null ? Quaternion.Euler(0f, owner.transform.eulerAngles.y, 0f) : Quaternion.identity;
        SnowGolem golem = go.AddComponent<SnowGolem>();
        golem.Hostile = hostile;
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
        col.radius = 0.42f * Size;
        col.height = 1.9f * Size;
        col.center = new Vector3(0f, 0.95f * Size, 0f);

        _agent = gameObject.AddComponent<NavMeshAgent>();
        _agent.radius = 0.4f * Size;
        _agent.height = 1.9f * Size;
        _agent.speed = 4.2f;
        _agent.acceleration = 14f;
        _agent.angularSpeed = 540f;
        _agent.stoppingDistance = 0.6f;

        Health = gameObject.AddComponent<Damageable>();
        Health.displayName = "Gólem de nieve";
        Health.maxHealth = health;
        Health.popupHeight = 2.1f * Size;
        Health.visual = _model;
        Health.friendly = !Hostile;
        if (Hostile) { Health.displayName = "Gólem corrupto"; Corrupt(); }
        Health.Died += OnDied;
    }

    // ---------- Modelo ----------

    private static void EnsureMaterials()
    {
        if (_snow != null) return;
        _snow = Mat("Nieve compacta", SnowTexture(), Color.white, 0.15f);
        _snow.SetTextureScale("_BaseMap", new Vector2(4f, 2f));
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

    private Transform Shape(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 size, Material mat, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    private Transform Ball(Transform parent, string name, Vector3 pos, float diameter, Material mat) =>
        Shape(PrimitiveType.Sphere, parent, name, pos, Vector3.one * diameter, mat);

    private Transform Part(string name, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(_sway, false);
        t.localPosition = pos;
        return t;
    }

    // Tamaño del gólem respecto al modelo: un poco más bajo que el pingüino (unos 0,9 m).
    public const float Size = 0.48f;

    // Diámetros de las tres bolas de nieve (base, cuerpo, cabeza).
    private const float BaseSize = 0.86f, BodySize = 0.66f, HeadSize = 0.5f;

    private void BuildModel()
    {
        EnsureMaterials();
        _model = new GameObject("Modelo").transform;
        _model.SetParent(transform, false);
        _sway = new GameObject("Balanceo").transform;
        _sway.SetParent(_model, false);

        // Base: la bola más grande, un poco aplastada contra el suelo, con dos botones de hielo.
        _base = Part("Base", new Vector3(0f, 0.4f, 0f));
        Shape(PrimitiveType.Sphere, _base, "Bola", Vector3.zero, new Vector3(BaseSize, BaseSize * 0.92f, BaseSize), _snow);
        Ball(_base, "Boton", new Vector3(0f, 0.14f, BaseSize * 0.47f), 0.09f, _ice);
        Ball(_base, "Boton", new Vector3(0f, -0.06f, BaseSize * 0.49f), 0.09f, _ice);

        // Cuerpo: bufanda en el cuello y los brazos de rama levantados.
        _body = Part("Cuerpo", new Vector3(0f, 1.05f, 0f));
        Ball(_body, "Bola", Vector3.zero, BodySize, _snow);
        Ball(_body, "Boton", new Vector3(0f, 0.1f, BodySize * 0.48f), 0.075f, _ice);
        Ball(_body, "Boton", new Vector3(0f, -0.08f, BodySize * 0.49f), 0.075f, _ice);
        Shape(PrimitiveType.Cylinder, _body, "Bufanda", new Vector3(0f, 0.27f, 0f), new Vector3(0.5f, 0.05f, 0.5f), _scarf);
        Shape(PrimitiveType.Cube, _body, "Bufanda_Cola", new Vector3(0.12f, 0.12f, 0.27f), new Vector3(0.12f, 0.3f, 0.04f), _scarf, new Vector3(-18f, 0f, -10f));

        // Los brazos salen de la bola del medio, a los lados y un poco por encima de su ecuador.
        _armL = Part("Brazo_L", Vector3.zero);
        _armR = Part("Brazo_R", Vector3.zero);
        _armL.SetParent(_body, false);
        _armR.SetParent(_body, false);
        _armL.localPosition = new Vector3(-BodySize * 0.44f, 0.07f, 0f);
        _armR.localPosition = new Vector3(BodySize * 0.44f, 0.07f, 0f);
        foreach (Transform arm in new[] { _armL, _armR })
        {
            float side = arm == _armR ? 1f : -1f;
            arm.localRotation = Quaternion.Euler(0f, 0f, side * -ArmLift);
            Shape(PrimitiveType.Cylinder, arm, "Rama", new Vector3(0f, 0.36f, 0f), new Vector3(0.07f, 0.36f, 0.07f), _wood);
            Shape(PrimitiveType.Cylinder, arm, "Ramita", new Vector3(side * 0.07f, 0.6f, 0f), new Vector3(0.045f, 0.12f, 0.045f), _wood, new Vector3(0f, 0f, side * -38f));
            Shape(PrimitiveType.Cylinder, arm, "Ramita", new Vector3(-side * 0.05f, 0.7f, 0f), new Vector3(0.04f, 0.09f, 0.04f), _wood, new Vector3(0f, 0f, side * 32f));
        }

        // Cabeza: ojos y sonrisa de carbón, nariz de carámbano y gorro de lana con pompón.
        _head = Part("Cabeza", new Vector3(0f, 1.58f, 0f));
        Ball(_head, "Bola", Vector3.zero, HeadSize, _snow);
        float r = HeadSize * 0.5f;
        foreach (float x in new[] { -0.09f, 0.09f })
            Ball(_head, "Ojo", new Vector3(x, 0.06f, Mathf.Sqrt(r * r - x * x - 0.0036f) - 0.005f), 0.075f, _coal);
        for (int i = 0; i < 5; i++)
        {
            float x = (i - 2) * 0.05f;
            float y = -0.09f + Mathf.Abs(i - 2) * 0.022f;
            Ball(_head, "Sonrisa", new Vector3(x, y, Mathf.Sqrt(Mathf.Max(0f, r * r - x * x - y * y)) - 0.004f), 0.04f, _coal);
        }
        var nose = new GameObject("Nariz");
        nose.transform.SetParent(_head, false);
        nose.transform.localPosition = new Vector3(0f, -0.01f, r + 0.08f);
        nose.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        nose.transform.localScale = new Vector3(0.07f, 0.22f, 0.07f);
        nose.AddComponent<MeshFilter>().sharedMesh = MenuRuins.Octahedron();
        nose.AddComponent<MeshRenderer>().sharedMaterial = _ice;
        Shape(PrimitiveType.Sphere, _head, "Gorro", new Vector3(0f, 0.13f, -0.01f), new Vector3(0.48f, 0.34f, 0.48f), _wool);
        Shape(PrimitiveType.Cylinder, _head, "Gorro_Borde", new Vector3(0f, 0.12f, -0.01f), new Vector3(0.5f, 0.035f, 0.5f), _wool);
        Ball(_head, "Pompon", new Vector3(0.03f, 0.32f, -0.02f), 0.13f, _snow);
    }

    private const float ArmLift = 32f;

    private IEnumerator Rise()
    {
        GroundImpact.Spawn(transform.position, 1.6f, 1.2f);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
            _model.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, k), Mathf.Lerp(0.05f, 1f, k), Mathf.Lerp(0.6f, 1f, k)) * Size;
            yield return null;
        }
        _model.localScale = Vector3.one * Size;
    }

    // ---------- Comportamiento ----------

    void Update()
    {
        if (!Alive) return;
        if (Time.time - _bornAt > Lifetime) { Melt(); return; }
        if (_owner == null && !Hostile)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) _owner = p.transform;
        }
        AnimateIdle();
        if (_agent == null || !_agent.isOnNavMesh) return;
        _agent.speed = 4.2f * Health.SpeedMultiplier;
        _agent.isStopped = Health.IsFrozen;
        if (Hostile) { HostileUpdate(); return; }

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
            if (d == null || d.IsDead || d.friendly) continue;
            SnowGolem g = d.GetComponent<SnowGolem>();
            if (d.GetComponent<EnemyBrain>() == null && (g == null || !g.Hostile)) continue;
            float dist = (d.transform.position - transform.position).sqrMagnitude;
            if (dist < bestD) { bestD = dist; best = d; }
        }
        return best;
    }

    // ---------- Gólem corrupto ----------

    private Transform _foe;
    private static Material _projCorrupt;

    private void HostileUpdate()
    {
        Transform player = EnemyBrain.Player;
        CharacterStats ps = player != null ? player.GetComponent<CharacterStats>() : null;
        // Objetivo: el gólem aliado más cercano si está más cerca que el héroe; si no, el héroe.
        _foe = null;
        float best = float.MaxValue;
        if (player != null && ps != null && !ps.IsDead && !SafeZone.Contains(player.position, 1.5f))
        {
            best = (player.position - transform.position).sqrMagnitude;
            _foe = player;
        }
        foreach (SnowGolem g in All)
        {
            if (g == null || g == this || !g.Alive || g.Hostile) continue;
            float d = (g.transform.position - transform.position).sqrMagnitude;
            if (d < best * 0.8f) { best = d; _foe = g.transform; }
        }
        if (_foe == null || best > 22f * 22f)
        {
            if (_owner != null && (transform.position - _owner.position).sqrMagnitude > 4f * 4f && Time.time >= _repathAt) Move(_owner.position + _owner.right * 1.8f * _followSide);
            return;
        }
        Vector3 to = _foe.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist > 10f) { if (Time.time >= _repathAt) Move(_foe.position); return; }
        _agent.ResetPath();
        if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 400f * Time.deltaTime);
        if (Time.time >= _readyAt && Vector3.Angle(transform.forward, to) < 25f) StartCoroutine(ThrowCorrupt(_foe));
    }

    private IEnumerator ThrowCorrupt(Transform target)
    {
        _readyAt = Time.time + 1.4f;
        _throwT = 0f;
        yield return new WaitForSeconds(0.22f);
        if (!Alive || target == null) yield break;
        if (_projCorrupt == null) _projCorrupt = MenuRuins.Lit("Nieve corrupta", new Color(0.16f, 0.2f, 0.32f), 0.4f, 0f, new Color(0.15f, 0.5f, 1.2f));
        Vector3 origin = _armR.position + _armR.up * 0.6f * Size;
        Vector3 aim = target.position + Vector3.up * 0.6f;
        EnemyProjectile.Launch(origin, aim - origin, 15f, 16f, _damage * Random.Range(0.9f, 1.1f), _projCorrupt, gameObject);
    }

    // Nieve sucia y azulada, cristales del Frost por botones y ojos que brillan.
    private void Corrupt()
    {
        EnsureMaterials();
        if (_csnow == null)
        {
            _csnow = Mat("Nieve corrupta gólem", SnowTexture(), new Color(0.42f, 0.48f, 0.62f), 0.2f);
            _csnow.SetTextureScale("_BaseMap", new Vector2(4f, 2f));
            _cice = MenuRuins.Lit("Cristal corrupto gólem", new Color(0.3f, 0.5f, 0.95f), 0.9f, 0f, new Color(0.3f, 0.9f, 2.4f));
            _ceye = MenuRuins.Lit("Ojos corruptos", new Color(0.4f, 0.9f, 1f), 0.5f, 0f, new Color(0.6f, 2.2f, 3f));
            _cwool = Mat("Lana corrupta", WoolTexture(new Color(0.12f, 0.12f, 0.18f), new Color(0.25f, 0.3f, 0.45f)), Color.white, 0.05f);
        }
        foreach (Renderer r in _model.GetComponentsInChildren<Renderer>(true))
        {
            Material m = r.sharedMaterial;
            if (m == _snow) r.sharedMaterial = _csnow;
            else if (m == _ice) r.sharedMaterial = _cice;
            else if (m == _coal) r.sharedMaterial = r.name == "Ojo" ? _ceye : _coal;
            else if (m == _wool || m == _scarf) r.sharedMaterial = _cwool;
        }
    }

    private static Material _csnow, _cice, _ceye, _cwool;

    private IEnumerator Throw(Damageable target)
    {
        _readyAt = Time.time + 1.15f;
        _throwT = 0f;
        yield return new WaitForSeconds(0.22f);
        if (!Alive || target == null || target.IsDead) yield break;
        Vector3 origin = _armR.position + _armR.up * 0.6f * Size;
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
        _armL.localRotation = Quaternion.Euler(Mathf.Sin(t) * 25f * walk, 0f, ArmLift + Mathf.Sin(Time.time * 2.3f) * 6f);
        if (_throwT < 1f)
        {
            _throwT += Time.deltaTime / 0.45f;
            float k = _throwT < 0.45f ? -Mathf.Sin(_throwT / 0.45f * Mathf.PI * 0.5f) * 80f : Mathf.Lerp(-80f, 70f, Mathf.SmoothStep(0f, 1f, (_throwT - 0.45f) / 0.3f)) * (1f - Mathf.SmoothStep(0.8f, 1f, _throwT));
            _armR.localRotation = Quaternion.Euler(-k, 0f, -ArmLift + Mathf.Abs(k) * 0.2f);
        }
        else _armR.localRotation = Quaternion.Euler(-Mathf.Sin(t) * 25f * walk, 0f, -ArmLift - Mathf.Sin(Time.time * 2.3f + 1f) * 6f);
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
        Rigidbody baseRb = Round(_base, BaseSize * 0.48f, 3f);
        Rigidbody bodyRb = Round(_body, BodySize * 0.5f, 2f);
        Rigidbody headRb = Round(_head, HeadSize * 0.5f, 1.2f);
        Rigidbody armL = Physical(_armL, new Vector3(0.08f, 0.78f, 0.08f), 0.2f, new Vector3(0f, 0.38f, 0f));
        Rigidbody armR = Physical(_armR, new Vector3(0.08f, 0.78f, 0.08f), 0.2f, new Vector3(0f, 0.38f, 0f));
        Joint(bodyRb, baseRb, new Vector3(0f, -BodySize * 0.45f, 0f), 260f);
        Joint(headRb, bodyRb, new Vector3(0f, -HeadSize * 0.45f, 0f), 180f);
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

    private Rigidbody Round(Transform part, float radius, float mass)
    {
        part.SetParent(null, true);
        var sphere = part.gameObject.AddComponent<SphereCollider>();
        sphere.radius = radius;
        var rb = part.gameObject.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.4f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        return rb;
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
