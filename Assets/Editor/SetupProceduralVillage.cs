using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Convierte el exterior del poblado en generado por seed: guarda el pino, el montón de nieve y la hoguera como
// prefabs, quita los del poblado fijo, agrupa los bancos y los aldeanos de la hoguera y añade el generador.
// Se puede ejecutar varias veces. También crea Resources/GameDatabase.asset (clases, objetos, paleta y UI).
public static class SetupProceduralVillage
{
    private const string PrefabFolder = "Assets/Prefabs/Village";
    public const string PinePath = PrefabFolder + "/Pino.prefab";
    public const string MoundPath = PrefabFolder + "/Monton_Nieve.prefab";
    public const string BonfirePath = PrefabFolder + "/Hoguera.prefab";
    public const string CastlePath = PrefabFolder + "/Castillo.prefab";
    public const string DatabasePath = "Assets/Resources/GameDatabase.asset";
    private const string CastleMatFolder = "Assets/Materials/Castle";
    private const string GeneratorName = "Generador_Exterior";
    private const string CampName = "Campamento_Hoguera";

    [MenuItem("Tools/Frostbound/Poblado/Exterior procedural y hogueras")]
    public static void Run()
    {
        if (!FrostboundBridge.ConfirmSave()) return;
        string result = Convert();
        FrostboundBridge.Dialog("Frostbound", result, "OK");
    }

    public static string Convert()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != SceneIds.VillagePath) scene = EditorSceneManager.OpenScene(SceneIds.VillagePath, OpenSceneMode.Single);
        string result = ConvertOpenScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EnsureDatabase();
        AssetDatabase.SaveAssets();
        return result;
    }

    public static string ConvertOpenScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Village");

        GameObject forest = Find("Bosque");
        GameObject mounds = Find("Montones_Nieve");
        GameObject fire = Find("Hoguera");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PinePath) == null && forest != null && forest.transform.childCount > 0)
            SaveTemplate(forest.transform.GetChild(0).gameObject, PinePath, false, false);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MoundPath) == null && mounds != null && mounds.transform.childCount > 0)
            SaveTemplate(mounds.transform.GetChild(0).gameObject, MoundPath, true, false);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BonfirePath) == null && fire != null)
            SaveTemplate(fire, BonfirePath, true, true);

        Transform camp = EnsureCamp(fire);
        if (forest != null) Object.DestroyImmediate(forest);
        if (mounds != null) Object.DestroyImmediate(mounds);
        if (fire != null) Object.DestroyImmediate(fire);

        GameObject genGo = GameObject.Find(GeneratorName);
        if (genGo == null) genGo = new GameObject(GeneratorName);
        VillageWorldGenerator gen = genGo.GetComponent<VillageWorldGenerator>();
        if (gen == null) gen = genGo.AddComponent<VillageWorldGenerator>();
        gen.pinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PinePath);
        gen.moundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoundPath);
        gen.bonfirePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BonfirePath);
        gen.bonfireCamp = camp;
        gen.navMesh = Object.FindAnyObjectByType<NavMeshSurface>();
        gen.castlePrefab = EnsureCastlePrefab();
        GameObject groundGo = GameObject.Find("Ground");
        gen.ground = groundGo != null ? groundGo.transform : null;
        if (camp != null) camp.position = SetupVillage.FirePos;
        EditorUtility.SetDirty(gen);

        GameObject player = GameObject.Find("Player");
        if (player != null && player.GetComponent<PlayerPersistence>() == null) player.AddComponent<PlayerPersistence>();

        return "Exterior procedural listo: pino " + (gen.pinePrefab != null) + ", nieve " + (gen.moundPrefab != null)
            + ", hoguera " + (gen.bonfirePrefab != null) + ", campamento " + (camp != null ? camp.childCount + " piezas" : "no")
            + ", NavMesh " + (gen.navMesh != null) + ", castillo " + (gen.castlePrefab != null) + ", suelo " + (gen.ground != null) + ", jugador " + (player != null);
    }

    private static CastleBuilder EnsureCastlePrefab()
    {
        if (!AssetDatabase.IsValidFolder(CastleMatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Castle");
        Material template = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Village/Stone.mat");
        var go = new GameObject("Castillo");
        var b = go.AddComponent<CastleBuilder>();
        b.stone = CastleMat("Gothic_Stone", template, new Color(0.83f, 0.84f, 0.86f), 0.12f);
        b.trim = CastleMat("Gothic_Trim", template, new Color(0.58f, 0.61f, 0.66f), 0.15f);
        b.roof = CastleMat("Gothic_Roof", template, new Color(0.19f, 0.24f, 0.33f), 0.25f);
        b.glass = CastleMat("Gothic_Glass", template, new Color(0.35f, 0.55f, 0.95f), 0.8f, new Color(0.35f, 0.75f, 1.6f));
        b.gold = CastleMat("Gothic_Gold", template, new Color(0.86f, 0.70f, 0.36f), 0.7f, new Color(0.25f, 0.18f, 0.05f));
        b.rock = CastleMat("Gothic_Rock", template, new Color(0.33f, 0.37f, 0.45f), 0.08f);
        b.snow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Village/Snow.mat");
        b.ice = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Village/IceStatue.mat");
        b.door = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Village/WoodDark.mat");
        b.lampGlow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Village/WindowGlow.mat");
        b.cone = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/Cone_LowPoly.asset");
        b.prism = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/Prism_Roof.asset");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, CastlePath);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<CastleBuilder>();
    }

    private static Material CastleMat(string name, Material template, Color color, float smoothness, Color? emission = null)
    {
        string path = CastleMatFolder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Transform EnsureCamp(GameObject fire)
    {
        GameObject existing = GameObject.Find(CampName);
        Vector3 center = fire != null ? fire.transform.position : SetupVillage.FirePos;
        Transform camp = existing != null ? existing.transform : new GameObject(CampName).transform;
        if (existing == null) camp.position = center;

        GameObject benches = Find("Bancos");
        if (benches != null && benches.transform.parent != camp) benches.transform.SetParent(camp, true);
        foreach (VillagerNPC v in Object.FindObjectsByType<VillagerNPC>(FindObjectsInactive.Include))
        {
            if (v.mood != VillagerMood.Huddle || v.transform.parent == camp) continue;
            v.transform.SetParent(camp, true);
            v.focusPoint = null;
            EditorUtility.SetDirty(v);
        }
        return camp;
    }

    private static void SaveTemplate(GameObject source, string path, bool resetScale, bool bonfire)
    {
        GameObject copy = Object.Instantiate(source);
        copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
        copy.transform.SetParent(null);
        copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        if (resetScale) copy.transform.localScale = Vector3.one;
        foreach (Transform t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
        if (bonfire && copy.GetComponent<Bonfire>() == null) copy.AddComponent<Bonfire>();
        PrefabUtility.SaveAsPrefabAsset(copy, path);
        Object.DestroyImmediate(copy);
    }

    private static GameObject Find(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }

    public static GameDatabase EnsureDatabase()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        GameDatabase db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<GameDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
        }
        db.classes = AssetDatabase.FindAssets("t:CharacterClass", new[] { "Assets/Data/Classes" })
            .Select(g => AssetDatabase.LoadAssetAtPath<CharacterClass>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null).OrderBy(c => c.name).ToList();
        db.items = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/Data/Items/ItemDatabase.asset");
        db.palette = AssetDatabase.LoadAssetAtPath<PlumagePalette>("Assets/Data/PlumagePalette.asset");
        db.uiSkin = AssetDatabase.LoadAssetAtPath<UISkin>("Assets/Data/UI/UISkin.asset");
        EditorUtility.SetDirty(db);
        return db;
    }
}
