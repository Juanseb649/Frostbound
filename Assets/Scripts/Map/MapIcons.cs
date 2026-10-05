using System.Collections.Generic;
using UnityEngine;

// Iconos del mapa dibujados por código (blancos, se tiñen con Image.color).
public static class MapIcons
{
    public enum Shape { Circle, Ring, Diamond, Arrow, Square, Star, Flame, Tower, House, Skull, Exclaim }

    private static readonly Dictionary<Shape, Sprite> Cache = new Dictionary<Shape, Sprite>();

    public static Sprite Get(Shape shape)
    {
        if (Cache.TryGetValue(shape, out Sprite s) && s != null) return s;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Icono_" + shape };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float d = Distance(shape, u, v);
                // d < 0 dentro. Borde suave de ~1.5 px y contorno oscuro.
                float aa = 2.5f / n;
                float fill = Mathf.Clamp01(0.5f - d / aa);
                float outline = Mathf.Clamp01(0.5f - (d - 0.09f) / aa);
                float a = Mathf.Max(fill, outline * 0.85f);
                float l = fill > 0f ? Mathf.Lerp(0.08f, 1f, fill) : 0.08f;
                byte lb = (byte)(l * 255f);
                px[y * n + x] = new Color32(lb, lb, lb, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        s.name = tex.name;
        Cache[shape] = s;
        return s;
    }

    private static float Box(float u, float v, float hx, float hy) => Mathf.Max(Mathf.Abs(u) - hx, Mathf.Abs(v) - hy);

    private static float Distance(Shape shape, float u, float v)
    {
        switch (shape)
        {
            case Shape.Circle: return Mathf.Sqrt(u * u + v * v) - 0.62f;
            case Shape.Ring: return Mathf.Abs(Mathf.Sqrt(u * u + v * v) - 0.55f) - 0.13f;
            case Shape.Diamond: return (Mathf.Abs(u) + Mathf.Abs(v)) * 0.7071f - 0.5f;
            case Shape.Square: return Box(u, v, 0.5f, 0.5f);
            case Shape.Arrow:
            {
                // Punta hacia arriba (+v) con una muesca en la base.
                float tri = Mathf.Max(Mathf.Abs(u) * 1.5f + v * 0.75f - 0.55f, -v - 0.6f);
                float notch = (v + 0.6f) - 0.35f + Mathf.Abs(u) * 0.9f;
                return Mathf.Max(tri, -notch);
            }
            case Shape.Star:
            {
                float ang = Mathf.Atan2(u, v), r = Mathf.Sqrt(u * u + v * v);
                float k = Mathf.Cos(5f * ang) * 0.5f + 0.5f;
                return r - Mathf.Lerp(0.32f, 0.72f, Mathf.Pow(k, 2.2f));
            }
            case Shape.Flame:
            {
                float body = Mathf.Sqrt(u * u * 1.6f + (v + 0.2f) * (v + 0.2f)) - 0.5f;
                float tip = Mathf.Abs(u) * 1.6f + (v - 0.2f) * 0.9f - 0.55f;
                return Mathf.Min(body, Mathf.Max(tip, -v - 0.1f));
            }
            case Shape.Tower:
            {
                float body = Box(u, v + 0.25f, 0.32f, 0.45f);
                float roof = Mathf.Abs(u) * 1.8f + (v - 0.2f) - 0.62f;
                return Mathf.Min(body, Mathf.Max(roof, -(v - 0.15f)));
            }
            case Shape.House:
            {
                float body = Box(u, v + 0.25f, 0.5f, 0.38f);
                float roof = Mathf.Abs(u) * 0.9f + (v - 0.1f) - 0.58f;
                return Mathf.Min(body, Mathf.Max(roof, -(v - 0.08f)));
            }
            case Shape.Skull:
            {
                float head = Mathf.Sqrt(u * u + (v - 0.1f) * (v - 0.1f)) - 0.52f;
                float jaw = Box(u, v + 0.45f, 0.3f, 0.18f);
                float eyes = Mathf.Min(Mathf.Sqrt((u - 0.2f) * (u - 0.2f) + (v - 0.05f) * (v - 0.05f)), Mathf.Sqrt((u + 0.2f) * (u + 0.2f) + (v - 0.05f) * (v - 0.05f))) - 0.13f;
                return Mathf.Max(Mathf.Min(head, jaw), -eyes);
            }
            case Shape.Exclaim:
            {
                float bar = Box(u, v - 0.2f, 0.14f, 0.42f);
                float dot = Mathf.Sqrt(u * u + (v + 0.55f) * (v + 0.55f)) - 0.16f;
                return Mathf.Min(bar, dot);
            }
        }
        return 1f;
    }
}
