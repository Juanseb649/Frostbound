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
    public const string DatabasePath = "Assets/Resources/GameDatabase.asset";
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
        EditorUtility.SetDirty(gen);

        GameObject player = GameObject.Find("Player");
        if (player != null && player.GetComponent<PlayerPersistence>() == null) player.AddComponent<PlayerPersistence>();

        return "Exterior procedural listo: pino " + (gen.pinePrefab != null) + ", nieve " + (gen.moundPrefab != null)
            + ", hoguera " + (gen.bonfirePrefab != null) + ", campamento " + (camp != null ? camp.childCount + " piezas" : "no")
            + ", NavMesh " + (gen.navMesh != null) + ", jugador " + (player != null);
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
