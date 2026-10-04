using UnityEngine;
using UnityEditor;
using System.IO;

public static class SnowTextureGenerator
{
    [MenuItem("Tools/Frostbound/Legacy/Generate Club Penguin Snow Texture")]
    public static void Generate()
    {
        int width = 1024;
        int height = 1024;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        Color baseSnow    = new Color(0.94f, 0.96f, 0.98f);
        Color patchColor1 = new Color(0.82f, 0.90f, 0.95f);
        Color patchColor2 = new Color(0.75f, 0.86f, 0.93f);
        Color crackColor  = new Color(0.65f, 0.78f, 0.88f);
        Color sparkleColor = new Color(1f, 1f, 1f);

        Color[] pixels = new Color[width * height];

        float scale = 4.0f;
        System.Random rng = new System.Random(12345); // seed fijo = resultado reproducible

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width * scale;
                float v = (float)y / height * scale;

                // --- Domain warping: distorsiona las coordenadas antes de samplear ---
                // Esto rompe el look "geométrico" del Perlin noise puro
                float warpX = Mathf.PerlinNoise(u * 0.5f + 5.2f, v * 0.5f + 1.3f) * 1.5f;
                float warpY = Mathf.PerlinNoise(u * 0.5f - 7.7f, v * 0.5f + 9.9f) * 1.5f;
                float wu = u + warpX;
                float wv = v + warpY;

                float n1 = Mathf.PerlinNoise(wu, wv);
                float n2 = Mathf.PerlinNoise(wu * 2.5f + 12.3f, wv * 2.5f + 45.1f);
                float n3 = Mathf.PerlinNoise(wu * 5.0f - 88.2f, wv * 5.0f + 19.4f);

                float combined = (n1 * 0.6f + n2 * 0.3f + n3 * 0.1f);

                Color finalColor = baseSnow;

                // --- Smoothstep en vez de Lerp lineal: transición mucho más orgánica ---
                float band1 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.72f, combined));
                float band2 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1.0f, combined));

                if (combined > 0.58f && combined <= 0.72f)
                    finalColor = Color.Lerp(baseSnow, patchColor1, band1);
                else if (combined > 0.72f)
                    finalColor = Color.Lerp(patchColor1, patchColor2, band2);

                // Grietas con grosor ligeramente variable (menos uniformes)
                float crackNoise = Mathf.Abs(n3 - 0.5f);
                float crackThreshold = 0.010f + n1 * 0.008f; // grosor variable según n1
                if (crackNoise < crackThreshold && combined > 0.5f)
                {
                    float t = crackNoise / crackThreshold;
                    finalColor = Color.Lerp(crackColor, finalColor, t);
                }

                // --- Sparkle: destellos aleatorios pequeños, solo en zonas de nieve base ---
                if (combined < 0.5f && rng.NextDouble() < 0.0015)
                {
                    float sparkleStrength = 0.5f + (float)rng.NextDouble() * 0.5f;
                    finalColor = Color.Lerp(finalColor, sparkleColor, sparkleStrength);
                }

                pixels[y * width + x] = finalColor;
            }
        }

        tex.SetPixels(pixels);

        // --- Suavizado leve tipo blur para eliminar aliasing entre bandas ---
        SoftBlur(tex, width, height);

        tex.Apply();

        byte[] bytes = tex.EncodeToPNG();
        string dir = "Assets/Textures";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "snow_seamless.png");
        File.WriteAllBytes(path, bytes);

        AssetDatabase.Refresh();

        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ground.mat");
        Texture2D savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (mat != null && savedTex != null)
        {
            mat.SetTexture("_BaseMap", savedTex);
            mat.SetTexture("_MainTex", savedTex);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            FrostboundBridge.Dialog("Snow Texture Generada",
                "Se generó snow_seamless.png en Assets/Textures y se asignó a Ground.mat.",
                "OK");
        }
        else
        {
            Debug.LogWarning("");
        }
    }

    // Blur simple 3x3 con wrap (para mantener el tileable) — suaviza bordes duros entre bandas
    private static void SoftBlur(Texture2D tex, int width, int height)
    {
        Color[] original = tex.GetPixels();
        Color[] blurred = new Color[original.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color sum = Color.black;
                int count = 0;
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int sx = (x + ox + width) % width;   // wrap horizontal
                        int sy = (y + oy + height) % height; // wrap vertical
                        sum += original[sy * width + sx];
                        count++;
                    }
                }
                blurred[y * width + x] = sum / count;
            }
        }

        tex.SetPixels(blurred);
    }
}