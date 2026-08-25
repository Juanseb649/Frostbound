using UnityEngine;

[CreateAssetMenu(fileName = "Castillo_Nuevo", menuName = "Frostbound/Dungeon Config")]
public class DungeonConfig : ScriptableObject
{
    [Header("Identidad")]
    public string dungeonId = "castillo_1";
    public string dungeonName = "Castillo";

    [Header("Pisos")]
    [Min(1)] public int floors = 3;

    [Header("Salas por piso")]
    [Tooltip("Cantidad de salas objetivo del piso (aleatoria entre min y max).")]
    public int roomsMin = 6;
    public int roomsMax = 9;

    [Tooltip("Tamaño de sala en celdas.")]
    public Vector2Int roomSizeMin = new Vector2Int(3, 3);
    public Vector2Int roomSizeMax = new Vector2Int(8, 8);

    [Header("Grid lógico")]
    [Tooltip("Tamaño del área lógica donde se reparten las salas.")]
    [Min(8)] public int gridExtent = 28;
    [Tooltip("Separación mínima entre salas.")]
    [Min(1)] public int roomGap = 2;
    [Tooltip("Conexiones extra sobre el camino mínimo (rutas alternativas).")]
    [Min(0)] public int extraLinks = 2;

    [Header("Construcción")]
    [Tooltip("Metros por celda del grid.")]
    public float cellSize = 4f;
    public float wallHeight = 4f;
    [Range(1, 3)] public int corridorWidthCells = 1;
}
