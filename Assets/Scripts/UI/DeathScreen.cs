using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Pantalla de muerte al estilo Dark Souls 3: franja negra, "HAS MUERTO" en rojo que crece despacio
// y, después, la elección entre reaparecer o volver a la pantalla de título.
public class DeathScreen : MonoBehaviour
{
    private static readonly Color Blood = new Color32(0x9E, 0x1B, 0x1B, 0xFF);
    private static readonly Color Parchment = new Color32(0xD9, 0xCF, 0xBC, 0xFF);
    private static readonly Color ParchmentDim = new Color32(0x8C, 0x84, 0x76, 0xFF);
    private static readonly Color Ember = new Color32(0xC9, 0xA2, 0x5F, 0xFF);

    private CanvasGroup _root, _veil, _band, _options;
    private TextMeshProUGUI _title;
    private Option[] _items;
    private Action _onRespawn, _onTitle;
    private bool _chosen;

    private class Option
    {
        public Button button;
        public TextMeshProUGUI label;
        public Image highlight;
    }

    public static DeathScreen Show(UISkin skin, Action onRespawn, Action onTitle)
    {
        var go = new GameObject("DeathScreen", typeof(RectTransform));
        var screen = go.AddComponent<DeathScreen>();
        screen._onRespawn = onRespawn;
        screen._onTitle = onTitle;
        screen.Build(skin);
        screen.StartCoroutine(screen.Play());
        return screen;
    }

    private void Build(UISkin skin)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(FrostboundUI.ReferenceWidth, FrostboundUI.ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        _root = gameObject.AddComponent<CanvasGroup>();

        RectTransform rt = (RectTransform)transform;

        Image veil = UIFactory.Img(rt, "Velo", null, new Color(0.02f, 0.02f, 0.03f, 0.72f));
        UIFactory.Stretch(veil.rectTransform);
        _veil = veil.gameObject.AddComponent<CanvasGroup>();

        Image band = UIFactory.Img(rt, "Franja", VerticalFade(), Color.black);
        band.preserveAspect = false;
        band.type = Image.Type.Simple;
        UIFactory.Anchored(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(4000f, 260f));
        _band = band.gameObject.AddComponent<CanvasGroup>();

        TMP_FontAsset display = skin != null ? skin.cinzel600 : null;
        _title = UIFactory.Text(band.transform, "Titulo", "HAS MUERTO", display, 112f, Blood, TextAlignmentOptions.Center, 10f);
        UIFactory.Stretch(_title.rectTransform);

        Image optionsBand = UIFactory.Img(rt, "FranjaOpciones", VerticalFade(), new Color(0f, 0f, 0f, 0.85f));
        optionsBand.preserveAspect = false;
        optionsBand.type = Image.Type.Simple;
        UIFactory.Anchored(optionsBand.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), new Vector2(4000f, 190f));
        RectTransform options = UIFactory.Node(optionsBand.rectTransform, "Opciones");
        UIFactory.Anchored(options, new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(560f, 120f));
        _options = optionsBand.gameObject.AddComponent<CanvasGroup>();

        TMP_FontAsset body = skin != null ? skin.cinzel600 : null;
        _items = new[]
        {
            MakeOption(options, "Reaparecer junto a la hoguera", body, 0, () => Choose(_onRespawn)),
            MakeOption(options, "Volver a la pantalla de título", body, 1, () => Choose(_onTitle))
        };
        for (int i = 0; i < _items.Length; i++)
        {
            var nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = _items[(i + _items.Length - 1) % _items.Length].button,
                selectOnDown = _items[(i + 1) % _items.Length].button
            };
            _items[i].button.navigation = nav;
        }

        _veil.alpha = 0f;
        _band.alpha = 0f;
        _options.alpha = 0f;
        _options.interactable = false;
        _options.blocksRaycasts = false;
    }

    private Option MakeOption(RectTransform parent, string text, TMP_FontAsset font, int index, Action onClick)
    {
        RectTransform row = UIFactory.Node(parent, "Opcion_" + index);
        UIFactory.Anchored(row, new Vector2(0.5f, 1f), new Vector2(0f, -index * 56f), new Vector2(560f, 48f));

        Image hit = UIFactory.Img(row, "Zona", null, new Color(0f, 0f, 0f, 0f));
        UIFactory.Stretch(hit.rectTransform);
        Button b = UIFactory.MakeButton(hit);
        b.onClick.AddListener(() => onClick());

        Image glow = UIFactory.Img(row, "Resalte", HorizontalFade(), new Color(Ember.r, Ember.g, Ember.b, 0.28f));
        glow.preserveAspect = false;
        glow.type = Image.Type.Simple;
        UIFactory.Stretch(glow.rectTransform);

        TextMeshProUGUI label = UIFactory.Text(row, "Texto", text, font, 24f, ParchmentDim, TextAlignmentOptions.Center, 2f);
        UIFactory.Stretch(label.rectTransform);

        var hover = hit.gameObject.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(b.gameObject); });
        hover.triggers.Add(enter);

        return new Option { button = b, label = label, highlight = glow };
    }

    private IEnumerator Play()
    {
        float t = 0f;
        while (t < 4.5f)
        {
            t += Time.unscaledDeltaTime;
            _veil.alpha = Mathf.SmoothStep(0f, 1f, t / 1.2f);
            _band.alpha = Mathf.SmoothStep(0f, 1f, (t - 0.3f) / 1.4f);
            float s = Mathf.Lerp(0.94f, 1.03f, Mathf.SmoothStep(0f, 1f, t / 4.5f));
            _title.rectTransform.localScale = new Vector3(s, s, 1f);
            if (t > 2.4f && !_options.interactable)
            {
                _options.interactable = true;
                _options.blocksRaycasts = true;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_items[0].button.gameObject);
            }
            if (_options.interactable) _options.alpha = Mathf.Clamp01((t - 2.4f) / 0.8f);
            yield return null;
        }
        _options.alpha = 1f;
    }

    void Update()
    {
        if (_items == null) return;
        GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (_options.interactable && sel == null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_items[0].button.gameObject);
        foreach (Option o in _items)
        {
            bool on = sel == o.button.gameObject;
            float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            Color hl = o.highlight.color;
            hl.a = Mathf.Lerp(hl.a, on ? 0.28f : 0f, k);
            o.highlight.color = hl;
            o.label.color = Color.Lerp(o.label.color, on ? Parchment : ParchmentDim, k);
        }
    }

    private void Choose(Action action)
    {
        if (_chosen) return;
        _chosen = true;
        _options.interactable = false;
        StartCoroutine(FadeOutThen(action));
    }

    private IEnumerator FadeOutThen(Action action)
    {
        Image black = UIFactory.Img(transform, "Negro", null, Color.black);
        UIFactory.Stretch(black.rectTransform);
        var g = black.gameObject.AddComponent<CanvasGroup>();
        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.SmoothStep(0f, 1f, t / 0.8f);
            yield return null;
        }
        action?.Invoke();
    }

    public IEnumerator FadeAwayAndDestroy()
    {
        float t = 0f;
        while (t < 0.9f)
        {
            t += Time.unscaledDeltaTime;
            _root.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / 0.9f);
            yield return null;
        }
        Destroy(gameObject);
    }

    private static Sprite _vertical, _horizontal;

    public static Sprite VerticalFade()
    {
        if (_vertical != null) return _vertical;
        var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
        {
            float v = y / 63f;
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Min(v, 1f - v) / 0.32f) * 0.94f;
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        _vertical = Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
        return _vertical;
    }

    private static Sprite HorizontalFade()
    {
        if (_horizontal != null) return _horizontal;
        var tex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < 64; x++)
        {
            float u = x / 63f;
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f - u) / 0.45f);
            tex.SetPixel(x, 0, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        _horizontal = Sprite.Create(tex, new Rect(0, 0, 64, 1), new Vector2(0.5f, 0.5f));
        return _horizontal;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _vertical = null;
        _horizontal = null;
    }
}
