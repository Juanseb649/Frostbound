using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Construye el poblado de los pingüinos: una plaza de mercado amurallada (inspirada en los mercados de pueblo
// de los Zelda clásicos) con fuente helada, casas, tiendas, puestos, faroles, banderines y dos puertas.
public static class SetupVillage
{
    private const string ScenePath = SceneIds.VillagePath;
    private const string MatFolder = "Assets/Materials/Village";
    private const string MeshFolder = "Assets/Meshes";
    private const string ConeMeshPath = MeshFolder + "/Cone_LowPoly.asset";
    private const string PrismMeshPath = MeshFolder + "/Prism_Roof.asset";
    private const string IglooTexPath = "Assets/Textures/igloo_blocks.png";
    private const string CobbleTexPath = "Assets/Textures/cobblestone.png";
    public const string RootName = "Poblado";
    private static readonly string[] LegacyRoots = { "Campamento", "Poblado" };

    public const float WallRadius = 32f;
    private const float PlazaRadius = 14f;
    private const float BuildingRadius = 20f;
    private const float GateHalfAngle = 8f;
    private const int LayoutSeed = 2026;
    public static readonly Vector3 FirePos = new Vector3(-7f, 0f, -7f);
    public static readonly Vector3 SteveClearing = new Vector3(-11f, 0f, -47f);
    public static readonly Vector3 PlayerSpawn = new Vector3(0f, 0.05f, -11f);

    private static Transform _root;
    private static System.Random _rng;
    private static Mesh _cone, _prism;
    private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

    private static readonly Color[] PenguinColors =
    {
        new Color(0.85f, 0.15f, 0.15f), new Color(0.15f, 0.60f, 0.22f), new Color(1.00f, 0.55f, 0.75f),
        new Color(1.00f, 0.85f, 0.20f), new Color(1.00f, 0.55f, 0.10f), new Color(0.40f, 0.75f, 1.00f),
        new Color(0.55f, 0.30f, 0.80f), new Color(0.50f, 0.30f, 0.15f), new Color(0.14f, 0.14f, 0.16f),
        new Color(0.60f, 0.90f, 0.20f), new Color(0.20f, 0.85f, 0.80f), new Color(0.10f, 0.35f, 0.22f),
        new Color(1.00f, 0.72f, 0.52f)
    };

    private struct Spots
    {
        public Transform fire, forge, stall, tavern, plazaCenter, steveFire;
        public Vector3 elderDoor, smith, merchant, tavernKeeper;
    }

    [MenuItem("Tools/Frostbound/Construir Poblado")]
    public static void Build()
    {
        if (!FrostboundBridge.ConfirmSave()) return;
        BuildScene(true);
    }

    public static void BuildScene(bool showDialog)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        _rng = new System.Random(LayoutSeed);
        Mats.Clear();
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Materials", "Village");
        EnsureFolder("Assets", "Meshes");
        EnsureFolder("Assets", "Textures");
        _cone = GetConeMesh();
        _prism = GetPrismMesh();
        Texture2D iglooTex = GetIglooTexture();
        Texture2D cobbleTex = GetCobbleTexture();

        foreach (string legacy in LegacyRoots)
        {
            GameObject old = GameObject.Find(legacy);
            if (old != null) Object.DestroyImmediate(old);
        }
        _root = new GameObject(RootName).transform;

        CreateMaterials(iglooTex, cobbleTex);

        var spots = new Spots();
        spots.plazaCenter = Group("Centro_Plaza", Vector3.zero);
        BuildPlaza();
        BuildFountain();
        spots.fire = BuildCampfire(FirePos, "Hoguera", 1f);
        BuildBenches(FirePos);
        BuildTown(ref spots);
        BuildStalls();
        BuildLampsAndBunting();
        BuildWalls();
        BuildGate(0f, true);
        BuildGate(180f, false);
        BuildOutsidePaths();
        spots.steveFire = BuildSteveClearing();
        BuildSnowMounds();
        BuildForest();
        BuildMountain();
        BuildSnowfall();
        BuildZoneExit();
        BuildVillagers(spots);
        SetupSteve.Add(_root, spots.steveFire);
        PlacePlayer();
        EnsureHUD();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        if (!showDialog) return;
        FrostboundBridge.Dialog("Frostbound",
            "Poblado construido en " + ScenePath + ".\n\n" +
            "Plaza con fuente helada, herrería, tienda, taberna, casas, puestos, faroles y muralla con puerta norte " +
            "(hacia The Frostspire) y puerta sur (hacia el bosque, donde toca Steve).\n\n" +
            "Haz clic en un pingüino para hablar con él.", "OK");
    }

    // ---------- Materiales ----------

    private static void CreateMaterials(Texture2D iglooTex, Texture2D cobbleTex)
    {
        Mat("Igloo", Color.white, 0.35f, null, iglooTex);
        Mat("IglooDoor", new Color(0.05f, 0.08f, 0.15f), 0f);
        Mat("Wood", new Color(0.45f, 0.28f, 0.15f), 0.1f);
        Mat("WoodDark", new Color(0.30f, 0.18f, 0.10f), 0.1f);
        Mat("Stone", new Color(0.52f, 0.56f, 0.63f), 0.15f);
        Mat("StoneDark", new Color(0.38f, 0.42f, 0.50f), 0.15f);
        Mat("WallStone", new Color(0.70f, 0.76f, 0.85f), 0.15f);
        Mat("Snow", new Color(0.93f, 0.96f, 1.00f), 0.4f);
        Mat("SnowPath", new Color(0.78f, 0.86f, 0.93f), 0.2f);
        Mat("Paving", new Color(0.86f, 0.89f, 0.94f), 0.2f, null, cobbleTex, new Vector2(7f, 7f));
        Mat("PavingRoad", new Color(0.86f, 0.89f, 0.94f), 0.2f, null, cobbleTex, new Vector2(1.5f, 5f));
        Mat("Pine", new Color(0.10f, 0.38f, 0.35f), 0.1f);
        Mat("Mountain", new Color(0.36f, 0.42f, 0.55f), 0.1f);
        Mat("Flame", new Color(1f, 0.55f, 0.1f), 0f, new Color(4f, 1.6f, 0.2f));
        Mat("FlameCore", new Color(1f, 0.9f, 0.4f), 0f, new Color(5f, 3.5f, 1f));
        Mat("Ember", new Color(0.9f, 0.3f, 0.05f), 0f, new Color(3f, 0.6f, 0.05f));
        Mat("Iron", new Color(0.22f, 0.24f, 0.28f), 0.6f);
        Mat("ClothRed", new Color(0.80f, 0.18f, 0.20f), 0.1f);
        Mat("ClothBlue", new Color(0.20f, 0.45f, 0.85f), 0.1f);
        Mat("ClothGreen", new Color(0.22f, 0.60f, 0.33f), 0.1f);
        Mat("ClothYellow", new Color(0.96f, 0.76f, 0.22f), 0.1f);
        Mat("ClothWhite", new Color(0.96f, 0.96f, 0.93f), 0.1f);
        Mat("ClothPurple", new Color(0.50f, 0.30f, 0.75f), 0.1f);
        Mat("Ice", new Color(0.62f, 0.85f, 0.98f), 0.85f);
        Mat("IceStatue", new Color(0.74f, 0.91f, 1.00f), 0.9f, new Color(0.08f, 0.2f, 0.32f));
        Mat("Fish", new Color(0.55f, 0.68f, 0.80f), 0.7f);
        Mat("Amp", new Color(0.10f, 0.10f, 0.12f), 0.3f);
        Mat("Door", new Color(0.36f, 0.22f, 0.12f), 0.1f);
        Mat("WindowGlow", new Color(1f, 0.82f, 0.45f), 0.2f, new Color(2.2f, 1.5f, 0.6f));
        Mat("Wall_Cream", new Color(0.93f, 0.86f, 0.70f), 0.1f);
        Mat("Wall_Ice", new Color(0.66f, 0.80f, 0.93f), 0.1f);
        Mat("Wall_Terracotta", new Color(0.85f, 0.55f, 0.42f), 0.1f);
        Mat("Wall_Sage", new Color(0.72f, 0.84f, 0.66f), 0.1f);
        Mat("Wall_Rose", new Color(0.91f, 0.72f, 0.78f), 0.1f);
        Mat("Roof_Red", new Color(0.62f, 0.24f, 0.20f), 0.15f);
        Mat("Roof_Slate", new Color(0.24f, 0.33f, 0.50f), 0.15f);
        Mat("Roof_Green", new Color(0.20f, 0.42f, 0.33f), 0.15f);
    }

    private static Material Mat(string name, Color color, float smoothness, Color? emission = null, Texture2D tex = null, Vector2? tiling = null)
    {
        string path = MatFolder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(LitShader());
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (tex != null)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            Vector2 t = tiling ?? Vector2.one;
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTextureScale("_MainTex", t);
        }
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        Mats[name] = m;
        return m;
    }

    private static Shader LitShader()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        return s;
    }

    // ---------- Plaza y fuente ----------

    private static void BuildPlaza()
    {
        Transform g = Group("Plaza", Vector3.zero);
        Prim(PrimitiveType.Cylinder, "Borde_Plaza", g, new Vector3(0f, 0.006f, 0f), Vector3.zero,
            new Vector3(PlazaRadius * 2f + 1.2f, 0.006f, PlazaRadius * 2f + 1.2f), "StoneDark", false);
        Prim(PrimitiveType.Cylinder, "Empedrado", g, new Vector3(0f, 0.014f, 0f), Vector3.zero,
            new Vector3(PlazaRadius * 2f, 0.01f, PlazaRadius * 2f), "Paving", false);

        Road(g, "Calle_Norte", 0f, PlazaRadius - 0.5f, WallRadius + 1.5f, 5f);
        Road(g, "Calle_Sur", 180f, PlazaRadius - 0.5f, WallRadius + 1.5f, 5f);
        Road(g, "Callejon_Este", 90f, PlazaRadius - 0.5f, WallRadius - 3f, 3f);
        Road(g, "Callejon_Oeste", 270f, PlazaRadius - 0.5f, WallRadius - 3f, 3f);
    }

    private static void Road(Transform parent, string name, float deg, float from, float to, float width)
    {
        Vector3 a = Polar(deg, from);
        Vector3 b = Polar(deg, to);
        Transform t = Prim(PrimitiveType.Cube, name, parent, (a + b) * 0.5f + Vector3.up * 0.012f, Vector3.zero,
            new Vector3(width, 0.01f, Vector3.Distance(a, b)), "PavingRoad", false).transform;
        t.rotation = Quaternion.LookRotation((b - a).normalized);
    }

    private static void BuildFountain()
    {
        Transform g = Group("Fuente_Helada", Vector3.zero);
        Prim(PrimitiveType.Cylinder, "Pileta", g, new Vector3(0f, 0.35f, 0f), Vector3.zero, new Vector3(6.4f, 0.35f, 6.4f), "WallStone", false);
        Prim(PrimitiveType.Cylinder, "Pileta_Borde", g, new Vector3(0f, 0.72f, 0f), Vector3.zero, new Vector3(6.7f, 0.04f, 6.7f), "Snow", false);
        Prim(PrimitiveType.Cylinder, "Agua_Congelada", g, new Vector3(0f, 0.7f, 0f), Vector3.zero, new Vector3(5.8f, 0.03f, 5.8f), "Ice", false);
        Prim(PrimitiveType.Cylinder, "Columna", g, new Vector3(0f, 1.6f, 0f), Vector3.zero, new Vector3(0.8f, 0.95f, 0.8f), "WallStone", false);
        Prim(PrimitiveType.Cylinder, "Taza", g, new Vector3(0f, 2.55f, 0f), Vector3.zero, new Vector3(2.6f, 0.14f, 2.6f), "WallStone", false);
        Prim(PrimitiveType.Cylinder, "Taza_Hielo", g, new Vector3(0f, 2.7f, 0f), Vector3.zero, new Vector3(2.3f, 0.03f, 2.3f), "Ice", false);

        for (int i = 0; i < 10; i++)
        {
            float deg = i * 36f + Rand(-8f, 8f);
            Vector3 edge = Polar(deg, 1.25f);
            float len = Rand(0.35f, 0.8f);
            GameObject icicle = new GameObject("Carambano");
            icicle.transform.SetParent(g, false);
            icicle.transform.localPosition = edge + Vector3.up * 2.45f;
            icicle.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            icicle.transform.localScale = new Vector3(0.14f, len, 0.14f);
            icicle.isStatic = true;
            icicle.AddComponent<MeshFilter>().sharedMesh = _cone;
            icicle.AddComponent<MeshRenderer>().sharedMaterial = Mats["Ice"];
        }

        GameObject statueModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Penguin.obj");
        if (statueModel != null)
        {
            var statue = (GameObject)PrefabUtility.InstantiatePrefab(statueModel, g);
            statue.name = "Estatua_Fundador";
            statue.transform.localPosition = new Vector3(0f, 2.72f, 0f);
            statue.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            statue.transform.localScale = Vector3.one;
            Bounds? sb = GetBounds(statue, "");
            float statueHeight = sb.HasValue ? sb.Value.size.y : 1f;
            statue.transform.localScale = Vector3.one * (1.5f / Mathf.Max(0.05f, statueHeight));
            Bounds? sb2 = GetBounds(statue, "");
            if (sb2.HasValue) statue.transform.position += Vector3.up * (g.position.y + 2.72f - sb2.Value.min.y);
            foreach (Renderer r in statue.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = Mats["IceStatue"];
                r.sharedMaterials = mats;
            }
            foreach (Collider c in statue.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }
        else
        {
            Cone("Cristal", g, new Vector3(0f, 2.7f, 0f), new Vector3(0.7f, 2.2f, 0.7f), "IceStatue");
        }

        Light l = AddPointLight(g, new Vector3(0f, 3.8f, 0f), new Color(0.55f, 0.8f, 1f), 1.2f, 7f, false);
        l.name = "Luz_Fuente";

        CapsuleCollider block = g.gameObject.AddComponent<CapsuleCollider>();
        block.center = new Vector3(0f, 1.5f, 0f);
        block.radius = 3.3f;
        block.height = 3f;
    }

    // ---------- Hoguera ----------

    private static Transform BuildCampfire(Vector3 pos, string name, float scale)
    {
        Transform fire = Group(name, pos);
        fire.localScale = Vector3.one * scale;

        for (int i = 0; i < 9; i++)
        {
            float a = i * Mathf.PI * 2f / 9f;
            Prim(PrimitiveType.Sphere, "Piedra", fire, new Vector3(Mathf.Cos(a) * 1.1f, 0.08f, Mathf.Sin(a) * 1.1f),
                Vector3.zero, new Vector3(0.5f, 0.3f, 0.45f), "Stone", false);
        }
        for (int i = 0; i < 4; i++)
        {
            Prim(PrimitiveType.Cylinder, "Tronco", fire, new Vector3(0f, 0.15f, 0f),
                new Vector3(0f, i * 45f, 80f), new Vector3(0.2f, 0.6f, 0.2f), "WoodDark", false);
        }
        Prim(PrimitiveType.Sphere, "Brasas", fire, new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(1.1f, 0.15f, 1.1f), "Ember", false);
        Transform f1 = Prim(PrimitiveType.Sphere, "Llama", fire, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(0.7f, 1.0f, 0.7f), "Flame", false).transform;
        Transform f2 = Prim(PrimitiveType.Sphere, "LlamaNucleo", fire, new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(0.4f, 0.7f, 0.4f), "FlameCore", false).transform;

        f1.gameObject.isStatic = false;
        f2.gameObject.isStatic = false;
        Light light = AddPointLight(fire, new Vector3(0f, 1.4f, 0f), new Color(1f, 0.6f, 0.3f), 3f * scale, 12f * scale, scale >= 1f);
        FlickerLight flicker = fire.gameObject.AddComponent<FlickerLight>();
        flicker.targetLight = light;
        flicker.flames = new[] { f1, f2 };
        flicker.baseIntensity = 3f * scale;

        CapsuleCollider block = fire.gameObject.AddComponent<CapsuleCollider>();
        block.center = new Vector3(0f, 0.5f, 0f);
        block.radius = 1.1f;
        block.height = 1f;
        return fire;
    }

    private static void BuildBenches(Vector3 center)
    {
        Transform g = Group("Bancos", center);
        for (int i = 0; i < 4; i++)
        {
            float deg = 45f + i * 90f;
            Vector3 p = Polar(deg, 3.4f);
            Prim(PrimitiveType.Cylinder, "Banco", g, p + Vector3.up * 0.22f, new Vector3(0f, deg, 90f),
                new Vector3(0.45f, 0.9f, 0.45f), "Wood", true);
        }
    }

    // ---------- Edificios ----------

    private enum Sign { None, Smith, Shop, Tavern }

    private static void BuildTown(ref Spots spots)
    {
        Transform g = Group("Edificios", Vector3.zero);

        House(g, "Casa_Azul", 22f, 6f, 5.5f, 4.6f, "Wall_Ice", "Roof_Slate", Sign.None, "ClothBlue");
        House(g, "Herreria", 42f, 6.5f, 5.5f, 4.2f, "Wall_Terracotta", "Roof_Red", Sign.Smith, "ClothRed");
        BuildIgloo(g, "Iglu_Familia_A", Polar(64f, BuildingRadius + 0.5f), 2.4f);
        House(g, "Casa_Crema", 116f, 6f, 5f, 3.4f, "Wall_Cream", "Roof_Green", Sign.None, "ClothGreen");
        House(g, "Casa_Rosa", 138f, 5.5f, 5f, 3.2f, "Wall_Rose", "Roof_Slate", Sign.None, "ClothBlue");
        BuildIgloo(g, "Iglu_Familia_B", Polar(158f, BuildingRadius + 0.5f), 2.2f);

        Vector3 elderPos = Polar(204f, BuildingRadius + 1.5f);
        BuildIgloo(g, "Iglu_Anciano", elderPos, 3.2f);
        Prim(PrimitiveType.Cylinder, "Estandarte_Poste", g, elderPos - elderPos.normalized * 3.6f + Quaternion.Euler(0f, 90f, 0f) * elderPos.normalized * 2.6f + Vector3.up * 1.8f,
            Vector3.zero, new Vector3(0.15f, 1.8f, 0.15f), "WoodDark", true);
        spots.elderDoor = elderPos - elderPos.normalized * (3.2f + 2.4f);

        House(g, "Casa_Salvia", 224f, 6f, 5f, 3.4f, "Wall_Sage", "Roof_Red", Sign.None, "ClothYellow");
        BuildIgloo(g, "Iglu_Familia_C", Polar(244f, BuildingRadius + 0.5f), 2.3f);
        Transform tavern = House(g, "Taberna_ElPezDorado", 298f, 7.5f, 6f, 4.8f, "Wall_Cream", "Roof_Red", Sign.Tavern, "ClothYellow");
        House(g, "Tienda", 320f, 6f, 5.5f, 4.2f, "Wall_Ice", "Roof_Green", Sign.Shop, "ClothGreen");
        House(g, "Casa_Terracota", 340f, 5.5f, 5f, 4.4f, "Wall_Terracotta", "Roof_Slate", Sign.None, "ClothWhite");

        Vector3 forgePos = Polar(42f, BuildingRadius - 6.2f);
        spots.forge = BuildForge(forgePos);
        spots.smith = forgePos - forgePos.normalized * 1.6f;

        Vector3 stallPos = Polar(320f, BuildingRadius - 6.4f);
        spots.stall = BuildMerchantStall(stallPos);
        spots.merchant = stallPos - stallPos.normalized * 0.2f;

        Vector3 tavernFront = Polar(298f, BuildingRadius - 3f);
        spots.tavernKeeper = Polar(298f, BuildingRadius - 4.3f);
        spots.tavern = tavern;
        BuildTavernTerrace(tavernFront);
    }

    private static Transform House(Transform parent, string name, float deg, float w, float d, float h,
        string wallMat, string roofMat, Sign sign, string accent)
    {
        Vector3 pos = Polar(deg, BuildingRadius + d * 0.5f - 1f);
        Transform t = Group(name, pos);
        t.rotation = Quaternion.LookRotation(-new Vector3(pos.x, 0f, pos.z).normalized);
        t.SetParent(parent, true);
        float front = d * 0.5f;

        Prim(PrimitiveType.Cube, "Zocalo", t, new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(w + 0.3f, 0.5f, d + 0.3f), "StoneDark", false);
        Prim(PrimitiveType.Cube, "Muros", t, new Vector3(0f, h * 0.5f, 0f), Vector3.zero, new Vector3(w, h, d), wallMat, true);
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Viga", t, new Vector3(sx * w * 0.5f, h * 0.5f, sz * front), Vector3.zero, new Vector3(0.24f, h, 0.24f), "WoodDark", false);
        Prim(PrimitiveType.Cube, "Viga_Frontal", t, new Vector3(0f, h - 0.1f, front + 0.03f), Vector3.zero, new Vector3(w + 0.1f, 0.22f, 0.12f), "WoodDark", false);
        if (h > 4f) Prim(PrimitiveType.Cube, "Viga_Piso", t, new Vector3(0f, 2.55f, front + 0.03f), Vector3.zero, new Vector3(w, 0.16f, 0.1f), "WoodDark", false);

        float doorX = sign == Sign.None ? Rand(-0.8f, 0.8f) : 0f;
        Prim(PrimitiveType.Cube, "Puerta", t, new Vector3(doorX, 1.05f, front + 0.04f), Vector3.zero, new Vector3(1.1f, 2.1f, 0.1f), "Door", false);
        Prim(PrimitiveType.Cube, "Marco_Puerta", t, new Vector3(doorX, 2.18f, front + 0.06f), Vector3.zero, new Vector3(1.45f, 0.2f, 0.14f), "WoodDark", false);
        Prim(PrimitiveType.Cube, "Escalon", t, new Vector3(doorX, 0.08f, front + 0.45f), Vector3.zero, new Vector3(1.5f, 0.16f, 0.6f), "StoneDark", false);

        foreach (float sx in new[] { -1f, 1f })
        {
            float wx = doorX + sx * Mathf.Max(1.6f, w * 0.3f);
            if (Mathf.Abs(wx) > w * 0.5f - 0.6f) continue;
            Window(t, new Vector3(wx, 1.55f, front), accent);
            if (h > 4f) Window(t, new Vector3(wx, 3.35f, front), accent);
        }
        if (h > 4f) Window(t, new Vector3(doorX, 3.35f, front), accent);

        float roofH = Mathf.Clamp(w * 0.38f, 1.6f, 2.6f);
        Roof(t, "Tejado", new Vector3(0f, h, 0f), new Vector3(w + 0.8f, roofH, d + 0.8f), roofMat);
        Roof(t, "Tejado_Nieve", new Vector3(0f, h + roofH * 0.52f, 0f), new Vector3(w + 0.9f, roofH * 0.5f, (d + 0.8f) * 0.52f), "Snow");
        Prim(PrimitiveType.Cube, "Alero_Nieve", t, new Vector3(0f, h + 0.05f, front + 0.42f), Vector3.zero, new Vector3(w + 0.9f, 0.12f, 0.25f), "Snow", false);

        float cx = w * 0.28f * (Rand(0f, 1f) < 0.5f ? -1f : 1f);
        Prim(PrimitiveType.Cube, "Chimenea", t, new Vector3(cx, h + roofH * 0.55f, -d * 0.18f), Vector3.zero, new Vector3(0.6f, roofH * 1.1f, 0.6f), "StoneDark", false);
        Prim(PrimitiveType.Cube, "Chimenea_Nieve", t, new Vector3(cx, h + roofH * 1.1f + 0.05f, -d * 0.18f), Vector3.zero, new Vector3(0.72f, 0.14f, 0.72f), "Snow", false);

        if (sign != Sign.None) HangingSign(t, new Vector3(w * 0.5f + 0.1f, h * 0.72f, front + 0.2f), sign);
        return t;
    }

    private static void Window(Transform house, Vector3 at, string accent)
    {
        Prim(PrimitiveType.Cube, "Ventana_Marco", house, at + new Vector3(0f, 0f, 0.03f), Vector3.zero, new Vector3(1.0f, 1.0f, 0.08f), "WoodDark", false);
        Prim(PrimitiveType.Cube, "Ventana_Luz", house, at + new Vector3(0f, 0f, 0.07f), Vector3.zero, new Vector3(0.78f, 0.78f, 0.04f), "WindowGlow", false);
        Prim(PrimitiveType.Cube, "Ventana_Cruz", house, at + new Vector3(0f, 0f, 0.1f), Vector3.zero, new Vector3(0.08f, 0.8f, 0.03f), "WoodDark", false);
        Prim(PrimitiveType.Cube, "Ventana_Cruz", house, at + new Vector3(0f, 0f, 0.1f), Vector3.zero, new Vector3(0.8f, 0.08f, 0.03f), "WoodDark", false);
        Prim(PrimitiveType.Cube, "Alfeizar_Nieve", house, at + new Vector3(0f, -0.55f, 0.12f), Vector3.zero, new Vector3(1.1f, 0.1f, 0.26f), "Snow", false);
        foreach (float sx in new[] { -1f, 1f })
            Prim(PrimitiveType.Cube, "Contraventana", house, at + new Vector3(sx * 0.72f, 0f, 0.05f), Vector3.zero, new Vector3(0.4f, 0.95f, 0.06f), accent, false);
    }

    private static void HangingSign(Transform house, Vector3 at, Sign sign)
    {
        Prim(PrimitiveType.Cube, "Letrero_Brazo", house, at + new Vector3(0.55f, 0f, 0f), Vector3.zero, new Vector3(1.2f, 0.1f, 0.1f), "Iron", false);
        Prim(PrimitiveType.Cube, "Letrero", house, at + new Vector3(0.75f, -0.5f, 0f), Vector3.zero, new Vector3(0.9f, 0.7f, 0.1f), "Wood", false);
        Vector3 icon = at + new Vector3(0.75f, -0.5f, 0f);
        switch (sign)
        {
            case Sign.Smith:
                foreach (float s in new[] { -1f, 1f })
                {
                    Prim(PrimitiveType.Cube, "Icono_Martillo_Mango", house, icon + new Vector3(0f, 0f, s * 0.07f), new Vector3(0f, 0f, 35f), new Vector3(0.08f, 0.5f, 0.03f), "WoodDark", false);
                    Prim(PrimitiveType.Cube, "Icono_Martillo_Cabeza", house, icon + new Vector3(-0.12f, 0.17f, s * 0.07f), new Vector3(0f, 0f, 35f), new Vector3(0.3f, 0.14f, 0.04f), "Iron", false);
                }
                break;
            case Sign.Shop:
                foreach (float s in new[] { -1f, 1f })
                {
                    Prim(PrimitiveType.Sphere, "Icono_Bolsa", house, icon + new Vector3(0f, -0.05f, s * 0.07f), Vector3.zero, new Vector3(0.36f, 0.34f, 0.05f), "ClothYellow", false);
                    Prim(PrimitiveType.Cube, "Icono_Bolsa_Nudo", house, icon + new Vector3(0f, 0.16f, s * 0.07f), Vector3.zero, new Vector3(0.14f, 0.08f, 0.05f), "WoodDark", false);
                }
                break;
            case Sign.Tavern:
                foreach (float s in new[] { -1f, 1f })
                {
                    Prim(PrimitiveType.Sphere, "Icono_Pez", house, icon + new Vector3(0.06f, 0f, s * 0.07f), Vector3.zero, new Vector3(0.42f, 0.2f, 0.05f), "ClothYellow", false);
                    Prim(PrimitiveType.Cube, "Icono_Pez_Cola", house, icon + new Vector3(-0.22f, 0f, s * 0.07f), new Vector3(0f, 0f, 45f), new Vector3(0.14f, 0.14f, 0.04f), "ClothYellow", false);
                }
                break;
        }
    }

    private static void BuildTavernTerrace(Vector3 pos)
    {
        Transform g = Group("Terraza_Taberna", pos);
        g.rotation = Quaternion.LookRotation(-new Vector3(pos.x, 0f, pos.z).normalized);
        for (int i = 0; i < 2; i++)
        {
            Vector3 p = new Vector3(i == 0 ? -2.2f : 2.2f, 0f, 0.6f);
            Prim(PrimitiveType.Cylinder, "Mesa", g, p + Vector3.up * 0.75f, Vector3.zero, new Vector3(1.1f, 0.04f, 1.1f), "Wood", false);
            Prim(PrimitiveType.Cylinder, "Mesa_Pata", g, p + Vector3.up * 0.37f, Vector3.zero, new Vector3(0.15f, 0.37f, 0.15f), "WoodDark", true);
            Prim(PrimitiveType.Cylinder, "Taza", g, p + new Vector3(0.2f, 0.85f, 0.1f), Vector3.zero, new Vector3(0.14f, 0.08f, 0.14f), "ClothWhite", false);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cylinder, "Taburete", g, p + new Vector3(s * 0.85f, 0.25f, 0f), Vector3.zero, new Vector3(0.4f, 0.25f, 0.4f), "WoodDark", false);
        }
        Prim(PrimitiveType.Cylinder, "Barril", g, new Vector3(0f, 0.5f, -0.4f), Vector3.zero, new Vector3(0.7f, 0.5f, 0.7f), "Wood", true);
    }

    private static Transform BuildForge(Vector3 pos)
    {
        Transform g = Group("Herreria_Fragua", pos);
        g.rotation = Quaternion.LookRotation(-new Vector3(pos.x, 0f, pos.z).normalized);

        Prim(PrimitiveType.Cube, "Fragua", g, new Vector3(-1.2f, 0.5f, 0f), Vector3.zero, new Vector3(1.2f, 1f, 1.2f), "Stone", true);
        Transform coals = Prim(PrimitiveType.Cube, "Fragua_Brasas", g, new Vector3(-1.2f, 1.01f, 0f), Vector3.zero, new Vector3(0.9f, 0.05f, 0.9f), "Ember", false).transform;
        coals.gameObject.isStatic = false;
        Light l = AddPointLight(g, new Vector3(-1.2f, 1.6f, 0f), new Color(1f, 0.45f, 0.15f), 1.8f, 6f, false);
        FlickerLight fl = g.gameObject.AddComponent<FlickerLight>();
        fl.targetLight = l;
        fl.baseIntensity = 1.8f;
        fl.intensityVariation = 0.5f;
        fl.flames = new[] { coals };

        Prim(PrimitiveType.Cylinder, "Yunque_Base", g, new Vector3(0.8f, 0.3f, 0f), Vector3.zero, new Vector3(0.5f, 0.3f, 0.5f), "WoodDark", true);
        Prim(PrimitiveType.Cube, "Yunque", g, new Vector3(0.8f, 0.7f, 0f), Vector3.zero, new Vector3(0.9f, 0.25f, 0.4f), "Iron", false);
        Prim(PrimitiveType.Cube, "Yunque_Punta", g, new Vector3(1.35f, 0.72f, 0f), new Vector3(0f, 0f, -12f), new Vector3(0.35f, 0.15f, 0.25f), "Iron", false);

        Prim(PrimitiveType.Cube, "Armero", g, new Vector3(0f, 0.9f, -1.3f), Vector3.zero, new Vector3(1.6f, 0.1f, 0.2f), "WoodDark", false);
        for (int i = 0; i < 3; i++)
        {
            Prim(PrimitiveType.Cube, "Arma_Apoyada", g, new Vector3(-0.5f + i * 0.5f, 0.75f, -1.15f), new Vector3(-12f, 0f, 0f),
                new Vector3(0.08f, 1.4f, 0.12f), "Iron", false);
        }
        return g;
    }

    private static Transform BuildMerchantStall(Vector3 pos)
    {
        Transform g = Stall(Group("Puesto_Mercader", pos), 1.4f, 0.9f, "ClothRed");
        g.rotation = Quaternion.LookRotation(-new Vector3(pos.x, 0f, pos.z).normalized);
        Prim(PrimitiveType.Sphere, "Mercancia_Hielo", g, new Vector3(-0.6f, 1.12f, 0.72f), Vector3.zero, new Vector3(0.25f, 0.25f, 0.25f), "Ice", false);
        Prim(PrimitiveType.Cube, "Mercancia_Caja", g, new Vector3(0.5f, 1.1f, 0.72f), new Vector3(0f, 20f, 0f), new Vector3(0.3f, 0.2f, 0.3f), "ClothBlue", false);
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = new Vector3(i < 2 ? -2.2f : 2.2f, 0f, Rand(-1.2f, 0.2f));
            float s = Rand(0.5f, 0.75f);
            if (i % 2 == 0)
                Prim(PrimitiveType.Cylinder, "Barril", g, p + Vector3.up * s * 0.6f, Vector3.zero, new Vector3(s, s * 0.6f, s), "Wood", true);
            else
                Prim(PrimitiveType.Cube, "Caja", g, p + Vector3.up * s * 0.5f, new Vector3(0f, Rand(0f, 45f), 0f), Vector3.one * s, "WoodDark", true);
        }
        return g;
    }

    // Puesto con toldo a rayas (dos colores alternados) y mostrador.
    private static Transform Stall(Transform g, float w, float d, string stripe)
    {
        foreach (Vector2 c in new[] { new Vector2(-w, -d), new Vector2(w, -d), new Vector2(-w, d), new Vector2(w, d) })
            Prim(PrimitiveType.Cylinder, "Poste", g, new Vector3(c.x, 1.15f, c.y), Vector3.zero, new Vector3(0.12f, 1.15f, 0.12f), "Wood", true);

        const int strips = 6;
        float sw = (w * 2f + 0.5f) / strips;
        for (int i = 0; i < strips; i++)
        {
            float x = -w - 0.25f + sw * (i + 0.5f);
            Prim(PrimitiveType.Cube, "Toldo", g, new Vector3(x, 2.35f, 0.1f), new Vector3(-12f, 0f, 0f),
                new Vector3(sw + 0.01f, 0.07f, d * 2f + 0.8f), i % 2 == 0 ? stripe : "ClothWhite", false);
            Prim(PrimitiveType.Cube, "Toldo_Faldon", g, new Vector3(x, 2.05f, d + 0.55f), Vector3.zero,
                new Vector3(sw + 0.01f, 0.35f, 0.05f), i % 2 == 0 ? stripe : "ClothWhite", false);
        }
        Prim(PrimitiveType.Cube, "Toldo_Nieve", g, new Vector3(0f, 2.45f, -0.1f), new Vector3(-12f, 0f, 0f), new Vector3(w * 2f + 0.3f, 0.06f, d * 1.2f), "Snow", false);
        Prim(PrimitiveType.Cube, "Mostrador", g, new Vector3(0f, 0.5f, d * 0.8f), Vector3.zero, new Vector3(w * 2f, 1f, 0.5f), "Wood", true);
        return g;
    }

    private static void BuildStalls()
    {
        Transform g = Group("Puestos", Vector3.zero);
        float[] angles = { 72f, 108f, 146f, 282f };
        string[] stripes = { "ClothBlue", "ClothGreen", "ClothPurple", "ClothYellow" };
        for (int i = 0; i < angles.Length; i++)
        {
            Vector3 pos = Polar(angles[i], PlazaRadius - 2.2f);
            Transform s = Stall(Group("Puesto_" + (i + 1), pos), 1.1f, 0.7f, stripes[i]);
            s.SetParent(g, true);
            s.rotation = Quaternion.LookRotation(-new Vector3(pos.x, 0f, pos.z).normalized);
            float z = 0.56f;
            switch (i % 3)
            {
                case 0:
                    for (int k = 0; k < 4; k++)
                    {
                        Vector3 p = new Vector3(-0.75f + k * 0.5f, 1.08f, z);
                        Prim(PrimitiveType.Sphere, "Pescado", s, p, new Vector3(0f, Rand(-20f, 20f), 0f), new Vector3(0.38f, 0.12f, 0.16f), "Fish", false);
                    }
                    break;
                case 1:
                    for (int k = 0; k < 3; k++)
                        Prim(PrimitiveType.Cube, "Bloque_Hielo", s, new Vector3(-0.6f + k * 0.6f, 1.15f, z), new Vector3(0f, Rand(0f, 30f), 0f), Vector3.one * 0.28f, "Ice", false);
                    break;
                default:
                    for (int k = 0; k < 3; k++)
                        Prim(PrimitiveType.Cylinder, "Tarro", s, new Vector3(-0.6f + k * 0.6f, 1.14f, z), Vector3.zero, new Vector3(0.22f, 0.14f, 0.22f), k == 1 ? "ClothRed" : "ClothYellow", false);
                    break;
            }
            Prim(PrimitiveType.Cube, "Caja", s, new Vector3(1.5f, 0.3f, -0.2f), new Vector3(0f, Rand(0f, 40f), 0f), Vector3.one * 0.6f, "WoodDark", true);
        }
    }

    private static void BuildLampsAndBunting()
    {
        Transform g = Group("Faroles", Vector3.zero);
        const int count = 8;
        const float r = PlazaRadius + 0.3f;
        var tops = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            float deg = 22.5f + i * 45f;
            Vector3 p = Polar(deg, r);
            Transform lamp = Group("Farol", p);
            lamp.SetParent(g, true);
            Prim(PrimitiveType.Cylinder, "Poste", lamp, new Vector3(0f, 1.6f, 0f), Vector3.zero, new Vector3(0.16f, 1.6f, 0.16f), "Iron", true);
            Prim(PrimitiveType.Cylinder, "Base", lamp, new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(0.4f, 0.12f, 0.4f), "Iron", false);
            Prim(PrimitiveType.Cube, "Farol_Luz", lamp, new Vector3(0f, 3.4f, 0f), Vector3.zero, new Vector3(0.36f, 0.45f, 0.36f), "WindowGlow", false);
            Prim(PrimitiveType.Cube, "Farol_Techo", lamp, new Vector3(0f, 3.7f, 0f), new Vector3(0f, 45f, 0f), new Vector3(0.5f, 0.12f, 0.5f), "Iron", false);
            Prim(PrimitiveType.Sphere, "Farol_Nieve", lamp, new Vector3(0f, 3.78f, 0f), Vector3.zero, new Vector3(0.45f, 0.12f, 0.45f), "Snow", false);
            if (i % 2 == 0) AddPointLight(lamp, new Vector3(0f, 3.3f, 0f), new Color(1f, 0.78f, 0.45f), 1.4f, 8f, false);
            tops.Add(p + Vector3.up * 3.25f);
        }

        Transform b = Group("Banderines", Vector3.zero);
        string[] colors = { "ClothRed", "ClothYellow", "ClothBlue", "ClothGreen", "ClothWhite" };
        for (int i = 0; i < count; i++)
        {
            Vector3 a = tops[i];
            Vector3 c = tops[(i + 1) % count];
            const int flags = 9;
            for (int k = 0; k <= flags; k++)
            {
                float t = k / (float)flags;
                Vector3 p = Vector3.Lerp(a, c, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.9f);
                if (k < flags)
                {
                    float t2 = (k + 1) / (float)flags;
                    Vector3 p2 = Vector3.Lerp(a, c, t2) + Vector3.down * (Mathf.Sin(t2 * Mathf.PI) * 0.9f);
                    Transform rope = Prim(PrimitiveType.Cylinder, "Cuerda", b, (p + p2) * 0.5f, Vector3.zero,
                        new Vector3(0.03f, Vector3.Distance(p, p2) * 0.5f, 0.03f), "WoodDark", false).transform;
                    rope.rotation = Quaternion.FromToRotation(Vector3.up, (p2 - p).normalized);
                }
                if (k == 0 || k == flags) continue;
                Transform flag = Prim(PrimitiveType.Cube, "Banderin", b, p + Vector3.down * 0.2f, Vector3.zero,
                    new Vector3(0.28f, 0.28f, 0.02f), colors[(i + k) % colors.Length], false).transform;
                flag.rotation = Quaternion.LookRotation((c - a).normalized.y > 0.99f ? Vector3.forward : Vector3.Cross(Vector3.up, (c - a).normalized)) * Quaternion.Euler(0f, 0f, 45f);
            }
        }
    }

    // ---------- Muralla y puertas ----------

    private static void BuildWalls()
    {
        Transform g = Group("Muralla", Vector3.zero);
        var towers = new List<float> { GateHalfAngle, 50f, 95f, 135f, 180f - GateHalfAngle, 180f + GateHalfAngle, 225f, 265f, 310f, 360f - GateHalfAngle };
        for (int i = 0; i < towers.Count; i++)
        {
            float a0 = towers[i];
            float a1 = towers[(i + 1) % towers.Count];
            bool gate = Mathf.Approximately(a0, 180f - GateHalfAngle) || Mathf.Approximately(a0, 360f - GateHalfAngle);
            bool gateTower = Mathf.Abs(Mathf.DeltaAngle(a0, 0f)) < GateHalfAngle + 0.1f || Mathf.Abs(Mathf.DeltaAngle(a0, 180f)) < GateHalfAngle + 0.1f;
            Tower(g, a0, gateTower ? 7f : 5.8f);
            if (!gate) WallSegment(g, a0, a1);
        }
    }

    private static void WallSegment(Transform parent, float a0, float a1)
    {
        Vector3 p0 = Polar(a0, WallRadius);
        Vector3 p1 = Polar(a1 < a0 ? a1 + 360f : a1, WallRadius);
        float len = Vector3.Distance(p0, p1) - 3.2f;
        Transform seg = Group("Muro", (p0 + p1) * 0.5f);
        seg.SetParent(parent, true);
        seg.rotation = Quaternion.LookRotation((p1 - p0).normalized);
        const float h = 3.6f;
        Prim(PrimitiveType.Cube, "Muro_Piedra", seg, new Vector3(0f, h * 0.5f, 0f), Vector3.zero, new Vector3(1.3f, h, len), "WallStone", true);
        Prim(PrimitiveType.Cube, "Muro_Zocalo", seg, new Vector3(0f, 0.4f, 0f), Vector3.zero, new Vector3(1.5f, 0.8f, len), "StoneDark", false);
        Prim(PrimitiveType.Cube, "Muro_Nieve", seg, new Vector3(0f, h + 0.06f, 0f), Vector3.zero, new Vector3(1.45f, 0.14f, len), "Snow", false);
        int n = Mathf.FloorToInt(len / 1.5f);
        for (int k = 0; k < n; k++)
        {
            float z = -len * 0.5f + 0.75f + k * (len - 1.5f) / Mathf.Max(1, n - 1);
            Prim(PrimitiveType.Cube, "Almena", seg, new Vector3(0f, h + 0.45f, z), Vector3.zero, new Vector3(1.3f, 0.75f, 0.7f), "WallStone", false);
            Prim(PrimitiveType.Cube, "Almena_Nieve", seg, new Vector3(0f, h + 0.86f, z), Vector3.zero, new Vector3(1.38f, 0.1f, 0.78f), "Snow", false);
        }
    }

    private static void Tower(Transform parent, float deg, float h)
    {
        Transform t = Group("Torre", Polar(deg, WallRadius));
        t.SetParent(parent, true);
        Prim(PrimitiveType.Cylinder, "Torre_Cuerpo", t, new Vector3(0f, h * 0.5f, 0f), Vector3.zero, new Vector3(3.4f, h * 0.5f, 3.4f), "WallStone", true);
        Prim(PrimitiveType.Cylinder, "Torre_Base", t, new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(3.7f, 0.5f, 3.7f), "StoneDark", false);
        Prim(PrimitiveType.Cylinder, "Torre_Corona", t, new Vector3(0f, h + 0.15f, 0f), Vector3.zero, new Vector3(4.1f, 0.2f, 4.1f), "StoneDark", false);
        Cone("Torre_Techo", t, new Vector3(0f, h + 0.3f, 0f), new Vector3(4.4f, 3.2f, 4.4f), "Roof_Slate");
        Cone("Torre_Techo_Nieve", t, new Vector3(0f, h + 1.75f, 0f), new Vector3(2.4f, 1.8f, 2.4f), "Snow");
        Vector3 outward = Polar(deg, 1f);
        Transform slit = Prim(PrimitiveType.Cube, "Aspillera", t, outward * -1.68f + Vector3.up * (h * 0.6f), Vector3.zero, new Vector3(0.18f, 0.8f, 0.1f), "WindowGlow", false).transform;
        slit.rotation = Quaternion.LookRotation(outward);
    }

    private static void BuildGate(float deg, bool north)
    {
        Transform g = Group(north ? "Puerta_Norte" : "Puerta_Sur", Polar(deg, WallRadius));
        g.rotation = Quaternion.LookRotation(Polar(deg, 1f));
        Vector3 left = g.InverseTransformPoint(Polar(deg - GateHalfAngle, WallRadius));
        Vector3 right = g.InverseTransformPoint(Polar(deg + GateHalfAngle, WallRadius));
        float span = Vector3.Distance(left, right);

        Prim(PrimitiveType.Cube, "Arco", g, new Vector3(0f, 5.2f, 0f), Vector3.zero, new Vector3(span, 1.8f, 1.6f), "WallStone", false);
        Prim(PrimitiveType.Cube, "Arco_Nieve", g, new Vector3(0f, 6.17f, 0f), Vector3.zero, new Vector3(span, 0.14f, 1.75f), "Snow", false);
        for (int k = 0; k < 5; k++)
        {
            float x = -span * 0.35f + k * span * 0.7f / 4f;
            Prim(PrimitiveType.Cube, "Almena", g, new Vector3(x, 6.55f, 0f), Vector3.zero, new Vector3(0.8f, 0.75f, 1.5f), "WallStone", false);
        }
        for (int k = 0; k < 9; k++)
        {
            float x = -2.2f + k * 0.55f;
            Prim(PrimitiveType.Cube, "Rastrillo", g, new Vector3(x, 4.1f, 0f), Vector3.zero, new Vector3(0.09f, 0.7f, 0.09f), "Iron", false);
        }
        Prim(PrimitiveType.Cube, "Rastrillo_Barra", g, new Vector3(0f, 4.35f, 0f), Vector3.zero, new Vector3(4.6f, 0.1f, 0.1f), "Iron", false);

        foreach (float side in new[] { -1f, 1f })
        {
            foreach (float face in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Estandarte", g, new Vector3(side * (span * 0.5f - 0.2f), 4.1f, face * 1.75f), Vector3.zero,
                    new Vector3(0.9f, 2.0f, 0.05f), north ? "ClothBlue" : "ClothRed", false);

            Vector3 torch = new Vector3(side * (span * 0.5f - 2.0f), 0f, -1.3f);
            Prim(PrimitiveType.Cylinder, "Antorcha", g, torch + Vector3.up * 0.8f, Vector3.zero, new Vector3(0.1f, 0.8f, 0.1f), "Wood", false);
            Transform flame = Prim(PrimitiveType.Sphere, "Antorcha_Llama", g, torch + Vector3.up * 1.72f, Vector3.zero, new Vector3(0.25f, 0.4f, 0.25f), "Flame", false).transform;
            flame.gameObject.isStatic = false;
            Light l = AddPointLight(g, torch + Vector3.up * 2f, new Color(1f, 0.6f, 0.3f), 1.5f, 6f, false);
            FlickerLight fl = flame.gameObject.AddComponent<FlickerLight>();
            fl.targetLight = l;
            fl.baseIntensity = 1.5f;
            fl.intensityVariation = 0.4f;
            fl.flames = new[] { flame };
        }

        Vector3 sign = new Vector3(3.6f, 0f, 3.5f);
        Prim(PrimitiveType.Cylinder, "Cartel_Poste", g, sign + Vector3.up * 0.9f, Vector3.zero, new Vector3(0.12f, 0.9f, 0.12f), "Wood", true);
        Prim(PrimitiveType.Cube, north ? "Cartel_HaciaFrostspire" : "Cartel_HaciaElBosque", g, sign + Vector3.up * 1.6f,
            new Vector3(0f, 0f, 8f), new Vector3(1.3f, 0.4f, 0.08f), "Wood", false);
    }

    // ---------- Exterior ----------

    private static void BuildOutsidePaths()
    {
        Transform g = Group("Caminos", Vector3.zero);
        for (float z = WallRadius + 2f; z < 60f; z += 1.6f)
        {
            float x = Mathf.Sin(z * 0.15f) * 0.8f;
            Prim(PrimitiveType.Cylinder, "Huella", g, new Vector3(x, 0.01f, z), Vector3.zero,
                new Vector3(2.4f + Rand(-0.3f, 0.3f), 0.01f, 2.0f), "SnowPath", false);
        }
        Vector3 start = Polar(180f, WallRadius + 2f);
        Vector3 end = SteveClearing + new Vector3(3f, 0f, 3f);
        Vector3 ctrl = new Vector3(0f, 0f, (start.z + end.z) * 0.5f - 2f);
        for (float t = 0f; t <= 1f; t += 0.12f)
        {
            Vector3 p = Vector3.Lerp(Vector3.Lerp(start, ctrl, t), Vector3.Lerp(ctrl, end, t), t);
            Prim(PrimitiveType.Cylinder, "Huella", g, p + Vector3.up * 0.01f, Vector3.zero,
                new Vector3(2.0f + Rand(-0.3f, 0.3f), 0.01f, 1.8f), "SnowPath", false);
        }
    }

    private static Transform BuildSteveClearing()
    {
        Transform g = Group("Claro_Steve", SteveClearing);
        Prim(PrimitiveType.Cylinder, "Claro", g, new Vector3(0f, 0.008f, 0f), Vector3.zero, new Vector3(9f, 0.008f, 9f), "SnowPath", false);
        Transform fire = BuildCampfire(SteveClearing, "Fogata_Steve", 0.7f);
        fire.SetParent(g, true);
        Prim(PrimitiveType.Cylinder, "Tronco_Asiento", g, new Vector3(-2.2f, 0.25f, 0.6f), new Vector3(0f, 20f, 90f), new Vector3(0.5f, 1.1f, 0.5f), "Wood", true);
        Transform amp = Group("Amplificador", SteveClearing + new Vector3(2.3f, 0f, -1.4f));
        amp.SetParent(g, true);
        amp.rotation = Quaternion.LookRotation(-new Vector3(2.3f, 0f, -1.4f).normalized);
        Prim(PrimitiveType.Cube, "Caja", amp, new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(0.9f, 0.9f, 0.5f), "Amp", true);
        Prim(PrimitiveType.Cube, "Rejilla", amp, new Vector3(0f, 0.4f, 0.26f), Vector3.zero, new Vector3(0.75f, 0.6f, 0.02f), "StoneDark", false);
        Prim(PrimitiveType.Cube, "Panel", amp, new Vector3(0f, 0.8f, 0.26f), Vector3.zero, new Vector3(0.75f, 0.1f, 0.02f), "ClothYellow", false);
        Prim(PrimitiveType.Cube, "Nieve", amp, new Vector3(0f, 0.92f, 0f), Vector3.zero, new Vector3(0.95f, 0.06f, 0.55f), "Snow", false);

        for (int i = 0; i < 14; i++)
        {
            float deg = i * 360f / 14f + Rand(-8f, 8f);
            if (Mathf.Abs(Mathf.DeltaAngle(deg, 40f)) < 28f) continue;
            BuildPine(g, SteveClearing + Polar(deg, Rand(6.5f, 9f)), Rand(0.9f, 1.4f));
        }
        return fire;
    }

    private static void BuildSnowMounds()
    {
        Transform g = Group("Montones_Nieve", Vector3.zero);
        int placed = 0;
        for (int attempt = 0; attempt < 200 && placed < 30; attempt++)
        {
            float deg = Rand(0f, 360f);
            float r = placed < 8 ? Rand(WallRadius - 4.5f, WallRadius - 2.5f) : Rand(WallRadius + 4f, 65f);
            Vector3 p = Polar(deg, r);
            if (OnRoad(p) || Vector3.Distance(p, SteveClearing) < 7f) continue;
            float s = Rand(0.8f, 2.2f);
            Prim(PrimitiveType.Sphere, "Monton", g, p, Vector3.zero, new Vector3(s * 1.4f, s * 0.6f, s), "Snow", false);
            placed++;
        }
    }

    private static bool OnRoad(Vector3 p)
    {
        if (Mathf.Abs(p.x) < 5f && p.z > 0f) return true;
        if (Mathf.Abs(p.x) < 4f && p.z < 0f && p.z > -WallRadius - 4f) return true;
        Vector3 start = Polar(180f, WallRadius + 2f);
        Vector3 end = SteveClearing + new Vector3(3f, 0f, 3f);
        Vector3 ctrl = new Vector3(0f, 0f, (start.z + end.z) * 0.5f - 2f);
        for (float t = 0f; t <= 1f; t += 0.1f)
        {
            Vector3 q = Vector3.Lerp(Vector3.Lerp(start, ctrl, t), Vector3.Lerp(ctrl, end, t), t);
            if ((q - p).sqrMagnitude < 16f) return true;
        }
        return false;
    }

    private static void BuildForest()
    {
        Transform g = Group("Bosque", Vector3.zero);
        int placed = 0;
        for (int attempt = 0; attempt < 600 && placed < 95; attempt++)
        {
            float deg = Rand(0f, 360f);
            float r = Rand(WallRadius + 4.5f, 85f);
            Vector3 p = Polar(deg, r);
            if (OnRoad(p) || Vector3.Distance(p, SteveClearing) < 10f) continue;
            BuildPine(g, p, Rand(0.8f, 1.5f));
            placed++;
        }
    }

    private static void BuildPine(Transform parent, Vector3 pos, float scale)
    {
        Transform t = Group("Pino", pos);
        t.SetParent(parent, true);
        t.localScale = Vector3.one * scale;
        t.rotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);

        Prim(PrimitiveType.Cylinder, "Tronco", t, new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(0.35f, 0.5f, 0.35f), "WoodDark", true);
        Cone("Copa1", t, new Vector3(0f, 0.7f, 0f), new Vector3(2.6f, 2.0f, 2.6f), "Pine");
        Cone("Copa2", t, new Vector3(0f, 1.7f, 0f), new Vector3(2.0f, 1.7f, 2.0f), "Pine");
        Cone("Copa3", t, new Vector3(0f, 2.6f, 0f), new Vector3(1.3f, 1.5f, 1.3f), "Pine");
        Cone("Nieve", t, new Vector3(0f, 3.35f, 0f), new Vector3(0.66f, 0.76f, 0.66f), "Snow");
    }

    private static void BuildMountain()
    {
        Transform g = Group("The_Frostspire", new Vector3(0f, -1f, 185f));
        Peak(g, Vector3.zero, 150f, 120f);
        Peak(g, new Vector3(-70f, 0f, 20f), 90f, 70f);
        Peak(g, new Vector3(75f, 0f, 15f), 100f, 80f);
    }

    private static void Peak(Transform parent, Vector3 localPos, float diameter, float height)
    {
        const float cap = 0.35f;
        Cone("Pico", parent, localPos, new Vector3(diameter, height, diameter), "Mountain");
        Cone("Pico_Nieve", parent, localPos + Vector3.up * (height * (1f - cap) - 0.5f),
            new Vector3(diameter * cap * 1.02f, height * cap + 0.6f, diameter * cap * 1.02f), "Snow");
    }

    private static void BuildSnowfall()
    {
        GameObject go = new GameObject("Nevada");
        go.transform.SetParent(_root, false);
        go.transform.position = new Vector3(0f, 18f, 0f);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 9f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.maxParticles = 3500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;

        var emission = ps.emission;
        emission.rateOverTime = 300f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(80f, 80f, 1f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.3f;

        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader != null)
        {
            string path = MatFolder + "/Snowflake.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(particleShader);
                AssetDatabase.CreateAsset(m, path);
            }
            Texture2D dot = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (dot != null) m.SetTexture("_BaseMap", dot);
            m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.9f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = m;
        }
    }

    private static void BuildZoneExit()
    {
        GameObject go = new GameObject("Salida_HaciaFoothills");
        go.transform.SetParent(_root, false);
        go.transform.position = new Vector3(0f, 1.5f, 62f);
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(12f, 3f, 2f);
        ZoneExit exit = go.AddComponent<ZoneExit>();
        exit.zoneName = "The Foothills";
    }

    // ---------- Pingüinos ----------

    public class NpcSpec
    {
        public string name, role;
        public bool showRole;
        public VillagerMood mood;
        public Vector3 pos;
        public float scale = 1f, fear;
        public Color plumage;
        public string materialName;
        public List<OutfitItem> outfits = new List<OutfitItem>();
        public Transform focus;
        public Vector3 lookout = Vector3.forward;
        public float wanderRadius = 3f, walkSpeed = 1.3f;
        public List<NPCOption> options = new List<NPCOption> { NPCOption.Hablar };
        public string[] lines;
    }

    private static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>
    {
        { "Herrero", new[] {
            "Mi fragua nunca se apaga. Si el Frost te congela la espada, tráemela.",
            "El hierro de la montaña está cambiando: se vuelve negro al enfriarse.",
            "Reparo lo que traigas, pero no hago milagros." } },
        { "Mercader", new[] {
            "¡Pescado fresco, cristales, cuerdas! Todo lo que un aventurero necesita.",
            "Desde que cerraron el paso norte, los precios... bueno, ya sabes.",
            "Si encuentras algo raro en la montaña, te lo compro." } },
        { "Tabernera", new[] {
            "Chocolate caliente y sopa de pescado. Nada mejor contra el frío.",
            "Los vigías dicen que anoche la montaña brilló otra vez.",
            "Siéntate un rato, héroe. El Frost puede esperar un sorbo." } },
        { "Anciano", new[] {
            "Hace mucho sellamos algo bajo el Frostspire. Temo que el sello se esté rompiendo.",
            "Sube la montaña, joven. Descubre qué está despertando la corrupción.",
            "Las ruinas guardan la historia de quienes lo intentaron antes." } },
        { "Vigía", new[] {
            "Desde aquí se ve la luz azul en la cima. No me gusta nada.",
            "Nadie sale por la puerta norte sin avisarme.",
            "Algunos cazadores volvieron... pero ya no eran ellos." } },
        { "Aldeano", new[] {
            "¿Oíste eso? Algo cruje en el bosque por las noches.",
            "Mi hermano subió a la montaña hace tres días.",
            "Menos mal que la hoguera sigue encendida." } },
        { "Pingüinito", new[] {
            "¡Te reto a una carrera hasta la fuente!",
            "Dicen que la estatua de hielo se mueve cuando nadie mira.",
            "¿Eres un héroe de verdad?" } },
    };

    private static void BuildVillagers(Spots spots)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SetupPenguinWardrobe.PrefabPath) == null)
        {
            Debug.LogWarning("[SetupVillage] Falta " + SetupPenguinWardrobe.PrefabPath + "; ejecuta Configurar Pingüino Vestible.");
            return;
        }
        Transform g = Group("Pinguinos", Vector3.zero);
        Vector3 gateIn = Polar(0f, WallRadius - 3f);

        var specs = new List<NpcSpec>
        {
            Spec("Anciano Pingo", "Anciano", true, VillagerMood.Work, spots.elderDoor, 1.05f, 0.2f, 7, spots.plazaCenter, "Sombrero_Mago"),
            Spec("Herrera Brunna", "Herrero", true, VillagerMood.Work, spots.smith, 1.1f, 0.1f, 0, spots.forge, "Casco_Cuernos", "Armadura"),
            Spec("Mercader Tico", "Mercader", true, VillagerMood.Work, spots.merchant, 1f, 0.3f, 3, spots.plazaCenter),
            Spec("Tabernera Mora", "Tabernera", true, VillagerMood.Work, spots.tavernKeeper, 1f, 0.2f, 10, spots.plazaCenter),
            Spec("Vigía Kora", "Vigía", true, VillagerMood.Lookout, gateIn + new Vector3(-3.2f, 0f, 0f), 1f, 0.5f, 11, null, "Traje_Capucha"),
            Spec("Vigía Brisk", "Vigía", true, VillagerMood.Lookout, gateIn + new Vector3(3.2f, 0f, 0f), 1f, 0.55f, 8, null, "Traje_Capucha"),
        };
        specs[1].options = new List<NPCOption> { NPCOption.Vender, NPCOption.Reparar, NPCOption.Hablar };
        specs[2].options = new List<NPCOption> { NPCOption.Comprar, NPCOption.Vender, NPCOption.Hablar };
        specs[3].options = new List<NPCOption> { NPCOption.Comprar, NPCOption.Hablar };

        string[] huddle = { "Lumi", "Nilo", "Tundra", "Copo", "Escarcha" };
        for (int i = 0; i < huddle.Length; i++)
        {
            float deg = 20f + i * 72f + Rand(-10f, 10f);
            specs.Add(Spec(huddle[i], "Aldeano", false, VillagerMood.Huddle, FirePos + Polar(deg, 2.3f), Rand(0.9f, 1.05f), Rand(0.7f, 1f), 1 + i * 2, spots.fire));
        }

        string[] kids = { "Pip", "Tuki", "Bru", "Nieves" };
        Vector3[] kidSpots = { new Vector3(6f, 0f, -6f), new Vector3(7f, 0f, 5f), new Vector3(-6.5f, 0f, 6f), new Vector3(3f, 0f, -9f) };
        for (int i = 0; i < kids.Length; i++)
        {
            NpcSpec s = Spec(kids[i], "Pingüinito", false, VillagerMood.Pace, kidSpots[i], Rand(0.6f, 0.72f), Rand(0.5f, 0.9f), 2 + i * 3, null);
            s.wanderRadius = 3f;
            s.walkSpeed = 1.6f;
            specs.Add(s);
        }

        foreach (NpcSpec s in specs) CreateNPC(g, s);
    }

    private static NpcSpec Spec(string name, string role, bool showRole, VillagerMood mood, Vector3 pos, float scale, float fear,
        int color, Transform focus, params string[] outfits)
    {
        var s = new NpcSpec
        {
            name = name, role = role, showRole = showRole, mood = mood, pos = pos, scale = scale, fear = fear,
            plumage = PenguinColors[color % PenguinColors.Length], materialName = "Toon_Penguin_" + color, focus = focus
        };
        foreach (string o in outfits)
        {
            OutfitItem item = SetupPenguinWardrobe.LoadItem(o);
            if (item != null) s.outfits.Add(item);
        }
        return s;
    }

    public static GameObject CreateNPC(Transform parent, NpcSpec s)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SetupPenguinWardrobe.PrefabPath);
        if (prefab == null) return null;

        GameObject root = new GameObject("NPC_" + s.name.Replace(" ", "_"));
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(s.pos.x, 0f, s.pos.z);
        root.transform.localScale = Vector3.one * s.scale;

        GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
        body.name = "Penguin";
        Material mat = SetupPenguinWardrobe.ToonPenguinVariant(s.materialName, s.plumage);
        foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }

        // La ropa se pone ya en el editor para que se vea en la escena (y no depende de Start).
        PenguinOutfit outfit = body.GetComponent<PenguinOutfit>();
        if (outfit != null)
        {
            outfit.useClassOutfit = false;
            outfit.startingItems = new List<OutfitItem>();
            outfit.CacheBones();
            foreach (OutfitItem item in s.outfits) outfit.Equip(item);
            PrefabUtility.RecordPrefabInstancePropertyModifications(outfit);
        }

        Bounds? b = GetBounds(body, "Penguin_");
        float height = 1f;
        if (b.HasValue)
        {
            body.transform.localPosition = new Vector3(0f, -(b.Value.min.y - root.transform.position.y) / s.scale, 0f);
            height = b.Value.size.y / s.scale;
        }

        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.height = height;
        col.radius = height * 0.3f;
        col.center = new Vector3(0f, height * 0.5f, 0f);
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        VillagerNPC npc = root.AddComponent<VillagerNPC>();
        npc.villagerName = s.name;
        npc.role = s.role;
        npc.mood = s.mood;
        npc.fear = s.fear;
        npc.model = body.transform;
        npc.focusPoint = s.focus;
        npc.lookoutDirection = s.lookout;
        npc.wanderRadius = s.wanderRadius;
        npc.walkSpeed = s.walkSpeed;
        string[] lines = s.lines ?? (Lines.TryGetValue(s.role, out string[] l) ? l : new[] { "..." });
        npc.dialogue = (string[])lines.Clone();

        var tag = root.AddComponent<NameTag>();
        tag.displayName = s.name;
        tag.subtitle = s.showRole ? s.role : "";

        var interact = root.AddComponent<NPCInteractable>();
        interact.displayName = s.name;
        interact.role = s.role;
        interact.options = new List<NPCOption>(s.options);
        interact.lines = (string[])lines.Clone();
        interact.headHeight = height * 1.05f;

        PenguinRigAnimator rigAnim = body.GetComponent<PenguinRigAnimator>();
        if (rigAnim != null) rigAnim.referenceSpeed = npc.walkSpeed;

        Vector3 look = (s.focus != null ? s.focus.position : root.transform.position + Vector3.forward) - root.transform.position;
        if (s.mood == VillagerMood.Lookout) look = s.lookout;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) root.transform.rotation = Quaternion.LookRotation(look.normalized);
        return root;
    }

    private static void PlacePlayer()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) return;
        player.transform.position = PlayerSpawn;
        player.transform.rotation = Quaternion.identity;

        if (player.GetComponent<NameTag>() == null) player.AddComponent<NameTag>().isPlayer = true;
        PlayerLoadout loadout = player.GetComponent<PlayerLoadout>();
        if (loadout == null) loadout = player.AddComponent<PlayerLoadout>();
        if (loadout.palette == null) loadout.palette = AssetDatabase.LoadAssetAtPath<PlumagePalette>("Assets/Data/PlumagePalette.asset");

        GameObject cam = GameObject.Find("Main Camera");
        if (cam == null) return;
        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow == null) return;
        cam.transform.position = player.transform.position + follow.offset;
        cam.transform.LookAt(player.transform.position + Vector3.up * follow.lookHeight);
    }

    // HUD de nombres, globos y menú de NPC; y un EventSystem para poder hacer clic en él.
    public static void EnsureHUD()
    {
        GameObject hudGo = GameObject.Find("HUD_Mundo");
        if (hudGo == null) hudGo = new GameObject("HUD_Mundo", typeof(RectTransform));
        WorldHUD hud = hudGo.GetComponent<WorldHUD>();
        if (hud == null) hud = hudGo.AddComponent<WorldHUD>();
        hud.skin = AssetDatabase.LoadAssetAtPath<UISkin>("Assets/Data/UI/UISkin.asset");
        hud.roleColor = new Color(0.47f, 0.29f, 0.02f);
        if (hud.skin == null || hud.skin.nunito800 == null) Debug.LogWarning("[SetupVillage] Falta el UISkin con las fuentes TMP: ejecuta primero Tools > Frostbound > UI > Construir menú.");
        EditorUtility.SetDirty(hud);

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    // ---------- Utilidades ----------

    private static Transform Group(string name, Vector3 pos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(_root, false);
        go.transform.position = pos;
        go.isStatic = true;
        return go.transform;
    }

    private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localEuler,
        Vector3 scale, string matName, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = scale;
        go.isStatic = true;
        if (Mats.TryGetValue(matName, out Material m)) go.GetComponent<Renderer>().sharedMaterial = m;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static void Cone(string name, Transform parent, Vector3 localPos, Vector3 scale, string matName)
    {
        MeshObject(name, parent, localPos, scale, _cone, matName, false);
    }

    private static void Roof(Transform parent, string name, Vector3 localPos, Vector3 scale, string matName)
    {
        MeshObject(name, parent, localPos, scale, _prism, matName, false);
    }

    private static GameObject MeshObject(string name, Transform parent, Vector3 localPos, Vector3 scale, Mesh mesh, string matName, bool collider)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.isStatic = true;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        if (Mats.TryGetValue(matName, out Material m)) mr.sharedMaterial = m;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    private static Light AddPointLight(Transform parent, Vector3 localPos, Color color, float intensity, float range, bool shadows)
    {
        GameObject go = new GameObject("Luz");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        return l;
    }

    private static void BuildIgloo(Transform parent, string name, Vector3 pos, float radius)
    {
        Vector3 toCenter = -pos;
        toCenter.y = 0f;
        Transform igloo = Group(name, pos);
        igloo.rotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
        igloo.SetParent(parent, true);

        GameObject dome = Prim(PrimitiveType.Sphere, "Cupula", igloo, Vector3.zero, Vector3.zero,
            new Vector3(radius * 2f, radius * 1.8f, radius * 2f), "Igloo", false);
        MeshCollider mc = dome.AddComponent<MeshCollider>();
        mc.sharedMesh = dome.GetComponent<MeshFilter>().sharedMesh;

        float tunnelDia = Mathf.Clamp(radius * 0.75f, 1.5f, 2.3f);
        float tunnelLen = 1.6f;
        float tunnelZ = radius * 0.8f + tunnelLen * 0.5f;
        Prim(PrimitiveType.Cylinder, "Tunel", igloo, new Vector3(0f, 0f, tunnelZ), new Vector3(90f, 0f, 0f),
            new Vector3(tunnelDia, tunnelLen * 0.5f, tunnelDia), "Igloo", true);
        Prim(PrimitiveType.Cylinder, "Puerta", igloo, new Vector3(0f, 0f, tunnelZ + tunnelLen * 0.5f + 0.01f), new Vector3(90f, 0f, 0f),
            new Vector3(tunnelDia * 0.72f, 0.02f, tunnelDia * 0.72f), "IglooDoor", false);
    }

    private static Vector3 Polar(float degFromNorth, float radius)
    {
        float a = degFromNorth * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
    }

    private static float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

    private static Bounds? GetBounds(GameObject root, string namePrefix)
    {
        Bounds? combined = null;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.name.StartsWith(namePrefix)) continue;
            if (combined.HasValue)
            {
                Bounds c = combined.Value;
                c.Encapsulate(r.bounds);
                combined = c;
            }
            else combined = r.bounds;
        }
        return combined;
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
    }

    // Cono low-poly con caras planas (estilo Club Penguin). Base en y=0, punta en y=1, radio 0.5.
    private static Mesh GetConeMesh()
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(ConeMeshPath);
        if (existing != null) return existing;

        const int segments = 10;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        Vector3 apex = new Vector3(0f, 1f, 0f);

        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments;
            float a1 = (i + 1) * Mathf.PI * 2f / segments;
            Vector3 v0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
            Vector3 v1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
            Vector3 outward = (v0 + v1).normalized;
            AddTri(verts, tris, v0, apex, v1, outward);
            AddTri(verts, tris, Vector3.zero, v0, v1, Vector3.down);
        }

        Mesh mesh = new Mesh { name = "Cone_LowPoly" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, ConeMeshPath);
        return mesh;
    }

    // Prisma triangular para tejados a dos aguas: base 1×1 en y=0, cumbrera a lo largo de X en y=1.
    private static Mesh GetPrismMesh()
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(PrismMeshPath);
        if (existing != null) return existing;

        var verts = new List<Vector3>();
        var tris = new List<int>();
        Vector3 fl = new Vector3(-0.5f, 0f, 0.5f), fr = new Vector3(0.5f, 0f, 0.5f);
        Vector3 bl = new Vector3(-0.5f, 0f, -0.5f), br = new Vector3(0.5f, 0f, -0.5f);
        Vector3 tl = new Vector3(-0.5f, 1f, 0f), tr = new Vector3(0.5f, 1f, 0f);
        Vector3 front = new Vector3(0f, 0.5f, 1f).normalized, back = new Vector3(0f, 0.5f, -1f).normalized;

        AddTri(verts, tris, fl, fr, tr, front);
        AddTri(verts, tris, fl, tr, tl, front);
        AddTri(verts, tris, bl, tl, tr, back);
        AddTri(verts, tris, bl, tr, br, back);
        AddTri(verts, tris, bl, fl, tl, Vector3.left);
        AddTri(verts, tris, br, tr, fr, Vector3.right);
        AddTri(verts, tris, bl, br, fr, Vector3.down);
        AddTri(verts, tris, bl, fr, fl, Vector3.down);

        Mesh mesh = new Mesh { name = "Prism_Roof" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, PrismMeshPath);
        return mesh;
    }

    private static void AddTri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 expectedNormal)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(n, expectedNormal) < 0f)
        {
            Vector3 tmp = b;
            b = c;
            c = tmp;
        }
        int start = verts.Count;
        verts.Add(a);
        verts.Add(b);
        verts.Add(c);
        tris.Add(start);
        tris.Add(start + 1);
        tris.Add(start + 2);
    }

    // Textura de bloques de hielo para las cúpulas (mapeo UV de la esfera de Unity).
    private static Texture2D GetIglooTexture()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(IglooTexPath);
        if (existing != null) return existing;

        const int size = 512;
        const int rows = 16;
        const int cols = 18;
        const int line = 3;
        Color block = new Color(0.97f, 0.99f, 1f);
        Color blockShade = new Color(0.86f, 0.93f, 0.99f);
        Color mortar = new Color(0.66f, 0.80f, 0.92f);

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] px = new Color[size * size];
        float rowH = (float)size / rows;
        float colW = (float)size / cols;

        for (int y = 0; y < size; y++)
        {
            int row = Mathf.FloorToInt(y / rowH);
            float yIn = y - row * rowH;
            float offset = (row % 2) * colW * 0.5f;
            for (int x = 0; x < size; x++)
            {
                float xs = (x + offset) % size;
                int col = Mathf.FloorToInt(xs / colW);
                float xIn = xs - col * colW;
                bool isMortar = yIn < line || xIn < line;
                float shade = Mathf.PerlinNoise(col * 1.7f + row * 3.1f, row * 0.9f);
                float grad = yIn / rowH;
                Color c = Color.Lerp(block, blockShade, shade * 0.6f + grad * 0.3f);
                px[y * size + x] = isMortar ? mortar : c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(IglooTexPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(IglooTexPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(IglooTexPath);
    }

    // Empedrado: filas de piedras de ancho variable, juntas oscuras y un poco de variación por piedra.
    private static Texture2D GetCobbleTexture()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(CobbleTexPath);
        if (existing != null) return existing;

        const int size = 512;
        const int rows = 8;
        const int gap = 4;
        var rng = new System.Random(77);
        Color stoneA = new Color(0.93f, 0.95f, 0.98f);
        Color stoneB = new Color(0.78f, 0.83f, 0.90f);
        Color joint = new Color(0.55f, 0.61f, 0.70f);

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] px = new Color[size * size];
        int rowH = size / rows;
        for (int row = 0; row < rows; row++)
        {
            var cuts = new List<int> { 0 };
            int x = rng.Next(0, 40);
            cuts[0] = x - size;
            while (x < size)
            {
                cuts.Add(x);
                x += rng.Next(48, 88);
            }
            cuts.Add(x);
            for (int y = row * rowH; y < (row + 1) * rowH; y++)
            {
                int yIn = y - row * rowH;
                for (int px0 = 0; px0 < size; px0++)
                {
                    int k = 0;
                    while (k < cuts.Count - 1 && cuts[k + 1] <= px0) k++;
                    int xIn = px0 - cuts[k];
                    int w = cuts[k + 1] - cuts[k];
                    bool isJoint = yIn < gap || xIn < gap;
                    float edge = Mathf.Min(Mathf.Min(yIn, rowH - yIn), Mathf.Min(xIn, w - xIn)) / 10f;
                    float shade = (float)((row * 31 + k * 17) % 11) / 10f;
                    Color c = Color.Lerp(stoneB, stoneA, Mathf.Clamp01(0.35f + shade * 0.5f + Mathf.Clamp01(edge) * 0.15f));
                    px[y * size + px0] = isJoint ? joint : c;
                }
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(CobbleTexPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(CobbleTexPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(CobbleTexPath);
    }
}
