using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// El final del capítulo III: al rehacer el sello la pantalla se funde a negro y una voz cuenta lo que pasó después.
// Luego el héroe vuelve al poblado (misión "epilogo"). Sobrevive al cambio de escena hasta que empieza la carga.
public class EndingSequence : MonoBehaviour
{
    public static readonly string[] Narration =
    {
        "El Corazón del Frost se apagó bajo la montaña, y el sello volvió a cerrarse.",
        "Esa noche, por primera vez en muchas lunas, la ventisca se detuvo.",
        "En las laderas, los corruptos que quedaban despertaron confusos: volvían a ser pingüinos.",
        "Bobby bajó de la cumbre junto al héroe. En el poblado, alguien los esperaba."
    };

    public static bool Running { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Running = false;

    public static void Play()
    {
        if (Running) return;
        var go = new GameObject("Final", typeof(RectTransform));
        DontDestroyOnLoad(go);
        go.AddComponent<EndingSequence>();
    }

    private CanvasGroup _group;
    private TextMeshProUGUI _title, _line;

    void Awake()
    {
        Running = true;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(FrostboundUI.ReferenceWidth, FrostboundUI.ReferenceHeight);
        ScreenAdapter.Match(scaler);
        gameObject.AddComponent<GraphicRaycaster>();
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        Image bg = FrostUI.Img(transform, "Fondo", null, new Color(0.02f, 0.03f, 0.06f, 1f), false);
        FrostUI.Stretch(bg.rectTransform);
        _title = FrostUI.Title(transform, "Titulo", "El sello", 64f);
        FrostUI.Place(_title.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(1400f, 100f));
        _line = FrostUI.Text(transform, "Linea", "", 30f, FrostboundUI.Bone, TextAlignmentOptions.Center);
        _line.textWrappingMode = TextWrappingModes.Normal;
        FrostUI.Place(_line.rectTransform, new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(1200f, 160f));
        GameplayInput.Block();
    }

    void OnDestroy()
    {
        Running = false;
        GameplayInput.Unblock();
    }

    IEnumerator Start()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.6f)
        {
            _group.alpha = t;
            yield return null;
        }
        _group.alpha = 1f;
        foreach (string line in Narration)
        {
            yield return FadeLine(line);
        }
        // Al poblado: la misión del epílogo empieza sola.
        ZoneTravel.Go(SceneIds.Village, "");
        yield return new WaitForSecondsRealtime(0.6f);
        Destroy(gameObject);
    }

    private IEnumerator FadeLine(string line)
    {
        _line.text = line;
        float hold = Mathf.Clamp(2.4f + line.Length * 0.04f, 3.5f, 6f);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.8f)
        {
            _line.alpha = t;
            yield return null;
        }
        _line.alpha = 1f;
        yield return new WaitForSecondsRealtime(hold);
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.6f)
        {
            _line.alpha = t;
            yield return null;
        }
        _line.alpha = 0f;
    }
}
