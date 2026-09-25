using UnityEngine;

// Tiñe solo el plumaje del pingüino (cuerpo y aletas) con un MaterialPropertyBlock.
// El equipo y el material compartido no se tocan; la barriga, los ojos, el pico y las patas
// salen de las máscaras del shader y mantienen su color.
public class PenguinAppearance : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock _block;

    public Color Plumage { get; private set; } = Color.white;

    public void SetPlumage(Color color)
    {
        Plumage = color;
        if (_block == null) _block = new MaterialPropertyBlock();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (!IsPenguinPart(r)) continue;
            r.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            r.SetPropertyBlock(_block);
        }
    }

    private static bool IsPenguinPart(Renderer r)
    {
        return r.name.StartsWith("Penguin_") || r.name == "Penguin" || r.name.StartsWith("geometry");
    }
}
