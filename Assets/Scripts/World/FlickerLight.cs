using UnityEngine;

public class FlickerLight : MonoBehaviour
{
    public Light targetLight;
    [Tooltip("Llamas que laten con la luz (opcional).")]
    public Transform[] flames;
    public float baseIntensity = 3f;
    public float intensityVariation = 0.8f;
    public float speed = 7f;
    public float flameScaleVariation = 0.18f;

    private Vector3[] _flameScales;
    private float _seed;

    void Awake()
    {
        if (targetLight == null) targetLight = GetComponentInChildren<Light>();
        _seed = Random.value * 50f;
        if (flames == null) return;
        _flameScales = new Vector3[flames.Length];
        for (int i = 0; i < flames.Length; i++)
            if (flames[i] != null) _flameScales[i] = flames[i].localScale;
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(Time.time * speed, _seed);
        if (targetLight != null) targetLight.intensity = baseIntensity + (n - 0.5f) * 2f * intensityVariation;

        if (flames == null) return;
        for (int i = 0; i < flames.Length; i++)
        {
            if (flames[i] == null) continue;
            float f = Mathf.PerlinNoise(Time.time * speed * 1.3f, _seed + i * 3.7f);
            float s = 1f + (f - 0.5f) * 2f * flameScaleVariation;
            Vector3 b = _flameScales[i];
            flames[i].localScale = new Vector3(b.x * (2f - s), b.y * s, b.z * (2f - s));
        }
    }
}
