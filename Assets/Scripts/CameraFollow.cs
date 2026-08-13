using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    [Tooltip("Desplazamiento fijo del mundo (cámara isométrica estilo Diablo).")]
    public Vector3 offset = new Vector3(0f, 9f, -8f);
    public float smoothTime = 0.15f;
    public float lookHeight = 1f;

    private Vector3 _velocity;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }
}