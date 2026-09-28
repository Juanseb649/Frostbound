using UnityEngine;
using UnityEditor;
using System.IO;

public static class PropTexturesGenerator
{
    const int Size = 1024;

    [MenuItem("Tools/Frostbound/Legacy/Prop Textures/Generate All Prop Textures")]
    public static void GenerateAll()
    {
        GenerateSnowMound();
        GenerateIceWall();
        GeneratePineTree();
        GenerateWoodenFence();
        FrostboundBridge.Dialog("Nieve Club Penguin", "Las 4 texturas de props se generaron en Assets/Textures.", "OK");
    }

    // TEXTURE 1 - Snow mound / rock prop
    [MenuItem("Tools/Frostbound/Legacy/Prop Textures/Snow Mound (Rock)")]
    public static void GenerateSnowMound()
    {
        Color[] p = new Color[Size * Size];
        Color back = new Color(0.93f, 0.95f, 0.97f);
        Color backPatch = new Color(0.82f, 0.90f, 0.95f);
        Color rockBase = new Color(0.55f, 0.58f, 0.65f);
        Color rockDark = new Color(0.46f, 0.49f, 0.57f);
        Color snow = new Color(0.96f, 0.98f, 1.0f);
        Color snowPatch = new Color(0.78f, 0.88f, 0.94f);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.004f, y * 0.004f);
                Color c = back;
                if (n > 0.58f) c = Color.Lerp(back, backPatch, Mathf.InverseLerp(0.58f, 0.85f, n));
                p[y * Size + x] = c;
            }
        }

        Vector4[] mounds = new Vector4[]
        {
            new Vector4(300, 780, 230, 150),
            new Vector4(560, 560, 200, 125),
            new Vector4(790, 760, 190, 115),
            new Vector4(760, 300, 210, 140),
            new Vector4(300, 300, 210, 135),
            new Vector4(550, 150, 190, 105),
        };

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                for (int i = 0; i < mounds.Length; i++)
                {
                    float cx = mounds[i].x, cy = mounds[i].y, rx = mounds[i].z, ry = mounds[i].w;
                    float dx = (x - cx) / rx;
                    float dy = (y - cy) / ry;
                    if (dx * dx + dy * dy > 1f) continue;

                    Color c = rockBase;
                    if (dy > 0.05f) c = Color.Lerp(rockBase, rockDark, Mathf.InverseLerp(0.05f, 0.45f, dy));

                    float wobble = (Mathf.PerlinNoise(x * 0.02f + i * 7.3f, y * 0.02f) - 0.5f) * 0.18f;
                    float snowTop = -0.5f + 0.65f * Mathf.Abs(dx) + wobble;
                    if (dy < snowTop)
                    {
                        c = snow;
                        float sn = Mathf.PerlinNoise(x * 0.01f + i * 31.7f, y * 0.01f);
                        if (sn > 0.6f) c = Color.Lerp(snow, snowPatch, Mathf.InverseLerp(0.6f, 0.9f, sn));
                    }
                    p[y * Size + x] = c;
                }
            }
        }

        MakeSeamless(p, 24);
        SavePng(p, "Assets/Textures/prop_snow_mound.png", "Snow mound / rock prop");
    }

    // TEXTURE 2 - Ice boundary wall
    [MenuItem("Tools/Frostbound/Legacy/Prop Textures/Ice Boundary Wall")]
    public static void GenerateIceWall()
    {
        Color[] p = new Color[Size * Size];
        Color back = new Color(0.945f, 0.965f, 0.98f);
        Color ice = new Color(0.72f, 0.86f, 0.94f);
        Color iceDark = new Color(0.60f, 0.78f, 0.90f);
        Color crack = new Color(0.52f, 0.72f, 0.86f);
        Color snow = new Color(0.97f, 0.99f, 1.0f);

        float[] crackXs = { 120, 300, 480, 640, 830, 980 };

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Color c = back;
                float top = 830f + 42f * Mathf.Sin(x * 0.013f) + 22f * Mathf.Sin(x * 0.037f + 1.3f);
                top += (Mathf.PerlinNoise(x * 0.02f, 13.7f) - 0.5f) * 10f;

                if (y >= 60 && y <= top)
                {
                    c = ice;
                    if (y < 360f) c = Color.Lerp(ice, iceDark, Mathf.InverseLerp(360f, 220f, y));
                    if (y > top - 60f) c = snow;

                    for (int i = 0; i < crackXs.Length; i++)
                    {
                        float cxp = crackXs[i] + 14f * Mathf.Sin(y * 0.02f + i * 2.1f);
                        float w = 4.5f;
                        float dist = Mathf.Abs(x - cxp);
                        if (y > 180f && y < 700f && dist < w * 2f)
                        {
                            float t = Mathf.Clamp01(1f - dist / (w * 2f));
                            float fade = Mathf.Sin(Mathf.PI * (y - 180f) / 520f);
                            c = Color.Lerp(c, crack, t * fade * 0.85f);
                        }
                    }
                }
                p[y * Size + x] = c;
            }
        }

        MakeSeamless(p, 24);
        SavePng(p, "Assets/Textures/prop_ice_wall.png", "Ice boundary wall");
    }

    // TEXTURE 3 - Snowy pine tree (billboard with transparent background)
    [MenuItem("Tools/Frostbound/Legacy/Prop Textures/Snowy Pine Tree (Billboard)")]
    public static void GeneratePineTree()
    {
        Color[] p = new Color[Size * Size];
        Color clear = new Color(0f, 0f, 0f, 0f);
        int cx = 512;
        Color snow = new Color(0.97f, 0.99f, 1f);
        Color greenLight = new Color(0.38f, 0.62f, 0.47f);
        Color greenMid = new Color(0.28f, 0.50f, 0.37f);
        Color greenDark = new Color(0.19f, 0.37f, 0.27f);
        Color trunkDark = new Color(0.30f, 0.19f, 0.12f);
        Color trunkMid = new Color(0.42f, 0.28f, 0.19f);

        int[][] tiers = new int[][]
        {
            new int[] { 585, 740, 200 },
            new int[] { 445, 610, 270 },
            new int[] { 300, 470, 340 },
        };

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Color c = clear;

                bool trunkInside = x >= cx - 46 && x <= cx + 46 && y >= 120 && y <= 300;
                if (trunkInside && y < 172)
                {
                    float nx = (x - cx) / 46f;
                    float ny = (y - 120f) / 52f;
                    trunkInside = nx * nx + ny * ny <= 1f;
                }
                if (trunkInside) c = (x > cx + 4) ? trunkDark : trunkMid;

                for (int i = 0; i < tiers.Length; i++)
                {
                    int tb = tiers[i][0], tt = tiers[i][1], hw = tiers[i][2];
                    if (y < tb || y > tt) continue;

                    float dx = Mathf.Abs(x - cx);
                    float frac = (float)(tt - y) / (tt - tb);
                    float half = hw * frac;
                    float edge = Mathf.Clamp01((half + 8f - dx) / 8f);
                    if (edge <= 0f) continue;

                    float band = (float)(y - tb) / (tt - tb);
                    Color tc;
                    if (band < 0.30f) tc = greenDark;
                    else if (band < 0.58f) tc = greenMid;
                    else if (band < 0.74f) tc = greenLight;
                    else tc = snow;

                    c = Color.Lerp(c, tc, edge);
                }

                p[y * Size + x] = c;
            }
        }

        SavePng(p, "Assets/Textures/prop_snowy_pine.png", "Snowy pine tree (billboard)");
    }

    // TEXTURE 4 - Wooden fence / camp boundary
    [MenuItem("Tools/Frostbound/Legacy/Prop Textures/Wooden Fence")]
    public static void GenerateWoodenFence()
    {
        Color[] p = new Color[Size * Size];
        Color back = new Color(0.93f, 0.95f, 0.97f);
        Color wood = new Color(0.56f, 0.36f, 0.23f);
        Color woodDark = new Color(0.43f, 0.27f, 0.17f);
        Color woodLight = new Color(0.65f, 0.45f, 0.30f);
        Color snow = new Color(0.96f, 0.98f, 1.0f);

        int period = 128;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Color c = back;

                if (y >= 560 && y <= 664)
                {
                    c = wood;
                    if (y < 600) c = woodDark;
                    if (y < 580) c = snow;
                    if (x > 512) c = Color.Lerp(c, woodDark, 0.4f);
                }
                if (y >= 300 && y <= 372)
                {
                    c = wood;
                    if (y < 332) c = woodDark;
                    if (y < 316) c = snow;
                    if (x > 512) c = Color.Lerp(c, woodDark, 0.4f);
                }

                int cell = Mathf.FloorToInt(x / (float)period);
                float pcx = cell * period + period * 0.5f;
                float dx = x - pcx;
                if (Mathf.Abs(dx) <= 46)
                {
                    bool inside = y >= 96 && y <= 800;
                    if (y > 748)
                    {
                        float nx = dx / 46f;
                        float ny = (y - 800f) / 52f;
                        inside = nx * nx + ny * ny <= 1f;
                    }
                    else if (y < 150)
                    {
                        float nx = dx / 46f;
                        float ny = (y - 96f) / 54f;
                        inside = nx * nx + ny * ny <= 1f;
                    }

                    if (inside)
                    {
                        c = wood;
                        if (dx > 10) c = woodDark;
                        else if (dx < -10) c = woodLight;

                        if (y > 748)
                        {
                            float nx = dx / 46f;
                            float ny = (y - 800f) / 52f;
                            if (nx * nx + ny * ny > 0.45f) c = snow;
                        }
                    }
                }

                p[y * Size + x] = c;
            }
        }

        SavePng(p, "Assets/Textures/prop_wooden_fence.png", "Wooden fence / camp boundary");
    }

    static void MakeSeamless(Color[] p, int border)
    {
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < border; x++)
            {
                float t = (float)x / border;
                int iL = y * Size + x;
                int iR = y * Size + (Size - border + x);
                Color cL = Color.Lerp(p[iL], p[iR], t);
                Color cR = Color.Lerp(p[iR], p[iL], 1f - t);
                p[iL] = cL;
                p[iR] = cR;
            }
        }
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < border; y++)
            {
                float t = (float)y / border;
                int iB = y * Size + x;
                int iT = (Size - border + y) * Size + x;
                Color cB = Color.Lerp(p[iB], p[iT], t);
                Color cT = Color.Lerp(p[iT], p[iB], 1f - t);
                p[iB] = cB;
                p[iT] = cT;
            }
        }
    }

    static void SavePng(Color[] pixels, string path, string label)
    {
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        string dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();

        Debug.Log("Textura generada: " + path + " (" + label + ")");
        FrostboundBridge.Dialog("Nieve Club Penguin", "Textura generada: " + label + "\n" + path, "OK");
    }
}
