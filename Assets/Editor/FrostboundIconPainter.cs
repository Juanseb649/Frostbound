using System;
using System.Collections.Generic;
using UnityEngine;

// Pinta iconos planos (estilo toon, con contorno) a partir de formas con campo de distancia.
// Coordenadas de diseño: lienzo de 128 × 128, origen arriba a la izquierda.
public static class FrostboundIconPainter
{
    public abstract class Shape
    {
        public abstract float Sdf(Vector2 p);
    }

    public class Circle : Shape
    {
        private readonly Vector2 c; private readonly float r;
        public Circle(float x, float y, float r) { c = new Vector2(x, y); this.r = r; }
        public override float Sdf(Vector2 p) => (p - c).magnitude - r;
    }

    public class Ellipse : Shape
    {
        private readonly Vector2 c, r;
        public Ellipse(float x, float y, float rx, float ry) { c = new Vector2(x, y); r = new Vector2(rx, ry); }
        public override float Sdf(Vector2 p)
        {
            Vector2 q = p - c;
            float k = new Vector2(q.x / r.x, q.y / r.y).magnitude;
            return (k - 1f) * Mathf.Min(r.x, r.y);
        }
    }

    public class Ring : Shape
    {
        private readonly Vector2 c; private readonly float r, t;
        public Ring(float x, float y, float r, float thickness) { c = new Vector2(x, y); this.r = r; t = thickness; }
        public override float Sdf(Vector2 p) => Mathf.Abs((p - c).magnitude - r) - t;
    }

    public class Box : Shape
    {
        private readonly Vector2 c, h; private readonly float r, a;
        public Box(float x, float y, float hw, float hh, float radius = 0f, float angle = 0f)
        {
            c = new Vector2(x, y); h = new Vector2(hw, hh); r = Mathf.Min(radius, Mathf.Min(hw, hh)); a = angle * Mathf.Deg2Rad;
        }
        public override float Sdf(Vector2 p)
        {
            Vector2 q = Rotate(p - c, -a);
            Vector2 d = new Vector2(Mathf.Abs(q.x) - (h.x - r), Mathf.Abs(q.y) - (h.y - r));
            Vector2 m = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
            return m.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - r;
        }
    }

    public class Poly : Shape
    {
        private readonly Vector2[] v;
        public Poly(params float[] xy)
        {
            v = new Vector2[xy.Length / 2];
            for (int i = 0; i < v.Length; i++) v[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
        }
        public override float Sdf(Vector2 p)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }
    }

    // Intersección: la forma solo dentro de la máscara.
    public class Cut : Shape
    {
        private readonly Shape a, mask;
        public Cut(Shape a, Shape mask) { this.a = a; this.mask = mask; }
        public override float Sdf(Vector2 p) => Mathf.Max(a.Sdf(p), mask.Sdf(p));
    }

    public class Layer
    {
        public Shape shape;
        public Color color;
        public bool outline = true;
        public bool shade = true;
    }

    public class Icon
    {
        public readonly List<Layer> layers = new List<Layer>();
        public float rotation;
        public float scale = 1f;

        public Icon Add(Shape s, Color c, bool outline = true, bool shade = true)
        {
            layers.Add(new Layer { shape = s, color = c, outline = outline, shade = shade });
            return this;
        }

        public Icon Rotated(float degrees, float scale = 1f)
        {
            rotation = degrees;
            this.scale = scale;
            return this;
        }
    }

    public static readonly Color OutlineColor = new Color(0.039f, 0.071f, 0.133f, 1f);

    public static Texture2D Render(Icon icon, int size = 128, float outlineWidth = 5f, bool silhouette = false)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        float px = 128f / size;
        float ang = -icon.rotation * Mathf.Deg2Rad;
        var center = new Vector2(64f, 64f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) * px, (y + 0.5f) * px);
                Vector2 q = Rotate(p - center, ang) / icon.scale + center;
                float toPixels = icon.scale / px;

                Color c = new Color(0f, 0f, 0f, 0f);
                float union = float.MaxValue;
                foreach (Layer l in icon.layers)
                    if (l.outline || silhouette) union = Mathf.Min(union, l.shape.Sdf(q));

                if (silhouette)
                {
                    float a = Mathf.Clamp01(0.5f - union * toPixels);
                    c = new Color(1f, 1f, 1f, a);
                }
                else
                {
                    float ao = Mathf.Clamp01(0.5f - (union * toPixels - outlineWidth));
                    c = Over(c, OutlineColor, ao);
                    float shadeK = Mathf.Lerp(1.14f, 0.84f, q.y / 128f);
                    foreach (Layer l in icon.layers)
                    {
                        float a = Mathf.Clamp01(0.5f - l.shape.Sdf(q) * toPixels);
                        if (a <= 0f) continue;
                        Color lc = l.color;
                        if (l.shade) lc = new Color(Mathf.Clamp01(lc.r * shadeK), Mathf.Clamp01(lc.g * shadeK), Mathf.Clamp01(lc.b * shadeK), lc.a);
                        c = Over(c, lc, a * lc.a);
                    }
                }
                pixels[(size - 1 - y) * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Color Over(Color dst, Color src, float a)
    {
        float outA = a + dst.a * (1f - a);
        if (outA <= 0f) return new Color(0f, 0f, 0f, 0f);
        Color rgb = (src * a + dst * dst.a * (1f - a)) / outA;
        return new Color(rgb.r, rgb.g, rgb.b, outA);
    }

    private static Vector2 Rotate(Vector2 v, float a)
    {
        float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    // ---------- Catálogo ----------

    private static Color H(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    private static readonly Color Steel = H("#C4D3E3"), SteelDark = H("#8195AB"), Gold = H("#E0B44A"), Ice = H("#BFEAFF"),
        IceDeep = H("#58C7FF"), Leather = H("#8A5A3B"), LeatherDark = H("#5E3B24"), Fur = H("#D9B58C"), Purple = H("#7A4FC4"),
        PurpleDark = H("#56358F"), Black = H("#2B3040"), Red = H("#D9483E"), Blue = H("#3F7BE8"), Wood = H("#9A6B43"),
        WoodDark = H("#6B4428"), Horn = H("#EFE6CC"), White = H("#F2F9FF"), Slot = H("#0A1222"), Green = H("#5FC27A"), Orange = H("#F08A5D");

    public static Dictionary<string, Icon> Catalog()
    {
        var d = new Dictionary<string, Icon>();

        d["helm_knight"] = new Icon()
            .Add(new Ellipse(64, 24, 9, 18), Red)
            .Add(new Box(64, 70, 36, 38, 26), Steel)
            .Add(new Box(64, 68, 25, 4.5f, 2), Slot, false, false)
            .Add(new Box(64, 50, 3, 14, 1.5f), SteelDark, false);

        d["helm_viking"] = VikingHelm(Steel, Horn);
        d["helm_horned"] = VikingHelm(SteelDark, Gold);

        d["hat_mage"] = WizardHat(Purple, PurpleDark, Gold);
        d["hat_wizard"] = WizardHat(Blue, H("#2C5AAF"), Ice);

        d["hood_ninja"] = new Icon()
            .Add(new Ellipse(64, 66, 38, 44), Black)
            .Add(new Ellipse(64, 74, 24, 20), H("#141A28"), false, false)
            .Add(new Box(64, 70, 19, 4.5f, 2), White, false, false)
            .Add(new Box(64, 96, 22, 5, 2), Red, false);

        d["chest_knight"] = Torso(Steel)
            .Add(new Box(64, 78, 12, 28, 3), Red, false)
            .Add(new Circle(33, 42, 15), SteelDark)
            .Add(new Circle(95, 42, 15), SteelDark);

        d["chest_plate"] = Torso(Steel)
            .Add(new Box(64, 70, 20, 24, 7), SteelDark, false)
            .Add(new Circle(33, 42, 15), Gold)
            .Add(new Circle(95, 42, 15), Gold)
            .Add(new Circle(52, 56, 3), Gold, false)
            .Add(new Circle(76, 56, 3), Gold, false);

        d["robe_mage"] = new Icon()
            .Add(new Poly(46, 24, 82, 24, 100, 112, 28, 112), Purple)
            .Add(new Ellipse(64, 26, 18, 8), PurpleDark, false)
            .Add(new Box(64, 62, 29, 5, 2), Gold, false)
            .Add(new Box(64, 90, 4, 20, 2), PurpleDark, false);

        d["suit_ninja"] = Torso(Black)
            .Add(new Box(64, 64, 34, 5, 2, 22), Red, false)
            .Add(new Box(28, 56, 7, 20, 4), H("#C9D7E6"))
            .Add(new Box(100, 56, 7, 20, 4), H("#C9D7E6"));

        d["fur_viking"] = new Icon()
            .Add(new Poly(38, 36, 90, 36, 96, 106, 32, 106), Leather)
            .Add(new Ellipse(64, 36, 36, 13), Fur)
            .Add(new Box(64, 80, 33, 5, 2), LeatherDark, false)
            .Add(new Box(64, 80, 6, 6, 1.5f), Gold, false);

        d["boots_steel"] = Boots(Steel, SteelDark);
        d["boots_mage"] = Boots(Purple, Gold);
        d["boots_ninja"] = Boots(Black, Red);
        d["boots_fur"] = Boots(Leather, Fur);
        d["boots_ice"] = Boots(Ice, White);

        d["sword"] = new Icon()
            .Add(new Poly(57, 12, 71, 12, 64, 0), Ice)
            .Add(new Box(64, 48, 7, 37, 1), Ice)
            .Add(new Box(64, 50, 2, 30, 1), IceDeep, false)
            .Add(new Box(64, 90, 24, 5, 2.5f), Gold)
            .Add(new Box(64, 104, 4.5f, 10, 2), Leather)
            .Add(new Circle(64, 118, 6), Gold)
            .Rotated(45f, 0.82f);

        d["staff"] = new Icon()
            .Add(new Box(64, 74, 4.5f, 48, 2), Wood)
            .Add(new Circle(64, 48, 7), WoodDark)
            .Add(new Poly(64, 4, 80, 24, 64, 44, 48, 24), IceDeep)
            .Add(new Poly(64, 12, 72, 24, 64, 34, 58, 24), Ice, false, false)
            .Rotated(-30f, 0.9f);

        d["kunai"] = new Icon()
            .Add(new Poly(64, 6, 78, 52, 64, 64, 50, 52), Ice)
            .Add(new Box(64, 58, 3, 12, 1), IceDeep, false)
            .Add(new Box(64, 80, 4.5f, 16, 2), Red)
            .Add(new Ring(64, 106, 9, 3.5f), Steel)
            .Rotated(-40f, 0.9f);

        d["shuriken"] = new Icon()
            .Add(new Poly(64, 12, 73, 55, 116, 64, 73, 73, 64, 116, 55, 73, 12, 64, 55, 55), Steel)
            .Add(new Circle(64, 64, 8), Slot, false, false)
            .Rotated(15f, 0.95f);

        d["axe"] = new Icon()
            .Add(new Box(62, 68, 5, 52, 2.5f), Wood)
            .Add(new Poly(64, 20, 102, 8, 112, 40, 102, 62, 64, 52), Steel)
            .Add(new Poly(102, 8, 112, 40, 102, 62, 106, 36), Ice, false)
            .Rotated(-35f, 0.88f);

        d["shield"] = new Icon()
            .Add(new Circle(64, 64, 50), Wood)
            .Add(new Box(46, 64, 1.5f, 46), WoodDark, false, false)
            .Add(new Box(82, 64, 1.5f, 46), WoodDark, false, false)
            .Add(new Ring(64, 64, 47, 4), SteelDark, false)
            .Add(new Circle(64, 64, 13), Steel);

        d["potion_red"] = Potion(Red);
        d["potion_blue"] = Potion(Blue);

        d["amulet"] = new Icon()
            .Add(new Cut(new Ring(64, 44, 34, 3), new Box(64, 26, 60, 30)), Gold)
            .Add(new Poly(64, 52, 88, 80, 64, 114, 40, 80), H("#9A6BFF"))
            .Add(new Poly(64, 60, 76, 80, 64, 100, 56, 80), H("#D2B8FF"), false, false);

        d["shard"] = new Icon()
            .Add(new Poly(64, 6, 92, 44, 80, 114, 48, 114, 36, 44), IceDeep)
            .Add(new Poly(64, 16, 76, 44, 66, 102, 54, 44), Ice, false, false);

        d["stat_strength"] = new Icon()
            .Add(new Box(64, 46, 9, 34, 2), Orange)
            .Add(new Box(64, 88, 28, 6, 3), Orange)
            .Add(new Box(64, 104, 6, 12, 3), Orange)
            .Rotated(45f, 0.9f);
        d["stat_mana"] = new Icon()
            .Add(new Circle(64, 80, 32), Blue)
            .Add(new Poly(64, 8, 94, 68, 34, 68), Blue)
            .Add(new Ellipse(52, 78, 7, 11), White, false, false);
        d["stat_agility"] = new Icon()
            .Add(new Poly(76, 6, 32, 72, 60, 72, 48, 122, 96, 50, 68, 50, 86, 6), Green);
        d["stat_health"] = new Icon()
            .Add(new Circle(45, 50, 25), Red)
            .Add(new Circle(83, 50, 25), Red)
            .Add(new Poly(22, 60, 106, 60, 64, 112), Red)
            .Add(new Ellipse(40, 44, 7, 9), H("#FF9C94"), false, false);

        return d;
    }

    private static Icon VikingHelm(Color metal, Color horn)
    {
        return new Icon()
            .Add(new Poly(34, 66, 10, 30, 18, 22, 44, 54), horn)
            .Add(new Poly(94, 66, 118, 30, 110, 22, 84, 54), horn)
            .Add(new Box(64, 72, 34, 32, 26), metal)
            .Add(new Box(64, 86, 36, 6, 2), Gold, false)
            .Add(new Box(64, 62, 3, 20, 1.5f), SteelDark, false);
    }

    private static Icon WizardHat(Color main, Color dark, Color band)
    {
        return new Icon()
            .Add(new Poly(66, 6, 96, 82, 34, 82), main)
            .Add(new Ellipse(64, 86, 52, 13), dark)
            .Add(new Box(64, 76, 27, 5, 2), band, false);
    }

    private static Icon Torso(Color c)
    {
        return new Icon()
            .Add(new Poly(36, 32, 92, 32, 86, 110, 42, 110), c)
            .Add(new Ellipse(64, 32, 13, 8), Slot, false, false);
    }

    private static Icon Boots(Color main, Color trim)
    {
        return new Icon()
            .Add(new Box(54, 56, 19, 32, 7), main)
            .Add(new Box(66, 92, 34, 15, 12), main)
            .Add(new Box(54, 28, 22, 8, 3), trim)
            .Add(new Box(66, 105, 35, 4, 2), trim, false);
    }

    private static Icon Potion(Color liquid)
    {
        return new Icon()
            .Add(new Box(64, 36, 10, 16, 3), H("#DDEFFA"))
            .Add(new Circle(64, 80, 34), liquid)
            .Add(new Box(64, 16, 13, 8, 3), Wood)
            .Add(new Ellipse(50, 70, 6, 11), new Color(1f, 1f, 1f, 0.7f), false, false);
    }
}
