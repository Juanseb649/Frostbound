using NUnit.Framework;
using UnityEngine;

public class CastleTests
{
    [Test]
    public void ElInteriorEsDeterministaYTodasLasSalasSeAlcanzan()
    {
        for (int seed = 1; seed <= 150; seed++)
        {
            var a = CastleInteriorLayout.Generate(seed * 104729);
            var b = CastleInteriorLayout.Generate(seed * 104729);
            Assert.AreEqual(a.rooms.Count, b.rooms.Count, "seed " + seed);
            for (int i = 0; i < a.rooms.Count; i++)
            {
                Assert.AreEqual(a.rooms[i].rect.origin, b.rooms[i].rect.origin);
                Assert.AreEqual(a.rooms[i].theme, b.rooms[i].theme);
            }
            Assert.IsNotNull(a.Entry, "sin vestíbulo, seed " + seed);
            Assert.IsNotNull(a.Throne, "sin sala del trono, seed " + seed);
            Assert.GreaterOrEqual(a.rooms.Count, 4, "pocas salas, seed " + seed);
            Assert.IsTrue(CastleInteriorLayout.AllRoomsReachable(a), "sala inalcanzable, seed " + seed);
            Assert.Greater(Vector2.Distance(a.Entry.Center, a.Throne.Center), 12f, "el trono está junto a la entrada, seed " + seed);
            Assert.AreEqual(4, a.Throne.elites, "el ninja va con una élite de cada clase");
            for (int x = 0; x < a.size; x++)
            {
                Assert.IsFalse(a.floor[x, 0] || a.floor[x, a.size - 1], "suelo en el borde, seed " + seed);
                Assert.IsFalse(a.floor[0, x] || a.floor[a.size - 1, x], "suelo en el borde, seed " + seed);
            }
        }
    }

    [Test]
    public void LaNieblaDelMapaSeEmpaquetaYDesempaquetaIgual()
    {
        var rng = new DeterministicRng(42);
        var bits = new bool[128 * 128];
        for (int i = 0; i < bits.Length; i++) bits[i] = rng.Chance(0.3f);
        string packed = MapSystem.Pack(bits);
        var back = new bool[bits.Length];
        MapSystem.Unpack(packed, back);
        CollectionAssert.AreEqual(bits, back);
        Assert.Less(packed.Length, 3000, "la niebla ocupa demasiado en la partida");
        MapSystem.Unpack("esto no es base64!!", back);
    }

    [Test]
    public void LaMisionAvanzaSinPartidaYAvisaDelCambio()
    {
        int seen = -1;
        void OnChanged(string id, int stage) { if (id == QuestLog.Citadel) seen = stage; }
        QuestLog.Changed += OnChanged;
        try
        {
            QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelTalked);
            Assert.AreEqual(QuestLog.CitadelTalked, QuestLog.Stage(QuestLog.Citadel));
            Assert.AreEqual(QuestLog.CitadelTalked, seen);
            Assert.IsTrue(QuestLog.Active(QuestLog.Citadel));
            Assert.IsNotEmpty(QuestLog.Objective(QuestLog.Citadel, QuestLog.CitadelTalked));
            QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelDone);
            Assert.IsFalse(QuestLog.Active(QuestLog.Citadel));
            Assert.IsEmpty(QuestLog.Objective(QuestLog.Citadel, QuestLog.CitadelDone));
        }
        finally
        {
            QuestLog.Changed -= OnChanged;
            QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelNone);
        }
    }

    [Test]
    public void LaMisionYLaNieblaSeGuardanEnLaPartida()
    {
        var data = new SaveData { slot = 3, worldSeed = 99 };
        data.quests.Add(new SavedQuest { id = QuestLog.Citadel, stage = QuestLog.CitadelBarbarian });
        data.explored.Add(new SavedFog { area = "poblado", size = 128, bits = MapSystem.Pack(new bool[128 * 128]) });
        var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        Assert.AreEqual(1, back.quests.Count);
        Assert.AreEqual(QuestLog.CitadelBarbarian, back.quests[0].stage);
        Assert.AreEqual("poblado", back.explored[0].area);
        Assert.AreEqual(128, back.explored[0].size);
    }
}
