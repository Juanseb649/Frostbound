using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class WorldAndSaveTests
{
    private static readonly List<Vector3> Anchors = new List<Vector3>
    {
        new Vector3(-16f, 0f, 50f), new Vector3(18f, 0f, 56f), new Vector3(40f, 0f, 42f), new Vector3(-42f, 0f, 44f),
        new Vector3(52f, 0f, 12f), new Vector3(-54f, 0f, 10f), new Vector3(45f, 0f, -40f)
    };

    private string _folder;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "frostbound_tests_" + System.Guid.NewGuid().ToString("N"));
        SaveSystem.UseFolderForTests(_folder);
    }

    [TearDown]
    public void TearDown()
    {
        SaveSystem.UseFolderForTests(null);
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static string Fingerprint(VillageLayout.Result r)
    {
        var parts = new List<string> { r.castleBonfire.position.ToString("F3") };
        parts.AddRange(r.bonfires.Select(b => b.position.ToString("F3")));
        parts.AddRange(r.camps.Select(c => c.ToString("F3")));
        parts.AddRange(r.pines.Select(p => p.position.ToString("F3") + p.scale.ToString("F3")));
        parts.AddRange(r.mounds.Select(m => m.position.ToString("F3")));
        return string.Join("|", parts);
    }

    [Test]
    public void LaMismaSeedGeneraElMismoExterior()
    {
        Assert.AreEqual(Fingerprint(VillageLayout.Generate(777, Anchors)), Fingerprint(VillageLayout.Generate(777, Anchors)));
        Assert.AreNotEqual(Fingerprint(VillageLayout.Generate(777, Anchors)), Fingerprint(VillageLayout.Generate(778, Anchors)));
    }

    [Test]
    public void ElExteriorRespetaZonasSegurasCaminosYDistancias()
    {
        for (int seed = 1; seed <= 200; seed++)
        {
            VillageLayout.Result r = VillageLayout.Generate(seed * 7919, Anchors);
            Vector3 castle = r.castleBonfire.position;
            float castleR = new Vector2(castle.x, castle.z).magnitude;
            Assert.That(castleR, Is.InRange(VillageLayout.WallRadius + 6f, VillageLayout.CampSafeRadius + 2f), "hoguera del castillo, seed " + seed);
            Assert.IsFalse(VillageLayout.OnRoad(castle), "hoguera del castillo en el camino, seed " + seed);

            Assert.AreEqual(Anchors.Count, r.camps.Count);
            foreach (Vector3 c in r.camps)
            {
                Assert.GreaterOrEqual(new Vector2(c.x, c.z).magnitude, VillageLayout.MinCampDistance - 0.01f, "campamento en zona segura, seed " + seed);
                Assert.GreaterOrEqual(Vector3.Distance(c, VillageLayout.SteveClearing), VillageLayout.MinCampFromSteve - 0.01f, "campamento junto a Steve, seed " + seed);
            }

            Assert.That(r.bonfires.Count, Is.InRange(2, 3), "hogueras, seed " + seed);
            foreach (VillageLayout.Spot b in r.bonfires)
            {
                foreach (Vector3 c in r.camps) Assert.GreaterOrEqual(Vector3.Distance(b.position, c), VillageLayout.MinBonfireFromCamp - 0.01f, "hoguera junto a un campamento, seed " + seed);
                Assert.IsFalse(VillageLayout.OnRoad(b.position));
            }

            Assert.Greater(r.pines.Count, 80, "pinos, seed " + seed);
            foreach (VillageLayout.Spot p in r.pines)
            {
                Assert.IsFalse(VillageLayout.OnRoad(p.position), "pino en el camino, seed " + seed);
                Assert.Greater(new Vector2(p.position.x, p.position.z).magnitude, VillageLayout.WallRadius + 4f, "pino dentro de la muralla, seed " + seed);
            }
        }
    }

    [Test]
    public void TodosLosObjetosEstanEnLaBaseDeDatosConIdUnico()
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/Data/Items/ItemDatabase.asset");
        var ids = new HashSet<string>();
        foreach (ItemDefinition item in db.items)
        {
            Assert.IsNotNull(item);
            Assert.IsFalse(string.IsNullOrEmpty(item.id), item.name + " no tiene id");
            Assert.IsTrue(ids.Add(item.id), "id repetido: " + item.id);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.IsTrue(db.items.Contains(item), item.name + " no está en ItemDatabase: no se podría cargar de una partida");
        }
    }

    [Test]
    public void LaBaseDelJuegoTieneClasesObjetosYUI()
    {
        GameDatabase db = Resources.Load<GameDatabase>("GameDatabase");
        Assert.IsNotNull(db);
        Assert.AreEqual(4, db.classes.Count);
        Assert.IsNotNull(db.items);
        Assert.IsNotNull(db.palette);
        Assert.IsNotNull(db.uiSkin);
        Assert.IsNotNull(db.FindClass("Knight_Caballero"));
    }

    [Test]
    public void LaPartidaSeGuardaYSeCargaIgual()
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/Data/Items/ItemDatabase.asset");
        ItemDefinition sword = db.items.First(i => i.HasDurability);
        ItemDefinition rune = db.items.First(i => i.category == ItemCategory.Rune);
        var stack = new ItemStack(sword, 1) { quality = LootQuality.Rare, rareName = "Esquirla Invernal" };
        stack.durability = sword.maxDurability * 0.4f;
        stack.runes.Add(rune);
        stack.affixes.Add(new RolledAffix("afilado", AffixKind.Damage, 3.5f));

        var data = new SaveData { slot = 2, worldSeed = 424242, heroName = "Pingo", classId = "Knight_Caballero", plumageId = "azul", level = 4, experience = 37, pointsAvailable = 2 };
        data.allocated.Add(StatType.Strength, 3);
        SavedStack saved = SavedStack.From(stack);
        saved.index = 5;
        data.inventory.Add(saved);
        data.litBonfires.Add(Bonfire.CastleId);
        data.litBonfires.Add("hoguera_2");
        data.lastBonfire = "hoguera_2";

        Assert.IsTrue(SaveSystem.Save(data));
        Assert.IsTrue(SaveSystem.Exists(2));
        Assert.IsFalse(SaveSystem.Exists(0));

        SaveData loaded = SaveSystem.Load(2);
        Assert.AreEqual(424242, loaded.worldSeed);
        Assert.AreEqual("Pingo", loaded.heroName);
        Assert.AreEqual(4, loaded.level);
        Assert.AreEqual(3, loaded.allocated.strength);
        Assert.AreEqual("hoguera_2", loaded.lastBonfire);
        CollectionAssert.AreEqual(new[] { Bonfire.CastleId, "hoguera_2" }, loaded.litBonfires);

        ItemStack back = loaded.inventory[0].ToStack(db);
        Assert.AreEqual(5, loaded.inventory[0].index);
        Assert.AreEqual(sword, back.item);
        Assert.AreEqual(stack.durability, back.durability, 0.001f);
        Assert.AreEqual(LootQuality.Rare, back.quality);
        Assert.AreEqual("Esquirla Invernal", back.rareName);
        Assert.AreEqual(rune, back.runes[0]);
        Assert.AreEqual(3.5f, back.affixes[0].value, 0.001f);

        data.level = 5;
        Assert.IsTrue(SaveSystem.Save(data));
        Assert.AreEqual(5, SaveSystem.Load(2).level);
        Assert.IsFalse(File.Exists(SaveSystem.PathOf(2) + ".tmp"));

        SaveSystem.Delete(2);
        Assert.IsFalse(SaveSystem.AnyExists());
    }
}
