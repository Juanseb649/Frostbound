using System;
using UnityEngine;

// Monedas del héroe. Se ganan matando corruptos y vendiendo en la tienda; se gastan comprando y se pueden guardar en el arcón.
public class Wallet : MonoBehaviour
{
    [SerializeField] private int coins;
    public int Coins => coins;
    public event Action<int> Changed;

    public static Wallet Of(GameObject go)
    {
        if (go == null) return null;
        Wallet w = go.GetComponent<Wallet>();
        return w != null ? w : go.AddComponent<Wallet>();
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        coins += amount;
        Changed?.Invoke(coins);
    }

    public bool Spend(int amount)
    {
        if (amount < 0 || coins < amount) return false;
        coins -= amount;
        Changed?.Invoke(coins);
        return true;
    }

    // Monedas del bolsillo más las guardadas en el arcón de la taberna (los NPC del poblado aceptan ambas).
    public int CoinsWithStash => coins + Stash.Coins;

    // Paga primero con el bolsillo y, si no alcanza, el resto sale del arcón.
    public bool SpendWithStash(int amount)
    {
        if (amount < 0 || CoinsWithStash < amount) return false;
        int fromStash = Mathf.Max(0, amount - coins);
        if (fromStash > 0 && !Stash.TakeCoins(fromStash)) return false;
        coins -= amount - fromStash;
        Changed?.Invoke(coins);
        return true;
    }

    public void Set(int amount)
    {
        coins = Mathf.Max(0, amount);
        Changed?.Invoke(coins);
    }
}
