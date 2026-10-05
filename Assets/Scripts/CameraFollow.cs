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

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        transform.LookAt(target.position + Vector3.up * lookHeight);
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