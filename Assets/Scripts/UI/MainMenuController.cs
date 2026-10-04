using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Menú principal: botones, héroes 3D, transición a la selección de clase y paneles pendientes.
public class MainMenuController : MonoBehaviour
{
    [Header("Pantallas")]
    public CanvasGroup mainGroup;
    public CanvasGroup classGroup;
    public ClassSelectController classSelect;

    [Header("Botones")]
    public Button newGameButton;
    public Button continueButton;
    public TMP_Text continueLabel;
    public Button optionsButton;
    public Button creditsButton;
    public Button quitButton;

    [Header("Panel pendiente (Opciones / Créditos)")]
    public CanvasGroup stubGroup;
    public TMP_Text stubTitle;
    public Button stubCloseButton;

    [Header("Héroes del menú")]
    public RawImage heroesImage;
    public GameObject penguinPrefab;
    public PlumagePalette palette;
    public List<CharacterClass> heroClasses = new List<CharacterClass>();
    public string previewLayerName = "UIPreview";

    private Button _returnFocus;

    void Start()
    {
        GameSession.Ensure();

        newGameButton.onClick.AddListener(OpenClassSelect);
        optionsButton.onClick.AddListener(() => OpenStub("Opciones", optionsButton));
        creditsButton.onClick.AddListener(() => OpenStub("Créditos", creditsButton));
        quitButton.onClick.AddListener(Quit);
        continueButton.onClick.AddListener(OpenLoad);
        if (stubCloseButton != null) stubCloseButton.onClick.AddListener(CloseStub);

        bool hasSave = SaveSystem.AnyExists();
        continueButton.interactable = hasSave;
        if (!hasSave)
        {
            var nav = continueButton.navigation;
            nav.mode = Navigation.Mode.None;
            continueButton.navigation = nav;
            if (continueLabel != null) continueLabel.color = FrostboundUI.Disabled;
        }
        SetupMenuNavigation(hasSave);

        BuildHeroes();
        SetGroup(mainGroup, true);
        SetGroup(classGroup, false);
        SetGroup(stubGroup, false);
        Select(newGameButton);
    }

    private void SetupMenuNavigation(bool hasSave)
    {
        var order = new List<Button> { newGameButton };
        if (hasSave) order.Add(continueButton);
        order.Add(optionsButton);
        order.Add(creditsButton);
        order.Add(quitButton);
        for (int i = 0; i < order.Count; i++)
        {
            var nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = order[(i - 1 + order.Count) % order.Count],
                selectOnDown = order[(i + 1) % order.Count]
            };
            order[i].navigation = nav;
        }
    }

    private void BuildHeroes()
    {
        if (heroesImage == null || penguinPrefab == null || heroClasses.Count == 0) return;
        int layer = LayerMask.NameToLayer(previewLayerName);
        if (layer < 0) layer = 31;
        PreviewStudio studio = PreviewStudio.Create("MenuHeroesStudio", layer);
        Rect r = heroesImage.rectTransform.rect;
        int w = Mathf.RoundToInt(r.width * 2f), h = Mathf.RoundToInt(r.height * 2f);
        float ortho = 0.74f;
        float width = ortho * 2f * r.width / r.height;
        PreviewStudio.Shot shot = studio.AddShot(null, Vector3.zero, w, h, ortho, new Vector3(0f, ortho - 0.02f, 0f));
        heroesImage.texture = shot.texture;

        float spacing = Mathf.Min(0.86f, (width - 0.9f) / Mathf.Max(1, heroClasses.Count - 1));
        for (int i = 0; i < heroClasses.Count; i++)
        {
            CharacterClass cls = heroClasses[i];
            float yaw = 180f + (i - 1.5f) * -10f;
            var actor = studio.AddActor(penguinPrefab, new Vector3((i - (heroClasses.Count - 1) * 0.5f) * spacing, 0f, -i * 0.05f), yaw);
            studio.Dress(actor, cls, palette != null ? palette.ColorOf(cls.defaultPlumageId) : Color.white);
        }
    }

    public void OpenClassSelect()
    {
        _returnFocus = newGameButton;
        StartCoroutine(Swap(mainGroup, classGroup, () => classSelect.Open(this)));
    }

    public void ReturnFromClassSelect()
    {
        StartCoroutine(Swap(classGroup, mainGroup, () => Select(_returnFocus != null ? _returnFocus : newGameButton)));
    }

    private void OpenLoad()
    {
        mainGroup.interactable = false;
        SaveSlotsPanel.Open(SaveSlotsPanel.Mode.Load, slot =>
        {
            if (GameSession.Ensure().LoadGame(slot)) SceneManager.LoadScene(SceneIds.Village);
            else
            {
                mainGroup.interactable = true;
                Select(continueButton);
            }
        }, () =>
        {
            mainGroup.interactable = true;
            bool any = SaveSystem.AnyExists();
            continueButton.interactable = any;
            if (continueLabel != null) continueLabel.color = any ? FrostboundUI.Text : FrostboundUI.Disabled;
            SetupMenuNavigation(any);
            Select(any ? continueButton : newGameButton);
        });
    }

    private void OpenStub(string title, Button from)
    {
        _returnFocus = from;
        if (stubTitle != null) stubTitle.text = title;
        StartCoroutine(Swap(mainGroup, stubGroup, () => Select(stubCloseButton)));
    }

    private void CloseStub()
    {
        StartCoroutine(Swap(stubGroup, mainGroup, () => Select(_returnFocus)));
    }

    public void Quit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Update()
    {
        if (!SaveSlotsPanel.IsOpen && stubGroup != null && stubGroup.interactable && UICancel.Pressed()) CloseStub();
    }

    private IEnumerator Swap(CanvasGroup from, CanvasGroup to, System.Action done)
    {
        if (from != null) from.interactable = false;
        float t = 0f;
        if (to != null)
        {
            to.gameObject.SetActive(true);
            to.alpha = 0f;
        }
        while (t < FrostboundUI.FadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / FrostboundUI.FadeDuration);
            if (from != null) from.alpha = 1f - k;
            if (to != null) to.alpha = k;
            yield return null;
        }
        SetGroup(from, false);
        SetGroup(to, true);
        done?.Invoke();
    }

    private static void SetGroup(CanvasGroup g, bool on)
    {
        if (g == null) return;
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
        g.gameObject.SetActive(on);
    }

    public static void Select(Selectable s)
    {
        if (s == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(s.gameObject);
    }
}
