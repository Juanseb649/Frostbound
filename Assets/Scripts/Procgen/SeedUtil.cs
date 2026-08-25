public static class SeedUtil
{
    // Combina un seed con un valor; el orden altera el resultado.
    public static int Combine(int seed, int value)
    {
        unchecked
        {
            uint h = (uint)seed ^ 0x9E3779B9u;
            h ^= (uint)value;
            h *= 2654435761u;
            h ^= h >> 13;
            h *= 3266489917u;
            h ^= h >> 16;
            return (int)h;
        }
    }

    public static int Combine(int seed, int a, int b)
    {
        return Combine(Combine(seed, a), b);
    }

    // Hash estable para textos (ids de castillos, nombres, etc.).
    public static int FromString(string text)
    {
        unchecked
        {
            uint h = 2166136261u;
            if (!string.IsNullOrEmpty(text))
            {
                foreach (char c in text)
                {
                    h ^= c;
                    h *= 16777619u;
                }
            }
            return (int)h;
        }
    }
}
