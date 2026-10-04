using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Campamento de una horda (spec §4.3). Genera el grupo cuando el jugador se acerca, con la seed de la partida;
// si lo limpian, vuelve a poblarse cuando el jugador está lejos. Nunca coloca enemigos dentro de una zona segura.
public class EnemyCamp : MonoBehaviour
{
    public string campId = "Campamento";
    [Tooltip("Tropa cuerpo a cuerpo.")]
    public EnemyDefinition melee;
    [Tooltip("Tropa a distancia.")]
    public EnemyDefinition ranged;
    [Tooltip("Élites que pueden acompañar al grupo.")]
    public EnemyDefinition[] elites = new EnemyDefinition[0];

    [Header("Grupo")]
    public Vector2Int groupSize = new Vector2Int(4, 7);
    [Range(0f, 1f)] public float rangedFraction = 0.3f;
    [Range(0f, 1f)] public float eliteChance;
    [Tooltip("Las élites solo aparecen cuando el héroe llega a este nivel (el jugador avanza y la corrupción se agrava).")]
    [Min(1)] public int eliteMinPlayerLevel = 3;
    [Range(0f, 1f)] public float championChance = 0.05f;
    public Vector2Int monsterLevel = new Vector2Int(1, 2);
    [Tooltip("Paleta del jugador: cada corrupto toma uno de estos colores, apagado.")]
    public PlumagePalette palette;
    [Min(1f)] public float radius = 5f;

    [Header("Activación")]
    public float activateDistance = 55f;
    public float respawnSeconds = 150f;
    public float respawnMinPlayerDistance = 70f;

    private readonly List<EnemyBrain> _members = new List<EnemyBrain>();
    private bool _spawned;
    private int _wave;
    private float _clearedAt = -1f;
    private Transform _player;

    public IReadOnlyList<EnemyBrain> Members => _members;
    public bool Cleared => _spawned && _members.Count == 0;

    void Update()
    {
        if (_player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p == null) return;
            _player = p.transform;
        }
        float dist = Vector3.Distance(_player.position, transform.position);
        if (!_spawned)
        {
            if (dist < activateDistance) Spawn();
            return;
        }
        if (_members.Count == 0 && _clearedAt >= 0f && Time.time - _clearedAt > respawnSeconds && dist > respawnMinPlayerDistance)
        {
            _spawned = false;
            _clearedAt = -1f;
        }
    }

    public void Spawn()
    {
        _spawned = true;
        _wave++;
        int seed = SeedUtil.Combine(GameSession.Instance != null ? GameSession.Instance.worldSeed : 12345, SeedUtil.FromString(campId), _wave);
        var rng = new DeterministicRng(seed);

        int count = rng.NextInt(groupSize.x, groupSize.y + 1);
        int rangedCount = ranged != null ? Mathf.RoundToInt(count * rangedFraction) : 0;
        CharacterStats heroStats = _player != null ? _player.GetComponent<CharacterStats>() : null;
        bool eliteUnlocked = heroStats != null && heroStats.level >= eliteMinPlayerLevel;
        bool withElite = eliteUnlocked && elites != null && elites.Length > 0 && rng.Chance(eliteChance);
        int slot = 0;
        for (int i = 0; i < count; i++)
        {
            EnemyDefinition def = i < rangedCount ? ranged : melee;
            if (withElite && i == count - 1) def = elites[rng.NextInt(0, elites.Length)];
            if (def == null || def.prefab == null) continue;
            if (!FindPoint(rng, out Vector3 pos)) continue;

            GameObject go = Instantiate(def.prefab, pos, Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), transform);
            go.name = def.displayName + "_" + i;
            EnemyBrain brain = go.GetComponent<EnemyBrain>();
            int level = rng.NextInt(monsterLevel.x, monsterLevel.y + 1);
            bool champion = rng.Chance(championChance);
            Color plumage = CorruptPlumage(palette, rng);
            brain.Setup(def, this, level, champion, plumage);
            if (!def.ranged) brain.Slot = slot++;
            _members.Add(brain);
        }
    }

    private bool FindPoint(DeterministicRng rng, out Vector3 pos)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float a = rng.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(rng.Next01()) * radius;
            Vector3 p = transform.position + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
            if (SafeZone.Contains(p, 4f)) continue;
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                pos = hit.position;
                return true;
            }
        }
        pos = transform.position;
        return false;
    }

    // Plumaje de la paleta del jugador, apagado y frío (spec §2.1).
    public static Color CorruptPlumage(PlumagePalette palette, DeterministicRng rng)
    {
        Color c = palette != null && palette.entries != null && palette.entries.Count > 0
            ? palette.entries[rng.NextInt(0, palette.entries.Count)].color
            : new Color(0.12f, 0.31f, 0.71f);
        return Corrupt(c);
    }

    public static Color Corrupt(Color c)
    {
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        float l = (max + min) * 0.5f, s = 0f, h = 0f;
        if (max > min)
        {
            float d = max - min;
            s = l > 0.5f ? d / (2f - max - min) : d / (max + min);
            if (max == c.r) h = (c.g - c.b) / d + (c.g < c.b ? 6f : 0f);
            else if (max == c.g) h = (c.b - c.r) / d + 2f;
            else h = (c.r - c.g) / d + 4f;
            h /= 6f;
        }
        s *= 0.45f;
        l *= 0.72f;
        Color o;
        if (s <= 0f) o = new Color(l, l, l);
        else
        {
            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;
            o = new Color(Hue(p, q, h + 1f / 3f), Hue(p, q, h), Hue(p, q, h - 1f / 3f));
        }
        return Color.Lerp(o, new Color(0.227f, 0.29f, 0.42f), 0.28f);
    }

    private static float Hue(float p, float q, float t)
    {
        if (t < 0f) t += 1f;
        if (t > 1f) t -= 1f;
        if (t < 1f / 6f) return p + (q - p) * 6f * t;
        if (t < 0.5f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
        return p;
    }

    public void AlertAll(EnemyBrain source)
    {
        foreach (EnemyBrain b in _members) if (b != source) b.WakeUp();
    }

    public void OnMemberDied(EnemyBrain dead)
    {
        _members.Remove(dead);
        foreach (EnemyBrain b in _members)
            if ((b.transform.position - dead.transform.position).sqrMagnitude < 36f) b.Frenzy();
        if (_members.Count == 0) _clearedAt = Time.time;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.55f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
