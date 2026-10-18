using System.Collections.Generic;
using UnityEngine;

// Distribución de la Cumbre del Frostspire (capítulo III) a partir de la seed de la partida (función pura).
// Una grieta de hielo que sube hacia el norte (+Z) entre paredes de hielo azul: campamentos, hogueras, los tres
// altares del sello y, arriba, la cámara del sello donde espera Bobby y late el Corazón del Frost.
public static class SummitLayout
{
    public const string BonfirePrefix = "cumbre_hoguera_";
    public const string ArrivalFromForest = ForestLayout.ArrivalFromForest;

    public const float Length = 260f;
    public const float HalfWidth = 110f;
    public const float Margin = 30f;
    public const float CliffWidth = 3f;
    public const float CliffHeight = 8f;

    public enum ClearingKind { Entry, Camp, Bonfire, Altar, Chamber }

    public struct Spot
    {
        public Vector3 position;
        public float yaw;
        public float scale;
    }

    public class Clearing
    {
        public ClearingKind kind;
        public Vector3 center;
        public float radius;
        public int index;
    }

    public class Result
    {
        public int seed;
        public float a1, f1, p1, a2, f2, p2, nx, nz;
        public float[] profile;
        public readonly List<Clearing> clearings = new List<Clearing>();
        public readonly List<Spot> spires = new List<Spot>();
        public readonly List<Spot> crystals = new List<Spot>();
        public readonly List<Spot> rocks = new List<Spot>();
        public readonly List<Spot> frozen = new List<Spot>();
        public Vector3 entry, backGate, barrier;

        public float CenterX(float z)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 36f, z)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Length - 4f, Length - 60f, z));
            return k * (a1 * Mathf.Sin(z * f1 + p1) + a2 * Mathf.Sin(z * f2 + p2));
        }

        private float Slope(float z) => CenterX(z + 0.5f) - CenterX(z - 0.5f);

        public float HalfWidthAt(float z) => 4.8f + 2f * Mathf.PerlinNoise(nx + z * 0.05f, nz);

        public float PathHeight(float z)
        {
            z = Mathf.Clamp(z, 0f, Length);
            int i = Mathf.Min(profile.Length - 2, Mathf.FloorToInt(z));
            return Mathf.Lerp(profile[i], profile[i + 1], z - i);
        }

        public float Edge(float x, float z)
        {
            float s = Slope(z);
            float d = Mathf.Abs(x - CenterX(z)) / Mathf.Sqrt(1f + s * s) - HalfWidthAt(z);
            foreach (Clearing c in clearings)
            {
                float dx = x - c.center.x, dz = z - c.center.z;
                d = Mathf.Min(d, Mathf.Sqrt(dx * dx + dz * dz) - c.radius);
            }
            d = Mathf.Max(d, 1.5f - z);
            d = Mathf.Max(d, z - (Length - 1.5f));
            return d;
        }

        public bool Walkable(float x, float z) => Edge(x, z) < 0f;

        // Paredes de hielo verticales que suben a crestas dentadas.
        public float Height(float x, float z)
        {
            float e = Edge(x, z);
            float h = PathHeight(z);
            float soft = Mathf.PerlinNoise(nx + x * 0.14f, nz + z * 0.14f) - 0.5f;
            if (e <= 0f) return h + soft * 0.25f * Mathf.Clamp01(-e / 3f);
            float cliff = Mathf.SmoothStep(0f, 1f, e / CliffWidth) * CliffHeight;
            float ridge = (Mathf.PerlinNoise(nx + x * 0.05f, nz + z * 0.05f) - 0.25f) * 16f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CliffWidth, 24f, e));
            float far = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14f, 90f, e)) * 40f;
            return h + cliff + Mathf.Max(0f, ridge) + far + soft * 1.2f;
        }

        public Vector3 Ground(float x, float z) => new Vector3(x, Height(x, z), z);

        public Clearing Find(ClearingKind kind)
        {
            foreach (Clearing c in clearings) if (c.kind == kind) return c;
            return null;
        }

        public IEnumerable<Clearing> All(ClearingKind kind)
        {
            foreach (Clearing c in clearings) if (c.kind == kind) yield return c;
        }

        public float PathYaw(float z) => Mathf.Atan2(Slope(z), 1f) * Mathf.Rad2Deg;
    }

    public static Result Generate(int seed)
    {
        var rng = new DeterministicRng(SeedUtil.Combine(seed, SeedUtil.FromString("cumbre")));
        var r = new Result
        {
            seed = seed,
            a1 = rng.Range(12f, 18f) * (rng.Chance(0.5f) ? 1f : -1f),
            f1 = rng.Range(0.022f, 0.028f),
            p1 = rng.Range(0f, 6.28f),
            a2 = rng.Range(4f, 7f),
            f2 = rng.Range(0.05f, 0.07f),
            p2 = rng.Range(0f, 6.28f),
            nx = rng.Range(0f, 900f),
            nz = rng.Range(0f, 900f)
        };

        var plan = new[]
        {
            (ClearingKind.Camp, 38f), (ClearingKind.Altar, 62f), (ClearingKind.Camp, 86f), (ClearingKind.Bonfire, 106f),
            (ClearingKind.Altar, 128f), (ClearingKind.Camp, 152f), (ClearingKind.Altar, 176f), (ClearingKind.Bonfire, 198f)
        };
        var spots = new List<(ClearingKind kind, float z, float offset, float radius, int index)> { (ClearingKind.Entry, 14f, 0f, 10f, 0) };
        int camps = 0, bonfires = 0, altars = 0;
        foreach (var (kind, z) in plan)
        {
            float zz = z + rng.Range(-4f, 4f);
            float side = rng.Chance(0.5f) ? 1f : -1f;
            float radius = kind == ClearingKind.Camp ? rng.Range(10.5f, 12.5f) : kind == ClearingKind.Altar ? rng.Range(8f, 9f) : rng.Range(8f, 9.5f);
            int index = kind == ClearingKind.Camp ? camps++ : kind == ClearingKind.Bonfire ? bonfires++ : kind == ClearingKind.Altar ? altars++ : 0;
            spots.Add((kind, zz, side * rng.Range(2f, 5f), radius, index));
        }
        spots.Add((ClearingKind.Chamber, Length - 28f, 0f, 22f, 0));
        r.profile = Profile(spots, rng);
        foreach (var sp in spots)
        {
            float x = r.CenterX(sp.z) + sp.offset;
            r.clearings.Add(new Clearing { kind = sp.kind, center = new Vector3(x, r.PathHeight(sp.z), sp.z), radius = sp.radius, index = sp.index });
        }

        r.entry = r.Ground(r.CenterX(12f), 12f);
        r.backGate = r.Ground(r.CenterX(3.5f), 3.5f);
        Clearing chamber = r.Find(ClearingKind.Chamber);
        float bz = chamber.center.z - chamber.radius - 2f;
        r.barrier = r.Ground(r.CenterX(bz), bz);

        Decorate(r, new DeterministicRng(SeedUtil.Combine(seed, SeedUtil.FromString("cumbre_decorado"))));
        return r;
    }

    // Llanos en los claros, rampas empinadas entre ellos.
    private static float[] Profile(List<(ClearingKind kind, float z, float offset, float radius, int index)> spots, DeterministicRng rng)
    {
        int n = Mathf.CeilToInt(Length) + 2;
        var flat = new bool[n];
        foreach (var sp in spots)
            for (int z = Mathf.Max(0, Mathf.FloorToInt(sp.z - sp.radius * 0.6f)); z <= Mathf.Min(n - 1, Mathf.CeilToInt(sp.z + sp.radius * 0.6f)); z++) flat[z] = true;
        var h = new float[n];
        float y = 0f;
        for (int z = 1; z < n; z++)
        {
            if (!flat[z]) y += rng.Range(0.18f, 0.3f);
            h[z] = y;
        }
        var smooth = new float[n];
        for (int z = 0; z < n; z++)
        {
            float sum = 0f; int c = 0;
            for (int k = -3; k <= 3; k++) { int i = Mathf.Clamp(z + k, 0, n - 1); sum += h[i]; c++; }
            smooth[z] = sum / c;
        }
        return smooth;
    }

    private static void Decorate(Result r, DeterministicRng rng)
    {
        // Agujas de hielo en lo alto de las paredes.
        for (int attempt = 0; attempt < 2600 && r.spires.Count < 160; attempt++)
        {
            float x = rng.Range(-HalfWidth + 6f, HalfWidth - 6f), z = rng.Range(-Margin + 6f, Length + Margin - 6f);
            float e = r.Edge(x, z);
            if (e < CliffWidth + 1f || e > 60f) continue;
            if (Near(r.spires, x, z, 6f)) continue;
            r.spires.Add(new Spot { position = r.Ground(x, z) - Vector3.up * 0.5f, yaw = rng.Range(0f, 360f), scale = rng.Range(2f, 6f) });
        }
        // Cristales del Frost al pie de las paredes.
        for (int attempt = 0; attempt < 1600 && r.crystals.Count < 90; attempt++)
        {
            float x = rng.Range(-60f, 60f), z = rng.Range(6f, Length - 6f);
            float e = r.Edge(x, z);
            if (e < -0.5f || e > 2.5f) continue;
            if (Near(r.crystals, x, z, 4f)) continue;
            r.crystals.Add(new Spot { position = r.Ground(x, z), yaw = rng.Range(0f, 360f), scale = rng.Range(0.6f, 1.5f) });
        }
        for (int attempt = 0; attempt < 900 && r.rocks.Count < 60; attempt++)
        {
            float x = rng.Range(-HalfWidth + 6f, HalfWidth - 6f), z = rng.Range(-Margin + 6f, Length + Margin - 6f);
            float e = r.Edge(x, z);
            if (e < 0.4f || e > 30f) continue;
            if (Near(r.rocks, x, z, 5f)) continue;
            float s = rng.Range(1.2f, 3f);
            r.rocks.Add(new Spot { position = r.Ground(x, z) - Vector3.up * s * 0.3f, yaw = rng.Range(0f, 360f), scale = s });
        }
        // Pingüinos congelados: los que subieron antes que el héroe.
        for (int attempt = 0; attempt < 600 && r.frozen.Count < 9; attempt++)
        {
            float x = rng.Range(-40f, 40f), z = rng.Range(20f, Length - 40f);
            float e = r.Edge(x, z);
            if (e < -2.5f || e > -0.8f) continue;
            if (Near(r.frozen, x, z, 18f)) continue;
            r.frozen.Add(new Spot { position = r.Ground(x, z), yaw = rng.Range(0f, 360f), scale = rng.Range(0.9f, 1.1f) });
        }
    }

    private static bool Near(List<Spot> list, float x, float z, float d)
    {
        foreach (Spot s in list)
        {
            float dx = s.position.x - x, dz = s.position.z - z;
            if (dx * dx + dz * dz < d * d) return true;
        }
        return false;
    }
}
