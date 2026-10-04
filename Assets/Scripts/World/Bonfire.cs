using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Hoguera: punto de control. Se enciende la primera vez que el héroe pasa cerca, queda guardada en la partida
// y es donde reaparece al morir (la encendida más cercana al lugar de la muerte).
public class Bonfire : MonoBehaviour
{
    public const string CastleId = "hoguera_castillo";

    public string id = CastleId;
    public string displayName = "Hoguera";
    [Tooltip("Distancia a la que el héroe la enciende al pasar.")]
    public float lightRadius = 3.5f;
    [Tooltip("Distancia desde el centro donde reaparece el héroe.")]
    public float spawnDistance = 2.4f;

    private static readonly List<Bonfire> All = new List<Bonfire>();

    private readonly List<Renderer> _flames = new List<Renderer>();
    private Light _light;
    private FlickerLight _flicker;
    private Transform _player;
    private CharacterStats _playerStats;
    private bool _lit;
    private bool _initialized;

    public bool IsLit => _lit;
    public static IReadOnlyList<Bonfire> Instances => All;

    void Awake()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            if (r.name.StartsWith("Llama")) _flames.Add(r);
        _light = GetComponentInChildren<Light>(true);
        _flicker = GetComponentInChildren<FlickerLight>(true);
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        GameSession s = GameSession.Instance;
        SetLit(s != null ? s.IsBonfireLit(id) : id == CastleId);
        _initialized = true;
    }

    void Update()
    {
        if (_lit || !_initialized) return;
        if (_player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p == null) return;
            _player = p.transform;
            _playerStats = p.GetComponent<CharacterStats>();
        }
        if (_playerStats != null && _playerStats.IsDead) return;
        Vector3 d = _player.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude <= lightRadius * lightRadius) Kindle();
    }

    public void Kindle()
    {
        if (_lit) return;
        SetLit(true);
        GameSession s = GameSession.Instance;
        if (s != null) s.LightBonfire(id);
        BonfireBanner.Show();
    }

    private void SetLit(bool lit)
    {
        _lit = lit;
        foreach (Renderer r in _flames) if (r != null) r.enabled = lit;
        if (_light != null) _light.enabled = lit;
        if (_flicker != null) _flicker.enabled = lit;
    }

    public Vector3 SpawnPoint
    {
        get
        {
            Vector3 dir = transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            Vector3 p = transform.position + dir.normalized * spawnDistance;
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas)) p = hit.position;
            p.y = Mathf.Max(p.y, transform.position.y) + 0.05f;
            return p;
        }
    }

    public Quaternion SpawnRotation
    {
        get
        {
            Vector3 away = SpawnPoint - transform.position;
            away.y = 0f;
            return away.sqrMagnitude > 0.01f ? Quaternion.LookRotation(away.normalized) : Quaternion.identity;
        }
    }

    public static Bonfire Find(string bonfireId)
    {
        foreach (Bonfire b in All) if (b != null && b.id == bonfireId) return b;
        return null;
    }

    public static Bonfire NearestLit(Vector3 position)
    {
        Bonfire best = null;
        float bestDist = float.MaxValue;
        foreach (Bonfire b in All)
        {
            if (b == null || !b.IsLit) continue;
            float d = (b.transform.position - position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = b; }
        }
        return best != null ? best : Find(CastleId);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();
}
