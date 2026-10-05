using UnityEngine;

// Montoncito de monedas en el suelo: salta al caer y el héroe lo recoge solo al pasar cerca (como el oro de Diablo).
public class CoinPile : MonoBehaviour
{
    public int amount;
    private Vector3 _vel;
    private bool _landed;
    private float _born;
    private Transform _player;
    private static Material _gold;
    private const float PickupRadius = 1.7f, MagnetRadius = 3.2f;

    public static void Spawn(int amount, Vector3 position, Vector3 velocity)
    {
        if (amount <= 0) return;
        var go = new GameObject("Monedas_" + amount);
        go.transform.position = position;
        var c = go.AddComponent<CoinPile>();
        c.amount = amount;
        c._vel = velocity;
        c.Build();
    }

    private void Build()
    {
        _born = Time.time;
        if (_gold == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            _gold = new Material(sh) { name = "Monedas" };
            _gold.SetColor("_BaseColor", new Color(1f, 0.78f, 0.25f));
            _gold.SetFloat("_Metallic", 0.8f);
            _gold.SetFloat("_Smoothness", 0.7f);
            _gold.EnableKeyword("_EMISSION");
            _gold.SetColor("_EmissionColor", new Color(0.35f, 0.24f, 0.04f));
        }
        int coins = Mathf.Clamp(2 + amount / 6, 2, 7);
        for (int i = 0; i < coins; i++)
        {
            GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(c.GetComponent<Collider>());
            c.transform.SetParent(transform, false);
            c.transform.localScale = new Vector3(0.16f, 0.012f, 0.16f);
            Vector2 r = Random.insideUnitCircle * 0.1f;
            c.transform.localPosition = new Vector3(r.x, 0.012f + (i % 3) * 0.024f, r.y);
            c.transform.localRotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
            c.GetComponent<MeshRenderer>().sharedMaterial = _gold;
        }
    }

    void Update()
    {
        if (!_landed)
        {
            _vel += Physics.gravity * Time.deltaTime;
            Vector3 next = transform.position + _vel * Time.deltaTime;
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 0.3f + Mathf.Max(0f, -_vel.y * Time.deltaTime), ~0, QueryTriggerInteraction.Ignore)
                && _vel.y < 0f && hit.collider.GetComponentInParent<CharacterStats>() == null && hit.collider.GetComponentInParent<EnemyBrain>() == null)
            {
                next = hit.point;
                _landed = true;
            }
            if (next.y < -5f) _landed = true;
            transform.position = next;
        }
        else transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);

        if (Time.time - _born < 0.45f) return;
        if (_player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p == null) return;
            _player = p.transform;
        }
        Vector3 to = _player.position + Vector3.up * 0.5f - transform.position;
        float d = to.magnitude;
        if (d < MagnetRadius && _landed) transform.position += to.normalized * Mathf.Min(d, 9f * Time.deltaTime);
        if (d > PickupRadius) return;
        CharacterStats stats = _player.GetComponent<CharacterStats>();
        if (stats != null && stats.IsDead) return;
        Wallet.Of(_player.gameObject).Add(amount);
        if (WorldHUD.Instance != null) WorldHUD.Instance.Popup(_player.position + Vector3.up * 1.9f, "+" + amount + " monedas", new Color(1f, 0.82f, 0.3f), 0.85f);
        Destroy(gameObject);
    }
}
