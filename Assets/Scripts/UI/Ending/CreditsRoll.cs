using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Créditos que suben al terminar el juego (tras hablar con Steve en el epílogo). Esc, B o clic los saltan.
// Al acabar: "Seguir jugando" (el mundo sigue abierto) o "Volver al menú".
public class CreditsRoll : MonoBehaviour
{
    public static bool Showing { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Showing = false;

    public static void Show()
    {
        if (Showing) return;
        new GameObject("Creditos", typeof(RectTransform)).AddComponent<CreditsRoll>();
    }

    private RectTransform _scroll;
    private TextMeshProUGUI _body;
    private CanvasGroup _group, _buttons;
    private bool _skip;
    private const float Seconds = 38f;

    void Awake()
    {
        Showing = true;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 410;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(FrostboundUI.ReferenceWidth, FrostboundUI.ReferenceHeight);
        ScreenAdapter.Match(scaler);
        gameObject.AddComponent<GraphicRaycaster>();
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        Image bg = FrostUI.Img(transform, "Fondo", null, new Color(0.02f, 0.03f, 0.06f, 1f), false);
        FrostUI.Stretch(bg.rectTransform);
        bg.raycastTarget = true;

        _scroll = FrostUI.Node(transform, "Rollo");
        _scroll.anchorMin = _scroll.anchorMax = new Vector2(0.5f, 0f);
        _scroll.pivot = new Vector2(0.5f, 1f);
        _scroll.sizeDelta = new Vector2(1000f, 2200f);
        TextMeshProUGUI title = FrostUI.Title(_scroll, "Titulo", "Frostbound", 86f);
        FrostUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1000f, 120f));
        TextMeshProUGUI fin = FrostUI.Text(_scroll, "Fin", "Fin", 34f, FrostboundUI.OldGoldLight, TextAlignmentOptions.Center, FrostUI.Heading, 12f);
        FrostUI.Place(fin.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1000f, 50f));
        TextMeshProUGUI body = _body = FrostUI.Text(_scroll, "Texto", GameCredits.Rich, 24f, FrostboundUI.Bone, TextAlignmentOptions.Top);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = 12f;
        FrostUI.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(1000f, 1880f));
        body.rectTransform.pivot = new Vector2(0.5f, 1f);

        // Botones del final.
        RectTransform row = FrostUI.Node(transform, "Botones");
        FrostUI.Place(row, new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(760f, 80f));
        _buttons = row.gameObject.AddComponent<CanvasGroup>();
        _buttons.alpha = 0f;
        _buttons.interactable = false;
        _buttons.blocksRaycasts = false;
        FrostButton stay = FrostButton.Create(row, "Seguir", "Seguir jugando", new Vector2(340f, 70f), FrostButton.Style.Normal, 26f);
        FrostUI.Place((RectTransform)stay.transform, new Vector2(0.5f, 0.5f), new Vector2(-185f, 0f), new Vector2(340f, 70f));
        stay.button.onClick.AddListener(Close);
        FrostButton menu = FrostButton.Create(row, "Menu", "Volver al menú", new Vector2(340f, 70f), FrostButton.Style.Normal, 26f);
        FrostUI.Place((RectTransform)menu.transform, new Vector2(0.5f, 0.5f), new Vector2(185f, 0f), new Vector2(340f, 70f));
        menu.button.onClick.AddListener(ToMenu);
        GameplayInput.Block();
    }

    void OnDestroy()
    {
        Showing = false;
        GameplayInput.Unblock();
    }

    void Update()
    {
        if (_buttons.interactable) return;
        bool key = (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
                   || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
                   || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        if (key) _skip = true;
    }

    IEnumerator Start()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.2f)
        {
            _group.alpha = t;
            yield return null;
        }
        _group.alpha = 1f;
        float h = ((RectTransform)transform).rect.height;
        float from = 0f, to = h + _scroll.sizeDelta.y * 0.82f;
        for (float t = 0f; t < 1f && !_skip; t += Time.unscaledDeltaTime / Seconds)
        {
            _scroll.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, to, t));
            yield return null;
        }
        // Se queda solo el título a la vista y aparecen los botones.
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.4f)
        {
            _body.alpha = t;
            yield return null;
        }
        _body.alpha = 0f;
        _scroll.anchoredPosition = new Vector2(0f, h * 0.68f);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.5f)
        {
            _buttons.alpha = t;
            yield return null;
        }
        _buttons.alpha = 1f;
        _buttons.interactable = true;
        _buttons.blocksRaycasts = true;
        Button first = _buttons.GetComponentInChildren<Button>();
        if (first != null && UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    private void Close() => StartCoroutine(FadeOut(false));

    private void ToMenu() => StartCoroutine(FadeOut(true));

    private IEnumerator FadeOut(bool menu)
    {
        _buttons.interactable = false;
        if (menu)
        {
            PauseMenu pause = FindAnyObjectByType<PauseMenu>();
            if (pause != null)
            {
                pause.QuitToMenu();
                yield return new WaitForSecondsRealtime(0.3f);
                Destroy(gameObject);
                yield break;
            }
        }
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.6f)
        {
            _group.alpha = t;
            yield return null;
        }
        Destroy(gameObject);
    }
}

// Texto de los créditos (los mismos en el menú y al final del juego).
public static class GameCredits
{
    public static string Rich =>
        H("Diseño y desarrollo") + "Sebastián\n\n" +
        H("Programación asistida") + "Claude (Anthropic)\n\n" +
        H("Historia") + "La caída de la ciudadela, el ascenso del Frostspire,\nel Bosque Corrupto y la Cumbre del sello\n\n" +
        H("Tipografías") + "Cinzel, Nunito y Lilita One\nSIL Open Font License\n\n" +
        H("Texturas y cielo de la mansión") + "Poly Haven (CC0)\n\n" +
        H("Mansión del menú") + "Modelada con Blender\n\n" +
        H("Motor") + "Unity 6 · Universal Render Pipeline\n\n" +
        H("Gracias por jugar") + "Pipo, Rocco y Bobby te mandan saludos.";

    public static string Plain =>
        "Diseño y desarrollo: Sebastián\nProgramación asistida: Claude\n\n" +
        "Tipografías: Lilita One, Nunito y Cinzel (SIL Open Font License)\nTexturas y cielo: Poly Haven (CC0)\nHecho con Unity";

    private static string H(string s) => "<color=#C9A65A><size=80%>" + s.ToUpperInvariant() + "</size></color>\n";
}
