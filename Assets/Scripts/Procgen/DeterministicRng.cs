using System.Collections.Generic;
using UnityEngine;

// Generador pseudoaleatorio determinista: mismo seed = misma secuencia.
// Usa streams separados (varios DeterministicRng con seeds derivados) para que
// cada subsistema (layout, enemigos, loot) sea independiente entre sí.
public class DeterministicRng
{
    private readonly System.Random _random;

    public DeterministicRng(int seed)
    {
        _random = new System.Random(seed);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return _random.Next(minInclusive, maxExclusive);
    }

    public float Next01()
    {
        return (float)_random.NextDouble();
    }

    public float Range(float min, float max)
    {
        return min + (max - min) * Next01();
    }

    public bool Chance(float probability)
    {
        return Next01() < probability;
    }

    public T Pick<T>(IList<T> items)
    {
        return items[NextInt(0, items.Count)];
    }

    // Elige un elemento según pesos; para tablas de enemigos y loot.
    public T PickWeighted<T>(IList<T> items, IList<float> weights)
    {
        float total = 0f;
        for (int i = 0; i < weights.Count; i++) total += Mathf.Max(0f, weights[i]);
        if (total <= 0f) return items[0];

        float roll = Next01() * total;
        for (int i = 0; i < items.Count; i++)
        {
            roll -= Mathf.Max(0f, weights[i]);
            if (roll <= 0f) return items[i];
        }
        return items[items.Count - 1];
    }

    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = NextInt(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
