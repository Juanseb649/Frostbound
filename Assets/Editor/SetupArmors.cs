using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Frostbound > Equipamiento > Armaduras de botín
// Sets de caballero y shinobi que solo se consiguen como botín (modelos de Blender/frostbound_armors.py):
// materiales toon, piezas (casco, torso, botas), objetos, sets, iconos y alta en la base de datos.
// SetupInventory también lo ejecuta, así rehacer el inventario no las borra.
public static class SetupArmors
{
    private const string ItemDir = "Assets/Data/Items";
    private const string SetDir = ItemDir + "/Sets";
    private const string PieceDir = "Assets/Data/Outfits/Pieces";
    private const string IconDir = "Assets/Frostbound/UI/Icons/Armor";
    private const string BootPrefabDir = "Assets/Prefabs/Gear";
    private const string BootMatDir = "Assets/Materials/Gear";
    private const string DatabasePath = ItemDir + "/ItemDatabase.asset";

    private static readonly string[] KnightChest = { "Chest", "Surcoat", "Cape", "Shoulder_L", "Shoulder_R", "Gauntlet_L", "Gauntlet_R" };
    private static readonly string[] NinjaChest = { "Suit", "Wraps_L", "Wraps_R", "Katana_Back" };

    private class ArmorSetSpec
    {
        public string key, fbx, setName, helmPart;
        public string[] chestParts;
        public string bootBody, bootTrim;
        public Piece helm, chest, boots;
        public ArmorSet.Bonus[] bonuses;
    }

    private class Piece
    {
        public string id, name, desc;
        public ItemRarity rarity;
        public int level, armor;
        public StatModifier[] stats;
        public StatModifier[] requires = new StatModifier[0];
    }

    private static StatModifier S(StatType t, int v) => new StatModifier(t, v);
    private static readonly StatType Str = StatType.Strength, Man = StatType.Mana, Agi = StatType.Agility, Hp = StatType.Health;

    private static ArmorSet.Bonus Bonus(int pieces, int armor, params StatModifier[] stats)
        => new ArmorSet.Bonus { piecesRequired = pieces, armor = armor, stats = new List<StatModifier>(stats) };

    private static ArmorSetSpec[] Sets()
    {
        return new[]
        {
            new ArmorSetSpec
            {
                key = "Silver", fbx = "Frostbound_Silver_Gear", setName = "Set del Guardián de Plata",
                helmPart = "Helm", chestParts = KnightChest, bootBody = "#C7CFDA", bootTrim = "#D9B24C",
                helm = new Piece { id = "silver_helm", name = "Yelmo del Guardián de Plata", rarity = ItemRarity.Rare, level = 8, armor = 8, stats = new[] { S(Hp, 2), S(Str, 1) }, requires = new[] { S(Str, 12) },
                    desc = "Plata pulida y un penacho blanco. Los guardianes de la catedral helada nunca bajaban la cabeza." },
                chest = new Piece { id = "silver_chest", name = "Coraza del Guardián de Plata", rarity = ItemRarity.Rare, level = 9, armor = 14, stats = new[] { S(Str, 2), S(Hp, 2) }, requires = new[] { S(Str, 14) },
                    desc = "Lleva grabado un sol de oro que ya nadie recuerda haber visto salir." },
                boots = new Piece { id = "silver_boots", name = "Grebas del Guardián de Plata", rarity = ItemRarity.Rare, level = 8, armor = 6, stats = new[] { S(Hp, 2) },
                    desc = "Suenan como campanas al subir la escalinata." },
                bonuses = new[] { Bonus(2, 0, S(Hp, 3)), Bonus(3, 8, S(Str, 3), S(Hp, 2)) },
            },
            new ArmorSetSpec
            {
                key = "BlackWolf", fbx = "Frostbound_BlackWolf_Gear", setName = "Set del Lobo Negro",
                helmPart = "Helm", chestParts = KnightChest, bootBody = "#1F2024", bootTrim = "#6B1A17",
                helm = new Piece { id = "blackwolf_helm", name = "Yelmo del Lobo Negro", rarity = ItemRarity.Epic, level = 16, armor = 12, stats = new[] { S(Str, 3), S(Hp, 2) }, requires = new[] { S(Str, 22) },
                    desc = "Un hocico de hierro y dos ojos que arden en rojo. Quien lo lleva deja de sentir el dolor." },
                chest = new Piece { id = "blackwolf_chest", name = "Coraza del Lobo Negro", rarity = ItemRarity.Epic, level = 17, armor = 22, stats = new[] { S(Str, 4), S(Hp, 3) }, requires = new[] { S(Str, 24) },
                    desc = "Hierro negro con púas y una capa hecha jirones. Dicen que la armadura elige a su portador." },
                boots = new Piece { id = "blackwolf_boots", name = "Grebas del Lobo Negro", rarity = ItemRarity.Epic, level = 16, armor = 9, stats = new[] { S(Str, 2), S(Hp, 1) },
                    desc = "Cada paso deja la nieve teñida de ceniza." },
                bonuses = new[] { Bonus(2, 0, S(Str, 4)), Bonus(3, 12, S(Str, 4), S(Hp, 4)) },
            },
            new ArmorSetSpec
            {
                key = "LoneWolf", fbx = "Frostbound_LoneWolf_Gear", setName = "Set del Lobo Manco",
                helmPart = "Hood", chestParts = NinjaChest, bootBody = "#383D46", bootTrim = "#B5652A",
                helm = new Piece { id = "lonewolf_hood", name = "Capucha del Lobo Manco", rarity = ItemRarity.Rare, level = 7, armor = 5, stats = new[] { S(Agi, 2) }, requires = new[] { S(Agi, 12) },
                    desc = "Capucha gastada y una coleta atada con un cordón ocre." },
                chest = new Piece { id = "lonewolf_suit", name = "Atuendo del Lobo Manco", rarity = ItemRarity.Rare, level = 8, armor = 9, stats = new[] { S(Agi, 3), S(Str, 1) }, requires = new[] { S(Agi, 14) },
                    desc = "Bufanda ocre y una aleta de hierro con mecanismos que aún chirrían." },
                boots = new Piece { id = "lonewolf_boots", name = "Tabi del Lobo Manco", rarity = ItemRarity.Rare, level = 7, armor = 4, stats = new[] { S(Agi, 2) },
                    desc = "Remendados mil veces. Nunca resbalan." },
                bonuses = new[] { Bonus(2, 0, S(Agi, 3)), Bonus(3, 4, S(Agi, 3), S(Str, 2)) },
            },
            new ArmorSetSpec
            {
                key = "Owl", fbx = "Frostbound_Owl_Gear", setName = "Set del Búho",
                helmPart = "Hood", chestParts = NinjaChest, bootBody = "#3D3833", bootTrim = "#6B4A8E",
                helm = new Piece { id = "owl_hood", name = "Capucha del Búho", rarity = ItemRarity.Epic, level = 14, armor = 9, stats = new[] { S(Agi, 3), S(Man, 1) }, requires = new[] { S(Agi, 20) },
                    desc = "Dos penachos de plumas y una mirada que no parpadea." },
                chest = new Piece { id = "owl_suit", name = "Manto del Búho", rarity = ItemRarity.Epic, level = 15, armor = 16, stats = new[] { S(Agi, 4), S(Hp, 2) }, requires = new[] { S(Agi, 22) },
                    desc = "Gorguera y manto de plumas. El viejo maestro enseñaba a matar sin ser visto." },
                boots = new Piece { id = "owl_boots", name = "Tabi del Búho", rarity = ItemRarity.Epic, level = 14, armor = 7, stats = new[] { S(Agi, 3) },
                    desc = "Tan silenciosos como el vuelo de un búho." },
                bonuses = new[] { Bonus(2, 0, S(Agi, 4)), Bonus(3, 8, S(Agi, 4), S(Man, 3)) },
            },
        };
    }

    [MenuItem("Tools/Frostbound/Equipamiento/Armaduras de botín")]
    public static void SetupMenu()
    {
        int count = Setup();
        FrostboundBridge.Dialog("Frostbound",
            "Armaduras de botín listas: " + count + " objetos (caballero y shinobi) en " + ItemDir + ".\n\n" +
            "Caen como botín según su nivel. En Play, G da un objeto al azar.", "OK");
    }

    // Devuelve cuántos objetos creó.
    public static int Setup()
    {
        Dictionary<string, Material> mats = FrostboundGearSetup.CreateMaterials();
        FrostboundGearSetup.EnsureFolder(ItemDir);
        FrostboundGearSetup.EnsureFolder(SetDir);
        FrostboundGearSetup.EnsureFolder(PieceDir);
        FrostboundGearSetup.EnsureFolder(IconDir);
        FrostboundGearSetup.EnsureFolder(BootPrefabDir);
        FrostboundGearSetup.EnsureFolder(BootMatDir);

        ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
        Mesh boot = SetupInventory.BuildBootMesh();
        int count = 0;

        foreach (ArmorSetSpec s in Sets())
        {
            string fbxPath = FrostboundGearSetup.GearModelDir + "/" + s.fbx + ".fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath) == null)
            {
                Debug.LogWarning("[Armaduras] Falta " + fbxPath);
                continue;
            }
            Remap(fbxPath, mats);
            OutfitItem gear = FrostboundGearSetup.GearItem("Gear_" + s.key, s.fbx);
            if (gear == null) continue;

            OutfitItem helmOutfit = MakePiece("Pieza_" + s.key + "_Casco", s.helm.name, gear, new[] { s.helmPart }, OutfitSlot.Head, new OutfitSlot[0]);
            OutfitItem chestOutfit = MakePiece("Pieza_" + s.key + "_Torso", s.chest.name, gear, s.chestParts, OutfitSlot.Chest, new[] { OutfitSlot.Back });
            OutfitItem bootOutfit = Boots("Botas_" + s.key, s.boots.name, s.bootBody, s.bootTrim, boot);

            var items = new List<ItemDefinition>
            {
                Item(s.helm, EquipSlot.Head, helmOutfit, RenderIcon(gear.prefab, new[] { s.helmPart }, "Icon_" + s.helm.id)),
                Item(s.chest, EquipSlot.Chest, chestOutfit, RenderIcon(gear.prefab, s.chestParts, "Icon_" + s.chest.id)),
                Item(s.boots, EquipSlot.Feet, bootOutfit, RenderIcon(bootOutfit.prefab, null, "Icon_" + s.boots.id)),
            };

            ArmorSet set = LoadOrCreate<ArmorSet>(SetDir + "/Set_" + s.key + ".asset");
            set.displayName = s.setName;
            set.pieces = items;
            set.bonuses = new List<ArmorSet.Bonus>(s.bonuses);
            EditorUtility.SetDirty(set);

            foreach (ItemDefinition item in items)
            {
                item.armorSet = set;
                EditorUtility.SetDirty(item);
                if (db != null && !db.items.Contains(item)) db.items.Add(item);
                count++;
            }
        }

        // Los escudos nuevos también viven fuera de SetupInventory: que rehacer el inventario no los quite.
        if (db != null)
        {
            foreach (string id in SetupShields.Ids)
            {
                var shield = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemDir + "/" + id + ".asset");
                if (shield != null && !db.items.Contains(shield)) db.items.Add(shield);
            }
            EditorUtility.SetDirty(db);
        }
        AssetDatabase.SaveAssets();
        return count;
    }

    private static void Remap(string path, Dictionary<string, Material> mats)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        imp.importAnimation = false;
        imp.importCameras = false;
        imp.importLights = false;
        imp.importBlendShapes = false;
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.optimizeGameObjects = false;
        imp.isReadable = true;
        foreach (var pair in mats)
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
        imp.SaveAndReimport();
    }

    private static OutfitItem MakePiece(string asset, string display, OutfitItem gear, string[] parts, OutfitSlot slot, OutfitSlot[] also)
    {
        OutfitItem item = LoadOrCreate<OutfitItem>(PieceDir + "/" + asset + ".asset");
        item.displayName = display;
        item.prefab = gear.prefab;
        item.materialOverride = null;
        item.attachMode = OutfitAttachMode.Skinned;
        item.onlyParts = parts;
        item.slot = slot;
        item.alsoOccupies = also;
        EditorUtility.SetDirty(item);
        return item;
    }

    // Mismas botas que las de clase (SetupInventory), con los colores del set.
    private static OutfitItem Boots(string name, string display, string body, string trim, Mesh mesh)
    {
        Material b = SetupInventory.ToonMaterial(BootMatDir + "/" + name + "_Body.mat", SetupInventory.Hex(body));
        Material t = SetupInventory.ToonMaterial(BootMatDir + "/" + name + "_Trim.mat", SetupInventory.Hex(trim));
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { b, t };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, BootPrefabDir + "/" + name + ".prefab");
        Object.DestroyImmediate(go);

        OutfitItem item = LoadOrCreate<OutfitItem>(PieceDir + "/" + name + ".asset");
        item.displayName = display;
        item.prefab = prefab;
        item.materialOverride = null;
        item.attachMode = OutfitAttachMode.FitToBones;
        item.fitBones = new[] { "Foot_L", "Foot_R" };
        item.slot = OutfitSlot.Feet;
        item.alsoOccupies = new OutfitSlot[0];
        item.positionOffset = new Vector3(0f, -0.1f, 0.02f);
        item.rotationOffset = Vector3.zero;
        item.scale = new Vector3(0.88f, 2.1f, 0.98f);
        EditorUtility.SetDirty(item);
        return item;
    }

    private static ItemDefinition Item(Piece p, EquipSlot slot, OutfitItem outfit, Sprite icon)
    {
        ItemDefinition item = LoadOrCreate<ItemDefinition>(ItemDir + "/" + p.id + ".asset");
        item.id = p.id;
        item.displayName = p.name;
        item.description = p.desc ?? "";
        if (icon != null) item.icon = icon;
        item.category = ItemCategory.Armor;
        item.rarity = p.rarity;
        item.equipSlot = slot;
        item.maxStack = 1;
        item.requiredLevel = p.level;
        item.armor = p.armor;
        item.stats = new List<StatModifier>(p.stats);
        item.requiredStats = new List<StatModifier>(p.requires);
        item.outfit = outfit;
        EditorUtility.SetDirty(item);
        return item;
    }

    // ---------- Iconos ----------

    // Renderiza solo las piezas indicadas del modelo, de frente y algo girado, sobre fondo transparente.
    private static Sprite RenderIcon(GameObject model, string[] parts, string name)
    {
        if (model == null) return null;
        const int size = 256;
        int layer = LayerMask.NameToLayer("UIPreview");
        if (layer < 0) layer = 31;

        var root = new GameObject("IconoTemp") { hideFlags = HideFlags.HideAndDontSave };
        root.transform.position = new Vector3(0f, -800f, 0f);
        GameObject m = Object.Instantiate(model, root.transform, false);
        m.transform.localRotation = Quaternion.Euler(8f, 200f, 0f);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

        Bounds b = new Bounds(root.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            bool keep = parts == null;
            if (!keep)
                foreach (string p in parts)
                    if (string.Equals(r.name, p, System.StringComparison.OrdinalIgnoreCase)) keep = true;
            r.enabled = keep;
            if (!keep) continue;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }

        var camGo = new GameObject("IconoCam") { hideFlags = HideFlags.HideAndDontSave };
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y) * 1.1f + 0.02f;
        cam.transform.position = b.center - Vector3.forward * 5f;
        cam.transform.rotation = Quaternion.identity;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << layer;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 20f;

        var light = new GameObject("IconoLuz") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
        light.intensity = 1.1f;
        light.cullingMask = 1 << layer;

        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        cam.targetTexture = rt;
        var req = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
        if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req)) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);
        else cam.Render();

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        string path = IconDir + "/" + name + ".png";
        File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(light.gameObject);
        Object.DestroyImmediate(root);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.filterMode = FilterMode.Bilinear;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.spritePixelsPerUnit = 256f;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
