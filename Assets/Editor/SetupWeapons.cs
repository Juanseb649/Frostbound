using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Armas y runas: modelos (Assets/Models/Weapons), materiales toon, iconos renderizados desde los modelos,
// objetos con tipo, nivel, requisitos, durabilidad y ranuras de runa, y en la escena: combate del héroe,
// muñecos de práctica junto a la herrería y botín de prueba que cae al empezar.
public static class SetupWeapons
{
    private const string ModelDir = "Assets/Models/Weapons";
    private const string MatDir = "Assets/Materials/Weapons";
    private const string GearMatDir = "Assets/Materials/Gear";
    private const string IconDir = "Assets/Frostbound/UI/Icons/Weapons";
    private const string ItemDir = "Assets/Data/Items";
    private const string WeaponItemDir = ItemDir + "/Weapons";
    private const string RuneItemDir = ItemDir + "/Runes";
    private const string DatabasePath = ItemDir + "/ItemDatabase.asset";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    private static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>
    {
        { "W_Steel", new Color(0.74f, 0.78f, 0.84f) }, { "W_DarkIron", new Color(0.30f, 0.32f, 0.38f) },
        { "W_Wood", new Color(0.62f, 0.42f, 0.25f) }, { "W_DarkWood", new Color(0.40f, 0.25f, 0.15f) },
        { "W_Leather", new Color(0.45f, 0.27f, 0.16f) }, { "W_Gold", new Color(0.96f, 0.76f, 0.30f) },
        { "W_Bronze", new Color(0.78f, 0.52f, 0.26f) }, { "W_Ice", new Color(0.55f, 0.86f, 1f) },
        { "W_Red", new Color(0.78f, 0.20f, 0.20f) }, { "W_Blue", new Color(0.22f, 0.40f, 0.80f) },
        { "W_String", new Color(0.93f, 0.91f, 0.84f) }, { "W_Black", new Color(0.13f, 0.13f, 0.16f) },
        { "W_Feather", new Color(0.96f, 0.96f, 0.96f) },
    };

    private struct W
    {
        public string id, model, name, desc;
        public WeaponType type;
        public float dmg, speed;
        public int level, durability, runes;
        public StatModifier[] req, bonus;
        public ItemRarity rarity;
        public string projectile;
    }

    private static StatModifier S(StatType t, int v) => new StatModifier(t, v);
    private static readonly StatType Str = StatType.Strength, Man = StatType.Mana, Agi = StatType.Agility, Hp = StatType.Health;

    // Armas nuevas: dos manos, una mano y a distancia (inspiradas en Diablo II y Minecraft Dungeons).
    private static W[] NewWeapons() => new[]
    {
        new W { id = "claymore", model = "W_claymore", name = "Claymore de las Ruinas", type = WeaponType.Claymore, dmg = 14, speed = 0.85f, level = 4, durability = 60, runes = 2, req = new[] { S(Str, 12) }, rarity = ItemRarity.Uncommon, desc = "Hoja ancha de los antiguos guardianes. Barre todo lo que tiene delante." },
        new W { id = "long_axe", model = "W_longaxe", name = "Hacha larga del glaciar", type = WeaponType.LongAxe, dmg = 16, speed = 0.75f, level = 6, durability = 55, runes = 2, req = new[] { S(Str, 14) }, bonus = new[] { S(Str, 1) }, rarity = ItemRarity.Rare, desc = "Una media luna de acero en la punta de un asta. Lenta, pero no perdona." },
        new W { id = "spear", model = "W_spear", name = "Lanza de los vigías", type = WeaponType.Spear, dmg = 11, speed = 1.0f, level = 3, durability = 50, runes = 2, req = new[] { S(Str, 8), S(Agi, 7) }, rarity = ItemRarity.Uncommon, desc = "Mantiene a los corrompidos a distancia. El mayor alcance cuerpo a cuerpo." },
        new W { id = "maul", model = "W_maul", name = "Mazo del deshielo", type = WeaponType.Mace, dmg = 19, speed = 0.65f, level = 8, durability = 70, runes = 3, req = new[] { S(Str, 16) }, bonus = new[] { S(Hp, 2) }, rarity = ItemRarity.Epic, desc = "Cada golpe hace temblar el hielo bajo tus patas." },
        new W { id = "knife", model = "W_knife", name = "Cuchillo de pescador", type = WeaponType.Knife, dmg = 3, speed = 1.9f, level = 1, durability = 30, runes = 1, req = new[] { S(Agi, 5) }, rarity = ItemRarity.Common, desc = "Para limpiar pescado. O para defenderte si hace falta." },
        new W { id = "sword", model = "W_sword", name = "Espada de la guardia", type = WeaponType.Sword, dmg = 7, speed = 1.2f, level = 2, durability = 45, runes = 2, req = new[] { S(Str, 6) }, rarity = ItemRarity.Common, desc = "La espada reglamentaria de la guardia del poblado." },
        new W { id = "scimitar", model = "W_scimitar", name = "Cimitarra del viento blanco", type = WeaponType.Scimitar, dmg = 8, speed = 1.3f, level = 4, durability = 40, runes = 2, req = new[] { S(Str, 6), S(Agi, 7) }, bonus = new[] { S(Agi, 1) }, rarity = ItemRarity.Uncommon, desc = "Curva como una duna de nieve. Corta mientras gira." },
        new W { id = "hammer", model = "W_hammer", name = "Martillo del herrero", type = WeaponType.Hammer, dmg = 9, speed = 1.0f, level = 3, durability = 55, runes = 1, req = new[] { S(Str, 8) }, rarity = ItemRarity.Common, desc = "Brunna lo usaba en la fragua hasta que encontró uno mejor." },
        new W { id = "nunchaku", model = "W_nunchaku", name = "Nunchaku de hielo negro", type = WeaponType.Nunchaku, dmg = 5, speed = 2.0f, level = 3, durability = 35, runes = 2, req = new[] { S(Agi, 9) }, rarity = ItemRarity.Uncommon, desc = "Golpes rápidos y seguidos. Hace falta agilidad para no darse a uno mismo." },
        new W { id = "kunai", model = "W_kunai", name = "Kunai templado", type = WeaponType.Kunai, dmg = 5, speed = 1.8f, level = 2, durability = 30, runes = 1, req = new[] { S(Agi, 9) }, rarity = ItemRarity.Common, desc = "Hoja corta de filo helado. Pide manos rápidas." },
        new W { id = "katana", model = "W_katana", name = "Katana de Tsurara", type = WeaponType.Katana, dmg = 11, speed = 1.35f, level = 7, durability = 45, runes = 3, req = new[] { S(Agi, 12), S(Str, 6) }, bonus = new[] { S(Agi, 2) }, rarity = ItemRarity.Rare, desc = "Forjada con el carámbano más largo del Frostspire." },
        new W { id = "celtic_sword", model = "W_celtic", name = "Espada celta de hoja de sauce", type = WeaponType.CelticSword, dmg = 8, speed = 1.2f, level = 5, durability = 50, runes = 3, req = new[] { S(Str, 8), S(Man, 5) }, bonus = new[] { S(Man, 2) }, rarity = ItemRarity.Rare, desc = "Los antiguos grababan runas en su hoja. Acepta más que ninguna otra." },
        new W { id = "sickle", model = "W_sickle", name = "Hoz de escarcha", type = WeaponType.Sickle, dmg = 6, speed = 1.6f, level = 4, durability = 35, runes = 2, req = new[] { S(Agi, 10) }, rarity = ItemRarity.Uncommon, desc = "Se engancha y corta. Solo los ágiles la manejan sin herirse." },
        new W { id = "machete", model = "W_machete", name = "Machete del bosque", type = WeaponType.Machete, dmg = 7, speed = 1.35f, level = 2, durability = 40, runes = 1, req = new[] { S(Str, 5), S(Agi, 5) }, rarity = ItemRarity.Common, desc = "Abre camino entre los pinos helados del bosque corrupto." },
        new W { id = "bow", model = "W_bow", name = "Arco corto", type = WeaponType.Bow, dmg = 6, speed = 1.3f, level = 1, durability = 40, runes = 2, req = new[] { S(Agi, 6) }, rarity = ItemRarity.Common, projectile = "W_arrow", desc = "Ligero y rápido. Ideal para mantener la distancia." },
        new W { id = "longbow", model = "W_longbow", name = "Arco largo del vigía", type = WeaponType.Longbow, dmg = 10, speed = 0.9f, level = 5, durability = 45, runes = 2, req = new[] { S(Agi, 12), S(Str, 6) }, bonus = new[] { S(Agi, 1) }, rarity = ItemRarity.Rare, projectile = "W_arrow", desc = "Kora dice que con él se acierta a un copo de nieve." },
        new W { id = "crossbow", model = "W_crossbow", name = "Ballesta de hierro", type = WeaponType.Crossbow, dmg = 13, speed = 0.7f, level = 6, durability = 55, runes = 2, req = new[] { S(Str, 9), S(Agi, 6) }, rarity = ItemRarity.Rare, projectile = "W_bolt", desc = "Lenta de recargar, pero el virote atraviesa el hielo." },
        new W { id = "throwing_kunai", model = "W_throwing_kunai", name = "Kunais arrojadizos", type = WeaponType.ThrowingKunai, dmg = 4, speed = 2.0f, level = 3, durability = 30, runes = 1, req = new[] { S(Agi, 11) }, rarity = ItemRarity.Uncommon, desc = "Tres kunais equilibrados para lanzar. Siempre vuelven a tu cinturón." },
    };

    // Armas del equipo inicial: se conservan y ahora se sujetan con la aleta.
    private static readonly (string id, string model, WeaponType type, float speed, int durability, int runes)[] Starters =
    {
        ("knight_sword", "W_knight_sword", WeaponType.Sword, 1.1f, 50, 1),
        ("mage_staff", "W_mage_staff", WeaponType.Staff, 1.0f, 40, 2),
        ("ninja_kunai", "W_ninja_kunai", WeaponType.Kunai, 1.7f, 35, 1),
        ("ninja_offhand", "W_ninja_shuriken", WeaponType.Shuriken, 1.5f, 30, 0),
        ("viking_axe", "W_viking_axe", WeaponType.Axe, 1.0f, 55, 1),
    };

    private struct R
    {
        public string id, name, desc;
        public RuneEffect effect;
        public float power, duration, chance;
        public ItemRarity rarity;
        public int mana;
    }

    private static R[] Runes() => new[]
    {
        new R { id = "rune_fire", effect = RuneEffect.Fire, power = 3f, duration = 4f, chance = 1f, rarity = ItemRarity.Uncommon, mana = 15, desc = "Un hechizo de brasas atrapado en piedra. Lo que tocas, arde." },
        new R { id = "rune_poison", effect = RuneEffect.Poison, power = 2f, duration = 6f, chance = 1f, rarity = ItemRarity.Uncommon, mana = 15, desc = "Savia de los pinos negros del bosque corrupto." },
        new R { id = "rune_frost", effect = RuneEffect.Frost, power = 0.4f, duration = 2.5f, chance = 0.2f, rarity = ItemRarity.Uncommon, mana = 15, desc = "Frío puro, sin corrupción. Ralentiza y a veces congela." },
        new R { id = "rune_chain", effect = RuneEffect.Chain, power = 0.5f, duration = 0f, chance = 0.3f, rarity = ItemRarity.Rare, mana = 20, desc = "Tormenta de la cima. El rayo salta de enemigo en enemigo." },
        new R { id = "rune_lifesteal", effect = RuneEffect.Lifesteal, power = 0.06f, duration = 0f, chance = 1f, rarity = ItemRarity.Rare, mana = 20, desc = "Una runa antigua que cura a quien la empuña." },
        new R { id = "rune_sharpness", effect = RuneEffect.Sharpness, power = 0.2f, duration = 0f, chance = 1f, rarity = ItemRarity.Uncommon, mana = 12, desc = "Afila el arma más allá de lo que puede una piedra." },
        new R { id = "rune_swiftness", effect = RuneEffect.Swiftness, power = 0.15f, duration = 0f, chance = 1f, rarity = ItemRarity.Uncommon, mana = 12, desc = "El arma pesa menos y golpea antes." },
        new R { id = "rune_knockback", effect = RuneEffect.Knockback, power = 1.5f, duration = 0f, chance = 0.5f, rarity = ItemRarity.Uncommon, mana = 12, desc = "Cada golpe lleva una ráfaga de viento helado." },
    };

    [MenuItem("Tools/Frostbound/Armas/Configurar armas y runas")]
    public static void Build()
    {
        if (!FrostboundBridge.ConfirmSave()) return;
        string result = Setup(true);
        FrostboundBridge.Dialog("Frostbound", result, "OK");
    }

    public static string Setup(bool includeScene)
    {
        FrostboundGearSetup.EnsureFolder(MatDir);
        FrostboundGearSetup.EnsureFolder(IconDir);
        FrostboundGearSetup.EnsureFolder(WeaponItemDir);
        FrostboundGearSetup.EnsureFolder(RuneItemDir);

        ConfigureModels();
        var created = new List<ItemDefinition>();
        var byId = new Dictionary<string, ItemDefinition>();

        foreach (W w in NewWeapons())
        {
            ItemDefinition item = LoadOrCreate<ItemDefinition>(WeaponItemDir + "/" + w.id + ".asset");
            item.id = w.id;
            item.displayName = w.name;
            item.description = w.desc;
            item.category = ItemCategory.Weapon;
            item.rarity = w.rarity;
            item.maxStack = 1;
            item.equipSlot = EquipSlot.Weapon;
            item.requiredLevel = w.level;
            item.damage = w.dmg;
            item.armor = 0;
            item.stats = new List<StatModifier>(w.bonus ?? new StatModifier[0]);
            item.outfit = null;
            ApplyWeaponData(item, w.type, w.speed, w.durability, w.runes, w.req, w.model, w.projectile);
            item.icon = RenderIcon(item.weaponModel, "Icon_W_" + w.id, w.type);
            EditorUtility.SetDirty(item);
            created.Add(item);
            byId[w.id] = item;
        }

        foreach (var s in Starters)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemDir + "/" + s.id + ".asset");
            if (item == null) continue;
            item.outfit = null;
            item.category = s.type == WeaponType.Shuriken ? ItemCategory.Weapon : item.category;
            ApplyWeaponData(item, s.type, s.speed, s.durability, s.runes, new StatModifier[0], s.model, s.type == WeaponType.Shuriken ? null : null);
            EditorUtility.SetDirty(item);
            byId[s.id] = item;
        }
        // El escudo vikingo también se ve en la aleta izquierda.
        ItemDefinition shield = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemDir + "/viking_shield.asset");
        if (shield != null)
        {
            shield.outfit = null;
            shield.weaponModel = LoadModel("W_viking_shield");
            EditorUtility.SetDirty(shield);
        }

        foreach (R r in Runes())
        {
            ItemDefinition item = LoadOrCreate<ItemDefinition>(RuneItemDir + "/" + r.id + ".asset");
            item.id = r.id;
            item.displayName = "Runa " + WeaponCatalog.RuneName(r.effect);
            item.description = r.desc;
            item.category = ItemCategory.Rune;
            item.rarity = r.rarity;
            item.maxStack = 10;
            item.equipSlot = EquipSlot.None;
            item.weaponType = WeaponType.None;
            item.runeEffect = r.effect;
            item.runePower = r.power;
            item.runeDuration = r.duration;
            item.runeChance = r.chance;
            item.engraveManaCost = r.mana;
            item.icon = PaintRuneIcon(r.effect, "Icon_Rune_" + r.id);
            EditorUtility.SetDirty(item);
            created.Add(item);
            byId[r.id] = item;
        }

        ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
        if (db != null)
        {
            foreach (ItemDefinition item in created)
                if (!db.items.Contains(item)) db.items.Add(item);
            EditorUtility.SetDirty(db);
        }
        AssetDatabase.SaveAssets();

        string scene = includeScene ? SetupScene(byId) : "";
        return "Armas y runas listas.\n\n• " + NewWeapons().Length + " armas nuevas (dos manos, una mano y a distancia) + 5 del equipo inicial, sujetas con la aleta.\n• " +
               Runes().Length + " runas grabables.\n• Iconos en " + IconDir + ".\n" + scene +
               "\n\nEn Play: clic derecho ataca hacia el cursor, Espacio rueda; clic izquierdo sobre un muñeco se acerca y ataca.\n" +
               "Inventario (I): selecciona una runa y pulsa Grabar. Tirar hace caer el objeto al suelo; haz clic en él para recogerlo.\n" +
               "La herrera Brunna repara las armas (Reparar).";
    }

    private static void ApplyWeaponData(ItemDefinition item, WeaponType type, float speed, int durability, int runes,
        StatModifier[] req, string model, string projectile)
    {
        item.weaponType = type;
        item.attackSpeed = speed;
        item.damageSpread = 0.15f;
        item.maxDurability = durability;
        item.runeSlots = runes;
        item.requiredStats = new List<StatModifier>(req ?? new StatModifier[0]);
        item.weaponModel = LoadModel(model);
        item.projectileModel = string.IsNullOrEmpty(projectile) ? null : LoadModel(projectile);
        item.runeEffect = RuneEffect.None;
    }

    private static GameObject LoadModel(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "/" + name + ".fbx");

    // ---------- Modelos y materiales ----------

    private static void ConfigureModels()
    {
        var mats = new Dictionary<string, Material>();
        Shader toon = Shader.Find("Frostbound/Toon");
        foreach (var pair in Colors)
        {
            string path = MatDir + "/" + pair.Key + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(toon);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = toon;
            m.SetFloat("_VertexColorMode", 0f);
            m.SetColor("_BaseColor", pair.Value);
            m.SetColor("_EmissionColor", pair.Key == "W_Ice" ? new Color(0.15f, 0.35f, 0.5f) : Color.black);
            m.SetFloat("_Cull", 0f);
            m.SetColor("_ShadowColor", new Color(0.8f, 0.82f, 0.9f));
            m.SetFloat("_AmbientStrength", 0.3f);
            m.SetFloat("_OutlineWidth", 1.6f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            mats[pair.Key] = m;
        }

        if (!Directory.Exists(ModelDir)) return;
        foreach (string file in Directory.GetFiles(ModelDir, "*.fbx"))
        {
            string path = file.Replace('\\', '/');
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            if (imp == null) continue;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importBlendShapes = false;
            imp.animationType = ModelImporterAnimationType.None;
            foreach (var pair in mats)
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            // Materiales de las armas del paquete: W_Knight_Steel → Assets/Materials/Gear/Knight_Steel.
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { GearMatDir }))
            {
                Material gm = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (gm != null) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "W_" + gm.name), gm);
            }
            imp.SaveAndReimport();
        }
    }

    // ---------- Iconos ----------

    // Renderiza el modelo en diagonal sobre fondo transparente (con el contorno del shader toon).
    private static Sprite RenderIcon(GameObject model, string name, WeaponType type)
    {
        if (model == null) return null;
        const int size = 256;
        int layer = LayerMask.NameToLayer("UIPreview");
        if (layer < 0) layer = 31;

        var root = new GameObject("IconoTemp") { hideFlags = HideFlags.HideAndDontSave };
        root.transform.position = new Vector3(0f, -800f, 0f);
        GameObject m = Object.Instantiate(model, root.transform, false);
        bool bow = type == WeaponType.Bow || type == WeaponType.Longbow;
        m.transform.localRotation = Quaternion.Euler(0f, 0f, -45f) * Quaternion.Euler(0f, bow ? 90f : 12f, 0f);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

        Bounds b = new Bounds(root.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }

        var camGo = new GameObject("IconoCam") { hideFlags = HideFlags.HideAndDontSave };
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y) * 1.12f + 0.02f;
        cam.transform.position = b.center - Vector3.forward * 5f;
        cam.transform.rotation = Quaternion.identity;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << layer;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 20f;

        var light = new GameObject("IconoLuz") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        light.intensity = 1.1f;
        light.cullingMask = 1 << layer;

        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        cam.targetTexture = rt;
        var req = new RenderPipeline.StandardRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
        else cam.Render();

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        string path = WritePng(tex, name);
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(light.gameObject);
        Object.DestroyImmediate(root);
        return ImportSprite(path);
    }

    // Piedra rúnica: disco de piedra y un glifo brillante del color de la runa.
    private static Sprite PaintRuneIcon(RuneEffect effect, string name)
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        Color glyph = WeaponCatalog.RuneColor(effect);
        Vector2[][] strokes = Glyph(effect);
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 q = (p - c) / radius;
                // Piedra algo irregular.
                float ang = Mathf.Atan2(q.y, q.x);
                float edge = 1f + 0.05f * Mathf.Sin(ang * 5f) + 0.03f * Mathf.Sin(ang * 11f + 1f);
                float d = q.magnitude / edge;
                Color col = new Color(0f, 0f, 0f, 0f);
                if (d < 1.06f)
                {
                    float shade = 0.55f + 0.25f * (-q.x * 0.5f + q.y * 0.7f);
                    Color stone = Color.Lerp(new Color(0.24f, 0.28f, 0.36f), new Color(0.55f, 0.6f, 0.7f), Mathf.Clamp01(shade));
                    Color outline = new Color(0.06f, 0.09f, 0.15f);
                    col = d > 0.95f ? outline : stone;
                    col.a = Mathf.Clamp01((1.06f - d) * 30f);

                    Vector2 g = new Vector2(q.x, q.y) / 0.62f;
                    float dist = float.MaxValue;
                    foreach (Vector2[] s in strokes)
                        for (int i = 0; i < s.Length - 1; i++) dist = Mathf.Min(dist, SegDist(g, s[i], s[i + 1]));
                    float core = Mathf.Clamp01((0.11f - dist) * 40f);
                    float glow = Mathf.Clamp01((0.3f - dist) * 3.5f) * 0.55f;
                    if (d < 0.93f)
                    {
                        col = Color.Lerp(col, glyph * 0.9f + new Color(0.1f, 0.1f, 0.1f), glow);
                        col = Color.Lerp(col, Color.Lerp(glyph, Color.white, 0.45f), core);
                    }
                    col.a = Mathf.Clamp01((1.06f - d) * 30f);
                }
                px[y * size + x] = col;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return ImportSprite(WritePng(tex, name));
    }

    private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
        return (p - (a + ab * t)).magnitude;
    }

    private static Vector2 V(float x, float y) => new Vector2(x, y);

    // Glifos inventados, de trazos rectos como las runas talladas.
    private static Vector2[][] Glyph(RuneEffect e)
    {
        switch (e)
        {
            case RuneEffect.Fire: return new[] { new[] { V(0.35f, 0.8f), V(-0.35f, 0f), V(0.35f, -0.8f) }, new[] { V(-0.05f, 0.35f), V(0.2f, 0f), V(-0.05f, -0.35f) } };
            case RuneEffect.Poison: return new[] { new[] { V(0f, -0.85f), V(0f, 0.85f) }, new[] { V(-0.5f, 0.75f), V(0f, 0.2f), V(0.5f, 0.75f) }, new[] { V(-0.3f, -0.4f), V(0.3f, -0.4f) } };
            case RuneEffect.Frost: return new[] { new[] { V(0f, -0.85f), V(0f, 0.85f) }, new[] { V(-0.74f, -0.42f), V(0.74f, 0.42f) }, new[] { V(-0.74f, 0.42f), V(0.74f, -0.42f) } };
            case RuneEffect.Chain: return new[] { new[] { V(0.25f, 0.9f), V(-0.35f, 0.1f), V(0.3f, 0.05f), V(-0.25f, -0.9f) } };
            case RuneEffect.Lifesteal: return new[] { new[] { V(0f, 0.85f), V(-0.55f, 0f), V(0f, -0.75f), V(0.55f, 0f), V(0f, 0.85f) }, new[] { V(0f, 0.3f), V(0f, -0.3f) } };
            case RuneEffect.Sharpness: return new[] { new[] { V(0f, -0.85f), V(0f, 0.85f) }, new[] { V(-0.5f, 0.35f), V(0f, 0.85f), V(0.5f, 0.35f) } };
            case RuneEffect.Swiftness: return new[] { new[] { V(-0.6f, 0.6f), V(-0.1f, 0f), V(-0.6f, -0.6f) }, new[] { V(0.05f, 0.6f), V(0.55f, 0f), V(0.05f, -0.6f) } };
            case RuneEffect.Knockback: return new[] { new[] { V(-0.55f, -0.8f), V(-0.55f, 0.8f), V(0f, 0.2f), V(0.55f, 0.8f), V(0.55f, -0.8f) } };
            default: return new[] { new[] { V(0f, -0.8f), V(0f, 0.8f) } };
        }
    }

    private static string WritePng(Texture2D tex, string name)
    {
        string path = IconDir + "/" + name + ".png";
        File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return path;
    }

    private static Sprite ImportSprite(string path)
    {
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

    // ---------- Escena ----------

    private static string SetupScene(Dictionary<string, ItemDefinition> items)
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        GameObject player = GameObject.Find("Player");
        if (player == null) return "• No hay Player en la escena.";

        PlayerCombat combat = player.GetComponent<PlayerCombat>();
        if (combat == null) combat = player.AddComponent<PlayerCombat>();
        combat.defaultArrow = LoadModel("W_arrow");
        combat.lootSpriteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        EditorUtility.SetDirty(combat);

        PenguinOutfit outfit = player.GetComponentInChildren<PenguinOutfit>(true);
        if (outfit != null && outfit.GetComponent<WeaponHolder>() == null) outfit.gameObject.AddComponent<WeaponHolder>();
        Equipment eq = player.GetComponent<Equipment>();
        if (eq != null && outfit != null)
        {
            eq.weaponHolder = outfit.GetComponent<WeaponHolder>();
            EditorUtility.SetDirty(eq);
        }

        GameObject town = GameObject.Find(SetupVillage.RootName);
        Transform parent = town != null ? town.transform : null;
        GameObject old = GameObject.Find("Campo_Practica");
        if (old != null) Object.DestroyImmediate(old);
        var field = new GameObject("Campo_Practica");
        if (parent != null) field.transform.SetParent(parent, true);

        Material straw = VillageMat("Paja", new Color(0.86f, 0.72f, 0.38f));
        Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Village/Wood.mat");
        Material cloth = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Village/ClothRed.mat");
        Vector3[] spots = { Polar(30f, 9.6f), Polar(55f, 9.2f), Polar(80f, 9.4f) };
        for (int i = 0; i < spots.Length; i++) Dummy(field.transform, "Muneco_Practica_" + (i + 1), spots[i], straw, wood, cloth);

        GameObject lootOld = GameObject.Find("Botin_Prueba");
        if (lootOld != null) Object.DestroyImmediate(lootOld);
        var loot = new GameObject("Botin_Prueba");
        Vector3 p0 = player.transform.position;
        string[] ids = { "sword", "bow", "knife", "katana", "crossbow", "throwing_kunai", "rune_fire", "rune_poison", "rune_frost", "rune_chain", "rune_lifesteal" };
        for (int i = 0; i < ids.Length; i++)
        {
            if (!items.TryGetValue(ids[i], out ItemDefinition item)) continue;
            float a = -70f + i * (140f / (ids.Length - 1));
            Vector3 pos = p0 + Quaternion.Euler(0f, a, 0f) * Vector3.forward * (i % 2 == 0 ? 2.6f : 3.4f) + Vector3.up * (2.5f + (i % 3) * 0.6f);
            var go = new GameObject("Botin_" + item.id);
            go.transform.SetParent(loot.transform, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(i * 37f, i * 71f, i * 13f);
            var wi = go.AddComponent<WorldItem>();
            wi.stack = new ItemStack(item, 1);
            wi.spriteMaterial = combat.lootSpriteMaterial;
        }

        // Armas nuevas expuestas en la herrería.
        GameObject forge = GameObject.Find("Herreria_Fragua");
        if (forge != null)
        {
            Transform rackOld = forge.transform.Find("Armero_Armas");
            if (rackOld != null) Object.DestroyImmediate(rackOld.gameObject);
            var rack = new GameObject("Armero_Armas").transform;
            rack.SetParent(forge.transform, false);
            rack.localPosition = new Vector3(0f, 0f, -1.25f);
            string[] shown = { "W_claymore", "W_spear", "W_longaxe", "W_maul" };
            for (int i = 0; i < shown.Length; i++)
            {
                GameObject model = LoadModel(shown[i]);
                if (model == null) continue;
                GameObject w = (GameObject)PrefabUtility.InstantiatePrefab(model, rack);
                w.transform.localPosition = new Vector3(-0.75f + i * 0.5f, 0.05f, 0f);
                w.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                w.isStatic = true;
            }
            foreach (Transform t in forge.GetComponentsInChildren<Transform>(true))
                if (t.name == "Arma_Apoyada") t.gameObject.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "• Escena: combate en el Player, 3 muñecos de práctica junto a la herrería, armero con armas nuevas y " + ids.Length + " objetos de prueba que caen al empezar.";
    }

    private static void Dummy(Transform parent, string name, Vector3 pos, Material straw, Material wood, Material cloth)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.LookRotation(-new Vector3(pos.x, 0f, pos.z).normalized);
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);

        Prim(PrimitiveType.Cylinder, "Poste", visual, new Vector3(0f, 0.7f, 0f), new Vector3(0.12f, 0.7f, 0.12f), wood);
        Prim(PrimitiveType.Cylinder, "Base", visual, new Vector3(0f, 0.06f, 0f), new Vector3(0.7f, 0.06f, 0.7f), wood);
        Prim(PrimitiveType.Sphere, "Cuerpo", visual, new Vector3(0f, 0.95f, 0f), new Vector3(0.7f, 0.85f, 0.6f), straw);
        Prim(PrimitiveType.Sphere, "Cabeza", visual, new Vector3(0f, 1.55f, 0f), new Vector3(0.42f, 0.42f, 0.42f), straw);
        Prim(PrimitiveType.Cylinder, "Brazos", visual, new Vector3(0f, 1.1f, 0f), new Vector3(0.08f, 0.55f, 0.08f), wood).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Prim(PrimitiveType.Cylinder, "Faja", visual, new Vector3(0f, 0.95f, 0f), new Vector3(0.72f, 0.06f, 0.62f), cloth);
        Prim(PrimitiveType.Cube, "Diana", visual, new Vector3(0f, 1.0f, 0.3f), new Vector3(0.22f, 0.22f, 0.02f), cloth);

        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.9f, 0f);
        col.radius = 0.38f;
        col.height = 1.8f;
        var dmg = root.AddComponent<Damageable>();
        dmg.displayName = "Muñeco de práctica";
        dmg.maxHealth = 250f;
        dmg.trainingDummy = true;
        dmg.popupHeight = 1.9f;
        dmg.visual = visual;
        var tag = root.AddComponent<NameTag>();
        tag.displayName = "Muñeco de práctica";
        tag.subtitle = "";
    }

    private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static Material VillageMat(string name, Color color)
    {
        string path = "Assets/Materials/Village/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Vector3 Polar(float deg, float r)
    {
        float a = deg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
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
