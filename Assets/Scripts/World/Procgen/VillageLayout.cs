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

    public class Result
    {
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
    public const float ForestRadius = 85f;
    public static readonly Vector3 SteveClearing = new Vector3(-11f, 0f, -47f);

    public const int PineCount = 95;
    public const int MoundCount = 30;
    public const float MinCampDistance = CampSafeRadius + CampRadius + 4f;
    public const float MinCampFromSteve = SteveSafeRadius + CampRadius + 4f;
    public const float MinBonfireFromCamp = 20f;

    public static Result Generate(int seed, IList<Vector3> campAnchors)
    {
        var r = new Result();
        PlaceCastleBonfire(r, Stream(seed, "hoguera_castillo"));
        PlaceCamps(r, Stream(seed, "campamentos"), campAnchors);
        PlaceBonfires(r, Stream(seed, "hogueras"));
        PlacePines(r, Stream(seed, "bosque"));
        PlaceMounds(r, Stream(seed, "nieve"));
        return r;
    }

    public static string BonfireId(int index) => "hoguera_" + (index + 1);

    private static DeterministicRng Stream(int seed, string name) => new DeterministicRng(SeedUtil.Combine(seed, SeedUtil.FromString("poblado_" + name)));

    private static void PlaceCastleBonfire(Result r, DeterministicRng rng)
    {
        float side = rng.Chance(0.5f) ? 1f : -1f;
        float deg = side * rng.Range(15f, 21f);
        Vector3 p = Polar(deg, WallRadius + rng.Range(6.5f, 7.5f));
        r.castleBonfire = new Spot { position = p, yaw = Yaw(-p), scale = 1f };
    }

    private static void PlaceCamps(Result r, DeterministicRng rng, IList<Vector3> anchors)
    {
        if (anchors == null) return;
        foreach (Vector3 anchor in anchors)
        {
            float baseDeg = Mathf.Atan2(anchor.x, anchor.z) * Mathf.Rad2Deg;
            float baseRadius = new Vector2(anchor.x, anchor.z).magnitude;
            Vector3 chosen = anchor;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector3 p = Polar(baseDeg + rng.Range(-16f, 16f), Mathf.Clamp(baseRadius + rng.Range(-5f, 7f), MinCampDistance + 0.5f, 66f));
                if (!CampOk(r, p)) continue;
                chosen = p;
                break;
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
        return Dist(p, r.castleBonfire.position) >= MinBonfireFromCamp;
    }

    private static void PlaceBonfires(Result r, DeterministicRng rng)
    {
        int count = rng.NextInt(2, 4);
        for (int attempt = 0; attempt < 400 && r.bonfires.Count < count; attempt++)
        {
            Vector3 p = Polar(rng.Range(0f, 360f), rng.Range(42f, 60f));
            if (OnRoad(p) || Dist(p, SteveClearing) < SteveSafeRadius + 6f) continue;
            if (Dist(p, r.castleBonfire.position) < 25f) continue;
            bool ok = true;
            foreach (Vector3 c in r.camps) if (Dist(p, c) < MinBonfireFromCamp) { ok = false; break; }
            foreach (Spot b in r.bonfires) if (Dist(p, b.position) < 25f) { ok = false; break; }
            if (!ok) continue;
            r.bonfires.Add(new Spot { position = p, yaw = Yaw(-p), scale = 0.85f });
        }
    }

    private static void PlacePines(Result r, DeterministicRng rng)
    {
        for (int attempt = 0; attempt < 900 && r.pines.Count < PineCount; attempt++)
        {
            Vector3 p = Polar(rng.Range(0f, 360f), rng.Range(WallRadius + 4.5f, ForestRadius));
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
