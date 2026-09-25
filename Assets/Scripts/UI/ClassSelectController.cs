using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Selección de clase: tarjetas, preview 3D, color de plumaje, nombre y comienzo de la aventura.
public class ClassSelectController : MonoBehaviour
{
    [Header("Datos")]
    public List<CharacterClass> classes = new List<CharacterClass>();
    public PlumagePalette palette;
    public GameObject penguinPrefab;
    public string gameScene = "SampleScene";
    public string previewLayerName = "UIPreview";

    [Header("Lista de clases")]
    public ClassCard[] cards;

    [Header("Preview")]
    public RawImage heroImage;
    public PreviewDrag heroDrag;
    public Image plumageRing;

    [Header("Panel de detalle")]
    public TMP_Text tagLabel;
    public TMP_Text classNameLabel;
    public TMP_Text roleChip;
    public TMP_Text attributeChip;
    public TMP_Text descriptionLabel;
    public TMP_Text[] gearLabels;
    public TMP_Text colorLabel;
    public PlumageSwatch[] swatches;
    public TMP_InputField nameField;
    public Button startButton;
    public Button backButton;

    private MainMenuController _menu;
    private PreviewStudio _studio;
    private PreviewStudio.Shot _hero;
    private readonly List<PreviewStudio.Shot> _thumbs = new List<PreviewStudio.Shot>();
    private int _classIndex;
    private int _plumageIndex;
    private bool _open;

    void Awake()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            int index = i;
            cards[i].button.onClick.AddListener(() => SelectClass(index, true));
        }
        for (int i = 0; i < swatches.Length; i++)
        {
            int index = i;
            swatches[i].button.onClick.AddListener(() => SelectPlumage(index));
        }
        backButton.onClick.AddListener(Back);
        startButton.onClick.AddListener(StartAdventure);
    }

    public void Open(MainMenuController menu)
    {
        _menu = menu;
        _open = true;
        EnsureStudio();
        for (int i = 0; i < swatches.Length && i < palette.entries.Count; i++)
            swatches[i].Setup(palette.entries[i].color, palette.entries[i].displayName);
        for (int i = 0; i < cards.Length && i < classes.Count; i++)
        {
            CharacterClass cls = classes[i];
            cards[i].nameLabel.text = cls.displayName;
            cards[i].roleLabel.text = cls.role;
            _studio.Dress(_thumbs[i], cls, palette.ColorOf(cls.defaultPlumageId));
            cards[i].thumbnail.texture = _thumbs[i].texture;
        }
        if (nameField != null) nameField.text = "";
        SelectClass(0, false);
        SetupNavigation();
        MainMenuController.Select(cards[0].button);
    }

    private void EnsureStudio()
    {
        if (_studio != null) return;
        int layer = LayerMask.NameToLayer(previewLayerName);
        if (layer < 0) layer = 31;
        _studio = PreviewStudio.Create("ClassSelectStudio", layer);
        _hero = _studio.AddShot(penguinPrefab, Vector3.zero, 1024, 1224, 0.86f, new Vector3(0f, 0.72f, 0f));
        heroImage.texture = _hero.texture;
        if (heroDrag != null) heroDrag.target = _hero.pivot;
        for (int i = 0; i < cards.Length; i++)
            _thumbs.Add(_studio.AddShot(penguinPrefab, new Vector3(10f + i * 4f, 0f, 0f), 192, 212, 0.78f, new Vector3(0f, 0.7f, 0f), 200f));
    }

    public void SelectClass(int index, bool fromClick)
    {
        if (index < 0 || index >= classes.Count) return;
        _classIndex = index;
        CharacterClass cls = classes[index];
        _plumageIndex = palette.IndexOf(cls.defaultPlumageId);

        for (int i = 0; i < cards.Length; i++) cards[i].SetSelected(i == index);
        for (int i = 0; i < cards.Length && i < classes.Count; i++)
            _studio.Tint(_thumbs[i], palette.ColorOf(classes[i].defaultPlumageId));

        tagLabel.text = cls.className;
        classNameLabel.text = cls.displayName;
        roleChip.text = cls.role;
        attributeChip.text = "Atributo principal: " + cls.primaryAttribute;
        descriptionLabel.text = cls.description;
        for (int i = 0; i < gearLabels.Length; i++)
        {
            bool has = cls.startingGear != null && i < cls.startingGear.Length;
            gearLabels[i].transform.parent.gameObject.SetActive(has);
            if (has) gearLabels[i].text = cls.startingGear[i];
        }

        _studio.Dress(_hero, cls, CurrentColor);
        if (heroDrag != null) heroDrag.ResetView();
        RefreshPlumage();
        SetupNavigation();
    }

    public void SelectPlumage(int index)
    {
        if (index < 0 || index >= palette.entries.Count) return;
        _plumageIndex = index;
        RefreshPlumage();
        SetupNavigation();
    }

    private Color CurrentColor => palette.entries[_plumageIndex].color;

    private void RefreshPlumage()
    {
        PlumagePalette.Entry e = palette.entries[_plumageIndex];
        colorLabel.text = "Color del plumaje · <color=#E8F1FA>" + e.displayName + "</color>";
        for (int i = 0; i < swatches.Length; i++) swatches[i].SetSelected(i == _plumageIndex);
        if (plumageRing != null) plumageRing.color = e.color;
        _studio.Tint(_hero, e.color);
        if (_classIndex < _thumbs.Count) _studio.Tint(_thumbs[_classIndex], e.color);
    }

    private void SetupNavigation()
    {
        Selectable firstCard = cards[0].button;
        Selectable currentSwatch = swatches[_plumageIndex].button;
        Selectable currentCard = cards[_classIndex].button;

        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i == 0 ? (Selectable)backButton : cards[i - 1].button,
                selectOnDown = i < cards.Length - 1 ? cards[i + 1].button : null,
                selectOnRight = currentSwatch
            };
        }
        for (int i = 0; i < swatches.Length; i++)
        {
            swatches[i].button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = i == 0 ? currentCard : swatches[i - 1].button,
                selectOnRight = i < swatches.Length - 1 ? swatches[i + 1].button : null,
                selectOnDown = nameField,
                selectOnUp = backButton
            };
        }
        nameField.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = currentSwatch,
            selectOnDown = startButton,
            selectOnLeft = currentCard
        };
        startButton.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = nameField,
            selectOnLeft = currentCard
        };
        backButton.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnDown = firstCard,
            selectOnRight = currentSwatch
        };
    }

    void Update()
    {
        if (!_open) return;
        if (nameField != null && nameField.isFocused) return;
        if (UICancel.Pressed()) Back();
    }

    public void Back()
    {
        if (!_open) return;
        _open = false;
        _menu.ReturnFromClassSelect();
    }

    public void StartAdventure()
    {
        CharacterClass cls = classes[_classIndex];
        string heroName = nameField != null ? nameField.text.Trim() : "";
        if (string.IsNullOrEmpty(heroName)) heroName = cls.displayName;
        GameSession session = GameSession.Ensure();
        session.SetHero(cls, palette.entries[_plumageIndex].id, heroName);
        session.NewGame();
        PlayerPrefs.SetString("username", heroName);
        SceneManager.LoadScene(gameScene);
    }
}
