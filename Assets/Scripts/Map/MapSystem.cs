using System;
using System.Collections.Generic;
using UnityEngine;

// Mapa del juego: una "foto" cenital estilizada de cada zona, la niebla de guerra que se va despejando
// al explorar (guardada en la partida) y los marcadores (hogueras, poblado, Steve, objetivos...).
public class MapSystem : MonoBehaviour
{
    public static MapSystem Instance { get; private set; }

    public class Area
    {
        public string id, title;
        public Vector3 center;
        public float half;
        public int fogSize;
        public float revealRadius;
        public Texture2D map;
        public Texture2D fogTex;
        public bool[] fog;
        public bool fogDirty;
        public int explored;

        public Vector2 ToUV(Vector3 world) => new Vector2((world.x - center.x) / (half * 2f) + 0.5f, (world.z - center.z) / (half * 2f) + 0.5f);
        public bool Contains(Vector3 p) => Mathf.Abs(p.x - center.x) <= half && Mathf.Abs(p.z - center.z) <= half;
        public float ExploredPercent => fog == null || fog.Length == 0 ? 0f : explored * 100f / fog.Length;

        public bool IsExplored(Vector3 world)
        {
            Vector2 uv = ToUV(world);
            int x = Mathf.FloorToInt(uv.x * fogSize), y = Mathf.FloorToInt(uv.y * fogSize);
            return x >= 0 && y >= 0 && x < fogSize && y < fogSize && fog[y * fogSize + x];
        }
    }

    public enum MarkerKind { Player, Village, Bonfire, BonfireLit, Steve, Objective, Castle, Citadel, Exit, Throne }

    public struct Marker
    {
        public MarkerKind kind;
        public Vector3 position;
        public string label;
        public bool always;      // visible aunque la zona no esté explorada
        public bool clampToEdge; // en el minimapa, se queda en el borde si está lejos
    }

    public readonly List<Area> areas = new List<Area>();
    public readonly List<Marker> markers = new List<Marker>();
    public Area Village { get; private set; }
    public Area Interior { get; private set; }

    private VillageLayout.Result _layout;
    private Transform _player, _steve;
    private float _fogApplyAt, _saveAt;

    public static readonly Color FogColor = new Color(0.02f, 0.035f, 0.07f, 1f);

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Setup(VillageLayout.Result layout)
    {
        _layout = layout;
        Village = new Area { id = "poblado", title = "Alrededores del poblado", center = Vector3.zero, half = VillageLayout.GroundHalfSize, fogSize = 128, revealRadius = 24f };
        InitFog(Village);
        areas.Add(Village);
    }

    void Start()
    {
        if (Village != null && Village.map == null) Village.map = Capture(Village, 1024, false);
        // El poblado y sus calles se conocen desde el principio.
        if (Village != null) Reveal(Village, Vector3.zero, VillageLayout.WallRadius + 6f);
    }

    private void InitFog(Area a)
    {
        a.fog = new bool[a.fogSize * a.fogSize];
        a.fogTex = new Texture2D(a.fogSize, a.fogSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Niebla_" + a.id };
        Load(a);
        a.fogDirty = true;
        ApplyFog(a);
    }

    // ---------- Foto cenital ----------

    private Texture2D Capture(Area a, int res, bool interior)
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
            foreach (Light l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional) { sun = l; break; }
        bool fog = RenderSettings.fog;
        var mode = RenderSettings.ambientMode;
        Color amb = RenderSettings.ambientLight;
        float sunI = sun != null ? sun.intensity : 1f;
        Color sunC = sun != null ? sun.color : Color.white;
        RenderSettings.fog = false;
        if (interior)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.66f);
            if (sun != null) { sun.intensity = 1.1f; sun.color = Color.white; }
        }

        var go = new GameObject("CamaraMapa");
        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = a.half;
        cam.transform.SetPositionAndRotation(a.center + Vector3.up * 600f, Quaternion.Euler(90f, 0f, 0f));
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 900f;
        cam.cullingMask = ~(1 << 5);
        var rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Mapa_" + a.id };
        tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
        RenderTexture.active = prev;
        cam.targetTexture = null;
        Destroy(rt);
        Destroy(go);

        RenderSettings.fog = fog;
        RenderSettings.ambientMode = mode;
        RenderSettings.ambientLight = amb;
        if (sun != null) { sun.intensity = sunI; sun.color = sunC; }

        Stylize(tex, interior);
        return tex;
    }

    // Tono de mapa: azul helado y algo de contraste, para que no parezca una captura de pantalla.
    private static void Stylize(Texture2D tex, bool interior)
    {
        Color32[] px = tex.GetPixels32();
        Color ink = interior ? new Color(0.62f, 0.7f, 0.86f) : new Color(0.72f, 0.82f, 0.95f);
        for (int i = 0; i < px.Length; i++)
        {
            Color c = px[i];
            float l = c.r * 0.3f + c.g * 0.55f + c.b * 0.15f;
            Color tinted = Color.Lerp(c, new Color(l, l, l) * ink, 0.35f);
            tinted = (tinted - new Color(0.5f, 0.5f, 0.5f)) * 1.25f + new Color(0.4f, 0.43f, 0.5f);
            tinted.a = 1f;
            px[i] = tinted;
        }
        tex.SetPixels32(px);
        tex.Apply();
    }

    // ---------- Niebla de guerra ----------

    void Update()
    {
        if (_player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p == null) return;
            _player = p.transform;
        }
        CastleInterior interior = CastleInterior.Instance;
        if (Interior == null && interior != null && interior.Built)
        {
            Interior = new Area
            {
                id = "castillo", title = "La Catedral Corrupta",
                center = CastleInterior.Origin + new Vector3(CastleInterior.Extent * 0.5f, 0f, CastleInterior.Extent * 0.5f),
                half = CastleInterior.Extent * 0.5f + 6f, fogSize = 64, revealRadius = 11f
            };
            InitFog(Interior);
            Interior.map = Capture(Interior, 512, true);
            areas.Add(Interior);
        }

        Area current = CurrentArea;
        if (current != null) Reveal(current, _player.position, current.revealRadius);
        if (Time.unscaledTime > _fogApplyAt)
        {
            _fogApplyAt = Time.unscaledTime + 0.25f;
            foreach (Area a in areas) ApplyFog(a);
        }
        if (Time.unscaledTime > _saveAt)
        {
            _saveAt = Time.unscaledTime + 2f;
            foreach (Area a in areas) Store(a);
        }
        RebuildMarkers();
    }

    public Area CurrentArea
    {
        get
        {
            if (_player == null) return Village;
            if (Interior != null && Interior.Contains(_player.position)) return Interior;
            return Village;
        }
    }

    public void Reveal(Area a, Vector3 world, float radius)
    {
        Vector2 uv = a.ToUV(world);
        float cellsPerMeter = a.fogSize / (a.half * 2f);
        int cx = Mathf.FloorToInt(uv.x * a.fogSize), cy = Mathf.FloorToInt(uv.y * a.fogSize);
        int r = Mathf.CeilToInt(radius * cellsPerMeter);
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= a.fogSize || y >= a.fogSize) continue;
                int i = y * a.fogSize + x;
                if (a.fog[i]) continue;
                float dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy > r * r) continue;
                a.fog[i] = true;
                a.explored++;
                a.fogDirty = true;
            }
    }

    private void ApplyFog(Area a)
    {
        if (!a.fogDirty) return;
        a.fogDirty = false;
        var px = new Color32[a.fog.Length];
        Color32 hidden = FogColor, shown = new Color32(0, 0, 0, 0);
        for (int i = 0; i < px.Length; i++) px[i] = a.fog[i] ? shown : hidden;
        a.fogTex.SetPixels32(px);
        a.fogTex.Apply();
    }

    private void Store(Area a)
    {
        SaveData data = GameSession.Instance != null ? GameSession.Instance.Current : null;
        if (data == null) return;
        SavedFog f = data.explored.Find(x => x.area == a.id);
        if (f == null) data.explored.Add(f = new SavedFog { area = a.id });
        f.size = a.fogSize;
        f.bits = Pack(a.fog);
    }

    private void Load(Area a)
    {
        SaveData data = GameSession.Instance != null ? GameSession.Instance.Current : null;
        SavedFog f = data != null ? data.explored.Find(x => x.area == a.id) : null;
        if (f == null || f.size != a.fogSize || string.IsNullOrEmpty(f.bits)) return;
        Unpack(f.bits, a.fog);
        a.explored = 0;
        foreach (bool b in a.fog) if (b) a.explored++;
    }

    public static string Pack(bool[] bits)
    {
        var bytes = new byte[(bits.Length + 7) / 8];
        for (int i = 0; i < bits.Length; i++) if (bits[i]) bytes[i >> 3] |= (byte)(1 << (i & 7));
        return Convert.ToBase64String(bytes);
    }

    public static void Unpack(string s, bool[] bits)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(s); }
        catch { return; }
        for (int i = 0; i < bits.Length && (i >> 3) < bytes.Length; i++) bits[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;
    }

    // ---------- Marcadores ----------

    private void RebuildMarkers()
    {
        markers.Clear();
        if (_layout == null) return;
        markers.Add(new Marker { kind = MarkerKind.Village, position = Vector3.zero, label = "Poblado", always = true });
        foreach (Bonfire b in Bonfire.Instances)
        {
            if (b == null) continue;
            bool lit = b.IsLit;
            markers.Add(new Marker { kind = lit ? MarkerKind.BonfireLit : MarkerKind.Bonfire, position = b.transform.position, label = lit ? "Hoguera" : "Hoguera apagada", always = lit });
        }
        markers.Add(new Marker { kind = MarkerKind.Castle, position = _layout.castle.Door, label = "Castillo", always = true });
        markers.Add(new Marker { kind = MarkerKind.Citadel, position = _layout.castle.TownCenter, label = "Ciudadela caída" });

        if (_steve == null)
        {
            GameObject s = GameObject.Find(SteveName);
            if (s != null) _steve = s.transform;
        }
        if (_steve != null) markers.Add(new Marker { kind = MarkerKind.Steve, position = _steve.position, label = "Steve", always = true });

        CastleInterior interior = CastleInterior.Instance;
        if (interior != null && interior.Built)
        {
            markers.Add(new Marker { kind = MarkerKind.Exit, position = interior.PortalPoint, label = "Salida", always = true });
            markers.Add(new Marker { kind = MarkerKind.Throne, position = interior.ThroneCenter, label = "Sala del trono" });
        }

        Vector3? objective = ObjectivePosition();
        if (objective.HasValue) markers.Add(new Marker { kind = MarkerKind.Objective, position = objective.Value, label = "Objetivo", always = true, clampToEdge = true });
        if (_player != null) markers.Add(new Marker { kind = MarkerKind.Player, position = _player.position, label = "Tú", always = true });
    }

    public const string SteveName = "NPC_Steve";

    // Dónde está el objetivo actual de la misión (según la etapa y si el héroe está dentro del castillo).
    public Vector3? ObjectivePosition()
    {
        int stage = QuestLog.Stage(QuestLog.Citadel);
        bool inside = CastleInterior.Instance != null && CastleInterior.Instance.PlayerInside;
        switch (stage)
        {
            case QuestLog.CitadelNone: return _steve != null ? _steve.position : (Vector3?)null;
            case QuestLog.CitadelTalked: return _layout.castle.TownCenter;
            case QuestLog.CitadelBarbarian: return inside ? CastleInterior.Instance.ThroneCenter : _layout.castle.Door;
            default: return null;
        }
    }

    public static MapIcons.Shape ShapeOf(MarkerKind k)
    {
        switch (k)
        {
            case MarkerKind.Player: return MapIcons.Shape.Arrow;
            case MarkerKind.Village: return MapIcons.Shape.House;
            case MarkerKind.Bonfire:
            case MarkerKind.BonfireLit: return MapIcons.Shape.Flame;
            case MarkerKind.Steve: return MapIcons.Shape.Star;
            case MarkerKind.Objective: return MapIcons.Shape.Exclaim;
            case MarkerKind.Castle: return MapIcons.Shape.Tower;
            case MarkerKind.Citadel: return MapIcons.Shape.Skull;
            case MarkerKind.Exit: return MapIcons.Shape.Square;
            default: return MapIcons.Shape.Skull;
        }
    }

    public static Color ColorOf(MarkerKind k)
    {
        switch (k)
        {
            case MarkerKind.Player: return Color.white;
            case MarkerKind.Village: return new Color(0.55f, 0.82f, 1f);
            case MarkerKind.Bonfire: return new Color(0.5f, 0.5f, 0.55f);
            case MarkerKind.BonfireLit: return new Color(1f, 0.62f, 0.22f);
            case MarkerKind.Steve: return new Color(1f, 0.85f, 0.3f);
            case MarkerKind.Objective: return new Color(1f, 0.82f, 0.25f);
            case MarkerKind.Castle: return new Color(0.62f, 0.55f, 1f);
            case MarkerKind.Citadel: return new Color(0.9f, 0.35f, 0.35f);
            case MarkerKind.Exit: return new Color(0.95f, 0.85f, 0.6f);
            default: return new Color(0.9f, 0.3f, 0.3f);
        }
    }

    public static float SizeOf(MarkerKind k) => k == MarkerKind.Player ? 1.25f : k == MarkerKind.Objective ? 1.15f : k == MarkerKind.Castle ? 1.2f : 1f;
}
