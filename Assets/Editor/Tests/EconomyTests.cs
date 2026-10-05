using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EconomyTests
{
    private ItemDefinition Item(string path) => AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);

    [Test]
    public void LosPreciosTienenSentido()
    {
        var sword = Item("Assets/Data/Items/Weapons/claymore.asset");
        var low = new ItemStack(sword, 1) { itemLevel = 1 };
        var high = new ItemStack(sword, 1) { itemLevel = 8 };
        var broken = new ItemStack(sword, 1) { itemLevel = 8, durability = 0f };
        Assert.Greater(ItemPricing.SellPrice(high), ItemPricing.SellPrice(low), "el arma de nivel alto vale más");
        Assert.Less(ItemPricing.SellPrice(broken), ItemPricing.SellPrice(high), "rota vale menos");
        Assert.Greater(ItemPricing.BuyPrice(low), ItemPricing.SellPrice(low), "comprar cuesta más que vender");
        var magic = new ItemStack(sword, 1) { itemLevel = 1, quality = LootQuality.Magic };
        Assert.Greater(ItemPricing.SellPrice(magic), ItemPricing.SellPrice(low), "la calidad sube el precio");
    }

    [Test]
    public void ElArconTieneSesentaCasillasYSeGuardaEnLaPartida()
    {
        var sword = Item("Assets/Data/Items/Weapons/claymore.asset");
        var data = new SaveData { stashCoins = 345 };
        data.stash.Add(new SavedStack { index = 59, id = sword.id, quantity = 1, itemLevel = 4 });
        Stash.Load(data);
        Assert.AreEqual(60, Stash.Items.Capacity);
        Assert.AreEqual(345, Stash.Coins);
        Assert.IsNotNull(Stash.Items.Get(59));
        Assert.AreEqual(4, Stash.Items.Get(59).itemLevel);

        var back = new SaveData();
        Stash.Capture(back);
        var json = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(back));
        Assert.AreEqual(345, json.stashCoins);
        Assert.AreEqual(1, json.stash.Count);
        Assert.AreEqual(59, json.stash[0].index);
        Stash.Load(new SaveData());
        Assert.AreEqual(0, Stash.Items.UsedSlots);
    }

    [Test]
    public void LasMonedasDelArconPasanDeLaBolsaAlArconYVuelven()
    {
        Stash.Load(new SaveData());
        var go = new GameObject("Cartera");
        try
        {
            var w = go.AddComponent<Wallet>();
            w.Set(120);
            Stash.DepositCoins(w, 100);
            Assert.AreEqual(20, w.Coins);
            Assert.AreEqual(100, Stash.Coins);
            Stash.DepositCoins(w, 500);
            Assert.AreEqual(0, w.Coins, "no se guardan más de las que llevas");
            Stash.WithdrawCoins(w, 1000);
            Assert.AreEqual(120, w.Coins);
            Assert.AreEqual(0, Stash.Coins);
            Assert.IsFalse(w.Spend(121));
        }
        finally { Object.DestroyImmediate(go); Stash.Load(new SaveData()); }
    }
}
