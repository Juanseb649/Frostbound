using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupVillage
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MatFolder = "Assets/Materials/Village";
    private const string MeshFolder = "Assets/Meshes";
    private const string ConeMeshPath = MeshFolder + "/Cone_LowPoly.asset";
    private const string IglooTexPath = "Assets/Textures/igloo_blocks.png";
    private const string RootName = "Campamento";

    private const float FenceRadius = 20f;
    private const float GateHalfAngle = 12f;
    private const int LayoutSeed = 2026;

    private static Transform _root;
    private static System.Random _rng;
    private static Mesh _cone;
    private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

    private static readonly Color[] PenguinColors =
    {
        new Color(0.85f, 0.15f, 0.15f), new Color(0.15f, 0.60f, 0.22f), new Color(1.00f, 0.55f, 0.75f),
        new Color(1.00f, 0.85f, 0.20f), new Color(1.00f, 0.55f, 0.10f), new Color(0.40f, 0.75f, 1.00f),
        new Color(0.55f, 0.30f, 0.80f), new Color(0.50f, 0.30f, 0.15f), new Color(0.14f, 0.14f, 0.16f),
        new Color(0.60f, 0.90f, 0.20f), new Color(0.20f, 0.85f, 0.80f), new Color(0.10f, 0.35f, 0.22f),
        new Color(1.00f, 0.72f, 0.52f)
    };

    [MenuItem("Tools/Frostbound/Construir Campamento")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
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
        _cone = GetConeMesh();
        Texture2D iglooTex = GetIglooTexture();

        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        _root = new GameObject(RootName).transform;

        CreateMaterials(iglooTex);

        Transform fire = BuildCampfire(Vector3.zero);
        BuildBenches();
        BuildPaths();
        BuildIgloos(out Vector3 elderDoor, out Vector3 smithSpot, out Vector3 merchantSpot);
        BuildForge(smithSpot);
        BuildMerchantStall(merchantSpot);
        BuildFence();
        BuildGate();
        BuildSnowMounds();
        BuildForest();
        BuildMountain();
        BuildSnowfall();
        BuildZoneExit();
        BuildVillagers(fire, elderDoor, smithSpot, merchantSpot);
        PlacePlayer();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        if (!showDialog) return;
        EditorUtility.DisplayDialog("Frostbound",
            "Campamento construido en " + ScenePath + ".\n\n" +
            "Iglús, hoguera, herrería, puesto del mercader, empalizada con salida al norte (hacia The Frostspire) " +
            "y 14 pingüinos asustados.\n\nPulsa Play para recorrerlo. Puedes volver a ejecutar esta herramienta: reemplaza el campamento.",
            "OK");
    }

    // ---------- Materiales ----------

    private static void CreateMaterials(Texture2D iglooTex)
    {
        Mat("Igloo", Color.white, 0.35f, null, iglooTex);
        Mat("IglooDoor", new Color(0.05f, 0.08f, 0.15f), 0f);
        Mat("Wood", new Color(0.45f, 0.28f, 0.15f), 0.1f);
        Mat("WoodDark", new Color(0.30f, 0.18f, 0.10f), 0.1f);
        Mat("Stone", new Color(0.52f, 0.56f, 0.63f), 0.15f);
        Mat("Snow", new Color(0.93f, 0.96f, 1.00f), 0.4f);
        Mat("SnowPath", new Color(0.78f, 0.86f, 0.93f), 0.2f);
        Mat("Pine", new Color(0.10f, 0.38f, 0.35f), 0.1f);
        Mat("Mountain", new Color(0.36f, 0.42f, 0.55f), 0.1f);
        Mat("Flame", new Color(1f, 0.55f, 0.1f), 0f, new Color(4f, 1.6f, 0.2f));
        Mat("FlameCore", new Color(1f, 0.9f, 0.4f), 0f, new Color(5f, 3.5f, 1f));
        Mat("Ember", new Color(0.9f, 0.3f, 0.05f), 0f, new Color(3f, 0.6f, 0.05f));
        Mat("Iron", new Color(0.22f, 0.24f, 0.28f), 0.6f);
        Mat("ClothRed", new Color(0.80f, 0.18f, 0.20f), 0.1f);
        Mat("ClothBlue", new Color(0.20f, 0.45f, 0.85f), 0.1f);
        Mat("Ice", new Color(0.62f, 0.85f, 0.98f), 0.85f);
    }

    private static Material Mat(string name, Color color, float smoothness, Color? emission = null, Texture2D tex = null)
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

    private static Material PenguinMat(int index)
    {
        string name = "Penguin_" + index;
        if (Mats.TryGetValue(name, out Material m)) return m;
        return Mat(name, PenguinColors[index % PenguinColors.Length], 0.3f);
    }

    private static Shader LitShader()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        return s;
    }

    // ---------- Hoguera ----------

    private static Transform BuildCampfire(Vector3 pos)
    {
        Transform fire = Group("Hoguera", pos);

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
        Light light = AddPointLight(fire, new Vector3(0f, 1.4f, 0f), new Color(1f, 0.6f, 0.3f), 3f, 12f, true);
        FlickerLight flicker = fire.gameObject.AddComponent<FlickerLight>();
        flicker.targetLight = light;
        flicker.flames = new[] { f1, f2 };
        flicker.baseIntensity = 3f;

        CapsuleCollider block = fire.gameObject.AddComponent<CapsuleCollider>();
        block.center = new Vector3(0f, 0.5f, 0f);
        block.radius = 1.1f;
        block.height = 1f;
        return fire;
    }

    private static void BuildBenches()
    {
        Transform g = Group("Bancos", Vector3.zero);
        for (int i = 0; i < 4; i++)
        {
            float deg = 45f + i * 90f;
            Vector3 p = Polar(deg, 3.4f);
            Prim(PrimitiveType.Cylinder, "Banco", g, p + Vector3.up * 0.22f, new Vector3(0f, deg, 90f),
                new Vector3(0.45f, 0.9f, 0.45f), "Wood", true);
        }
    }

    private static void BuildPaths()
    {
        Transform g = Group("Caminos", Vector3.zero);
        for (float z = 2.5f; z < 60f; z += 1.6f)
        {
            float x = Mathf.Sin(z * 0.15f) * 0.8f;
            Prim(PrimitiveType.Cylinder, "Huella", g, new Vector3(x, 0.01f, z), Vector3.zero,
                new Vector3(2.4f + Rand(-0.3f, 0.3f), 0.01f, 2.0f), "SnowPath", false);
        }
        Prim(PrimitiveType.Cylinder, "Plaza", g, new Vector3(0f, 0.005f, 0f), Vector3.zero, new Vector3(9f, 0.01f, 9f), "SnowPath", false);
    }

    // ---------- Iglús ----------

    private static void BuildIgloos(out Vector3 elderDoor, out Vector3 smithSpot, out Vector3 merchantSpot)
    {
        Transform g = Group("Iglus", Vector3.zero);

        float[] angles = { 50f, 95f, 140f, 220f, 265f, 310f };
        string[] names = { "Iglu_Familia_A", "Iglu_Herreria", "Iglu_Familia_B", "Iglu_Familia_C", "Iglu_Mercader", "Iglu_Familia_D" };
        smithSpot = Vector3.zero;
        merchantSpot = Vector3.zero;

        for (int i = 0; i < angles.Length; i++)
        {
            float radius = 12.5f + Rand(-0.8f, 0.8f);
            float size = 2.2f + Rand(-0.25f, 0.35f);
            Vector3 pos = Polar(angles[i], radius);
            BuildIgloo(g, names[i], pos, size);

            Vector3 side = Quaternion.Euler(0f, 90f, 0f) * (-pos.normalized);
            if (names[i] == "Iglu_Herreria") smithSpot = pos - pos.normalized * (size + 2.2f) + side * 2.4f;
            if (names[i] == "Iglu_Mercader") merchantSpot = pos - pos.normalized * (size + 2.2f) - side * 2.4f;
        }

        Vector3 elderPos = Polar(180f, 14.5f);
        BuildIgloo(g, "Iglu_Anciano", elderPos, 3.2f);
        Prim(PrimitiveType.Cylinder, "Estandarte_Poste", g, elderPos + new Vector3(2.6f, 1.8f, 1.5f), Vector3.zero,
            new Vector3(0.15f, 1.8f, 0.15f), "WoodDark", true);
        Prim(PrimitiveType.Cube, "Estandarte_Tela", g, elderPos + new Vector3(2.6f, 3.0f, 1.9f), Vector3.zero,
            new Vector3(0.05f, 1.0f, 0.7f), "ClothBlue", false);
        elderDoor = elderPos - elderPos.normalized * (3.2f + 2.2f);
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

    // ---------- Herrería y mercader ----------

    private static void BuildForge(Vector3 pos)
    {
        Transform g = Group("Herreria", pos);
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

        for (int i = 0; i < 3; i++)
        {
            Prim(PrimitiveType.Cube, "Arma_Apoyada", g, new Vector3(-0.3f + i * 0.35f, 0.7f, -1.1f), new Vector3(-15f, 0f, 0f),
                new Vector3(0.08f, 1.4f, 0.12f), "Iron", false);
        }
    }

    private static void BuildMerchantStall(Vector3 pos)
    {
        Transform g = Group("Puesto_Mercader", pos);
        g.rotation = Quaternion.LookRotation(-new Vector3(pos.x, 0f, pos.z).normalized);

        float w = 1.4f, d = 0.9f;
        foreach (Vector2 c in new[] { new Vector2(-w, -d), new Vector2(w, -d), new Vector2(-w, d), new Vector2(w, d) })
        {
            Prim(PrimitiveType.Cylinder, "Poste", g, new Vector3(c.x, 1.1f, c.y), Vector3.zero, new Vector3(0.12f, 1.1f, 0.12f), "Wood", true);
        }
        Prim(PrimitiveType.Cube, "Techo", g, new Vector3(0f, 2.25f, 0f), new Vector3(-10f, 0f, 0f), new Vector3(w * 2f + 0.5f, 0.08f, d * 2f + 0.6f), "ClothRed", false);
        Prim(PrimitiveType.Cube, "Mostrador", g, new Vector3(0f, 0.5f, d * 0.8f), Vector3.zero, new Vector3(w * 2f, 1f, 0.5f), "Wood", true);
        Prim(PrimitiveType.Sphere, "Mercancia_Hielo", g, new Vector3(-0.6f, 1.12f, d * 0.8f), Vector3.zero, new Vector3(0.25f, 0.25f, 0.25f), "Ice", false);
        Prim(PrimitiveType.Cube, "Mercancia_Caja", g, new Vector3(0.5f, 1.1f, d * 0.8f), new Vector3(0f, 20f, 0f), new Vector3(0.3f, 0.2f, 0.3f), "ClothBlue", false);

        for (int i = 0; i < 5; i++)
        {
            Vector3 p = new Vector3(Rand(-2.6f, -1.8f) + (i % 2) * 4.4f, 0f, Rand(-1.5f, 0.5f));
            float s = Rand(0.5f, 0.8f);
            if (i % 3 == 0)
                Prim(PrimitiveType.Cylinder, "Barril", g, p + Vector3.up * s * 0.6f, Vector3.zero, new Vector3(s, s * 0.6f, s), "Wood", true);
            else
                Prim(PrimitiveType.Cube, "Caja", g, p + Vector3.up * s * 0.5f, new Vector3(0f, Rand(0f, 45f), 0f), Vector3.one * s, "WoodDark", true);
        }
    }

    // ---------- Empalizada y puerta ----------

    private static void BuildFence()
    {
        Transform g = Group("Empalizada", Vector3.zero);
        float circumference = 2f * Mathf.PI * FenceRadius;
        int count = Mathf.RoundToInt(circumference / 1.0f);
        for (int i = 0; i < count; i++)
        {
            float deg = i * 360f / count;
            if (Mathf.Abs(Mathf.DeltaAngle(deg, 0f)) < GateHalfAngle) continue;
            float h = Rand(0.75f, 1.0f);
            Vector3 p = Polar(deg, FenceRadius + Rand(-0.1f, 0.1f));
            Prim(PrimitiveType.Cylinder, "Estaca", g, p + Vector3.up * h, new Vector3(Rand(-4f, 4f), 0f, Rand(-4f, 4f)),
                new Vector3(0.28f, h, 0.28f), "Wood", true);
            Prim(PrimitiveType.Sphere, "Nieve_Estaca", g, p + Vector3.up * (h * 2f), Vector3.zero, new Vector3(0.32f, 0.14f, 0.32f), "Snow", false);
        }
    }

    private static void BuildGate()
    {
        Transform g = Group("Puerta_Norte", Vector3.zero);
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 p = Polar(side * GateHalfAngle, FenceRadius);
            Prim(PrimitiveType.Cylinder, "Poste_Puerta", g, p + Vector3.up * 1.6f, Vector3.zero, new Vector3(0.45f, 1.6f, 0.45f), "WoodDark", true);

            Vector3 torch = p + new Vector3(side * -0.5f, 0f, -0.4f);
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

        Vector3 left = Polar(-GateHalfAngle, FenceRadius);
        Vector3 right = Polar(GateHalfAngle, FenceRadius);
        Vector3 mid = (left + right) * 0.5f;
        float span = Vector3.Distance(left, right) + 0.6f;
        Prim(PrimitiveType.Cube, "Dintel", g, mid + Vector3.up * 3.1f, Vector3.zero, new Vector3(span, 0.35f, 0.4f), "WoodDark", false);
        Prim(PrimitiveType.Cube, "Dintel_Nieve", g, mid + Vector3.up * 3.33f, Vector3.zero, new Vector3(span + 0.1f, 0.12f, 0.5f), "Snow", false);

        Vector3 sign = mid + new Vector3(3.2f, 0f, 2.5f);
        Prim(PrimitiveType.Cylinder, "Cartel_Poste", g, sign + Vector3.up * 0.9f, Vector3.zero, new Vector3(0.12f, 0.9f, 0.12f), "Wood", true);
        Prim(PrimitiveType.Cube, "Cartel_HaciaFrostspire", g, sign + Vector3.up * 1.6f, new Vector3(0f, 0f, 8f), new Vector3(1.3f, 0.4f, 0.08f), "Wood", false);
    }

    // ---------- Entorno ----------

    private static void BuildSnowMounds()
    {
        Transform g = Group("Montones_Nieve", Vector3.zero);
        for (int i = 0; i < 26; i++)
        {
            float deg = Rand(0f, 360f);
            float r = i < 10 ? Rand(6f, 18f) : Rand(22f, 60f);
            Vector3 p = Polar(deg, r);
            if (Mathf.Abs(p.x) < 3.5f && p.z > 0f) continue;
            float s = Rand(0.8f, 2.4f);
            Prim(PrimitiveType.Sphere, "Monton", g, p, Vector3.zero, new Vector3(s * 1.4f, s * 0.6f, s), "Snow", false);
        }
    }

    private static void BuildForest()
    {
        Transform g = Group("Bosque", Vector3.zero);
        int placed = 0;
        for (int attempt = 0; attempt < 400 && placed < 70; attempt++)
        {
            float deg = Rand(0f, 360f);
            float r = Rand(FenceRadius + 3f, 75f);
            Vector3 p = Polar(deg, r);
            if (Mathf.Abs(p.x - Mathf.Sin(p.z * 0.15f) * 0.8f) < 5f && p.z > 0f) continue;
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
        Transform g = Group("The_Frostspire", new Vector3(0f, -1f, 175f));
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
        main.maxParticles = 3000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;

        var emission = ps.emission;
        emission.rateOverTime = 260f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(70f, 70f, 1f);

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

    private struct VillagerSpec
    {
        public string name, role;
        public VillagerMood mood;
        public Vector3 pos;
        public float scale, fear;
        public int color;
        public string[] outfits;
    }

    private static void BuildVillagers(Transform fire, Vector3 elderDoor, Vector3 smithSpot, Vector3 merchantSpot)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Penguin.obj");
        if (model == null)
        {
            Debug.LogWarning("[SetupVillage] No se encontró Assets/Penguin.obj; no se crearon pingüinos.");
            return;
        }

        Transform g = Group("Pinguinos", Vector3.zero);
        Vector3 gate = Polar(0f, FenceRadius - 2f);
        Vector3 smithFront = smithSpot - smithSpot.normalized * 1.6f;
        Vector3 merchantFront = merchantSpot - merchantSpot.normalized * 1.8f;

        var specs = new List<VillagerSpec>
        {
            new VillagerSpec { name = "Anciano Pingo", role = "Anciano", mood = VillagerMood.Work, pos = elderDoor, scale = 1.05f, fear = 0.2f, color = 7, outfits = new[] { "Sombrero_Mago" } },
            new VillagerSpec { name = "Herrera Brunna", role = "Herrero", mood = VillagerMood.Work, pos = smithFront, scale = 1.1f, fear = 0.1f, color = 0, outfits = new[] { "Casco_Cuernos", "Armadura" } },
            new VillagerSpec { name = "Mercader Tico", role = "Mercader", mood = VillagerMood.Work, pos = merchantFront, scale = 1f, fear = 0.3f, color = 3 },
            new VillagerSpec { name = "Vigía Kora", role = "Vigía", mood = VillagerMood.Lookout, pos = gate + new Vector3(-1.8f, 0f, 0f), scale = 1f, fear = 0.5f, color = 11, outfits = new[] { "Traje_Capucha" } },
            new VillagerSpec { name = "Vigía Brisk", role = "Vigía", mood = VillagerMood.Lookout, pos = gate + new Vector3(1.8f, 0f, 0f), scale = 1f, fear = 0.55f, color = 8, outfits = new[] { "Traje_Capucha" } },
        };

        string[] huddle = { "Lumi", "Nilo", "Tundra", "Copo", "Escarcha" };
        for (int i = 0; i < huddle.Length; i++)
        {
            float deg = 20f + i * 72f + Rand(-10f, 10f);
            specs.Add(new VillagerSpec { name = huddle[i], role = "Aldeano", mood = VillagerMood.Huddle, pos = Polar(deg, 2.3f), scale = Rand(0.9f, 1.05f), fear = Rand(0.7f, 1f), color = 1 + i * 2 });
        }

        string[] kids = { "Pip", "Tuki", "Bru", "Nieves" };
        for (int i = 0; i < kids.Length; i++)
        {
            float deg = i < 2 ? 100f + i * 40f : 220f + (i - 2) * 40f;
            specs.Add(new VillagerSpec { name = kids[i], role = "Pingüinito", mood = VillagerMood.Pace, pos = Polar(deg, 7.5f), scale = Rand(0.6f, 0.72f), fear = Rand(0.5f, 0.9f), color = 2 + i * 3 });
        }

        foreach (VillagerSpec s in specs)
        {
            CreateVillager(g, model, s, fire, smithSpot, merchantSpot, elderDoor);
        }
    }

    private static void CreateVillager(Transform parent, GameObject model, VillagerSpec s, Transform fire,
        Vector3 smithSpot, Vector3 merchantSpot, Vector3 elderDoor)
    {
        GameObject root = new GameObject("NPC_" + s.name.Replace(" ", "_"));
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(s.pos.x, 0f, s.pos.z);
        root.transform.localScale = Vector3.one * s.scale;

        GameObject rigged = AssetDatabase.LoadAssetAtPath<GameObject>(SetupPenguinWardrobe.PrefabPath);
        GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(rigged != null ? rigged : model, root.transform);
        body.name = "Penguin";
        Material mat = rigged != null
            ? SetupPenguinWardrobe.ToonPenguinVariant("Toon_Penguin_" + s.color, PenguinColors[s.color % PenguinColors.Length])
            : PenguinMat(s.color);
        foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }

        PenguinOutfit outfit = body.GetComponent<PenguinOutfit>();
        if (outfit != null)
        {
            outfit.useClassOutfit = false;
            outfit.startingItems = new List<OutfitItem>();
            if (s.outfits != null)
                foreach (string itemName in s.outfits)
                {
                    OutfitItem item = SetupPenguinWardrobe.LoadItem(itemName);
                    if (item != null) outfit.startingItems.Add(item);
                }
        }

        Bounds? b = GetBounds(body);
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

        switch (s.mood)
        {
            case VillagerMood.Huddle:
                npc.focusPoint = fire;
                break;
            case VillagerMood.Lookout:
                npc.lookoutDirection = Vector3.forward;
                break;
            case VillagerMood.Pace:
                npc.wanderRadius = 3.5f;
                npc.walkSpeed = 1.6f;
                break;
            case VillagerMood.Work:
                Transform focus = FindNear(s.role == "Herrero" ? "Herreria" : s.role == "Mercader" ? "Puesto_Mercader" : null);
                npc.focusPoint = focus != null ? focus : fire;
                break;
        }

        PenguinRigAnimator rigAnim = body.GetComponent<PenguinRigAnimator>();
        if (rigAnim != null) rigAnim.referenceSpeed = npc.walkSpeed;

        Vector3 look = (npc.focusPoint != null ? npc.focusPoint.position : root.transform.position + Vector3.forward) - root.transform.position;
        if (s.mood == VillagerMood.Lookout) look = Vector3.forward;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) root.transform.rotation = Quaternion.LookRotation(look.normalized);
    }

    private static Transform FindNear(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        Transform t = _root.Find(name);
        return t;
    }

    private static void PlacePlayer()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) return;
        player.transform.position = new Vector3(0f, 0.05f, -5.5f);
        player.transform.rotation = Quaternion.identity;

        GameObject cam = GameObject.Find("Main Camera");
        if (cam == null) return;
        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow == null) return;
        cam.transform.position = player.transform.position + follow.offset;
        cam.transform.LookAt(player.transform.position + Vector3.up * follow.lookHeight);
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
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.isStatic = true;
        go.AddComponent<MeshFilter>().sharedMesh = _cone;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        if (Mats.TryGetValue(matName, out Material m)) mr.sharedMaterial = m;
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

    private static Vector3 Polar(float degFromNorth, float radius)
    {
        float a = degFromNorth * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
    }

    private static float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

    private static Bounds? GetBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return null;
        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);
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
}
