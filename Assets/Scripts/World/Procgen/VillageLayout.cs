using System.Collections.Generic;
using UnityEngine;

// Distribución del exterior del poblado a partir de la seed de la partida. Es una función pura:
// la misma seed y los mismos campamentos base dan siempre el mismo resultado.
public static class VillageLayout
{
    public struct Spot
    {
        public Vector3 position;
        public float yaw;
        public float scale;
    }

    // Diseño del castillo gótico. Espacio local: la fachada mira a +Z (hacia el poblado), la nave se extiende hacia -Z.
    public struct CastleDesign
    {
        public Vector3 position;
        public float yaw;
        public int bays;
        public bool transept;
        public int chapels;
        public float plateauHeight;
        public float naveHeight;
        public float sideTowerHeight;
        public float centralTowerHeight;
        public float spireRatio;
        public int detailSeed;

        public float NaveLength => bays * 5f;
        public float ViaductLength => plateauHeight / Mathf.Tan(17f * Mathf.Deg2Rad);
        public const float Forecourt = 9f;
        public const float HalfWidth = 18f;
        public float FrontZ => Forecourt + ViaductLength;
        public float BackZ => -(NaveLength + 12f);

        public Vector3 ToWorld(Vector3 local) => position + Quaternion.Euler(0f, yaw, 0f) * local;
        public Vector3 Foot => ToWorld(new Vector3(0f, 0f, FrontZ));
        public Vector3 Back => ToWorld(new Vector3(0f, 0f, BackZ));
    }

    public class Result
    {
        public CastleDesign castle;
        public Spot castleBonfire;
        public readonly List<Spot> bonfires = new List<Spot>();
        public readonly List<Vector3> camps = new List<Vector3>();
        public readonly List<Spot> pines = new List<Spot>();
        public readonly List<Spot> mounds = new List<Spot>();
    }

    public const float WallRadius = 32f;
    public const float CampSafeRadius = 38f;
    public const float SteveSafeRadius = 20f;
    public const float CampRadius = 5f;
    public const float ForestRadius = 112f;
    public static readonly Vector3 SteveClearing = new Vector3(-11f, 0f, -47f);

    public const int PineCount = 140;
    public const int MoundCount = 30;
    public const float MinCampDistance = CampSafeRadius + CampRadius + 4f;
    public const float MinCampFromSteve = SteveSafeRadius + CampRadius + 4f;
    public const float MinBonfireFromCamp = 20f;
    public const float CastleClearance = CastleDesign.HalfWidth + 4f;
    public const float MinBonfireFromVillage = 48f;
    public const float MinBonfireSpacing = 35f;
    public const float GroundHalfSize = 130f;
    public const float GroundVisualHalfSize = 200f;

    public static Result Generate(int seed, IList<Vector3> campAnchors)
    {
        var r = new Result();
        PlaceCastle(r, Stream(seed, "castillo"));
        PlaceCamps(r, Stream(seed, "campamentos"), campAnchors);
        PlaceBonfires(r, Stream(seed, "hogueras"));
        PlacePines(r, Stream(seed, "bosque"));
        PlaceMounds(r, Stream(seed, "nieve"));
        return r;
    }

    public static string BonfireId(int index) => "hoguera_" + (index + 1);

    private static DeterministicRng Stream(int seed, string name) => new DeterministicRng(SeedUtil.Combine(seed, SeedUtil.FromString("poblado_" + name)));

    private static void PlaceCastle(Result r, DeterministicRng rng)
    {
        var d = new CastleDesign
        {
            bays = rng.NextInt(4, 7),
            transept = rng.Chance(0.7f),
            chapels = rng.NextInt(3, 6),
            plateauHeight = rng.Range(5f, 6.5f),
            naveHeight = rng.Range(14f, 17f),
            sideTowerHeight = rng.Range(22f, 28f),
            centralTowerHeight = rng.Range(31f, 38f),
            spireRatio = rng.Range(0.5f, 0.75f),
            detailSeed = rng.NextInt(1, int.MaxValue)
        };
        float side = rng.Chance(0.5f) ? 1f : -1f;
        float deg = side * rng.Range(36f, 54f);
        float footRadius = rng.Range(50f, 56f);
        Vector3 inward = -Polar(deg, 1f);
        d.yaw = Yaw(inward);
        d.position = Polar(deg, footRadius) - inward * d.FrontZ;
        r.castle = d;

        float bonfireSide = rng.Chance(0.5f) ? 1f : -1f;
        Vector3 local = new Vector3(bonfireSide * rng.Range(5.5f, 7f), 0f, d.FrontZ + rng.Range(2f, 4f));
        Vector3 p = d.ToWorld(local);
        r.castleBonfire = new Spot { position = p, yaw = d.yaw, scale = 1f };
    }

    public static float DistanceToCastle(CastleDesign c, Vector3 p)
    {
        Vector3 a = c.Foot, b = c.Back;
        Vector2 pa = new Vector2(p.x - a.x, p.z - a.z), ba = new Vector2(b.x - a.x, b.z - a.z);
        float t = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(0.001f, ba.sqrMagnitude));
        return (pa - ba * t).magnitude;
    }

    private static void PlaceCamps(Result r, DeterministicRng rng, IList<Vector3> anchors)
    {
        if (anchors == null) return;
        foreach (Vector3 anchor in anchors)
        {
            float baseDeg = Mathf.Atan2(anchor.x, anchor.z) * Mathf.Rad2Deg;
            float baseRadius = new Vector2(anchor.x, anchor.z).magnitude;
            Vector3 chosen = anchor;
            bool found = false;
            for (int attempt = 0; attempt < 120 && !found; attempt++)
            {
                float spread = attempt < 40 ? 16f : attempt < 80 ? 40f : 180f;
                Vector3 p = Polar(baseDeg + rng.Range(-spread, spread), Mathf.Clamp(baseRadius + rng.Range(-5f, 9f), MinCampDistance + 0.5f, 72f));
                if (!CampOk(r, p)) continue;
                chosen = p;
                found = true;
            }
            r.camps.Add(chosen);
        }
    }

    private static bool CampOk(Result r, Vector3 p)
    {
        if (Flat(p).magnitude < MinCampDistance) return false;
        if (Dist(p, SteveClearing) < MinCampFromSteve) return false;
        if (Mathf.Abs(p.x) < 7f && p.z > 0f) return false;
        foreach (Vector3 c in r.camps) if (Dist(p, c) < 18f) return false;
        if (DistanceToCastle(r.castle, p) < CastleClearance + CampRadius + 4f) return false;
        return Dist(p, r.castleBonfire.position) >= MinBonfireFromCamp;
    }

    private static void PlaceBonfires(Result r, DeterministicRng rng)
    {
        int count = rng.NextInt(2, 4);
        float offset = rng.Range(0f, 360f);
        for (int k = 0; k < count; k++)
        {
            float center = offset + k * 360f / count;
            for (int attempt = 0; attempt < 300; attempt++)
            {
                float halfSector = attempt < 150 ? 180f / count * 0.8f : 180f;
                Vector3 p = Polar(center + rng.Range(-halfSector, halfSector), rng.Range(MinBonfireFromVillage + 2f, 88f));
                if (!BonfireOk(r, p)) continue;
                r.bonfires.Add(new Spot { position = p, yaw = Yaw(-p), scale = 0.85f });
                break;
            }
        }
    }

    private static bool BonfireOk(Result r, Vector3 p)
    {
        if (OnRoad(p) || Flat(p).magnitude < MinBonfireFromVillage) return false;
        if (Mathf.Abs(p.x) > GroundHalfSize - 10f || Mathf.Abs(p.z) > GroundHalfSize - 10f) return false;
        if (Dist(p, SteveClearing) < SteveSafeRadius + 6f) return false;
        if (DistanceToCastle(r.castle, p) < CastleClearance + 4f) return false;
        if (Dist(p, r.castleBonfire.position) < MinBonfireSpacing) return false;
        foreach (Vector3 c in r.camps) if (Dist(p, c) < MinBonfireFromCamp) return false;
        foreach (Spot b in r.bonfires) if (Dist(p, b.position) < MinBonfireSpacing) return false;
        return true;
    }

    private static void PlacePines(Result r, DeterministicRng rng)
    {
        for (int attempt = 0; attempt < 1400 && r.pines.Count < PineCount; attempt++)
        {
            Vector3 p = Polar(rng.Range(0f, 360f), rng.Range(WallRadius + 4.5f, ForestRadius));
            if (Mathf.Abs(p.x) > GroundHalfSize - 4f || Mathf.Abs(p.z) > GroundHalfSize - 4f) continue;
            if (!Clear(r, p, 10f, 7f, 5f)) continue;
            foreach (Spot o in r.pines) if (Dist(p, o.position) < 2.6f) { p = Vector3.positiveInfinity; break; }
            if (float.IsInfinity(p.x)) continue;
            r.pines.Add(new Spot { position = p, yaw = rng.Range(0f, 360f), scale = rng.Range(0.8f, 1.5f) });
        }
    }

    private static void PlaceMounds(Result r, DeterministicRng rng)
    {
        for (int attempt = 0; attempt < 300 && r.mounds.Count < MoundCount; attempt++)
        {
            bool inside = r.mounds.Count < 8;
            float deg = rng.Range(0f, 360f);
            if (inside && (Mathf.Abs(Mathf.DeltaAngle(deg, 0f)) < 14f || Mathf.Abs(Mathf.DeltaAngle(deg, 180f)) < 14f)) continue;
            Vector3 p = Polar(deg, inside ? rng.Range(WallRadius - 4.5f, WallRadius - 2.5f) : rng.Range(WallRadius + 4f, 65f));
            if (!Clear(r, p, 7f, 5f, 4f)) continue;
            r.mounds.Add(new Spot { position = p, yaw = rng.Range(0f, 360f), scale = rng.Range(0.8f, 2.2f) });
        }
    }

    private static bool Clear(Result r, Vector3 p, float fromSteve, float fromCamp, float fromBonfire)
    {
        if (OnRoad(p) || Dist(p, SteveClearing) < fromSteve) return false;
        if (DistanceToCastle(r.castle, p) < CastleClearance + 2f) return false;
        foreach (Vector3 c in r.camps) if (Dist(p, c) < fromCamp) return false;
        if (Dist(p, r.castleBonfire.position) < fromBonfire + 3f) return false;
        foreach (Spot b in r.bonfires) if (Dist(p, b.position) < fromBonfire) return false;
        return true;
    }

    public static bool OnRoad(Vector3 p)
    {
        if (Mathf.Abs(p.x) < 5f && p.z > 0f) return true;
        if (Mathf.Abs(p.x) < 4f && p.z < 0f && p.z > -WallRadius - 4f) return true;
        Vector3 start = Polar(180f, WallRadius + 2f);
        Vector3 end = SteveClearing + new Vector3(3f, 0f, 3f);
        Vector3 ctrl = new Vector3(0f, 0f, (start.z + end.z) * 0.5f - 2f);
        for (float t = 0f; t <= 1f; t += 0.1f)
        {
            Vector3 q = Vector3.Lerp(Vector3.Lerp(start, ctrl, t), Vector3.Lerp(ctrl, end, t), t);
            if ((q - p).sqrMagnitude < 16f) return true;
        }
        return false;
    }

    public static Vector3 Polar(float degFromNorth, float radius)
    {
        float a = degFromNorth * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    private static float Dist(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    private static float Yaw(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
}
