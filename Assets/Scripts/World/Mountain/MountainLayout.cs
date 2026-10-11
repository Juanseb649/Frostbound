using System.Collections.Generic;
using UnityEngine;

// Distribución de Las Faldas a partir de la seed de la partida (función pura: misma seed, misma montaña).
// La zona es un valle que sube hacia el norte (+Z): un sendero serpenteante con claros (campamentos, hogueras,
// la cima) encajonado entre paredes de roca que crecen hasta las cumbres.
public static class MountainLayout
{
    public const string BonfirePrefix = "faldas_hoguera_";
    public const string ArrivalFromVillage = "desde_poblado";
    public const string ArrivalFromFoothills = "desde_faldas";

    public const float Length = 300f;      // del pie (z = 0) al paso helado
    public const float HalfWidth = 130f;   // extensión del terreno a cada lado
    public const float Margin = 40f;       // terreno extra por delante y por detrás
    public const float Rise = 0.11f;       // metros que sube el sendero por metro avanzado
    public const float CliffWidth = 3f;
    public const float CliffHeight = 5.5f;

    public enum ClearingKind { Entry, Camp, Bonfire, Ruins, Summit }

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
        public float a1, f1, p1, a2, f2, p2, p3, nx, nz;
        public readonly List<Clearing> clearings = new List<Clearing>();
        public readonly List<Spot> pines = new List<Spot>();
        public readonly List<Spot> rocks = new List<Spot>();
        public readonly List<Spot> stones = new List<Spot>();
        public readonly List<Spot> mounds = new List<Spot>();
        public readonly List<Spot> cairns = new List<Spot>();
        public readonly List<Spot> crystals = new List<Spot>();
        public readonly List<Spot> peaks = new List<Spot>();
        public Vector3 entry, bottomGate, summitGate;

        // Centro del sendero a la altura z.
        public float CenterX(float z)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 40f, z)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Length - 4f, Length - 40f, z));
            return k * (a1 * Mathf.Sin(z * f1 + p1) + a2 * Mathf.Sin(z * f2 + p2));
        }

        private float Slope(float z) => (CenterX(z + 0.5f) - CenterX(z - 0.5f));

        public float HalfWidthAt(float z) => 6.2f + 2.6f * Mathf.PerlinNoise(nx + z * 0.03f, nz);

        // Altura del sendero (sube hacia el norte con pequeñas ondulaciones).
        public float PathHeight(float z)
        {
            z = Mathf.Clamp(z, 0f, Length);
            return z * Rise + 1.4f * Mathf.Sin(z * 0.035f + p3) - 1.4f * Mathf.Sin(p3);
        }

        // Distancia (con signo) al borde de la zona transitable: negativa dentro del sendero o de un claro.
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

        public float Height(float x, float z)
        {
            float e = Edge(x, z);
            float h = PathHeight(z);
            float soft = Mathf.PerlinNoise(nx + x * 0.12f, nz + z * 0.12f) - 0.5f;
            if (e <= 0f) return h + soft * 0.35f * Mathf.Clamp01(-e / 3f);
            float cliff = Mathf.SmoothStep(0f, 1f, e / CliffWidth) * CliffHeight;
            float ridge = (Mathf.PerlinNoise(nx + x * 0.035f, nz + z * 0.035f) - 0.3f) * 10f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CliffWidth, 30f, e));
            float rough = (Mathf.PerlinNoise(nx + 50f + x * 0.22f, nz + z * 0.22f) - 0.5f) * 1.6f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 5f, e));
            float far = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(18f, 110f, e)) * 34f + Mathf.Max(0f, e - CliffWidth) * 0.12f;
            return h + cliff + Mathf.Max(0f, ridge) + rough + far;
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

        // Dirección del sendero (hacia la cima) a la altura z.
        public float PathYaw(float z) => Mathf.Atan2(Slope(z), 1f) * Mathf.Rad2Deg;
    }

    public static Result Generate(int seed)
    {
        var rng = new DeterministicRng(SeedUtil.Combine(seed, SeedUtil.FromString("faldas")));
        var r = new Result
        {
            seed = seed,
            a1 = rng.Range(15f, 23f) * (rng.Chance(0.5f) ? 1f : -1f),
            f1 = rng.Range(0.018f, 0.024f),
            p1 = rng.Range(0f, 6.28f),
            a2 = rng.Range(4.5f, 8f),
            f2 = rng.Range(0.045f, 0.06f),
            p2 = rng.Range(0f, 6.28f),
            p3 = rng.Range(0f, 6.28f),
            nx = rng.Range(0f, 900f),
            nz = rng.Range(0f, 900f)
        };

        AddClearing(r, ClearingKind.Entry, 15f, 0f, 12f, 0);
        // Recorrido: campamento, hoguera, campamento, ruinas, campamento, hoguera, campamento y la cima.
        var plan = new[]
        {
            (ClearingKind.Camp, 58f), (ClearingKind.Bonfire, 88f), (ClearingKind.Camp, 118f), (ClearingKind.Ruins, 146f),
            (ClearingKind.Camp, 176f), (ClearingKind.Bonfire, 205f), (ClearingKind.Camp, 236f)
        };
        int camps = 0, bonfires = 0;
        foreach (var (kind, z) in plan)
        {
            float zz = z + rng.Range(-7f, 7f);
            float side = rng.Chance(0.5f) ? 1f : -1f;
            float radius = kind == ClearingKind.Camp ? rng.Range(13f, 16f) : kind == ClearingKind.Bonfire ? rng.Range(9f, 11f) : rng.Range(11f, 13f);
            float offset = side * rng.Range(3f, kind == ClearingKind.Camp ? 9f : 6f);
            AddClearing(r, kind, zz, offset, radius, kind == ClearingKind.Camp ? camps++ : kind == ClearingKind.Bonfire ? bonfires++ : 0);
        }
        AddClearing(r, ClearingKind.Summit, Length - 18f, 0f, 16f, 0);

        r.entry = r.Ground(r.CenterX(14f), 14f);
        r.bottomGate = r.Ground(r.CenterX(3.5f), 3.5f);
        r.summitGate = r.Ground(r.CenterX(Length - 3.5f), Length - 3.5f);

        Decorate(r, new DeterministicRng(SeedUtil.Combine(seed, SeedUtil.FromString("faldas_decorado"))));
        return r;
    }

    private static void AddClearing(Result r, ClearingKind kind, float z, float offset, float radius, int index)
    {
        float x = r.CenterX(z) + offset;
        r.clearings.Add(new Clearing { kind = kind, center = new Vector3(x, r.PathHeight(z), z), radius = radius, index = index });
    }

    private static void Decorate(Result r, DeterministicRng rng)
    {
        // Pinos en las laderas y en lo alto de las paredes (nunca en el sendero).
        for (int attempt = 0; attempt < 2400 && r.pines.Count < 340; attempt++)
        {
            float x = rng.Range(-HalfWidth + 6f, HalfWidth - 6f), z = rng.Range(-Margin + 8f, Length + Margin - 8f);
            float e = r.Edge(x, z);
            if (e < CliffWidth + 0.8f || e > 70f) continue;
            if (Near(r.pines, x, z, 3.2f)) continue;
            r.pines.Add(new Spot { position = r.Ground(x, z) - Vector3.up * 0.2f, yaw = rng.Range(0f, 360f), scale = rng.Range(0.85f, 1.7f) });
        }
        // Rocas grandes en las paredes y medio enterradas en las laderas.
        for (int attempt = 0; attempt < 1600 && r.rocks.Count < 170; attempt++)
        {
            float x = rng.Range(-HalfWidth + 6f, HalfWidth - 6f), z = rng.Range(-Margin + 6f, Length + Margin - 6f);
            float e = r.Edge(x, z);
            if (e < 0.4f || e > 45f) continue;
            if (Near(r.rocks, x, z, 4f)) continue;
            float s = e < CliffWidth + 1f ? rng.Range(1.6f, 3.2f) : rng.Range(1.5f, 4.5f);
            r.rocks.Add(new Spot { position = r.Ground(x, z) - Vector3.up * s * 0.25f, yaw = rng.Range(0f, 360f), scale = s });
        }
        // Piedras pequeñas en el borde del sendero.
        for (int attempt = 0; attempt < 900 && r.stones.Count < 90; attempt++)
        {
            float x = rng.Range(-60f, 60f), z = rng.Range(4f, Length - 4f);
            float e = r.Edge(x, z);
            if (e < -1.2f || e > 0.6f) continue;
            r.stones.Add(new Spot { position = r.Ground(x, z), yaw = rng.Range(0f, 360f), scale = rng.Range(0.4f, 0.9f) });
        }
        // Montones de nieve pegados a las paredes.
        for (int attempt = 0; attempt < 600 && r.mounds.Count < 45; attempt++)
        {
            float x = rng.Range(-60f, 60f), z = rng.Range(4f, Length - 4f);
            float e = r.Edge(x, z);
            if (e < -2f || e > 0f) continue;
            if (Near(r.mounds, x, z, 6f)) continue;
            r.mounds.Add(new Spot { position = r.Ground(x, z) - Vector3.up * 0.2f, yaw = rng.Range(0f, 360f), scale = rng.Range(0.8f, 1.8f) });
        }
        // Mojones de piedra que marcan el sendero.
        for (float z = 26f; z < Length - 20f; z += rng.Range(22f, 32f))
        {
            float side = rng.Chance(0.5f) ? 1f : -1f;
            float x = r.CenterX(z) + side * (r.HalfWidthAt(z) - 1.1f);
            if (r.Edge(x, z) > -0.3f) continue;
            r.cairns.Add(new Spot { position = r.Ground(x, z), yaw = rng.Range(0f, 360f), scale = rng.Range(0.9f, 1.2f) });
        }
        // Cristales del Frost: brotan alrededor de los campamentos y en la cima.
        foreach (Clearing c in r.clearings)
        {
            if (c.kind != ClearingKind.Camp && c.kind != ClearingKind.Summit && c.kind != ClearingKind.Ruins) continue;
            int n = c.kind == ClearingKind.Summit ? 9 : rng.NextInt(4, 7);
            for (int i = 0; i < n; i++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f);
                float d = c.radius * rng.Range(0.75f, 1.05f);
                float x = c.center.x + Mathf.Sin(a) * d, z = c.center.z + Mathf.Cos(a) * d;
                r.crystals.Add(new Spot { position = r.Ground(x, z), yaw = rng.Range(0f, 360f), scale = rng.Range(0.7f, 1.6f) });
            }
        }
        // Cumbres lejanas: el Frostspire de fondo.
        for (int i = 0; i < 9; i++)
        {
            float x = rng.Range(-HalfWidth, HalfWidth), z = Length + rng.Range(50f, 170f);
            r.peaks.Add(new Spot { position = new Vector3(x, r.PathHeight(Length) + 10f, z), yaw = rng.Range(0f, 360f), scale = rng.Range(55f, 95f) });
        }
        r.peaks.Add(new Spot { position = new Vector3(0f, r.PathHeight(Length) + 6f, Length + 230f), yaw = 0f, scale = 150f });
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
