using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla de carga entre zonas: nombre del capítulo en letras de hielo, un consejo, nieve cayendo y la barra de progreso.
// Sobrevive al cambio de escena y se desvanece cuando la zona nueva ya está generada.
public class LoadingScreen : MonoBehaviour
{
    public const float MinSeconds = 1.8f;

    private static readonly string[] Tips =
    {
        "Las hogueras encendidas son tu punto de regreso al caer.",
        "Pulsa F para usar la habilidad de tu clase. Gasta maná.",
        "Repartir puntos en Maná da más maná máximo y más regeneración.",
        "Los corruptos a distancia se quedan atrás: acércate a ellos primero.",
        "Graba runas en tus armas: el hechizo de grabado consume maná.",
        "Rodar con Espacio te hace invulnerable un instante.",
        "Lleva las armas rotas al herrero del poblado antes de subir.",
        "La tabernera guarda tu arcón: deja ahí lo que no quieras perder.",
        "Pulsa Esc para pausar la partida."
    };

    private static LoadingScreen _instance;
    public static bool Active => _instance != null;

    private CanvasGroup _group;
    private Image _fill;
    private RectTransform _spinner;
    private readonly List<(RectTransform rt, float speed, float sway, float phase)> _flakes = new List<(RectTransform, float, float, float)>();
    private float _progress;

    public static void Load(string scene)
    {
        if (_instance != null) return;
        var go = new GameObject("PantallaCarga", typeof(RectTransform));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<LoadingScreen>();
        _instance.Build(scene);
        _instance.StartCoroutine(_instance.Run(scene));
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private IEnumerator Run(string scene)
    {
        GameplayInput.Block();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            _group.alpha = t / 0.35f;
            yield return null;
        }
        _group.alpha = 1f;

        AsyncOperation op = SceneManager.LoadSceneAsync(scene);
        op.allowSceneActivation = false;
        float shown = 0f;
        while (op.progress < 0.9f || shown < MinSeconds)
        {
            shown += Time.unscaledDeltaTime;
            _progress = Mathf.Min(op.progress / 0.9f, shown / MinSeconds) * 0.92f;
            yield return null;
        }
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        _progress = 1f;
        yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(0.25f);
        GameplayInput.Unblock();
        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            _group.alpha = 1f - t / 0.5f;
            yield return null;
        }
        Destroy(gameObject);
    }

    void Update()
    {
        if (_fill != null) _fill.fillAmount = Mathf.MoveTowards(_fill.fillAmount, _progress, Time.unscaledDeltaTime * 1.5f);
        if (_spinner != null) _spinner.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 2.2f) * 45f);
        float h = ((RectTransform)transform).rect.height;
        foreach (var f in _flakes)
        {
            Vector2 p = f.rt.anchoredPosition;
            p.y -= f.speed * Time.unscaledDeltaTime;
            p.x += Mathf.Sin(Time.unscaledTime * 0.8f + f.phase) * f.sway * Time.unscaledDeltaTime;
            if (p.y < -20f) p.y = h + 20f;
            f.rt.anchoredPosition = p;
        }
    }

    private void Build(string scene)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        RectTransform root = (RectTransform)transform;

        Image bg = FrostUI.Img(root, "Fondo", null, FrostUI.Hex("#050A14"));
        FrostUI.Stretch(bg.rectTransform);
        bg.raycastTarget = true;
        Image glow = FrostUI.Img(root, "Brillo", FrostUI.SoftFade, new Color(0.18f, 0.36f, 0.6f, 0.35f), false);
        FrostUI.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1500f, 700f));
        glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        var rng = new System.Random(scene.GetHashCode());
        for (int i = 0; i < 70; i++)
        {
            float size = 2f + (float)rng.NextDouble() * 5f;
            Image flake = FrostUI.Img(root, "Copo", FrostUI.Skin != null ? FrostUI.Skin.circle : null, new Color(0.85f, 0.93f, 1f, 0.25f + (float)rng.NextDouble() * 0.45f), false);
            flake.rectTransform.anchorMin = flake.rectTransform.anchorMax = Vector2.zero;
            flake.rectTransform.sizeDelta = new Vector2(size, size);
            flake.rectTransform.anchoredPosition = new Vector2((float)rng.NextDouble() * 1920f, (float)rng.NextDouble() * 1100f);
            _flakes.Add((flake.rectTransform, 20f + size * 9f, 10f + (float)rng.NextDouble() * 25f, (float)rng.NextDouble() * 6f));
        }

        TextMeshProUGUI kicker = FrostUI.Kicker(root, "Capitulo", SceneIds.Kicker(scene), 24f, FrostUI.Ice);
        kicker.alignment = TextAlignmentOptions.Center;
        FrostUI.Place(kicker.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1000f, 36f));
        kicker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        TextMeshProUGUI title = FrostUI.Title(root, "Titulo", SceneIds.Title(scene).ToUpperInvariant(), 130f, TextAlignmentOptions.Center, 0.24f);
        title.fontStyle = FontStyles.Italic;
        FrostUI.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1600f, 170f));
        title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        TextMeshProUGUI sub = FrostUI.Text(root, "Subtitulo", SceneIds.Subtitle(scene), 26f, FrostUI.Secondary, TextAlignmentOptions.Center);
        FrostUI.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1000f, 40f));
        sub.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        Image diamond = FrostUI.Img(root, "Rombo", FrostUI.Diamond(true), FrostUI.Ice, false);
        FrostUI.Place(diamond.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(26f, 26f));
        diamond.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _spinner = diamond.rectTransform;

        Image track = FrostUI.Img(root, "Barra", FrostUI.Skin != null ? FrostUI.Skin.pill : null, FrostUI.BorderDim);
        FrostUI.Place(track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(720f, 10f));
        track.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _fill = FrostUI.Img(track.transform, "Relleno", null, FrostUI.Ice, false);
        FrostUI.Stretch(_fill.rectTransform);
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillAmount = 0f;

        string tip = Tips[rng.Next(Tips.Length)];
        TextMeshProUGUI tipText = FrostUI.Text(root, "Consejo", tip, 22f, Color.white, TextAlignmentOptions.Center, FrostUI.BodyLight);
        FrostUI.Place(tipText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(1200f, 36f));
        tipText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        TextMeshProUGUI tipKicker = FrostUI.Kicker(root, "ConsejoEtiqueta", "Consejo", 15f);
        tipKicker.alignment = TextAlignmentOptions.Center;
        FrostUI.Place(tipKicker.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 124f), new Vector2(400f, 22f));
        tipKicker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }
}
