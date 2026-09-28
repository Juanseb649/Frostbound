using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

// Prepara el equipo del paquete Frostbound_Gear adaptado al esqueleto:
// materiales toon por pieza, remapeo de los FBX, prendas (OutfitItem), datos de clases y paleta de plumaje.
public static class FrostboundGearSetup
{
    public const string GearModelDir = "Assets/Models/Gear";
    public const string GearMatDir = "Assets/Materials/Gear";
    public const string HairTexture = "Assets/Models/Gear/Textures/Rocker_Hair_Curls.png";
    public const string PalettePath = "Assets/Data/PlumagePalette.asset";
    private const string SourceDirName = "Blender/Gear_Source";

    private static readonly (string cls, string fbx, string item)[] ClassGear =
    {
        ("Knight_Caballero", "Frostbound_Knight_Gear", "Gear_Knight"),
        ("Mage_Mago", "Frostbound_Mage_Gear", "Gear_Mage"),
        ("Ninja", "Frostbound_Ninja_Gear", "Gear_Ninja"),
        ("Viking_Vikingo", "Frostbound_Viking_Gear", "Gear_Viking"),
    };

    [MenuItem("Tools/Frostbound/Equipamiento/Preparar equipo de clases")]
    public static void SetupAll()
    {
        Setup();
        FrostboundBridge.Dialog("Frostbound", "Equipo listo: materiales, prendas de clase, datos de clases y paleta de plumaje.", "OK");
    }

    public static void Setup()
    {
        Dictionary<string, Material> mats = CreateMaterials();
        RemapModels(mats);
        PlumagePalette palette = CreatePalette();
        CreateItemsAndClasses();
        SetupPrefab();
        AssetDatabase.SaveAssets();
    }

    // ---------- Materiales ----------

    private static Dictionary<string, Material> CreateMaterials()
    {
        EnsureFolder(GearMatDir);
        var result = new Dictionary<string, Material>();
        string sourceDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, SourceDirName);
        if (!Directory.Exists(sourceDir))
        {
            Debug.LogWarning("[Gear] No se encontró " + sourceDir + " (archivos .mtl originales).");
            return result;
        }
        Shader shader = Shader.Find("Frostbound/Toon");
        Texture2D hair = AssetDatabase.LoadAssetAtPath<Texture2D>(HairTexture);

        foreach (string mtl in Directory.GetFiles(sourceDir, "*.mtl", SearchOption.AllDirectories))
        {
            foreach (var entry in ReadMtl(mtl))
            {
                string path = GearMatDir + "/" + entry.name + ".mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(shader);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = shader;
                m.SetFloat("_VertexColorMode", 0f);
                m.SetColor("_BaseColor", entry.kd);
                m.SetColor("_EmissionColor", entry.ke * 0.6f);
                m.SetFloat("_Cull", 0f);
                m.SetColor("_ShadowColor", new Color(0.8f, 0.82f, 0.9f));
                m.SetFloat("_AmbientStrength", 0.3f);
                m.SetFloat("_OutlineWidth", 2f);
                if (entry.texture && hair != null) m.SetTexture("_BaseMap", hair);
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                result[entry.name] = m;
            }
        }
        return result;
    }

    private struct MtlEntry
    {
        public string name;
        public Color kd, ke;
        public bool texture;
    }

    private static List<MtlEntry> ReadMtl(string path)
    {
        var list = new List<MtlEntry>();
        MtlEntry cur = default;
        bool has = false;
        foreach (string raw in File.ReadAllLines(path))
        {
            string[] p = raw.Trim().Split(' ');
            if (p.Length == 0) continue;
            switch (p[0])
            {
                case "newmtl":
                    if (has) list.Add(cur);
                    cur = new MtlEntry { name = p[1], kd = Color.white, ke = Color.black };
                    has = true;
                    break;
                case "Kd":
                    if (p.Length >= 4) cur.kd = Parse(p);
                    break;
                case "Ke":
                    if (p.Length >= 4) cur.ke = Parse(p);
                    break;
                case "map_Kd":
                    cur.texture = true;
                    break;
            }
        }
        if (has) list.Add(cur);
        return list;
    }

    private static Color Parse(string[] p)
    {
        float f(int i) => float.Parse(p[i], CultureInfo.InvariantCulture);
        return new Color(f(1), f(2), f(3), 1f);
    }

    // ---------- Modelos ----------

    private static void RemapModels(Dictionary<string, Material> mats)
    {
        if (!Directory.Exists(GearModelDir)) return;
        foreach (string file in Directory.GetFiles(GearModelDir, "*.fbx"))
        {
            string path = file.Replace('\\', '/');
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importBlendShapes = false;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.optimizeGameObjects = false;
            // Las capas y telas sueltas usan Cloth, que necesita leer la malla.
            imp.isReadable = true;
            foreach (var pair in mats)
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            imp.SaveAndReimport();
        }
    }

    // ---------- Paleta ----------

    public static PlumagePalette CreatePalette()
    {
        EnsureFolder("Assets/Data");
        PlumagePalette p = AssetDatabase.LoadAssetAtPath<PlumagePalette>(PalettePath);
        if (p == null)
        {
            p = ScriptableObject.CreateInstance<PlumagePalette>();
            AssetDatabase.CreateAsset(p, PalettePath);
        }
        p.entries = new List<PlumagePalette.Entry>
        {
            E("azul", "Azul", "#1F4FB4"), E("verde", "Verde", "#3F9B3A"), E("rojo", "Rojo", "#C7372B"),
            E("negro", "Negro", "#222222"), E("morado", "Morado", "#6A3FB0"), E("amarillo", "Amarillo", "#E8B21C"),
            E("rosa", "Rosa", "#E1679B"), E("aqua", "Aqua", "#1AA7A0"),
        };
        EditorUtility.SetDirty(p);
        return p;
    }

    private static PlumagePalette.Entry E(string id, string name, string hex)
        => new PlumagePalette.Entry { id = id, displayName = name, color = FrostboundUI.Hex(hex) };

    // ---------- Prendas y clases ----------

    private static void CreateItemsAndClasses()
    {
        EnsureFolder(SetupPenguinWardrobe.OutfitDataDir);
        var data = new Dictionary<string, (string display, string role, string attr, string desc, string[] gear, string plumage)>
        {
            ["Knight_Caballero"] = ("Caballero de la Escarcha", "Cuerpo a cuerpo · Defensa", "Fuerza",
                "Último guardián de la aldea. Su armadura se forjó con el hierro de las ruinas del Frostspire. Aguanta en primera línea y abre paso con su mandoble de escarcha.",
                new[] { "Yelmo cerrado con penacho", "Mandoble de filo de escarcha", "Hombreras y guanteletes", "Capa y sobreveste desgarradas" }, "azul"),
            ["Mage_Mago"] = ("Mago Errante", "A distancia · Magia", "Maná",
                "Viajero que lleva años estudiando el primer sellado del Frost. Canaliza el frío a través de su bastón y castiga a los corruptos desde lejos.",
                new[] { "Sombrero de ala ancha", "Bastón nudoso con cristal", "Capa de viaje", "Bolsa del caminante" }, "verde"),
            ["Ninja"] = ("Shinobi de la Escarcha", "Ágil · Golpes rápidos", "Agilidad",
                "Explorador silencioso de las laderas. Nadie lo ve llegar: golpea con kunai y shuriken, y esquiva antes de que el hielo oscuro lo alcance.",
                new[] { "Capucha y máscara", "Kunai de filo helado", "Shuriken", "Katana a la espalda" }, "negro"),
            ["Viking_Vikingo"] = ("Bárbaro del Glaciar", "Cuerpo a cuerpo · Aguante", "Salud",
                "Asaltante de las tierras altas que no le teme al frío. Resiste más castigo que nadie y parte el hielo corrupto de un hachazo.",
                new[] { "Yelmo de cuernos", "Hacha del glaciar", "Escudo redondo", "Manto de piel" }, "rojo"),
        };

        foreach (var g in ClassGear)
        {
            OutfitItem item = GearItem(g.item, g.fbx);
            CharacterClass cls = AssetDatabase.LoadAssetAtPath<CharacterClass>("Assets/Data/Classes/" + g.cls + ".asset");
            if (cls == null || !data.TryGetValue(g.cls, out var d)) continue;
            cls.className = g.fbx.Replace("Frostbound_", "").Replace("_Gear", "");
            cls.displayName = d.display;
            cls.role = d.role;
            cls.primaryAttribute = d.attr;
            cls.description = d.desc;
            cls.startingGear = d.gear;
            cls.defaultPlumageId = d.plumage;
            cls.gearPrefab = item != null ? item.prefab : null;
            cls.startingOutfit = item != null ? new[] { item } : new OutfitItem[0];
            EditorUtility.SetDirty(cls);
        }
        GearItem("Gear_Steve", "Frostbound_NPC_Rocker_Gear");
    }

    public static OutfitItem GearItem(string itemName, string fbx)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(GearModelDir + "/" + fbx + ".fbx");
        if (model == null)
        {
            Debug.LogWarning("[Gear] Falta " + fbx + ".fbx");
            return null;
        }
        string path = SetupPenguinWardrobe.OutfitDataDir + "/" + itemName + ".asset";
        OutfitItem item = AssetDatabase.LoadAssetAtPath<OutfitItem>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<OutfitItem>();
            AssetDatabase.CreateAsset(item, path);
        }
        item.displayName = itemName.Replace("Gear_", "Equipo ");
        item.slot = OutfitSlot.Body;
        item.alsoOccupies = new[] { OutfitSlot.Head, OutfitSlot.Face, OutfitSlot.Chest, OutfitSlot.Back, OutfitSlot.HandL, OutfitSlot.HandR };
        item.attachMode = OutfitAttachMode.Skinned;
        item.prefab = model;
        item.materialOverride = null;
        EditorUtility.SetDirty(item);
        return item;
    }

    // El prefab del pingüino gana PenguinAppearance (plumaje) y HeroGearEquipper (API del paquete).
    private static void SetupPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SetupPenguinWardrobe.PrefabPath);
        if (root == null) return;
        if (root.GetComponent<PenguinAppearance>() == null) root.AddComponent<PenguinAppearance>();
        HeroGearEquipper eq = root.GetComponent<HeroGearEquipper>() ?? root.AddComponent<HeroGearEquipper>();
        eq.classes = new List<CharacterClass>();
        foreach (var g in ClassGear)
        {
            CharacterClass cls = AssetDatabase.LoadAssetAtPath<CharacterClass>("Assets/Data/Classes/" + g.cls + ".asset");
            if (cls != null) eq.classes.Add(cls);
        }
        eq.startingClass = "";
        PrefabUtility.SaveAsPrefabAsset(root, SetupPenguinWardrobe.PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // Activa Read/Write en los modelos del equipo (la tela con física lo necesita) sin rehacer todo lo demás.
    public static string MakeGearReadable()
    {
        int n = 0;
        foreach (string file in Directory.GetFiles(GearModelDir, "*.fbx"))
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
            if (imp == null || imp.isReadable) continue;
            imp.isReadable = true;
            imp.SaveAndReimport();
            n++;
        }
        return n + " modelos";
    }

    public static void EnsureFolder(string path)
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
