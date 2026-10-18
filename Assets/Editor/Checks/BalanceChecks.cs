using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

// Comprobaciones de equilibrio, muerte, tiendas y co-op. Se lanzan por el bridge (exec BalanceChecks.X).
public static class BalanceChecks
{
    private static GameObject Hero => GameObject.Find("Player");

    public static string EquipState()
    {
        GameObject p = Hero;
        if (p == null) return "sin héroe";
        var eq = p.GetComponent<Equipment>();
        var sb = new StringBuilder();
        foreach (EquipSlot s in Equipment.Slots)
        {
            ItemStack st = eq.GetStack(s);
            if (st != null) sb.Append(s).Append("=").Append(st.item.id).Append("(").Append(Mathf.RoundToInt(st.durability)).Append(") ");
        }
        int weapons = 0;
        for (int i = 0; i < eq.Inventory.Capacity; i++) { ItemStack st = eq.Inventory.Get(i); if (st != null && st.item.IsWeapon) weapons++; }
        var holder = p.GetComponentInChildren<WeaponHolder>();
        sb.Append("| armas_mochila=").Append(weapons);
        sb.Append(" modelo=").Append(holder != null && holder.MainModel != null ? holder.MainModel.name + (holder.MainModel.activeInHierarchy ? "" : "(oculto)") : "ninguno");
        sb.Append(" vida=").Append(Mathf.RoundToInt(p.GetComponent<CharacterStats>().currentHealth));
        var outfit = p.GetComponentInChildren<PenguinOutfit>();
        sb.Append(" ropa=").Append(outfit != null ? string.Join(",", outfit.EquippedItems.Select(o => o.name)) : "sin PenguinOutfit");
        var cs = p.GetComponent<CharacterStats>();
        sb.Append(" clase=").Append(cs.characterClass != null ? cs.characterClass.name : "-").Append(" nv").Append(cs.level);
        return sb.ToString();
    }

    public static string Kill()
    {
        var st = Hero.GetComponent<CharacterStats>();
        st.invulnerable = false;
        st.TakeDamage(1e6f);
        return "muerto=" + st.IsDead;
    }

    public static string Revive()
    {
        var pr = Hero.GetComponent<PlayerRespawn>();
        var m = typeof(PlayerRespawn).GetMethod("Respawn", BindingFlags.NonPublic | BindingFlags.Instance);
        pr.StartCoroutine((IEnumerator)m.Invoke(pr, null));
        return "ok";
    }

    public static string SavedEquipment()
    {
        var s = GameSession.Instance;
        if (s == null || s.Current == null) return "sin partida";
        s.SaveNow();
        return "guardado: " + string.Join(",", s.Current.equipment.Select(e => e.slot + "=" + e.id));
    }

    public static string LootStats()
    {
        var db = GameDatabase.Instance != null ? GameDatabase.Instance.items : null;
        if (db == null) return "sin db";
        var sb = new StringBuilder();
        foreach (bool elite in new[] { false, true })
        {
            int kills = 2000, potions = 0, runes = 0, weapons = 0, armor = 0, jewels = 0, scrolls = 0;
            var rng = new DeterministicRng(777);
            for (int k = 0; k < kills; k++)
                foreach (ItemStack s in LootRoller.Roll(db, 5, elite ? 2 : 1, elite, 0f, rng))
                {
                    if (s.item.IsRune) runes++;
                    else if (s.item.townPortal) scrolls++;
                    else if (s.item.IsConsumable) potions++;
                    else if (s.item.IsWeapon) weapons++;
                    else if (s.item.equipSlot == EquipSlot.Amulet) jewels++;
                    else if (s.item.IsEquippable) armor++;
                }
            sb.Append(elite ? " ÉLITE" : "TROPA").Append(" por 100 muertes: pociones=").Append(potions / 20f).Append(" runas=").Append(runes / 20f)
              .Append(" pergaminos=").Append(scrolls / 20f).Append(" armas=").Append(weapons / 20f).Append(" armaduras=").Append(armor / 20f).Append(" joyas=").Append(jewels / 20f);
        }
        return sb.ToString();
    }

    public static string SpeedTable()
    {
        var p = Hero;
        if (p == null) return "sin héroe";
        var st = p.GetComponent<CharacterStats>();
        var sb = new StringBuilder("clase=" + st.characterClass.className + " agi=" + st.Agility + " vel=" + st.MoveSpeed.ToString("0.00"));
        StatBlock old = st.allocated;
        foreach (int extra in new[] { 10, 25, 50, 100 })
        {
            StatBlock a = old;
            a.agility = old.agility + extra;
            st.allocated = a;
            sb.Append(" | +" + extra + "→" + st.MoveSpeed.ToString("0.00"));
        }
        st.allocated = old;
        return sb.ToString();
    }

    public static string GiveAgility()
    {
        var st = Hero.GetComponent<CharacterStats>();
        StatBlock a = st.allocated;
        a.agility += 60;
        st.allocated = a;
        return "agi=" + st.Agility + " vel=" + st.MoveSpeed.ToString("0.00");
    }

    private static float _maxUp;
    public static string RunFast()
    {
        var p = Hero;
        _maxUp = 0f;
        var pc = p.GetComponent<PlayerController>();
        pc.StartCoroutine(Track(p.GetComponent<Rigidbody>()));
        typeof(PlayerController).GetField("_clickTarget", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pc, (Vector3?)(p.transform.position + p.transform.forward * 25f));
        return "corriendo desde " + p.transform.position.ToString("F1");
    }

    private static IEnumerator Track(Rigidbody rb)
    {
        float until = Time.time + 4f;
        while (Time.time < until && rb != null) { _maxUp = Mathf.Max(_maxUp, rb.linearVelocity.y); yield return new WaitForFixedUpdate(); }
    }

    public static string RunReport()
    {
        var p = Hero;
        return "pos=" + p.transform.position.ToString("F1") + " vy_max=" + _maxUp.ToString("0.00");
    }

    public static string GiveWeapon()
    {
        var eq = Hero.GetComponent<Equipment>();
        var db = GameDatabase.Instance.items;
        var rng = new DeterministicRng(4242);
        ItemStack s = null;
        for (int i = 0; i < 400 && s == null; i++)
            foreach (ItemStack r in LootRoller.Roll(db, 3, 1, true, 0f, rng, true))
                if (r.item.IsWeapon && r.item.CanBeEquippedBy(eq.Stats, out _)) { s = r; break; }
        if (s == null) return "sin arma";
        eq.Inventory.AddStack(s);
        for (int i = 0; i < eq.Inventory.Capacity; i++)
            if (eq.Inventory.Get(i) == s) { eq.EquipFromInventory(i, out string msg); return "equipada " + s.DisplayName + " " + msg; }
        return "no está en la mochila";
    }

    private static int _slot;

    public static string ToTitle()
    {
        _slot = GameSession.Instance != null ? GameSession.Instance.CurrentSlot : 0;
        var pr = Hero.GetComponent<PlayerRespawn>();
        typeof(PlayerRespawn).GetMethod("ReturnToTitle", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pr, null);
        return "ok";
    }

    public static string ContinueFirst()
    {
        var menu = Object.FindAnyObjectByType<FrostMenu>();
        if (menu == null) return "sin menú";
        menu.LoadSlot(_slot);
        return "ok";
    }

    public static string UseScroll()
    {
        var eq = Hero.GetComponent<Equipment>();
        var scroll = GameDatabase.Instance.items.Find("town_scroll");
        eq.Inventory.Add(scroll, 2);
        for (int i = 0; i < eq.Inventory.Capacity; i++)
        {
            ItemStack s = eq.Inventory.Get(i);
            if (s != null && s.item == scroll) { bool ok = eq.Use(i, out string msg); return "usar=" + ok + " '" + msg + "' quedan=" + eq.Inventory.Count(scroll); }
        }
        return "sin pergamino";
    }

    public static string RepairTest()
    {
        var eq = Hero.GetComponent<Equipment>();
        var w = eq.MainWeapon;
        if (w == null) return "sin arma";
        w.EnsureInstance();
        w.durability = w.item.maxDurability * 0.4f;
        var wallet = Wallet.Of(eq.gameObject);
        wallet.Set(5);
        var smith = GameObject.Find("NPC_Herrera_Brunna").GetComponent<NPCInteractable>();
        string label = smith.LabelFor(NPCOption.Reparar);
        int cost = eq.RepairCostAll();
        smith.Choose(NPCOption.Reparar);
        string poor = "dur=" + Mathf.RoundToInt(w.durability) + " monedas=" + wallet.Coins;
        wallet.Set(500);
        smith.Choose(NPCOption.Reparar);
        return label + " coste=" + cost + " | sin dinero: " + poor + " | con dinero: dur=" + Mathf.RoundToInt(w.durability) + " monedas=" + wallet.Coins;
    }

    public static string ShopStock()
    {
        var sb = new StringBuilder();
        foreach (string npc in new[] { "NPC_Anciano_Pingo", "NPC_Mercader_Tico" })
        {
            var n = GameObject.Find(npc).GetComponent<NPCInteractable>();
            TradeWindow.OpenShop(n, false);
            var stock = (System.Collections.Generic.List<ItemStack>)typeof(TradeWindow).GetField("Stock", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            sb.Append(n.displayName).Append(" [").Append(string.Join(",", n.AvailableOptions())).Append("]: ")
              .Append(string.Join(", ", stock.Select(x => x.item.displayName + " " + ItemPricing.BuyPrice(x)))).Append(" | ");
            UICancelClose();
        }
        return sb.ToString();
    }

    private static void UICancelClose()
    {
        var w = Object.FindAnyObjectByType<TradeWindow>();
        if (w != null) typeof(TradeWindow).GetMethod("Close", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(w, null);
    }

    public static string CampMix()
    {
        int cursed = 0, minions = 0;
        foreach (var b in Object.FindObjectsByType<EnemyBrain>())
        {
            if (b.definition == null) continue;
            if (b.definition.id == "Corrupt_Melee" || b.definition.id == "Corrupt_Ranged") minions++; else cursed++;
        }
        return "despiertas=" + EnemyCamp.CursedAwake + " esbirros=" + minions + " malditos=" + cursed;
    }

    public static string RespawnAllCamps()
    {
        foreach (var b in Object.FindObjectsByType<EnemyBrain>()) Object.Destroy(b.gameObject);
        foreach (var c in Object.FindObjectsByType<EnemyCamp>())
        {
            var list = (System.Collections.Generic.List<EnemyBrain>)typeof(EnemyCamp).GetField("_members", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(c);
            list.Clear();
        }
        return "ok";
    }

    public static string SpawnCampsNow()
    {
        foreach (var c in Object.FindObjectsByType<EnemyCamp>()) c.Spawn();
        return CampMix();
    }

    public static string WakeCursed()
    {
        QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelBarbarian);
        return "etapa=" + QuestLog.Stage(QuestLog.Citadel);
    }

    // Equilibrio de los jefes de la campaña frente a Rocco (exec BalanceChecks.BossTable, sin Play).
    // Vida en "pingüinos normales del mismo nivel"; golpes en % de la vida típica del héroe en su tramo
    // (40 de base + un tercio de sus puntos en Vida, a 8 por punto).
    public static string BossTable()
    {
        var db = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        EnemyDefinition melee = db.FindEnemy("Corrupt_Melee"), mage = db.FindEnemy("Corrupt_Mage"), ninja = db.FindEnemy("Corrupt_Ninja");
        float Hero(int lvl) => 40f + (12f + (lvl - 1) * 5f / 3f) * 8f;
        float Eq(float hp, int lvl) => hp / melee.HealthAt(lvl);
        string Pct(float dmg, int lvl) => Mathf.RoundToInt(dmg / Hero(lvl) * 100f) + "%";
        float Hit(EnemyDefinition d, int lvl, float mult) => (d.damage.x + d.damage.y) * 0.5f * d.DamageScaleAt(lvl) * mult;
        var sb = new StringBuilder();

        int l = BossTuning.RoccoLevel;
        float hp = mage.HealthAt(l) * BossTuning.RoccoHealth;
        sb.Append("Rocco nv").Append(l).Append(" vida=").Append(Mathf.RoundToInt(hp)).Append(" (").Append(Eq(hp, l).ToString("0")).Append(" normales) golpe=").Append(Pct(Hit(mage, l, BossTuning.RoccoDamage), l));

        l = BossTuning.SapLevel;
        hp = mage.HealthAt(l) * BossTuning.SapHealth;
        sb.Append(" | Madre Savia nv").Append(l).Append(" vida=").Append(Mathf.RoundToInt(hp)).Append(" (").Append(Eq(hp, l).ToString("0")).Append(") orbe=").Append(Pct(Hit(mage, l, BossTuning.SapDamage), l))
          .Append(" lluvia=").Append(Pct(BossTuning.SapPower(l), l)).Append(" raíces=").Append(Pct(BossTuning.SapPower(l) * 1.3f, l))
          .Append(" charco/s=").Append(Pct(BossTuning.SapPoison(l), l));

        l = BossTuning.BobbyLevel;
        hp = ninja.HealthAt(l) * BossTuning.BobbyHealth * 0.9f;
        sb.Append(" | Bobby nv").Append(l).Append(" vida hasta rendirse=").Append(Mathf.RoundToInt(hp)).Append(" (").Append(Eq(hp, l).ToString("0")).Append(") ataque doble=").Append(Pct(Hit(ninja, l, BossTuning.BobbyDamage) * 2f, l))
          .Append(" lanza=").Append(Pct(BossTuning.BobbyLance(l), l));

        l = BossTuning.HeartLevel;
        hp = mage.HealthAt(l) * BossTuning.HeartHealth;
        float feeders = 3f * BossTuning.HeartFeeder(l);
        sb.Append(" | Corazón nv").Append(l).Append(" vida=").Append(Mathf.RoundToInt(hp)).Append("+cristales ").Append(Mathf.RoundToInt(feeders))
          .Append(" (").Append(Eq(hp + feeders, l).ToString("0")).Append(") esquirla=").Append(Pct(Hit(mage, l, BossTuning.HeartDamage), l))
          .Append(" anillo=").Append(Pct(BossTuning.HeartRing(l), l));
        sb.Append(" | héroe típico: nv9=").Append(Mathf.RoundToInt(Hero(9))).Append(" nv12=").Append(Mathf.RoundToInt(Hero(12))).Append(" nv18=").Append(Mathf.RoundToInt(Hero(18)));
        return sb.ToString();
    }

    // ---------- Presión de un jefe en juego: el héroe quieto (el peor caso) recibe golpes N segundos ----------
    private static float _probeHp, _probeAt;

    public static string ProbeStart()
    {
        CharacterStats st = Players.Primary != null ? Players.Primary.Stats : null;
        if (st == null) return "sin héroe";
        st.invulnerable = false;
        st.baseMaxHealth += 100000f;
        st.currentHealth = st.MaxHealth;
        _probeHp = st.currentHealth;
        _probeAt = Time.time;
        return "sonda en marcha (armadura " + Mathf.RoundToInt(st.DamageReduction * 100f) + "%)";
    }

    public static string ProbeResult()
    {
        CharacterStats st = Players.Primary != null ? Players.Primary.Stats : null;
        if (st == null) return "sin héroe";
        float lost = _probeHp - st.currentHealth, secs = Time.time - _probeAt;
        return "daño=" + Mathf.RoundToInt(lost) + " en " + secs.ToString("0") + " s → " + (lost / Mathf.Max(1f, secs)).ToString("0.0") + "/s";
    }

    // Escalinata: el héroe frente a Rocco.
    public static string GoRocco()
    {
        GameObject p = Players.PrimaryObject;
        Vector3 at = StairsLayout.ArenaCenter + new Vector3(0f, 0.2f, -5f);
        Rigidbody rb = p.GetComponent<Rigidbody>();
        if (rb != null) rb.position = at;
        p.transform.position = at;
        return "frente a Rocco";
    }
}
