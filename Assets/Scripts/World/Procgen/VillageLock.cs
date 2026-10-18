using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// El poblado empieza cerrado: el rastrillo de la puerta norte está bajado y una empalizada cierra el bosque del sur
// (solo se puede ir por el camino hasta el claro de Steve). Al terminar la charla con Steve empieza "La ciudadela
// caída" y el rastrillo sube. El estado sale de la misión (etapa ≥ 1 = abierto): sin banderas nuevas, las partidas
// viejas ya avanzadas quedan abiertas y no hay desbloqueos dobles.
// Por si alguien se cuela (rodando contra una esquina), mientras está cerrado el héroe vuelve al último punto válido.
public class VillageLock : MonoBehaviour
{
    public static VillageLock Instance { get; private set; }

    public static bool Open => QuestLog.Stage(QuestLog.Citadel) >= QuestLog.CitadelTalked;

    private const float WallRadius = VillageLayout.WallRadius;
    private const float GateHalfAngle = 8f;
    private const float ClearingRadius = 9.5f;
    private const float RoadHalfWidth = 3.4f;

    private Transform _portcullis;
    private Collider _portcullisCol;
    private NavMeshObstacle _obstacle;
    private float _noticeAt;
    private readonly Dictionary<PlayerController, Vector3> _lastValid = new Dictionary<PlayerController, Vector3>();
    private Material _iron, _wood, _woodDark;

    public bool PortcullisDown => _portcullis != null && _portcullisCol != null && _portcullisCol.enabled;

    public static VillageLock Build(Transform parent, Material iron, Material wood, Material woodDark)
    {
        var go = new GameObject("Cierre_Poblado");
        go.transform.SetParent(parent, false);
        VillageLock l = go.AddComponent<VillageLock>();
        l._iron = iron;
        l._wood = wood;
        l._woodDark = woodDark;
        l.Palisade();
        return l;
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        QuestLog.Changed -= OnQuest;
    }

    void Start()
    {
        Portcullis();
        QuestLog.Changed += OnQuest;
        if (Open) Raise(true);
    }

    private void OnQuest(string id, int stage)
    {
        if (id == QuestLog.Citadel && Open && PortcullisDown) StartCoroutine(RaiseSlowly());
    }

    // ---------- Rastrillo de la puerta norte ----------

    private void Portcullis()
    {
        Vector3 center = VillageLayout.Polar(0f, WallRadius);
        float span = 2f * WallRadius * Mathf.Sin(GateHalfAngle * Mathf.Deg2Rad) - 0.6f;
        const float h = 4.4f;
        _portcullis = new GameObject("Rastrillo_Norte").transform;
        _portcullis.SetParent(transform, false);
        _portcullis.position = center;
        _portcullis.rotation = Quaternion.identity;
        for (float x = -span * 0.5f + 0.25f; x <= span * 0.5f - 0.2f; x += 0.5f)
            Bar(new Vector3(x, h * 0.5f, 0f), new Vector3(0.12f, h, 0.12f));
        foreach (float y in new[] { 0.9f, 2.2f, 3.5f })
            Bar(new Vector3(0f, y, 0f), new Vector3(span, 0.14f, 0.14f));
        // Puntas abajo, clavadas en la nieve.
        for (float x = -span * 0.5f + 0.25f; x <= span * 0.5f - 0.2f; x += 0.5f)
        {
            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(tip.GetComponent<Collider>());
            tip.transform.SetParent(_portcullis, false);
            tip.transform.localPosition = new Vector3(x, 0.05f, 0f);
            tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tip.transform.localScale = new Vector3(0.14f, 0.14f, 0.12f);
            tip.GetComponent<Renderer>().sharedMaterial = _iron;
        }
        var col = _portcullis.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, h * 0.5f, 0f);
        col.size = new Vector3(span + 0.4f, h, 0.6f);
        _portcullisCol = col;
        _obstacle = _portcullis.gameObject.AddComponent<NavMeshObstacle>();
        _obstacle.shape = NavMeshObstacleShape.Box;
        _obstacle.center = col.center;
        _obstacle.size = col.size;
        _obstacle.carving = true;
    }

    private void Bar(Vector3 local, Vector3 size)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(b.GetComponent<Collider>());
        b.name = "Barrote";
        b.transform.SetParent(_portcullis, false);
        b.transform.localPosition = local;
        b.transform.localScale = size;
        b.GetComponent<Renderer>().sharedMaterial = _iron;
    }

    private IEnumerator RaiseSlowly()
    {
        BonfireBanner.Show("LA PUERTA NORTE SE ABRE", BonfireBanner.Victory);
        Notifications.Show("El rastrillo de la puerta norte sube. El camino a la Ciudadela Caída sale de la calle del norte.", FrostboundUI.Gold);
        AudioManager.Play3D(AudioId.Corrupt_Death, _portcullis.position, 0.45f, 3f, 40f, 0.05f);
        Vector3 from = _portcullis.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 2f)
        {
            _portcullis.position = from + Vector3.up * Mathf.SmoothStep(0f, 3.9f, t);
            yield return null;
        }
        Raise(false);
    }

    // Arriba del todo: ya no choca ni corta el NavMesh.
    private void Raise(bool instant)
    {
        if (_portcullis == null) return;
        _portcullis.position = VillageLayout.Polar(0f, WallRadius) + Vector3.up * 3.9f;
        _portcullisCol.enabled = false;
        _obstacle.enabled = false;
    }

    // ---------- Empalizada del sur ----------

    // Estacas a los dos lados del camino al claro de Steve y alrededor del claro (salvo por donde llega el camino).
    private void Palisade()
    {
        var root = new GameObject("Empalizada_Sur").transform;
        root.SetParent(transform, false);
        var posts = new List<Vector3>();
        // Lados del camino.
        List<Vector3> road = SouthRoad(0.5f);
        for (int side = -1; side <= 1; side += 2)
        {
            var line = new List<Vector3>();
            for (int i = 1; i < road.Count; i++)
            {
                Vector3 dir = (road[i] - road[i - 1]).normalized;
                Vector3 n = new Vector3(dir.z, 0f, -dir.x) * side;
                Vector3 p = road[i] + n * (RoadHalfWidth + 0.5f);
                if (Flat(p).magnitude < WallRadius + 1.4f) continue;
                if (Dist(p, VillageLayout.SteveClearing) < ClearingRadius + 0.2f) continue;
                line.Add(p);
            }
            StakeLine(root, line);
        }
        // Anillo del claro, abierto solo donde entra el camino (justo su anchura), empezando por el hueco para que
        // la fila no salte por encima de él.
        var ring = new List<Vector3>();
        float start = 0f;
        for (float a = 0f; a < 360f; a += 2f)
            if (OnSouthRoad(VillageLayout.SteveClearing + VillageLayout.Polar(a, ClearingRadius + 1.2f), 0.1f)) { start = a; break; }
        for (float k = 0f; k < 360f; k += 5f)
        {
            Vector3 p = VillageLayout.SteveClearing + VillageLayout.Polar(start + k, ClearingRadius + 1.2f);
            if (OnSouthRoad(p, 0.1f)) { StakeLine(root, ring); ring = new List<Vector3>(); continue; }
            ring.Add(p);
        }
        StakeLine(root, ring);
    }

    // Una fila de estacas con colisión continua entre cada par.
    private void StakeLine(Transform parent, List<Vector3> pts)
    {
        var rng = new System.Random(pts.Count * 31 + 7);
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 p = pts[i];
            float h = 1.9f + (float)rng.NextDouble() * 0.6f;
            var stake = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(stake.GetComponent<Collider>());
            stake.name = "Estaca";
            stake.transform.SetParent(parent, false);
            stake.transform.position = p + Vector3.up * h * 0.5f;
            stake.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 6f - 3f, 0f, (float)rng.NextDouble() * 6f - 3f);
            stake.transform.localScale = new Vector3(0.32f, h * 0.5f, 0.32f);
            stake.GetComponent<Renderer>().sharedMaterial = (i & 1) == 0 ? _wood : _woodDark;
            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(tip.GetComponent<Collider>());
            tip.transform.SetParent(parent, false);
            tip.transform.position = p + Vector3.up * (h + 0.1f);
            tip.transform.rotation = Quaternion.Euler(45f, (float)rng.NextDouble() * 90f, 45f);
            tip.transform.localScale = Vector3.one * 0.24f;
            tip.GetComponent<Renderer>().sharedMaterial = _woodDark;
            if (i == 0) continue;
            Vector3 a = pts[i - 1], b = p;
            float len = Dist(a, b);
            if (len > 3f) continue;
            var col = new GameObject("Colision_Empalizada").AddComponent<BoxCollider>();
            col.transform.SetParent(parent, false);
            col.transform.position = (a + b) * 0.5f + Vector3.up * 1.2f;
            col.transform.rotation = Quaternion.LookRotation(new Vector3(b.x - a.x, 0f, b.z - a.z));
            col.size = new Vector3(0.5f, 2.4f, len + 0.35f);
            // Viga horizontal que une las estacas.
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(beam.GetComponent<Collider>());
            beam.transform.SetParent(parent, false);
            beam.transform.position = (a + b) * 0.5f + Vector3.up * 1.1f;
            beam.transform.rotation = col.transform.rotation;
            beam.transform.localScale = new Vector3(0.14f, 0.16f, len + 0.2f);
            beam.GetComponent<Renderer>().sharedMaterial = _woodDark;
        }
    }

    // ---------- Límite de respaldo ----------

    // Zona permitida mientras está cerrado: dentro de la muralla, el camino del sur y el claro de Steve.
    public static bool Allowed(Vector3 p)
    {
        if (Flat(p).magnitude < WallRadius - 0.4f) return true;
        if (Mathf.Abs(p.x) < RoadHalfWidth && p.z < 0f && p.z > -WallRadius - 5f) return true;
        if (Dist(p, VillageLayout.SteveClearing) < ClearingRadius + 0.6f) return true;
        return OnSouthRoad(p, 0.3f);
    }

    private static bool OnSouthRoad(Vector3 p, float margin)
    {
        foreach (Vector3 q in SouthRoad(1f))
            if (Dist(p, q) < RoadHalfWidth + margin) return true;
        return false;
    }

    private static List<Vector3> _road;

    // El camino de huellas de la puerta sur al claro (la misma curva que dibuja SetupVillage.BuildOutsidePaths).
    private static List<Vector3> SouthRoad(float step)
    {
        if (_road != null && Mathf.Approximately(step, 1f)) return _road;
        Vector3 start = VillageLayout.Polar(180f, WallRadius + 2f);
        Vector3 end = VillageLayout.SteveClearing + new Vector3(3f, 0f, 3f);
        Vector3 ctrl = new Vector3(0f, 0f, (start.z + end.z) * 0.5f - 2f);
        var pts = new List<Vector3> { Vector3.Lerp(start, Vector3.zero, 0.06f) };
        float approx = Vector3.Distance(start, end) * 1.2f;
        int n = Mathf.Max(4, Mathf.CeilToInt(approx / step));
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            pts.Add(Vector3.Lerp(Vector3.Lerp(start, ctrl, t), Vector3.Lerp(ctrl, end, t), t));
        }
        if (Mathf.Approximately(step, 1f)) _road = pts;
        return pts;
    }

    void FixedUpdate()
    {
        if (Open) return;
        foreach (PlayerController pc in Players.All)
        {
            if (pc == null || pc.Stats == null || pc.Stats.IsDead) continue;
            Vector3 p = pc.transform.position;
            bool known = _lastValid.TryGetValue(pc, out Vector3 last);
            if (Allowed(p))
            {
                _lastValid[pc] = p;
                continue;
            }
            // Un salto enorme en un paso es un teletransporte de un script (viaje, pruebas): se respeta.
            if (!known || Dist(p, last) > 8f) continue;
            PlayerPersistence mover = pc.GetComponent<PlayerPersistence>();
            if (mover != null) mover.MoveTo(last, pc.transform.rotation);
            else pc.transform.position = last;
            if (Time.time > _noticeAt)
            {
                _noticeAt = Time.time + 4f;
                Notifications.Show("La puerta norte sigue cerrada. Habla con Steve, en su claro al sur del poblado.", FrostboundUI.Gold);
            }
        }
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    private static float Dist(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
}
