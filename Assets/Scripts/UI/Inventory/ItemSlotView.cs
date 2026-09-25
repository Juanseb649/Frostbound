using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Casilla de la mochila o del equipo. Clic: seleccionar. Clic sobre la seleccionada, doble clic,
// clic derecho o Submit del mando sobre la seleccionada: acción (equipar, usar, quitar).
public class ItemSlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public Image border;
    public Image fill;
    public Image icon;
    public Image placeholder;
    public TextMeshProUGUI quantity;
    public TextMeshProUGUI caption;
    public Button button;

    [NonSerialized] public int index = -1;
    [NonSerialized] public EquipSlot slot = EquipSlot.None;

    public Action<ItemSlotView> Action;
    public Action<ItemSlotView, bool> Hovered;

    public bool IsEquipmentSlot => slot != EquipSlot.None;
    public bool Selected { get; set; }
    public ItemDefinition Item { get; private set; }

    private bool _hover;

    void OnDisable()
    {
        _hover = false;
        transform.localScale = Vector3.one;
    }

    public void Show(ItemDefinition item, int count, bool meetsRequirements)
    {
        Item = item;
        bool has = item != null;
        icon.gameObject.SetActive(has && item.icon != null);
        if (has) icon.sprite = item.icon;
        icon.color = has && !meetsRequirements ? new Color(1f, 0.55f, 0.55f, 0.75f) : Color.white;
        if (placeholder != null) placeholder.gameObject.SetActive(!has);
        if (caption != null) caption.gameObject.SetActive(!has);
        if (quantity != null)
        {
            quantity.gameObject.SetActive(has && count > 1);
            quantity.text = count.ToString();
        }
        Refresh();
    }

    public void Refresh()
    {
        Color b = Item != null ? FrostboundUI.Rarity(Item.rarity) : FrostboundUI.Border;
        if (Item != null && Item.rarity == ItemRarity.Common) b = FrostboundUI.Border;
        if (Selected) b = FrostboundUI.Ice;
        else if (_hover) b = Color.Lerp(b, FrostboundUI.Ice, 0.6f);
        border.color = b;
        fill.color = Selected ? FrostboundUI.SurfaceSelected : (Item != null ? FrostboundUI.Surface : FrostboundUI.Bg);
    }

    void Update()
    {
        float target = _hover || Selected ? FrostboundUI.HoverScale : 1f;
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, FrostboundUI.HoverDuration) * (FrostboundUI.HoverScale - 1f);
        float s = Mathf.MoveTowards(transform.localScale.x, target, step);
        transform.localScale = new Vector3(s, s, 1f);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Right) Action?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData e) => SetHover(true);
    public void OnPointerExit(PointerEventData e) => SetHover(false);
    public void OnSelect(BaseEventData e) => SetHover(true);
    public void OnDeselect(BaseEventData e) => SetHover(false);

    private void SetHover(bool value)
    {
        _hover = value;
        Refresh();
        Hovered?.Invoke(this, value);
    }
}
