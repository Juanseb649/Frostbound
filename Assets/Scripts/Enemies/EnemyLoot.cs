using UnityEngine;

// Suelta el botín de un enemigo muerto desde el centro del estallido de hielo (spec §5.7).
public static class EnemyLoot
{
    private static int _kills;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetKills() => _kills = 0;

    public static void Drop(EnemyBrain enemy, Vector3 center)
    {
        LootDirector dir = LootDirector.Instance;
        if (dir == null || dir.database == null || enemy.definition == null) return;

        int world = GameSession.Instance != null ? GameSession.Instance.worldSeed : 12345;
        var rng = new DeterministicRng(SeedUtil.Combine(world, SeedUtil.FromString("loot"), ++_kills));
        bool elite = enemy.definition.elite || enemy.Champion;
        int picks = enemy.definition.lootPicks * (enemy.Champion ? 2 : 1);
        var drops = LootRoller.Roll(dir.database, enemy.Level, picks, elite, dir.PlayerMagicFind, rng, enemy.Champion);

        // Monedas: caen mucho más a menudo que las armas, más con las élites, los campeones y los jefes.
        bool boss = enemy.GetComponent<BossController>() != null;
        float chance = boss ? 1f : elite ? 0.95f : 0.8f;
        if (rng.Chance(chance))
        {
            int lvl = Mathf.Max(1, enemy.Level);
            int amount = elite ? rng.NextInt(8, 21) + 3 * lvl : rng.NextInt(2, 7) + lvl;
            if (enemy.Champion) amount *= 2;
            if (boss) amount *= 6;
            int piles = boss ? 4 : 1;
            for (int p = 0; p < piles; p++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Vector3 v = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Random.Range(0.8f, 1.8f) + Vector3.up * Random.Range(3f, 4f);
                CoinPile.Spawn(Mathf.Max(1, amount / piles), center + Vector3.up * 0.3f, v);
            }
        }

        for (int i = 0; i < drops.Count; i++)
        {
            float a = (i / (float)Mathf.Max(1, drops.Count)) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
            Vector3 v = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Random.Range(1.2f, 2.2f) + Vector3.up * Random.Range(3.5f, 4.5f);
            WorldItem.Spawn(drops[i], center + Vector3.up * 0.2f, v);
            if (LootQualityInfo.HasBeam(drops[i]))
                Notifications.Show("¡" + drops[i].DisplayName + "!", drops[i].DisplayColor);
        }
    }
}
