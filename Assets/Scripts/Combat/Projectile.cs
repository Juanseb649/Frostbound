using System;
using UnityEngine;

// Flecha, virote o kunai lanzado. Avanza en línea recta, se clava donde choca y avisa si golpea a algo dañable.
public class Projectile : MonoBehaviour
{
    private Vector3 _dir;
    private float _speed, _maxDistance, _travelled;
    private GameObject _owner;
    private Action<Damageable, Vector3> _onHit;
    private bool _done;
    private float _dieAt;
    private float _spin;
    private Transform _model;

    public static Projectile Launch(GameObject model, Vector3 origin, Vector3 direction, float speed, float maxDistance,
        GameObject owner, Action<Damageable, Vector3> onHit, bool spin = false)
    {
        var go = new GameObject("Proyectil");
        go.transform.position = origin;
        var p = go.AddComponent<Projectile>();
        p._dir = direction.normalized;
        p._speed = speed;
        p._maxDistance = maxDistance;
        p._owner = owner;
        p._onHit = onHit;
        p._spin = spin ? 1440f : 0f;
        go.transform.rotation = Quaternion.LookRotation(p._dir, Vector3.up);
        if (model != null)
        {
            GameObject m = Instantiate(model, go.transform, false);
            // Los modelos de armas apuntan su punta hacia +Y: se tumban para que miren hacia donde vuelan.
            m.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (Collider c in m.GetComponentsInChildren<Collider>(true)) Destroy(c);
            p._model = m.transform;
        }
        return p;
    }

    void Update()
    {
        if (_done)
        {
            if (Time.time > _dieAt) Destroy(gameObject);
            return;
        }

        float step = _speed * Time.deltaTime;
        if (_travelled > _maxDistance) _dir = Vector3.Slerp(_dir, Vector3.down, Time.deltaTime * 2f).normalized;

        Vector3 from = transform.position;
        RaycastHit[] hits = Physics.SphereCastAll(from, 0.12f, _dir, step, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        RaycastHit? hit = null;
        foreach (RaycastHit h in hits)
        {
            if (_owner != null && h.transform.IsChildOf(_owner.transform)) continue;
            if (h.collider.GetComponentInParent<WorldItem>() != null) continue;
            if (h.distance < best)
            {
                best = h.distance;
                hit = h;
            }
        }

        if (hit.HasValue)
        {
            RaycastHit h = hit.Value;
            Vector3 point = h.distance > 0f ? from + _dir * h.distance : from;
            transform.position = point;
            Damageable target = h.collider.GetComponentInParent<Damageable>();
            if (target != null) _onHit?.Invoke(target, point);
            // Se queda clavado un momento (en el objetivo, sigue su movimiento).
            transform.SetParent(h.collider.transform, true);
            _done = true;
            _dieAt = Time.time + (target != null ? 1.2f : 4f);
            return;
        }

        transform.position = from + _dir * step;
        transform.rotation = Quaternion.LookRotation(_dir, Vector3.up);
        if (_model != null && _spin > 0f) _model.Rotate(Vector3.forward, _spin * Time.deltaTime, Space.Self);
        _travelled += step;
        if (_travelled > _maxDistance * 2f || transform.position.y < -5f) Destroy(gameObject);
    }
}
