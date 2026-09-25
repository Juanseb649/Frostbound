using UnityEngine;

[CreateAssetMenu(menuName = "Penguin/Character Class", fileName = "NewClass")]
public class CharacterClass : ScriptableObject
{
    [Header("Info")]
    public string className = "Nuevo";
    [Tooltip("Nombre que ve el jugador (Caballero de la Escarcha…).")]
    public string displayName = "";
    [Tooltip("Texto del chip de rol.")]
    public string role = "";
    [Tooltip("Fuerza, Maná, Agilidad o Salud.")]
    public string primaryAttribute = "";
    [TextArea(3, 6)] public string description = "";
    public string[] startingGear = new string[0];
    [Tooltip("Id del color de plumaje por defecto (PlumagePalette).")]
    public string defaultPlumageId = "azul";
    [Tooltip("Modelo del equipo de la clase (el que se equipa va en Ropa inicial).")]
    public GameObject gearPrefab;
    [Tooltip("Color de la clase: tiñe el material del pingüino.")]
    public Color classColor = Color.white;

    [Header("Stats Base")]
    [Tooltip("Fuerza: aumenta el daño.")]
    public int baseStrength = 5;
    [Tooltip("Mana: energía para habilidades; suma al mana máximo.")]
    public int baseMana = 5;
    [Tooltip("Agilidad: velocidad de movimiento y control de armas.")]
    public int baseAgility = 5;
    [Tooltip("Salud: puntos de vida; suma a la vida máxima.")]
    public int baseHealth = 5;

    [Header("Derivados")]
    [Tooltip("Velocidad de movimiento base (m/s).")]
    public float baseMoveSpeed = 6f;
    [Tooltip("Daño base de ataque.")]
    public float baseDamage = 10f;

    [Header("Ropa inicial")]
    [Tooltip("Prendas que se pone el pingüino al empezar con esta clase.")]
    public OutfitItem[] startingOutfit = new OutfitItem[0];

    [Header("Objetos iniciales")]
    [Tooltip("Piezas de equipo que se equipan al empezar (casco, torso, pies, arma...). El resto va a la mochila. Si está vacío se usa la Ropa inicial.")]
    public ItemDefinition[] startingItems = new ItemDefinition[0];
}
