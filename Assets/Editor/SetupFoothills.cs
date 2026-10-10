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
    public static void Run()
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

        var mountain = new GameObject("Generador_Faldas");
        MountainGenerator mg = mountain.AddComponent<MountainGenerator>();
        mg.pinePrefab = pine;
        mg.moundPrefab = mound;
        mg.bonfirePrefab = bonfire;
        mg.snowMaterial = snow;
        mg.stoneMaterial = stone;

        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            var layout = MountainLayout.Generate(mg.fallbackSeed);
            player.transform.position = layout.entry + Vector3.up * 0.1f;
        }

        EditorSceneManager.SaveScene(scene, SceneIds.FoothillsPath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == SceneIds.FoothillsPath))
        {
            scenes.Add(new EditorBuildSettingsScene(SceneIds.FoothillsPath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Faldas] Escena creada: " + SceneIds.FoothillsPath);
    }
}
