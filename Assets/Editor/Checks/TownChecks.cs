using System.Linq;
using System.Text;
using UnityEngine;

// Comprobaciones en Play de la plaza (aldeanos charlando), la misión del castillo, el interior y el minimapa.
public static class TownChecks
{
    public static string ChatState()
    {
        var all = Object.FindObjectsByType<VillagerNPC>().Where(v => v.mood == VillagerMood.Chat).ToList();
        var sb = new StringBuilder();
        sb.Append("charlatanes ").Append(all.Count).Append(", charlando ").Append(all.Count(v => v.Chatting));
        if (WorldHUD.Instance != null) sb.Append(", globos ").Append(WorldHUD.Instance.ActiveAmbientBubbles);
        foreach (var v in all.Where(v => v.Chatting && v.Partner != null))
            sb.Append(" | ").Append(v.villagerName).Append("↔").Append(v.Partner.villagerName).Append(" L").Append(v.ChatLine)
              .Append(" ").Append(Vector3.Distance(v.transform.position, v.Partner.transform.position).ToString("0.0")).Append("m");
        bool fire = GameObject.Find("Fogata_Plaza") != null || GameObject.Find("Bancos") != null;
        sb.Append(fire ? " | ¡queda la fogata/bancos!" : " | plaza sin fogata");
        return sb.ToString();
    }

    public static string PlazaShot()
    {
        Vector3 c = new Vector3(-7f, 0f, -7f);
        return FrostboundBridge.CamShot("plaza", c + new Vector3(0f, 11f, -13f), c + Vector3.up, 50f);
    }

    private static VillageLayout.Result Layout => Object.FindAnyObjectByType<VillageWorldGenerator>()?.Layout;
    private static Transform Player => GameObject.Find("Player")?.transform;

    private static void Teleport(Vector3 p, float yaw = 0f)
    {
        PlayerPersistence mover = Object.FindAnyObjectByType<PlayerPersistence>();
        if (mover != null) mover.MoveTo(p, Quaternion.Euler(0f, yaw, 0f));
    }

    public static string CastleShots()
    {
        var L = Layout;
        if (L == null) return "sin layout";
        var c = L.castle;
        Vector3 front = c.ToWorld(new Vector3(0f, 0f, c.GateZ + 38f)) + Vector3.up * 26f;
        string a = FrostboundBridge.CamShot("castle_front", front, c.ToWorld(new Vector3(0f, 16f, c.FrontZ - 10f)), 55f);
        Vector3 side = c.ToWorld(new Vector3(62f, 34f, c.FrontZ - 6f));
        FrostboundBridge.CamShot("castle_side", side, c.ToWorld(new Vector3(0f, 14f, c.TownCenterZ - 20f)), 55f);
        Vector3 top = c.TownCenter + Vector3.up * 60f + (c.Gate - c.TownCenter).normalized * 30f;
        FrostboundBridge.CamShot("town_top", top, c.TownCenter, 60f);
        return a + " (pie " + c.Foot.ToString("0") + ", puerta " + c.Gate.ToString("0") + ", ruinas " + L.town.ruins.Count + ")";
    }

    public static string QuestState()
    {
        var sb = new StringBuilder("etapa " + QuestLog.Stage(QuestLog.Citadel));
        var siege = CitadelSiege.Instance;
        if (siege != null) sb.Append(", asedio " + siege.Current + " oleada " + siege.Wave + " quedan " + siege.Remaining);
        var boss = BossController.Active;
        if (boss != null) sb.Append(", jefe " + boss.title + " " + Mathf.CeilToInt(boss.Health.Health) + "/" + Mathf.CeilToInt(boss.Health.maxHealth) + (boss.Speaking ? " (hablando)" : ""));
        var entrance = Object.FindAnyObjectByType<CastleEntrance>();
        if (entrance != null) sb.Append(", puerta " + (entrance.Sealed ? "sellada" : "abierta"));
        var steve = GameObject.Find(MapSystem.SteveName);
        sb.Append(", steve " + (steve != null && steve.GetComponent<QuestGiver>() != null ? "con misión" : "sin misión"));
        return sb.ToString();
    }

    public static string TalkToSteve()
    {
        var steve = GameObject.Find(MapSystem.SteveName);
        if (steve == null) return "sin Steve";
        Teleport(steve.transform.position + steve.transform.forward * 2f, steve.transform.eulerAngles.y + 180f);
        steve.GetComponent<NPCInteractable>().Choose(NPCOption.Hablar);
        return "hablando";
    }

    public static string SkipSteve()
    {
        var steve = GameObject.Find(MapSystem.SteveName);
        steve.GetComponent<QuestGiver>().SkipIntro();
        return QuestState();
    }

    public static string GoToTown()
    {
        var c = Layout.castle;
        Teleport(c.ToWorld(new Vector3(0f, 0f, c.GateZ + 3f)), c.yaw + 180f);
        return "en la puerta de la ciudadela " + Player.position.ToString("0");
    }

    public static string GoToSquare()
    {
        var c = Layout.castle;
        Teleport(c.TownCenter + (c.Gate - c.TownCenter).normalized * 4f, c.yaw + 180f);
        return QuestState();
    }

    public static string Invulnerable()
    {
        var s = Player.GetComponent<CharacterStats>();
        s.invulnerable = !s.invulnerable;
        return "invulnerable " + s.invulnerable;
    }

    public static string KillWave()
    {
        CitadelSiege.Instance?.KillWave();
        return QuestState();
    }

    public static string HurtBoss()
    {
        var boss = BossController.Active;
        if (boss == null) return "sin jefe";
        boss.Health.TakeHit(new DamageInfo { amount = boss.Health.maxHealth * 0.97f, source = Player.gameObject, direction = Vector3.forward });
        return QuestState();
    }

    public static string GoToCastleDoor()
    {
        var c = Layout.castle;
        Teleport(c.ToWorld(c.DoorLocal + new Vector3(0f, 0f, 4f)), c.yaw + 180f);
        return "frente a la puerta " + Player.position.ToString("0");
    }

    public static string WalkIntoDoor()
    {
        var c = Layout.castle;
        Teleport(c.ToWorld(c.DoorLocal + new Vector3(0f, 0f, 0.9f)), c.yaw + 180f);
        return QuestState();
    }

    public static string InteriorState()
    {
        var i = CastleInterior.Instance;
        if (i == null) return "sin interior";
        if (!i.Built) return "sin construir";
        var L = i.Layout;
        var sb = new StringBuilder("salas " + L.rooms.Count + ", dentro " + i.PlayerInside + ", conectado " + CastleInteriorLayout.AllRoomsReachable(L)
            + ", salas activas " + i.RoomsSpawned + ", enemigos vivos " + i.AliveEnemies + " |");
        foreach (var r in L.rooms) sb.Append(" " + CastleInteriorLayout.ThemeName(r.theme) + "(" + r.troops + "+" + r.elites + ")");
        var nav = UnityEngine.AI.NavMesh.SamplePosition(i.PortalPoint, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas);
        sb.Append(" | navmesh en la entrada " + nav);
        return sb.ToString();
    }

    public static string InteriorShots()
    {
        var i = CastleInterior.Instance;
        if (i == null || !i.Built) return "sin interior";
        Vector3 c = CastleInterior.Origin + new Vector3(CastleInterior.Extent * 0.5f, 0f, CastleInterior.Extent * 0.5f);
        FrostboundBridge.CamShot("interior_top", c + Vector3.up * 95f + Vector3.back * 25f, c, 55f);
        Vector3 t = i.ThroneCenter;
        return FrostboundBridge.CamShot("interior_throne", t + new Vector3(0f, 16f, -16f), t, 55f);
    }

    public static string GoToThrone()
    {
        var i = CastleInterior.Instance;
        Teleport(i.ThroneCenter + Vector3.back * 4f);
        return "en el trono";
    }

    public static string GoToRoom2()
    {
        var i = CastleInterior.Instance;
        var room = i.Layout.rooms.Find(r => r.theme != CastleInteriorLayout.Theme.Vestibule && r.theme != CastleInteriorLayout.Theme.Throne);
        Teleport(CastleInterior.CellToWorld(room.Center.x, room.Center.y) + Vector3.back * 3f);
        return CastleInteriorLayout.ThemeName(room.theme);
    }

    public static string MapState()
    {
        var m = MapSystem.Instance;
        if (m == null) return "sin mapa";
        var a = m.CurrentArea;
        return "zona " + a.title + ", foto " + (a.map != null ? a.map.width + "px" : "no") + ", explorado " + a.ExploredPercent.ToString("0.0") + "%, marcadores " + m.markers.Count
            + ", objetivo " + (m.ObjectivePosition()?.ToString("0") ?? "ninguno") + ", hud " + (AdventureHUD.Instance != null);
    }

    public static string ToggleMap()
    {
        AdventureHUD.Instance.Map.Toggle();
        return "mapa completo " + AdventureHUD.Instance.Map.FullOpen;
    }

    public static string SaveMapTexture()
    {
        var a = MapSystem.Instance.CurrentArea;
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../FrostboundBridge/map_" + a.id + ".png"), a.map.EncodeToPNG());
        return "ok";
    }

    public static string BreakWeapon()
    {
        var eq = Player.GetComponent<Equipment>();
        var w = eq.MainWeapon;
        if (w == null) return "sin arma";
        string name = w.item.displayName;
        int before = eq.Inventory.UsedSlots;
        eq.WearWeapon(w.durability + 1f);
        return name + " rota · equipada " + (eq.MainWeapon != null ? eq.MainWeapon.item.displayName : "nada")
            + " · mochila " + before + "→" + eq.Inventory.UsedSlots + " · daño arma " + Player.GetComponent<CharacterStats>().WeaponDamage.ToString("0.#");
    }

    public static string WeaponInfo()
    {
        var eq = Player.GetComponent<Equipment>();
        var w = eq.MainWeapon;
        if (w == null) return "sin arma";
        return w.item.displayName + " nivel " + w.itemLevel + " daño " + w.BaseDamage.ToString("0.#") + " (base " + w.item.damage + ") dur " + w.durability + "/" + w.item.maxDurability;
    }

    public static string Bodies()
    {
        var sb = new StringBuilder();
        foreach (var go in Object.FindObjectsByType<Transform>())
            if (go.name.EndsWith("(cuerpo)")) sb.Append(go.name).Append(" en ").Append(go.position.ToString("0")).Append("; ");
        return sb.Length > 0 ? sb.ToString() : "ningún cuerpo";
    }
}
