using UnityEngine;

// Kit de marcos del "gótico de escarcha" (guía /d2r-ui), generado por código en una sola textura (hace de atlas):
// hierro forjado con esquinas biseladas a 45°, luz desde arriba a la izquierda, remaches, escarcha en la arista de
// arriba y una placa de piedra con grano. Los marcos llevan el centro transparente (la placa va aparte y se tiñe),
// así los paneles conservan sus colores de relleno. Todo se usa como sprite 9-slice.
public static class FrostFrames
{
    public enum Metal { Iron, Gold, Blood, Silver }

    private const int Atlas = 512;
    private static Texture2D _tex;
    private static Sprite _panel, _button, _buttonGold, _buttonBlood, _plate, _slot, _slotSilver, _divider;

    // La textura con todos los marcos (para revisarla desde el editor).
    public static Texture2D AtlasTexture { get { Build(); return _tex; } }

    // Marco grande de panel (banda de 10 px, remaches y escarcha).
    public static Sprite Panel { get { Build(); return _panel; } }
    // Placa de piedra con grano (se tiñe con el color de relleno).
    public static Sprite Plate { get { Build(); return _plate; } }
    // Celda de inventario: marco fino.
    public static Sprite Slot { get { Build(); return _slot; } }
    // Marco fino de plata escarchada: casi blanco, pensado para teñirse (rareza de la casilla, foco en oro).
    public static Sprite SlotSilver { get { Build(); return _slotSilver; } }
    // Separador horizontal con un rombo en el centro.
    public static Sprite Divider { get { Build(); return _divider; } }

    // Marco de botón en hierro, oro viejo o sangre.
    public static Sprite Button(Metal metal)
    {
        Build();
        return metal == Metal.Gold ? _buttonGold : metal == Metal.Blood ? _buttonBlood : _button;
    }

    private static void Build()
    {
        if (_tex != null) return;
        _tex = new Texture2D(Atlas, Atlas, TextureFormat.RGBA32, false) { name = "Marcos de escarcha", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[Atlas * Atlas];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

        // Regiones del atlas: (x, y, tamaño). Cada marco se dibuja en su cuadro y se recorta como sprite.
        _panel = Frame(px, 0, 0, 128, 12, 10f, 3, Metal.Iron, true, 34);
        _button = Frame(px, 128, 0, 96, 9, 6f, 0, Metal.Iron, true, 22);
        _buttonGold = Frame(px, 224, 0, 96, 9, 6f, 0, Metal.Gold, true, 22);
        _buttonBlood = Frame(px, 320, 0, 96, 9, 6f, 0, Metal.Blood, false, 22);
        _slot = Frame(px, 416, 0, 64, 6, 4f, 0, Metal.Iron, false, 14);
        _slotSilver = Frame(px, 384, 128, 64, 6, 4f, 0, Metal.Silver, false, 14);
        _plate = PlateSprite(px, 0, 128, 128, 10);
        _divider = DividerSprite(px, 128, 128, 256, 32);

        _tex.SetPixels32(px);
        _tex.Apply();
    }

    // Marco biselado: anillo de metal con labio claro, banda con relieve y surco interior; centro transparente.
    private static Sprite Frame(Color32[] px, int ox, int oy, int n, int chamfer, float band, int rivets, Metal metal, bool frost, int border)
    {
        Color dark = Base(metal) * 0.35f, lit = Base(metal) * 1.35f, mid = Base(metal);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float e = EdgeDistance(x, y, n, chamfer, out Vector2 normal);
                if (e < 0f || e > band + 2.5f) continue;
                // Luz desde arriba a la izquierda: los lados que miran ahí brillan, los de abajo a la derecha se apagan.
                float light = Vector2.Dot(normal, new Vector2(-0.6f, 0.8f));
                float grain = Noise(ox + x, oy + y) * 0.18f - 0.09f;
                Color c;
                float a = 1f;
                if (e < 1.2f) c = dark;                                          // filo exterior
                else if (e < 2.4f) c = Color.Lerp(mid, lit, 0.6f + light * 0.4f);  // labio iluminado
                else if (e < band) c = mid * (0.85f + light * 0.3f + grain);      // banda
                else if (e < band + 1.4f) c = dark * 0.8f;                        // surco interior
                else { c = dark * 0.6f; a = Mathf.Clamp01(1f - (e - band - 1.4f) / 1.1f) * 0.8f; } // sombra hacia dentro
                // Escarcha en la arista de arriba: motas claras y frías.
                if (frost && normal.y > 0.5f && e < band && Noise(ox + x + 7, oy + y + 3) > 0.86f)
                    c = Color.Lerp(c, new Color(0.85f, 0.95f, 1f), 0.75f);
                Put(px, ox + x, oy + y, c, a);
            }
        // Remaches en las esquinas, sobre la banda.
        if (rivets > 0)
        {
            float r = band * 0.5f + chamfer * 0.55f;
            Vector2[] corners = { new Vector2(r, r), new Vector2(n - 1 - r, r), new Vector2(r, n - 1 - r), new Vector2(n - 1 - r, n - 1 - r) };
            foreach (Vector2 cpt in corners) Rivet(px, ox, oy, cpt, rivets, mid, lit, dark);
        }
        var sprite = Sprite.Create(_tex, new Rect(ox, oy, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprite.name = "Marco " + metal + " " + n;
        return sprite;
    }

    // Placa de piedra: casi blanca con grano y vetas (se tiñe con el color del relleno), esquinas biseladas.
    private static Sprite PlateSprite(Color32[] px, int ox, int oy, int n, int chamfer)
    {
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float e = EdgeDistance(x, y, n, chamfer, out _);
                if (e < 0f) continue;
                float v = 0.86f + Noise(ox + x, oy + y) * 0.1f + Mathf.PerlinNoise(x * 0.05f, y * 0.12f) * 0.08f;
                Put(px, ox + x, oy + y, new Color(v, v, v), Mathf.Clamp01(e));
            }
        float b = chamfer + 2;
        var sprite = Sprite.Create(_tex, new Rect(ox, oy, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        sprite.name = "Placa";
        return sprite;
    }

    // Línea de hierro que se desvanece hacia los extremos con un rombo de oro en el centro.
    private static Sprite DividerSprite(Color32[] px, int ox, int oy, int w, int h)
    {
        float cy = h * 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = Mathf.Abs(x - w * 0.5f) / (w * 0.5f);
                float line = Mathf.Clamp01(1.5f - Mathf.Abs(y - cy));
                float fade = Mathf.Clamp01((1f - u) * 2.5f);
                float diamond = Mathf.Abs(x - w * 0.5f) + Mathf.Abs(y - cy);
                if (diamond < 7f)
                {
                    float k = diamond < 5f ? 1f : 0.5f;
                    Put(px, ox + x, oy + y, Color.Lerp(FrostboundUI.OldGold, FrostboundUI.OldGoldLight, (cy - y + 5f) / 10f) * k, 1f);
                }
                else if (line * fade > 0f)
                    Put(px, ox + x, oy + y, FrostboundUI.IronLight, line * fade);
            }
        var sprite = Sprite.Create(_tex, new Rect(ox, oy, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero);
        sprite.name = "Separador";
        return sprite;
    }

    private static void Rivet(Color32[] px, int ox, int oy, Vector2 c, int radius, Color mid, Color lit, Color dark)
    {
        for (int y = -radius - 1; y <= radius + 1; y++)
            for (int x = -radius - 1; x <= radius + 1; x++)
            {
                float d = Mathf.Sqrt(x * x + y * y);
                if (d > radius + 0.5f) continue;
                float shade = Mathf.Clamp01(0.5f + (-x + y) / (radius * 2.2f));
                Color col = d > radius - 0.6f ? dark : Color.Lerp(mid, lit * 1.2f, shade);
                Put(px, ox + Mathf.RoundToInt(c.x) + x, oy + Mathf.RoundToInt(c.y) + y, col, 1f);
            }
    }

    // Distancia hacia dentro desde el borde del cuadrado con esquinas biseladas, y la normal del lado más cercano.
    private static float EdgeDistance(int x, int y, int n, int chamfer, out Vector2 normal)
    {
        float fx = x + 0.5f, fy = y + 0.5f;
        float left = fx, right = n - fx, bottom = fy, top = n - fy;
        float dx = Mathf.Min(left, right), dy = Mathf.Min(bottom, top);
        float diag = (dx + dy - chamfer) * 0.7071f;
        float e = Mathf.Min(Mathf.Min(dx, dy), diag);
        if (e == diag)
            normal = new Vector2(left < right ? -0.7071f : 0.7071f, bottom < top ? -0.7071f : 0.7071f);
        else if (e == dx)
            normal = new Vector2(left < right ? -1f : 1f, 0f);
        else
            normal = new Vector2(0f, bottom < top ? -1f : 1f);
        return e;
    }

    private static Color Base(Metal metal)
    {
        switch (metal)
        {
            case Metal.Gold: return FrostboundUI.OldGold;
            case Metal.Blood: return FrostboundUI.Blood;
            case Metal.Silver: return new Color(0.74f, 0.77f, 0.82f);
            default: return FrostboundUI.Iron;
        }
    }

    private static void Put(Color32[] px, int x, int y, Color c, float a)
    {
        if (x < 0 || y < 0 || x >= Atlas || y >= Atlas) return;
        c.a = a;
        px[y * Atlas + x] = c;
    }

    private static float Noise(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }
}
