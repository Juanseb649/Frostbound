using System.Collections.Generic;
using UnityEngine;

// Materiales PBR sin texturas importadas: genera por código texturas que se repiten sin costura (color en escala de
// grises, brillo en el alfa y mapa de relieve) para sillares de piedra, pizarra, tablones, corteza, piedra tosca y
// nieve. El color lo pone el material; la textura aporta el detalle. Se usan con UVs proyectadas en el mundo
// (ProjectUVs), porque las piezas de MeshKit son primitivas escaladas y sus UVs se estirarían.
public static class ProceduralSurfaces
{
    public enum Kind { Blocks, Slate, Planks, Bark, Rough, Snow }

    private const int Size = 256;
    private static readonly Dictionary<Kind, (Texture2D albedo, Texture2D normal)> Cache = new Dictionary<Kind, (Texture2D, Texture2D)>();

    // Material Lit con la superficie indicada. smoothness escala el brillo que trae la textura.
    public static Material Make(string name, Kind kind, Color color, float smoothness, float bump = 1f)
    {
        var (albedo, normal) = Get(kind);
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        m.SetColor("_BaseColor", color);
        m.SetTexture("_BaseMap", albedo);
        m.SetTexture("_BumpMap", normal);
        m.SetFloat("_BumpScale", bump);
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_SmoothnessTextureChannel", 1f);
        m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        m.SetFloat("_Metallic", 0f);
        return m;
    }

    // Reescribe las UVs de una malla proyectándolas desde el eje dominante de cada normal (tiling = metros por
    // repetición) y recalcula las tangentes para el relieve.
    public static void ProjectUVs(Mesh mesh, float metersPerTile)
    {
        Vector3[] v = mesh.vertices, n = mesh.normals;
        var uv = new Vector2[v.Length];
        float k = 1f / metersPerTile;
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 a = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
            if (a.y >= a.x && a.y >= a.z) uv[i] = new Vector2(v[i].x, v[i].z) * k;
            else if (a.x >= a.z) uv[i] = new Vector2(v[i].z, v[i].y) * k;
            else uv[i] = new Vector2(v[i].x, v[i].y) * k;
        }
        mesh.uv = uv;
        mesh.RecalculateTangents();
    }

    private static (Texture2D, Texture2D) Get(Kind kind)
    {
        if (Cache.TryGetValue(kind, out var t) && t.Item1 != null) return t;
        var height = new float[Size * Size];
        var tone = new float[Size * Size];
        var gloss = new float[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (float)x / Size, v = (float)y / Size;
                Sample(kind, u, v, out float h, out float c, out float s);
                int i = y * Size + x;
                height[i] = h;
                tone[i] = c;
                gloss[i] = s;
            }

        var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false) { name = kind + " (color)", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
        var normal = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true) { name = kind + " (relieve)", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
        var cols = new Color32[Size * Size];
        var nors = new Color32[Size * Size];
        float strength = kind == Kind.Snow ? 1.5f : 5f;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int i = y * Size + x;
                byte c = (byte)(Mathf.Clamp01(tone[i]) * 255f);
                cols[i] = new Color32(c, c, c, (byte)(Mathf.Clamp01(gloss[i]) * 255f));
                float dx = height[y * Size + (x + 1) % Size] - height[y * Size + (x + Size - 1) % Size];
                float dy = height[((y + 1) % Size) * Size + x] - height[((y + Size - 1) % Size) * Size + x];
                Vector3 nv = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                nors[i] = new Color32((byte)((nv.x * 0.5f + 0.5f) * 255f), (byte)((nv.y * 0.5f + 0.5f) * 255f), (byte)((nv.z * 0.5f + 0.5f) * 255f), 255);
            }
        albedo.SetPixels32(cols);
        albedo.Apply(true, true);
        normal.SetPixels32(nors);
        normal.Apply(true, true);
        Cache[kind] = (albedo, normal);
        return (albedo, normal);
    }

    // h: altura (relieve) · c: tono del color · s: brillo.
    private static void Sample(Kind kind, float u, float v, out float h, out float c, out float s)
    {
        switch (kind)
        {
            case Kind.Blocks:
            {
                // Sillares de 2 × 4 por repetición, con las hiladas alternas desplazadas y llagas de mortero.
                const int rows = 4, cols = 2;
                float ry = v * rows;
                int row = Mathf.FloorToInt(ry);
                float rx = u * cols + (row % 2 == 0 ? 0f : 0.5f);
                int col = Mathf.FloorToInt(rx);
                float fx = rx - col, fy = ry - row;
                float edge = Mathf.Min(Mathf.Min(fx, 1f - fx) * 2.2f, Mathf.Min(fy, 1f - fy) * 1.1f);
                float mortar = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge * 9f));
                float block = Hash(((col % cols) + cols) % cols, row);
                float grain = Fbm(u, v, 16, 4);
                h = mortar * (0.75f + grain * 0.25f) + (Fbm(u, v, 4, 3) - 0.5f) * 0.12f * mortar;
                c = Mathf.Lerp(0.42f, 0.78f + block * 0.22f - grain * 0.18f, mortar);
                s = Mathf.Lerp(0.08f, 0.35f + grain * 0.25f, mortar);
                break;
            }
            case Kind.Slate:
            {
                // Tejas de pizarra solapadas: 8 hiladas, cada teja con su sombra en el borde de abajo.
                const int rows = 8, cols = 6;
                float ry = v * rows;
                int row = Mathf.FloorToInt(ry);
                float rx = u * cols + (row % 2 == 0 ? 0f : 0.5f);
                int col = Mathf.FloorToInt(rx);
                float fx = rx - col, fy = ry - row;
                float side = Mathf.Clamp01(Mathf.Min(fx, 1f - fx) * 14f);
                float tile = Hash(((col % cols) + cols) % cols, row);
                h = fy * 0.8f * side + tile * 0.1f + Fbm(u, v, 24, 3) * 0.08f;
                c = (0.55f + tile * 0.3f) * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(fy * 3f)) * Mathf.Lerp(0.6f, 1f, side);
                s = 0.45f + tile * 0.3f;
                break;
            }
            case Kind.Planks:
            {
                // Tablones verticales con veta estirada y juntas oscuras.
                const int cols = 6;
                float rx = u * cols;
                int col = Mathf.FloorToInt(rx);
                float fx = rx - col;
                float gap = Mathf.Clamp01(Mathf.Min(fx, 1f - fx) * 18f);
                float plank = Hash(col % cols, 3);
                float vein = Fbm(u + plank, v, 32, 4, 4);
                h = gap * (0.7f + vein * 0.3f);
                c = Mathf.Lerp(0.25f, 0.6f + plank * 0.3f - vein * 0.25f, gap);
                s = 0.12f + vein * 0.1f;
                break;
            }
            case Kind.Bark:
            {
                float groove = Fbm(u, v, 12, 2, 4);
                h = Mathf.Pow(groove, 1.6f);
                c = 0.45f + groove * 0.5f;
                s = 0.1f + groove * 0.15f;
                break;
            }
            case Kind.Rough:
            {
                float n = Fbm(u, v, 8, 5);
                float pits = Mathf.Clamp01((Fbm(u, v, 20, 2) - 0.62f) * 6f);
                h = n - pits * 0.3f;
                c = 0.55f + n * 0.4f - pits * 0.25f;
                s = 0.2f + n * 0.2f;
                break;
            }
            default:
            {
                // Nieve: ondulaciones suaves y un brillo moteado que destella con la luna.
                float n = Fbm(u, v, 4, 4);
                float sparkle = Hash(Mathf.FloorToInt(u * 128f), Mathf.FloorToInt(v * 128f)) > 0.97f ? 1f : 0f;
                h = n;
                c = 0.9f + n * 0.1f;
                s = 0.35f + n * 0.15f + sparkle * 0.55f;
                break;
            }
        }
    }

    // Ruido de valor periódico (se repite cada px × py celdas) para que la textura no tenga costuras.
    // Un periodo distinto por eje estira el ruido (vetas de madera, surcos de corteza).
    private static float Noise(float u, float v, int px, int py)
    {
        float x = u * px, y = v * py;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int xa = Wrap(x0, px), xb = Wrap(x0 + 1, px), ya = Wrap(y0, py), yb = Wrap(y0 + 1, py);
        float a = Mathf.Lerp(Hash(xa, ya), Hash(xb, ya), fx);
        float b = Mathf.Lerp(Hash(xa, yb), Hash(xb, yb), fx);
        return Mathf.Lerp(a, b, fy);
    }

    private static float Fbm(float u, float v, int period, int octaves) => Fbm(u, v, period, period, octaves);

    private static float Fbm(float u, float v, int px, int py, int octaves)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += Noise(u, v, px, py) * amp;
            norm += amp;
            amp *= 0.5f;
            px *= 2;
            py *= 2;
        }
        return sum / norm;
    }

    private static int Wrap(int i, int p) => ((i % p) + p) % p;

    private static float Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f;
    }
}
