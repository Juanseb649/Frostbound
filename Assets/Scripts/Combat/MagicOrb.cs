using System;
using UnityEngine;

// Orbe mágico amarillo que dispara el báculo del mago: esfera brillante con luz y estela que vuela recta,
// atraviesa a los aliados y estalla en un destello al tocar a un enemigo o el escenario.
public class MagicOrb : MonoBehaviour
{
    public static readonly Color Gold = new Color(1f, 0.82f, 0.28f);
    private static Material _core, _glow, _burst;

    private Vector3 _dir;
    private float _speed, _left;
    private GameObject _owner;
    private Action<Damageable, Vector3> _onHit;
    private Transform _visual;
    private Light _light;

    public static MagicOrb Launch(Vector3 origin, Vector3 direction, float speed, float range, GameObject owner, Action<Damageable, Vector3> onHit, float size = 1f)
    {
        EnsureMaterials();
        var go = new GameObject("Orbe_Magico");
        go.transform.position = origin;
        var orb = go.AddComponent<MagicOrb>();
        orb._dir = direction.normalized;
        orb._speed = speed;
        orb._left = range;
        orb._owner = owner;
        orb._onHit = onHit;

        orb._visual = new GameObject("Brillo").transform;
        orb._visual.SetParent(go.transform, false);
        Sphere(orb._visual, 0.3f * size, _glow);
        Sphere(orb._visual, 0.17f * size, _core);
        orb._light = go.AddComponent<Light>();
        orb._light.type = LightType.Point;
        orb._light.color = Gold;
        orb._light.range = 4f;
        orb._light.intensity = 2.6f;
        orb._light.shadows = LightShadows.None;

        var trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = _glow;
        trail.time = 0.22f;
        trail.minVertexDistance = 0.04f;
        trail.widthMultiplier = 0.2f * size;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(Gold, 1f) },
                  new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return orb;
    }

    private static void Sphere(Transform parent, float d, Material m)
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(s.GetComponent<Collider>());
        s.transform.SetParent(parent, false);
        s.transform.localScale = Vector3.one * d;
        var r = s.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void EnsureMaterials()
    {
        if (_core != null) return;
        _core = MenuRuins.Lit("Orbe núcleo", new Color(1f, 0.97f, 0.8f), 0.9f, 0f, new Color(2.4f, 2.1f, 1.2f));
        _glow = MenuRuins.Transparent("Orbe halo", MenuRuins.Soft(32), new Color(1f, 0.8f, 0.25f, 0.85f), true);
        _burst = MenuRuins.Transparent("Orbe estallido", MenuRuins.Soft(32), new Color(1f, 0.85f, 0.35f, 0.9f), true);
    }

    void Update()
    {
        float step = _speed * Time.deltaTime;
        float pulse = 1f + Mathf.Sin(Time.time * 30f) * 0.12f;
        _visual.localScale = Vector3.one * pulse;
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, 0.18f, _dir, step, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        RaycastHit? hit = null;
        foreach (RaycastHit h in hits)
        {
            if (_owner != null && h.transform.IsChildOf(_owner.transform)) continue;
            if (h.collider.GetComponentInParent<WorldItem>() != null) continue;
            if (h.collider.GetComponentInParent<PlayerController>() != null) continue;
            Damageable d = h.collider.GetComponentInParent<Damageable>();
            if (d != null && d.friendly) continue;
            if (h.distance < best) { best = h.distance; hit = h; }
        }
        if (hit.HasValue)
        {
            RaycastHit h = hit.Value;
            Vector3 point = h.distance > 0f ? transform.position + _dir * h.distance : transform.position;
            Damageable target = h.collider.GetComponentInParent<Damageable>();
            if (target != null && !target.IsDead) _onHit?.Invoke(target, point);
            Burst(point);
            Destroy(gameObject);
            return;
        }
        transform.position += _dir * step;
        _left -= step;
        if (_left <= 0f)
        {
            Burst(transform.position);
            Destroy(gameObject);
        }
    }

    // Destello: una esfera aditiva que se expande y se apaga con su luz.
    public static void Burst(Vector3 point)
    {
        EnsureMaterials();
        var go = new GameObject("Destello_Orbe");
        go.transform.position = point;
        Sphere(go.transform, 0.3f, _burst);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = Gold;
        l.range = 5f;
        l.intensity = 4f;
        go.AddComponent<OrbBurst>();
    }

    private class OrbBurst : MonoBehaviour
    {
        private float _t;
        private Light _l;
        private Transform _s;

        void Start()
        {
            _l = GetComponent<Light>();
            _s = transform.GetChild(0);
        }

        void Update()
        {
            _t += Time.deltaTime / 0.28f;
            if (_t >= 1f) { Destroy(gameObject); return; }
            _s.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.6f, _t);
            _l.intensity = Mathf.Lerp(4f, 0f, _t);
        }
    }
}
