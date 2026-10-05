using System.Linq;
using UnityEngine;

// Comprobaciones en Play de monedas, tienda y arcón (solo texto).
public static class TradeChecks
{
    private static Transform P => GameObject.Find("Player").transform;

    public static string KillNearbyForCoins()
    {
        int before = Wallet.Of(P.gameObject).Coins;
        var def = GameDatabase.Instance.FindEnemy("Corrupt_Melee");
        int spawned = 0;
        for (int i = 0; i < 6; i++)
        {
            var b = EnemySpawner.Spawn(def, P.position + Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * 1.5f, 0f, null, 2, false, new DeterministicRng(i + 10));
            if (b == null) continue;
            b.paused = true;
            b.GetComponent<Damageable>().Kill(P.gameObject, Vector3.forward);
            spawned++;
        }
        return "muertos " + spawned + " · montones " + Object.FindObjectsByType<CoinPile>().Length + " · monedas antes " + before;
    }

    public static string Coins() => "monedas " + Wallet.Of(P.gameObject).Coins + " · arcón " + Stash.Coins + " (" + Stash.Items.UsedSlots + "/60)";

    public static string OpenShopAndSell()
    {
        var tico = Object.FindObjectsByType<NPCInteractable>().FirstOrDefault(n => n.displayName == "Mercader Tico");
        if (tico == null) return "sin Tico";
        string opts = string.Join(",", tico.AvailableOptions());
        tico.Choose(NPCOption.Vender);
        var eq = P.GetComponent<Equipment>();
        int idx = -1;
        for (int i = 0; i < eq.Inventory.Capacity; i++) if (eq.Inventory.Get(i) != null && !eq.Inventory.Get(i).item.IsConsumable) { idx = i; break; }
        if (idx < 0) return "opciones " + opts + " · nada que vender";
        var s = eq.Inventory.Get(idx);
        int price = ItemPricing.SellPrice(s);
        int before = Wallet.Of(P.gameObject).Coins;
        eq.Inventory.RemoveAt(idx);
        Wallet.Of(P.gameObject).Add(price);
        return "opciones " + opts + " · ventana " + TradeWindow.IsOpen + " · vendido " + s.DisplayName + " por " + price + " (" + before + "→" + Wallet.Of(P.gameObject).Coins + ")";
    }

    public static string OpenStashAndStore()
    {
        var mora = Object.FindObjectsByType<NPCInteractable>().FirstOrDefault(n => n.displayName == "Tabernera Mora");
        if (mora == null) return "sin Mora";
        string opts = string.Join(",", mora.AvailableOptions());
        mora.Choose(NPCOption.Arcon);
        var eq = P.GetComponent<Equipment>();
        int idx = -1;
        for (int i = 0; i < eq.Inventory.Capacity; i++) if (eq.Inventory.Get(i) != null) { idx = i; break; }
        string stored = "nada";
        if (idx >= 0)
        {
            var s = eq.Inventory.RemoveAt(idx);
            Stash.Items.AddStack(s);
            stored = s.DisplayName;
        }
        Stash.DepositCoins(Wallet.Of(P.gameObject), 5);
        return "opciones " + opts + " · ventana " + TradeWindow.IsOpen + " · guardado " + stored + " · " + Coins();
    }
}
