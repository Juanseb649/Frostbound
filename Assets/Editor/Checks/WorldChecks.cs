using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Comprobaciones en Play del exterior procedural, las hogueras y las partidas guardadas (ranura 4 = pruebas).
public static class WorldChecks
{
    private const int TestSlot = 3;
    private static string _fingerprint;
    private static int _seed;

    public static string NewGameInTestSlot()
    {
        var select = Object.FindAnyObjectByType<ClassSelectController>();
        if (select == null) return "sin selección de clase";
        select.StartAdventure();
        return ClickSlot();
    }

    public static string ContinueTestSlot()
    {
        var menu = Object.FindAnyObjectByType<MainMenuController>();
        if (menu == null) return "sin menú";
        if (!menu.continueButton.interactable) return "Continuar deshabilitado";
        menu.continueButton.onClick.Invoke();
        return ClickSlot();
    }

    public static string OpenLoadPanel()
    {
        var menu = Object.FindAnyObjectByType<MainMenuController>();
        menu.continueButton.onClick.Invoke();
        return Object.FindAnyObjectByType<SaveSlotsPanel>() != null ? "abierto" : "no abrió";
    }

    public static string AskDeleteTestSlot()
    {
        var panel = Object.FindAnyObjectByType<SaveSlotsPanel>();
        Button del = panel.GetComponentsInChildren<Button>().Where(b => b.name == "Borrar").LastOrDefault();
        if (del == null) return "sin botón borrar";
        del.onClick.Invoke();
        return "pidiendo confirmación";
    }

    private static string ClickSlot()
    {
        var panel = Object.FindAnyObjectByType<SaveSlotsPanel>();
        if (panel == null) return "no se abrió el panel de ranuras";
        Button slot = panel.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "Ranura_" + (TestSlot + 1));
        if (slot == null) return "sin ranura";
        bool occupied = SaveSystem.Exists(TestSlot);
        slot.onClick.Invoke();
        if (occupied && Object.FindAnyObjectByType<SaveSlotsPanel>() != null)
        {
            panel = Object.FindAnyObjectByType<SaveSlotsPanel>();
            slot = panel.GetComponentsInChildren<Button>().First(b => b.name == "Ranura_" + (TestSlot + 1));
            slot.onClick.Invoke();
        }
        return "ranura " + (TestSlot + 1) + (occupied ? " (sobrescrita)" : "");
    }

    public static string WorldState()
    {
        var gen = Object.FindAnyObjectByType<VillageWorldGenerator>();
        var player = GameObject.Find("Player");
        var session = GameSession.Instance;
        if (gen == null || gen.Layout == null) return "sin generador";
        var sb = new StringBuilder();
        sb.Append("seed ").Append(gen.Seed).Append(" (sesión ").Append(session != null ? session.worldSeed : 0).Append(")");
        sb.Append(" · pinos ").Append(gen.Layout.pines.Count).Append(" · nieve ").Append(gen.Layout.mounds.Count);
        sb.Append(" · hogueras ");
        foreach (Bonfire b in Bonfire.Instances.OrderBy(b => b.id)) sb.Append(b.id).Append(b.IsLit ? "*" : "").Append(' ');
        Bonfire castle = Bonfire.Find(Bonfire.CastleId);
        if (player != null) sb.Append("· héroe en ").Append(player.transform.position.ToString("F0"));
        sb.Append(" · NavMesh ").Append(NavMesh.CalculateTriangulation().indices.Length / 3).Append(" tri");
        sb.Append(" · ranura ").Append(session != null ? session.CurrentSlot + 1 : 0);
        return sb.ToString();
    }

    public static string Remember()
    {
        var gen = Object.FindAnyObjectByType<VillageWorldGenerator>();
        _seed = gen.Seed;
        _fingerprint = Fingerprint(gen);
        return "seed " + _seed;
    }

    public static string CompareWithRemembered()
    {
        var gen = Object.FindAnyObjectByType<VillageWorldGenerator>();
        return gen.Seed == _seed && Fingerprint(gen) == _fingerprint ? "IGUAL (seed " + gen.Seed + ")" : "DISTINTO: " + _seed + " → " + gen.Seed;
    }

    private static string Fingerprint(VillageWorldGenerator gen)
    {
        var l = gen.Layout;
        return l.castleBonfire.position.ToString("F2") + string.Join(",", l.bonfires.Select(b => b.position.ToString("F2")))
            + string.Join(",", l.camps.Select(c => c.ToString("F2"))) + string.Join(",", l.pines.Select(p => p.position.ToString("F2")));
    }

    public static string GoToFirstBonfire() => GoTo("hoguera_1");

    private static string GoTo(string id)
    {
        Bonfire b = Bonfire.Find(id);
        var player = GameObject.Find("Player");
        if (b == null || player == null) return "sin hoguera " + id;
        player.GetComponent<CharacterStats>().invulnerable = true;
        player.GetComponent<PlayerPersistence>().MoveTo(b.transform.position + b.transform.forward * 2f, Quaternion.identity);
        return "en " + id + " (encendida antes: " + b.IsLit + ")";
    }

    public static string BonfireState()
    {
        var session = GameSession.Instance;
        bool banner = Object.FindAnyObjectByType<BonfireBanner>() != null;
        SaveData disk = SaveSystem.Load(TestSlot);
        return "aviso " + banner + " · encendidas en partida " + (session != null && session.Current != null ? string.Join(",", session.Current.litBonfires) : "-")
            + " · en disco " + (disk != null ? string.Join(",", disk.litBonfires) + " · última " + disk.lastBonfire : "-");
    }

    public static string WalkAwayAndDie()
    {
        var player = GameObject.Find("Player");
        Bonfire b = Bonfire.Find("hoguera_1");
        player.GetComponent<PlayerPersistence>().MoveTo(b.transform.position + b.transform.forward * 9f, Quaternion.identity);
        var stats = player.GetComponent<CharacterStats>();
        stats.invulnerable = false;
        stats.AddExperience(stats.ExperienceToNextLevel());
        stats.TakeDamage(stats.MaxHealth * 100f);
        return "nivel " + stats.level + " · vida " + stats.currentHealth;
    }

    public static string WhereAmI()
    {
        var player = GameObject.Find("Player");
        Bonfire nearest = Bonfire.Instances.OrderBy(b => Vector3.Distance(b.transform.position, player.transform.position)).First();
        var stats = player.GetComponent<CharacterStats>();
        return "junto a " + nearest.id + " (" + Vector3.Distance(nearest.transform.position, player.transform.position).ToString("F1") + " m) · nivel " + stats.level
            + " · vida " + Mathf.CeilToInt(stats.currentHealth) + "/" + Mathf.CeilToInt(stats.MaxHealth)
            + " · objetos " + player.GetComponent<Inventory>().UsedSlots + " · arma " + (player.GetComponent<Equipment>().MainWeapon != null ? player.GetComponent<Equipment>().MainWeapon.item.id : "-");
    }

    public static string CastleShot()
    {
        Bonfire b = Bonfire.Find(Bonfire.CastleId);
        if (b == null) return "sin hoguera";
        Vector3 c = b.transform.position;
        Vector3 outward = new Vector3(c.x, 0f, c.z).normalized;
        return FrostboundBridge.CamShot("w_castle", c + outward * 11f + Vector3.up * 9f, c, 45f);
    }

    public static string CastleShots()
    {
        var gen = Object.FindAnyObjectByType<VillageWorldGenerator>();
        VillageLayout.CastleDesign c = gen.Layout.castle;
        Vector3 facade = c.ToWorld(new Vector3(0f, c.plateauHeight + 12f, 0f));
        Vector3 front = c.ToWorld(new Vector3(-26f, 14f, c.FrontZ + 18f));
        FrostboundBridge.CamShot("c_front", front, facade, 55f);
        Vector3 side = c.ToWorld(new Vector3(-55f, 22f, -c.NaveLength * 0.5f));
        FrostboundBridge.CamShot("c_side", side, c.ToWorld(new Vector3(0f, 14f, -c.NaveLength * 0.5f)), 50f);
        Vector3 iso = c.ToWorld(new Vector3(30f, 45f, c.FrontZ + 30f));
        FrostboundBridge.CamShot("c_iso", iso, c.ToWorld(new Vector3(0f, 6f, 0f)), 60f);
        return "bahías " + c.bays + " · crucero " + c.transept + " · capillas " + c.chapels + " · torre " + c.centralTowerHeight.ToString("F0") + " m · a " + new Vector2(c.Foot.x, c.Foot.z).magnitude.ToString("F0") + " m del pueblo";
    }

    public static string WalkToCastle()
    {
        var gen = Object.FindAnyObjectByType<VillageWorldGenerator>();
        VillageLayout.CastleDesign c = gen.Layout.castle;
        var player = GameObject.Find("Player");
        player.GetComponent<CharacterStats>().invulnerable = true;
        player.GetComponent<PlayerPersistence>().MoveTo(c.Foot + Vector3.up * 0.1f, Quaternion.LookRotation(c.ToWorld(Vector3.zero) - c.Foot));
        player.GetComponent<PlayerController>().MoveTo(c.ToWorld(new Vector3(0f, c.plateauHeight, 4f)));
        return "caminando hacia la fachada";
    }

    public static string PlayerHeight()
    {
        var gen = Object.FindAnyObjectByType<VillageWorldGenerator>();
        var player = GameObject.Find("Player");
        Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, gen.Layout.castle.yaw, 0f)) * (player.transform.position - gen.Layout.castle.position);
        return "altura " + player.transform.position.y.ToString("F2") + " (meseta " + gen.Layout.castle.plateauHeight.ToString("F2") + ") · z local " + local.z.ToString("F1");
    }

    public static string MapShot()
    {
        var gen = Object.FindAnyObjectByType<VillageWorldGenerator>();
        foreach (Bonfire bf in Bonfire.Instances)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.transform.position = bf.transform.position + Vector3.up * 30f;
            marker.transform.localScale = new Vector3(6f, 30f, 6f);
            Object.Destroy(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().material.color = new Color(1f, 0.55f, 0.1f);
            Object.Destroy(marker, 2f);
        }
        var cam = new GameObject("MapCam").AddComponent<Camera>();
        Object.Destroy(cam.gameObject);
        return FrostboundBridge.CamShot("map", new Vector3(0f, 330f, -1f), Vector3.zero, 50f) + " | hogueras: "
            + string.Join(", ", Bonfire.Instances.Select(bf => bf.id + " a " + new Vector2(bf.transform.position.x, bf.transform.position.z).magnitude.ToString("F0") + " m"));
    }

    public static string DeleteTestSlot()
    {
        SaveSystem.Delete(TestSlot);
        return "ranura " + (TestSlot + 1) + " borrada: " + !SaveSystem.Exists(TestSlot);
    }
}
