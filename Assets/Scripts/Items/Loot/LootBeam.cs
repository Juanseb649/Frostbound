using UnityEngine;

// Columna de luz sobre el botín valioso: se queda vertical aunque el objeto ruede.
public class LootBeam : MonoBehaviour
{
    private LineRenderer _line;
    private Color _color;
    private float _height = 3.2f;
    private Light _light;
    private float _lightIntensity;

    public void Init(Color color, float height = 3.2f, float light = 0f)
    {
        _color = color;
        _height = height;
        if (light > 0f)
        {
            var lg = new GameObject("Brillo");
            lg.transform.SetParent(transform, false);
            _light = lg.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = color;
            _light.range = 2.6f;
            _light.intensity = _lightIntensity = light;
            _light.shadows = LightShadows.None;
        }
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
        _line.SetPosition(1, p + Vector3.up * _height * pulse);
        if (_light != null)
        {
            _light.transform.position = p + Vector3.up * 0.35f;
            _light.intensity = _lightIntensity * (0.75f + 0.25f * Mathf.Sin(Time.time * 4f));
        }
    }
}
