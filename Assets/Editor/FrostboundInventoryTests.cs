using UnityEngine;

// Pasos de prueba del inventario y la progresión para el canal de comandos (FrostboundBridge "exec").
public static class FrostboundInventoryTests
{
    private static Equipment Player => Object.FindAnyObjectByType<Equipment>();
    private static InventoryScreen Screen => Object.FindAnyObjectByType<InventoryScreen>();

    public static string State()
    {
        Equipment eq = Player;
        if (eq == null) return "sin Equipment";
        CharacterStats s = eq.Stats;
        string gear = "";
        foreach (EquipSlot slot in Equipment.Slots)
        {
            ItemDefinition item = eq.Get(slot);
            if (item != null) gear += slot + "=" + item.id + " ";
        }
        return "nv " + s.level + " xp " + s.experience + "/" + s.ExperienceToNextLevel() + " pts " + s.pointsAvailable
            + " FUE " + s.Strength + " MAN " + s.Mana + " AGI " + s.Agility + " SAL " + s.Health
            + " vida " + s.currentHealth.ToString("0") + "/" + s.MaxHealth.ToString("0") + " arm " + s.Armor + " daño " + s.Damage.ToString("0.#")
            + " | " + gear + "| mochila " + eq.Inventory.UsedSlots + "/" + eq.Inventory.Capacity;
    }

    public static string LevelUp()
    {
        Equipment eq = Player;
        if (eq == null) return "sin Equipment";
        eq.Stats.AddExperience(eq.Stats.ExperienceToNextLevel() - eq.Stats.experience);
        return State();
    }

    public static string SpendStrength()
    {
        Equipment eq = Player;
        if (eq == null) return "sin Equipment";
        eq.Stats.AddPoints(StatType.Strength, 3);
        eq.Stats.AddPoints(StatType.Health, 2);
        return State();
    }

    public static string GiveLoot()
    {
        Equipment eq = Player;
        if (eq == null) return "sin Equipment";
        var db = Object.FindAnyObjectByType<ProgressionDebug>().database;
        foreach (string id in new[] { "ice_boots", "horned_helm", "plate_armor", "wizard_hat", "snow_amulet", "frost_shard" })
        {
            ItemDefinition item = db.Find(id);
            if (item != null) eq.Inventory.Add(item, item.IsStackable ? 5 : 1);
        }
        return State();
    }

    public static string EquipIceBoots()
    {
        Equipment eq = Player;
        if (eq == null) return "sin Equipment";
        for (int i = 0; i < eq.Inventory.Capacity; i++)
        {
            ItemStack s = eq.Inventory.Get(i);
            if (s != null && s.item.id == "ice_boots")
            {
                bool ok = eq.EquipFromInventory(i, out string msg);
                return (ok ? "equipado " : "no: " + msg + " ") + State();
            }
        }
        return "no hay botas de hielo";
    }

    public static string Open()
    {
        InventoryScreen screen = Screen;
        if (screen == null) return "sin InventoryScreen";
        screen.Open();
        return "abierto";
    }

    public static string Close()
    {
        InventoryScreen screen = Screen;
        if (screen == null) return "sin InventoryScreen";
        screen.Close();
        return "cerrado";
    }

    public static string BootsInfo()
    {
        Equipment eq = Player;
        if (eq == null) return "sin Equipment";
        string r = "";
        foreach (Transform t in eq.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Outfit_") && t.name.Contains("Botas"))
                r += t.name + " pos " + t.position.ToString("F2") + " escala " + t.lossyScale.ToString("F3") + "; ";
        foreach (SkinnedMeshRenderer smr in eq.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (smr.name.Contains("Foot")) r += smr.name + " " + smr.bounds.center.ToString("F2") + " " + smr.bounds.size.ToString("F3") + "; ";
        return r.Length > 0 ? r : "sin botas";
    }

    public static string TeleportToPickup()
    {
        Equipment eq = Player;
        ItemPickup pickup = Object.FindAnyObjectByType<ItemPickup>();
        if (eq == null || pickup == null) return "sin objetos en el suelo";
        Rigidbody rb = eq.GetComponent<Rigidbody>();
        Vector3 p = pickup.transform.position + Vector3.up * 0.05f;
        if (rb != null) rb.position = p;
        eq.transform.position = p;
        return "junto a " + pickup.item.displayName;
    }

    // Combina piezas de sets distintos: casco vikingo, túnica de mago, tabi ninja, hacha y escudo.
    public static string MixSets()
    {
        Equipment eq = Player;
        if (eq == null) return "sin Equipment";
        var db = Object.FindAnyObjectByType<ProgressionDebug>().database;
        eq.Stats.AddExperience(5000);
        foreach (string id in new[] { "viking_helm", "mage_robe", "ninja_boots", "viking_axe", "viking_shield" })
        {
            ItemDefinition item = db.Find(id);
            eq.Inventory.Add(item);
            for (int i = 0; i < eq.Inventory.Capacity; i++)
            {
                ItemStack s = eq.Inventory.Get(i);
                if (s != null && s.item == item) { eq.EquipFromInventory(i, out _); break; }
            }
        }
        return State();
    }

    // Cámara cercana al jugador para ver el equipo.
    public static string CloseUp()
    {
        Equipment eq = Player;
        if (eq == null) return "sin Equipment";
        Vector3 p = eq.transform.position;
        Vector3 f = eq.transform.forward;
        return FrostboundBridge.CamShot("inv_closeup", p + f * 2.2f + Vector3.up * 1.0f + eq.transform.right * 0.8f, p + Vector3.up * 0.45f, 40f);
    }
}
