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
        var menu = Object.FindAnyObjectByType<FrostMenu>();
        if (menu == null) return "sin FrostMenu";
        menu.ShowCreate();
        return "ok";
    }

    public static string MountainInfo()
    {
        var r = MountainLayout.Generate(12345);
        int walk = 0, total = 0;
        for (float z = 0f; z < MountainLayout.Length; z += 2f)
            for (float x = -60f; x < 60f; x += 2f) { total++; if (r.Walkable(x, z)) walk++; }
        var sb = new System.Text.StringBuilder();
        sb.Append("transitable=").Append(walk * 100 / total).Append("% claros=").Append(r.clearings.Count);
        foreach (var c in r.clearings) sb.Append(" ").Append(c.kind).Append("@").Append(Mathf.RoundToInt(c.center.z));
        sb.Append(" pinos=").Append(r.pines.Count).Append(" rocas=").Append(r.rocks.Count).Append(" mojones=").Append(r.cairns.Count);
        sb.Append(" entrada=").Append(r.entry).Append(" cima=").Append(r.summitGate);
        bool linked = true;
        for (float z = 4f; z < MountainLayout.Length - 4f; z += 1f) if (!r.Walkable(r.CenterX(z), z)) { linked = false; sb.Append(" corte@").Append(z); break; }
        sb.Append(" sendero_continuo=").Append(linked);
        return sb.ToString();
    }

    public static string ZoneState()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("escena=").Append(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        GameObject p = GameObject.Find("Player");
        sb.Append(" heroe=").Append(p != null ? p.transform.position.ToString("F1") : "no");
        if (p != null)
        {
            bool nav = UnityEngine.AI.NavMesh.SamplePosition(p.transform.position, out _, 2f, UnityEngine.AI.NavMesh.AllAreas);
            sb.Append(" navmesh=").Append(nav);
            var ab = p.GetComponent<PlayerAbility>();
            sb.Append(" habilidad=").Append(ab != null ? ab.Data.name : "no");
            var st = p.GetComponent<CharacterStats>();
            if (st != null) sb.Append(" mana=").Append(Mathf.RoundToInt(st.currentMana)).Append("/").Append(Mathf.RoundToInt(st.MaxMana));
        }
        var mg = MountainGenerator.Instance;
        if (mg != null) sb.Append(" montaña=").Append(mg.Built).Append(" seed=").Append(mg.Seed).Append(" cima=").Append(mg.SummitCamp != null);
        sb.Append(" campamentos=").Append(Object.FindObjectsByType<EnemyCamp>().Length);
        sb.Append(" hogueras=").Append(Bonfire.Instances.Count);
        sb.Append(" pasos=").Append(Object.FindObjectsByType<ZoneGate>().Length);
        sb.Append(" pausa=").Append(Object.FindAnyObjectByType<PauseMenu>() != null);
        sb.Append(" mision=").Append(QuestLog.Stage(QuestLog.Ascent)).Append(" '").Append(QuestLog.Objective(QuestLog.Tracked(), QuestLog.Stage(QuestLog.Tracked()))).Append("'");
        return sb.ToString();
    }

    public static string InputState()
    {
        var m = UnityEngine.InputSystem.Mouse.current;
        var k = UnityEngine.InputSystem.Keyboard.current;
        GameObject p = GameObject.Find("Player");
        var rb = p != null ? p.GetComponent<Rigidbody>() : null;
        return "izq=" + (m != null && m.leftButton.isPressed) + " der=" + (m != null && m.rightButton.isPressed)
            + " w=" + (k != null && k.wKey.isPressed) + " vel=" + (rb != null ? rb.linearVelocity.ToString("F1") : "-")
            + " foco=" + Application.isFocused;
    }

    public static string UseAbility()
    {
        GameObject p = GameObject.Find("Player");
        var ab = p != null ? p.GetComponent<PlayerAbility>() : null;
        if (ab == null) return "sin habilidad";
        var st = p.GetComponent<CharacterStats>();
        float before = st.currentMana;
        bool ok = ab.TryUse();
        return ok + " maná " + Mathf.RoundToInt(before) + "→" + Mathf.RoundToInt(st.currentMana) + " recarga=" + ab.CooldownLeft.ToString("F1");
    }

    public static string TogglePause()
    {
        var pm = Object.FindAnyObjectByType<PauseMenu>();
        if (pm == null) return "sin pausa";
        if (PauseMenu.IsPaused) pm.Resume(); else pm.Pause();
        return "pausado=" + PauseMenu.IsPaused + " timeScale=" + Time.timeScale;
    }

    public static string TryLeaveMountain()
    {
        var mg = MountainGenerator.Instance;
        GameObject p = GameObject.Find("Player");
        if (mg == null || p == null) return "no";
        var pp = p.GetComponent<PlayerPersistence>();
        Vector3 gate = mg.Layout.bottomGate;
        pp.MoveTo(gate + Vector3.up * 0.2f, Quaternion.identity);
        return "en el paso";
    }

    public static string MenuState()
    {
        var menu = Object.FindAnyObjectByType<FrostMenu>();
        return menu != null ? menu.State() : "sin FrostMenu";
    }

    public static string MenuHeroes()
    {
        var menu = Object.FindAnyObjectByType<FrostMenu>();
        if (menu == null) return "sin FrostMenu";
        menu.ShowHeroes();
        return "ok";
    }

    public static string StartAdventure()
    {
        var menu = Object.FindAnyObjectByType<FrostMenu>();
        if (menu == null) return "sin FrostMenu";
        menu.StartAdventure();
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
