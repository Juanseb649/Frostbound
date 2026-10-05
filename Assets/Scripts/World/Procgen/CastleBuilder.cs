using System.Collections.Generic;
using UnityEngine;

// Construye por código el castillo gótico (solo exterior) a partir de VillageLayout.CastleDesign:
// meseta rocosa con viaducto de arcos (inspirado en el santuario de Las Lajas), catedral con nave,
// naves laterales, arbotantes, pináculos, rosetón, torres con agujas y ábside con capillas (inspirado en
// las catedrales de Anor Londo). Las piezas se combinan en una malla por material; las colisiones van aparte.
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

    public void Build(VillageLayout.CastleDesign design)
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
        BuildCrystals();
        Combine();
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
    private const float AisleHeight = 8f;

    private void BuildNave()
    {
        float L = _d.NaveLength, H = _d.naveHeight, f = _floor;
        float outer = NaveWidth * 0.5f + AisleWidth;
        Box(new Vector3(0f, _d.plateauHeight + 0.3f, -L * 0.5f + 0.25f), Vector3.zero, new Vector3(outer * 2f + 2f, 0.6f, L + 1.5f), trim, true);
        for (int s = 0; s < 3; s++)
            Box(new Vector3(0f, _d.plateauHeight + 0.1f + s * 0.2f, 1.4f + (2 - s) * 0.45f), Vector3.zero, new Vector3(5f, 0.2f, 0.5f), trim, false);

        Box(new Vector3(0f, f + H * 0.5f, -L * 0.5f), Vector3.zero, new Vector3(NaveWidth, H, L), stone, true);
        Prism(new Vector3(0f, f + H, -L * 0.5f), new Vector3(0f, 90f, 0f), new Vector3(L + 0.6f, 5.5f, NaveWidth + 1f), roof);

        foreach (float side in new[] { -1f, 1f })
        {
            float ax = side * (NaveWidth * 0.5f + AisleWidth * 0.5f);
            Box(new Vector3(ax, f + AisleHeight * 0.5f, -L * 0.5f), Vector3.zero, new Vector3(AisleWidth, AisleHeight, L), stone, true);
            float rise = 2.4f, w = Mathf.Sqrt(AisleWidth * AisleWidth + rise * rise);
            float tilt = -Mathf.Atan2(rise, AisleWidth) * Mathf.Rad2Deg * side;
            Box(new Vector3(ax, f + AisleHeight + rise * 0.5f, -L * 0.5f), new Vector3(0f, 0f, tilt), new Vector3(w + 0.4f, 0.3f, L + 0.4f), roof, false);

            for (int i = 0; i < _d.bays; i++)
            {
                float z = -2.5f - 5f * i;
                Lancet(new Vector3(side * (NaveWidth * 0.5f + 0.06f), f + AisleHeight + 2.4f + 1.9f, z), 1.3f, 3.6f, true);
                Lancet(new Vector3(side * (outer + 0.06f), f + AisleHeight * 0.45f, z), 1.1f, 3.2f, true);
            }
            for (int i = 1; i < _d.bays; i++)
            {
                float z = -5f * i;
                float pierX = side * (outer + 0.7f), pierH = AisleHeight + 4f;
                Box(new Vector3(pierX, f + pierH * 0.5f, z), Vector3.zero, new Vector3(1.4f, pierH, 1.2f), stone, false);
                Pinnacle(new Vector3(pierX, f + pierH, z), 1.1f, 3.2f);
                Vector3 from = new Vector3(side * (outer + 0.2f), f + pierH - 0.8f, z);
                Vector3 to = new Vector3(side * (NaveWidth * 0.5f), f + H - 2.2f, z);
                Strut(from, to, 0.55f, 0.6f, trim);
                Pinnacle(new Vector3(side * (NaveWidth * 0.5f + 0.25f), f + H, z), 0.7f, 2.4f);
            }
        }
    }

    private void BuildFacade()
    {
        float H = _d.naveHeight, f = _floor;
        float fw = NaveWidth + 0.8f;
        Box(new Vector3(0f, f + H * 0.5f, 0.6f), Vector3.zero, new Vector3(fw, H, 1.2f), stone, false);
        Prism(new Vector3(0f, f + H, 0.6f), new Vector3(0f, 90f, 0f), new Vector3(1.2f, 6.5f, fw), stone);
        Pinnacle(new Vector3(0f, f + H + 6.5f, 0.6f), 0.9f, 2.6f);

        float z = 1.24f;
        Box(new Vector3(0f, f + 3.2f, z), Vector3.zero, new Vector3(3.6f, 6.4f, 0.12f), trim, false);
        Box(new Vector3(0f, f + 2.7f, z + 0.05f), Vector3.zero, new Vector3(2.6f, 5.4f, 0.12f), door, false);
        foreach (float side in new[] { -1f, 1f })
        {
            Box(new Vector3(side * 2.1f, f + 3.3f, z + 0.1f), Vector3.zero, new Vector3(0.5f, 6.6f, 0.3f), stone, false);
            Box(new Vector3(side * 1.05f, f + 7.3f, z + 0.1f), new Vector3(0f, 0f, side * -32f), new Vector3(2.6f, 0.45f, 0.3f), stone, false);
        }

        float roseY = f + H - 4.2f;
        Disc(new Vector3(0f, roseY, z - 0.02f), 5.6f, 0.12f, trim);
        Disc(new Vector3(0f, roseY, z + 0.02f), 4.6f, 0.12f, glass);
        for (int k = 0; k < 8; k++)
            Box(new Vector3(0f, roseY, z + 0.1f), new Vector3(0f, 0f, k * 22.5f), new Vector3(0.16f, 4.6f, 0.1f), trim, false);
        Disc(new Vector3(0f, roseY, z + 0.12f), 1.2f, 0.12f, trim);

        float towerX = NaveWidth * 0.5f + 2.6f;
        foreach (float side in new[] { -1f, 1f }) Tower(new Vector3(side * towerX, 0f, -1.6f), 5.2f, _d.sideTowerHeight, _d.spireRatio, false);
        Tower(new Vector3(0f, 0f, -3.2f), 5.6f, _d.centralTowerHeight, _d.spireRatio * 0.8f, true);
    }

    private void Tower(Vector3 basePos, float size, float height, float spireRatio, bool central)
    {
        float f = _floor;
        float startY = central ? f + _d.naveHeight - 1f : f;
        float body = height - (startY - f);
        Box(new Vector3(basePos.x, startY + body * 0.5f, basePos.z), Vector3.zero, new Vector3(size, body, size), stone, !central);
        float top = f + height;
        Box(new Vector3(basePos.x, top + 0.2f, basePos.z), Vector3.zero, new Vector3(size + 0.5f, 0.4f, size + 0.5f), trim, false);

        float half = size * 0.5f + 0.06f;
        Vector3[] faces = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        foreach (Vector3 n in faces)
        {
            Vector3 c = new Vector3(basePos.x, 0f, basePos.z) + n * half;
            bool sideways = Mathf.Abs(n.x) > 0.5f;
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 offset = (sideways ? Vector3.forward : Vector3.right) * k * size * 0.2f;
                LancetOn(c + offset + Vector3.up * (top - 4.2f), n, 0.9f, 3.6f, glass);
            }
            if (!central) LancetOn(c + Vector3.up * (f + height * 0.45f), n, 0.8f, 2.6f, glass);
        }

        float corner = size * 0.5f;
        for (int cx = -1; cx <= 1; cx += 2)
            for (int cz = -1; cz <= 1; cz += 2)
            {
                Vector3 p = new Vector3(basePos.x + cx * corner, 0f, basePos.z + cz * corner);
                Box(new Vector3(p.x, startY + body * 0.5f, p.z), new Vector3(0f, 45f, 0f), new Vector3(0.7f, body, 0.7f), trim, false);
                Pinnacle(new Vector3(p.x, top + 0.4f, p.z), 0.9f, 3.4f);
            }

        float spireH = height * spireRatio;
        Cylinder(new Vector3(basePos.x, top + 1.1f, basePos.z), size * 0.82f, 1.4f, trim);
        Cone(new Vector3(basePos.x, top + 1.8f, basePos.z), size * 0.86f, spireH, roof);
        Vector3 tip = new Vector3(basePos.x, top + 1.8f + spireH, basePos.z);
        if (central)
        {
            Box(tip + Vector3.up * 1.2f, new Vector3(0f, 45f, 20f), new Vector3(0.6f, 2.6f, 0.6f), ice, false);
            Box(tip + Vector3.up * 0.8f, new Vector3(0f, 0f, -35f), new Vector3(0.35f, 1.4f, 0.35f), ice, false);
        }
        else
        {
            Box(tip + Vector3.up * 0.3f, Vector3.zero, new Vector3(0.45f, 0.45f, 0.45f), gold, false);
        }
    }

    private void BuildTransept()
    {
        float L = _d.NaveLength, H = _d.naveHeight, f = _floor;
        float z = -L * 0.62f, width = NaveWidth + 2f * AisleWidth + 8f;
        Box(new Vector3(0f, f + H * 0.5f, z), Vector3.zero, new Vector3(width, H, 8f), stone, true);
        Prism(new Vector3(0f, f + H, z), Vector3.zero, new Vector3(width + 0.6f, 5.5f, 9f), roof);
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * (width * 0.5f + 0.06f);
            Vector3 c = new Vector3(x, f + H - 4f, z);
            Disc(c, 4.4f, 0.12f, trim, true);
            Disc(c + Vector3.right * side * 0.04f, 3.6f, 0.12f, glass, true);
            LancetOn(new Vector3(x, f + 4f, z), Vector3.right * side, 1.6f, 5f, glass);
            Pinnacle(new Vector3(x, f + H, z + 3.6f), 0.9f, 3f);
            Pinnacle(new Vector3(x, f + H, z - 3.6f), 0.9f, 3f);
        }
        Cylinder(new Vector3(0f, f + H + 5.2f, z), 2.4f, 1.6f, trim);
        Cone(new Vector3(0f, f + H + 6f, z), 2.2f, 11f, roof);
        Box(new Vector3(0f, f + H + 17.3f, z), Vector3.zero, new Vector3(0.35f, 0.35f, 0.35f), gold, false);
    }

    private void BuildApse()
    {
        float L = _d.NaveLength, f = _floor, h = _d.naveHeight - 2f;
        Cylinder(new Vector3(0f, f + h * 0.5f, -L), NaveWidth, h, stone, true);
        Cone(new Vector3(0f, f + h, -L), NaveWidth + 0.8f, 5f, roof);
        for (int k = 0; k < 6; k++)
        {
            float a = Mathf.Lerp(-75f, 75f, k / 5f) * Mathf.Deg2Rad;
            Vector3 n = new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a));
            LancetOn(new Vector3(0f, f + h * 0.55f, -L) + n * (NaveWidth * 0.5f + 0.04f), n, 0.9f, 4f, glass);
        }
        for (int i = 0; i < _d.chapels; i++)
        {
            float t = _d.chapels == 1 ? 0.5f : i / (float)(_d.chapels - 1);
            float a = Mathf.Lerp(-70f, 70f, t) * Mathf.Deg2Rad;
            Vector3 p = new Vector3(0f, 0f, -L) + new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)) * (NaveWidth * 0.5f + 1.6f);
            Cylinder(new Vector3(p.x, f + 3f, p.z), 3.4f, 6f, stone);
            Cone(new Vector3(p.x, f + 6f, p.z), 3.8f, 2.6f, roof);
        }
    }

    private void BuildCrystals()
    {
        int clusters = 7 + _rng.NextInt(0, 5);
        float w = VillageLayout.CastleDesign.HalfWidth;
        for (int i = 0; i < clusters; i++)
        {
            Vector3 p = PerimeterPoint(_rng.Next01(), w + 1.5f, VillageLayout.CastleDesign.Forecourt + 1.5f, _d.BackZ - 1.5f);
            int shards = _rng.NextInt(3, 6);
            for (int k = 0; k < shards; k++)
            {
                float hgt = _rng.Range(1.5f, 4.5f);
                Vector3 offset = new Vector3(_rng.Range(-1.2f, 1.2f), hgt * 0.4f, _rng.Range(-1.2f, 1.2f));
                Box(p + offset, new Vector3(_rng.Range(-25f, 25f), _rng.Range(0f, 90f), _rng.Range(-25f, 25f)), new Vector3(0.6f, hgt, 0.6f) * _rng.Range(0.8f, 1.3f), ice, false);
            }
        }
    }

    // ---------- Piezas ----------

    private void Lancet(Vector3 center, float width, float height, bool sideWall)
    {
        Vector3 normal = sideWall ? (center.x > 0f ? Vector3.right : Vector3.left) : Vector3.forward;
        LancetOn(center, normal, width, height, glass);
    }

    private void LancetOn(Vector3 center, Vector3 normal, float width, float height, Material mat)
    {
        Quaternion face = Quaternion.LookRotation(normal);
        Box(center, face.eulerAngles, new Vector3(width, height, 0.12f), mat, false);
        Box(center + Vector3.up * (height * 0.5f), (face * Quaternion.Euler(0f, 0f, 45f)).eulerAngles, new Vector3(width * 0.7071f, width * 0.7071f, 0.12f), mat, false);
        Box(center - normal * 0.05f + Vector3.up * 0.15f, face.eulerAngles, new Vector3(width + 0.4f, height + width * 0.6f, 0.1f), trim, false);
    }

    private void Pinnacle(Vector3 basePos, float diameter, float height)
    {
        Box(basePos + Vector3.up * 0.3f, new Vector3(0f, 45f, 0f), new Vector3(diameter * 0.75f, 0.6f, diameter * 0.75f), trim, false);
        Cone(basePos + Vector3.up * 0.6f, diameter, height, stone);
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

    private void Cone(Vector3 basePos, float diameter, float height, Material mat)
    {
        if (cone == null) return;
        MeshObject(cone, basePos, Vector3.zero, new Vector3(diameter, height, diameter), mat);
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
