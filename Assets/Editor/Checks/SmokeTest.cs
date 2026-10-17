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

    public static string MageTest()
    {
        GameObject p = GameObject.Find("Player");
        var eq = p.GetComponent<Equipment>();
        var db = GameDatabase.Instance;
        eq.SetEquippedStack(EquipSlot.Weapon, new ItemStack(db.items.Find("mage_staff"), 1));
        eq.SetEquippedStack(EquipSlot.Chest, new ItemStack(db.items.Find("mage_robe"), 1));
        eq.SetEquippedStack(EquipSlot.Head, new ItemStack(db.items.Find("mage_hat"), 1));
        var c = p.GetComponent<PlayerCombat>();
        bool ok = c.TryAttack(p.transform.position + p.transform.forward * 6f);
        var parts = new System.Text.StringBuilder();
        foreach (var r in p.GetComponentsInChildren<SkinnedMeshRenderer>())
            if (r.name.StartsWith("Outfit_")) parts.Append(r.name.Replace("Outfit_", "")).Append(",");
        return "magia=" + c.UsesMagic + " alcance=" + c.Range + " ataque=" + ok + " piezas=" + parts;
    }

    public static string OrbCount() => "orbes=" + Object.FindObjectsByType<MagicOrb>().Length + " destellos=" + GameObject.FindObjectsByType<Light>().Length;

    public static string NinjaSetup()
    {
        GameObject p = GameObject.Find("Player");
        var eq = p.GetComponent<Equipment>();
        var db = GameDatabase.Instance;
        p.GetComponent<CharacterStats>().characterClass = db.FindClass("Ninja");
        eq.SetEquippedStack(EquipSlot.Chest, new ItemStack(db.items.Find("ninja_suit"), 1));
        eq.SetEquippedStack(EquipSlot.Head, new ItemStack(db.items.Find("ninja_hood"), 1));
        eq.SetEquippedStack(EquipSlot.Weapon, new ItemStack(db.items.Find("ninja_katana"), 1));
        return "katana=" + (eq.MainWeapon != null ? eq.MainWeapon.item.displayName : "no");
    }

    public static string NinjaState()
    {
        GameObject p = GameObject.Find("Player");
        var c = p.GetComponent<PlayerCombat>();
        var holder = p.GetComponentInChildren<WeaponHolder>();
        var sb = new System.Text.StringBuilder();
        sb.Append("estilo=").Append(c.Carry != null ? c.Carry.GetType().Name : "-");
        if (c.Carry is BackSheathCarry b) sb.Append(" envainada=").Append(b.Sheathed);
        sb.Append(" lista=").Append(c.Carry != null && c.Carry.Ready);
        sb.Append(" arma_en=").Append(holder.MainModel != null && holder.MainModel.transform.parent != null ? holder.MainModel.transform.parent.name : "-");
        var parts = new System.Text.StringBuilder();
        foreach (var r in p.GetComponentsInChildren<SkinnedMeshRenderer>())
            if (r.name.StartsWith("Outfit_Pieza_Ninja")) parts.Append(r.name.Replace("Outfit_Pieza_Ninja_", "")).Append(",");
        sb.Append(" piezas=").Append(parts);
        return sb.ToString();
    }

    public static string Attack()
    {
        GameObject p = GameObject.Find("Player");
        var c = p.GetComponent<PlayerCombat>();
        return "ataque=" + c.TryAttack(p.transform.position + p.transform.forward * 3f);
    }

    public static string StairsState()
    {
        var sb = new System.Text.StringBuilder();
        var sg = StairsGenerator.Instance;
        sb.Append("escalinata=").Append(sg != null && sg.Built);
        GameObject p = GameObject.Find("Player");
        if (p != null) sb.Append(" héroe=").Append(p.transform.position.ToString("F1"));
        var path = new UnityEngine.AI.NavMeshPath();
        bool a = UnityEngine.AI.NavMesh.SamplePosition(StairsLayout.Entry, out var from, 2f, UnityEngine.AI.NavMesh.AllAreas);
        bool b = UnityEngine.AI.NavMesh.SamplePosition(StairsLayout.ArenaCenter, out var to, 3f, UnityEngine.AI.NavMesh.AllAreas);
        if (a && b) { UnityEngine.AI.NavMesh.CalculatePath(from.position, to.position, UnityEngine.AI.NavMesh.AllAreas, path); sb.Append(" camino_a_la_cima=").Append(path.status).Append(" esquinas=").Append(path.corners.Length); }
        else sb.Append(" navmesh entrada=").Append(a).Append(" cima=").Append(b);
        for (int i = 1; i < StairsLayout.Terraces; i++)
        {
            bool t = UnityEngine.AI.NavMesh.SamplePosition(StairsLayout.TerraceCenter(i), out var h, 1.5f, UnityEngine.AI.NavMesh.AllAreas);
            if (!t) sb.Append(" sin_navmesh_terraza_").Append(i);
        }
        var boss = sg != null && sg.Encounter != null ? sg.Encounter.Boss : null;
        sb.Append(" rocco=").Append(boss != null ? boss.title + " vida=" + Mathf.RoundToInt(boss.Health.Health) : "no");
        sb.Append(" campamentos=").Append(Object.FindObjectsByType<EnemyCamp>().Length).Append(" hogueras=").Append(Bonfire.Instances.Count);
        return sb.ToString();
    }

    public static string WalkPlayerToArena()
    {
        GameObject p = GameObject.Find("Player");
        p.GetComponent<PlayerController>().MoveTo(StairsLayout.ArenaCenter);
        return "en marcha";
    }

    public static string RoccoHit(float fraction)
    {
        var boss = StairsGenerator.Instance.Encounter.Boss;
        boss.Health.TakeHit(new DamageInfo { amount = boss.Health.maxHealth * fraction, source = GameObject.Find("Player") });
        return "vida=" + Mathf.RoundToInt(boss.Health.Health) + "/" + Mathf.RoundToInt(boss.Health.maxHealth) + " golems_corruptos=" + CountHostile();
    }

    public static string RoccoHit35() => RoccoHit(0.35f);

    private static int CountHostile()
    {
        int n = 0;
        foreach (var g in SnowGolem.Instances) if (g != null && g.Hostile && g.Alive) n++;
        return n;
    }

    public static string ShamanTest()
    {
        QuestLog.SetStage(QuestLog.Ascent, QuestLog.AscentRocco);
        int shamans = 0, knights = 0, total = 0;
        foreach (var camp in Object.FindObjectsByType<EnemyCamp>())
        {
            if (camp.Members.Count == 0) camp.Spawn();
            foreach (var b in camp.Members)
            {
                total++;
                if (b.GetComponent<ShamanCaster>() != null) shamans++;
                if (b.definition != null && b.definition.id == "Corrupt_Knight") knights++;
            }
        }
        return "enemigos=" + total + " chamanes=" + shamans + " caballeros=" + knights;
    }

    public static string ProfileInfo()
    {
        var r = MountainLayout.Generate(12345);
        int flat = 0;
        for (int z = 1; z < r.profile.Length; z++) if (Mathf.Abs(r.profile[z] - r.profile[z - 1]) < 0.02f) flat++;
        return "metros llanos=" + flat + "/" + r.profile.Length + " altura final=" + r.profile[r.profile.Length - 1].ToString("F1");
    }

    public static string SteerTest()
    {
        GameObject p = GameObject.Find("Player");
        var pc = p.GetComponent<PlayerController>();
        var mg = MountainGenerator.Instance;
        Vector3 goal = mg != null ? mg.Layout.Ground(mg.Layout.CenterX(48f), 48f) : p.transform.position + new Vector3(0f, 0f, 25f);
        pc.MoveTo(goal);
        return "moviendo";
    }

    public static string SteerState()
    {
        GameObject p = GameObject.Find("Player");
        return "navmesh=" + p.GetComponent<PlayerController>().UsingNavMesh + " pos=" + p.transform.position.ToString("F1");
    }

    public static string HordeInfo()
    {
        var mg = MountainGenerator.Instance;
        var t = typeof(MountainGenerator);
        var f = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        return "t=" + Time.timeSinceLevelLoad.ToString("F0") + " siguiente=" + ((float)t.GetField("_nextHorde", f).GetValue(mg)).ToString("F0")
            + " hordas=" + t.GetField("_hordeCount", f).GetValue(mg) + " jugador=" + (t.GetField("_player", f).GetValue(mg) != null);
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

    // Pulsa el botón "Jugar" del título como lo haría el jugador.
    public static string MenuPlayButton()
    {
        var menu = Object.FindAnyObjectByType<FrostMenu>();
        if (menu == null) return "sin FrostMenu";
        var field = typeof(FrostMenu).GetField("_play", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var play = field != null ? field.GetValue(menu) as FrostButton : null;
        if (play == null) return "sin botón Jugar";
        play.button.onClick.Invoke();
        return "jugar pulsado";
    }

    // Pasa el foco del título a "Opciones" (para ver el destello y la escarcha del marco).
    public static string MenuFocusOptions()
    {
        var menu = Object.FindAnyObjectByType<FrostMenu>();
        if (menu == null) return "sin FrostMenu";
        var field = typeof(FrostMenu).GetField("_options", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var b = field != null ? field.GetValue(menu) as FrostButton : null;
        if (b == null || UnityEngine.EventSystems.EventSystem.current == null) return "sin botón";
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(b.gameObject);
        string mat = b.border.material != null ? b.border.material.shader.name : "-";
        return "foco en Opciones · shader=" + mat;
    }

    // Valores del material de efectos del botón "Opciones" (destello y escarcha) en este instante.
    public static string MenuFxState()
    {
        var menu = Object.FindAnyObjectByType<FrostMenu>();
        if (menu == null) return "sin FrostMenu";
        var field = typeof(FrostMenu).GetField("_options", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var b = field != null ? field.GetValue(menu) as FrostButton : null;
        Material m = b != null ? b.border.material : null;
        if (m == null || !m.HasProperty("_SheenPos")) return "sin material de efectos";
        Material shown = b.border.materialForRendering;
        return "sheen=" + m.GetFloat("_SheenPos").ToString("0.00") + " frost=" + m.GetFloat("_Frost").ToString("0.00")
            + " rect=" + m.GetVector("_Rect") + " t=" + Time.unscaledTime.ToString("0.00")
            + " render=" + (shown != null ? shown.shader.name + (shown == m ? " (mismo)" : " (copia de máscara)") : "-");
    }

    // Congela el botón "Opciones" con el destello a mitad de recorrido (para capturarlo). Pasa false para soltarlo.
    public static string MenuFreezeSheen()
    {
        var menu = Object.FindAnyObjectByType<FrostMenu>();
        var field = typeof(FrostMenu).GetField("_options", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var b = menu != null && field != null ? field.GetValue(menu) as FrostButton : null;
        if (b == null || b.border.material == null) return "sin botón";
        b.enabled = false;
        b.border.material.SetFloat("_SheenPos", 0.5f);
        return "destello congelado en 0,5";
    }

    // Cierra la mochila con una disolución lenta (6 s) para poder capturarla a medio camino.
    public static string SlowDissolveInventory()
    {
        var inv = Object.FindAnyObjectByType<InventoryScreen>();
        if (inv == null) return "sin InventoryScreen";
        var field = typeof(InventoryScreen).GetField("_window", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var win = field != null ? field.GetValue(inv) as RectTransform : null;
        UIDissolve.Ghost(win, 6f);
        inv.Close();
        return "disolución lenta en marcha · fantasmas=" + Object.FindObjectsByType<UIDissolve>().Length;
    }

    // Abre y cierra la mochila (para capturar la ventana del héroe).
    public static string OpenInventory()
    {
        var inv = Object.FindAnyObjectByType<InventoryScreen>();
        if (inv == null) return "sin InventoryScreen";
        inv.OpenTab(InventoryScreen.Tab.Inventory);
        return "mochila abierta";
    }

    public static string OpenEquipment()
    {
        var inv = Object.FindAnyObjectByType<InventoryScreen>();
        if (inv == null) return "sin InventoryScreen";
        inv.OpenTab(InventoryScreen.Tab.Equipment);
        return "equipo abierto";
    }

    public static string OpenCharacter()
    {
        var inv = Object.FindAnyObjectByType<InventoryScreen>();
        if (inv == null) return "sin InventoryScreen";
        inv.OpenTab(InventoryScreen.Tab.Character);
        return "personaje abierto";
    }

    // Abre la tienda del mercader Tico (comprar) para revisar la ventana de comercio.
    public static string OpenShop()
    {
        GameObject go = GameObject.Find("NPC_Mercader_Tico");
        if (go == null) return "sin Tico";
        TradeWindow.OpenShop(go.GetComponent<NPCInteractable>(), false);
        return "tienda abierta";
    }

    // Abre el arcón de la taberna sin mover objetos (OpenStashAndStore sí guarda cosas).
    public static string OpenStash()
    {
        GameObject go = GameObject.Find("NPC_Tabernera_Mora");
        if (go == null) return "sin Tabernera";
        TradeWindow.OpenStash(go.GetComponent<NPCInteractable>());
        return "arcón abierto";
    }

    public static string CloseInventory()
    {
        var inv = Object.FindAnyObjectByType<InventoryScreen>();
        if (inv != null) inv.Close();
        return "mochila cerrada";
    }

    // Como pulsar "Jugar" en la pantalla de héroes: carga la partida seleccionada (no crea ninguna nueva).
    public static string MenuPlaySelected()
    {
        var menu = Object.FindAnyObjectByType<FrostMenu>();
        if (menu == null) return "sin FrostMenu";
        var field = typeof(FrostMenu).GetField("_selectedSlot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        int slot = field != null ? (int)field.GetValue(menu) : -1;
        if (slot < 0) return "sin héroe seleccionado";
        menu.LoadSlot(slot);
        return "cargando ranura " + slot;
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
