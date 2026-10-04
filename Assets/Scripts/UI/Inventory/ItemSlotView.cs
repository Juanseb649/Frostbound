using System;
using System.Collections.Generic;
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
    private Image _durBg, _durFill;
    private readonly List<Image> _pips = new List<Image>();

    // Barra de durabilidad (solo si está gastada) y rombos de las ranuras de runa.
    private ItemStack _stack;

    public void ShowState(ItemStack s)
    {
        _stack = s;
        bool durable = s != null && s.item != null && s.item.HasDurability;
        if (durable || _durBg != null)
        {
            if (_durBg == null)
            {
                _durBg = NewImage("Durabilidad", new Color(0.04f, 0.07f, 0.13f, 0.9f));
                var rt = _durBg.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.offsetMin = new Vector2(8f, 6f);
                rt.offsetMax = new Vector2(-8f, 11f);
                _durFill = NewImage("Relleno", Color.white);
                _durFill.transform.SetParent(_durBg.transform, false);
                var f = _durFill.rectTransform;
                f.anchorMin = Vector2.zero;
                f.pivot = new Vector2(0f, 0.5f);
                f.offsetMin = new Vector2(1f, 1f);
                f.offsetMax = new Vector2(-1f, -1f);
            }
            float d = durable ? s.Durability01 : 1f;
            bool show = durable && d < 0.999f;
            _durBg.gameObject.SetActive(show);
            if (show)
            {
                _durFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, d), 1f);
                _durFill.color = d > 0.5f ? FrostboundUI.Positive : d > 0.2f ? FrostboundUI.Gold : FrostboundUI.Negative;
            }
            if (durable && s.IsBroken) icon.color = new Color(1f, 0.45f, 0.45f, 0.6f);
        }

        int slots = s != null && s.item != null ? s.item.runeSlots : 0;
        while (_pips.Count < slots)
        {
            Image pip = NewImage("Runa", Color.white);
            var rt = pip.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(7f, 7f);
            rt.anchoredPosition = new Vector2(11f + _pips.Count * 10f, -11f);
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _pips.Add(pip);
        }
        for (int i = 0; i < _pips.Count; i++)
        {
            bool on = i < slots;
            _pips[i].gameObject.SetActive(on);
            if (!on) continue;
            ItemDefinition rune = i < s.runes.Count ? s.runes[i] : null;
            _pips[i].color = rune != null ? WeaponCatalog.RuneColor(rune.runeEffect) : new Color(0.42f, 0.51f, 0.61f, 0.7f);
        }
    }

    private Image NewImage(string objName, Color color)
    {
        var go = new GameObject(objName, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

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
        if (Item != null && _stack != null && _stack.item == Item && _stack.quality > LootQuality.Normal) b = _stack.DisplayColor;
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
