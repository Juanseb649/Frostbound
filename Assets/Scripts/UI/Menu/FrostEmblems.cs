using System.Collections.Generic;
using UnityEngine;

// Emblemas de clase de "Crea tu héroe" (guía /d2r-ui): siluetas rellenas de acero escarchado con bisel, grabados
// hundidos y detalles de oro viejo, sobre un medallón de hierro. Se dibujan por código a 256 px con mipmaps, así
// que se ven nítidos a cualquier escala de interfaz. Los iconos de línea de FrostUI.Icon siguen para el resto.
public static class FrostEmblems
{
    private const int Size = 256;
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    private static Sprite _medallion;

    // Tres capas por emblema: la silueta, los grabados (hundidos) y las incrustaciones de oro (dentro de la silueta).
    private struct Shape
    {
        public System.Func<Vector2, float> body, groove, gold;
    }

    public static Sprite Class(string id)
    {
        if (Cache.TryGetValue(id, out Sprite s) && s != null) return s;
        s = Render(ShapeOf(id));
        s.name = "Emblema_" + id;
        Cache[id] = s;
        return s;
    }

    // ---------- Formas ----------

    private static Shape ShapeOf(string id)
    {
        switch (id)
        {
            case "Knight":
                return new Shape
                {
                    // Escudo de heráldica con cruz grabada y un filete interior.
                    body = p => Round(Poly(p, ShieldPts), 0.04f),
                    groove = p => Mathf.Min(Mathf.Abs(Round(Poly(p, ShieldPts), 0.04f) + 0.13f) - 0.022f,
                                            Mathf.Min(Box(p - new Vector2(0f, -0.02f), 0.075f, 0.44f), Box(p - new Vector2(0f, 0.2f), 0.33f, 0.075f))),
                    gold = p => Circle(p - new Vector2(0f, 0.2f), 0.085f),
                };
            case "Mage":
                return new Shape
                {
                    // Sombrero de punta caída con ala, cinta de oro y estrella grabada.
                    body = p => Mathf.Min(Round(Poly(p, HatPts), 0.03f), Ellipse(p - new Vector2(0f, -0.46f), 0.88f, 0.15f)),
                    groove = p => Star5(p - new Vector2(-0.02f, 0.12f), 0.17f, 0.45f),
                    gold = p => Mathf.Max(Box(p - new Vector2(0f, -0.25f), 1f, 0.06f), Round(Poly(p, HatPts), 0.03f)),
                };
            case "Ninja":
                return new Shape
                {
                    // Shuriken de cuatro puntas, centro hueco y un aro grabado.
                    body = p => Mathf.Max(Round(Poly(Rotate(p, 12f), ShurikenPts), 0.02f), -Circle(p, 0.13f)),
                    groove = p => Mathf.Abs(Circle(p, 0.27f)) - 0.022f,
                    gold = p => Mathf.Abs(Circle(p, 0.155f)) - 0.025f,
                };
            case "Viking":
                return new Shape
                {
                    // Hacha vikinga de doble hoja: mango, hojas en abanico con el filo grabado, ojo de oro y empuñadura.
                    body = p => Mathf.Min(Mathf.Min(AxeBlade(p), Segment(p, new Vector2(0f, -0.9f), new Vector2(0f, 0.86f), 0.075f)),
                                          Circle(p - new Vector2(0f, -0.9f), 0.1f)),
                    groove = p => Mathf.Max(Mathf.Abs(Circle(new Vector2(Mathf.Abs(p.x), p.y) - AxeCenter, AxeRadius) + 0.11f) - 0.02f, AxeBlade(p)),
                    gold = p => Mathf.Min(Box(p - new Vector2(0f, 0.36f), 0.1f, 0.2f) - 0.01f,
                                          Mathf.Max(Mathf.Min(Mathf.Abs(p.y + 0.48f) - 0.035f, Mathf.Abs(p.y + 0.64f) - 0.035f), Mathf.Abs(p.x) - 0.09f)),
                };
            default:
                return new Shape { body = p => Circle(p, 0.6f) };
        }
    }

    private static readonly Vector2[] ShieldPts =
    {
        new Vector2(-0.6f, 0.74f), new Vector2(0f, 0.82f), new Vector2(0.6f, 0.74f), new Vector2(0.6f, 0.08f), new Vector2(0.52f, -0.3f),
        new Vector2(0.32f, -0.62f), new Vector2(0f, -0.86f), new Vector2(-0.32f, -0.62f), new Vector2(-0.52f, -0.3f), new Vector2(-0.6f, 0.08f),
    };

    private static readonly Vector2[] HatPts =
    {
        new Vector2(-0.5f, -0.36f), new Vector2(0.5f, -0.36f), new Vector2(0.26f, 0.22f), new Vector2(0.3f, 0.5f), new Vector2(0.52f, 0.82f),
        new Vector2(0.14f, 0.6f), new Vector2(-0.06f, 0.36f),
    };

    private static readonly Vector2[] ShurikenPts =
    {
        new Vector2(0f, 0.96f), new Vector2(0.17f, 0.17f), new Vector2(0.96f, 0f), new Vector2(0.17f, -0.17f),
        new Vector2(0f, -0.96f), new Vector2(-0.17f, -0.17f), new Vector2(-0.96f, 0f), new Vector2(-0.17f, 0.17f),
    };

    private static readonly Vector2 AxeCenter = new Vector2(-0.2f, 0.36f);
    private const float AxeRadius = 0.82f;

    private static float AxeBlade(Vector2 p)
    {
        // Dos hojas simétricas en media luna: el filo es un arco grande; arriba y abajo se vacía para dejar el
        // cuello estrecho junto al mango.
        p.x = Mathf.Abs(p.x);
        float fan = Mathf.Max(Circle(p - AxeCenter, AxeRadius), -(p.x - 0.04f));
        fan = Mathf.Max(fan, Mathf.Abs(p.y - 0.36f) - 0.62f);
        fan = Mathf.Max(fan, -Circle(p - new Vector2(0.44f, -0.38f), 0.5f));
        fan = Mathf.Max(fan, -Circle(p - new Vector2(0.44f, 1.1f), 0.5f));
        return Round(fan, 0.015f);
    }

    // ---------- Funciones de distancia (negativas dentro) ----------

    private static float Circle(Vector2 p, float r) => p.magnitude - r;
    private static float Round(float d, float r) => d - r;

    private static float Box(Vector2 p, float hx, float hy)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x) - hx, Mathf.Abs(p.y) - hy);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
    }

    private static float Ellipse(Vector2 p, float rx, float ry) => (new Vector2(p.x / rx, p.y / ry).magnitude - 1f) * Mathf.Min(rx, ry);

    private static float Segment(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }

    private static Vector2 Rotate(Vector2 p, float deg)
    {
        float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
    }

    // Polígono cualquiera (Í. Quílez).
    private static float Poly(Vector2 p, Vector2[] v)
    {
        float d = Vector2.Dot(p - v[0], p - v[0]);
        float sign = 1f;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
        {
            Vector2 e = v[j] - v[i], w = p - v[i];
            Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
            d = Mathf.Min(d, Vector2.Dot(b, b));
            bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) sign = -sign;
        }
        return sign * Mathf.Sqrt(d);
    }

    // Estrella de cinco puntas (Í. Quílez).
    private static float Star5(Vector2 p, float r, float rf)
    {
        Vector2 k1 = new Vector2(0.809016994f, -0.587785252f), k2 = new Vector2(-k1.x, k1.y);
        p.x = Mathf.Abs(p.x);
        p -= 2f * Mathf.Max(Vector2.Dot(k1, p), 0f) * k1;
        p -= 2f * Mathf.Max(Vector2.Dot(k2, p), 0f) * k2;
        p.x = Mathf.Abs(p.x);
        p.y -= r;
        Vector2 ba = rf * new Vector2(-k1.y, k1.x) - new Vector2(0f, 1f);
        float h = Mathf.Clamp(Vector2.Dot(p, ba) / Vector2.Dot(ba, ba), 0f, r);
        return (p - ba * h).magnitude * Mathf.Sign(p.y * ba.x - p.x * ba.y);
    }

    // ---------- Pintado ----------

    private static readonly Vector3 Light = new Vector3(-0.45f, 0.7f, 0.55f).normalized;
    private const float Bevel = 0.075f, Outline = 0.035f;

    private static float Shade(System.Func<Vector2, float> f, Vector2 p, float d, float bevel, float sign)
    {
        const float e = 0.004f;
        Vector2 g = new Vector2(f(p + new Vector2(e, 0f)) - f(p - new Vector2(e, 0f)), f(p + new Vector2(0f, e)) - f(p - new Vector2(0f, e))) / (2f * e);
        float slope = 1f - Mathf.Clamp01(-d / bevel);
        Vector3 n = new Vector3(g.x * slope * 1.3f * sign, g.y * slope * 1.3f * sign, 1f).normalized;
        return Vector3.Dot(n, Light);
    }

    private static Sprite Render(Shape s)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
        var px = new Color[Size * Size];
        float aa = 2f / Size * 1.2f;
        Color outline = FrostboundUI.OutlineNight;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f) * 1.08f;
                float d = s.body(p);
                float cover = Mathf.Clamp01(0.5f - (d - Outline) / aa);
                if (cover <= 0f) { px[y * Size + x] = Color.clear; continue; }

                // Acero escarchado: degradado vertical de hielo claro a acero azul, iluminado por el bisel.
                float k = Mathf.InverseLerp(-0.9f, 0.9f, p.y);
                Color steel = Color.Lerp(Color.Lerp(FrostboundUI.IceDeep, FrostboundUI.OutlineNight, 0.35f), Color.Lerp(FrostboundUI.Ice, Color.white, 0.35f), k);
                float lit = Shade(s.body, p, d, Bevel, 1f);
                Color c = steel * (0.42f + 0.78f * Mathf.Max(0f, lit));
                c += Color.white * Mathf.Pow(Mathf.Max(0f, lit), 18f) * 0.35f;

                if (s.groove != null)
                {
                    float gd = s.groove(p);
                    float gin = Mathf.Clamp01(0.5f - gd / aa);
                    if (gin > 0f)
                    {
                        // Grabado hundido: más oscuro, con la luz al revés en sus paredes.
                        float gl = Shade(s.groove, p, gd, 0.03f, -1f);
                        Color dark = Color.Lerp(FrostboundUI.InnerOutline, FrostboundUI.OutlineNight, 0.55f) * (0.7f + 0.6f * Mathf.Max(0f, gl));
                        c = Color.Lerp(c, dark, gin * 0.9f);
                    }
                }
                if (s.gold != null)
                {
                    float od = s.gold(p);
                    float oin = Mathf.Clamp01(0.5f - od / aa);
                    if (oin > 0f)
                    {
                        float ol = Shade(s.gold, p, od, 0.035f, 1f);
                        Color gold = Color.Lerp(FrostboundUI.OldGold, FrostboundUI.OldGoldLight, Mathf.InverseLerp(-0.6f, 0.6f, p.y));
                        Color g = gold * (0.5f + 0.7f * Mathf.Max(0f, ol)) + Color.white * Mathf.Pow(Mathf.Max(0f, ol), 14f) * 0.3f;
                        c = Color.Lerp(c, g, oin);
                    }
                }

                // Contorno noche alrededor de la silueta para que se lea sobre cualquier fondo.
                float body = Mathf.Clamp01(0.5f - d / aa);
                c = Color.Lerp(outline, c, body);
                c.a = cover;
                px[y * Size + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply(true);
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
    }

    // Medallón de hierro detrás del emblema: disco abismo con brillo interior, aro de hierro biselado y un filete de oro.
    public static Sprite Medallion
    {
        get
        {
            if (_medallion != null) return _medallion;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            var px = new Color[Size * Size];
            float aa = 2f / Size * 1.2f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                    float r = p.magnitude;
                    float cover = Mathf.Clamp01(0.5f - (r - 0.97f) / aa);
                    if (cover <= 0f) { px[y * Size + x] = Color.clear; continue; }
                    // Disco: abismo con un resplandor frío arriba a la izquierda.
                    float glow = Mathf.Clamp01(1f - (p - new Vector2(-0.25f, 0.3f)).magnitude / 1.1f);
                    Color c = Color.Lerp(FrostboundUI.Abyss, FrostboundUI.ChipIceBg, glow * 0.9f);
                    // Sombra interior junto al aro.
                    c *= Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(0.66f, 0.84f, r));
                    // Aro de hierro biselado (0.86 - 0.97).
                    float ring = Mathf.Clamp01(0.5f - (Mathf.Abs(r - 0.915f) - 0.055f) / aa);
                    if (ring > 0f)
                    {
                        Vector2 n2 = r > 0.0001f ? p / r : Vector2.up;
                        float t = (r - 0.915f) / 0.055f;
                        Vector3 n = new Vector3(n2.x * t, n2.y * t, 1f).normalized;
                        float lit = Mathf.Max(0f, Vector3.Dot(n, Light));
                        Color iron = Color.Lerp(FrostboundUI.Iron, FrostboundUI.IronLight, lit * lit) * (0.45f + 0.55f * lit);
                        c = Color.Lerp(c, iron, ring);
                    }
                    // Filete de oro viejo por dentro del aro.
                    float gold = Mathf.Clamp01(0.5f - (Mathf.Abs(r - 0.835f) - 0.011f) / aa);
                    c = Color.Lerp(c, FrostboundUI.OldGold, gold * 0.85f);
                    c.a = cover;
                    px[y * Size + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(true);
            _medallion = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
            _medallion.name = "Medallon";
            return _medallion;
        }
    }

    // Para revisar los emblemas fuera del menú: los guarda en PNG uno al lado del otro.
    public static Texture2D Sheet()
    {
        string[] ids = { "Knight", "Mage", "Ninja", "Viking" };
        var sheet = new Texture2D(Size * ids.Length, Size * 2, TextureFormat.RGBA32, false);
        var bg = new Color[sheet.width * sheet.height];
        for (int i = 0; i < bg.Length; i++) bg[i] = FrostboundUI.Slate;
        sheet.SetPixels(bg);
        for (int i = 0; i < ids.Length; i++)
        {
            Color[] m = Medallion.texture.GetPixels();
            Color[] e = Class(ids[i]).texture.GetPixels();
            for (int j = 0; j < m.Length; j++)
            {
                int x = j % Size, y = j / Size;
                Color under = Color.Lerp(FrostboundUI.Slate, m[j], m[j].a);
                // Arriba: sobre el medallón (como en el menú, a 76 %). Abajo: solo, sobre pizarra.
                int ex = Mathf.RoundToInt((x - Size * 0.12f) / 0.76f), ey = Mathf.RoundToInt((y - Size * 0.12f) / 0.76f);
                Color top = under;
                if (ex >= 0 && ey >= 0 && ex < Size && ey < Size) { Color c = e[ey * Size + ex]; top = Color.Lerp(under, c, c.a); }
                sheet.SetPixel(i * Size + x, Size + y, top);
                Color low = Color.Lerp(FrostboundUI.Slate, e[j], e[j].a);
                sheet.SetPixel(i * Size + x, y, low);
            }
        }
        sheet.Apply();
        return sheet;
    }
}
