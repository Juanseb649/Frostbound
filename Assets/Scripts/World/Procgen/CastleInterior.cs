using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

// Interior del castillo. Se genera la primera vez que el héroe cruza la puerta (con la seed de la partida),
// lejos del exterior en la misma escena. Ambiente oscuro, salas temáticas con tropas y élites malditas
// y, al fondo, la sala del trono del ninja maldito con su escolta de élite.
public class CastleInterior : MonoBehaviour
{
    public static CastleInterior Instance { get; private set; }
    public static readonly Vector3 Origin = new Vector3(1500f, 0f, 1500f);

    [Header("Materiales (los del castillo)")]
    public Material stone, trim, roof, glass, gold, ice, door, lampGlow;
    public Mesh cone, prism;

    public int Seed { get; set; }
    public bool Built { get; private set; }
    public bool PlayerInside { get; private set; }
    public CastleInteriorLayout.Result Layout { get; private set; }
    public Vector3 ExitPoint { get; set; }
    public Quaternion ExitRotation { get; set; }
    public static float Extent => CastleInteriorLayout.Size * CastleInteriorLayout.Cell;

    private Transform _root, _enemies;
    private MeshKit _kit;
    private DeterministicRng _rng;
    private Material _carpet, _wood, _flame, _void, _bone;
    private readonly List<RoomState> _rooms = new List<RoomState>();
    private Vector3 _portal;
    private float _enteredAt;
    private bool _transition;
    private Transform _player;
    private Ambience _outside;
    private bool _ambienceInside;

    private class RoomState
    {
        public CastleInteriorLayout.Room room;
        public Vector3 center;
        public float radius;
        public bool spawned;
        public readonly List<EnemyBrain> enemies = new List<EnemyBrain>();
    }

    private struct Ambience
    {
        public Color ambient;
        public UnityEngine.Rendering.AmbientMode mode;
        public bool fog;
        public Color fogColor;
        public float fogDensity;
        public FogMode fogMode;
        public float fogStart, fogEnd;
        public float sunIntensity;
        public Color sunColor;
        public Light sun;
    }

    void Awake() => Instance = this;
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_ambienceInside) ApplyOutside();
    }

    public static Vector3 CellToWorld(float x, float z) =>
        Origin + new Vector3((x + 0.5f) * CastleInteriorLayout.Cell, 0f, (z + 0.5f) * CastleInteriorLayout.Cell);

    public static bool Contains(Vector3 p) =>
        p.x > Origin.x - 6f && p.z > Origin.z - 6f && p.x < Origin.x + Extent + 6f && p.z < Origin.z + Extent + 6f;

    // ---------- Entrar y salir ----------

    public void Enter()
    {
        if (_transition) return;
        StartCoroutine(Transition(true));
    }

    public void Exit()
    {
        if (_transition) return;
        StartCoroutine(Transition(false));
    }

    private IEnumerator Transition(bool enter)
    {
        _transition = true;
        GameplayInput.Block();
        yield return ScreenFade.Instance.Fade(1f, 0.55f);
        if (enter && !Built) Build();
        PlayerPersistence mover = FindAnyObjectByType<PlayerPersistence>();
        Vector3 to = enter ? _portal + Vector3.forward * 3.5f : ExitPoint;
        Quaternion rot = enter ? Quaternion.identity : ExitRotation;
        if (enter)
        {
            Vector3 inward = (CellToWorld(Layout.Entry.Center.x, Layout.Entry.Center.y) - _portal);
            inward.y = 0f;
            if (inward.sqrMagnitude > 0.01f)
            {
                to = _portal + inward.normalized * 3.5f;
                rot = Quaternion.LookRotation(inward.normalized);
            }
        }
        if (mover != null) mover.MoveTo(to, rot);
        _enteredAt = Time.time;
        UpdateAmbience(enter);
        yield return null;
        yield return ScreenFade.Instance.Fade(0f, 0.7f);
        GameplayInput.Unblock();
        _transition = false;
        if (enter)
        {
            BonfireBanner.Show("LA CATEDRAL CORRUPTA", new Color(0.62f, 0.82f, 1f));
            if (QuestLog.Stage(QuestLog.Citadel) == QuestLog.CitadelBarbarian)
                Notifications.Show("El ninja maldito aguarda en la sala del trono.", FrostboundUI.Muted);
        }
    }

    void Update()
    {
        if (_player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p == null) return;
            _player = p.transform;
        }
        PlayerInside = Built && Contains(_player.position);
        if (!_transition && PlayerInside != _ambienceInside) UpdateAmbience(PlayerInside);
        if (!PlayerInside) return;

        foreach (RoomState rs in _rooms)
        {
            if (rs.spawned) continue;
            if ((_player.position - rs.center).magnitude < rs.radius + 14f) SpawnRoom(rs);
        }
        if (!_transition && Time.time - _enteredAt > 2.5f && (_player.position - _portal).magnitude < 1.8f) Exit();
    }

    // ---------- Ambiente ----------

    private void UpdateAmbience(bool inside)
    {
        if (inside == _ambienceInside) return;
        if (inside)
        {
            _outside = Capture();
            _ambienceInside = true;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.19f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.06f);
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 75f;
            if (_outside.sun != null)
            {
                _outside.sun.intensity = _outside.sunIntensity * 0.3f;
                _outside.sun.color = new Color(0.55f, 0.65f, 0.95f);
            }
        }
        else ApplyOutside();
    }

    private Ambience Capture()
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
            foreach (Light l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional) { sun = l; break; }
        return new Ambience
        {
            ambient = RenderSettings.ambientLight, mode = RenderSettings.ambientMode, fog = RenderSettings.fog,
            fogColor = RenderSettings.fogColor, fogDensity = RenderSettings.fogDensity, fogMode = RenderSettings.fogMode,
            fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance,
            sun = sun, sunIntensity = sun != null ? sun.intensity : 1f, sunColor = sun != null ? sun.color : Color.white
        };
    }

    private void ApplyOutside()
    {
        _ambienceInside = false;
        RenderSettings.ambientMode = _outside.mode;
        RenderSettings.ambientLight = _outside.ambient;
        RenderSettings.fog = _outside.fog;
        RenderSettings.fogColor = _outside.fogColor;
        RenderSettings.fogDensity = _outside.fogDensity;
        RenderSettings.fogMode = _outside.fogMode;
        RenderSettings.fogStartDistance = _outside.fogStart;
        RenderSettings.fogEndDistance = _outside.fogEnd;
        if (_outside.sun != null)
        {
            _outside.sun.intensity = _outside.sunIntensity;
            _outside.sun.color = _outside.sunColor;
        }
    }

    // ---------- Construcción ----------

    public void Build()
    {
        if (Built) return;
        Built = true;
        Layout = CastleInteriorLayout.Generate(Seed);
        _rng = new DeterministicRng(SeedUtil.Combine(Seed, SeedUtil.FromString("castillo_interior_detalle")));
        _root = new GameObject("Castillo_Interior").transform;
        _root.SetParent(transform, false);
        _root.position = Origin;
        _enemies = new GameObject("Enemigos").transform;
        _enemies.SetParent(_root, false);
        _kit = new MeshKit(_root, cone, prism, stone);

        Material baseMat = stone;
        _carpet = MeshKit.Tint(baseMat, "Interior_Alfombra", new Color(0.28f, 0.05f, 0.08f));
        _wood = MeshKit.Tint(baseMat, "Interior_Madera", new Color(0.18f, 0.12f, 0.09f));
        _flame = MeshKit.Tint(baseMat, "Interior_Llama", new Color(1f, 0.7f, 0.35f), new Color(2.4f, 1.2f, 0.4f));
        _void = MeshKit.Tint(baseMat, "Interior_Vacio", new Color(0.01f, 0.012f, 0.02f), null, 0f);
        _bone = MeshKit.Tint(baseMat, "Interior_Hueso", new Color(0.62f, 0.6f, 0.55f));

        float ext = Extent;
        _kit.Box(new Vector3(ext * 0.5f, -0.8f, ext * 0.5f), Vector3.zero, new Vector3(ext + 160f, 1f, ext + 160f), _void);
        BuildFloors();
        BuildWalls();
        foreach (CastleInteriorLayout.Room room in Layout.rooms) BuildRoom(room);
        _kit.Combine("Interior");

        var surface = _root.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
    }

    private Vector3 Local(float cx, float cz) => new Vector3((cx + 0.5f) * CastleInteriorLayout.Cell, 0f, (cz + 0.5f) * CastleInteriorLayout.Cell);

    private void BuildFloors()
    {
        float c = CastleInteriorLayout.Cell;
        for (int z = 0; z < Layout.size; z++)
        {
            int x = 0;
            while (x < Layout.size)
            {
                if (!Layout.floor[x, z]) { x++; continue; }
                int start = x;
                while (x < Layout.size && Layout.floor[x, z]) x++;
                float len = (x - start) * c;
                Vector3 center = new Vector3(start * c + len * 0.5f, -0.2f, z * c + c * 0.5f);
                _kit.Box(center, Vector3.zero, new Vector3(len, 0.4f, c), (z % 2 == 0) ? trim : stone, true);
            }
        }
        // Juntas entre losas.
        for (int z = 0; z < Layout.size; z++)
            for (int x = 0; x < Layout.size; x++)
                if (Layout.floor[x, z] && (x + z) % 3 == 0)
                    _kit.Box(Local(x, z) + Vector3.up * 0.01f, Vector3.zero, new Vector3(c * 0.92f, 0.02f, c * 0.92f), roof);
    }

    private void BuildWalls()
    {
        float c = CastleInteriorLayout.Cell, h = CastleInteriorLayout.WallHeight, t = 0.8f;
        int n = Layout.size;
        // Muros horizontales (bordes norte/sur) en tramos seguidos, una sola colisión por tramo.
        for (int side = 0; side < 2; side++)
        {
            int dz = side == 0 ? 1 : -1;
            for (int z = 0; z < n; z++)
            {
                int x = 0;
                while (x < n)
                {
                    if (!(Layout.IsFloor(x, z) && !Layout.IsFloor(x, z + dz))) { x++; continue; }
                    int start = x;
                    while (x < n && Layout.IsFloor(x, z) && !Layout.IsFloor(x, z + dz)) x++;
                    float len = (x - start) * c;
                    float ez = z * c + (dz > 0 ? c : 0f);
                    Vector3 center = new Vector3(start * c + len * 0.5f, h * 0.5f, ez);
                    WallRun(center, new Vector3(len + t, h, t), Vector3.forward * -dz, start, x, true);
                }
            }
        }
        for (int side = 0; side < 2; side++)
        {
            int dx = side == 0 ? 1 : -1;
            for (int x = 0; x < n; x++)
            {
                int z = 0;
                while (z < n)
                {
                    if (!(Layout.IsFloor(x, z) && !Layout.IsFloor(x + dx, z))) { z++; continue; }
                    int start = z;
                    while (z < n && Layout.IsFloor(x, z) && !Layout.IsFloor(x + dx, z)) z++;
                    float len = (z - start) * c;
                    float ex = x * c + (dx > 0 ? c : 0f);
                    Vector3 center = new Vector3(ex, h * 0.5f, start * c + len * 0.5f);
                    WallRun(center, new Vector3(t, h, len + t), Vector3.right * -dx, start, z, false);
                }
            }
        }
    }

    // Tramo de muro: piedra oscura, remate, pilastras y alguna vidriera que brilla en cobalto.
    private void WallRun(Vector3 center, Vector3 size, Vector3 inward, int from, int to, bool alongX)
    {
        float c = CastleInteriorLayout.Cell, h = CastleInteriorLayout.WallHeight;
        _kit.Box(center, Vector3.zero, size, stone, true);
        _kit.Box(center + Vector3.up * (h * 0.5f + 0.15f), Vector3.zero, new Vector3(size.x + 0.2f, 0.3f, size.z + 0.2f), trim);
        _kit.Box(center + Vector3.down * (h * 0.5f - 0.2f) + inward * 0.1f, Vector3.zero, new Vector3(size.x + 0.05f, 0.4f, size.z + 0.05f), trim);
        Quaternion face = Quaternion.LookRotation(inward);
        for (int i = from; i <= to; i++)
        {
            float u = i * c;
            Vector3 p = alongX ? new Vector3(u, 0f, center.z) : new Vector3(center.x, 0f, u);
            p += inward * 0.5f;
            if ((i - from) % 2 == 0)
            {
                _kit.Box(p + Vector3.up * h * 0.5f, face.eulerAngles, new Vector3(0.7f, h, 0.4f), trim);
                _kit.Cone(p + Vector3.up * (h + 0.3f), 0.5f, 1.4f, roof);
                // Antorcha en la pilastra (una de cada tres), con su luz.
                if ((i - from) % 6 == 2) Sconce(p + inward * 0.35f + Vector3.up * 2.6f, face);
            }
            else if (i < to && _rng.Chance(0.35f))
            {
                Vector3 w = p + inward * -0.08f + Vector3.up * (h * 0.58f) + (alongX ? Vector3.right : Vector3.forward) * 0f;
                _kit.Box(w, face.eulerAngles, new Vector3(0.9f, 2.2f, 0.08f), glass);
                _kit.Box(w + Vector3.up * 1.1f, (face * Quaternion.Euler(0f, 0f, 45f)).eulerAngles, new Vector3(0.64f, 0.64f, 0.08f), glass);
            }
            else if (i < to && _rng.Chance(0.25f))
            {
                // Estandarte rasgado.
                _kit.Box(p + Vector3.up * (h * 0.62f) + inward * 0.05f, face.eulerAngles, new Vector3(1f, 2.4f, 0.05f), _carpet);
                _kit.Box(p + Vector3.up * (h * 0.62f + 1.25f) + inward * 0.1f, face.eulerAngles, new Vector3(1.3f, 0.12f, 0.12f), gold);
            }
        }
    }

    private void BuildRoom(CastleInteriorLayout.Room room)
    {
        RoomRect r = room.rect;
        float c = CastleInteriorLayout.Cell;
        Vector3 min = new Vector3(r.MinX * c, 0f, r.MinZ * c);
        Vector3 size = new Vector3(r.size.x * c, 0f, r.size.y * c);
        Vector3 center = min + size * 0.5f;
        var state = new RoomState { room = room, center = Origin + center, radius = Mathf.Max(size.x, size.z) * 0.5f };
        _rooms.Add(state);

        switch (room.theme)
        {
            case CastleInteriorLayout.Theme.Vestibule: Vestibule(center, size); break;
            case CastleInteriorLayout.Theme.Nave: Nave(center, size); break;
            case CastleInteriorLayout.Theme.Library: Library(min, size); break;
            case CastleInteriorLayout.Theme.Armory: Armory(min, size); break;
            case CastleInteriorLayout.Theme.Crypt: Crypt(center, size); break;
            case CastleInteriorLayout.Theme.Cloister: Cloister(center, size); break;
            case CastleInteriorLayout.Theme.Refectory: Refectory(center, size); break;
            case CastleInteriorLayout.Theme.Throne: Throne(center, size); break;
        }
    }

    private void Candelabra(Vector3 p, bool light = true)
    {
        _kit.Cylinder(p + Vector3.up * 0.05f, 0.8f, 0.1f, gold);
        _kit.Box(p + Vector3.up * 0.9f, Vector3.zero, new Vector3(0.12f, 1.8f, 0.12f), gold);
        _kit.Box(p + Vector3.up * 1.8f, Vector3.zero, new Vector3(0.9f, 0.08f, 0.08f), gold);
        foreach (float dx in new[] { -0.42f, 0f, 0.42f })
        {
            _kit.Box(p + new Vector3(dx, 2f, 0f), Vector3.zero, new Vector3(0.1f, 0.35f, 0.1f), _bone);
            _kit.Box(p + new Vector3(dx, 2.25f, 0f), new Vector3(0f, 45f, 0f), new Vector3(0.07f, 0.16f, 0.07f), _flame);
        }
        _kit.ColliderBox(p + Vector3.up * 0.9f, Vector3.zero, new Vector3(0.6f, 1.8f, 0.6f));
        if (light) PointLight(p + Vector3.up * 2.6f, new Color(1f, 0.62f, 0.32f), 8f, 1.6f);
    }

    private int _sconces;

    private void Sconce(Vector3 local, Quaternion face)
    {
        _kit.Box(local, face.eulerAngles, new Vector3(0.16f, 0.16f, 0.5f), gold);
        _kit.Box(local + face * new Vector3(0f, 0.25f, 0.25f), (face * Quaternion.Euler(25f, 0f, 0f)).eulerAngles, new Vector3(0.12f, 0.55f, 0.12f), _wood);
        _kit.Box(local + face * new Vector3(0f, 0.6f, 0.38f), (face * Quaternion.Euler(0f, 45f, 0f)).eulerAngles, new Vector3(0.2f, 0.32f, 0.2f), _flame);
        if (_sconces++ < 70) PointLight(local + face * new Vector3(0f, 0.7f, 0.6f), new Color(1f, 0.6f, 0.3f), 8f, 1.5f);
    }

    private void PointLight(Vector3 local, Color color, float range, float intensity)
    {
        var go = new GameObject("Luz");
        go.transform.SetParent(_root, false);
        go.transform.localPosition = local;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        if (color.r > color.b)
        {
            FlickerLight f = go.AddComponent<FlickerLight>();
            f.baseIntensity = intensity;
            f.intensityVariation = intensity * 0.25f;
        }
    }

    private void Shards(Vector3 p, int count, float minH, float maxH)
    {
        for (int k = 0; k < count; k++)
        {
            float hgt = _rng.Range(minH, maxH);
            Vector3 off = new Vector3(_rng.Range(-0.9f, 0.9f), hgt * 0.35f, _rng.Range(-0.9f, 0.9f));
            Vector3 e = new Vector3(_rng.Range(-30f, 30f), _rng.Range(0f, 90f), _rng.Range(-30f, 30f));
            float th = _rng.Range(0.3f, 0.6f);
            _kit.Box(p + off, e, new Vector3(th, hgt, th), ice);
        }
    }

    private void Pillar(Vector3 p, float h)
    {
        _kit.Box(p + Vector3.up * 0.25f, Vector3.zero, new Vector3(1.4f, 0.5f, 1.4f), trim);
        _kit.Box(p + Vector3.up * h * 0.5f, new Vector3(0f, 45f, 0f), new Vector3(0.9f, h, 0.9f), stone, true);
        _kit.Box(p + Vector3.up * h * 0.5f, Vector3.zero, new Vector3(0.7f, h, 0.7f), stone);
        _kit.Box(p + Vector3.up * (h - 0.2f), Vector3.zero, new Vector3(1.3f, 0.4f, 1.3f), trim);
    }

    private void Vestibule(Vector3 center, Vector3 size)
    {
        // Portal de salida en el muro más cercano al borde sur (por donde se entró).
        _portal = Origin + new Vector3(center.x, 0f, center.z - size.z * 0.5f + 1.2f);
        Vector3 lp = _portal - Origin;
        _kit.Box(lp + new Vector3(0f, 2.6f, -0.6f), Vector3.zero, new Vector3(3.2f, 5.2f, 0.3f), door);
        foreach (float s in new[] { -1f, 1f })
            _kit.Box(lp + new Vector3(s * 1.8f, 2.8f, -0.4f), Vector3.zero, new Vector3(0.5f, 5.6f, 0.6f), trim);
        _kit.Box(lp + new Vector3(0f, 5.6f, -0.4f), Vector3.zero, new Vector3(4.1f, 0.5f, 0.6f), trim);
        _kit.Box(lp + new Vector3(0f, 0.03f, 1f), Vector3.zero, new Vector3(2.2f, 0.04f, 1.6f), lampGlow);
        PointLight(lp + new Vector3(0f, 2.5f, 1.2f), new Color(1f, 0.8f, 0.55f), 7f, 1.8f);
        _kit.Box(center + Vector3.up * 0.02f, Vector3.zero, new Vector3(2.4f, 0.03f, size.z - 2f), _carpet);
        foreach (float s in new[] { -1f, 1f })
        {
            Pillar(center + new Vector3(s * (size.x * 0.5f - 2.2f), 0f, size.z * 0.15f), 4.6f);
            Candelabra(center + new Vector3(s * 2.4f, 0f, size.z * 0.3f), s > 0);
        }
        // Estatuas de guardianes decapitadas.
        foreach (float s in new[] { -1f, 1f })
        {
            Vector3 p = center + new Vector3(s * (size.x * 0.5f - 1.6f), 0f, -size.z * 0.25f);
            _kit.Box(p + Vector3.up * 0.5f, Vector3.zero, new Vector3(1.2f, 1f, 1.2f), trim, true);
            _kit.Box(p + Vector3.up * 1.7f, Vector3.zero, new Vector3(0.8f, 1.4f, 0.6f), stone);
        }
    }

    private void Nave(Vector3 center, Vector3 size)
    {
        _kit.Box(center + Vector3.up * 0.02f, Vector3.zero, new Vector3(2f, 0.03f, size.z - 1.5f), _carpet);
        int rows = Mathf.Max(2, Mathf.FloorToInt((size.z - 5f) / 1.8f));
        for (int i = 0; i < rows; i++)
        {
            float z = center.z - size.z * 0.5f + 1.8f + i * 1.8f;
            foreach (float s in new[] { -1f, 1f })
            {
                float w = size.x * 0.5f - 2.6f;
                if (w < 1f) continue;
                bool broken = _rng.Chance(0.25f);
                Vector3 p = new Vector3(center.x + s * (1.6f + w * 0.5f), 0f, z);
                _kit.Box(p + Vector3.up * 0.45f, broken ? new Vector3(0f, _rng.Range(-25f, 25f), 8f) : Vector3.zero, new Vector3(w, 0.12f, 0.6f), _wood, true);
                _kit.Box(p + new Vector3(0f, 0.8f, -0.3f), Vector3.zero, new Vector3(w, 0.7f, 0.1f), _wood);
            }
        }
        Vector3 altar = new Vector3(center.x, 0f, center.z + size.z * 0.5f - 1.8f);
        _kit.Box(altar + Vector3.up * 0.3f, Vector3.zero, new Vector3(4f, 0.6f, 2.4f), trim, true);
        _kit.Box(altar + Vector3.up * 1.1f, Vector3.zero, new Vector3(2.6f, 1f, 1.1f), stone, true);
        Shards(altar + Vector3.up * 1.6f, 5, 0.8f, 2.4f);
        PointLight(altar + Vector3.up * 3f, new Color(0.35f, 0.6f, 1f), 9f, 2f);
        Candelabra(altar + new Vector3(-2.6f, 0f, -0.8f), false);
        Candelabra(altar + new Vector3(2.6f, 0f, -0.8f), true);
    }

    private void Library(Vector3 min, Vector3 size)
    {
        float c = 0f;
        for (float x = min.x + 1.5f; x < min.x + size.x - 1.5f; x += 3.2f)
        {
            for (int row = 0; row < 2; row++)
            {
                float z = row == 0 ? min.z + size.z * 0.33f : min.z + size.z * 0.66f;
                Vector3 p = new Vector3(x, 0f, z);
                _kit.Box(p + Vector3.up * 1.4f, Vector3.zero, new Vector3(2.4f, 2.8f, 0.7f), _wood, true);
                for (int shelf = 0; shelf < 4; shelf++)
                    _kit.Box(p + new Vector3(0f, 0.4f + shelf * 0.65f, 0f), Vector3.zero, new Vector3(2.2f, 0.45f, 0.75f), shelf % 2 == 0 ? _carpet : trim);
                if (_rng.Chance(0.4f)) Shards(p + Vector3.up * 2.8f, 3, 0.5f, 1.4f);
                c++;
            }
        }
        Vector3 desk = min + size * 0.5f + new Vector3(0f, 0f, 0f);
        _kit.Box(desk + Vector3.up * 0.5f, Vector3.zero, new Vector3(1.8f, 1f, 1.2f), _wood, true);
        PointLight(desk + Vector3.up * 2.4f, new Color(1f, 0.65f, 0.35f), 9f, 1.4f);
        _kit.Box(desk + new Vector3(0.3f, 1.1f, 0f), Vector3.zero, new Vector3(0.1f, 0.25f, 0.1f), _flame);
    }

    private void Armory(Vector3 min, Vector3 size)
    {
        for (float x = min.x + 2f; x < min.x + size.x - 1.5f; x += 2.6f)
        {
            Vector3 p = new Vector3(x, 0f, min.z + 1.2f);
            _kit.Box(p + Vector3.up * 1.1f, Vector3.zero, new Vector3(2f, 0.15f, 0.4f), _wood);
            for (int k = -1; k <= 1; k++)
                _kit.Box(p + new Vector3(k * 0.55f, 1.2f, 0.1f), new Vector3(0f, 0f, k * 8f), new Vector3(0.08f, 2.2f, 0.08f), trim);
            _kit.ColliderBox(p + Vector3.up * 1f, Vector3.zero, new Vector3(2f, 2f, 0.6f));
        }
        int stands = Mathf.Max(2, Mathf.FloorToInt(size.x / 4f));
        for (int i = 0; i < stands; i++)
        {
            Vector3 p = new Vector3(min.x + (i + 0.5f) * size.x / stands, 0f, min.z + size.z * 0.6f);
            _kit.Box(p + Vector3.up * 0.9f, Vector3.zero, new Vector3(0.15f, 1.8f, 0.15f), _wood);
            _kit.Box(p + Vector3.up * 1.4f, Vector3.zero, new Vector3(0.9f, 0.9f, 0.5f), trim, true);
            _kit.Cylinder(p + Vector3.up * 2.05f, 0.55f, 0.5f, trim);
        }
        Candelabra(min + new Vector3(size.x * 0.5f, 0f, size.z - 1.6f));
    }

    private void Crypt(Vector3 center, Vector3 size)
    {
        int cols = Mathf.Max(1, Mathf.FloorToInt((size.x - 2f) / 3.4f)), rows = Mathf.Max(1, Mathf.FloorToInt((size.z - 2f) / 4f));
        for (int i = 0; i < cols; i++)
            for (int j = 0; j < rows; j++)
            {
                Vector3 p = center + new Vector3((i - (cols - 1) * 0.5f) * 3.4f, 0f, (j - (rows - 1) * 0.5f) * 4f);
                _kit.Box(p + Vector3.up * 0.45f, Vector3.zero, new Vector3(1.3f, 0.9f, 2.4f), stone, true);
                bool open = _rng.Chance(0.35f);
                _kit.Box(p + (open ? new Vector3(0.7f, 0.5f, 0.3f) : Vector3.up * 0.95f), open ? new Vector3(0f, 25f, 70f) : Vector3.zero, new Vector3(1.45f, 0.18f, 2.55f), trim);
                if (open) _kit.Box(p + Vector3.up * 0.92f, Vector3.zero, new Vector3(0.5f, 0.1f, 1.2f), _bone);
            }
        Shards(center + new Vector3(size.x * 0.3f, 0f, size.z * 0.3f), 5, 1f, 3f);
        PointLight(center + Vector3.up * 3f, new Color(0.3f, 0.55f, 1f), 10f, 1.5f);
    }

    private void Cloister(Vector3 center, Vector3 size)
    {
        float hx = size.x * 0.5f - 2f, hz = size.z * 0.5f - 2f;
        for (float x = -hx; x <= hx + 0.01f; x += hx)
            for (float z = -hz; z <= hz + 0.01f; z += hz)
                if (!(Mathf.Abs(x) < 0.1f && Mathf.Abs(z) < 0.1f)) Pillar(center + new Vector3(x, 0f, z), 4.2f);
        // Jardín helado en el centro.
        _kit.Box(center + Vector3.up * 0.15f, Vector3.zero, new Vector3(hx * 1.1f, 0.3f, hz * 1.1f), trim);
        _kit.Box(center + Vector3.up * 0.32f, Vector3.zero, new Vector3(hx * 1.0f, 0.06f, hz * 1.0f), roof);
        Shards(center, 7, 1.2f, 3.6f);
        _kit.ColliderBox(center + Vector3.up * 0.8f, Vector3.zero, new Vector3(1.8f, 1.6f, 1.8f));
        PointLight(center + Vector3.up * 3.2f, new Color(0.35f, 0.7f, 1f), 11f, 2.2f);
    }

    private void Refectory(Vector3 center, Vector3 size)
    {
        float len = size.z - 4f;
        foreach (float s in new[] { -1f, 1f })
        {
            Vector3 p = center + new Vector3(s * Mathf.Min(2.6f, size.x * 0.25f), 0f, 0f);
            _kit.Box(p + Vector3.up * 0.75f, Vector3.zero, new Vector3(1.4f, 0.12f, len), _wood, true);
            for (float z = -len * 0.5f + 0.6f; z < len * 0.5f; z += 2.2f)
            {
                _kit.Box(p + new Vector3(0f, 0.35f, z), Vector3.zero, new Vector3(1.2f, 0.7f, 0.15f), _wood);
                if (_rng.Chance(0.4f)) _kit.Cylinder(p + new Vector3(_rng.Range(-0.4f, 0.4f), 0.9f, z), 0.3f, 0.18f, gold);
            }
        }
        Candelabra(center + new Vector3(0f, 0f, len * 0.3f));
        Candelabra(center + new Vector3(0f, 0f, -len * 0.3f), false);
    }

    private void Throne(Vector3 center, Vector3 size)
    {
        _kit.Box(center + Vector3.up * 0.02f, Vector3.zero, new Vector3(3f, 0.03f, size.z - 2f), _carpet);
        for (int i = 0; i < 4; i++)
            foreach (float s in new[] { -1f, 1f })
                Pillar(center + new Vector3(s * (size.x * 0.5f - 3f), 0f, -size.z * 0.35f + i * size.z * 0.22f), 5f);
        Vector3 dais = new Vector3(center.x, 0f, center.z + size.z * 0.5f - 3.2f);
        _kit.Box(dais + Vector3.up * 0.3f, Vector3.zero, new Vector3(8f, 0.6f, 4.4f), trim, true);
        _kit.Box(dais + Vector3.up * 0.75f, Vector3.zero, new Vector3(6f, 0.3f, 3.4f), stone, true);
        Vector3 seat = dais + new Vector3(0f, 0.9f, 0.6f);
        _kit.Box(seat + Vector3.up * 0.5f, Vector3.zero, new Vector3(1.8f, 1f, 1.4f), roof, true);
        _kit.Box(seat + new Vector3(0f, 2f, 0.6f), Vector3.zero, new Vector3(1.8f, 3f, 0.3f), roof);
        _kit.Cone(seat + new Vector3(0f, 3.5f, 0.6f), 1.2f, 2.6f, roof);
        foreach (float s in new[] { -1f, 1f }) _kit.Cone(seat + new Vector3(s * 0.8f, 3.2f, 0.6f), 0.5f, 1.8f, roof);
        // El latido del Frost: cristales enormes detrás del trono.
        Shards(seat + new Vector3(0f, 0f, 1.6f), 9, 2f, 5.5f);
        PointLight(seat + new Vector3(0f, 3.5f, 0.5f), new Color(0.25f, 0.55f, 1f), 16f, 3.2f);
        foreach (float s in new[] { -1f, 1f }) Candelabra(dais + new Vector3(s * 4.8f, 0f, -2.6f));
    }

    // ---------- Enemigos ----------

    private static readonly string[] EliteIds = { "Corrupt_Knight", "Corrupt_Mage", "Corrupt_Viking", "Corrupt_Ninja" };

    private void SpawnRoom(RoomState rs)
    {
        rs.spawned = true;
        CastleInteriorLayout.Room room = rs.room;
        if (room.theme == CastleInteriorLayout.Theme.Vestibule) return;
        var rng = new DeterministicRng(SeedUtil.Combine(Seed, SeedUtil.FromString("sala_" + room.rect.MinX + "_" + room.rect.MinZ)));
        int level = EnemySpawner.LevelForPlayer(1);

        if (room.theme == CastleInteriorLayout.Theme.Throne)
        {
            if (QuestLog.Stage(QuestLog.Citadel) >= QuestLog.CitadelDone) return;
            gameObject.AddComponent<ThroneEncounter>().Setup(this, rs.center, rs.radius, rng, level, _enemies);
            return;
        }

        EnemyDefinition melee = EnemySpawner.Find("Corrupt_Melee"), ranged = EnemySpawner.Find("Corrupt_Ranged");
        for (int i = 0; i < room.troops; i++)
        {
            EnemyDefinition def = rng.Chance(0.35f) ? ranged : melee;
            EnemyBrain b = EnemySpawner.Spawn(def, RandomPoint(rs, rng), rng.Range(0f, 360f), _enemies, level, rng.Chance(0.06f), rng);
            if (b != null)
            {
                b.Slot = i;
                rs.enemies.Add(b);
            }
        }
        for (int i = 0; i < room.elites; i++)
        {
            EnemyDefinition def = EnemySpawner.Find(EliteIds[rng.NextInt(0, EliteIds.Length)]);
            EnemyBrain b = EnemySpawner.Spawn(def, RandomPoint(rs, rng), rng.Range(0f, 360f), _enemies, level, false, rng);
            if (b != null) rs.enemies.Add(b);
        }
    }

    public Vector3 RandomPoint(Vector3 center, float radius, DeterministicRng rng)
    {
        for (int k = 0; k < 10; k++)
        {
            Vector2 r = new Vector2(rng.Range(-1f, 1f), rng.Range(-1f, 1f)) * radius * 0.65f;
            Vector3 p = center + new Vector3(r.x, 0f, r.y);
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return hit.position;
        }
        return center;
    }

    private Vector3 RandomPoint(RoomState rs, DeterministicRng rng) => RandomPoint(rs.center, rs.radius, rng);

    public int AliveEnemies
    {
        get
        {
            int n = 0;
            foreach (RoomState rs in _rooms) foreach (EnemyBrain b in rs.enemies) if (b != null && !b.IsDead) n++;
            return n;
        }
    }

    public int RoomsSpawned => _rooms.FindAll(r => r.spawned).Count;
    public Vector3 PortalPoint => _portal;
    public Vector3 ThroneCenter => _rooms.Find(r => r.room.theme == CastleInteriorLayout.Theme.Throne)?.center ?? Origin;
}
