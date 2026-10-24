using System.Collections.Generic;
using UnityEngine;

// Distribución del Bosque Corrupto (capítulo II) a partir de la seed de la partida (función pura).
// Un sendero que serpentea hacia el norte (+Z) entre pinos negros, casi llano, con claros: campamentos, hogueras,
// las tres pistas de Pipo, las tres raíces corruptas y, al final, el corazón del bosque donde canta la Madre Savia.
public static class ForestLayout
{
    public const string BonfirePrefix = "bosque_hoguera_";
    public const string ArrivalFromStairs = "desde_escalinata";
    public const string ArrivalFromForest = "desde_bosque";
    public const string ArrivalFromSummit = "desde_cumbre";

    public const float Length = 300f;
    public const float HalfWidth = 110f;
    public const float Margin = 30f;
    public const float ThicketWidth = 4f;   // del borde del sendero a la maleza alta

    public enum ClearingKind { Entry, Camp, Bonfire, Clue, Root, Heart }

    // Las pistas, en orden: id del marcador, nombre y lo que se lee al examinarla.
    public static readonly (string id, string name, string text)[] Clues =
    {
        ("pista_huellas", "Huellas pequeñas", "Huellas de un pingüino pequeño... y al lado, otras enormes que arrastran algo."),
        ("pista_bufanda", "Bufanda roja", "Una bufanda roja, enganchada en una rama. Pipo la llevaba en el dibujo de Rocco."),
        ("pista_dibujo", "Dibujo en la nieve", "Un dibujo hecho con una aleta: una montaña, un corazón de hielo y una flecha hacia el bosque profundo."),
    };

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
        public readonly List<Clearing> clearings = new List<Clearing>();
        public readonly List<Spot> pines = new List<Spot>();
        public readonly List<Spot> deadTrees = new List<Spot>();
        public readonly List<Spot> rocks = new List<Spot>();
        public readonly List<Spot> mushrooms = new List<Spot>();
        public readonly List<Spot> pools = new List<Spot>();
        public Vector3 entry, backGate, onwardGate;

        public float CenterX(float z)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 36f, z)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Length - 4f, Length - 40f, z));
            return k * (a1 * Mathf.Sin(z * f1 + p1) + a2 * Mathf.Sin(z * f2 + p2));
        }

        private float Slope(float z) => CenterX(z + 0.5f) - CenterX(z - 0.5f);

        public float HalfWidthAt(float z) => 5.4f + 2.2f * Mathf.PerlinNoise(nx + z * 0.04f, nz);

        // El bosque apenas sube: lomas largas.
        public float PathHeight(float z) => z * 0.025f + Mathf.Sin(z * 0.035f + p1) * 1.2f;

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
            float soft = Mathf.PerlinNoise(nx + x * 0.1f, nz + z * 0.1f) - 0.5f;
            if (e <= 0f) return h + soft * 0.3f * Mathf.Clamp01(-e / 3f);
            // Fuera del sendero: un talud de maleza y luego lomas boscosas.
            float bank = Mathf.SmoothStep(0f, 1f, e / ThicketWidth) * 1.6f;
            float hills = (Mathf.PerlinNoise(nx + x * 0.03f, nz + z * 0.03f) - 0.35f) * 9f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ThicketWidth, 40f, e));
            float rough = soft * 0.9f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 6f, e));
            return h + bank + Mathf.Max(0f, hills) + rough;
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
        var rng = new DeterministicRng(SeedUtil.Combine(seed, SeedUtil.FromString("bosque")));
        var r = new Result
        {
            seed = seed,
            a1 = rng.Range(16f, 24f) * (rng.Chance(0.5f) ? 1f : -1f),
            f1 = rng.Range(0.02f, 0.026f),
            p1 = rng.Range(0f, 6.28f),
            a2 = rng.Range(5f, 9f),
            f2 = rng.Range(0.05f, 0.065f),
            p2 = rng.Range(0f, 6.28f),
            nx = rng.Range(0f, 900f),
            nz = rng.Range(0f, 900f)
        };

        // Recorrido: primero las pistas de Pipo, luego las raíces (cada una guardada) y al final el corazón.
        var plan = new[]
        {
            (ClearingKind.Camp, 40f), (ClearingKind.Clue, 60f), (ClearingKind.Camp, 82f), (ClearingKind.Bonfire, 102f),
            (ClearingKind.Clue, 122f), (ClearingKind.Camp, 144f), (ClearingKind.Clue, 164f), (ClearingKind.Root, 188f),
            (ClearingKind.Bonfire, 208f), (ClearingKind.Root, 228f), (ClearingKind.Root, 250f)
        };
        AddClearing(r, ClearingKind.Entry, 14f, 0f, 11f, 0);
        int camps = 0, bonfires = 0, clues = 0, roots = 0;
        foreach (var (kind, z) in plan)
        {
            float zz = z + rng.Range(-4f, 4f);
            float side = rng.Chance(0.5f) ? 1f : -1f;
            float radius = kind == ClearingKind.Camp ? rng.Range(11f, 13f) : kind == ClearingKind.Root ? rng.Range(11f, 12.5f)
                : kind == ClearingKind.Bonfire ? rng.Range(8.5f, 10f) : rng.Range(6.5f, 7.5f);
            // Las pistas quedan a un lado del sendero, medio escondidas.
            float offset = side * (kind == ClearingKind.Clue ? rng.Range(9f, 12f) : rng.Range(2f, 6f));
            int index = kind == ClearingKind.Camp ? camps++ : kind == ClearingKind.Bonfire ? bonfires++ : kind == ClearingKind.Clue ? clues++ : kind == ClearingKind.Root ? roots++ : 0;
            AddClearing(r, kind, zz, offset, radius, index);
        }
        AddClearing(r, ClearingKind.Heart, Length - 24f, 0f, 19f, 0);

        r.entry = r.Ground(r.CenterX(12f), 12f);
        r.backGate = r.Ground(r.CenterX(3.5f), 3.5f);
        r.onwardGate = r.Ground(r.CenterX(Length - 3.5f), Length - 3.5f);

        Decorate(r, new DeterministicRng(SeedUtil.Combine(seed, SeedUtil.FromString("bosque_decorado"))));
        return r;
    }

    private static void AddClearing(Result r, ClearingKind kind, float z, float offset, float radius, int index)
    {
        float x = r.CenterX(z) + offset;
        r.clearings.Add(new Clearing { kind = kind, center = new Vector3(x, r.PathHeight(z), z), radius = radius, index = index });
    }

    private static void Decorate(Result r, DeterministicRng rng)
    {
        // Pinos negros muy juntos: el bosque es una pared a los lados del sendero.
        for (int attempt = 0; attempt < 5200 && r.pines.Count < 620; attempt++)
        {
            float x = rng.Range(-HalfWidth + 4f, HalfWidth - 4f), z = rng.Range(-Margin + 6f, Length + Margin - 6f);
            float e = r.Edge(x, z);
            if (e < 1.6f || e > 75f) continue;
            if (Near(r.pines, x, z, e < 12f ? 2.6f : 3.6f)) continue;
            r.pines.Add(new Spot { position = r.Ground(x, z) - Vector3.up * 0.2f, yaw = rng.Range(0f, 360f), scale = rng.Range(1.1f, 2.3f) });
        }
        // Árboles secos y retorcidos al borde del sendero y en los claros.
        for (int attempt = 0; attempt < 1600 && r.deadTrees.Count < 70; attempt++)
        {
            float x = rng.Range(-70f, 70f), z = rng.Range(8f, Length - 8f);
            float e = r.Edge(x, z);
            if (e < 0.3f || e > 3.5f) continue;
            if (Near(r.deadTrees, x, z, 7f)) continue;
            r.deadTrees.Add(new Spot { position = r.Ground(x, z) - Vector3.up * 0.1f, yaw = rng.Range(0f, 360f), scale = rng.Range(0.8f, 1.4f) });
        }
        for (int attempt = 0; attempt < 1200 && r.rocks.Count < 90; attempt++)
        {
            float x = rng.Range(-HalfWidth + 6f, HalfWidth - 6f), z = rng.Range(-Margin + 6f, Length + Margin - 6f);
            float e = r.Edge(x, z);
            if (e < 0.5f || e > 30f) continue;
            if (Near(r.rocks, x, z, 5f)) continue;
            float s = rng.Range(1f, 2.6f);
            r.rocks.Add(new Spot { position = r.Ground(x, z) - Vector3.up * s * 0.3f, yaw = rng.Range(0f, 360f), scale = s });
        }
        // Setas que brillan con la savia, en el borde del sendero.
        for (int attempt = 0; attempt < 1500 && r.mushrooms.Count < 140; attempt++)
        {
            float x = rng.Range(-70f, 70f), z = rng.Range(6f, Length - 6f);
            float e = r.Edge(x, z);
            if (e < -1f || e > 2.5f) continue;
            r.mushrooms.Add(new Spot { position = r.Ground(x, z), yaw = rng.Range(0f, 360f), scale = rng.Range(0.5f, 1.2f) });
        }
        // Charcos de savia negra alrededor de las raíces y del corazón.
        foreach (Clearing c in r.clearings)
        {
            if (c.kind != ClearingKind.Root && c.kind != ClearingKind.Heart && c.kind != ClearingKind.Camp) continue;
            int n = c.kind == ClearingKind.Heart ? 8 : c.kind == ClearingKind.Root ? 4 : 1;
            for (int i = 0; i < n; i++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f);
                float d = c.radius * rng.Range(0.35f, 0.8f);
                float x = c.center.x + Mathf.Sin(a) * d, z = c.center.z + Mathf.Cos(a) * d;
                r.pools.Add(new Spot { position = r.Ground(x, z), yaw = rng.Range(0f, 360f), scale = rng.Range(1.2f, 2.6f) });
            }
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
