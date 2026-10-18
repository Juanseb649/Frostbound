using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Crea la escena 02_Foothills (Capítulo I · Las Faldas) a partir del poblado: copia al héroe, la cámara,
// el HUD, la luz y el volumen, y añade el generador de la montaña. Se puede ejecutar varias veces.
public static class SetupFoothills
{
    private static readonly string[] Shared = { "EventSystem", "Main Camera", "HUD_Mundo", "GameUI", "Directional Light", "Global Volume", "Player" };

    public static string BuildList() => string.Join(", ", EditorBuildSettings.scenes.Select(s => s.path));

    [MenuItem("Tools/Frostbound/Montaña/Crear escena de Las Faldas")]
    public static void Run() => Create(SceneIds.FoothillsPath, (go, pine, mound, bonfire, snow, stone) =>
    {
        MountainGenerator mg = go.AddComponent<MountainGenerator>();
        mg.pinePrefab = pine; mg.moundPrefab = mound; mg.bonfirePrefab = bonfire; mg.snowMaterial = snow; mg.stoneMaterial = stone;
        return MountainLayout.Generate(mg.fallbackSeed).entry + Vector3.up * 0.1f;
    }, "Generador_Faldas");

    [MenuItem("Tools/Frostbound/Montaña/Crear escena de La Escalinata")]
    public static void RunStairs()
    {
        Create(SceneIds.StairsPath, (go, pine, mound, bonfire, snow, stone) =>
        {
            StairsGenerator sg = go.AddComponent<StairsGenerator>();
            sg.pinePrefab = pine; sg.bonfirePrefab = bonfire; sg.snowMaterial = snow; sg.stoneMaterial = stone;
            return StairsLayout.Entry;
        }, "Generador_Escalinata");
        Shaman();
    }

    [MenuItem("Tools/Frostbound/Bosque/Crear escena del Bosque Corrupto")]
    public static void RunForest() => Create(SceneIds.ForestPath, (go, pine, mound, bonfire, snow, stone) =>
    {
        ForestGenerator fg = go.AddComponent<ForestGenerator>();
        fg.pinePrefab = pine; fg.bonfirePrefab = bonfire; fg.snowMaterial = snow; fg.stoneMaterial = stone;
        fg.npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SetupPenguinWardrobe.PrefabPath);
        return ForestLayout.Generate(fg.fallbackSeed).entry + Vector3.up * 0.1f;
    }, "Generador_Bosque");

    // Para el bridge: crea la escena y vuelve a la que estaba abierta.
    public static string CreateForest()
    {
        string back = EditorSceneManager.GetActiveScene().path;
        RunForest();
        if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
        return "bosque creado; build=" + BuildList();
    }

    // Chamán corrupto: parte del mago corrupto; se une a las hordas tras la caída de Rocco.
    public static string Shaman()
    {
        const string path = "Assets/Data/Enemies/Corrupt_Shaman.asset";
        var mage = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/Corrupt_Mage.asset");
        var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
        if (def == null)
        {
            def = Object.Instantiate(mage);
            AssetDatabase.CreateAsset(def, path);
        }
        def.name = "Corrupt_Shaman";
        def.id = EnemyCamp.ShamanId;
        def.displayName = "Chamán corrupto";
        def.elite = false;
        def.maxHealth = 60f;
        def.experience = 24;
        def.ranged = true;
        def.attackRange = 11f;
        def.preferredDistance = new Vector2(8f, 11f);
        def.sightRange = 13f;
        EditorUtility.SetDirty(def);
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        if (!db.enemies.Contains(def)) { db.enemies.Add(def); EditorUtility.SetDirty(db); }
        AssetDatabase.SaveAssets();
        return "chamán listo";
    }

    public delegate Vector3 AddGenerator(GameObject go, GameObject pine, GameObject mound, GameObject bonfire, Material snow, Material stone);

    private static void Create(string scenePath, AddGenerator add, string generatorName)
    {
        Scene village = EditorSceneManager.OpenScene(SceneIds.VillagePath, OpenSceneMode.Single);
        VillageWorldGenerator gen = Object.FindAnyObjectByType<VillageWorldGenerator>();
        if (gen == null) { Debug.LogError("[Faldas] El poblado no tiene VillageWorldGenerator"); return; }
        GameObject pine = gen.pinePrefab, mound = gen.moundPrefab, bonfire = gen.bonfirePrefab;
        Material snow = gen.ground != null && gen.ground.GetComponent<Renderer>() != null ? gen.ground.GetComponent<Renderer>().sharedMaterial : null;
        Material stone = gen.castlePrefab != null ? gen.castlePrefab.stone : null;

        var holder = new GameObject("__Copia");
        SceneManager.MoveGameObjectToScene(holder, village);
        foreach (GameObject root in village.GetRootGameObjects())
            if (Shared.Contains(root.name)) root.transform.SetParent(holder.transform, true);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        GameObject copy = Object.Instantiate(holder);
        SceneManager.MoveGameObjectToScene(copy, scene);
        var children = new List<Transform>();
        foreach (Transform t in copy.transform) children.Add(t);
        foreach (Transform t in children) t.SetParent(null, true);
        Object.DestroyImmediate(copy);
        EditorSceneManager.CloseScene(village, true);
        SceneManager.SetActiveScene(scene);

        var generator = new GameObject(generatorName);
        Vector3 start = add(generator, pine, mound, bonfire, snow, stone);
        GameObject player = GameObject.Find("Player");
        if (player != null) player.transform.position = start;

        EditorSceneManager.SaveScene(scene, scenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == scenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Montaña] Escena creada: " + scenePath);
    }
}
