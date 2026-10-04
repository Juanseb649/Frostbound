using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Simulación del botín (spec §5.9): frecuencias por calidad y determinismo por seed.
public static class LootSimulation
{
    [MenuItem("Tools/Frostbound/Debug/Simular botín (150 y 100.000 muertes)")]
    public static void Menu() => Debug.Log("[Botín] " + Run());

    public static string Run()
    {
        ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/Data/Items/ItemDatabase.asset");
        if (db == null) return "Falta ItemDatabase";

        var sb = new StringBuilder();
        sb.Append("Sesión Foothills (150 muertes, Nv2): ").Append(Summary(db, 150, 2, 7)).Append(" | ");
        sb.Append("100.000 muertes Nv3: ").Append(Summary(db, 100000, 3, 11)).Append(" | ");

        string a = Fingerprint(db, 42), b = Fingerprint(db, 42), c = Fingerprint(db, 43);
        sb.Append(a == b && a != c ? "Determinismo OK" : "Determinismo FALLA");
        return sb.ToString();
    }

    private static string Summary(ItemDatabase db, int kills, int level, int seed)
    {
        var counts = new Dictionary<string, int>();
        int total = 0;
        for (int k = 0; k < kills; k++)
        {
            var rng = new DeterministicRng(SeedUtil.Combine(seed, SeedUtil.FromString("loot"), k));
            foreach (ItemStack s in LootRoller.Roll(db, level, 1, false, 0f, rng))
            {
                total++;
                string key = s.item.IsRune ? "Runa" : s.item.IsConsumable ? "Poción" : s.quality == LootQuality.None ? "Otro" : LootQualityInfo.Name(s.quality);
                counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
            }
        }
        var sb = new StringBuilder(total + " objetos (");
        foreach (var p in counts) sb.Append(p.Key).Append(" ").Append(p.Value).Append(", ");
        return sb.ToString().TrimEnd(',', ' ') + ")";
    }

    private static string Fingerprint(ItemDatabase db, int seed)
    {
        var sb = new StringBuilder();
        for (int k = 0; k < 200; k++)
        {
            var rng = new DeterministicRng(SeedUtil.Combine(seed, k));
            foreach (ItemStack s in LootRoller.Roll(db, 3, 1, true, 0f, rng))
            {
                sb.Append(s.item.id).Append(s.quality);
                foreach (RolledAffix af in s.affixes) sb.Append(af.id).Append(af.value.ToString("0.00"));
            }
        }
        return sb.ToString();
    }
}
