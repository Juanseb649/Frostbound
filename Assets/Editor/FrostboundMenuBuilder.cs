using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Construye el menú principal y la selección de clase según el lienzo
// "Frostbound — Menú y selección de clase" (referencia 1440 × 900).
public static class FrostboundMenuBuilder
{
    private const string UIRoot = "Assets/Frostbound/UI";
    private const string FontDir = UIRoot + "/Fonts";
    private const string SpriteDir = UIRoot + "/Sprites";
    private const string BackgroundDir = UIRoot + "/Backgrounds";
    private const string MenuScene = "Assets/Scenes/Main menu.unity";
    private const string GameScene = "Assets/Scenes/SampleScene.unity";
    private const string PreviewLayer = "UIPreview";

    private static TMP_FontAsset _cinzel600, _cinzel800, _nunito500, _nunito700, _nunito800;
    private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

    [MenuItem("Tools/Frostbound/UI/Construir menú y selección de clase")]
    public static void Build()
    {
        if (!EnsureTmpResources()) return;
        if (!FrostboundBridge.ConfirmSave()) return;

        EnsureLayer(PreviewLayer);
        FrostboundGearSetup.Setup();
        ConfigureTextures();
        LoadFonts();
        LoadSprites();

        PlumagePalette palette = AssetDatabase.LoadAssetAtPath<PlumagePalette>(FrostboundGearSetup.PalettePath);
        GameObject penguin = AssetDatabase.LoadAssetAtPath<GameObject>(SetupPenguinWardrobe.PrefabPath);
        var classes = new List<CharacterClass>();
        foreach (string n in new[] { "Knight_Caballero", "Mage_Mago", "Ninja", "Viking_Vikingo" })
        {
            CharacterClass c = AssetDatabase.LoadAssetAtPath<CharacterClass>("Assets/Data/Classes/" + n + ".asset");
            if (c != null) classes.Add(c);
        }

        Scene scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        DisableOldMenu(scene);
        EnsureEventSystem();
        ExcludeLayerFromCameras();

        GameObject canvasGo = BuildCanvas("MainMenuCanvas");
        var menu = canvasGo.AddComponent<MainMenuController>();
        Transform root = canvasGo.transform;

        BuildMainScreen(root, menu);
        ClassSelectController select = BuildClassSelect(root);
        BuildStub(root, menu);

        menu.classSelect = select;
        menu.penguinPrefab = penguin;
        menu.palette = palette;
        menu.heroClasses = classes;
        select.penguinPrefab = penguin;
        select.palette = palette;
        select.classes = classes;
        select.gameScene = Path.GetFileNameWithoutExtension(GameScene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        SetupBuildSettings();
        AddLoadoutToPlayer(palette);

        FrostboundBridge.Dialog("Frostbound",
            "Menú principal y selección de clase construidos en " + MenuScene + ".\n\n" +
            "El Canvas anterior quedó desactivado (no se borró). Pulsa Play para probar el flujo: " +
            "Nueva partida → elegir clase y color → Comenzar aventura.", "OK");
    }

    // ---------- Preparación ----------

    private static bool EnsureTmpResources()
    {
        if (Resources.Load<TMP_Settings>("TMP Settings") != null) return true;
        string pkg = null;
        foreach (string dir in Directory.GetDirectories("Library/PackageCache", "com.unity.ugui*"))
        {
            string candidate = Path.Combine(dir, "Package Resources", "TMP Essential Resources.unitypackage");
            if (File.Exists(candidate)) pkg = candidate;
        }
        if (pkg == null)
        {
            FrostboundBridge.Dialog("Frostbound", "Importa TMP Essential Resources (Window > TextMeshPro) y vuelve a ejecutar.", "OK");
            return false;
        }
        AssetDatabase.ImportPackage(pkg, false);
        FrostboundBridge.Dialog("Frostbound", "Se importaron los recursos de TextMeshPro. Ejecuta de nuevo Tools > Frostbound > UI > Construir menú y selección de clase.", "OK");
        return false;
    }

    private static void EnsureLayer(string layerName)
    {
        if (LayerMask.NameToLayer(layerName) >= 0) return;
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 31; i >= 8; i--)
        {
            SerializedProperty p = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(p.stringValue))
            {
                p.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return;
            }
        }
    }

    private static void ConfigureTextures()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir, BackgroundDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            string file = Path.GetFileNameWithoutExtension(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.filterMode = FilterMode.Bilinear;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.spritePixelsPerUnit = 200f;
            imp.spriteBorder = Vector4.zero;
            if (file.StartsWith("UI_Round") || file == "UI_Pill")
            {
                int r = file == "UI_Pill" ? 15 : int.Parse(file.Substring("UI_Round".Length));
                float b = r * 2f + 2f;
                imp.spriteBorder = new Vector4(b, b, b, b);
            }
            imp.SaveAndReimport();
        }
    }

    private static void LoadFonts()
    {
        _cinzel600 = FontAsset("Cinzel-SemiBold");
        _cinzel800 = FontAsset("Cinzel-ExtraBold");
        _nunito500 = FontAsset("Nunito-Medium");
        _nunito700 = FontAsset("Nunito-Bold");
        _nunito800 = FontAsset("Nunito-ExtraBold");
    }

    private static TMP_FontAsset FontAsset(string name)
    {
        string assetPath = FontDir + "/" + name + " SDF.asset";
        TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fa != null) return fa;
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "/" + name + ".ttf");
        fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        fa.name = name + " SDF";
        AssetDatabase.CreateAsset(fa, assetPath);
        fa.material.name = name + " SDF Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        fa.atlasTextures[0].name = name + " SDF Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?¿¡·—-áéíóúÁÉÍÓÚñÑüÜ'\"()/");
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    private static void LoadSprites()
    {
        Sprites.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { SpriteDir, BackgroundDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) Sprites[Path.GetFileNameWithoutExtension(path)] = s;
        }
    }

    private static void DisableOldMenu(Scene scene)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == "MainMenuCanvas") { Object.DestroyImmediate(go); continue; }
            bool isOldUi = go.GetComponentInChildren<Canvas>(true) != null || go.GetComponentInChildren<MenuManager>(true) != null;
            if (isOldUi && go.GetComponent<EventSystem>() == null) go.SetActive(false);
        }
    }

    private static void EnsureEventSystem()
    {
        EventSystem es = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (es == null) es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        es.gameObject.SetActive(true);
        foreach (var legacy in es.GetComponents<StandaloneInputModule>()) Object.DestroyImmediate(legacy);
        if (es.GetComponent<InputSystemUIInputModule>() == null) es.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    private static void ExcludeLayerFromCameras()
    {
        int layer = LayerMask.NameToLayer(PreviewLayer);
        if (layer < 0) return;
        foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            cam.cullingMask &= ~(1 << layer);
    }

    // ---------- Canvas ----------

    private static GameObject BuildCanvas(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(FrostboundUI.ReferenceWidth, FrostboundUI.ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return go;
    }

    private static void BuildMainScreen(Transform root, MainMenuController menu)
    {
        RectTransform group = Stretch(Node("MainGroup", root));
        menu.mainGroup = group.gameObject.AddComponent<CanvasGroup>();

        Image bg = Background(group, "Background", "MainMenu_Background");
        Image heart = Img(bg.transform, "FrostHeart", Sprites.GetValueOrDefault("FrostHeart"), Color.white);
        Place(heart.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((905f - 720f), -(560f - 450f)), new Vector2(160f, 160f));
        heart.raycastTarget = false;
        heart.gameObject.AddComponent<UIPulse>();

        RectTransform snow = Stretch(Node("Snow", group));
        var snowFx = snow.gameObject.AddComponent<UISnow>();
        snowFx.flake = Sprites.GetValueOrDefault("UI_Snowflake");

        RectTransform heroes = Node("HeroesLineup", group);
        Anchor(heroes, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 59f), new Vector2(746f, 286f));
        var heroesImage = heroes.gameObject.AddComponent<RawImage>();
        heroesImage.raycastTarget = false;
        menu.heroesImage = heroesImage;

        RectTransform title = Node("TitleBlock", group);
        Anchor(title, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(64f, -89f), new Vector2(573f, 754f));
        VLayout(title.gameObject, 44f, TextAnchor.UpperLeft);

        RectTransform head = Node("Heading", title);
        VLayout(head.gameObject, 10f, TextAnchor.UpperLeft);
        Txt(head, "Kicker", "Asciende el Frostspire", _nunito800, 14f, FrostboundUI.Gold, 6f, true);
        TMP_Text t = Txt(head, "Title", "FROST\nBOUND", _cinzel800, 104f, FrostboundUI.Title, 4f, false);
        SetLineHeight(t, 0.95f);

        RectTransform buttons = Node("MenuButtons", title);
        VLayout(buttons.gameObject, 12f, TextAnchor.UpperLeft);
        menu.newGameButton = PrimaryButton(buttons, "BtnNewGame", "Nueva partida", 340f, 60f, 20f);
        menu.continueButton = SecondaryButton(buttons, "BtnContinue", "Continuar", 340f, 52f, out TMP_Text contLabel);
        menu.continueLabel = contLabel;
        menu.optionsButton = SecondaryButton(buttons, "BtnOptions", "Opciones", 340f, 52f, out _);
        menu.creditsButton = SecondaryButton(buttons, "BtnCredits", "Créditos", 340f, 52f, out _);
        menu.quitButton = SecondaryButton(buttons, "BtnQuit", "Salir", 340f, 52f, out _);

        TMP_Text version = Txt(group, "VersionLabel", "v0.1 · Vertical slice", _nunito700, 13f, FrostboundUI.Version, 0f, false);
        Anchor(version.rectTransform, Vector2.zero, Vector2.zero, new Vector2(96f, 36f), new Vector2(300f, 20f));
        version.rectTransform.pivot = Vector2.zero;
        Object.DestroyImmediate(version.GetComponent<LayoutElement>());
    }

    private static ClassSelectController BuildClassSelect(Transform root)
    {
        RectTransform group = Stretch(Node("ClassSelectGroup", root));
        var canvasGroup = group.gameObject.AddComponent<CanvasGroup>();
        var select = group.gameObject.AddComponent<ClassSelectController>();
        root.GetComponent<MainMenuController>().classGroup = canvasGroup;

        Background(group, "Background", "ClassSelect_Background");

        // Encabezado
        select.backButton = SmallButton(group, "BtnBack", "Volver");
        Anchor((RectTransform)select.backButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -36f), new Vector2(124f, 44f));
        RectTransform titleGroup = Node("TitleGroup", group);
        Anchor(titleGroup, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(700f, 76f));
        titleGroup.pivot = new Vector2(0.5f, 1f);
        VLayout(titleGroup.gameObject, 4f, TextAnchor.UpperCenter);
        Txt(titleGroup, "Kicker", "Nueva partida", _nunito800, 13f, FrostboundUI.Gold, 5f, true).alignment = TextAlignmentOptions.Center;
        Txt(titleGroup, "Title", "Elige tu héroe", _cinzel800, 40f, FrostboundUI.Title, 3f, false).alignment = TextAlignmentOptions.Center;

        const float bodyTop = 136f;
        const float bodyHeight = 900f - bodyTop - 36f;

        // Lista de clases
        RectTransform list = Node("ClassList", group);
        Anchor(list, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -bodyTop), new Vector2(330f, 4 * 124f + 3 * 14f));
        VLayout(list.gameObject, 14f, TextAnchor.UpperLeft);
        var cards = new List<ClassCard>();
        for (int i = 0; i < 4; i++) cards.Add(ClassCardUI(list, i));
        select.cards = cards.ToArray();

        // Escenario del héroe
        RectTransform stage = Node("HeroStage", group);
        Anchor(stage, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(410f, -bodyTop), new Vector2(510f, bodyHeight));
        Image pedestal = Img(stage, "Pedestal", Sprites.GetValueOrDefault("UI_Pedestal"), Color.white);
        Anchor(pedestal.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(520f, 140f));
        pedestal.rectTransform.pivot = new Vector2(0.5f, 0f);
        pedestal.raycastTarget = false;
        Image ring = Img(stage, "PlumageRing", Sprites.GetValueOrDefault("UI_PedestalRing"), FrostboundUI.Hex("#1F4FB4"));
        Anchor(ring.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(520f, 140f));
        ring.rectTransform.pivot = new Vector2(0.5f, 0f);
        ring.raycastTarget = false;
        select.plumageRing = ring;
        RectTransform heroRt = Node("HeroPreview", stage);
        Anchor(heroRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(500f, 596f));
        heroRt.pivot = new Vector2(0.5f, 0f);
        select.heroImage = heroRt.gameObject.AddComponent<RawImage>();
        select.heroDrag = heroRt.gameObject.AddComponent<PreviewDrag>();

        // Panel de detalle
        RectTransform panel = Framed(group, "DetailPanel", 20, FrostboundUI.Surface, FrostboundUI.Border, out RectTransform content);
        Anchor(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -bodyTop), new Vector2(440f, bodyHeight));
        panel.pivot = new Vector2(1f, 1f);
        var vl = VLayout(content.gameObject, 18f, TextAnchor.UpperLeft);
        vl.padding = new RectOffset(24, 24, 22, 22);

        RectTransform headBlock = Node("Header", content);
        VLayout(headBlock.gameObject, 6f, TextAnchor.UpperLeft);
        select.tagLabel = Txt(headBlock, "Tag", "Knight", _nunito800, 12f, FrostboundUI.Gold, 4f, true);
        select.classNameLabel = Txt(headBlock, "ClassName", "Caballero de la Escarcha", _cinzel800, 30f, FrostboundUI.Title, 0f, false);
        SetLineHeight(select.classNameLabel, 1.15f);

        RectTransform chips = Node("Chips", content);
        var chipsLayout = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
        chipsLayout.spacing = 8f;
        chipsLayout.childControlWidth = true;
        chipsLayout.childControlHeight = true;
        chipsLayout.childForceExpandWidth = false;
        chipsLayout.childForceExpandHeight = false;
        select.roleChip = Chip(chips, "RoleChip", FrostboundUI.SurfaceSelected, FrostboundUI.Ice);
        select.attributeChip = Chip(chips, "AttributeChip", FrostboundUI.ChipGoldBg, FrostboundUI.ChipGoldText);

        select.descriptionLabel = Txt(content, "Description", "", _nunito500, 15f, FrostboundUI.Body, 0f, false);
        SetLineHeight(select.descriptionLabel, 1.55f);

        RectTransform gear = Node("StartingGear", content);
        VLayout(gear.gameObject, 8f, TextAnchor.UpperLeft);
        Txt(gear, "Label", "Equipo inicial", _nunito800, 12f, FrostboundUI.Muted, 3f, true);
        RectTransform cols = Node("Columns", gear);
        var colsLayout = cols.gameObject.AddComponent<HorizontalLayoutGroup>();
        colsLayout.spacing = 12f;
        colsLayout.childControlWidth = true;
        colsLayout.childControlHeight = true;
        colsLayout.childForceExpandWidth = true;
        colsLayout.childForceExpandHeight = false;
        RectTransform left = Node("Left", cols);
        RectTransform right = Node("Right", cols);
        VLayout(left.gameObject, 6f, TextAnchor.UpperLeft);
        VLayout(right.gameObject, 6f, TextAnchor.UpperLeft);
        var gearLabels = new List<TMP_Text>();
        for (int i = 0; i < 4; i++) gearLabels.Add(GearItem(i % 2 == 0 ? left : right, i));
        select.gearLabels = gearLabels.ToArray();

        RectTransform plumage = Node("PlumagePicker", content);
        VLayout(plumage.gameObject, 10f, TextAnchor.UpperLeft);
        select.colorLabel = Txt(plumage, "Label", "Color del plumaje · Azul", _nunito800, 12f, FrostboundUI.Muted, 3f, true);
        RectTransform row = Node("Swatches", plumage);
        var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 2f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = false;
        rowLayout.padding = new RectOffset(4, 4, 4, 4);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;
        var swatches = new List<PlumageSwatch>();
        for (int i = 0; i < 8; i++) swatches.Add(Swatch(row, i));
        select.swatches = swatches.ToArray();

        RectTransform nameBlock = Node("HeroName", content);
        VLayout(nameBlock.gameObject, 6f, TextAnchor.UpperLeft);
        Txt(nameBlock, "Label", "Nombre del héroe", _nunito800, 12f, FrostboundUI.Muted, 3f, true);
        select.nameField = InputField(nameBlock, "NameField", "Escribe un nombre");

        RectTransform spacer = Node("Spacer", content);
        spacer.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        select.startButton = PrimaryButton(content, "BtnStart", "Comenzar aventura", 388f, 58f, 19f);
        select.startButton.GetComponent<LayoutElement>().flexibleWidth = 1f;

        group.gameObject.SetActive(false);
        return select;
    }

    private static void BuildStub(Transform root, MainMenuController menu)
    {
        RectTransform group = Stretch(Node("StubGroup", root));
        menu.stubGroup = group.gameObject.AddComponent<CanvasGroup>();
        Image dim = group.gameObject.AddComponent<Image>();
        dim.color = new Color(FrostboundUI.Bg.r, FrostboundUI.Bg.g, FrostboundUI.Bg.b, 0.92f);

        RectTransform panel = Framed(group, "Panel", 20, FrostboundUI.Surface, FrostboundUI.Border, out RectTransform content);
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 260f));
        var vl = VLayout(content.gameObject, 18f, TextAnchor.MiddleCenter);
        vl.padding = new RectOffset(32, 32, 32, 32);
        menu.stubTitle = Txt(content, "Title", "Opciones", _cinzel800, 30f, FrostboundUI.Title, 3f, false);
        menu.stubTitle.alignment = TextAlignmentOptions.Center;
        TMP_Text body = Txt(content, "Body", "Próximamente.", _nunito500, 15f, FrostboundUI.Body, 0f, false);
        body.alignment = TextAlignmentOptions.Center;
        menu.stubCloseButton = SmallButton(content, "BtnClose", "Volver");
        group.gameObject.SetActive(false);
    }

    // ---------- Componentes ----------

    private static ClassCard ClassCardUI(Transform parent, int index)
    {
        RectTransform card = Framed(parent, "ClassCard_" + index, 16, FrostboundUI.Surface, FrostboundUI.Border, out RectTransform content);
        card.gameObject.AddComponent<LayoutElement>().preferredHeight = 124f;
        var cc = card.gameObject.AddComponent<ClassCard>();
        cc.button = card.gameObject.AddComponent<Button>();
        cc.button.transition = Selectable.Transition.None;
        cc.fill = content.GetComponent<Image>();
        cc.fx = card.gameObject.AddComponent<UIButtonFx>();
        cc.fx.border = card.GetComponent<Image>();
        cc.fx.normalBorder = FrostboundUI.Border;
        cc.fx.activeBorder = FrostboundUI.Ice;

        var hl = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(8, 16, 8, 8);
        hl.spacing = 14f;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;

        RectTransform thumb = Node("Thumbnail", content);
        cc.thumbnail = thumb.gameObject.AddComponent<RawImage>();
        cc.thumbnail.raycastTarget = false;
        var le = thumb.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 96f;
        le.preferredHeight = 106f;

        RectTransform texts = Node("Texts", content);
        VLayout(texts.gameObject, 4f, TextAnchor.MiddleLeft);
        texts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        cc.nameLabel = Txt(texts, "Name", "Clase", _cinzel800, 17f, FrostboundUI.Title, 0f, false);
        SetLineHeight(cc.nameLabel, 1.2f);
        cc.roleLabel = Txt(texts, "Role", "Rol", _nunito700, 13f, FrostboundUI.Muted, 0f, false);
        return cc;
    }

    private static TMP_Text GearItem(Transform parent, int index)
    {
        RectTransform item = Node("GearItem_" + index, parent);
        var hl = item.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 10f;
        hl.childAlignment = TextAnchor.UpperLeft;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;
        Image bullet = Img(item, "Bullet", Sprites.GetValueOrDefault("UI_Diamond"), FrostboundUI.Ice);
        var ble = bullet.gameObject.AddComponent<LayoutElement>();
        ble.preferredWidth = 12f;
        ble.preferredHeight = 12f;
        ble.minHeight = 12f;
        TMP_Text text = Txt(item, "Text", "", _nunito700, 15f, FrostboundUI.Text, 0f, false);
        text.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
        SetLineHeight(text, 1.3f);
        return text;
    }

    private static PlumageSwatch Swatch(Transform parent, int index)
    {
        RectTransform root = Node("Swatch_" + index, parent);
        root.sizeDelta = new Vector2(44f, 44f);
        var sw = root.gameObject.AddComponent<PlumageSwatch>();
        sw.halo = Img(root, "Halo", Sprites.GetValueOrDefault("UI_Circle"), Color.white);
        Place(sw.halo.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f));
        sw.halo.raycastTarget = false;
        sw.ring = Img(root, "Ring", Sprites.GetValueOrDefault("UI_Circle"), FrostboundUI.Surface);
        Place(sw.ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
        sw.fill = Img(root, "Fill", Sprites.GetValueOrDefault("UI_Circle"), Color.white);
        Place(sw.fill.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38f, 38f));
        sw.button = root.gameObject.AddComponent<Button>();
        sw.button.targetGraphic = sw.ring;
        sw.button.transition = Selectable.Transition.None;
        var fx = root.gameObject.AddComponent<UIButtonFx>();
        fx.hoverScale = 1.08f;
        return sw;
    }

    private static TMP_Text Chip(Transform parent, string name, Color bg, Color fg)
    {
        RectTransform chip = Node(name, parent);
        Image img = chip.gameObject.AddComponent<Image>();
        img.sprite = Sprites.GetValueOrDefault("UI_Pill");
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = bg;
        var hl = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(12, 12, 0, 0);
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        var le = chip.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 30f;
        le.minHeight = 30f;
        TMP_Text t = Txt(chip, "Text", "", _nunito800, 13f, fg, 0f, false);
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    private static TMP_InputField InputField(Transform parent, string name, string placeholder)
    {
        RectTransform field = Framed(parent, name, 10, FrostboundUI.Bg, FrostboundUI.Border, out RectTransform content);
        var le = field.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 46f;
        le.minHeight = 46f;
        RectTransform area = Node("TextArea", content);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(14f, 4f);
        area.offsetMax = new Vector2(-14f, -4f);
        area.gameObject.AddComponent<RectMask2D>();

        TMP_Text ph = Txt(area, "Placeholder", placeholder, _nunito500, 15f, FrostboundUI.Disabled, 0f, false);
        TMP_Text txt = Txt(area, "Text", "", _nunito700, 15f, FrostboundUI.Text, 0f, false);
        foreach (TMP_Text t in new[] { ph, txt })
        {
            Object.DestroyImmediate(t.GetComponent<LayoutElement>());
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }

        var input = field.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = txt;
        input.placeholder = ph;
        input.fontAsset = _nunito700;
        input.pointSize = 15f;
        input.characterLimit = 20;
        input.caretColor = FrostboundUI.Ice;
        input.customCaretColor = true;
        input.selectionColor = new Color(FrostboundUI.Ice.r, FrostboundUI.Ice.g, FrostboundUI.Ice.b, 0.35f);
        input.targetGraphic = field.GetComponent<Image>();
        input.transition = Selectable.Transition.None;
        var fx = field.gameObject.AddComponent<UIButtonFx>();
        fx.border = field.GetComponent<Image>();
        fx.normalBorder = FrostboundUI.Border;
        fx.hoverScale = 1f;
        return input;
    }

    private static Button PrimaryButton(Transform parent, string name, string label, float width, float height, float fontSize)
    {
        RectTransform root = Framed(parent, name, 12, FrostboundUI.Ice, Color.white, out RectTransform content);
        root.sizeDelta = new Vector2(width, height);
        var le = root.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;
        var hl = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(24, 24, 0, 0);
        hl.spacing = 14f;
        hl.childAlignment = name == "BtnStart" ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;
        if (name != "BtnStart")
        {
            Image icon = Img(content, "Icon", Sprites.GetValueOrDefault("UI_Diamond"), FrostboundUI.Bg);
            var ile = icon.gameObject.AddComponent<LayoutElement>();
            ile.preferredWidth = 18f;
            ile.preferredHeight = 18f;
        }
        TMP_Text t = Txt(content, "Label", label, _cinzel800, fontSize, FrostboundUI.Bg, 2f, true);
        t.alignment = name == "BtnStart" ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
        return MakeButton(root, Color.white);
    }

    private static Button SecondaryButton(Transform parent, string name, string label, float width, float height, out TMP_Text text)
    {
        RectTransform root = Framed(parent, name, 12, FrostboundUI.Surface, FrostboundUI.Border, out RectTransform content);
        root.sizeDelta = new Vector2(width, height);
        var le = root.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;
        var hl = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(24, 24, 0, 0);
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        text = Txt(content, "Label", label, _cinzel600, 18f, FrostboundUI.Text, 2f, true);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return MakeButton(root, FrostboundUI.Border);
    }

    private static Button SmallButton(Transform parent, string name, string label)
    {
        RectTransform root = Framed(parent, name, 10, FrostboundUI.Surface, FrostboundUI.Border, out RectTransform content);
        root.sizeDelta = new Vector2(124f, 44f);
        var le = root.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 124f;
        le.preferredHeight = 44f;
        var hl = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(16, 16, 0, 0);
        hl.spacing = 8f;
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;
        Image icon = Img(content, "Chevron", Sprites.GetValueOrDefault("UI_Chevron"), FrostboundUI.Text);
        var ile = icon.gameObject.AddComponent<LayoutElement>();
        ile.preferredWidth = 16f;
        ile.preferredHeight = 16f;
        TMP_Text t = Txt(content, "Label", label, _nunito800, 14f, FrostboundUI.Text, 1f, true);
        t.alignment = TextAlignmentOptions.Midline;
        return MakeButton(root, FrostboundUI.Border);
    }

    private static Button MakeButton(RectTransform root, Color normalBorder)
    {
        var b = root.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = root.GetComponent<Image>();
        var fx = root.gameObject.AddComponent<UIButtonFx>();
        fx.border = root.GetComponent<Image>();
        fx.normalBorder = normalBorder;
        fx.activeBorder = FrostboundUI.Ice;
        return b;
    }

    // Caja con borde de 2 px: la raíz es el borde y "Fill" el relleno.
    private static RectTransform Framed(Transform parent, string name, int radius, Color fill, Color border, out RectTransform content)
    {
        RectTransform root = Node(name, parent);
        Sprite s = Sprites.GetValueOrDefault("UI_Round" + radius) ?? Sprites.GetValueOrDefault("UI_Round12");
        Image b = root.gameObject.AddComponent<Image>();
        b.sprite = s;
        b.type = Image.Type.Sliced;
        b.color = border;
        content = Node("Fill", root);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(2f, 2f);
        content.offsetMax = new Vector2(-2f, -2f);
        Image f = content.gameObject.AddComponent<Image>();
        f.sprite = s;
        f.type = Image.Type.Sliced;
        f.color = fill;
        f.raycastTarget = false;
        return root;
    }

    private static Image Background(Transform parent, string name, string sprite)
    {
        Image img = Img(parent, name, Sprites.GetValueOrDefault(sprite), Color.white);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(FrostboundUI.ReferenceWidth, FrostboundUI.ReferenceHeight);
        var fit = img.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = FrostboundUI.ReferenceWidth / FrostboundUI.ReferenceHeight;
        return img;
    }

    private static TMP_Text Txt(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, float trackingPx, bool upper)
    {
        RectTransform rt = Node(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.text = text;
        t.characterSpacing = trackingPx > 0f ? FrostboundUI.Tracking(trackingPx, size) : 0f;
        t.fontStyle = upper ? FontStyles.UpperCase : FontStyles.Normal;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.richText = true;
        t.raycastTarget = false;
        t.alignment = TextAlignmentOptions.TopLeft;
        rt.gameObject.AddComponent<LayoutElement>();
        return t;
    }

    private static void SetLineHeight(TMP_Text t, float cssLineHeight)
    {
        if (t.font == null) return;
        var face = t.font.faceInfo;
        float natural = face.lineHeight / face.pointSize;
        t.lineSpacing = (cssLineHeight - natural) * 100f;
    }

    private static Image Img(Transform parent, string name, Sprite sprite, Color color)
    {
        RectTransform rt = Node(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = true;
        return img;
    }

    private static VerticalLayoutGroup VLayout(GameObject go, float spacing, TextAnchor align)
    {
        var vl = go.AddComponent<VerticalLayoutGroup>();
        vl.spacing = spacing;
        vl.childAlignment = align;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandWidth = align != TextAnchor.UpperLeft && align != TextAnchor.MiddleLeft ? true : true;
        vl.childForceExpandHeight = false;
        return vl;
    }

    private static RectTransform Node(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = min;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // ---------- Proyecto ----------

    private static void SetupBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MenuScene, true),
            new EditorBuildSettingsScene(GameScene, true)
        };
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.path != MenuScene && s.path != GameScene && File.Exists(s.path)) scenes.Add(s);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void AddLoadoutToPlayer(PlumagePalette palette)
    {
        Scene game = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Additive);
        foreach (GameObject go in game.GetRootGameObjects())
        {
            if (go.name != "Player") continue;
            PlayerLoadout loadout = go.GetComponent<PlayerLoadout>() ?? go.AddComponent<PlayerLoadout>();
            loadout.palette = palette;
            EditorSceneManager.MarkSceneDirty(game);
        }
        int layer = LayerMask.NameToLayer(PreviewLayer);
        if (layer >= 0)
            foreach (GameObject go in game.GetRootGameObjects())
                foreach (Camera cam in go.GetComponentsInChildren<Camera>(true))
                    cam.cullingMask &= ~(1 << layer);
        EditorSceneManager.SaveScene(game);
        EditorSceneManager.CloseScene(game, true);
    }
}
