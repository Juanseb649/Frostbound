using System;
using UnityEngine;

// Aviso en el suelo para los ataques de jefe: un círculo que se llena durante unos segundos y luego descarga
// (daña a los héroes que sigan dentro, salvo si ruedan) y llama a onDone (charco, raíces, cristales...).
public class GroundWarning : MonoBehaviour
{
    private float _radius, _seconds, _damage, _born;
    private Color _color;
    private Action<Vector3> _onDone;
    private Renderer _r;
    private Transform _edge;
    private MaterialPropertyBlock _mpb;
    private static Material _mat;
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    public static GroundWarning Spawn(Vector3 at, float radius, float seconds, float damage, Color color, Action<Vector3> onDone = null)
    {
        if (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(go.GetComponent<Collider>());
        go.name = "Aviso_Suelo";
        go.transform.position = at + Vector3.up * 0.06f;
        if (_mat == null) _mat = MenuRuins.Transparent("Aviso de jefe", MenuRuins.Soft(32), Color.white, false);
        var w = go.AddComponent<GroundWarning>();
        w._r = go.GetComponent<Renderer>();
        w._r.sharedMaterial = _mat;
        w._r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        w._mpb = new MaterialPropertyBlock();
        w._radius = radius;
        w._seconds = seconds;
        w._damage = damage;
        w._color = color;
        w._onDone = onDone;
        w._born = Time.time;
        w.Paint(0f);
        // Borde tenue del tamaño final: se ve desde el principio hasta dónde llegará.
        var edge = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(edge.GetComponent<Collider>());
        edge.name = "Aviso_Borde";
        edge.transform.SetParent(go.transform, false);
        edge.transform.position = at + Vector3.up * 0.05f;
        Renderer er = edge.GetComponent<Renderer>();
        er.sharedMaterial = _mat;
        er.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var eb = new MaterialPropertyBlock();
        eb.SetColor(ColorId, new Color(color.r, color.g, color.b, 0.22f));
        er.SetPropertyBlock(eb);
        w._edge = edge.transform;
        return w;
    }

    void Update()
    {
        float k = (Time.time - _born) / Mathf.Max(0.05f, _seconds);
        if (k < 1f)
        {
            Paint(k);
            return;
        }
        foreach (PlayerController pc in Players.All)
        {
            if (pc == null || pc.Stats == null || pc.Stats.IsDead) continue;
            Vector3 d = pc.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > _radius * _radius) continue;
            PlayerDodge dodge = pc.GetComponent<PlayerDodge>();
            if (dodge != null && dodge.IsRolling) continue;
            EnemyBrain.DamagePlayer(pc.Stats, _damage, d);
        }
        _onDone?.Invoke(transform.position);
        Destroy(gameObject);
    }

    private void Paint(float k)
    {
        float s = _radius * 2f * Mathf.Lerp(0.35f, 1f, k);
        transform.localScale = new Vector3(s, 0.01f, s);
        // El borde no se escala con el relleno.
        if (_edge != null) _edge.localScale = new Vector3(_radius * 2f / s, 0.5f, _radius * 2f / s);
        _mpb.SetColor(ColorId, new Color(_color.r, _color.g, _color.b, Mathf.Lerp(0.35f, 0.85f, k)));
        _r.SetPropertyBlock(_mpb);
    }
}
