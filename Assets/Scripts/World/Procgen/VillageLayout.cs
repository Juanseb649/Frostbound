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
        public const float TownDepth = 46f;
        public const float TownHalfWidth = 23f;
        public float TownCenterZ => FrontZ + TownDepth * 0.5f;
        public float GateZ => FrontZ + TownDepth;
        public float ViaductLength => plateauHeight / Mathf.Tan(17f * Mathf.Deg2Rad);
        public const float Forecourt = 9f;
        public const float HalfWidth = 18f;
        public float FrontZ => Forecourt + ViaductLength;
        public float BackZ => -(NaveLength + 12f);

        public Vector3 ToWorld(Vector3 local) => position + Quaternion.Euler(0f, yaw, 0f) * local;
        public Vector3 Foot => ToWorld(new Vector3(0f, 0f, FrontZ));
        public Vector3 Back => ToWorld(new Vector3(0f, 0f, BackZ));
        public Vector3 Gate => ToWorld(new Vector3(0f, 0f, GateZ));
        public Vector3 TownCenter => ToWorld(new Vector3(0f, 0f, TownCenterZ));
        public Vector3 DoorLocal => new Vector3(0f, plateauHeight + 0.6f, 1.6f);
        public Vector3 Door => ToWorld(DoorLocal);
    }

    // Casa en ruinas del poblado del castillo (espacio local del castillo).
    public struct Ruin
    {
        public Vector3 local;
        public float yaw;
        public float width, depth, height;
        public int state;       // 0 en pie (tejado roto), 1 sin tejado, 2 derrumbada
        public bool burnt;
    }

    public class CitadelTown
    {
        public readonly List<Ruin> ruins = new List<Ruin>();
        public readonly List<Vector3> wallPosts = new List<Vector3>();  // perímetro, en orden; null = hueco
        public readonly List<bool> wallGaps = new List<bool>();
        public readonly List<Vector3> lamps = new List<Vector3>();
        public readonly List<Vector3> crystals = new List<Vector3>();
        public readonly List<Vector3> spawnPoints = new List<Vector3>();
        public readonly List<Vector3> camp = new List<Vector3>();
        public Vector3 bossSpawn;
        public const float ArenaRadius = 11f;
    }

    public class Result
    {
        public CastleDesign castle;
        public CitadelTown town;
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
    public const float ForestRadius = 150f;
    public static readonly Vector3 SteveClearing = new Vector3(-11f, 0f, -47f);

    public const int PineCount = 210;
    public const int MoundCount = 30;
    public const float MinCampDistance = CampSafeRadius + CampRadius + 4f;
    public const float MinCampFromSteve = SteveSafeRadius + CampRadius + 4f;
    public const float MinBonfireFromCamp = 20f;
    public const float CastleClearance = CastleDesign.TownHalfWidth + 4f;
    public const float MinBonfireFromVillage = 48f;
    public const float MinBonfireSpacing = 35f;
    public const float GroundHalfSize = 185f;
    public const float GroundVisualHalfSize = 260f;

    public static Result Generate(int seed, IList<Vector3> campAnchors)
    {
        var r = new Result();
        PlaceCastle(r, Stream(seed, "castillo"));
        PlaceTown(r, Stream(seed, "ciudadela"));
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
            naveHeight = rng.Range(18f, 22f),
            sideTowerHeight = rng.Range(30f, 37f),
            centralTowerHeight = rng.Range(44f, 52f),
            spireRatio = rng.Range(0.85f, 1.15f),
            detailSeed = rng.NextInt(1, int.MaxValue)
        };
        float side = rng.Chance(0.5f) ? 1f : -1f;
        float deg = side * rng.Range(52f, 68f);
        float footRadius = rng.Range(92f, 98f);
        Vector3 inward = -Polar(deg, 1f);
        d.yaw = Yaw(inward);
        d.position = Polar(deg, footRadius) - inward * d.FrontZ;
        r.castle = d;

        // La hoguera del castillo queda justo fuera de la puerta del poblado en ruinas.
        float bonfireSide = rng.Chance(0.5f) ? 1f : -1f;
        Vector3 local = new Vector3(bonfireSide * rng.Range(5.5f, 7f), 0f, d.GateZ + rng.Range(3f, 5f));
        Vector3 p = d.ToWorld(local);
        r.castleBonfire = new Spot { position = p, yaw = d.yaw, scale = 1f };
    }

    // Poblado en ruinas entre el pie del viaducto y la puerta: casas alrededor de una plaza, muralla rota,
    // farolas en la avenida y cristales del Frost. Los enemigos de las oleadas salen de los bordes de la plaza.
    private static void PlaceTown(Result r, DeterministicRng rng)
    {
        CastleDesign c = r.castle;
        var t = new CitadelTown();
        float cz = c.TownCenterZ, hw = CastleDesign.TownHalfWidth, hd = CastleDesign.TownDepth * 0.5f;

        int target = rng.NextInt(9, 13);
        for (int attempt = 0; attempt < 400 && t.ruins.Count < target; attempt++)
        {
            float w = rng.Range(4.5f, 7f), dd = rng.Range(4.5f, 6.5f);
            Vector3 p = new Vector3(rng.Range(-hw + 4f, hw - 4f), 0f, cz + rng.Range(-hd + 4f, hd - 4f));
            Vector2 fromCenter = new Vector2(p.x, p.z - cz);
            if (fromCenter.magnitude < CitadelTown.ArenaRadius + 3f) continue;
            if (Mathf.Abs(p.x) < 5.5f) continue;
            bool clash = false;
            foreach (Ruin o in t.ruins)
                if (Mathf.Abs(o.local.x - p.x) < (o.width + w) * 0.5f + 2.2f && Mathf.Abs(o.local.z - p.z) < (o.depth + dd) * 0.5f + 2.2f) { clash = true; break; }
            if (clash) continue;
            float face = Mathf.Atan2(-fromCenter.x, -fromCenter.y) * Mathf.Rad2Deg;
            t.ruins.Add(new Ruin
            {
                local = p,
                yaw = Mathf.Round(face / 90f) * 90f + rng.Range(-6f, 6f),
                width = w,
                depth = dd,
                height = rng.Range(3.2f, 4.6f),
                state = rng.NextInt(0, 3),
                burnt = rng.Chance(0.4f)
            });
        }

        // Muralla: rectángulo con huecos (la puerta delante, el viaducto detrás y algún derrumbe).
        float step = 2.5f;
        Vector3[] corners =
        {
            new Vector3(-hw, 0f, cz + hd), new Vector3(hw, 0f, cz + hd), new Vector3(hw, 0f, cz - hd), new Vector3(-hw, 0f, cz - hd)
        };
        for (int side = 0; side < 4; side++)
        {
            Vector3 a = corners[side], b = corners[(side + 1) % 4];
            int n = Mathf.CeilToInt(Vector3.Distance(a, b) / step);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)n);
                bool gap = (side == 0 || side == 2) && Mathf.Abs(p.x) < 4.5f;
                if (!gap && rng.Chance(0.12f)) gap = true;
                t.wallPosts.Add(p);
                t.wallGaps.Add(gap);
            }
        }

        for (float z = c.FrontZ + 4f; z < c.GateZ - 1f; z += 7f)
            foreach (float x in new[] { -4f, 4f })
                if (Mathf.Abs(z - cz) > CitadelTown.ArenaRadius - 1f) t.lamps.Add(new Vector3(x, 0f, z));

        int crystals = rng.NextInt(6, 10);
        for (int i = 0; i < crystals; i++)
            t.crystals.Add(new Vector3(rng.Range(-hw + 2f, hw - 2f), 0f, cz + rng.Range(-hd + 2f, hd - 2f)));

        for (int i = 0; i < 10; i++)
        {
            float a = (i / 10f) * Mathf.PI * 2f + rng.Range(-0.15f, 0.15f);
            float rad = CitadelTown.ArenaRadius + rng.Range(1f, 4f);
            t.spawnPoints.Add(new Vector3(Mathf.Sin(a) * rad, 0f, cz + Mathf.Cos(a) * rad));
        }
        for (int i = 0; i < 3; i++) t.camp.Add(new Vector3(rng.Range(-6f, 6f), 0f, cz + rng.Range(-5f, 5f)));
        t.bossSpawn = new Vector3(0f, 0f, c.FrontZ + 2.5f);
        r.town = t;
    }

    public static float DistanceToCastle(CastleDesign c, Vector3 p)
    {
        Vector3 a = c.Gate, b = c.Back;
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
        if (Mathf.Abs(p.x) > GroundHalfSize - 10f || Mathf.Abs(p.z) > GroundHalfSize - 10f) return false;
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
