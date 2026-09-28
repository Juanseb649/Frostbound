using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupPenguinWardrobe
{
    public const string ModelPath = "Assets/Models/Penguin/Penguin_Rigged.fbx";
    public const string OutfitModelDir = "Assets/Models/Outfits";
    public const string PrefabPath = "Assets/Prefabs/Penguin_Rigged.prefab";
    public const string ToonMatDir = "Assets/Materials/Toon";
    public const string OutfitDataDir = "Assets/Data/Outfits";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    private static readonly (string asset, string display, string fbx, OutfitSlot slot, OutfitSlot[] also)[] Outfits =
    {
        ("Casco_Cuernos", "Casco con cuernos", "Outfit_HornedHelmet", OutfitSlot.Head, new OutfitSlot[0]),
        ("Sombrero_Mago", "Sombrero de mago", "Outfit_WizardHat", OutfitSlot.Head, new OutfitSlot[0]),
        ("Armadura", "Armadura de placas", "Outfit_Armor", OutfitSlot.Chest, new OutfitSlot[0]),
        ("Traje_Capucha", "Traje con capucha", "Outfit_HoodedSuit", OutfitSlot.Back, new[] { OutfitSlot.Head }),
    };

    private static readonly Dictionary<string, string> ClassOutfit = new Dictionary<string, string>
    {
        { "Knight_Caballero", "Armadura" },
        { "Mage_Mago", "Sombrero_Mago" },
        { "Viking_Vikingo", "Casco_Cuernos" },
        { "Ninja", "Traje_Capucha" },
    };

    [MenuItem("Tools/Frostbound/Configurar Pingüino Vestible")]
    public static void Setup()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
        {
            FrostboundBridge.Dialog("Frostbound", "No se encontró " + ModelPath + ".\nGenera el modelo con Blender/frostbound_penguin_rig.py.", "OK");
            return;
        }
        if (!FrostboundBridge.ConfirmSave()) return;

        ConfigureImporters();
        Material penguinMat = ToonMaterial("Toon_Penguin", 3f, new Color(0.10f, 0.16f, 0.42f));
        Material outfitMat = ToonMaterial("Toon_Outfit", 2f, Color.white);
        Dictionary<string, OutfitItem> items = CreateOutfitItems(outfitMat);
        GameObject prefab = BuildPrefab(penguinMat, out string orientationNote, out float height);
        AssignClassOutfits(items);
        ReplacePlayerModel(prefab, items);
        SetupVillage.BuildScene(false);

        FrostboundBridge.Dialog("Frostbound",
            "Pingüino vestible listo.\n\n" +
            "• Prefab: " + PrefabPath + " (altura " + height.ToString("0.00") + " m" + orientationNote + ")\n" +
            "• Shader: Frostbound/Toon (materiales en " + ToonMatDir + ")\n" +
            "• Prendas: " + OutfitDataDir + "\n" +
            "• Clases: Caballero→Armadura, Mago→Sombrero, Vikingo→Casco, Ninja→Capucha\n" +
            "• Poblado reconstruido con pingüinos vestidos.\n\n" +
            "En Play: teclas 1-4 ponen/quitan prendas, 0 quita todo.", "OK");
    }

    [MenuItem("Tools/Frostbound/Actualizar materiales toon")]
    public static void RefreshToonMaterials()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models" }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

        Shader shader = Shader.Find("Frostbound/Toon");
        int updated = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { ToonMatDir }))
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null) continue;
            m.shader = shader;
            if (m.name.StartsWith("Toon_Penguin")) m.SetFloat("_VertexColorMode", 3f);
            m.SetColor("_BellyColor", new Color(0.97f, 0.97f, 0.96f));
            m.SetColor("_AccentColor", new Color(1f, 0.62f, 0.12f));
            m.SetColor("_PupilColor", new Color(0.03f, 0.03f, 0.05f));
            m.SetColor("_ShadowColor", new Color(0.8f, 0.82f, 0.9f));
            m.SetFloat("_ShadowThreshold", 0f);
            m.SetFloat("_ShadowSoftness", 0.02f);
            m.SetFloat("_AmbientStrength", 0.3f);
            m.SetFloat("_RimStrength", 0.15f);
            m.SetFloat("_RimThreshold", 0.72f);
            m.SetColor("_OutlineColor", new Color(0.04f, 0.05f, 0.1f));
            m.SetFloat("_OutlineWidth", 2.2f);
            m.SetVector("_EyeCenter", new Vector4(0.043f, 0.84f, 0f, 0f));
            m.SetVector("_EyeSize", new Vector4(0.042f, 0.055f, 0f, 0f));
            m.SetVector("_PupilSize", new Vector4(0.021f, 0.025f, 0.012f, -0.006f));
            m.SetVector("_BellyShape", new Vector4(0.35f, 0.27f, 0.28f, 0.28f));
            m.SetVector("_BeakShape", new Vector4(0.205f, 0.23f, 0.012f, 0.145f));
            m.SetVector("_BeakRange", new Vector4(0.625f, 0.815f, 0f, 0f));
            EditorUtility.SetDirty(m);
            updated++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Wardrobe] Materiales toon actualizados: " + updated);
    }

    private static void ConfigureImporters()
    {
        var paths = new List<string> { ModelPath };
        if (Directory.Exists(OutfitModelDir))
            foreach (string f in Directory.GetFiles(OutfitModelDir, "*.fbx")) paths.Add(f.Replace('\\', '/'));

        foreach (string p in paths)
        {
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp == null) continue;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importBlendShapes = false;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.optimizeGameObjects = false;
            imp.SaveAndReimport();
        }
    }

    public static Material ToonMaterial(string name, float vertexMode, Color baseColor)
    {
        EnsureFolder(ToonMatDir);
        string path = ToonMatDir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Frostbound/Toon");
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader)
        {
            m.shader = shader;
        }
        m.SetFloat("_VertexColorMode", vertexMode);
        m.SetColor("_BaseColor", baseColor);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    public static Material ToonPenguinVariant(string name, Color plumage)
    {
        Material baseMat = AssetDatabase.LoadAssetAtPath<Material>(ToonMatDir + "/Toon_Penguin.mat");
        string path = ToonMatDir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Frostbound/Toon"));
            AssetDatabase.CreateAsset(m, path);
        }
        if (baseMat != null) m.CopyPropertiesFromMaterial(baseMat);
        m.SetFloat("_VertexColorMode", 3f);
        m.SetColor("_BaseColor", plumage);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Dictionary<string, OutfitItem> CreateOutfitItems(Material outfitMat)
    {
        EnsureFolder(OutfitDataDir);
        var result = new Dictionary<string, OutfitItem>();
        foreach (var o in Outfits)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(OutfitModelDir + "/" + o.fbx + ".fbx");
            if (model == null)
            {
                Debug.LogWarning("[Wardrobe] Falta " + o.fbx + ".fbx");
                continue;
            }
            string path = OutfitDataDir + "/" + o.asset + ".asset";
            OutfitItem item = AssetDatabase.LoadAssetAtPath<OutfitItem>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<OutfitItem>();
                AssetDatabase.CreateAsset(item, path);
            }
            item.displayName = o.display;
            item.slot = o.slot;
            item.alsoOccupies = o.also;
            item.attachMode = OutfitAttachMode.Skinned;
            item.prefab = model;
            item.materialOverride = outfitMat;
            EditorUtility.SetDirty(item);
            result[o.asset] = item;
        }
        return result;
    }

    public static OutfitItem LoadItem(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<OutfitItem>(OutfitDataDir + "/" + assetName + ".asset");
    }

    private static GameObject BuildPrefab(Material penguinMat, out string orientationNote, out float height)
    {
        EnsureFolder("Assets/Prefabs");
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var root = new GameObject("Penguin_Rigged");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        inst.name = "Model";
        inst.transform.SetParent(root.transform, false);

        orientationNote = "";
        Transform chest = FindDeep(inst.transform, "Anchor_Chest");
        Transform rigRoot = FindDeep(inst.transform, "Root");
        if (chest != null && rigRoot != null)
        {
            Vector3 d = chest.position - rigRoot.position;
            d.y = 0f;
            if (Vector3.Dot(d, root.transform.forward) < 0f)
            {
                inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                orientationNote = ", girado 180° para mirar a +Z";
            }
        }

        height = 1f;
        Bounds? b = null;
        foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < mats.Length; i++) mats[i] = penguinMat;
            r.sharedMaterials = mats;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            if (b == null) b = r.bounds;
            else { Bounds bb = b.Value; bb.Encapsulate(r.bounds); b = bb; }
        }
        if (b.HasValue)
        {
            height = b.Value.size.y;
            inst.transform.localPosition = new Vector3(0f, -b.Value.min.y, 0f);
        }

        root.AddComponent<PenguinOutfit>();
        root.AddComponent<PenguinRigAnimator>();
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static void AssignClassOutfits(Dictionary<string, OutfitItem> items)
    {
        foreach (var pair in ClassOutfit)
        {
            CharacterClass cls = AssetDatabase.LoadAssetAtPath<CharacterClass>("Assets/Data/Classes/" + pair.Key + ".asset");
            if (cls == null || !items.TryGetValue(pair.Value, out OutfitItem item)) continue;
            cls.startingOutfit = new[] { item };
            EditorUtility.SetDirty(cls);
        }
    }

    private static void ReplacePlayerModel(GameObject prefab, Dictionary<string, OutfitItem> items)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogWarning("[Wardrobe] No hay 'Player' en " + ScenePath);
            return;
        }

        Transform old = player.transform.Find("Penguin");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
        model.name = "Penguin";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        PenguinBodySway anim = player.GetComponent<PenguinBodySway>();
        if (anim != null) anim.model = model.transform;

        CharacterStats stats = player.GetComponent<CharacterStats>();
        PenguinRigAnimator rigAnim = model.GetComponent<PenguinRigAnimator>();
        if (rigAnim != null && stats != null) rigAnim.referenceSpeed = Mathf.Max(1f, stats.MoveSpeed);

        CapsuleCollider col = player.GetComponent<CapsuleCollider>();
        Bounds? b = null;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (b == null) b = r.bounds;
            else { Bounds bb = b.Value; bb.Encapsulate(r.bounds); b = bb; }
        }
        if (col != null && b.HasValue)
        {
            col.height = b.Value.size.y;
            col.radius = Mathf.Max(b.Value.size.x, b.Value.size.z) * 0.25f;
            col.center = new Vector3(0f, b.Value.size.y * 0.5f, 0f);
        }

        OutfitTester tester = player.GetComponent<OutfitTester>();
        if (tester == null) tester = player.AddComponent<OutfitTester>();
        tester.outfit = model.GetComponent<PenguinOutfit>();
        tester.items = new List<OutfitItem>();
        foreach (var o in Outfits)
            if (items.TryGetValue(o.asset, out OutfitItem item)) tester.items.Add(item);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Transform FindDeep(Transform t, string name)
    {
        foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
            if (c.name == name) return c;
        return null;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
