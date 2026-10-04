using UnityEngine;

// Columna de luz sobre el botín valioso: se queda vertical aunque el objeto ruede.
public class LootBeam : MonoBehaviour
{
    private LineRenderer _line;
    private Color _color;

    public void Init(Color color)
    {
        _color = color;
        _line = gameObject.AddComponent<LineRenderer>();
        Shader s = Shader.Find("Sprites/Default");
        if (s != null) _line.sharedMaterial = new Material(s);
        _line.useWorldSpace = true;
        _line.positionCount = 2;
        _line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.16f), new Keyframe(1f, 0.04f));
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        _line.colorGradient = g;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
    }

    void LateUpdate()
    {
        if (_line == null) return;
        Vector3 p = transform.parent != null ? transform.parent.position : transform.position;
        float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 3f);
        _line.SetPosition(0, p);
        _line.SetPosition(1, p + Vector3.up * 3.2f * pulse);
    }
}
