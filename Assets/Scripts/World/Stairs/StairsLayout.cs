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
    public const float TerraceSpacing = 14f;   // distancia en Z entre terrazas
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
        to = new Vector3(s * 21f, TerraceY(i), TerraceZ(i) + TerraceDepth * 0.5f);
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
