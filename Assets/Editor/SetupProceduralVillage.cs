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

        int chatters = PlazaToChatters();
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
        gen.navMesh = Object.FindAnyObjectByType<NavMeshSurface>();
        gen.castlePrefab = EnsureCastlePrefab();
        GameObject groundGo = GameObject.Find("Ground");
        gen.ground = groundGo != null ? groundGo.transform : null;
        EditorUtility.SetDirty(gen);

        GameObject player = GameObject.Find("Player");
        if (player != null && player.GetComponent<PlayerPersistence>() == null) player.AddComponent<PlayerPersistence>();

        return "Exterior procedural listo: pino " + (gen.pinePrefab != null) + ", nieve " + (gen.moundPrefab != null)
            + ", hoguera " + (gen.bonfirePrefab != null) + ", aldeanos charlando " + chatters
            + ", NavMesh " + (gen.navMesh != null) + ", castillo " + (gen.castlePrefab != null) + ", suelo " + (gen.ground != null) + ", jugador " + (player != null);
    }

    private static CastleBuilder EnsureCastlePrefab()
    {
        if (!AssetDatabase.IsValidFolder(CastleMatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Castle");
        Material template = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Village/Stone.mat");
        var go = new GameObject("Castillo");
        var b = go.AddComponent<CastleBuilder>();
        b.stone = CastleMat("Gothic_Stone", template, new Color(0.17f, 0.18f, 0.22f), 0.18f);
        b.trim = CastleMat("Gothic_Trim", template, new Color(0.25f, 0.26f, 0.31f), 0.22f);
        b.roof = CastleMat("Gothic_Roof", template, new Color(0.055f, 0.06f, 0.085f), 0.55f);
        b.glass = CastleMat("Gothic_Glass", template, new Color(0.08f, 0.35f, 0.6f), 0.85f, new Color(0.15f, 0.75f, 2.2f));
        b.gold = CastleMat("Gothic_Gold", template, new Color(0.3f, 0.29f, 0.32f), 0.6f);
        b.rock = CastleMat("Gothic_Rock", template, new Color(0.15f, 0.17f, 0.21f), 0.1f);
        b.snow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Village/Snow.mat");
        b.ice = CastleMat("Frost_Corrupt", template, new Color(0.03f, 0.05f, 0.12f), 0.92f, new Color(0.08f, 0.35f, 1.4f));
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

    // La plaza ya no tiene fogata: los aldeanos que se acurrucaban ahí ahora pasean y charlan entre ellos.
    // Se quitan los bancos y el grupo antiguo "Campamento_Hoguera" (si existía).
    private static int PlazaToChatters()
    {
        GameObject camp = GameObject.Find(CampName);
        GameObject steveFire = Find("Fogata_Steve");
        int n = 0;
        foreach (VillagerNPC v in Object.FindObjectsByType<VillagerNPC>(FindObjectsInactive.Include))
        {
            if (camp != null && v.transform.IsChildOf(camp.transform)) v.transform.SetParent(null, true);
            if (v.name == SetupSteve.ObjectName)
            {
                // Steve se queda en su claro, junto a su fogata.
                v.mood = VillagerMood.Huddle;
                if (steveFire != null) v.focusPoint = steveFire.transform;
                EditorUtility.SetDirty(v);
                continue;
            }
            if (v.mood != VillagerMood.Huddle && v.mood != VillagerMood.Chat) continue;
            Vector3 off = v.transform.position - SetupVillage.FirePos;
            off.y = 0f;
            if (off.magnitude > 10f) continue;
            if (v.mood == VillagerMood.Huddle)
            {
                if (off.sqrMagnitude < 0.01f) off = Vector3.forward;
                v.transform.position = SetupVillage.FirePos + off.normalized * (3.5f + n * 0.9f);
            }
            v.mood = VillagerMood.Chat;
            v.focusPoint = null;
            v.wanderRadius = 6f;
            v.walkSpeed = 1.1f;
            v.fear = Mathf.Min(v.fear, 0.45f);
            EditorUtility.SetDirty(v);
            n++;
        }
        GameObject benches = Find("Bancos");
        if (benches != null) Object.DestroyImmediate(benches);
        if (camp != null) Object.DestroyImmediate(camp);
        return n;
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
        db.enemies = AssetDatabase.FindAssets("t:EnemyDefinition", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(e => e != null).OrderBy(e => e.name).ToList();
        EditorUtility.SetDirty(db);
        return db;
    }
}
