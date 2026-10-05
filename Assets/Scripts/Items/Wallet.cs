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

    public void Set(int amount)
    {
        coins = Mathf.Max(0, amount);
        Changed?.Invoke(coins);
    }
}
