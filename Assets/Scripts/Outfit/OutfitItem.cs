using UnityEngine;

public enum OutfitSlot { Head, Face, Chest, Back, HandL, HandR, Feet, Body }

public enum OutfitAttachMode
{
    [Tooltip("La prenda trae el mismo esqueleto del pingüino y se deforma con él (ropa hecha a medida).")]
    Skinned,
    [Tooltip("Objeto rígido pegado a un punto de anclaje (sombreros, armas, mochilas de otros paquetes).")]
    Anchor
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

    [Header("Solo modo Anchor")]
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
    public Vector3 scale = Vector3.one;
}
