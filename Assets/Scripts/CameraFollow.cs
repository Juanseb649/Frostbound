using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    [Tooltip("Desplazamiento fijo del mundo (cámara isométrica estilo Diablo).")]
    public Vector3 offset = new Vector3(0f, 9f, -8f);
    public float smoothTime = 0.15f;
    public float lookHeight = 1f;

    private Vector3 _velocity;
    private static float _shake, _shakeUntil, _shakeDuration;

    // Sacudida de cámara (golpes pesados). Se queda con la más fuerte si llegan varias.
    public static void Shake(float amplitude, float seconds)
    {
        float remaining = Mathf.Max(0f, _shakeUntil - Time.unscaledTime) / Mathf.Max(0.01f, _shakeDuration) * _shake;
        if (amplitude < remaining) return;
        _shake = amplitude;
        _shakeDuration = Mathf.Max(0.05f, seconds);
        _shakeUntil = Time.unscaledTime + _shakeDuration;
    }

    private float _zoom = 1f;

    // En co-op la cámara encuadra a los dos héroes vivos (su punto medio) y se aleja un poco si se separan.
    private Vector3 Focus(out float spread)
    {
        spread = 0f;
        Vector3 focus = target.position;
        if (Players.Count < 2) return focus;
        Vector3 sum = Vector3.zero;
        int n = 0;
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (PlayerController p in Players.All)
        {
            if (!Players.IsAlive(p)) continue;
            Vector3 q = p.transform.position;
            sum += q;
            n++;
            min = Vector3.Min(min, q);
            max = Vector3.Max(max, q);
        }
        if (n == 0) return focus;
        if (n > 1) spread = new Vector2(max.x - min.x, max.z - min.z).magnitude;
        return sum / n;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 focus = Focus(out float spread);
        _zoom = Mathf.Lerp(_zoom, 1f + Mathf.Clamp01((spread - 6f) / 16f) * 0.55f, Time.deltaTime * 3f);
        Vector3 desired = focus + offset * _zoom;
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        transform.LookAt(focus + Vector3.up * lookHeight);
        float left = _shakeUntil - Time.unscaledTime;
        if (left > 0f)
        {
            float k = _shake * (left / _shakeDuration);
            float t = Time.unscaledTime * 38f;
            Vector3 jitter = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * 2f * k;
            transform.position += transform.rotation * jitter;
        }
    }
}