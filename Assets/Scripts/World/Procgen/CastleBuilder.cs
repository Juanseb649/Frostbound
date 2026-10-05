using System.Collections.Generic;
using UnityEngine;

// Construye por código el castillo gótico corrompido por el Frost (exterior) a partir de VillageLayout.CastleDesign:
// meseta rocosa con viaducto de arcos (Las Lajas), catedral de piedra oscura con nave, arbotantes dobles,
// un bosque de agujas finísimas (Irithyll / Anor Londo), torreones en las esquinas, vidrieras que brillan
// en cobalto y espinas de hielo negro brotando por todas partes. Una malla por material; colisiones aparte.
public class CastleBuilder : MonoBehaviour
{
    [Header("Materiales")]
    public Material stone;
    public Material trim;
    public Material roof;
    public Material glass;
    public Material gold;
    public Material rock;
    public Material snow;
    public Material ice;
    public Material door;
    public Material lampGlow;

    [Header("Mallas")]
    public Mesh cone;
    public Mesh prism;

    private Transform _visual, _colliders;
    private VillageLayout.CastleDesign _d;
    private DeterministicRng _rng;
    private float _floor;

    public void Build(VillageLayout.CastleDesign design, VillageLayout.CitadelTown town = null)
    {
        _d = design;
        _rng = new DeterministicRng(design.detailSeed);
        transform.SetPositionAndRotation(design.position, Quaternion.Euler(0f, design.yaw, 0f));
        _visual = Child("Piezas");
        _colliders = Child("Colisiones");
        _floor = design.plateauHeight + 0.6f;

        BuildPlateau();
        BuildViaduct();
        BuildNave();
        BuildFacade();
        if (design.transept) BuildTransept();
        BuildApse();
        BuildCornerTurrets();
        BuildCrystals();
        if (town != null) BuildTown(town);
        Combine();
    }

    // ---------- Poblado en ruinas al pie del castillo ----------

    private void BuildTown(VillageLayout.CitadelTown town)
    {
        _rng = new DeterministicRng(SeedUtil.Combine(_d.detailSeed, 77));
        float cz = _d.TownCenterZ;

        // Avenida y plaza empedradas en piedra oscura.
        float z0 = _d.FrontZ - 0.5f, z1 = _d.GateZ + 2f;
        Box(new Vector3(0f, 0.03f, (z0 + z1) * 0.5f), Vector3.zero, new Vector3(7f, 0.06f, z1 - z0), trim, false);
        Cylinder(new Vector3(0f, 0.035f, cz), VillageLayout.CitadelTown.ArenaRadius * 2f + 2f, 0.07f, trim);
        Cylinder(new Vector3(0f, 0.04f, cz), VillageLayout.CitadelTown.ArenaRadius * 2f - 1f, 0.07f, rock);
        for (int k = 0; k < 16; k++)
            Box(new Vector3(0f, 0.08f, cz), new Vector3(0f, k * 11.25f, 0f), new Vector3(0.14f, 0.04f, VillageLayout.CitadelTown.ArenaRadius * 2f - 1.2f), trim, false);

        // Estatua rota del guardián en el centro de la plaza.
        Cylinder(new Vector3(0f, 0.6f, cz), 3f, 1.2f, stone, true);
        Cylinder(new Vector3(0f, 1.5f, cz), 2f, 0.6f, trim);
        Box(new Vector3(0f, 2.6f, cz), new Vector3(0f, 20f, 6f), new Vector3(1.2f, 1.8f, 0.9f), stone, false);
        Box(new Vector3(1.4f, 0.35f, cz + 1.9f), new Vector3(70f, 30f, 10f), new Vector3(0.9f, 1.2f, 0.8f), stone, false);
        Shards(new Vector3(0f, 1.6f, cz), 4, 1f, 2.6f, 40f);

        foreach (VillageLayout.Ruin r in town.ruins) BuildRuin(r);

        // Muralla rota.
        int n = town.wallPosts.Count;
        for (int i = 0; i < n; i++)
        {
            if (town.wallGaps[i]) continue;
            Vector3 a = town.wallPosts[i], b = town.wallPosts[(i + 1) % n];
            Vector3 d = b - a;
            float h = _rng.Range(1.6f, 3.4f);
            Quaternion rot = Quaternion.LookRotation(d.normalized);
            Box((a + b) * 0.5f + Vector3.up * h * 0.5f, rot.eulerAngles, new Vector3(0.9f, h, d.magnitude + 0.1f), stone, true);
            Box((a + b) * 0.5f + Vector3.up * (h + 0.06f), rot.eulerAngles, new Vector3(1f, 0.12f, d.magnitude + 0.1f), snow, false);
            if (_rng.Chance(0.25f))
                Box(a + Vector3.up * (h + 0.4f), new Vector3(0f, rot.eulerAngles.y, 0f), new Vector3(1.1f, 0.8f, 1.1f), trim, false);
            if (_rng.Chance(0.2f))
                Box(a + rot * new Vector3(_rng.Chance(0.5f) ? 1.4f : -1.4f, 0.3f, 0f), new Vector3(_rng.Range(0f, 40f), _rng.Range(0f, 90f), 0f), new Vector3(1f, 0.6f, 0.8f), stone, false);
        }

        // Torres de la puerta, con el arco derrumbado.
        float gz = _d.GateZ;
        foreach (float side in new[] { -1f, 1f })
        {
            float th = side < 0 ? 8.5f : 5.5f;
            Box(new Vector3(side * 5.6f, th * 0.5f, gz), Vector3.zero, new Vector3(2.6f, th, 2.6f), stone, true);
            Box(new Vector3(side * 5.6f, th + 0.2f, gz), Vector3.zero, new Vector3(3f, 0.4f, 3f), trim, false);
            if (side < 0) Spirelet(new Vector3(side * 5.6f, th + 0.4f, gz), 1.6f, 6f);
            else Shards(new Vector3(side * 5.6f, th, gz), 4, 1.2f, 3f, 40f);
        }
        Box(new Vector3(-2.5f, 7.4f, gz), new Vector3(0f, 0f, -8f), new Vector3(6.5f, 0.9f, 1.4f), trim, false);
        Box(new Vector3(2.8f, 0.5f, gz + 1.2f), new Vector3(15f, 25f, 75f), new Vector3(3.5f, 0.9f, 1.4f), trim, false);

        foreach (Vector3 l in town.lamps)
        {
            bool fallen = _rng.Chance(0.3f);
            if (fallen)
            {
                Box(l + new Vector3(0.9f, 0.15f, 0f), new Vector3(0f, _rng.Range(0f, 180f), 88f), new Vector3(0.18f, 3.2f, 0.18f), trim, false);
                continue;
            }
            Box(l + Vector3.up * 1.6f, Vector3.zero, new Vector3(0.2f, 3.2f, 0.2f), trim, true);
            Box(l + Vector3.up * 3.3f, Vector3.zero, new Vector3(0.5f, 0.6f, 0.5f), glass, false);
            Cone(l + Vector3.up * 3.6f, 0.75f, 0.9f, roof);
        }

        foreach (Vector3 c in town.crystals) Shards(c, _rng.NextInt(3, 7), 1.2f, 4.5f, 35f);
    }

    private void BuildRuin(VillageLayout.Ruin r)
    {
        Quaternion q = Quaternion.Euler(0f, r.yaw, 0f);
        Vector3 Local(float x, float y, float z) => r.local + q * new Vector3(x, y, z);
        Vector3 E(Vector3 e) => (q * Quaternion.Euler(e)).eulerAngles;
        Material wall = r.burnt ? roof : stone;
        float w = r.width, d = r.depth, h = r.state == 2 ? _rng.Range(0.8f, 1.6f) : r.height;
        const float t = 0.4f;

        Box(Local(0f, 0.12f, 0f), E(Vector3.zero), new Vector3(w + 0.4f, 0.24f, d + 0.4f), trim, false);
        // Paredes: la delantera (hacia la plaza) con hueco de puerta; las demás con remates irregulares.
        for (int side = 0; side < 4; side++)
        {
            bool front = side == 0;
            float len = side % 2 == 0 ? w : d;
            Vector3 c = side == 0 ? new Vector3(0f, 0f, d * 0.5f) : side == 1 ? new Vector3(w * 0.5f, 0f, 0f) : side == 2 ? new Vector3(0f, 0f, -d * 0.5f) : new Vector3(-w * 0.5f, 0f, 0f);
            float yaw = side * 90f;
            Quaternion sq = Quaternion.Euler(0f, yaw, 0f);
            int segs = Mathf.Max(2, Mathf.RoundToInt(len / 1.4f));
            for (int k = 0; k < segs; k++)
            {
                float u = -len * 0.5f + (k + 0.5f) * len / segs;
                if (front && Mathf.Abs(u) < 0.9f) continue;
                float sh = r.state == 0 ? h : h * _rng.Range(0.35f, 1f);
                Vector3 p = c + sq * new Vector3(u, sh * 0.5f, 0f);
                Box(Local(p.x, p.y, p.z), E(new Vector3(0f, yaw, 0f)), new Vector3(len / segs + 0.02f, sh, t), wall, true);
                if (r.state != 2 && k % 2 == 1 && sh > 2.4f && !front)
                {
                    Vector3 win = c + sq * new Vector3(u, sh * 0.62f, 0.22f);
                    Box(Local(win.x, win.y, win.z), E(new Vector3(0f, yaw, 0f)), new Vector3(0.6f, 0.9f, 0.06f), _rng.Chance(0.25f) ? glass : door, false);
                }
            }
            if (front && r.state != 2)
            {
                Vector3 lintel = c + new Vector3(0f, 2.3f, 0f);
                Box(Local(lintel.x, lintel.y, lintel.z), E(new Vector3(0f, yaw, 0f)), new Vector3(2.2f, 0.3f, t + 0.1f), door, false);
            }
        }
        // Tejado: medio tejado hundido y vigas sueltas.
        if (r.state == 0)
        {
            Prism(Local(-w * 0.25f, h, 0f), E(Vector3.zero), new Vector3(w * 0.55f, 2.2f, d + 0.6f), roof);
            for (int k = 0; k < 3; k++)
                Box(Local(w * 0.2f, h + 0.9f, -d * 0.3f + k * d * 0.3f), E(new Vector3(0f, 0f, 28f)), new Vector3(w * 0.6f, 0.22f, 0.22f), door, false);
        }
        else if (r.state == 1)
        {
            Box(Local(0f, h * 0.6f, 0f), E(new Vector3(_rng.Range(-20f, 20f), 0f, 35f)), new Vector3(w * 0.9f, 0.22f, 0.22f), door, false);
        }
        int rubble = r.state == 2 ? 6 : 3;
        for (int k = 0; k < rubble; k++)
        {
            Vector3 p = new Vector3(_rng.Range(-w * 0.5f, w * 0.5f), 0.25f, _rng.Range(-d * 0.5f, d * 0.5f));
            Box(Local(p.x, p.y, p.z), E(new Vector3(_rng.Range(-30f, 30f), _rng.Range(0f, 90f), _rng.Range(-30f, 30f))), new Vector3(_rng.Range(0.5f, 1.2f), _rng.Range(0.3f, 0.7f), _rng.Range(0.5f, 1f)), stone, false);
        }
        if (_rng.Chance(0.5f)) Shards(Local(_rng.Range(-w * 0.4f, w * 0.4f), 0f, -d * 0.5f), _rng.NextInt(2, 5), 1f, 3f, 40f);
    }

    // ---------- Meseta y viaducto ----------

    private void BuildPlateau()
    {
        float h = _d.plateauHeight, front = VillageLayout.CastleDesign.Forecourt, back = _d.BackZ, w = VillageLayout.CastleDesign.HalfWidth;
        float depth = front - back, cz = (front + back) * 0.5f;
        Box(new Vector3(0f, h * 0.5f, cz), Vector3.zero, new Vector3(w * 2f, h, depth), rock, true);
        Box(new Vector3(0f, h + 0.04f, cz), Vector3.zero, new Vector3(w * 2f - 0.6f, 0.12f, depth - 0.6f), snow, false);

        int rocks = 26;
        for (int i = 0; i < rocks; i++)
        {
            float t = i / (float)rocks;
            Vector3 p = PerimeterPoint(t, w, front, back);
            Vector3 s = new Vector3(_rng.Range(3f, 6f), h * _rng.Range(0.55f, 1.05f), _rng.Range(3f, 6f));
            p.y = s.y * 0.5f - 0.3f;
            Box(p, new Vector3(_rng.Range(-8f, 8f), _rng.Range(0f, 90f), _rng.Range(-8f, 8f)), s, rock, false);
            if (_rng.Chance(0.5f)) Box(p + Vector3.up * (s.y * 0.5f + 0.05f), new Vector3(0f, _rng.Range(0f, 90f), 0f), new Vector3(s.x * 0.8f, 0.2f, s.z * 0.8f), snow, false);
        }

        Box(new Vector3(0f, h + 0.08f, front * 0.5f), Vector3.zero, new Vector3(20f, 0.1f, front), trim, false);
        Balustrade(new Vector3(-w + 0.4f, h, front), new Vector3(-w + 0.4f, h, back), true);
        Balustrade(new Vector3(w - 0.4f, h, front), new Vector3(w - 0.4f, h, back), true);
        Balustrade(new Vector3(-w + 0.4f, h, back), new Vector3(w - 0.4f, h, back), true);
        Balustrade(new Vector3(-w + 0.4f, h, front - 0.3f), new Vector3(-3.4f, h, front - 0.3f), true);
        Balustrade(new Vector3(3.4f, h, front - 0.3f), new Vector3(w - 0.4f, h, front - 0.3f), true);
    }

    private Vector3 PerimeterPoint(float t, float w, float front, float back)
    {
        float depth = front - back, per = 2f * (2f * w + depth), d = t * per;
        if (d < 2f * w) return new Vector3(-w + d, 0f, front);
        d -= 2f * w;
        if (d < depth) return new Vector3(w, 0f, front - d);
        d -= depth;
        if (d < 2f * w) return new Vector3(w - d, 0f, back);
        d -= 2f * w;
        return new Vector3(-w, 0f, back + d);
    }

    private void BuildViaduct()
    {
        float h = _d.plateauHeight, len = _d.ViaductLength, z0 = VillageLayout.CastleDesign.Forecourt, z1 = z0 + len;
        float slope = Mathf.Atan2(h, len) * Mathf.Rad2Deg;
        float deckLen = Mathf.Sqrt(len * len + h * h) + 0.8f;
        Vector3 mid = new Vector3(0f, h * 0.5f - 0.2f, (z0 + z1) * 0.5f);
        Box(mid, new Vector3(slope, 0f, 0f), new Vector3(6f, 0.5f, deckLen), trim, true);
        Box(mid + Vector3.up * 0.27f, new Vector3(slope, 0f, 0f), new Vector3(5.4f, 0.06f, deckLen - 0.4f), snow, false);
        foreach (float side in new[] { -1f, 1f })
        {
            Box(mid + new Vector3(side * 2.85f, 0.95f, 0f), new Vector3(slope, 0f, 0f), new Vector3(0.3f, 0.2f, deckLen), stone, true);
            Box(mid + new Vector3(side * 2.85f, 0.5f, 0f), new Vector3(slope, 0f, 0f), new Vector3(0.22f, 0.9f, deckLen), stone, false);
        }

        float span = 4.6f;
        int pillars = Mathf.Max(2, Mathf.FloorToInt((len - 2f) / span));
        float prevZ = z0, prevH = h;
        for (int i = 1; i <= pillars; i++)
        {
            float z = z0 + i * (len - 2f) / pillars;
            float deckY = Mathf.Lerp(h, 0f, (z - z0) / len) - 0.45f;
            if (deckY < 0.6f) break;
            Box(new Vector3(0f, deckY * 0.5f, z), Vector3.zero, new Vector3(5.4f, deckY, 1.3f), stone, true);
            ArchFaces((prevZ + z) * 0.5f, (z - prevZ) * 0.5f - 0.65f, Mathf.Min(prevH, deckY));
            prevZ = z;
            prevH = deckY;
        }

        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 lamp = new Vector3(side * 3.6f, 0f, z1 + 0.6f);
            Box(lamp + Vector3.up * 1.4f, Vector3.zero, new Vector3(0.2f, 2.8f, 0.2f), trim, true);
            Box(lamp + Vector3.up * 2.95f, Vector3.zero, new Vector3(0.55f, 0.55f, 0.55f), lampGlow, false);
            Cone(lamp + Vector3.up * 3.2f, 0.7f, 0.6f, roof);
        }
    }

    private void ArchFaces(float zCenter, float radius, float springTop)
    {
        if (radius < 0.8f || springTop < radius + 0.6f) return;
        float cy = springTop - radius - 0.1f;
        foreach (float side in new[] { -2.75f, 2.75f })
        {
            const int stones = 9;
            for (int k = 0; k < stones; k++)
            {
                float a = Mathf.PI * (k + 0.5f) / stones;
                Vector3 p = new Vector3(side, cy + Mathf.Sin(a) * radius, zCenter - Mathf.Cos(a) * radius);
                Box(p, new Vector3(a * Mathf.Rad2Deg - 90f, 0f, 0f), new Vector3(0.5f, 0.45f, radius * Mathf.PI / stones + 0.05f), trim, false);
            }
        }
    }

    private void Balustrade(Vector3 a, Vector3 b, bool collider)
    {
        Vector3 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.5f) return;
        Quaternion rot = Quaternion.LookRotation(dir.normalized);
        Vector3 mid = (a + b) * 0.5f;
        Box(mid + Vector3.up * 1.0f, rot.eulerAngles, new Vector3(0.3f, 0.18f, len), stone, collider, 1.4f);
        int posts = Mathf.Max(2, Mathf.RoundToInt(len / 2.2f));
        for (int i = 0; i <= posts; i++)
            Box(Vector3.Lerp(a, b, i / (float)posts) + Vector3.up * 0.5f, rot.eulerAngles, new Vector3(0.32f, 1f, 0.32f), trim, false);
    }

    // ---------- Catedral ----------

    private const float NaveWidth = 9f;
    private const float AisleWidth = 4.5f;
    private const float AisleHeight = 10f;

    private void BuildNave()
    {
        float L = _d.NaveLength, H = _d.naveHeight, f = _floor;
        float outer = NaveWidth * 0.5f + AisleWidth;
        Box(new Vector3(0f, _d.plateauHeight + 0.3f, -L * 0.5f + 0.25f), Vector3.zero, new Vector3(outer * 2f + 2f, 0.6f, L + 1.5f), trim, true);
        for (int s = 0; s < 3; s++)
            Box(new Vector3(0f, _d.plateauHeight + 0.1f + s * 0.2f, 1.4f + (2 - s) * 0.45f), Vector3.zero, new Vector3(5f, 0.2f, 0.5f), trim, false);

        Box(new Vector3(0f, f + H * 0.5f, -L * 0.5f), Vector3.zero, new Vector3(NaveWidth, H, L), stone, true);
        Prism(new Vector3(0f, f + H, -L * 0.5f), new Vector3(0f, 90f, 0f), new Vector3(L + 0.6f, 7f, NaveWidth + 1f), roof);
        Box(new Vector3(0f, f + H + 7.02f, -L * 0.5f), Vector3.zero, new Vector3(0.5f, 0.12f, L + 0.4f), snow, false);
        // Cresta de pinchos sobre la cumbrera.
        for (float z = -1.5f; z > -L + 0.5f; z -= 1.6f)
            Cone(new Vector3(0f, f + H + 6.9f, z), 0.28f, 1.3f, trim);

        foreach (float side in new[] { -1f, 1f })
        {
            float ax = side * (NaveWidth * 0.5f + AisleWidth * 0.5f);
            Box(new Vector3(ax, f + AisleHeight * 0.5f, -L * 0.5f), Vector3.zero, new Vector3(AisleWidth, AisleHeight, L), stone, true);
            float rise = 2.6f, w = Mathf.Sqrt(AisleWidth * AisleWidth + rise * rise);
            float tilt = -Mathf.Atan2(rise, AisleWidth) * Mathf.Rad2Deg * side;
            Box(new Vector3(ax, f + AisleHeight + rise * 0.5f, -L * 0.5f), new Vector3(0f, 0f, tilt), new Vector3(w + 0.4f, 0.3f, L + 0.4f), roof, false);

            // Carámbanos negros colgando del alero.
            for (float z = -0.8f; z > -L; z -= 1.1f)
                if (_rng.Chance(0.7f))
                    Icicle(new Vector3(side * (outer + 0.15f), f + AisleHeight - 0.05f, z), _rng.Range(0.6f, 1.8f));

            for (int i = 0; i < _d.bays; i++)
            {
                float z = -2.5f - 5f * i;
                LancetOn(new Vector3(side * (NaveWidth * 0.5f + 0.06f), f + AisleHeight + 2.6f + 2.4f, z), side > 0 ? Vector3.right : Vector3.left, 1.3f, 4.8f, glass);
                LancetOn(new Vector3(side * (outer + 0.06f), f + AisleHeight * 0.42f, z), side > 0 ? Vector3.right : Vector3.left, 1.1f, 4.2f, glass);
            }
            for (int i = 0; i <= _d.bays; i++)
            {
                float z = -5f * i - (i == _d.bays ? -0.5f : 0f);
                if (i == 0) z = -0.3f;
                float pierX = side * (outer + 0.8f), pierH = AisleHeight + 5f;
                Box(new Vector3(pierX, f + pierH * 0.5f, z), Vector3.zero, new Vector3(1.6f, pierH, 1.4f), stone, false);
                Box(new Vector3(pierX + side * 0.5f, f + pierH * 0.3f, z), new Vector3(0f, 0f, side * 12f), new Vector3(1.2f, pierH * 0.6f, 1.2f), stone, false);
                // Agujas finas sobre cada contrafuerte: el bosque de pinchos de Irithyll.
                Spirelet(new Vector3(pierX, f + pierH, z), 1.25f, _rng.Range(7f, 10.5f));
                Vector3 from = new Vector3(side * (outer + 0.3f), f + pierH - 1f, z);
                Vector3 to = new Vector3(side * (NaveWidth * 0.5f), f + H - 2.6f, z);
                Strut(from, to, 0.55f, 0.6f, trim);
                Strut(from + Vector3.down * 3.2f, to + Vector3.down * 5.5f, 0.45f, 0.5f, trim);
                Spirelet(new Vector3(side * (NaveWidth * 0.5f + 0.2f), f + H, z), 0.8f, _rng.Range(4f, 6f));
            }
        }
    }

    private void BuildFacade()
    {
        float H = _d.naveHeight, f = _floor;
        float fw = NaveWidth + 0.8f;
        Box(new Vector3(0f, f + H * 0.5f, 0.6f), Vector3.zero, new Vector3(fw, H, 1.2f), stone, false);
        Prism(new Vector3(0f, f + H, 0.6f), new Vector3(0f, 90f, 0f), new Vector3(1.2f, 8f, fw), stone);
        Spirelet(new Vector3(0f, f + H + 8f, 0.6f), 1f, 6f);
        Spirelet(new Vector3(-fw * 0.5f, f + H, 0.6f), 0.9f, 5f);
        Spirelet(new Vector3(fw * 0.5f, f + H, 0.6f), 0.9f, 5f);

        // Portal: arquivoltas apuntadas en escalones (la puerta y su sello se colocan aparte).
        float z = 1.24f;
        for (int k = 0; k < 4; k++)
        {
            float wv = 4.6f - k * 0.55f, hv = 7.6f - k * 0.5f, zz = z + 0.35f - k * 0.1f;
            foreach (float side in new[] { -1f, 1f })
            {
                Box(new Vector3(side * wv * 0.5f, f + hv * 0.4f, zz), Vector3.zero, new Vector3(0.38f, hv * 0.8f, 0.4f), k % 2 == 0 ? trim : stone, false);
                Box(new Vector3(side * wv * 0.26f, f + hv * 0.8f + wv * 0.18f, zz), new Vector3(0f, 0f, side * -38f), new Vector3(wv * 0.62f, 0.38f, 0.4f), k % 2 == 0 ? trim : stone, false);
            }
        }
        Box(new Vector3(0f, f + 2.9f, z), Vector3.zero, new Vector3(2.6f, 5.8f, 0.12f), door, false);
        Box(new Vector3(0f, f + 3.1f, z - 0.2f), Vector3.zero, new Vector3(3.2f, 6.2f, 0.4f), stone, false);

        float roseY = f + H - 5.2f;
        Disc(new Vector3(0f, roseY, z - 0.02f), 6.2f, 0.12f, trim);
        Disc(new Vector3(0f, roseY, z + 0.02f), 5.2f, 0.12f, glass);
        for (int k = 0; k < 12; k++)
            Box(new Vector3(0f, roseY, z + 0.1f), new Vector3(0f, 0f, k * 15f), new Vector3(0.14f, 5.2f, 0.1f), trim, false);
        Disc(new Vector3(0f, roseY, z + 0.12f), 1.4f, 0.12f, trim);
        Disc(new Vector3(0f, roseY, z + 0.16f), 0.7f, 0.12f, ice);
        // Galería de reyes bajo el rosetón: hornacinas oscuras.
        for (int k = -3; k <= 3; k++)
            LancetOn(new Vector3(k * 1.25f, f + H - 10.4f, z + 0.02f), Vector3.forward, 0.7f, 1.8f, trim);

        float towerX = NaveWidth * 0.5f + 2.8f;
        foreach (float side in new[] { -1f, 1f }) Tower(new Vector3(side * towerX, 0f, -1.6f), 5.4f, _d.sideTowerHeight, _d.spireRatio, false);
        Tower(new Vector3(0f, 0f, -3.4f), 5.8f, _d.centralTowerHeight, _d.spireRatio * 0.9f, true);
    }

    // Torre de dos cuerpos: base cuadrada con contrafuertes, campanario octogonal y una aguja altísima
    // rodeada de agujas menores (perfil de Irithyll / Anor Londo).
    private void Tower(Vector3 basePos, float size, float height, float spireRatio, bool central)
    {
        float f = _floor;
        float startY = central ? f + _d.naveHeight - 1f : f;
        float squareTop = f + height * 0.68f;
        float body = squareTop - startY;
        Box(new Vector3(basePos.x, startY + body * 0.5f, basePos.z), Vector3.zero, new Vector3(size, body, size), stone, !central);
        Box(new Vector3(basePos.x, squareTop + 0.2f, basePos.z), Vector3.zero, new Vector3(size + 0.6f, 0.4f, size + 0.6f), trim, false);

        float half = size * 0.5f + 0.06f;
        Vector3[] faces = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        foreach (Vector3 n in faces)
        {
            Vector3 c = new Vector3(basePos.x, 0f, basePos.z) + n * half;
            bool sideways = Mathf.Abs(n.x) > 0.5f;
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 offset = (sideways ? Vector3.forward : Vector3.right) * k * size * 0.21f;
                LancetOn(c + offset + Vector3.up * (squareTop - 5f), n, 0.8f, 4.2f, glass);
            }
            if (!central)
            {
                LancetOn(c + Vector3.up * (f + height * 0.3f), n, 0.8f, 3f, glass);
                Box(c + Vector3.up * (f + height * 0.48f), Quaternion.LookRotation(n).eulerAngles, new Vector3(size * 0.9f, 0.3f, 0.3f), trim, false);
            }
        }

        float corner = size * 0.5f;
        for (int cx = -1; cx <= 1; cx += 2)
            for (int cz = -1; cz <= 1; cz += 2)
            {
                Vector3 p = new Vector3(basePos.x + cx * corner, 0f, basePos.z + cz * corner);
                Box(new Vector3(p.x, startY + body * 0.5f, p.z), new Vector3(0f, 45f, 0f), new Vector3(0.8f, body, 0.8f), trim, false);
                Spirelet(new Vector3(p.x, squareTop + 0.4f, p.z), 1f, height * 0.22f);
            }

        // Campanario octogonal.
        float belfryH = height * 0.2f, belfryD = size * 0.78f;
        Cylinder(new Vector3(basePos.x, squareTop + 0.4f + belfryH * 0.5f, basePos.z), belfryD, belfryH, stone);
        for (int k = 0; k < 8; k++)
        {
            float a = k * 45f * Mathf.Deg2Rad;
            Vector3 n = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            LancetOn(new Vector3(basePos.x, squareTop + 0.4f + belfryH * 0.45f, basePos.z) + n * (belfryD * 0.5f + 0.02f), n, 0.6f, belfryH * 0.55f, glass);
            Spirelet(new Vector3(basePos.x, squareTop + 0.4f + belfryH, basePos.z) + n * (belfryD * 0.5f), 0.5f, 2.6f);
        }
        float top = squareTop + 0.4f + belfryH;
        Cylinder(new Vector3(basePos.x, top + 0.3f, basePos.z), belfryD + 0.4f, 0.6f, trim);

        float spireH = height * spireRatio;
        Cone(new Vector3(basePos.x, top + 0.6f, basePos.z), belfryD * 0.92f, spireH, roof);
        // Bandas a media aguja.
        Cylinder(new Vector3(basePos.x, top + 0.6f + spireH * 0.3f, basePos.z), belfryD * 0.92f * 0.72f, 0.35f, trim);
        Cylinder(new Vector3(basePos.x, top + 0.6f + spireH * 0.6f, basePos.z), belfryD * 0.92f * 0.42f, 0.3f, trim);
        Vector3 tip = new Vector3(basePos.x, top + 0.6f + spireH, basePos.z);
        if (central)
        {
            // Corona de cristal del Frost: late con luz cobalto.
            Box(tip + Vector3.up * 1.6f, new Vector3(0f, 45f, 18f), new Vector3(0.7f, 3.4f, 0.7f), ice, false);
            Box(tip + Vector3.up * 1.0f, new Vector3(0f, 10f, -38f), new Vector3(0.4f, 1.8f, 0.4f), ice, false);
            Box(tip + Vector3.up * 1.1f, new Vector3(25f, 80f, 30f), new Vector3(0.35f, 1.6f, 0.35f), ice, false);
        }
        else
        {
            Box(tip + Vector3.up * 0.6f, Vector3.zero, new Vector3(0.12f, 1.4f, 0.12f), gold, false);
            Box(tip + Vector3.up * 0.9f, Vector3.zero, new Vector3(0.7f, 0.12f, 0.12f), gold, false);
        }
    }

    private void BuildTransept()
    {
        float L = _d.NaveLength, H = _d.naveHeight, f = _floor;
        float z = -L * 0.62f, width = NaveWidth + 2f * AisleWidth + 8f;
        Box(new Vector3(0f, f + H * 0.5f, z), Vector3.zero, new Vector3(width, H, 8f), stone, true);
        Prism(new Vector3(0f, f + H, z), Vector3.zero, new Vector3(width + 0.6f, 7f, 9f), roof);
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * (width * 0.5f + 0.06f);
            Vector3 c = new Vector3(x, f + H - 4.6f, z);
            Disc(c, 4.8f, 0.12f, trim, true);
            Disc(c + Vector3.right * side * 0.04f, 4f, 0.12f, glass, true);
            LancetOn(new Vector3(x, f + 4.4f, z), Vector3.right * side, 1.6f, 6f, glass);
            Spirelet(new Vector3(x, f + H, z + 3.6f), 1f, 7f);
            Spirelet(new Vector3(x, f + H, z - 3.6f), 1f, 7f);
            Spirelet(new Vector3(x, f + H + 6.5f, z), 0.8f, 4f);
        }
        // Flecha del crucero: aguja calada muy fina.
        Cylinder(new Vector3(0f, f + H + 6.6f, z), 2.6f, 2.4f, stone);
        for (int k = 0; k < 6; k++)
        {
            float a = k * 60f * Mathf.Deg2Rad;
            Spirelet(new Vector3(Mathf.Sin(a) * 1.3f, f + H + 7.8f, z + Mathf.Cos(a) * 1.3f), 0.4f, 3f);
        }
        Cone(new Vector3(0f, f + H + 7.8f, z), 2.2f, 17f, roof);
        Box(new Vector3(0f, f + H + 25.3f, z), new Vector3(0f, 45f, 0f), new Vector3(0.45f, 1.3f, 0.45f), ice, false);
    }

    private void BuildApse()
    {
        float L = _d.NaveLength, f = _floor, h = _d.naveHeight - 2f;
        Cylinder(new Vector3(0f, f + h * 0.5f, -L), NaveWidth, h, stone, true);
        Cone(new Vector3(0f, f + h, -L), NaveWidth + 0.8f, 7f, roof);
        for (int k = 0; k < 6; k++)
        {
            float a = Mathf.Lerp(-75f, 75f, k / 5f) * Mathf.Deg2Rad;
            Vector3 n = new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a));
            LancetOn(new Vector3(0f, f + h * 0.55f, -L) + n * (NaveWidth * 0.5f + 0.04f), n, 0.9f, 5f, glass);
            Spirelet(new Vector3(0f, f + h, -L) + n * (NaveWidth * 0.5f + 0.3f), 0.7f, 4.5f);
        }
        for (int i = 0; i < _d.chapels; i++)
        {
            float t = _d.chapels == 1 ? 0.5f : i / (float)(_d.chapels - 1);
            float a = Mathf.Lerp(-70f, 70f, t) * Mathf.Deg2Rad;
            Vector3 p = new Vector3(0f, 0f, -L) + new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)) * (NaveWidth * 0.5f + 1.6f);
            Cylinder(new Vector3(p.x, f + 3.5f, p.z), 3.4f, 7f, stone);
            Cone(new Vector3(p.x, f + 7f, p.z), 3.8f, 5f, roof);
            Box(new Vector3(p.x, f + 12.3f, p.z), Vector3.zero, new Vector3(0.12f, 0.8f, 0.12f), gold, false);
        }
    }

    // Torreones en las esquinas de la meseta: cilindros con almenas y tejado cónico muy afilado.
    private void BuildCornerTurrets()
    {
        float w = VillageLayout.CastleDesign.HalfWidth - 1.6f, h0 = _d.plateauHeight;
        Vector3[] spots =
        {
            new Vector3(-w, 0f, VillageLayout.CastleDesign.Forecourt - 1.5f), new Vector3(w, 0f, VillageLayout.CastleDesign.Forecourt - 1.5f),
            new Vector3(-w, 0f, _d.BackZ + 1.6f), new Vector3(w, 0f, _d.BackZ + 1.6f)
        };
        foreach (Vector3 s in spots)
        {
            float th = _rng.Range(13f, 18f);
            Cylinder(new Vector3(s.x, h0 + th * 0.5f, s.z), 3.6f, th, stone, true);
            Cylinder(new Vector3(s.x, h0 + th + 0.3f, s.z), 4.2f, 0.6f, trim);
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f * Mathf.Deg2Rad;
                Box(new Vector3(s.x + Mathf.Sin(a) * 1.9f, h0 + th + 1f, s.z + Mathf.Cos(a) * 1.9f), new Vector3(0f, k * 45f, 0f), new Vector3(0.7f, 0.9f, 0.5f), stone, false);
            }
            LancetOn(new Vector3(s.x, h0 + th * 0.6f, s.z) + Vector3.forward * 1.82f, Vector3.forward, 0.5f, 1.8f, glass);
            Cone(new Vector3(s.x, h0 + th + 0.6f, s.z), 3.4f, _rng.Range(9f, 13f), roof);
        }
        // Muros almenados entre los torreones de delante.
        foreach (float side in new[] { -1f, 1f })
        {
            float x0 = side * 4.2f, x1 = side * (w - 1.8f);
            Box(new Vector3((x0 + x1) * 0.5f, h0 + 2.2f, VillageLayout.CastleDesign.Forecourt - 1.5f), Vector3.zero, new Vector3(Mathf.Abs(x1 - x0), 4.4f, 1f), stone, true);
            for (float x = Mathf.Min(x0, x1) + 0.4f; x < Mathf.Max(x0, x1); x += 1.2f)
                Box(new Vector3(x, h0 + 4.75f, VillageLayout.CastleDesign.Forecourt - 1.5f), Vector3.zero, new Vector3(0.6f, 0.7f, 1f), stone, false);
            Box(new Vector3(side * 3.6f, h0 + 4f, VillageLayout.CastleDesign.Forecourt - 1.5f), Vector3.zero, new Vector3(1.4f, 8f, 1.4f), trim, false);
            Spirelet(new Vector3(side * 3.6f, h0 + 8f, VillageLayout.CastleDesign.Forecourt - 1.5f), 1.1f, 5f);
        }
    }

    // La corrupción: espinas de hielo negro que brotan de la roca, trepan por los muros y rodean el portal.
    private void BuildCrystals()
    {
        float w = VillageLayout.CastleDesign.HalfWidth;
        int clusters = 12 + _rng.NextInt(0, 6);
        for (int i = 0; i < clusters; i++)
        {
            Vector3 p = PerimeterPoint(_rng.Next01(), w + 1.5f, VillageLayout.CastleDesign.Forecourt + 1.5f, _d.BackZ - 1.5f);
            p.y = _rng.Range(0f, _d.plateauHeight * 0.6f);
            Shards(p, _rng.NextInt(4, 8), 2f, 6.5f, 35f);
        }
        float L = _d.NaveLength, outer = NaveWidth * 0.5f + AisleWidth;
        int climbing = 6 + _rng.NextInt(0, 4);
        for (int i = 0; i < climbing; i++)
        {
            float side = _rng.Chance(0.5f) ? 1f : -1f;
            Vector3 p = new Vector3(side * (outer + 0.3f), _floor + _rng.Range(0.5f, AisleHeight * 0.7f), -_rng.Range(1f, L - 1f));
            Shards(p, _rng.NextInt(3, 6), 1.2f, 3.5f, 55f);
        }
        foreach (float side in new[] { -1f, 1f })
            Shards(new Vector3(side * 3.4f, _floor, 2.2f), 5, 1.5f, 4.5f, 30f);
        Shards(new Vector3(0f, _floor + _d.naveHeight - 1f, 1.4f), 4, 1.2f, 3f, 60f);
    }

    private void Shards(Vector3 p, int count, float minH, float maxH, float tilt)
    {
        for (int k = 0; k < count; k++)
        {
            float hgt = _rng.Range(minH, maxH);
            Vector3 offset = new Vector3(_rng.Range(-1.3f, 1.3f), hgt * 0.35f, _rng.Range(-1.3f, 1.3f));
            Vector3 euler = new Vector3(_rng.Range(-tilt, tilt), _rng.Range(0f, 90f), _rng.Range(-tilt, tilt));
            float thick = _rng.Range(0.35f, 0.75f) * Mathf.Sqrt(hgt / 3f);
            Box(p + offset, euler, new Vector3(thick, hgt, thick), ice, false);
            Cone(p + offset + Quaternion.Euler(euler) * Vector3.up * hgt * 0.5f, thick * 1.2f, thick * 2.2f, ice, euler);
        }
    }

    private void Spirelet(Vector3 basePos, float diameter, float height)
    {
        Box(basePos + Vector3.up * 0.35f, new Vector3(0f, 45f, 0f), new Vector3(diameter * 0.8f, 0.7f, diameter * 0.8f), trim, false);
        Box(basePos + Vector3.up * (0.7f + height * 0.12f), new Vector3(0f, 45f, 0f), new Vector3(diameter * 0.6f, height * 0.24f, diameter * 0.6f), stone, false);
        Cone(basePos + Vector3.up * (0.7f + height * 0.24f), diameter * 0.7f, height * 0.76f, roof);
    }

    private void Icicle(Vector3 top, float length)
    {
        Cone(top, 0.22f, length, ice, new Vector3(180f, 0f, 0f));
    }

    // ---------- Piezas ----------

    private void LancetOn(Vector3 center, Vector3 normal, float width, float height, Material mat)
    {
        Quaternion face = Quaternion.LookRotation(normal);
        Box(center, face.eulerAngles, new Vector3(width, height, 0.12f), mat, false);
        Box(center + Vector3.up * (height * 0.5f), (face * Quaternion.Euler(0f, 0f, 45f)).eulerAngles, new Vector3(width * 0.7071f, width * 0.7071f, 0.12f), mat, false);
        Box(center - normal * 0.05f + Vector3.up * 0.15f, face.eulerAngles, new Vector3(width + 0.4f, height + width * 0.6f, 0.1f), trim, false);
    }

    private void Strut(Vector3 from, Vector3 to, float thickness, float depth, Material mat)
    {
        Vector3 d = to - from;
        Quaternion rot = Quaternion.LookRotation(Vector3.forward, d.normalized);
        Box((from + to) * 0.5f, rot.eulerAngles, new Vector3(thickness, d.magnitude, depth), mat, false);
    }

    private void Disc(Vector3 center, float diameter, float thickness, Material mat, bool facingX = false)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Place(go, center, facingX ? new Vector3(0f, 0f, 90f) : new Vector3(90f, 0f, 0f), new Vector3(diameter, thickness * 0.5f, diameter), mat);
    }

    private void Cylinder(Vector3 center, float diameter, float height, Material mat, bool collider = false)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Place(go, center, Vector3.zero, new Vector3(diameter, height * 0.5f, diameter), mat);
        if (collider) ColliderBox(center, Vector3.zero, new Vector3(diameter * 0.8f, height, diameter * 0.8f));
    }

    private void Cone(Vector3 basePos, float diameter, float height, Material mat, Vector3 euler = default)
    {
        if (cone == null) return;
        MeshObject(cone, basePos, euler, new Vector3(diameter, height, diameter), mat);
    }

    private void Prism(Vector3 basePos, Vector3 euler, Vector3 scale, Material mat)
    {
        if (prism == null) return;
        MeshObject(prism, basePos, euler, scale, mat);
    }

    private void Box(Vector3 center, Vector3 euler, Vector3 size, Material mat, bool collider, float colliderHeight = 0f)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Place(go, center, euler, size, mat);
        if (!collider) return;
        Vector3 s = size;
        if (colliderHeight > 0f) s.y = colliderHeight;
        ColliderBox(center, euler, s);
    }

    private void MeshObject(Mesh mesh, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
    {
        var go = new GameObject("Pieza", typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        Place(go, pos, euler, scale, mat);
    }

    private void Place(GameObject go, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
    {
        Collider c = go.GetComponent<Collider>();
        if (c != null) DestroyImmediate(c);
        go.transform.SetParent(_visual, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat != null ? mat : stone;
    }

    private void ColliderBox(Vector3 center, Vector3 euler, Vector3 size)
    {
        var go = new GameObject("Colision");
        go.transform.SetParent(_colliders, false);
        go.transform.localPosition = center;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.AddComponent<BoxCollider>().size = size;
    }

    private Transform Child(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(transform, false);
        return t;
    }

    private void Combine()
    {
        var groups = new Dictionary<Material, List<CombineInstance>>();
        Matrix4x4 toLocal = transform.worldToLocalMatrix;
        foreach (MeshFilter mf in _visual.GetComponentsInChildren<MeshFilter>())
        {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null) continue;
            Material m = mr.sharedMaterial;
            if (!groups.TryGetValue(m, out List<CombineInstance> list)) groups[m] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mf.transform.localToWorldMatrix });
        }
        for (int i = _visual.childCount - 1; i >= 0; i--) Destroy(_visual.GetChild(i).gameObject);

        foreach (var pair in groups)
        {
            var mesh = new Mesh { name = "Castillo_" + (pair.Key != null ? pair.Key.name : "sin_material"), indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(pair.Value.ToArray(), true, true);
            mesh.RecalculateBounds();
            var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(_visual, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = pair.Key;
        }
    }
}
