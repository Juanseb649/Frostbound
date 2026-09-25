using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Frostbound > Inventario > Configurar progresión e inventario
// Crea iconos, botas, piezas de armadura separadas (casco, torso, pies, armas), objetos, sets,
// objetos iniciales de cada clase y la interfaz del juego (HUD + inventario) en el campamento.
public static class SetupInventory
{
    private const string IconDir = "Assets/Frostbound/UI/Icons";
    private const string SpriteDir = "Assets/Frostbound/UI/Sprites";
    private const string FontDir = "Assets/Frostbound/UI/Fonts";
    private const string ItemDir = "Assets/Data/Items";
    private const string SetDir = ItemDir + "/Sets";
    private const string PieceDir = "Assets/Data/Outfits/Pieces";
    private const string BootPrefabDir = "Assets/Prefabs/Gear";
    private const string BootMeshPath = "Assets/Models/Gear/Boot_Mesh.asset";
    private const string BootMatDir = "Assets/Materials/Gear";
    private const string SkinPath = "Assets/Data/UI/UISkin.asset";
    private const string DatabasePath = ItemDir + "/ItemDatabase.asset";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/Frostbound/Inventario/Configurar progresión e inventario")]
    public static void Setup()
    {
        if (!FrostboundBridge.ConfirmSave()) return;

        Dictionary<string, Sprite> icons = GenerateIcons();
        Dictionary<string, OutfitItem> outfits = CreateOutfits();
        Dictionary<string, ItemDefinition> items = CreateItems(icons, outfits);
        CreateSets(items);
        AssignClassItems(items);
        ItemDatabase db = CreateDatabase(items);
        UISkin skin = CreateSkin(icons);
        AssetDatabase.SaveAssets();

        string sceneResult = SetupScene(skin, db, items);

        FrostboundBridge.Dialog("Frostbound",
            "Progresión e inventario listos.\n\n" +
            "• " + items.Count + " objetos en " + ItemDir + " (4 sets de clase + botín)\n" +
            "• Piezas separadas: casco, torso, pies, arma y mano secundaria (" + PieceDir + ")\n" +
            "• Iconos en " + IconDir + "\n" +
            "• " + sceneResult + "\n\n" +
            "En Play: I o C abre el personaje e inventario, Q usa una poción.\n" +
            "Depuración: X da experiencia (Mayús+X sube un nivel), G da un objeto al azar.", "OK");
    }

    // ---------- Iconos ----------

    private static readonly (string slot, string from)[] SlotIcons =
    {
        ("Slot_Head", "helm_knight"), ("Slot_Chest", "chest_knight"), ("Slot_Feet", "boots_steel"),
        ("Slot_Weapon", "sword"), ("Slot_Offhand", "shield"), ("Slot_Amulet", "amulet"),
    };

    private static Dictionary<string, Sprite> GenerateIcons()
    {
        FrostboundGearSetup.EnsureFolder(IconDir);
        var catalog = FrostboundIconPainter.Catalog();
        var paths = new Dictionary<string, string>();

        foreach (var pair in catalog)
            paths[pair.Key] = WritePng(FrostboundIconPainter.Render(pair.Value), "Icon_" + pair.Key);
        foreach (var s in SlotIcons)
            paths[s.slot] = WritePng(FrostboundIconPainter.Render(catalog[s.from], 128, 5f, true), "Icon_" + s.slot);

        AssetDatabase.Refresh();
        var result = new Dictionary<string, Sprite>();
        foreach (var pair in paths)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(pair.Value);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.filterMode = FilterMode.Bilinear;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.spritePixelsPerUnit = 128f;
            imp.SaveAndReimport();
            result[pair.Key] = AssetDatabase.LoadAssetAtPath<Sprite>(pair.Value);
        }
        return result;
    }

    private static string WritePng(Texture2D tex, string name)
    {
        string path = IconDir + "/" + name + ".png";
        File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return path;
    }

    // ---------- Modelos: piezas y botas ----------

    // Piezas del equipo de cada clase: mismo modelo, pero solo las mallas de esa parte.
    private static readonly (string asset, string display, string gear, string[] parts, OutfitSlot slot, OutfitSlot[] also)[] Pieces =
    {
        ("Pieza_Knight_Casco", "Yelmo del caballero", "Gear_Knight", new[] { "Helm" }, OutfitSlot.Head, new OutfitSlot[0]),
        ("Pieza_Knight_Torso", "Coraza del caballero", "Gear_Knight", new[] { "Chest", "Surcoat", "Cape", "Shoulder_L", "Shoulder_R", "Gauntlet_L", "Gauntlet_R" }, OutfitSlot.Chest, new[] { OutfitSlot.Back }),
        ("Pieza_Knight_Arma", "Mandoble", "Gear_Knight", new[] { "Weapon_R" }, OutfitSlot.HandR, new OutfitSlot[0]),
        ("Pieza_Mage_Casco", "Sombrero del mago", "Gear_Mage", new[] { "Hat", "Beard" }, OutfitSlot.Head, new[] { OutfitSlot.Face }),
        ("Pieza_Mage_Torso", "Túnica del mago", "Gear_Mage", new[] { "Robe", "Cloak", "Belt" }, OutfitSlot.Chest, new[] { OutfitSlot.Back }),
        ("Pieza_Mage_Arma", "Bastón", "Gear_Mage", new[] { "Weapon_R" }, OutfitSlot.HandR, new OutfitSlot[0]),
        ("Pieza_Ninja_Casco", "Capucha del ninja", "Gear_Ninja", new[] { "Hood" }, OutfitSlot.Head, new OutfitSlot[0]),
        ("Pieza_Ninja_Torso", "Traje del ninja", "Gear_Ninja", new[] { "Suit", "Wraps_L", "Wraps_R", "Katana_Back" }, OutfitSlot.Chest, new[] { OutfitSlot.Back }),
        ("Pieza_Ninja_Arma", "Kunai", "Gear_Ninja", new[] { "Weapon_R" }, OutfitSlot.HandR, new OutfitSlot[0]),
        ("Pieza_Ninja_Secundaria", "Kunai izquierdo", "Gear_Ninja", new[] { "Weapon_L" }, OutfitSlot.HandL, new OutfitSlot[0]),
        ("Pieza_Viking_Casco", "Yelmo vikingo", "Gear_Viking", new[] { "Helm" }, OutfitSlot.Head, new OutfitSlot[0]),
        ("Pieza_Viking_Torso", "Manto vikingo", "Gear_Viking", new[] { "Fur", "Shoulder_R", "Belt", "Kilt" }, OutfitSlot.Chest, new[] { OutfitSlot.Back }),
        ("Pieza_Viking_Arma", "Hacha", "Gear_Viking", new[] { "Weapon_R" }, OutfitSlot.HandR, new OutfitSlot[0]),
        ("Pieza_Viking_Escudo", "Escudo", "Gear_Viking", new[] { "Offhand_L" }, OutfitSlot.HandL, new OutfitSlot[0]),
    };

    private static readonly (string name, string display, string body, string trim)[] Boots =
    {
        ("Botas_Caballero", "Grebas de acero", "#B9C8D8", "#7C8FA6"),
        ("Botas_Errante", "Botas del errante", "#6A45B0", "#E0B44A"),
        ("Botas_Shinobi", "Tabi negros", "#2B3040", "#C7372B"),
        ("Botas_Piel", "Botas de piel", "#7A4E33", "#D9B58C"),
        ("Botas_Hielo", "Botas de hielo", "#A9DDF5", "#F2F9FF"),
    };

    private static Dictionary<string, OutfitItem> CreateOutfits()
    {
        FrostboundGearSetup.EnsureFolder(PieceDir);
        var result = new Dictionary<string, OutfitItem>();

        foreach (var p in Pieces)
        {
            OutfitItem gear = AssetDatabase.LoadAssetAtPath<OutfitItem>(SetupPenguinWardrobe.OutfitDataDir + "/" + p.gear + ".asset");
            if (gear == null || gear.prefab == null)
            {
                Debug.LogWarning("[Inventario] Falta " + p.gear + " (ejecuta Tools > Frostbound > Equipamiento > Preparar equipo de clases).");
                continue;
            }
            OutfitItem item = LoadOrCreate<OutfitItem>(PieceDir + "/" + p.asset + ".asset");
            item.displayName = p.display;
            item.prefab = gear.prefab;
            item.materialOverride = gear.materialOverride;
            item.attachMode = OutfitAttachMode.Skinned;
            item.onlyParts = p.parts;
            item.slot = p.slot;
            item.alsoOccupies = p.also;
            EditorUtility.SetDirty(item);
            result[p.asset] = item;
        }

        Mesh boot = BuildBootMesh();
        FrostboundGearSetup.EnsureFolder(BootPrefabDir);
        FrostboundGearSetup.EnsureFolder(BootMatDir);
        foreach (var b in Boots)
        {
            Material body = ToonMaterial(BootMatDir + "/" + b.name + "_Body.mat", Hex(b.body));
            Material trim = ToonMaterial(BootMatDir + "/" + b.name + "_Trim.mat", Hex(b.trim));
            var go = new GameObject(b.name);
            go.AddComponent<MeshFilter>().sharedMesh = boot;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { body, trim };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, BootPrefabDir + "/" + b.name + ".prefab");
            Object.DestroyImmediate(go);

            OutfitItem item = LoadOrCreate<OutfitItem>(PieceDir + "/" + b.name + ".asset");
            item.displayName = b.display;
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
            result[b.name] = item;
        }

        foreach (string legacy in new[] { "Casco_Cuernos", "Sombrero_Mago", "Armadura" })
        {
            OutfitItem o = AssetDatabase.LoadAssetAtPath<OutfitItem>(SetupPenguinWardrobe.OutfitDataDir + "/" + legacy + ".asset");
            if (o != null) result[legacy] = o;
        }
        return result;
    }

    private static Material ToonMaterial(string path, Color color)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Frostbound/Toon");
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        if (shader != null) m.shader = shader;
        m.SetFloat("_VertexColorMode", 0f);
        m.SetColor("_BaseColor", color);
        m.SetColor("_ShadowColor", new Color(0.8f, 0.82f, 0.9f));
        m.SetFloat("_AmbientStrength", 0.3f);
        m.SetFloat("_OutlineWidth", 2f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Bota de 1 × 1 × 1 con el origen abajo en el centro del pie y la punta hacia +Z.
    // Submalla 0: cuerpo. Submalla 1: puño y suela.
    private static Mesh BuildBootMesh()
    {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var body = new List<int>();
        var trim = new List<int>();
        Prism(v, n, body, 0f, 0.02f, 0.5f, 0.5f, 0.04f, 0.42f, 24, 3.2f);
        Prism(v, n, body, 0f, -0.2f, 0.42f, 0.34f, 0.3f, 0.9f, 24, 2.4f);
        Prism(v, n, trim, 0f, -0.2f, 0.47f, 0.39f, 0.8f, 1f, 24, 2.4f);
        Prism(v, n, trim, 0f, 0.02f, 0.53f, 0.53f, 0f, 0.09f, 24, 3.2f);

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BootMeshPath);
        bool create = mesh == null;
        if (create) mesh = new Mesh();
        mesh.Clear();
        mesh.name = "Boot_Mesh";
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(body, 0);
        mesh.SetTriangles(trim, 1);
        mesh.RecalculateBounds();
        FrostboundGearSetup.EnsureFolder(Path.GetDirectoryName(BootMeshPath).Replace('\\', '/'));
        if (create) AssetDatabase.CreateAsset(mesh, BootMeshPath);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // Prisma de perfil superelipse (power 2 = elipse, más alto = más cuadrado) con tapas.
    private static void Prism(List<Vector3> v, List<Vector3> n, List<int> t, float cx, float cz, float rx, float rz, float y0, float y1, int seg, float power)
    {
        var ring = new Vector3[seg];
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            ring[i] = new Vector3(cx + rx * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / power), 0f, cz + rz * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 2f / power));
        }
        var center = new Vector3(cx, 0f, cz);

        int start = v.Count;
        for (int i = 0; i < seg; i++)
        {
            Vector3 tangent = ring[(i + 1) % seg] - ring[(i - 1 + seg) % seg];
            Vector3 normal = new Vector3(tangent.z, 0f, -tangent.x).normalized;
            if (Vector3.Dot(normal, ring[i] - center) < 0f) normal = -normal;
            v.Add(ring[i] + Vector3.up * y0); n.Add(normal);
            v.Add(ring[i] + Vector3.up * y1); n.Add(normal);
        }
        for (int i = 0; i < seg; i++)
        {
            int b0 = start + i * 2, t0 = b0 + 1, b1 = start + ((i + 1) % seg) * 2, t1 = b1 + 1;
            Tri(v, t, b0, t0, b1, n[b0]);
            Tri(v, t, b1, t0, t1, n[b0]);
        }

        foreach (bool top in new[] { true, false })
        {
            float y = top ? y1 : y0;
            Vector3 normal = top ? Vector3.up : Vector3.down;
            int c = v.Count;
            v.Add(center + Vector3.up * y); n.Add(normal);
            for (int i = 0; i < seg; i++) { v.Add(ring[i] + Vector3.up * y); n.Add(normal); }
            for (int i = 0; i < seg; i++) Tri(v, t, c, c + 1 + i, c + 1 + (i + 1) % seg, normal);
        }
    }

    private static void Tri(List<Vector3> v, List<int> t, int a, int b, int c, Vector3 outward)
    {
        Vector3 face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(face, outward) < 0f) { int tmp = b; b = c; c = tmp; }
        t.Add(a); t.Add(b); t.Add(c);
    }

    // ---------- Objetos ----------

    private class Spec
    {
        public string id, name, desc, icon, outfit;
        public ItemCategory cat = ItemCategory.Armor;
        public ItemRarity rarity = ItemRarity.Common;
        public EquipSlot slot = EquipSlot.None;
        public int level = 1, armor, stack = 1;
        public float damage, heal, mana;
        public StatModifier[] stats = new StatModifier[0];
    }

    private static StatModifier S(StatType t, int v) => new StatModifier(t, v);

    private static readonly StatType Str = StatType.Strength, Man = StatType.Mana, Agi = StatType.Agility, Hp = StatType.Health;

    private static List<Spec> Specs()
    {
        return new List<Spec>
        {
            // Caballero de la Escarcha
            new Spec { id = "knight_helm", name = "Yelmo de la Escarcha", slot = EquipSlot.Head, armor = 4, stats = new[] { S(Hp, 1) }, icon = "helm_knight", outfit = "Pieza_Knight_Casco", desc = "Yelmo cerrado con penacho, forjado con el hierro de las ruinas." },
            new Spec { id = "knight_chest", name = "Coraza de la Escarcha", slot = EquipSlot.Chest, armor = 8, stats = new[] { S(Str, 1), S(Hp, 1) }, icon = "chest_knight", outfit = "Pieza_Knight_Torso", desc = "Hombreras, guanteletes y una sobreveste desgarrada." },
            new Spec { id = "knight_boots", name = "Grebas de la Escarcha", slot = EquipSlot.Feet, armor = 3, stats = new[] { S(Hp, 1) }, icon = "boots_steel", outfit = "Botas_Caballero", desc = "Pesan, pero no resbalan en el hielo." },
            new Spec { id = "knight_sword", name = "Mandoble de escarcha", cat = ItemCategory.Weapon, slot = EquipSlot.Weapon, damage = 6, stats = new[] { S(Str, 1) }, icon = "sword", outfit = "Pieza_Knight_Arma", desc = "Su filo nunca se descongela." },
            // Mago Errante
            new Spec { id = "mage_hat", name = "Sombrero del Errante", slot = EquipSlot.Head, armor = 1, stats = new[] { S(Man, 2) }, icon = "hat_mage", outfit = "Pieza_Mage_Casco", desc = "Ala ancha para la nieve, y una barba que no es parte del sombrero." },
            new Spec { id = "mage_robe", name = "Túnica del Errante", slot = EquipSlot.Chest, armor = 3, stats = new[] { S(Man, 2), S(Hp, 1) }, icon = "robe_mage", outfit = "Pieza_Mage_Torso", desc = "Capa de viaje con la bolsa del caminante." },
            new Spec { id = "mage_boots", name = "Botas del Errante", slot = EquipSlot.Feet, armor = 1, stats = new[] { S(Man, 1), S(Agi, 1) }, icon = "boots_mage", outfit = "Botas_Errante", desc = "Gastadas de tanto subir la montaña." },
            new Spec { id = "mage_staff", name = "Bastón nudoso", cat = ItemCategory.Weapon, slot = EquipSlot.Weapon, damage = 4, stats = new[] { S(Man, 2) }, icon = "staff", outfit = "Pieza_Mage_Arma", desc = "El cristal de la punta canaliza el frío." },
            // Shinobi de la Escarcha
            new Spec { id = "ninja_hood", name = "Capucha del Shinobi", slot = EquipSlot.Head, armor = 2, stats = new[] { S(Agi, 1) }, icon = "hood_ninja", outfit = "Pieza_Ninja_Casco", desc = "Capucha y máscara. Nadie lo ve llegar." },
            new Spec { id = "ninja_suit", name = "Traje del Shinobi", slot = EquipSlot.Chest, armor = 4, stats = new[] { S(Agi, 2) }, icon = "suit_ninja", outfit = "Pieza_Ninja_Torso", desc = "Vendas en las aletas y una katana a la espalda." },
            new Spec { id = "ninja_boots", name = "Tabi del Shinobi", slot = EquipSlot.Feet, armor = 1, stats = new[] { S(Agi, 2) }, icon = "boots_ninja", outfit = "Botas_Shinobi", desc = "No hacen ruido ni sobre la nieve dura." },
            new Spec { id = "ninja_kunai", name = "Kunai de filo helado", cat = ItemCategory.Weapon, slot = EquipSlot.Weapon, damage = 5, stats = new[] { S(Agi, 1) }, icon = "kunai", outfit = "Pieza_Ninja_Arma" },
            new Spec { id = "ninja_offhand", name = "Kunai de reserva", cat = ItemCategory.Weapon, slot = EquipSlot.Offhand, damage = 2, stats = new[] { S(Agi, 1) }, icon = "shuriken", outfit = "Pieza_Ninja_Secundaria", desc = "Para la otra aleta." },
            // Bárbaro del Glaciar
            new Spec { id = "viking_helm", name = "Yelmo de cuernos del glaciar", slot = EquipSlot.Head, armor = 4, stats = new[] { S(Hp, 1) }, icon = "helm_viking", outfit = "Pieza_Viking_Casco" },
            new Spec { id = "viking_fur", name = "Manto de piel", slot = EquipSlot.Chest, armor = 6, stats = new[] { S(Hp, 2) }, icon = "fur_viking", outfit = "Pieza_Viking_Torso", desc = "Abriga más que cualquier hechizo." },
            new Spec { id = "viking_boots", name = "Botas de piel", slot = EquipSlot.Feet, armor = 2, stats = new[] { S(Hp, 1) }, icon = "boots_fur", outfit = "Botas_Piel" },
            new Spec { id = "viking_axe", name = "Hacha del glaciar", cat = ItemCategory.Weapon, slot = EquipSlot.Weapon, damage = 7, stats = new[] { S(Str, 1) }, icon = "axe", outfit = "Pieza_Viking_Arma", desc = "Parte el hielo corrupto de un hachazo." },
            new Spec { id = "viking_shield", name = "Escudo redondo", slot = EquipSlot.Offhand, armor = 5, stats = new[] { S(Hp, 1) }, icon = "shield", outfit = "Pieza_Viking_Escudo" },
            // Botín para mezclar
            new Spec { id = "horned_helm", name = "Casco con cuernos", rarity = ItemRarity.Uncommon, slot = EquipSlot.Head, level = 2, armor = 5, stats = new[] { S(Str, 1), S(Hp, 1) }, icon = "helm_horned", outfit = "Casco_Cuernos" },
            new Spec { id = "wizard_hat", name = "Sombrero de mago", rarity = ItemRarity.Uncommon, slot = EquipSlot.Head, level = 2, armor = 1, stats = new[] { S(Man, 3), S(Agi, 1) }, icon = "hat_wizard", outfit = "Sombrero_Mago" },
            new Spec { id = "plate_armor", name = "Armadura de placas", rarity = ItemRarity.Rare, slot = EquipSlot.Chest, level = 3, armor = 12, stats = new[] { S(Str, 1), S(Hp, 2) }, icon = "chest_plate", outfit = "Armadura", desc = "Placas remachadas en oro. Alguien la dejó en las ruinas." },
            new Spec { id = "ice_boots", name = "Botas de hielo", rarity = ItemRarity.Rare, slot = EquipSlot.Feet, level = 2, armor = 3, stats = new[] { S(Agi, 2), S(Hp, 1) }, icon = "boots_ice", outfit = "Botas_Hielo", desc = "Talladas en un hielo que no se derrite." },
            new Spec { id = "snow_amulet", name = "Amuleto del copo de nieve", rarity = ItemRarity.Epic, slot = EquipSlot.Amulet, level = 3, stats = new[] { S(Str, 1), S(Man, 1), S(Agi, 1), S(Hp, 1) }, icon = "amulet", desc = "Late con la misma luz azul que la montaña." },
            // Consumibles y materiales
            new Spec { id = "health_potion", name = "Poción de vida", cat = ItemCategory.Consumable, stack = 20, heal = 50, icon = "potion_red" },
            new Spec { id = "mana_potion", name = "Poción de maná", cat = ItemCategory.Consumable, stack = 20, mana = 40, icon = "potion_blue" },
            new Spec { id = "frost_shard", name = "Fragmento de Frost", cat = ItemCategory.Material, rarity = ItemRarity.Uncommon, stack = 99, icon = "shard", desc = "Un trozo de hielo que late débilmente. Quizá el herrero sepa qué hacer con él." },
        };
    }

    private static Dictionary<string, ItemDefinition> CreateItems(Dictionary<string, Sprite> icons, Dictionary<string, OutfitItem> outfits)
    {
        FrostboundGearSetup.EnsureFolder(ItemDir);
        var result = new Dictionary<string, ItemDefinition>();
        foreach (Spec s in Specs())
        {
            ItemDefinition item = LoadOrCreate<ItemDefinition>(ItemDir + "/" + s.id + ".asset");
            item.id = s.id;
            item.displayName = s.name;
            item.description = s.desc ?? "";
            item.icon = s.icon != null && icons.TryGetValue(s.icon, out Sprite icon) ? icon : null;
            item.category = s.cat;
            item.rarity = s.rarity;
            item.equipSlot = s.slot;
            item.maxStack = s.slot != EquipSlot.None ? 1 : s.stack;
            item.requiredLevel = s.level;
            item.armor = s.armor;
            item.damage = s.damage;
            item.heal = s.heal;
            item.restoreMana = s.mana;
            item.stats = new List<StatModifier>(s.stats);
            item.outfit = s.outfit != null && outfits.TryGetValue(s.outfit, out OutfitItem o) ? o : null;
            item.armorSet = null;
            EditorUtility.SetDirty(item);
            result[s.id] = item;
        }
        return result;
    }

    private static void CreateSets(Dictionary<string, ItemDefinition> items)
    {
        FrostboundGearSetup.EnsureFolder(SetDir);
        MakeSet("Set_Caballero", "Set del Caballero de la Escarcha", items, new[] { "knight_helm", "knight_chest", "knight_boots" },
            Bonus(2, 0, S(Hp, 2)), Bonus(3, 6, S(Str, 2)));
        MakeSet("Set_Errante", "Set del Mago Errante", items, new[] { "mage_hat", "mage_robe", "mage_boots" },
            Bonus(2, 0, S(Man, 2)), Bonus(3, 2, S(Man, 3), S(Hp, 1)));
        MakeSet("Set_Shinobi", "Set del Shinobi", items, new[] { "ninja_hood", "ninja_suit", "ninja_boots" },
            Bonus(2, 0, S(Agi, 2)), Bonus(3, 3, S(Agi, 2), S(Str, 1)));
        MakeSet("Set_Glaciar", "Set del Bárbaro del Glaciar", items, new[] { "viking_helm", "viking_fur", "viking_boots" },
            Bonus(2, 0, S(Hp, 2)), Bonus(3, 4, S(Hp, 3), S(Str, 1)));
    }

    private static ArmorSet.Bonus Bonus(int pieces, int armor, params StatModifier[] stats)
    {
        return new ArmorSet.Bonus { piecesRequired = pieces, armor = armor, stats = new List<StatModifier>(stats) };
    }

    private static void MakeSet(string asset, string display, Dictionary<string, ItemDefinition> items, string[] ids, params ArmorSet.Bonus[] bonuses)
    {
        ArmorSet set = LoadOrCreate<ArmorSet>(SetDir + "/" + asset + ".asset");
        set.displayName = display;
        set.pieces = new List<ItemDefinition>();
        foreach (string id in ids)
        {
            if (!items.TryGetValue(id, out ItemDefinition item)) continue;
            set.pieces.Add(item);
            item.armorSet = set;
            EditorUtility.SetDirty(item);
        }
        set.bonuses = new List<ArmorSet.Bonus>(bonuses);
        EditorUtility.SetDirty(set);
    }

    private static readonly (string cls, string[] items)[] ClassItems =
    {
        ("Knight_Caballero", new[] { "knight_helm", "knight_chest", "knight_boots", "knight_sword", "health_potion", "health_potion", "health_potion", "mana_potion" }),
        ("Mage_Mago", new[] { "mage_hat", "mage_robe", "mage_boots", "mage_staff", "health_potion", "health_potion", "mana_potion", "mana_potion", "mana_potion" }),
        ("Ninja", new[] { "ninja_hood", "ninja_suit", "ninja_boots", "ninja_kunai", "ninja_offhand", "health_potion", "health_potion", "health_potion", "mana_potion" }),
        ("Viking_Vikingo", new[] { "viking_helm", "viking_fur", "viking_boots", "viking_axe", "viking_shield", "health_potion", "health_potion", "health_potion", "mana_potion" }),
    };

    private static void AssignClassItems(Dictionary<string, ItemDefinition> items)
    {
        foreach (var c in ClassItems)
        {
            CharacterClass cls = AssetDatabase.LoadAssetAtPath<CharacterClass>("Assets/Data/Classes/" + c.cls + ".asset");
            if (cls == null) continue;
            var list = new List<ItemDefinition>();
            foreach (string id in c.items)
                if (items.TryGetValue(id, out ItemDefinition item)) list.Add(item);
            cls.startingItems = list.ToArray();
            EditorUtility.SetDirty(cls);
        }
    }

    private static ItemDatabase CreateDatabase(Dictionary<string, ItemDefinition> items)
    {
        ItemDatabase db = LoadOrCreate<ItemDatabase>(DatabasePath);
        db.items = new List<ItemDefinition>(items.Values);
        EditorUtility.SetDirty(db);
        return db;
    }

    private static UISkin CreateSkin(Dictionary<string, Sprite> icons)
    {
        FrostboundGearSetup.EnsureFolder(Path.GetDirectoryName(SkinPath).Replace('\\', '/'));
        UISkin skin = LoadOrCreate<UISkin>(SkinPath);
        skin.cinzel600 = Font("Cinzel-SemiBold");
        skin.cinzel800 = Font("Cinzel-ExtraBold");
        skin.nunito500 = Font("Nunito-Medium");
        skin.nunito700 = Font("Nunito-Bold");
        skin.nunito800 = Font("Nunito-ExtraBold");
        skin.round10 = UISprite("UI_Round10");
        skin.round12 = UISprite("UI_Round12");
        skin.round16 = UISprite("UI_Round16");
        skin.round20 = UISprite("UI_Round20");
        skin.pill = UISprite("UI_Pill");
        skin.circle = UISprite("UI_Circle");
        skin.diamond = UISprite("UI_Diamond");
        skin.iconStrength = icons.GetValueOrDefault("stat_strength");
        skin.iconMana = icons.GetValueOrDefault("stat_mana");
        skin.iconAgility = icons.GetValueOrDefault("stat_agility");
        skin.iconHealth = icons.GetValueOrDefault("stat_health");
        skin.slotHead = icons.GetValueOrDefault("Slot_Head");
        skin.slotChest = icons.GetValueOrDefault("Slot_Chest");
        skin.slotFeet = icons.GetValueOrDefault("Slot_Feet");
        skin.slotWeapon = icons.GetValueOrDefault("Slot_Weapon");
        skin.slotOffhand = icons.GetValueOrDefault("Slot_Offhand");
        skin.slotAmulet = icons.GetValueOrDefault("Slot_Amulet");
        EditorUtility.SetDirty(skin);
        if (skin.nunito800 == null || skin.round12 == null)
            Debug.LogWarning("[Inventario] Faltan fuentes o sprites de la UI: ejecuta antes Tools > Frostbound > UI > Construir menú y selección de clase.");
        return skin;
    }

    private static TMP_FontAsset Font(string name) => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + "/" + name + " SDF.asset");
    private static Sprite UISprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/" + name + ".png");

    // ---------- Escena ----------

    private static string SetupScene(UISkin skin, ItemDatabase db, Dictionary<string, ItemDefinition> items)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Player");
        if (player == null) return "No se encontró el objeto Player en " + ScenePath + ": la escena no se modificó.";

        Inventory inv = GetOrAdd<Inventory>(player);
        inv.capacity = Mathf.Max(inv.capacity, 60);
        Equipment eq = GetOrAdd<Equipment>(player);
        eq.outfit = player.GetComponentInChildren<PenguinOutfit>(true);
        eq.giveClassStartingItems = true;
        ProgressionDebug dbg = GetOrAdd<ProgressionDebug>(player);
        dbg.database = db;
        OutfitTester tester = player.GetComponent<OutfitTester>();
        if (tester != null) tester.enabled = false;

        GameObject old = GameObject.Find("GameUI");
        if (old != null) Object.DestroyImmediate(old);
        var ui = new GameObject("GameUI", typeof(RectTransform));
        ui.layer = 5;
        var canvas = ui.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = ui.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(FrostboundUI.ReferenceWidth, FrostboundUI.ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;
        ui.AddComponent<GraphicRaycaster>();

        RectTransform hudRt = UIFactory.Stretch(UIFactory.Node(ui.transform, "HUD"));
        var hud = hudRt.gameObject.AddComponent<GameHUD>();
        hud.skin = skin;
        hud.player = eq;
        RectTransform invRt = UIFactory.Stretch(UIFactory.Node(ui.transform, "InventoryScreen"));
        var screen = invRt.gameObject.AddComponent<InventoryScreen>();
        screen.skin = skin;
        screen.player = eq;
        screen.hud = hud;
        hud.inventoryScreen = screen;

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        PlaceTestPickups(player.transform, items);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Escena " + Path.GetFileNameWithoutExtension(ScenePath) + ": Player con Inventory, Equipment y ProgressionDebug; Canvas GameUI; 3 objetos de prueba junto al jugador.";
    }

    private static void PlaceTestPickups(Transform player, Dictionary<string, ItemDefinition> items)
    {
        GameObject old = GameObject.Find("Pickups_Prueba");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("Pickups_Prueba");
        Physics.SyncTransforms();

        Vector3 fwd = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        var drops = new (string id, int qty, Vector3 offset)[]
        {
            ("ice_boots", 1, fwd * 3f - right * 2f),
            ("health_potion", 2, fwd * 3.5f),
            ("frost_shard", 3, fwd * 3f + right * 2f),
        };
        Material spriteMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");

        foreach (var d in drops)
        {
            if (!items.TryGetValue(d.id, out ItemDefinition item)) continue;
            Vector3 pos = player.position + d.offset;
            if (Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)) pos.y = hit.point.y;

            var go = new GameObject("Pickup_" + item.id);
            go.transform.SetParent(root.transform, false);
            go.transform.position = pos;
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.8f;
            col.center = Vector3.up * 0.5f;
            var pickup = go.AddComponent<ItemPickup>();
            pickup.item = item;
            pickup.quantity = d.qty;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = Vector3.up * 0.8f;
            visual.transform.localScale = Vector3.one * 0.6f;
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = item.icon;
            if (spriteMat != null) sr.sharedMaterial = spriteMat;
            pickup.visual = visual.transform;

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.6f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = FrostboundUI.Rarity(item.rarity);
            light.range = 2.5f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
        }
    }

    // ---------- Utilidades ----------

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
