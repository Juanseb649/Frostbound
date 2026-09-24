using UnityEngine;

[CreateAssetMenu(menuName = "Penguin/Character Class", fileName = "NewClass")]
public class CharacterClass : ScriptableObject
{
    [Header("Info")]
    public string className = "Nuevo";
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
}
