using System.Text;
using UnityEngine;

// Comprobaciones de los enemigos en Play (se llaman desde el bridge con exec).
public static class EnemyChecks
{
    // Mata a los enemigos a menos de 12 m del jugador para probar muerte, XP y botín.
    public static string KillNearby()
    {
        GameObject p = GameObject.Find("Player");
        if (p == null) return "no hay Player";
        CharacterStats stats = p.GetComponent<CharacterStats>();
        int xpBefore = stats.experience + stats.level * 100000;
        int killed = 0;
        foreach (EnemyBrain e in Object.FindObjectsByType<EnemyBrain>())
        {
            if (e.IsDead || Vector3.Distance(e.transform.position, p.transform.position) > 12f) continue;
            Damageable d = e.GetComponent<Damageable>();
            d.TakeHit(new DamageInfo { amount = d.maxHealth * 10f, type = DamageType.Physical, source = p, direction = e.transform.position - p.transform.position });
            killed++;
        }
        int items = Object.FindObjectsByType<WorldItem>().Length;
        return "muertos " + killed + " · XP ganada " + ((stats.experience + stats.level * 100000) - xpBefore) + " · objetos en el suelo " + items;
    }

    // Enemigos vivos dentro de una zona segura (debe ser 0) y distancia mínima a Steve.
    public static string SafeZoneReport()
    {
        int inside = 0, alive = 0;
        float nearSteve = float.MaxValue;
        Transform steve = null;
        foreach (NameTag t in Object.FindObjectsByType<NameTag>()) if (t.displayName == "Steve") steve = t.transform;
        var states = new StringBuilder();
        foreach (EnemyBrain e in Object.FindObjectsByType<EnemyBrain>())
        {
            if (e.IsDead) continue;
            alive++;
            if (SafeZone.Contains(e.transform.position)) inside++;
            if (steve != null) nearSteve = Mathf.Min(nearSteve, Vector3.Distance(e.transform.position, steve.position));
            states.Append(e.Current.ToString()[0]);
        }
        return "vivos " + alive + " · dentro de zona segura " + inside + " · más cercano a Steve " + (steve != null ? nearSteve.ToString("0.0") + " m" : "sin Steve") + " · estados " + states;
    }

    public static string PlayerHealth()
    {
        GameObject p = GameObject.Find("Player");
        CharacterStats s = p != null ? p.GetComponent<CharacterStats>() : null;
        return s == null ? "?" : "vida " + s.currentHealth.ToString("0") + "/" + s.MaxHealth.ToString("0") + " · nivel " + s.level + " · pos " + p.transform.position.ToString("0");
    }

    public static string FrostRuneOn() => SetFrostRune(true);
    public static string FrostRuneOff() => SetFrostRune(false);

    private static string SetFrostRune(bool on)
    {
        GameObject p = GameObject.Find("Player");
        Equipment eq = p != null ? p.GetComponent<Equipment>() : null;
        ItemStack w = eq != null ? eq.MainWeapon : null;
        if (w == null) return "sin arma";
        w.runes.RemoveAll(r => r != null && r.runeEffect == RuneEffect.Frost);
        if (on && LootDirector.Instance != null)
            foreach (ItemDefinition it in LootDirector.Instance.database.items)
                if (it != null && it.runeEffect == RuneEffect.Frost) { w.runes.Add(it); break; }
        return w.item.displayName + " runas: " + w.runes.Count + (on ? " (con hielo)" : " (sin hielo)");
    }

    public static string LootStatus()
    {
        LootDirector d = LootDirector.Instance;
        if (d == null) return "sin LootDirector (" + Object.FindObjectsByType<LootDirector>().Length + " en escena)";
        return "LootDirector OK · objetos en base " + (d.database != null ? d.database.items.Count : -1) + " · MF " + d.PlayerMagicFind;
    }

    public static string DropTest()
    {
        GameObject p = GameObject.Find("Player");
        LootDirector d = LootDirector.Instance;
        if (p == null || d == null) return "falta Player o LootDirector";
        var rng = new DeterministicRng(777);
        var list = LootRoller.Roll(d.database, 3, 12, true, 0f, rng, true);
        var sb = new StringBuilder(list.Count + " objetos: ");
        for (int i = 0; i < list.Count; i++)
        {
            float a = i * 0.6f;
            WorldItem.Spawn(list[i], p.transform.position + new Vector3(Mathf.Sin(a) * 2.5f, 1.2f, Mathf.Cos(a) * 2.5f), Vector3.up * 3f);
            sb.Append(list[i].DisplayName).Append(" [").Append(LootQualityInfo.Name(list[i].quality)).Append("]; ");
        }
        return sb.ToString();
    }

    public static string KillPlayer()
    {
        GameObject p = GameObject.Find("Player");
        CharacterStats s = p != null ? p.GetComponent<CharacterStats>() : null;
        if (s == null) return "?";
        s.invulnerable = false;
        EnemyBrain.DamagePlayer(s, 99999f);
        return "vida " + s.currentHealth.ToString("0");
    }

    public static string DroppedLoot()
    {
        var sb = new StringBuilder();
        foreach (WorldItem w in Object.FindObjectsByType<WorldItem>()) sb.Append(w.Label).Append(" [").Append(w.stack.quality).Append("]; ");
        return sb.Length == 0 ? "nada" : sb.ToString();
    }
    // Jerarquía de huesos del pingüino con posiciones locales (para ajustar el ragdoll).
    public static string RigHierarchy()
    {
        GameObject pre = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy_Corrupt_Melee.prefab");
        if (pre == null) return "sin prefab";
        var sb = new StringBuilder();
        Dump(pre.transform, 0, sb);
        return sb.ToString();
    }

    private static void Dump(Transform t, int d, StringBuilder sb)
    {
        if (t.GetComponent<Renderer>() == null || d < 2)
            sb.Append(new string('.', d)).Append(t.name).Append(' ').Append(t.localPosition.ToString("0.000")).Append(" s").Append(t.lossyScale.x.ToString("0.00")).Append(" | ");
        foreach (Transform c in t) Dump(c, d + 1, sb);
    }
}
