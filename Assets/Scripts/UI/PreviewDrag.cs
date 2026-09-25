using UnityEngine;
using UnityEngine.EventSystems;

// Arrastrar sobre la imagen de la preview gira al pingüino; sin arrastre se mece suave (±15° en ~4 s).
public class PreviewDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Transform target;
    public float degreesPerPixel = 0.45f;
    public float idleAmplitude = 15f;
    public float idlePeriod = 4f;
    public float baseYaw = 180f;

    private float _yaw;
    private float _dragOffset;
    private bool _dragging;
    private float _idleTime;

    public void ResetView()
    {
        _dragOffset = 0f;
        _idleTime = 0f;
    }

    public void OnBeginDrag(PointerEventData e) => _dragging = true;
    public void OnDrag(PointerEventData e) => _dragOffset -= e.delta.x * degreesPerPixel;
    public void OnEndDrag(PointerEventData e) => _dragging = false;

    void Update()
    {
        if (target == null) return;
        if (!_dragging) _idleTime += Time.unscaledDeltaTime;
        float idle = Mathf.Sin(_idleTime * Mathf.PI * 2f / idlePeriod) * idleAmplitude;
        _yaw = Mathf.LerpAngle(_yaw, baseYaw + _dragOffset + (_dragging ? 0f : idle), Time.unscaledDeltaTime * 8f);
        target.localRotation = Quaternion.Euler(0f, _yaw, 0f);
    }
}
