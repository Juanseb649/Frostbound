using UnityEngine;

// Material de los cristales de hielo del mundo (cuarzo cristalino: blanco y gris perla con vetas), compartido por
// todas las zonas. Lo prepara SetupQuartzIce en Resources/Materials/Cuarzo; sin él, un gris perla sencillo.
public static class QuartzMaterial
{
    private static Material _cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _cached = null;

    public static Material Get()
    {
        if (_cached != null) return _cached;
        _cached = Resources.Load<Material>("Materials/Cuarzo");
        if (_cached == null) _cached = MenuRuins.Lit("Cuarzo", new Color(0.84f, 0.85f, 0.87f), 0.85f);
        return _cached;
    }
}
