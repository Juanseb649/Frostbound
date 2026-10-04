using System.Collections.Generic;
using UnityEngine;

// Zona segura: los enemigos no aparecen dentro ni la persiguen hasta aquí (campamento, claro de Steve).
public class SafeZone : MonoBehaviour
{
    public string zoneName = "Zona segura";
    [Min(1f)] public float radius = 36f;

    private static readonly List<SafeZone> Zones = new List<SafeZone>();

    void OnEnable() => Zones.Add(this);
    void OnDisable() => Zones.Remove(this);

    public static bool Contains(Vector3 point, float margin = 0f)
    {
        foreach (SafeZone z in Zones)
        {
            Vector3 d = point - z.transform.position;
            d.y = 0f;
            if (d.magnitude < z.radius + margin) return true;
        }
        return false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.5f, 1f, 0.6f, 0.6f);
        Vector3 c = transform.position;
        for (int i = 0; i < 64; i++)
        {
            float a = i * Mathf.PI * 2f / 64f, b = (i + 1) * Mathf.PI * 2f / 64f;
            Gizmos.DrawLine(c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius, c + new Vector3(Mathf.Sin(b), 0f, Mathf.Cos(b)) * radius);
        }
    }
}
