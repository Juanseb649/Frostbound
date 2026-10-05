using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Aviso al encender una hoguera, al estilo Dark Souls 3: franja oscura y "HOGUERA ENCENDIDA" en dorado cálido.
public class BonfireBanner : MonoBehaviour
{
    private static readonly Color Ember = new Color32(0xE8, 0xB4, 0x5A, 0xFF);
    private static BonfireBanner _current;

    private CanvasGroup _group;
    private TextMeshProUGUI _title;

    public static readonly Color Victory = new Color32(0xE8, 0xC8, 0x6A, 0xFF);
    public static readonly Color Threat = new Color32(0xB8, 0x2E, 0x2E, 0xFF);

    public static void Show(string text = "HOGUERA ENCENDIDA") => Show(text, Ember);

    public static void Show(string text, Color color)
    {
        if (_current != null) Destroy(_current.gameObject);
        var go = new GameObject("BonfireBanner", typeof(RectTransform));
        _current = go.AddComponent<BonfireBanner>();
        _current.Build(text, color);
        _current.StartCoroutine(_current.Play());
    }

    private void Build(string text, Color color)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(FrostboundUI.ReferenceWidth, FrostboundUI.ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;

        UISkin skin = GameDatabase.Instance != null ? GameDatabase.Instance.uiSkin : null;
        Image band = UIFactory.Img(transform, "Franja", DeathScreen.VerticalFade(), new Color(0f, 0f, 0f, 0.8f));
        band.preserveAspect = false;
        band.type = Image.Type.Simple;
        UIFactory.Anchored(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(4000f, 200f));

        _title = UIFactory.Text(band.transform, "Titulo", text, skin != null ? skin.cinzel600 : null, text.Length > 22 ? 58f : 78f, color, TextAlignmentOptions.Center, 8f);
        UIFactory.Stretch(_title.rectTransform);
        _group.alpha = 0f;
    }

    private IEnumerator Play()
    {
        const float fadeIn = 0.5f, hold = 1.8f, fadeOut = 1.1f, total = fadeIn + hold + fadeOut;
        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = t < fadeIn ? Mathf.SmoothStep(0f, 1f, t / fadeIn)
                : t < fadeIn + hold ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, (t - fadeIn - hold) / fadeOut);
            float s = Mathf.Lerp(0.97f, 1.03f, t / total);
            _title.rectTransform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_current == this) _current = null;
    }
}
