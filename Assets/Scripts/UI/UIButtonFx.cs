using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Hover y foco: el borde pasa a hielo y el botón crece a 1.03 en 0,1 s (ratón, teclado y mando).
public class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public Graphic border;
    public Color normalBorder = new Color(0.165f, 0.239f, 0.361f);
    public Color activeBorder = new Color(0.749f, 0.918f, 1f);
    [Tooltip("Mantiene el estado activo (por ejemplo, la tarjeta de la clase elegida).")]
    public bool forceActive;
    public float hoverScale = FrostboundUI.HoverScale;

    private bool _hover;
    private bool _selected;
    private Selectable _selectable;

    void Awake()
    {
        _selectable = GetComponent<Selectable>();
    }

    void OnDisable()
    {
        _hover = false;
        _selected = false;
        transform.localScale = Vector3.one;
    }

    public void OnPointerEnter(PointerEventData e) => _hover = true;
    public void OnPointerExit(PointerEventData e) => _hover = false;
    public void OnSelect(BaseEventData e) => _selected = true;
    public void OnDeselect(BaseEventData e) => _selected = false;

    void Update()
    {
        bool interactable = _selectable == null || _selectable.IsInteractable();
        bool emphasis = interactable && (_hover || _selected);
        float speed = Time.unscaledDeltaTime / Mathf.Max(0.01f, FrostboundUI.HoverDuration);
        float targetScale = emphasis ? hoverScale : 1f;
        float s = Mathf.MoveTowards(transform.localScale.x, targetScale, speed * (hoverScale - 1f));
        transform.localScale = new Vector3(s, s, 1f);
        if (border != null)
        {
            Color target = emphasis || forceActive ? activeBorder : normalBorder;
            border.color = Color.Lerp(border.color, target, Mathf.Clamp01(speed));
        }
    }
}
