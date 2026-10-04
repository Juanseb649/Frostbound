using System.Collections;
using UnityEngine;

// Muerte de un corrupto: se congela del todo un instante y estalla en esquirlas de hielo (spec §2.5).
public class IceShatter : MonoBehaviour
{
    private static Material _shardMat;

    public static void Play(EnemyBrain enemy, Transform model, Vector3 center, Color plumage)
    {
        enemy.StartCoroutine(Run(enemy, model, center, plumage));
    }

    private static Material ShardMaterial()
    {
        if (_shardMat != null) return _shardMat;
        Shader s = Shader.Find("Frostbound/Toon");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
        _shardMat = new Material(s);
        Color ice = new Color(0.66f, 0.83f, 1f);
        if (_shardMat.HasProperty("_BaseColor")) _shardMat.SetColor("_BaseColor", ice);
        if (_shardMat.HasProperty("_EmissionColor")) _shardMat.SetColor("_EmissionColor", new Color(0.12f, 0.31f, 0.82f) * 0.8f);
        return _shardMat;
    }

    private static IEnumerator Run(EnemyBrain enemy, Transform model, Vector3 center, Color plumage)
    {
        // Congelación total: el Damageable tiñe de azul mientras está congelado.
        Damageable d = enemy.GetComponent<Damageable>();
        if (d != null) d.enabled = false;
        var block = new MaterialPropertyBlock();
        float t = 0f;
        Vector3 scale = model.localScale;
        while (t < 0.18f)
        {
            t += Time.deltaTime;
            float k = t / 0.18f;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                for (int m = 0; m < r.sharedMaterials.Length; m++)
                {
                    r.GetPropertyBlock(block, m);
                    block.SetColor("_BaseColor", Color.Lerp(Color.white, new Color(0.7f, 0.88f, 1.3f), k));
                    r.SetPropertyBlock(block, m);
                }
            }
            model.localScale = scale * (1f + 0.06f * k);
            yield return null;
        }

        Material mat = ShardMaterial();
        int n = Random.Range(9, 13);
        for (int i = 0; i < n; i++)
        {
            GameObject s = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Capsule);
            s.name = "Esquirla";
            s.transform.position = center + Random.insideUnitSphere * 0.35f;
            s.transform.rotation = Random.rotation;
            float size = Random.Range(0.07f, 0.16f);
            s.transform.localScale = new Vector3(size, size * Random.Range(1.4f, 2.6f), size);
            s.GetComponent<Renderer>().sharedMaterial = mat;
            s.layer = 2;
            Rigidbody rb = s.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            Vector3 v = Random.insideUnitSphere * 3.5f;
            v.y = Mathf.Abs(v.y) + 2.5f;
            rb.linearVelocity = v;
            rb.angularVelocity = Random.insideUnitSphere * 12f;
            s.AddComponent<IceShatter>().Begin(Random.Range(0.9f, 1.4f));
        }
        Object.Destroy(enemy.gameObject);
    }

    private float _life, _age;
    private Vector3 _scale;

    private void Begin(float life)
    {
        _life = life;
        _scale = transform.localScale;
    }

    void Update()
    {
        _age += Time.deltaTime;
        float k = Mathf.Clamp01((_age - _life * 0.6f) / (_life * 0.4f));
        transform.localScale = _scale * (1f - k);
        if (_age >= _life) Destroy(gameObject);
    }
}
