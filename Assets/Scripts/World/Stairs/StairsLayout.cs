using UnityEngine;

// La Escalinata: terrazas talladas en la cara norte del Frostspire, unidas por tramos de escalera en zigzag.
// Se sube hacia el sur (hacia la cámara) y al norte queda el precipicio con el reino a lo lejos, cada vez más abajo.
// Arriba, la explanada donde espera Rocco Vendaval.
public static class StairsLayout
{
    public const string ArrivalFromFoothills = "inicio_escalinata";
    public const string ArrivalFromStairs = "desde_escalinata";
    public const string BonfirePrefix = "escalinata_hoguera_";

    public const int Terraces = 7;
    public const float TerraceSpacing = 20f;   // distancia en Z entre terrazas (deja sitio a la curva del tramo)
    public const float TerraceRise = 5.5f;     // altura entre terrazas
    public const float TerraceDepth = 8f;
    public const float HalfWidth = 24f;
    public const float FlightWidth = 3.4f;
    public const float ArenaRadius = 15f;
    public const float KingdomDepth = -70f;    // altura del valle del reino

    public static float TerraceZ(int i) => -i * TerraceSpacing;
    public static float TerraceY(int i) => i * TerraceRise;
    public static Vector3 TerraceCenter(int i) => new Vector3(0f, TerraceY(i), TerraceZ(i));

    // Lado del tramo que sube a la terraza i: alterna este / oeste.
    public static float FlightSide(int i) => i % 2 == 1 ? 1f : -1f;

    // Tramo de escalera de la terraza i-1 a la i: del borde sur de la de abajo al borde norte de la de arriba.
    public static void Flight(int i, out Vector3 from, out Vector3 to)
    {
        float s = FlightSide(i);
        from = new Vector3(s * 10f, TerraceY(i - 1), TerraceZ(i - 1) - TerraceDepth * 0.5f);
        to = new Vector3(s * 14f, TerraceY(i), TerraceZ(i) + TerraceDepth * 0.5f);
    }

    // Punto del tramo curvo (S suave): sale perpendicular a la terraza de abajo y llega perpendicular a la de arriba,
    // así los huecos de los muros encajan con los muretes del tramo y no queda ningún agujero.
    public static Vector3 FlightPoint(Vector3 from, Vector3 to, float t)
    {
        Vector3 q = Bezier(from, to, t);
        // La altura sube según la distancia recorrida: pendiente pareja en toda la curva.
        float done = 0f, total = 0f;
        Vector3 prev = Bezier(from, to, 0f);
        for (int i = 1; i <= 32; i++)
        {
            float ti = i / 32f;
            Vector3 cur = Bezier(from, to, ti);
            float d = Vector2.Distance(new Vector2(prev.x, prev.z), new Vector2(cur.x, cur.z));
            if (ti <= t) done += d;
            else if (ti - 1f / 32f < t) done += d * (t - (ti - 1f / 32f)) * 32f;
            total += d;
            prev = cur;
        }
        q.y = Mathf.Lerp(from.y, to.y, total > 0f ? done / total : t);
        return q;
    }

    private static Vector3 Bezier(Vector3 from, Vector3 to, float t)
    {
        float k = Mathf.Abs(from.z - to.z) * 0.5f;
        Vector3 p1 = from + new Vector3(0f, 0f, -k), p2 = to + new Vector3(0f, 0f, k);
        float u = 1f - t;
        return u * u * u * from + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * to;
    }

    public static float ArenaY => TerraceY(Terraces - 1) + 3.5f;
    public static Vector3 ArenaCenter => new Vector3(0f, ArenaY, TerraceZ(Terraces - 1) - 27f);

    public static void FinalFlight(out Vector3 from, out Vector3 to)
    {
        from = new Vector3(0f, TerraceY(Terraces - 1), TerraceZ(Terraces - 1) - TerraceDepth * 0.5f);
        to = new Vector3(0f, ArenaY, ArenaCenter.z + ArenaRadius - 0.5f);
    }

    public static Vector3 Entry => new Vector3(-18f, 0.1f, 0f);
    public static Vector3 BottomGate => new Vector3(-HalfWidth + 1.2f, 0f, 0f);
    public static Vector3 ArenaGate => ArenaCenter + new Vector3(0f, 0f, -ArenaRadius + 1.5f);
}
