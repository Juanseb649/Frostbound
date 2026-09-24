using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public static class SetupMainMenu
{
    private const string ScenePath = "Assets/Scenes/Main menu.unity";
    private const string GameScenePath = "Assets/Scenes/SampleScene.unity";

    private static readonly Color MenuBlue = new Color(0.09f, 0.16f, 0.26f);
    private static readonly Color Overlay = new Color(0.03f, 0.06f, 0.10f, 0.78f);
    private static readonly Color PanelBlue = new Color(0.14f, 0.24f, 0.36f);
    private static readonly Color ButtonBlue = new Color(0.22f, 0.42f, 0.60f);
    private static readonly Color ButtonHover = new Color(0.33f, 0.56f, 0.75f);
    private static readonly Color ButtonPressed = new Color(0.14f, 0.30f, 0.46f);

    [MenuItem("Tools/Frostbound/Setup Main Menu")]
    public static void Setup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene;
        if (File.Exists(ScenePath))
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        Cleanup();

        // Cámara de fondo (color de cielo hielo).
        GameObject camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 0f, -10f);
        Camera cam = camGO.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = MenuBlue;
        cam.orthographic = true;

        // Canvas.
        Canvas canvas = CreateCanvas();

        // EventSystem con soporte de Input System.
        CreateEventSystem();

        // Fondo a pantalla completa (bajo el canvas) con la imagen del menú cargada.
        Sprite bgSprite = GetMenuBgSprite();
        GameObject bgGO = CreateFullImage(canvas.transform, "Background", Color.white, false);
        if (bgSprite != null)
        {
            Image bgImg = bgGO.GetComponent<Image>();
            bgImg.sprite = bgSprite;
            bgImg.color = Color.white;
        }

        // --- Panel principal ---
        GameObject mainPanel = CreateFullImage(canvas.transform, "MainPanel", new Color(0, 0, 0, 0), false);

        // Los 4 botones principales alineados perfectamente sobre el arte dibujado en la imagen:
        Button btnNew = CreateArtButton(mainPanel.transform, "NewGameButton", new Vector2(0f, 9f), new Vector2(460f, 100f));
        Button btnLoad = CreateArtButton(mainPanel.transform, "LoadButton", new Vector2(0f, -124f), new Vector2(420f, 100f));
        Button btnOptions = CreateArtButton(mainPanel.transform, "OptionsButton", new Vector2(0f, -244f), new Vector2(420f, 100f));
        Button btnQuit = CreateArtButton(mainPanel.transform, "QuitButton", new Vector2(0f, -366f), new Vector2(420f, 100f));

        // --- Panel de opciones ---
        GameObject optionsPanel = CreateFullImage(canvas.transform, "OptionsPanel", Overlay, true);
        optionsPanel.SetActive(false);
        Image optionsBox = CreateImage(optionsPanel.transform, "OptionsBox", UISprite, Vector2.zero, new Vector2(620f, 640f));
        optionsBox.color = PanelBlue;

        CreateText(optionsBox.transform, "Title", "OPCIONES", new Vector2(0f, 240f), new Vector2(500f, 60f), 44, FontStyle.Bold);
        CreateText(optionsBox.transform, "VolLabel", "Volumen", new Vector2(0f, 150f), new Vector2(500f, 40f), 26, FontStyle.Normal);
        Slider volumeSlider = CreateSlider(optionsBox.transform, "VolumeSlider", new Vector2(0f, 105f), new Vector2(440f, 30f));
        CreateText(optionsBox.transform, "FsLabel", "Pantalla completa", new Vector2(0f, 20f), new Vector2(500f, 40f), 26, FontStyle.Normal);
        Toggle fullscreenToggle = CreateToggle(optionsBox.transform, "FullscreenToggle", new Vector2(180f, 20f), new Vector2(44f, 44f));
        CreateText(optionsBox.transform, "ResLabel", "Resolución", new Vector2(0f, -70f), new Vector2(500f, 40f), 26, FontStyle.Normal);
        Button resPrev = CreateButton(optionsBox.transform, "ResPrev", "\u25C0", new Vector2(-120f, -115f), new Vector2(60f, 44f));
        Text resText = CreateText(optionsBox.transform, "ResValue", "1920x1080", new Vector2(0f, -115f), new Vector2(220f, 44f), 26, FontStyle.Normal);
        Button resNext = CreateButton(optionsBox.transform, "ResNext", "\u25B6", new Vector2(120f, -115f), new Vector2(60f, 44f));
        Button btnOptionsBack = CreateButton(optionsBox.transform, "BackButton", "VOLVER", new Vector2(0f, -230f), new Vector2(220f, 64f));

        // --- Panel de cargar partida ---
        GameObject loadPanel = CreateFullImage(canvas.transform, "LoadPanel", Overlay, true);
        loadPanel.SetActive(false);
        Image loadBox = CreateImage(loadPanel.transform, "LoadBox", UISprite, Vector2.zero, new Vector2(620f, 640f));
        loadBox.color = PanelBlue;

        CreateText(loadBox.transform, "Title", "CARGAR PARTIDA", new Vector2(0f, 240f), new Vector2(500f, 60f), 40, FontStyle.Bold);
        Button slot1 = CreateButton(loadBox.transform, "Slot1", "Hueco 1 \u2014 Vacío", new Vector2(0f, 150f), new Vector2(440f, 60f));
        Button slot2 = CreateButton(loadBox.transform, "Slot2", "Hueco 2 \u2014 Vacío", new Vector2(0f, 70f), new Vector2(440f, 60f));
        Button slot3 = CreateButton(loadBox.transform, "Slot3", "Hueco 3 \u2014 Vacío", new Vector2(0f, -10f), new Vector2(440f, 60f));
        Text loadStatus = CreateText(loadBox.transform, "Status", "Selecciona un hueco para cargar.", new Vector2(0f, -90f), new Vector2(520f, 50f), 22, FontStyle.Italic);
        Button btnLoadBack = CreateButton(loadBox.transform, "BackButton", "VOLVER", new Vector2(0f, -230f), new Vector2(220f, 64f));

        // --- MenuManager (lógica) ---
        MenuManager mgr = new GameObject("MenuManager").AddComponent<MenuManager>();
        mgr.gameSceneName = "SampleScene";
        mgr.mainPanel = mainPanel;
        mgr.optionsPanel = optionsPanel;
        mgr.loadPanel = loadPanel;
        mgr.iceCursorTexture = GetIceCursorTexture();
        mgr.newGameButton = btnNew;
        mgr.loadButton = btnLoad;
        mgr.optionsButton = btnOptions;
        mgr.quitButton = btnQuit;
        mgr.volumeSlider = volumeSlider;
        mgr.fullscreenToggle = fullscreenToggle;
        mgr.resPrevButton = resPrev;
        mgr.resNextButton = resNext;
        mgr.resText = resText;
        mgr.optionsBackButton = btnOptionsBack;
        mgr.loadBackButton = btnLoadBack;
        mgr.loadStatusText = loadStatus;
        mgr.loadSlotButtons = new Button[] { slot1, slot2, slot3 };

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // Verificación: ¿cuántos botones quedaron en el menú?
        int buttonCount = mainPanel.GetComponentsInChildren<Button>(true).Length;
        int inputCount = mainPanel.GetComponentsInChildren<InputField>(true).Length;
        Debug.Log("Frostbound: menú creado con " + buttonCount + " botones y " + inputCount + " campos de texto.");

        // Build Settings: MainMenu (0) y SampleScene (1).
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true),
        };

        EditorUtility.DisplayDialog("Frostbound",
            "Menú principal creado en Assets/Scenes/Main menu.unity.\n\n" +
            "Los botones ya funcionan (Nuevo juego, Cargar, Opciones, Salir).\n" +
            "Build Settings actualizado: MainMenu = índice 0.", "OK");
    }

    // ---------- Limpieza ----------

    static void Cleanup()
    {
        string[] roots = { "MenuRoot", "Canvas", "EventSystem", "Main Camera", "Background", "MenuManager" };
        foreach (string name in roots)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }

    // ---------- UI helpers ----------

    static Canvas CreateCanvas()
    {
        GameObject go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // El canvas se ajusta a la resolución del logo (1094x976) para que encaje 1:1.
        scaler.referenceResolution = new Vector2(1094f, 976f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    static void CreateEventSystem()
    {
        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    static GameObject CreateFullImage(Transform parent, string name, Color color, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rt = (RectTransform)go.transform;
        if (parent != null) rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return go;
    }

    static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        CenterRT(rt, pos, size);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        return img;
    }

    static Button CreateArtButton(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(MenuButtonEffects));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        CenterRT(rt, pos, size);

        // Fondo transparente para que el botón dibujado en el arte sea visible
        Image img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);

        // Aureola luminosa de selección (aura cian)
        GameObject glowGO = new GameObject("GlowOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform glowRT = (RectTransform)glowGO.transform;
        glowRT.SetParent(rt, false);
        glowRT.anchorMin = Vector2.zero;
        glowRT.anchorMax = Vector2.one;
        glowRT.offsetMin = new Vector2(-15f, -15f);
        glowRT.offsetMax = new Vector2(15f, 15f);

        Image glowImg = glowGO.GetComponent<Image>();
        glowImg.sprite = UISprite;
        glowImg.color = new Color(0.4f, 0.85f, 1.0f, 0f);
        glowImg.raycastTarget = false;

        Button btn = go.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.clear;
        cb.highlightedColor = Color.clear;
        cb.pressedColor = new Color(1f, 1f, 1f, 0.15f);
        cb.selectedColor = Color.clear;
        btn.colors = cb;

        MenuButtonEffects fx = go.GetComponent<MenuButtonEffects>();
        fx.glowOverlay = glowImg;
        fx.hoverScale = 1.06f;

        return btn;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        CenterRT(rt, pos, size);

        Image img = go.GetComponent<Image>();
        img.sprite = UISprite;
        img.color = ButtonBlue;

        Button btn = go.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = ButtonHover;
        cb.pressedColor = ButtonPressed;
        cb.selectedColor = ButtonHover;
        btn.colors = cb;

        Text t = CreateText(go.transform, "Text", label, Vector2.zero, size, 30, FontStyle.Bold);
        t.color = Color.white;
        return btn;
    }

    static Text CreateText(Transform parent, string name, string content, Vector2 pos, Vector2 size, int fontSize, FontStyle style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        CenterRT(rt, pos, size);

        Text t = go.GetComponent<Text>();
        t.font = MenuFont;
        t.text = content;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Slider CreateSlider(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        CenterRT(rt, pos, size);

        Image bg = go.GetComponent<Image>();
        bg.sprite = UISprite;
        bg.color = new Color(0.05f, 0.09f, 0.14f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        RectTransform faRT = (RectTransform)fillArea.transform;
        faRT.SetParent(rt, false);
        faRT.anchorMin = new Vector2(0f, 0.25f);
        faRT.anchorMax = new Vector2(1f, 0.75f);
        faRT.offsetMin = Vector2.zero;
        faRT.offsetMax = Vector2.zero;

        GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform fillRT = (RectTransform)fillGO.transform;
        fillRT.SetParent(faRT, false);
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        Image fillImg = fillGO.GetComponent<Image>();
        fillImg.sprite = UISprite;
        fillImg.color = ButtonBlue;

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        RectTransform haRT = (RectTransform)handleArea.transform;
        haRT.SetParent(rt, false);
        haRT.anchorMin = Vector2.zero;
        haRT.anchorMax = Vector2.one;
        haRT.offsetMin = new Vector2(10f, 0f);
        haRT.offsetMax = new Vector2(-10f, 0f);

        GameObject handleGO = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform handleRT = (RectTransform)handleGO.transform;
        handleRT.SetParent(haRT, false);
        handleRT.anchorMin = new Vector2(0f, 0f);
        handleRT.anchorMax = new Vector2(0f, 1f);
        handleRT.pivot = new Vector2(0.5f, 0.5f);
        handleRT.sizeDelta = new Vector2(24f, 24f);
        Image handleImg = handleGO.GetComponent<Image>();
        handleImg.sprite = UISprite;
        handleImg.color = Color.white;

        Slider slider = go.GetComponent<Slider>();
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.value = 1f;
        return slider;
    }

    static InputField CreateInputField(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        CenterRT(rt, pos, size);

        Image img = go.GetComponent<Image>();
        img.sprite = UISprite;
        img.color = new Color(0.05f, 0.09f, 0.14f);

        Text text = CreateText(go.transform, "Text", "", Vector2.zero, new Vector2(size.x - 24f, size.y), 28, FontStyle.Normal);
        text.alignment = TextAnchor.MiddleLeft;

        Text placeholder = CreateText(go.transform, "Placeholder", "Escribe tu nombre...", Vector2.zero, new Vector2(size.x - 24f, size.y), 28, FontStyle.Italic);
        placeholder.color = new Color(0.6f, 0.7f, 0.8f);
        placeholder.alignment = TextAnchor.MiddleLeft;

        InputField input = go.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.caretColor = Color.white;
        input.selectionColor = new Color(0.3f, 0.55f, 0.8f);
        input.lineType = InputField.LineType.SingleLine;
        input.characterLimit = 16;
        return input;
    }

    static Toggle CreateToggle(Transform parent, string name, Vector2 pos, Vector2 size)
    {        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Toggle));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        CenterRT(rt, pos, size);

        Image bg = go.GetComponent<Image>();
        bg.sprite = UISprite;
        bg.color = new Color(0.05f, 0.09f, 0.14f);

        GameObject checkGO = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform checkRT = (RectTransform)checkGO.transform;
        checkRT.SetParent(rt, false);
        checkRT.anchorMin = Vector2.zero;
        checkRT.anchorMax = Vector2.one;
        checkRT.offsetMin = new Vector2(6f, 6f);
        checkRT.offsetMax = new Vector2(-6f, -6f);
        Image checkImg = checkGO.GetComponent<Image>();
        checkImg.sprite = UISprite;
        checkImg.color = ButtonBlue;

        Toggle toggle = go.GetComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = checkImg;
        toggle.isOn = true;
        return toggle;
    }

    static void CenterRT(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // ---------- Recursos ----------

    static Sprite UISprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

    static Sprite GetMenuBgSprite()
    {
        const string path = "Assets/Textures/main_menu_bg.jpg";
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null)
        {
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Texture2D GetIceCursorTexture()
    {
        const string path = "Assets/Textures/cursor_ice.png";
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null)
        {
            if (importer.textureType != TextureImporterType.Cursor)
            {
                importer.textureType = TextureImporterType.Cursor;
                importer.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Importa el logo como Sprite (Single) para que la referencia persista al guardar la escena.
    static Sprite GetLogoSprite()
    {
        const string logoPath = "Assets/Textures/logo.png";
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(logoPath);
        if (importer != null)
        {
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(logoPath);
    }

    static Font MenuFont
    {
        get
        {
            // En Unity 6 la fuente integrada se obtiene vía Resources, no AssetDatabase.
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }
}
