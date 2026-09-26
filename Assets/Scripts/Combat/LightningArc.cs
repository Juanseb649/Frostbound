using UnityEngine;

// Rayo en zigzag entre dos puntos (runa de cadena). Dura un instante.
public class LightningArc : MonoBehaviour
{
    private static Material _material;
    private LineRenderer _line;
    private Vector3 _a, _b;
    private float _dieAt, _nextJitter;

    public static void Spawn(Vector3 a, Vector3 b, Color color, float seconds = 0.22f)
    {
        if (_material == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) return;
            _material = new Material(s);
        }
        var go = new GameObject("Rayo");
        var arc = go.AddComponent<LightningArc>();
        arc._a = a;
        arc._b = b;
        arc._dieAt = Time.time + seconds;
        arc._line = go.AddComponent<LineRenderer>();
        arc._line.sharedMaterial = _material;
        arc._line.startColor = color;
        arc._line.endColor = new Color(1f, 1f, 1f, 0.9f);
        arc._line.startWidth = 0.08f;
        arc._line.endWidth = 0.04f;
        arc._line.positionCount = 9;
        arc._line.useWorldSpace = true;
        arc._line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        arc.Jitter();
    }

    private void Jitter()
    {
        int n = _line.positionCount;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            Vector3 p = Vector3.Lerp(_a, _b, t);
            if (i > 0 && i < n - 1) p += Random.insideUnitSphere * 0.25f;
            _line.SetPosition(i, p);
        }
        _nextJitter = Time.time + 0.04f;
    }

    void Update()
    {
        if (Time.time > _dieAt)
        {
            Destroy(gameObject);
            return;
        }
        if (Time.time > _nextJitter) Jitter();
    }
}
