using System.Collections;
using UnityEngine;

// Chamán corrupto (aparece tras la caída de Rocco Vendaval): como los chamanes de Diablo II, devuelve a la vida
// a los corruptos caídos cerca y, si no hay a quién revivir, levanta gólems de nieve corruptos.
[RequireComponent(typeof(EnemyBrain))]
public class ShamanCaster : MonoBehaviour
{
    public float reviveRange = 11f;
    public float corpseLifetime = 25f;
    public float castEvery = 9f;
    public int maxGolems = 2;

    private EnemyBrain _brain;
    private Damageable _health;
    private float _nextCast;
    private static Material _beam;

    void Awake()
    {
        _brain = GetComponent<EnemyBrain>();
        _health = GetComponent<Damageable>();
        _nextCast = Time.time + Random.Range(3f, 6f);
    }

    void Update()
    {
        if (_health == null || _health.IsDead || _brain.Current == EnemyBrain.State.Idle || Time.time < _nextCast) return;
        _nextCast = Time.time + castEvery * Random.Range(0.85f, 1.2f);
        EnemyBrain.Corpse corpse = FindCorpse();
        if (corpse != null) StartCoroutine(Revive(corpse));
        else Summon();
    }

    private EnemyBrain.Corpse FindCorpse()
    {
        EnemyBrain.Corpse best = null;
        float bestD = reviveRange * reviveRange;
        for (int i = EnemyBrain.Corpses.Count - 1; i >= 0; i--)
        {
            EnemyBrain.Corpse c = EnemyBrain.Corpses[i];
            if (Time.time - c.time > corpseLifetime) { EnemyBrain.Corpses.RemoveAt(i); continue; }
            if (c.def == null || c.def.id == EnemyCamp.ShamanId) continue;
            float d = (c.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    private IEnumerator Revive(EnemyBrain.Corpse c)
    {
        EnemyBrain.Corpses.Remove(c);
        if (WorldHUD.Instance != null) WorldHUD.Instance.Popup(transform.position + Vector3.up * 1.8f, "¡Levántate!", new Color(0.5f, 0.85f, 1f), 1.1f);
        GameObject beam = Beam(c.position);
        yield return new WaitForSeconds(1.1f);
        if (beam != null) Destroy(beam);
        if (_health.IsDead) yield break;
        if (c.body != null) Destroy(c.body);
        EnemyCamp camp = c.camp != null ? c.camp : _brain.Camp;
        EnemyBrain revived = null;
        if (camp != null) revived = camp.SpawnMember(c.def, c.position, c.level, c.plumage);
        if (revived != null)
        {
            GroundImpact.Spawn(c.position, 1.4f, 1f);
            revived.Alert();
        }
    }

    private void Summon()
    {
        Vector3 at = transform.position + transform.right * (Random.value < 0.5f ? 1.8f : -1.8f) + transform.forward * 1.2f;
        float hp = 30f + _brain.Level * 10f;
        float dmg = 3f + _brain.Level * 1.1f;
        SnowGolem.Summon(gameObject, at, hp, dmg, true, maxGolems);
        if (WorldHUD.Instance != null) WorldHUD.Instance.Popup(transform.position + Vector3.up * 1.8f, "¡Nieve, obedece!", new Color(0.5f, 0.85f, 1f), 1f);
    }

    // Columna de luz azul sobre el caído mientras el chamán lo revive.
    private static GameObject Beam(Vector3 at)
    {
        if (_beam == null) _beam = MenuRuins.Transparent("Haz del chamán", MenuRuins.Soft(32), new Color(0.4f, 0.8f, 1f, 0.6f), true);
        var go = new GameObject("Haz_Revivir");
        go.transform.position = at;
        var q = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(go.transform, false);
        q.transform.localPosition = new Vector3(0f, 2f, 0f);
        q.transform.localScale = new Vector3(0.9f, 2f, 0.9f);
        q.GetComponent<Renderer>().sharedMaterial = _beam;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.4f, 0.8f, 1f);
        l.range = 6f;
        l.intensity = 3f;
        return go;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _beam = null;
}
