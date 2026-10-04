using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class LootAndRngTests
{
    private ItemDatabase _db;

    [SetUp]
    public void SetUp() => _db = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/Data/Items/ItemDatabase.asset");

    [Test]
    public void LaMismaSeedDaLaMismaSecuencia()
    {
        var a = new DeterministicRng(1234);
        var b = new DeterministicRng(1234);
        for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextInt(0, 1000), b.NextInt(0, 1000));
    }

    [Test]
    public void CombinarSeedsEsEstableYDistingue()
    {
        Assert.AreEqual(SeedUtil.Combine(7, 3, 2), SeedUtil.Combine(7, 3, 2));
        Assert.AreNotEqual(SeedUtil.Combine(7, 3, 2), SeedUtil.Combine(7, 2, 3));
        Assert.AreEqual(SeedUtil.FromString("castillo_1"), SeedUtil.FromString("castillo_1"));
    }

    [Test]
    public void LaMismaSeedDaElMismoBotin()
    {
        List<string> RollAll(int seed)
        {
            var rng = new DeterministicRng(seed);
            var names = new List<string>();
            for (int k = 0; k < 300; k++)
                foreach (ItemStack s in LootRoller.Roll(_db, 3, 1, false, 0f, rng))
                    names.Add(s.item.id + "|" + s.quantity + "|" + s.quality + "|" + s.affixes.Count + "|" + s.rareName);
            return names;
        }
        var first = RollAll(42);
        CollectionAssert.AreEqual(first, RollAll(42));
        Assert.Greater(first.Count, 0);
        CollectionAssert.AreNotEqual(first, RollAll(43));
    }

    [Test]
    public void ElHallazgoMagicoRindeMenosCuantoMasTienes()
    {
        float low = LootRoller.EffectiveMF(LootQuality.Unique, 100f);
        float high = LootRoller.EffectiveMF(LootQuality.Unique, 1000f);
        Assert.Greater(high, low);
        Assert.Less(high, 1000f);
        Assert.AreEqual(500f, LootRoller.EffectiveMF(LootQuality.Magic, 500f), 0.001f);
    }

    [Test]
    public void LaTropaSueltaMagicosPeroPocosUnicos()
    {
        var rng = new DeterministicRng(99);
        int items = 0, magic = 0, top = 0;
        for (int k = 0; k < 20000; k++)
            foreach (ItemStack s in LootRoller.Roll(_db, 3, 1, false, 0f, rng))
            {
                if (s.item == null || !s.item.IsEquippable) continue;
                items++;
                if (s.quality == LootQuality.Magic) magic++;
                if (s.quality == LootQuality.Unique || s.quality == LootQuality.Set) top++;
            }
        Assert.Greater(items, 1000);
        Assert.That(magic / (float)items, Is.InRange(0.12f, 0.40f));
        Assert.Less(top / (float)items, 0.03f);
    }

    [Test]
    public void LosEnemigosEscalanConSuNivel()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:EnemyDefinition"))
        {
            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.AreEqual(def.maxHealth * 1.18f, def.HealthAt(2), 0.01f, def.name);
            Assert.AreEqual(1f, def.DamageScaleAt(1), 0.0001f, def.name);
            Assert.Greater(def.ExperienceAt(5), def.ExperienceAt(1), def.name);
            Assert.LessOrEqual(def.damage.x, def.damage.y, def.name);
        }
    }
}
