using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Fase1Migration
{
    public static string RemoveOldMenu()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Main menu.unity", OpenSceneMode.Single);
        var removed = new List<string>();
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            bool old = go.name == "MenuManager" || (go.name == "Canvas" && go.GetComponent<Canvas>() != null && go.GetComponentInChildren<MainMenuController>(true) == null);
            if (!old) continue;
            removed.Add(go.name);
            Object.DestroyImmediate(go);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Eliminados: " + string.Join(", ", removed);
    }

    public static string MissingScripts()
    {
        var report = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            report.Add(System.IO.Path.GetFileName(path) + "=" + count);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            int count = p.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if (count > 0) report.Add(p.name + "=" + count);
        }
        return string.Join(" ", report);
    }

    public static string AssignWorldHudSkin()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        WorldHUD hud = Object.FindAnyObjectByType<WorldHUD>(FindObjectsInactive.Include);
        if (hud == null) return "sin WorldHUD";
        hud.skin = AssetDatabase.LoadAssetAtPath<UISkin>("Assets/Data/UI/UISkin.asset");
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return hud.skin != null ? "skin asignado" : "UISkin no encontrado";
    }
}
