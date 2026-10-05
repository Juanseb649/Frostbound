using UnityEngine;

// Flecha de hielo de los arqueros corruptos. Solo daña al jugador; se rompe contra cualquier obstáculo.
public class EnemyProjectile : MonoBehaviour
{
    private Vector3 _dir;
    private float _speed, _left, _damage;
    private GameObject _owner;

    public static EnemyProjectile Launch(Vector3 origin, Vector3 direction, float speed, float range, float damage, Material material, GameObject owner, GameObject model = null)
    {
        var go = new GameObject("FlechaDeHielo");
        go.transform.position = origin;
        Vector3 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        go.transform.rotation = Quaternion.LookRotation(dir);

        if (model != null)
        {
            // Flecha de verdad (la que llevaba encajada en el arco), helada.
            GameObject arrow = Object.Instantiate(model, go.transform, false);
            foreach (Collider c in arrow.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            arrow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            if (material != null)
                foreach (Renderer r in arrow.GetComponentsInChildren<Renderer>())
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = material;
                    r.sharedMaterials = mats;
                }
        }
        else
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.Destroy(shard.GetComponent<Collider>());
            shard.transform.SetParent(go.transform, false);
            shard.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shard.transform.localScale = new Vector3(0.07f, 0.22f, 0.07f);
            if (material != null) shard.GetComponent<Renderer>().sharedMaterial = material;
        }

        var p = go.AddComponent<EnemyProjectile>();
        p._dir = dir;
        p._speed = speed;
        p._left = range;
        p._damage = damage;
        p._owner = owner;
        return p;
    }

    void Update()
    {
        float step = _speed * Time.deltaTime;
        Vector3 from = transform.position;
        if (Physics.SphereCast(from, 0.12f, _dir, out RaycastHit hit, step, ~0, QueryTriggerInteraction.Ignore))
        {
            if (_owner == null || !hit.transform.IsChildOf(_owner.transform))
            {
                if (hit.collider.GetComponentInParent<EnemyBrain>() != null || hit.collider.GetComponentInParent<WorldItem>() != null)
                {
                    transform.position = hit.point + _dir * 0.3f;
                    return;
                }
                CharacterStats stats = hit.collider.GetComponentInParent<CharacterStats>();
                if (stats != null)
                {
                    PlayerDodge dodge = stats.GetComponent<PlayerDodge>();
                    if (dodge != null && dodge.IsRolling)
                    {
                        transform.position = hit.point + _dir * 0.6f;
                        return;
                    }
                    EnemyBrain.DamagePlayer(stats, _damage, _dir);
                }
                Destroy(gameObject);
                return;
            }
        }
        transform.position = from + _dir * step;
        _left -= step;
        if (_left <= 0f) Destroy(gameObject);
    }
}
