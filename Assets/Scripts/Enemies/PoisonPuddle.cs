using UnityEngine;

// Charco de savia venenosa (escupesavia, Madre Savia): envenena a los héroes que lo pisan y se seca en unos segundos.
public class PoisonPuddle : MonoBehaviour
{
    public float radius = 1.4f;
    public float dps = 4f;
    public float seconds = 5f;

    private float _born;
    private Renderer _r;
    private MaterialPropertyBlock _mpb;
    private static Material _mat;
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    public static PoisonPuddle Spawn(Vector3 at, float radius, float dps, float seconds)
    {
        // Al suelo: el proyectil puede acabar en el aire.
        if (Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<Damageable>() == null)
            at = hit.point;
        else if (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit nav, 3f, UnityEngine.AI.NavMesh.AllAreas))
            at = nav.position;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(go.GetComponent<Collider>());
        go.name = "Charco_Veneno";
        go.transform.SetPositionAndRotation(at + Vector3.up * 0.04f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        go.transform.localScale = new Vector3(radius * 2f, 0.015f, radius * 1.7f);
        if (_mat == null)
            _mat = MenuRuins.Transparent("Savia venenosa", MenuRuins.Soft(32), new Color(0.35f, 0.9f, 0.25f, 0.75f), false);
        go.GetComponent<Renderer>().sharedMaterial = _mat;
        var p = go.AddComponent<PoisonPuddle>();
        p.radius = radius;
        p.dps = dps;
        p.seconds = seconds;
        return p;
    }

    void Start()
    {
        _born = Time.time;
        _r = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        float age = Time.time - _born;
        if (age > seconds)
        {
            Destroy(gameObject);
            return;
        }
        // Crece al caer y se desvanece al final.
        float grow = Mathf.Clamp01(age / 0.25f);
        float fade = 1f - Mathf.Clamp01((age - (seconds - 1f)) / 1f);
        transform.localScale = new Vector3(radius * 2f * grow, 0.015f, radius * 1.7f * grow);
        _mpb.SetColor(ColorId, new Color(0.35f, 0.9f, 0.25f, 0.75f * fade));
        _r.SetPropertyBlock(_mpb);

        foreach (PlayerController pc in Players.All)
        {
            if (pc == null || pc.Stats == null || pc.Stats.IsDead) continue;
            Vector3 d = pc.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < radius * radius * grow) PlayerAfflictions.On(pc.Stats).ApplyPoison(dps, 1.5f);
        }
    }
}
