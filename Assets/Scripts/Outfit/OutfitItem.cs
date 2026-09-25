using UnityEngine;

public enum OutfitSlot { Head, Face, Chest, Back, HandL, HandR, Feet, Body }

public enum OutfitAttachMode
{
    [Tooltip("La prenda trae el mismo esqueleto del pingüino y se deforma con él (ropa hecha a medida).")]
    Skinned,
    [Tooltip("Objeto rígido pegado a un punto de anclaje (sombreros, armas, mochilas de otros paquetes).")]
    Anchor,
    [Tooltip("Una copia por hueso, ajustada al tamaño de esa parte del cuerpo (botas: una por pie).")]
    FitToBones
}

[CreateAssetMenu(menuName = "Frostbound/Outfit Item", fileName = "NuevaPrenda")]
public class OutfitItem : ScriptableObject
{
    public string displayName = "Prenda";
    public OutfitSlot slot = OutfitSlot.Head;
    [Tooltip("Otras ranuras que también ocupa (por ejemplo, un traje con capucha ocupa Back y Head).")]
    public OutfitSlot[] alsoOccupies = new OutfitSlot[0];
    public OutfitAttachMode attachMode = OutfitAttachMode.Skinned;
    [Tooltip("Modelo (FBX o prefab) de la prenda.")]
    public GameObject prefab;
    [Tooltip("Si se asigna, reemplaza los materiales del modelo.")]
    public Material materialOverride;
    [Tooltip("Solo modo Skinned: usa únicamente estas piezas del modelo (por ejemplo \"Helm\" del equipo del caballero). Vacío = todas.")]
    public string[] onlyParts = new string[0];

    [Header("Solo modo FitToBones")]
    [Tooltip("Huesos donde se coloca una copia (Foot_L, Foot_R).")]
    public string[] fitBones = new string[0];

    [Header("Modos Anchor y FitToBones")]
    [Tooltip("En FitToBones es relativo al tamaño de la parte (0.1 = 10 %).")]
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
    [Tooltip("En FitToBones multiplica el tamaño de la parte del cuerpo.")]
    public Vector3 scale = Vector3.one;

    public bool UsesPart(string partName)
    {
        if (onlyParts == null || onlyParts.Length == 0) return true;
        foreach (string p in onlyParts)
            if (string.Equals(p, partName, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
