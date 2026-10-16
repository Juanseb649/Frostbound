using System.Linq;
using System.Text;
using UnityEngine;

// Comprobaciones del diseño de niveles (mundo grande, borde, escalera del sendero, escalinata curva, mapas, botín).
public static class LevelChecks
{
    private const int Mask = ~(1 << 2);

    public static string VillageLayoutStats()
    {
        var sb = new StringBuilder();
        float minCastle = 999f, maxCastle = 0f;
        int minB = 99, maxB = 0, minP = 9999, minBorder = 99999, bad = 0;
        for (int seed = 1; seed <= 25; seed++)
        {
            var r = VillageLayout.Generate(seed * 7919, new[] { new Vector3(40, 0, 30), new Vector3(-45, 0, 25), new Vector3(30, 0, -50), new Vector3(-30, 0, -55) });
            float d = new Vector2(r.castle.Foot.x, r.castle.Foot.z).magnitude;
            minCastle = Mathf.Min(minCastle, d); maxCastle = Mathf.Max(maxCastle, d);
            minB = Mathf.Min(minB, r.bonfires.Count); maxB = Mathf.Max(maxB, r.bonfires.Count);
            minP = Mathf.Min(minP, r.pines.Count); minBorder = Mathf.Min(minBorder, r.border.Count);
            float lim = VillageLayout.PlayableHalfSize;
            foreach (Vector3 p in new[] { r.castle.Back, r.castle.Gate }.Concat(r.bonfires.Select(b => b.position)).Concat(r.camps))
                if (Mathf.Abs(p.x) > lim || Mathf.Abs(p.z) > lim) bad++;
        }
        return "castillo " + minCastle.ToString("0") + "-" + maxCastle.ToString("0") + " m · hogueras " + minB + "-" + maxB + " · pinos>=" + minP + " · borde>=" + minBorder + " · fuera de límites " + bad;
    }

    private static bool Down(Vector3 at, out RaycastHit hit) => Physics.Raycast(at + Vector3.up * 4f, Vector3.down, out hit, 12f, Mask, QueryTriggerInteraction.Ignore);

    public static string VillageTrail()
    {
        Vector3 t = VillageLayout.Trailhead;
        var sb = new StringBuilder();
        int holes = 0; float prev = -1f; int drops = 0;
        float run = VillageWorldGenerator.TrailRun, rise = VillageWorldGenerator.TrailRise;
        for (float z = t.z - run - 3f; z <= t.z + 10f; z += 0.25f)
            for (float x = -3.5f; x <= 3.5f; x += 0.5f)
            {
                if (!Down(new Vector3(x, 1f + Mathf.Clamp01((z - (t.z - run)) / run) * rise, z), out RaycastHit h)) { holes++; continue; }
                if (x == -3.5f) { if (h.point.y < prev - 0.05f) drops++; prev = h.point.y; }
            }
        Down(new Vector3(0, rise + 1f, t.z + 4f), out RaycastHit top);
        bool cliff = Physics.Raycast(new Vector3(0, rise + 1.5f, t.z + 6f), Vector3.forward, out RaycastHit c, 20f, Mask, QueryTriggerInteraction.Ignore);
        bool cliffFar = Physics.Raycast(new Vector3(80, 1f, t.z), Vector3.forward, out RaycastHit c2, 30f, Mask, QueryTriggerInteraction.Ignore);
        float b = VillageLayout.PlayableHalfSize;
        int walls = 0;
        foreach (Vector3 dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            if (Physics.Raycast(new Vector3(0, 1f, 0) + dir * (b - 8f), dir, 20f, Mask, QueryTriggerInteraction.Ignore)) walls++;
        return "huecos escalera " + holes + " · bajadas " + drops + " · alto plataforma " + top.point.y.ToString("0.0") + " · acantilado " + (cliff ? c.distance.ToString("0.0") + "m" : "NO") + " · acantilado lateral " + (cliffFar ? c2.distance.ToString("0.0") + "m" : "NO") + " · bordes con muro " + walls + "/4";
    }

    public static string StairsGaps()
    {
        var sb = new StringBuilder();
        int holes = 0, samples = 0, wallMiss = 0;
        for (int i = 1; i < StairsLayout.Terraces; i++)
        {
            StairsLayout.Flight(i, out Vector3 a, out Vector3 b);
            for (float t = 0f; t <= 1f; t += 0.01f)
            {
                Vector3 p = StairsLayout.FlightPoint(a, b, t);
                Vector3 q = StairsLayout.FlightPoint(a, b, Mathf.Min(1f, t + 0.01f));
                Vector3 fwd = q - p; fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.back;
                Vector3 right = Vector3.Cross(Vector3.up, fwd.normalized);
                for (float o = -1.3f; o <= 1.3f; o += 0.65f)
                {
                    samples++;
                    if (!Down(p + right * o, out RaycastHit h) || Mathf.Abs(h.point.y - p.y) > 0.7f) holes++;
                }
                foreach (float s in new[] { -1f, 1f })
                    if (!Physics.Raycast(p + Vector3.up * 1f, right * s, 2.6f, Mask, QueryTriggerInteraction.Ignore)) wallMiss++;
            }
            // Entrada desde la terraza de abajo y salida a la de arriba.
            for (float o = -1.3f; o <= 1.3f; o += 0.65f)
            {
                samples += 2;
                if (!Down(a + new Vector3(o, 0, 1f), out RaycastHit h0) || Mathf.Abs(h0.point.y - a.y) > 0.5f) holes++;
                if (!Down(b + new Vector3(o, 0, -1f), out RaycastHit h1) || Mathf.Abs(h1.point.y - b.y) > 0.5f) holes++;
            }
        }
        return "muestras " + samples + " · huecos " + holes + " · lados sin murete " + wallMiss;
    }

    public static string MapState()
    {
        MapSystem m = MapSystem.Instance;
        if (m == null) return "sin mapa";
        var a = m.CurrentArea;
        return "zona " + (a != null ? a.id + " (" + a.title + ", " + (a.half * 2f).ToString("0") + " m, foto " + (a.map != null) + ")" : "null") + " · marcadores " + m.markers.Count + " · objetivo " + m.ObjectivePosition();
    }

    public static string KillAllForLoot()
    {
        GameObject p = GameObject.Find("Player");
        int killed = 0, before = Object.FindObjectsByType<WorldItem>().Length + Object.FindObjectsByType<CoinPile>().Length;
        foreach (EnemyBrain e in Object.FindObjectsByType<EnemyBrain>())
        {
            if (e.IsDead || killed >= 12) continue;
            Damageable d = e.GetComponent<Damageable>();
            d.TakeHit(new DamageInfo { amount = d.maxHealth * 10f, type = DamageType.Physical, source = p, direction = Vector3.forward });
            killed++;
        }
        return "muertos " + killed + " · antes " + before + " · LootDirector " + (LootDirector.Instance != null);
    }

    public static string LootOnGround() => "objetos " + Object.FindObjectsByType<WorldItem>().Length + " · monedas " + Object.FindObjectsByType<CoinPile>().Length;

    public static string FallTest()
    {
        GameObject p = GameObject.Find("Player");
        var pp = p.GetComponent<PlayerPersistence>();
        Vector3 from = p.transform.position;
        pp.MoveTo(from + new Vector3(0, -40f, 0), p.transform.rotation);
        return "lanzado desde " + from.ToString("F1");
    }

    // Recorre el borde norte del área jugable: nada debe dejar entrar en una cumbre (rayos hacia el norte chocan antes de su malla).
    public static string PeaksSolid()
    {
        int inside = 0, open = 0;
        var peaks = Object.FindObjectsByType<MeshCollider>().Where(c => c.name.Contains("Frostspire")).ToArray();
        for (float x = -VillageLayout.PlayableHalfSize + 2f; x < VillageLayout.PlayableHalfSize; x += 4f)
            for (float z = 200f; z < VillageLayout.PlayableHalfSize; z += 4f)
            {
                Vector3 p = new Vector3(x, 1f, z);
                foreach (var c in peaks)
                    if (c.bounds.Contains(p) && Physics.Raycast(p + Vector3.up * 400f, Vector3.down, out RaycastHit h, 400f, ~(1 << 2), QueryTriggerInteraction.Ignore) && h.collider == c && h.point.y > 1.5f)
                    {
                        // p está bajo la ladera: ¿es alcanzable? (no debe haber camino por el NavMesh)
                        if (UnityEngine.AI.NavMesh.SamplePosition(p, out var nh, 1f, -1) && nh.position.y < 1f) inside++;
                    }
            }
        Vector3 t = VillageLayout.Trailhead;
        var main = GameObject.Find("Frostspire");
        Physics.Raycast(new Vector3(0, 30f, t.z + VillageWorldGenerator.TrailBack + 6f), Vector3.down, out RaycastHit back, 60f, ~(1 << 2), QueryTriggerInteraction.Ignore);
        return "cumbres " + peaks.Length + " · puntos de NavMesh bajo una cumbre " + inside + " · detrás de la plataforma: " + (back.collider != null ? back.collider.name + " y=" + back.point.y.ToString("0.0") : "nada") + " · Frostspire en z=" + (main != null ? main.transform.position.z.ToString("0") : "?");
    }

    public static string Where() { var p = GameObject.Find("Player"); return p.transform.position.ToString("F1"); }
}

public static class LevelWalk
{
    public static string WalkUpTrail()
    {
        GameObject p = GameObject.Find("Player");
        Vector3 t = VillageLayout.Trailhead;
        p.GetComponent<PlayerPersistence>().MoveTo(new Vector3(0, 0.1f, t.z - 24f), Quaternion.identity);
        p.GetComponent<PlayerController>().MoveTo(new Vector3(0, 5f, t.z + 2f));
        return "ok";
    }

    public static string WalkStairs(int flight)
    {
        GameObject p = GameObject.Find("Player");
        StairsLayout.Flight(flight, out Vector3 a, out Vector3 b);
        p.GetComponent<PlayerPersistence>().MoveTo(a + new Vector3(0, 0.1f, 2f), Quaternion.identity);
        p.GetComponent<PlayerController>().MoveTo(b + new Vector3(0, 0, -2.5f));
        return "de " + a.ToString("F1") + " a " + b.ToString("F1");
    }
    public static string WalkStairs1() => WalkStairs(1);
    public static string WalkStairs2() => WalkStairs(2);
    public static string WalkStairs5() => WalkStairs(5);
}

public static class LevelProbe
{
    public static string Ahead()
    {
        GameObject p = GameObject.Find("Player");
        var sb = new StringBuilder();
        foreach (float h in new[] { 0.05f, 0.2f, 0.4f, 0.8f, 1.2f })
        {
            Vector3 o = p.transform.position + Vector3.up * h;
            if (Physics.Raycast(o, Vector3.back, out RaycastHit hit, 3f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                sb.Append(h).Append(":").Append(hit.collider.name).Append("@").Append(hit.distance.ToString("0.00")).Append(" n").Append(hit.normal.ToString("F2")).Append("; ");
            else sb.Append(h).Append(":libre; ");
        }
        Physics.Raycast(p.transform.position + Vector3.up, Vector3.down, out RaycastHit g, 3f, ~(1 << 2), QueryTriggerInteraction.Ignore);
        sb.Append("suelo ").Append(g.collider != null ? g.collider.name + " y=" + g.point.y.ToString("0.00") : "-");
        var col = p.GetComponent<CapsuleCollider>();
        if (col != null) sb.Append(" cápsula r=").Append(col.radius).Append(" h=").Append(col.height).Append(" c=").Append(col.center);
        return sb.ToString();
    }
}

public static class LevelNav
{
    public static string StairPaths()
    {
        var sb = new StringBuilder();
        for (int i = 1; i < StairsLayout.Terraces; i++)
        {
            StairsLayout.Flight(i, out Vector3 a, out Vector3 b);
            var path = new UnityEngine.AI.NavMeshPath();
            UnityEngine.AI.NavMesh.SamplePosition(a + new Vector3(0, 0, 2f), out var ha, 2f, -1);
            UnityEngine.AI.NavMesh.SamplePosition(b + new Vector3(0, 0, -2.5f), out var hb, 2f, -1);
            UnityEngine.AI.NavMesh.CalculatePath(ha.position, hb.position, -1, path);
            int cover = 0;
            for (float t = 0; t <= 1f; t += 0.1f)
                if (UnityEngine.AI.NavMesh.SamplePosition(StairsLayout.FlightPoint(a, b, t), out var h, 0.6f, -1)) cover++;
            sb.Append(i).Append(":").Append(path.status).Append(" esquinas ").Append(path.corners.Length).Append(" navmesh ").Append(cover).Append("/11; ");
        }
        return sb.ToString();
    }
}

public static class LevelState
{
    public static string Mover()
    {
        GameObject p = GameObject.Find("Player");
        var pc = p.GetComponent<PlayerController>();
        var f = typeof(PlayerController).GetField("_clickTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var md = typeof(PlayerController).GetField("_moveDirection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var rb = p.GetComponent<Rigidbody>();
        return p.transform.position.ToString("F2") + " target " + f.GetValue(pc) + " dir " + md.GetValue(pc) + " vel " + rb.linearVelocity.ToString("F2") + " pad " + pc.IsPadPlayer + " diálogo " + pc.InDialogue + " bloqueado " + GameplayInput.Blocked;
    }
}

public static class LevelOverlap
{
    public static string Touching()
    {
        GameObject p = GameObject.Find("Player");
        var col = p.GetComponent<CapsuleCollider>();
        Bounds b = col.bounds;
        var sb = new StringBuilder("cápsula " + b.min.ToString("F2") + "-" + b.max.ToString("F2") + " | ");
        foreach (Collider c in Physics.OverlapBox(b.center + Vector3.back * 0.15f, b.extents + new Vector3(0.05f, 0.0f, 0.05f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.attachedRigidbody == col.attachedRigidbody) continue;
            sb.Append(c.name).Append("(").Append(c.GetType().Name).Append(" ").Append(c.transform.parent != null ? c.transform.parent.name : "").Append(" ").Append(c.bounds.center.ToString("F1")).Append(" sz ").Append(c.bounds.size.ToString("F1")).Append(" rot ").Append(c.transform.eulerAngles.ToString("F0")).Append("); ");
        }
        return sb.ToString();
    }
}

public static class LevelProfile
{
    public static string Profile()
    {
        var sb = new StringBuilder();
        StairsLayout.Flight(1, out Vector3 a, out Vector3 b);
        for (float z = a.z + 1f; z >= a.z - 3f; z -= 0.2f)
        {
            Physics.Raycast(new Vector3(10.8f, 5f, z), Vector3.down, out RaycastHit h, 10f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            sb.Append(z.ToString("0.0")).Append(":").Append(h.point.y.ToString("0.00")).Append(" ");
        }
        return sb.ToString();
    }
}

public static class LevelSlope
{
    public static string MaxSlope()
    {
        float worst = 0f;
        for (int i = 1; i < StairsLayout.Terraces; i++)
        {
            StairsLayout.Flight(i, out Vector3 a, out Vector3 b);
            for (float t = 0f; t <= 1f; t += 0.01f)
            {
                Vector3 p = StairsLayout.FlightPoint(a, b, t);
                foreach (float o in new[] { -1.4f, 0f, 1.4f })
                {
                    Physics.Raycast(p + Vector3.up * 3f + new Vector3(o, 0, 0), Vector3.down, out RaycastHit h, 8f, ~(1 << 2), QueryTriggerInteraction.Ignore);
                    if (h.collider != null) worst = Mathf.Max(worst, Vector3.Angle(h.normal, Vector3.up));
                }
            }
        }
        return "pendiente máxima " + worst.ToString("0.0") + "°";
    }
}
