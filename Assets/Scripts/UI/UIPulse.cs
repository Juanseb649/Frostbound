using UnityEngine;

// Latido suave (corazón del Frost en el fondo del menú).
public class UIPulse : MonoBehaviour
{
    public float minScale = 1f;
    public float maxScale = 1.06f;
    public float period = 1.2f;

    void Update()
    {
        float t = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.1f, period));
        float s = Mathf.Lerp(minScale, maxScale, t);
        transform.localScale = new Vector3(s, s, 1f);
    }
}
