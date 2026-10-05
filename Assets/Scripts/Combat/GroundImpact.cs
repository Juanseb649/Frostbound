using UnityEngine;

// Golpe contra el suelo de las armas pesadas: anillo de nieve que se expande, trozos de hielo que saltan y un destello.
public class GroundImpact : MonoBehaviour
{
    private static Material _ringMaterial, _chunkMaterial;
    private Transform _ring;
    private Transform[] _chunks;
    private Vector3[] _vel;
    private Light _flash;
    private float _t, _radius;
    private const float Life = 0.55f;

    public static void Spawn(Vector3 position, float radius, float intensity = 1f)
    {
        var go = new GameObject("Impacto");
        go.transform.position = position + Vector3.up * 0.04f;
        go.AddComponent<GroundImpact>().Build(radius, intensity);
    }

    private void Build(float radius, float intensity)
    {
        _radius = radius;
        if (_ringMaterial == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            _ringMaterial = new Material(sh) { color = new Color(0.92f, 0.96f, 1f, 0.75f) };
            _chunkMaterial = new Material(sh) { color = new Color(0.82f, 0.9f, 1f, 1f) };
        }
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.GetComponent<MeshRenderer>().sharedMaterial = new Material(_ringMaterial);
        ring.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.transform.SetParent(transform, false);
        ring.transform.localScale = new Vector3(0.2f, 0.01f, 0.2f);
        _ring = ring.transform;

        int n = Mathf.RoundToInt(8 * intensity) + 4;
        _chunks = new Transform[n];
        _vel = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(c.GetComponent<Collider>());
            c.GetComponent<MeshRenderer>().sharedMaterial = _chunkMaterial;
            c.transform.SetParent(transform, false);
            c.transform.localScale = Vector3.one * Random.Range(0.05f, 0.12f);
            c.transform.localRotation = Random.rotation;
            float a = Random.value * Mathf.PI * 2f;
            _vel[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(1.5f, 3.5f) * radius + Vector3.up * Random.Range(2.5f, 4.5f);
            _chunks[i] = c.transform;
        }
        _flash = gameObject.AddComponent<Light>();
        _flash.type = LightType.Point;
        _flash.color = new Color(0.75f, 0.88f, 1f);
        _flash.range = radius * 3f;
        _flash.intensity = 2.5f * intensity;
        _flash.shadows = LightShadows.None;
    }

    void Update()
    {
        _t += Time.deltaTime;
        float k = _t / Life;
        float r = Mathf.Lerp(0.2f, _radius * 2f, 1f - (1f - k) * (1f - k));
        _ring.localScale = new Vector3(r, 0.01f, r);
        Material m = _ring.GetComponent<MeshRenderer>().sharedMaterial;
        m.color = new Color(m.color.r, m.color.g, m.color.b, 0.75f * (1f - k));
        for (int i = 0; i < _chunks.Length; i++)
        {
            _vel[i] += Physics.gravity * Time.deltaTime;
            _chunks[i].localPosition += _vel[i] * Time.deltaTime;
            if (_chunks[i].localPosition.y < 0f) { _chunks[i].localPosition = new Vector3(_chunks[i].localPosition.x, 0f, _chunks[i].localPosition.z); _vel[i] *= 0.3f; }
            _chunks[i].localScale *= 1f - Time.deltaTime * 1.6f;
        }
        if (_flash != null) _flash.intensity *= 1f - Time.deltaTime * 9f;
        if (_t >= Life) Destroy(gameObject);
    }
}
