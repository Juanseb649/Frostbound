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

    public static string GolemSummon()
    {
        GameObject p = GameObject.Find("Player");
        if (p == null) return "sin héroe";
        var g = SnowGolem.Summon(p, p.transform.position + p.transform.forward * 2f, 120f, 8f);
        return "gólem vivo=" + g.Alive + " vida=" + g.Health.Health + "/" + g.Health.maxHealth + " navmesh=" + g.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh + " total=" + SnowGolem.Instances.Count;
    }

    public static string GolemState()
    {
        var sb = new System.Text.StringBuilder();
        GameObject p = GameObject.Find("Player");
        foreach (var g in SnowGolem.Instances)
            sb.Append(g.name).Append(" vivo=").Append(g.Alive).Append(" vida=").Append(Mathf.RoundToInt(g.Health.Health))
              .Append(" dist=").Append(p != null ? Vector3.Distance(p.transform.position, g.transform.position).ToString("F1") : "-").Append(" | ");
        return sb.Length > 0 ? sb.ToString() : "ninguno";
    }

    public static string GolemHitByPlayer()
    {
        GameObject p = GameObject.Find("Player");
        var g = SnowGolem.Instances.Count > 0 ? SnowGolem.Instances[0] : null;
        if (g == null) return "ninguno";
        float before = g.Health.Health;
        g.Health.TakeHit(new DamageInfo { amount = 50f, source = p });
        return "golpe del héroe: " + before + "→" + g.Health.Health;
    }

    public static string GolemKill()
    {
        var g = SnowGolem.Instances.Count > 0 ? SnowGolem.Instances[0] : null;
        if (g == null) return "ninguno";
        g.Health.TakeHit(new DamageInfo { amount = 99999f, source = null, direction = Vector3.forward });
        int rbs = 0;
        foreach (var rb in Object.FindObjectsByType<Rigidbody>()) if (rb.name == "Base" || rb.name == "Cuerpo" || rb.name == "Cabeza" || rb.name.StartsWith("Brazo")) rbs++;
        return "muerto=" + g.Health.IsDead + " piezas con rigidbody=" + rbs + " articulaciones=" + Object.FindObjectsByType<CharacterJoint>().Length;
    }

    public static string EquipSpear()
    {
        GameObject p = GameObject.Find("Player");
        var eq = p.GetComponent<Equipment>();
        var db = GameDatabase.Instance;
        eq.SetEquippedStack(EquipSlot.Weapon, new ItemStack(db.items.Find("spear"), 1));
        var c = p.GetComponent<PlayerCombat>();
        return "lanza=" + (eq.MainWeapon != null ? eq.MainWeapon.item.name : "no") + " combo=" + c.UsesPolearmCombo;
    }

    public static string SpearAttack()
    {
        GameObject p = GameObject.Find("Player");
        var c = p.GetComponent<PlayerCombat>();
        bool ok = c.TryAttack(p.transform.position + p.transform.forward * 3f);
        var rig = p.GetComponentInChildren<PenguinRigAnimator>();
        return "ataque=" + ok + " paso=" + rig.ComboStep + " lanza=" + rig.PolearmCombo;
    }

    public static string InventoryTabs()
    {
        var inv = Object.FindAnyObjectByType<InventoryScreen>();
        if (inv == null) return "sin inventario";
        var sb = new System.Text.StringBuilder();
        foreach (InventoryScreen.Tab t in new[] { InventoryScreen.Tab.Character, InventoryScreen.Tab.Equipment, InventoryScreen.Tab.Inventory })
        {
            inv.OpenTab(t);
            sb.Append(t).Append(": ");
            foreach (Transform child in inv.transform.Find("Window"))
                if (child.gameObject.activeSelf && child.name.EndsWith("Panel")) sb.Append(child.name).Append(" ");
            sb.Append("| ");
        }
        inv.Close();
        return sb.ToString() + " abierto=" + inv.IsOpen + " bloqueo=" + GameplayInput.Blocked;
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
