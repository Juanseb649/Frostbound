using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// HUD del mundo: nombres bajo los pingüinos, globos de diálogo, menú de interacción con NPC y avisos.
[DefaultExecutionOrder(300)]
public class WorldHUD : MonoBehaviour
{
    public static WorldHUD Instance { get; private set; }
    private static readonly List<NameTag> Tags = new List<NameTag>();

    [Header("Fuentes y sprites")]
    public TMP_FontAsset nameFont;
    public TMP_FontAsset textFont;
    public TMP_FontAsset subFont;
    public Sprite roundSprite;
    public Sprite bubbleSprite;
    public Sprite tailSprite;
    public Sprite pillSprite;

    [Header("Nombres")]
    public Color nameColor = new Color(0.07f, 0.13f, 0.24f);
    public Color highlightColor = new Color(0.12f, 0.31f, 0.71f);
    public Color roleColor = new Color(0.47f, 0.29f, 0.02f);
    public float nameSize = 17f;
    [Tooltip("Distancia máxima a la cámara para mostrar nombres.")]
    public float maxDistance = 32f;

    public static void Register(NameTag t)
    {
        if (!Tags.Contains(t)) Tags.Add(t);
    }

    public static void Unregister(NameTag t) => Tags.Remove(t);

    private static readonly List<WorldItem> Items = new List<WorldItem>();

    public static void RegisterItem(WorldItem w)
    {
        if (!Items.Contains(w)) Items.Add(w);
    }

    public static void UnregisterItem(WorldItem w) => Items.Remove(w);

    public static bool PointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    public bool MenuOpen => _menuNpc != null;
    public NPCInteractable MenuTarget => _menuNpc;

    private class Label
    {
        public RectTransform rt;
        public TextMeshProUGUI name;
        public TextMeshProUGUI sub;
        public int version = -1;
    }

    private readonly Dictionary<NameTag, Label> _labels = new Dictionary<NameTag, Label>();
    private readonly List<NameTag> _scratch = new List<NameTag>();
    private RectTransform _canvasRt, _namesLayer, _itemLayer, _numbersLayer, _bubbleLayer, _menuLayer;

    private class PopupEntry
    {
        public RectTransform rt;
        public TextMeshProUGUI text;
        public Vector3 world;
        public float born, scale;
        public bool alive;
    }
    private readonly List<PopupEntry> _popups = new List<PopupEntry>();

    private class ItemLabel
    {
        public RectTransform rt;
        public Image bg;
        public TextMeshProUGUI text;
        public string shown;
    }
    private readonly Dictionary<WorldItem, ItemLabel> _itemLabels = new Dictionary<WorldItem, ItemLabel>();
    private readonly List<WorldItem> _itemScratch = new List<WorldItem>();
    private Camera _cam;

    private RectTransform _bubble;
    private TextMeshProUGUI _bubbleText;
    private Transform _bubbleTarget;
    private float _bubbleHeight, _bubbleUntil, _bubbleStart;

    private RectTransform _menu;
    private TextMeshProUGUI _menuName, _menuRole;
    private RectTransform _menuButtons;
    private NPCInteractable _menuNpc;
    private float _menuOpenedAt;
    private readonly List<System.Action> _menuActions = new List<System.Action>();


    void Awake()
    {
        Instance = this;
        BuildCanvas();
        BuildBubble();
        BuildMenu();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- Construcción ----------

    private void BuildCanvas()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(FrostboundUI.ReferenceWidth, FrostboundUI.ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        _canvasRt = (RectTransform)transform;

        _namesLayer = Layer("Nombres");
        _itemLayer = Layer("Objetos");
        _numbersLayer = Layer("Numeros");
        _bubbleLayer = Layer("Globos");
        _menuLayer = Layer("Menu");
    }

    private RectTransform Layer(string layerName)
    {
        RectTransform rt = Rect(layerName, _canvasRt);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static RectTransform Rect(string objName, Transform parent)
    {
        var go = new GameObject(objName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private TextMeshProUGUI Text(Transform parent, string objName, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = Rect(objName, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    private Image Img(Transform parent, string objName, Sprite sprite, Color color)
    {
        RectTransform rt = Rect(objName, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private void BuildBubble()
    {
        _bubble = Rect("Globo", _bubbleLayer);
        _bubble.pivot = new Vector2(0.5f, 0f);
        _bubble.anchorMin = _bubble.anchorMax = new Vector2(0.5f, 0.5f);

        Image tail = Img(_bubble, "Cola", tailSprite, Color.white);
        tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        tail.rectTransform.sizeDelta = new Vector2(22f, 22f);
        tail.rectTransform.anchoredPosition = new Vector2(0f, 2f);
        tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        if (tailSprite == null) tail.rectTransform.sizeDelta = new Vector2(16f, 16f);

        Image outline = Img(_bubble, "Borde", bubbleSprite, new Color(0.12f, 0.16f, 0.24f));
        Stretch(outline.rectTransform, 0f);
        Image fill = Img(_bubble, "Fondo", bubbleSprite, Color.white);
        Stretch(fill.rectTransform, 2f);

        _bubbleText = Text(_bubble, "Texto", textFont, 15f, new Color(0.07f, 0.1f, 0.16f), TextAlignmentOptions.Center);
        _bubbleText.textWrappingMode = TextWrappingModes.Normal;
        Stretch(_bubbleText.rectTransform, 12f);
        _bubble.gameObject.SetActive(false);
    }

    private void BuildMenu()
    {
        _menu = Rect("MenuNPC", _menuLayer);
        _menu.anchorMin = _menu.anchorMax = new Vector2(0.5f, 0.5f);
        _menu.pivot = new Vector2(0f, 0.5f);
        _menu.sizeDelta = new Vector2(240f, 100f);

        Image border = Img(_menu, "Borde", roundSprite, FrostboundUI.Border);
        Stretch(border.rectTransform, 0f);
        Image fill = Img(_menu, "Fondo", roundSprite, new Color(FrostboundUI.Bg.r, FrostboundUI.Bg.g, FrostboundUI.Bg.b, 0.96f));
        Stretch(fill.rectTransform, 1.5f);
        fill.raycastTarget = true;

        RectTransform content = Rect("Contenido", _menu);
        Stretch(content, 0f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 14);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        _menuName = Text(content, "Nombre", nameFont, 20f, FrostboundUI.Title, TextAlignmentOptions.Left);
        _menuName.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
        _menuRole = Text(content, "Oficio", subFont, 13f, FrostboundUI.Gold, TextAlignmentOptions.Left);
        _menuRole.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;
        _menuRole.characterSpacing = FrostboundUI.Tracking(1f, 13f);

        Image divider = Img(content, "Separador", null, FrostboundUI.Border);
        divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 1f;

        _menuButtons = Rect("Opciones", content);
        var bl = _menuButtons.gameObject.AddComponent<VerticalLayoutGroup>();
        bl.spacing = 6f;
        bl.childControlWidth = true;
        bl.childControlHeight = true;
        bl.childForceExpandWidth = true;
        bl.childForceExpandHeight = false;
        bl.padding = new RectOffset(0, 0, 4, 0);

        _menu.gameObject.SetActive(false);
    }

    // ---------- API ----------

    public void Say(Transform speaker, float height, string text, float seconds)
    {
        _bubbleTarget = speaker;
        _bubbleHeight = height;
        _bubbleUntil = Time.time + seconds;
        _bubbleStart = Time.time;
        _bubbleText.text = text;
        const float maxWidth = 300f;
        Vector2 pref = _bubbleText.GetPreferredValues(text, maxWidth, 1000f);
        float w = Mathf.Min(pref.x, maxWidth);
        Vector2 pref2 = _bubbleText.GetPreferredValues(text, w, 1000f);
        _bubble.sizeDelta = new Vector2(w + 28f, pref2.y + 24f);
        _bubble.gameObject.SetActive(true);
        UpdateBubble();
    }

    public void OpenMenu(NPCInteractable npc)
    {
        if (npc == null) return;
        CloseMenu();
        _menuNpc = npc;
        npc.InConversation = true;
        _menuOpenedAt = Time.time;
        _menuName.text = npc.displayName;
        _menuRole.text = string.IsNullOrEmpty(npc.role) ? "" : npc.role.ToUpperInvariant();
        _menuRole.gameObject.SetActive(!string.IsNullOrEmpty(npc.role));

        for (int i = _menuButtons.childCount - 1; i >= 0; i--) DestroyImmediate(_menuButtons.GetChild(i).gameObject);
        _menuActions.Clear();

        GameObject first = null;
        int index = 1;
        List<NPCOption> available = npc.AvailableOptions();
        foreach (NPCOption option in available)
        {
            NPCOption o = option;
            GameObject b = MenuButton(index, NPCInteractable.Label(o), () => npc.Choose(o));
            if (first == null) first = b;
            index++;
        }
        MenuButton(index, "Adiós", CloseMenu);

        int buttons = available.Count + 1;
        bool hasRole = !string.IsNullOrEmpty(npc.role);
        float height = 12f + 26f + 6f + (hasRole ? 18f + 6f : 0f) + 1f + 6f + 4f + buttons * 38f + (buttons - 1) * 6f + 14f;
        _menu.sizeDelta = new Vector2(240f, height);
        _menu.gameObject.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_menu);
        UpdateMenu();
        if (EventSystem.current != null && first != null) EventSystem.current.SetSelectedGameObject(first);
    }

    public void CloseMenu()
    {
        if (_menuNpc != null) _menuNpc.InConversation = false;
        _menuNpc = null;
        if (_menu != null) _menu.gameObject.SetActive(false);
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
            EventSystem.current.SetSelectedGameObject(null);
    }

    private GameObject MenuButton(int number, string label, System.Action action)
    {
        Image border = Img(_menuButtons, "Opcion_" + label, roundSprite, FrostboundUI.Border);
        border.raycastTarget = true;
        border.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
        Image fill = Img(border.transform, "Fondo", roundSprite, FrostboundUI.Surface);
        Stretch(fill.rectTransform, 1.5f);

        TextMeshProUGUI text = Text(border.transform, "Texto", textFont, 16f, FrostboundUI.Text, TextAlignmentOptions.Left);
        Stretch(text.rectTransform, 0f);
        text.rectTransform.offsetMin = new Vector2(14f, 0f);
        text.text = label;

        TextMeshProUGUI key = Text(border.transform, "Tecla", subFont, 13f, FrostboundUI.Disabled, TextAlignmentOptions.Right);
        Stretch(key.rectTransform, 0f);
        key.rectTransform.offsetMax = new Vector2(-14f, 0f);
        key.text = number.ToString();

        var button = border.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = border;
        button.onClick.AddListener(() => action());
        var fx = border.gameObject.AddComponent<UIButtonFx>();
        fx.border = border;
        fx.normalBorder = FrostboundUI.Border;
        fx.activeBorder = FrostboundUI.Ice;
        _menuActions.Add(action);
        return border.gameObject;
    }

    // ---------- Ciclo ----------

    void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        UpdateNames();
        UpdateItemLabels();
        UpdatePopups();
        UpdateBubble();
        UpdateMenu();
        UpdateMenuKeys();
    }

    private bool ToCanvas(Vector3 world, out Vector2 local)
    {
        Vector3 sp = _cam.WorldToScreenPoint(world);
        local = Vector2.zero;
        if (sp.z <= 0f) return false;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, sp, null, out local);
        return true;
    }

    private void UpdateNames()
    {
        _scratch.Clear();
        foreach (var pair in _labels)
            if (pair.Key == null || !Tags.Contains(pair.Key)) _scratch.Add(pair.Key);
        foreach (NameTag dead in _scratch)
        {
            if (_labels[dead].rt != null) Destroy(_labels[dead].rt.gameObject);
            _labels.Remove(dead);
        }

        foreach (NameTag tag in Tags)
        {
            if (tag == null) continue;
            if (!_labels.TryGetValue(tag, out Label label))
            {
                label = CreateLabel(tag);
                _labels[tag] = label;
            }
            if (label.version != tag.Version)
            {
                label.version = tag.Version;
                label.name.text = tag.displayName;
                label.sub.text = tag.subtitle;
                label.sub.gameObject.SetActive(!string.IsNullOrEmpty(tag.subtitle));
            }

            Vector3 anchor = tag.Anchor;
            bool visible = (anchor - _cam.transform.position).sqrMagnitude < maxDistance * maxDistance
                           && ToCanvas(anchor, out Vector2 local);
            if (label.rt.gameObject.activeSelf != visible) label.rt.gameObject.SetActive(visible);
            if (!visible) continue;
            ToCanvas(anchor, out Vector2 p);
            label.rt.anchoredPosition = p + new Vector2(0f, -tag.screenOffset);
            float s = tag.Highlighted ? 1.12f : 1f;
            label.rt.localScale = Vector3.Lerp(label.rt.localScale, new Vector3(s, s, 1f), Time.deltaTime * 14f);
            label.name.color = tag.Highlighted ? highlightColor : nameColor;
        }
    }

    // ----- Números de daño -----

    public void Popup(Vector3 world, string text, Color color, float scale = 1f)
    {
        PopupEntry p = null;
        foreach (PopupEntry q in _popups) if (!q.alive) { p = q; break; }
        if (p == null)
        {
            p = new PopupEntry { rt = Rect("Numero", _numbersLayer) };
            p.rt.anchorMin = p.rt.anchorMax = new Vector2(0.5f, 0.5f);
            p.rt.sizeDelta = new Vector2(220f, 40f);
            p.text = Text(p.rt, "Texto", nameFont, 28f, Color.white, TextAlignmentOptions.Center);
            Stretch(p.text.rectTransform, 0f);
            p.text.outlineWidth = 0.25f;
            p.text.outlineColor = new Color32(10, 18, 34, 255);
            _popups.Add(p);
        }
        p.alive = true;
        p.world = world;
        p.born = Time.time;
        p.scale = scale;
        p.text.text = text;
        p.text.color = color;
        p.rt.gameObject.SetActive(true);
        p.rt.SetAsLastSibling();
    }

    private void UpdatePopups()
    {
        const float life = 0.9f;
        foreach (PopupEntry p in _popups)
        {
            if (!p.alive) continue;
            float t = (Time.time - p.born) / life;
            if (t >= 1f || !ToCanvas(p.world, out Vector2 pos))
            {
                p.alive = t < 1f;
                p.rt.gameObject.SetActive(false);
                continue;
            }
            if (!p.rt.gameObject.activeSelf) p.rt.gameObject.SetActive(true);
            p.rt.anchoredPosition = pos + new Vector2(0f, 60f * t);
            float pop = t < 0.12f ? Mathf.Lerp(1.5f, 1f, t / 0.12f) : 1f;
            p.rt.localScale = Vector3.one * p.scale * pop;
            Color c = p.text.color;
            c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            p.text.color = c;
        }
    }

    // ----- Etiquetas de objetos en el suelo (clic para recoger) -----

    private void UpdateItemLabels()
    {
        _itemScratch.Clear();
        foreach (var pair in _itemLabels)
            if (pair.Key == null || !Items.Contains(pair.Key)) _itemScratch.Add(pair.Key);
        foreach (WorldItem dead in _itemScratch)
        {
            if (_itemLabels[dead].rt != null) Destroy(_itemLabels[dead].rt.gameObject);
            _itemLabels.Remove(dead);
        }

        foreach (WorldItem item in Items)
        {
            if (item == null) continue;
            if (!_itemLabels.TryGetValue(item, out ItemLabel label))
            {
                label = CreateItemLabel(item);
                _itemLabels[item] = label;
            }
            string text = item.Label;
            if (label.shown != text)
            {
                label.shown = text;
                label.text.text = text;
                label.text.color = item.LabelColor;
                Vector2 pref = label.text.GetPreferredValues(text);
                label.rt.sizeDelta = new Vector2(pref.x + 22f, 26f);
            }
            Vector3 anchor = item.transform.position + Vector3.up * 0.35f;
            bool visible = (anchor - _cam.transform.position).sqrMagnitude < maxDistance * maxDistance && ToCanvas(anchor, out _);
            if (label.rt.gameObject.activeSelf != visible) label.rt.gameObject.SetActive(visible);
            if (!visible) continue;
            ToCanvas(anchor, out Vector2 p);
            label.rt.anchoredPosition = p + new Vector2(0f, 16f);
            label.rt.localScale = item.Highlighted ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
            label.bg.color = item.Highlighted ? new Color(0.11f, 0.17f, 0.29f, 0.95f) : new Color(0.04f, 0.07f, 0.13f, 0.82f);
        }
    }

    private ItemLabel CreateItemLabel(WorldItem item)
    {
        var label = new ItemLabel { rt = Rect("Objeto_" + item.name, _itemLayer) };
        label.rt.anchorMin = label.rt.anchorMax = new Vector2(0.5f, 0.5f);
        label.rt.pivot = new Vector2(0.5f, 0f);
        label.bg = label.rt.gameObject.AddComponent<Image>();
        label.bg.sprite = pillSprite != null ? pillSprite : roundSprite;
        label.bg.type = Image.Type.Sliced;
        label.bg.raycastTarget = true;
        label.text = Text(label.rt, "Texto", textFont, 14f, Color.white, TextAlignmentOptions.Center);
        Stretch(label.text.rectTransform, 0f);
        var button = label.rt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        WorldItem target = item;
        button.onClick.AddListener(() =>
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null && target != null) pc.PickUp(target);
        });
        var trigger = label.rt.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => { if (target != null) target.Highlighted = true; });
        var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => { if (target != null) target.Highlighted = false; });
        trigger.triggers.Add(enter);
        trigger.triggers.Add(exit);
        return label;
    }

    private Label CreateLabel(NameTag tag)
    {
        var label = new Label { rt = Rect("Nombre_" + tag.displayName, _namesLayer) };
        label.rt.anchorMin = label.rt.anchorMax = new Vector2(0.5f, 0.5f);
        label.rt.pivot = new Vector2(0.5f, 1f);
        label.rt.sizeDelta = new Vector2(280f, 44f);

        label.name = Text(label.rt, "Nombre", nameFont, nameSize, nameColor, TextAlignmentOptions.Top);
        label.name.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.name.rectTransform.anchorMax = new Vector2(1f, 1f);
        label.name.rectTransform.pivot = new Vector2(0.5f, 1f);
        label.name.rectTransform.sizeDelta = new Vector2(0f, 24f);
        label.name.outlineWidth = 0.22f;
        label.name.outlineColor = new Color32(255, 255, 255, 230);

        label.sub = Text(label.rt, "Oficio", textFont, 13.5f, roleColor, TextAlignmentOptions.Top);
        label.sub.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.sub.rectTransform.anchorMax = new Vector2(1f, 1f);
        label.sub.rectTransform.pivot = new Vector2(0.5f, 1f);
        label.sub.rectTransform.sizeDelta = new Vector2(0f, 18f);
        label.sub.rectTransform.anchoredPosition = new Vector2(0f, -21f);
        label.sub.outlineWidth = 0.2f;
        label.sub.outlineColor = new Color32(255, 255, 255, 210);
        return label;
    }

    private void UpdateBubble()
    {
        if (_bubble == null || !_bubble.gameObject.activeSelf) return;
        if (_bubbleTarget == null || Time.time > _bubbleUntil)
        {
            _bubble.gameObject.SetActive(false);
            return;
        }
        if (_cam == null) _cam = Camera.main;
        if (_cam == null || !ToCanvas(_bubbleTarget.position + Vector3.up * _bubbleHeight, out Vector2 p))
        {
            _bubble.localScale = Vector3.zero;
            return;
        }
        _bubble.anchoredPosition = p + new Vector2(0f, 16f);
        float pop = Mathf.Clamp01((Time.time - _bubbleStart) / 0.15f);
        float s = Mathf.Lerp(0.6f, 1f, 1f - (1f - pop) * (1f - pop));
        _bubble.localScale = new Vector3(s, s, 1f);
    }

    private void UpdateMenu()
    {
        if (_menuNpc == null) return;
        if (_cam == null || !ToCanvas(_menuNpc.transform.position + Vector3.up * _menuNpc.headHeight * 0.8f, out Vector2 p)) return;
        Vector2 half = _canvasRt.rect.size * 0.5f;
        Vector2 size = _menu.sizeDelta;
        Vector2 pos = p + new Vector2(56f, 0f);
        pos.x = Mathf.Clamp(pos.x, -half.x + 16f, half.x - size.x - 16f);
        pos.y = Mathf.Clamp(pos.y, -half.y + size.y * 0.5f + 16f, half.y - size.y * 0.5f - 16f);
        _menu.anchoredPosition = pos;
        float pop = Mathf.Clamp01((Time.time - _menuOpenedAt) / 0.12f);
        float s = Mathf.Lerp(0.92f, 1f, pop);
        _menu.localScale = new Vector3(s, s, 1f);
    }

    private void UpdateMenuKeys()
    {
        if (_menuNpc == null) return;
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
        {
            CloseMenu();
            return;
        }
        if (kb == null) return;
        for (int i = 0; i < _menuActions.Count && i < 9; i++)
        {
            var key = kb[(Key)((int)Key.Digit1 + i)];
            if (key.wasPressedThisFrame)
            {
                _menuActions[i]();
                return;
            }
        }
    }
}
