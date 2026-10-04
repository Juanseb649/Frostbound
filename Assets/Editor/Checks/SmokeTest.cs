using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Comprobaciones del proyecto. Se lanzan por el bridge (exec SmokeTest.X) o desde código.

public static class SmokeTest
{
    public static string NewGame()
    {
        var menu = Object.FindAnyObjectByType<MainMenuController>();
        if (menu == null) return "sin MainMenuController";
        menu.OpenClassSelect();
        return "ok";
    }

    public static string StartAdventure()
    {
        var select = Object.FindAnyObjectByType<ClassSelectController>();
        if (select == null) return "sin ClassSelectController";
        select.StartAdventure();
        return "ok";
    }

    public static string VillageState()
    {
        var session = GameSession.Instance;
        var player = Object.FindAnyObjectByType<PlayerController>();
        int sways = Object.FindObjectsByType<PenguinBodySway>().Length;
        int npcs = Object.FindObjectsByType<VillagerNPC>().Length;
        bool hud = Object.FindAnyObjectByType<GameHUD>() != null;
        return "escena " + SceneManager.GetActiveScene().name
            + " | clase " + (session != null && session.SelectedClass != null ? session.SelectedClass.name : "-")
            + " | héroe " + (session != null ? session.HeroName : "-")
            + " | player " + (player != null) + " | hud " + hud
            + " | sway " + sways + " / npc " + npcs;
    }

    public static string SmithOptions()
    {
        var smith = GameObject.Find("NPC_Herrera_Brunna");
        if (smith == null) return "sin herrera";
        var npc = smith.GetComponent<NPCInteractable>();
        return "configuradas: " + string.Join(",", npc.options) + " | visibles: " + string.Join(",", npc.AvailableOptions());
    }

    public static string Repair()
    {
        var hud = WorldHUD.Instance;
        if (hud == null || !hud.MenuOpen) return "menú cerrado";
        hud.MenuTarget.Choose(NPCOption.Reparar);
        return "ok";
    }

    public static string Notify()
    {
        Notifications.Show("Prueba de aviso", FrostboundUI.Ice);
        return "ok";
    }

    public static string SwayCheck()
    {
        var npc = Object.FindObjectsByType<VillagerNPC>().FirstOrDefault(v => v.mood == VillagerMood.Pace);
        if (npc == null) return "sin NPC Pace";
        var s = npc.GetComponent<PenguinBodySway>();
        return npc.name + " modelo " + (s.model != null ? s.model.localPosition.ToString("F3") + " " + s.model.localEulerAngles.ToString("F1") : "null");
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
}
