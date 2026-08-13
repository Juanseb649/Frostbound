using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MenuButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerClickHandler
{
    [Header("Visual Effects")]
    [Tooltip("Glow aura image shown on hover/select.")]
    public Image glowOverlay;
    [Tooltip("Scale factor when hovered/selected.")]
    public float hoverScale = 1.05f;
    [Tooltip("Smooth animation speed.")]
    public float animSpeed = 10f;

    private Vector3 _originalScale;
    private Vector3 _targetScale;
    private bool _isHoveredOrSelected;

    void Awake()
    {
        _originalScale = transform.localScale;
        _targetScale = _originalScale;
        if (glowOverlay != null)
        {
            Color c = glowOverlay.color;
            c.a = 0f;
            glowOverlay.color = c;
        }
    }

    void Update()
    {
        // Smooth scale animation
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * animSpeed);

        // Smooth glow fade
        if (glowOverlay != null)
        {
            float targetAlpha = _isHoveredOrSelected ? 0.85f : 0f;
            Color c = glowOverlay.color;
            c.a = Mathf.MoveTowards(c.a, targetAlpha, Time.deltaTime * 3f);
            glowOverlay.color = c;
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => SetHighlight(true);
    public void OnPointerExit(PointerEventData eventData) => SetHighlight(false);
    public void OnSelect(BaseEventData eventData) => SetHighlight(true);
    public void OnDeselect(BaseEventData eventData) => SetHighlight(false);

    public void OnPointerClick(PointerEventData eventData)
    {
        // Subtle pulse punch on click
        transform.localScale = _originalScale * 0.95f;
    }

    private void SetHighlight(bool active)
    {
        _isHoveredOrSelected = active;
        _targetScale = active ? _originalScale * hoverScale : _originalScale;
    }

    void OnDisable()
    {
        SetHighlight(false);
        transform.localScale = _originalScale;
    }
}
