using System.Collections.Generic;
using UnityEngine;

// Orquestador de generación: recibe un DungeonConfig y un seed, coloca salas,
// conecta pasillos, construye la geometría prototipo (cubos) y ubica el spawn.
public class DungeonGenerator : MonoBehaviour
{
    [Header("Configuración")]
    public DungeonConfig config;

    [Header("Prueba rápida")]
    [Tooltip("Genera un piso automáticamente al pulsar Play.")]
    [SerializeField] private bool generarAlIniciar = true;
    [SerializeField] private int pisoPrueba = 0;
    [Tooltip("Seed fijo para depurar siempre el mismo layout. 0 = usa el seed de la partida.")]
    [SerializeField] private int seedDebug = 0;

    [Header("Visual prototipo")]
    [SerializeField] private Material materialPiso;
    [SerializeField] private Material materialMuro;
    [Tooltip("Prefab del pingüino; si lo asignas se instancia en el spawn.")]
    [SerializeField] private GameObject prefabJugador;

    public Vector3 PlayerSpawn { get; private set; }
    public List<RoomRect> Rooms { get; private set; }

    private Transform _root;
    private bool[,] _walkable;

    void Start()
    {
        if (generarAlIniciar && config != null)
        {
            GenerateFloor(config.dungeonId, pisoPrueba);
        }
    }

    [ContextMenu("Generar piso")]
    public void GenerarPisoDesdeEditor()
    {
        if (config == null)
        {
            Debug.LogWarning("[DungeonGenerator] Asigna un DungeonConfig primero.");
            return;
        }
        GenerateFloor(config.dungeonId, pisoPrueba);
    }

    // Genera el piso usando el seed de la partida actual.
    public void GenerateFloor(string dungeonId, int floorIndex)
    {
        int seed = GameSession.Instance != null
            ? GameSession.Instance.FloorSeed(dungeonId, floorIndex)
            : SeedUtil.Combine(1234567, SeedUtil.FromString(dungeonId), floorIndex);

        GenerateFloorWithSeed(dungeonId, floorIndex, seed);
    }

    // Versión con seed explícito (útil para transiciones o depuración).
    public void GenerateFloorWithSeed(string dungeonId, int floorIndex, int seed)
    {
        if (config == null)
        {
            Debug.LogWarning("[DungeonGenerator] Falta el DungeonConfig.");
            return;
        }
        if (seedDebug != 0) seed = seedDebug;

        ClearRoot();

        var rngLayout = new DeterministicRng(SeedUtil.Combine(seed, 11));

        int targetRooms = rngLayout.NextInt(Mathf.Max(2, config.roomsMin), Mathf.Max(3, config.roomsMax + 1));
        Rooms = RoomPlacer.PlaceRooms(rngLayout, targetRooms, config.roomSizeMin, config.roomSizeMax, config.gridExtent, config.roomGap);
        RoomPlacer.AssignRoles(Rooms, rngLayout);

        List<CorridorPath> corridors = CorridorBuilder.BuildCorridors(Rooms, rngLayout, config.extraLinks);

        CarveGrid(corridors);
        BuildGeometry();
        SpawnPlayerAndMarkers();

        Debug.Log($"[DungeonGenerator] '{config.dungeonName}' piso {floorIndex}: {Rooms.Count} salas, seed {seed}.");
    }

    // ----- Grid lógico -----

    private void CarveGrid(List<CorridorPath> corridors)
    {
        _walkable = new bool[config.gridExtent, config.gridExtent];

        foreach (var room in Rooms)
        {
            CarveRoom(room);
        }

        foreach (var path in corridors)
        {
            // Tramo horizontal (From -> Mid) y tramo vertical (Mid -> To).
            int fx = Mathf.RoundToInt(path.From.x);
            int fz = Mathf.RoundToInt(path.From.y);
            int mx = Mathf.RoundToInt(path.Mid.x);
            int tz = Mathf.RoundToInt(path.To.y);
            CarveLine(fx, fz, mx, fz);
            CarveLine(mx, fz, mx, tz);
        }
    }

    private void CarveRoom(RoomRect room)
    {
        int w = _walkable.GetLength(0);
        int h = _walkable.GetLength(1);

        for (int z = room.MinZ; z <= room.MaxZ; z++)
        {
            for (int x = room.MinX; x <= room.MaxX; x++)
            {
                if (x >= 0 && z >= 0 && x < w && z < h) _walkable[x, z] = true;
            }
        }
    }

    private void CarveLine(int x0, int z0, int x1, int z1)
    {
        int width = Mathf.Clamp(config.corridorWidthCells, 1, 3);
        int half = width / 2;

        if (z0 == z1)
        {
            int from = Mathf.Min(x0, x1);
            int to = Mathf.Max(x0, x1);
            for (int x = from; x <= to; x++)
            {
                for (int d = 0; d < width; d++) CarveCell(x, z0 + d - half);
            }
        }
        else
        {
            int from = Mathf.Min(z0, z1);
            int to = Mathf.Max(z0, z1);
            for (int z = from; z <= to; z++)
            {
                for (int d = 0; d < width; d++) CarveCell(x0 + d - half, z);
            }
        }
    }

    private void CarveCell(int x, int z)
    {
        if (x < 0 || z < 0 || x >= _walkable.GetLength(0) || z >= _walkable.GetLength(1)) return;
        _walkable[x, z] = true;
    }

    // ----- Geometría prototipo -----

    private void BuildGeometry()
    {
        float cs = config.cellSize;
        float wallH = config.wallHeight;
        int w = _walkable.GetLength(0);
        int h = _walkable.GetLength(1);

        _root = new GameObject("Dungeon_Root").transform;
        _root.SetParent(transform, false);

        Material piso = GetPrototypeMaterial(ref materialPiso, new Color(0.72f, 0.76f, 0.80f));
        Material muro = GetPrototypeMaterial(ref materialMuro, new Color(0.45f, 0.55f, 0.65f));
        const float grosor = 0.4f;

        // Pisos: une tramos horizontales de celdas contiguas en un solo cubo.
        for (int z = 0; z < h; z++)
        {
            int x = 0;
            while (x < w)
            {
                if (!_walkable[x, z])
                {
                    x++;
                    continue;
                }
                int start = x;
                while (x < w && _walkable[x, z]) x++;
                int len = x - start;
                AddBox($"Piso_z{z}_{start}", new Vector3((start + len * 0.5f) * cs, -0.25f, (z + 0.5f) * cs), new Vector3(len * cs, 0.5f, cs), piso);
            }
        }

        // Muros paralelos al eje X: frontera entre las filas bz-1 y bz.
        for (int bz = 0; bz <= h; bz++)
        {
            int runStart = -1;
            for (int x = 0; x <= w; x++)
            {
                bool edge = x < w && IsBoundaryRowEdge(x, bz, h);
                if (edge && runStart < 0) runStart = x;
                if (!edge && runStart >= 0)
                {
                    int len = x - runStart;
                    AddBox($"Muro_X_z{bz}_{runStart}", new Vector3((runStart + len * 0.5f) * cs, wallH * 0.5f, bz * cs), new Vector3(len * cs, wallH, grosor), muro);
                    runStart = -1;
                }
            }
        }

        // Muros paralelos al eje Z: frontera entre las columnas bx-1 y bx.
        for (int bx = 0; bx <= w; bx++)
        {
            int runStart = -1;
            for (int z = 0; z <= h; z++)
            {
                bool edge = z < h && IsBoundaryColEdge(bx, z, w);
                if (edge && runStart < 0) runStart = z;
                if (!edge && runStart >= 0)
                {
                    int len = z - runStart;
                    AddBox($"Muro_Z_x{bx}_{runStart}", new Vector3(bx * cs, wallH * 0.5f, (runStart + len * 0.5f) * cs), new Vector3(grosor, wallH, len * cs), muro);
                    runStart = -1;
                }
            }
        }
    }

    private bool IsBoundaryRowEdge(int x, int bz, int h)
    {
        bool below = bz > 0 && _walkable[x, bz - 1];
        bool above = bz < h && _walkable[x, bz];
        return below != above;
    }

    private bool IsBoundaryColEdge(int bx, int z, int w)
    {
        bool left = bx > 0 && _walkable[bx - 1, z];
        bool right = bx < w && _walkable[bx, z];
        return left != right;
    }

    private Material GetPrototypeMaterial(ref Material field, Color color)
    {
        if (field != null) return field;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");

        field = new Material(shader) { color = color };
        return field;
    }

    private void AddBox(string name, Vector3 position, Vector3 scale, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(_root, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        if (material != null) cube.GetComponent<Renderer>().sharedMaterial = material;
    }

    // ----- Spawn y marcadores -----

    private void SpawnPlayerAndMarkers()
    {
        RoomRect entry = default;
        foreach (var room in Rooms)
        {
            if (room.role == RoomRole.Entry)
            {
                entry = room;
                break;
            }
        }

        Vector2 center = entry.CenterCell;
        PlayerSpawn = new Vector3((center.x + 0.5f) * config.cellSize, 1.2f, (center.y + 0.5f) * config.cellSize);

        if (prefabJugador != null)
        {
            Instantiate(prefabJugador, PlayerSpawn, Quaternion.identity);
        }

        AddFlatMarker(new Vector3(PlayerSpawn.x, 0.06f, PlayerSpawn.z), Color.green, "Spawn_Jugador");

        // Pilares de colores para identificar salas especiales en el prototipo.
        foreach (var room in Rooms)
        {
            Vector2 c = room.CenterCell;
            Vector3 pos = new Vector3((c.x + 0.5f) * config.cellSize, 1f, (c.y + 0.5f) * config.cellSize);
            if (room.role == RoomRole.Boss) AddPillarMarker(pos, Color.red, "Sala_Jefe");
            else if (room.role == RoomRole.Treasure) AddPillarMarker(pos, Color.yellow, "Sala_Tesoro");
        }
    }

    private void AddFlatMarker(Vector3 position, Color color, string name)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = name;
        RemoveCollider(marker);
        marker.transform.SetParent(_root, false);
        marker.transform.position = position;
        marker.transform.localScale = new Vector3(1.5f, 0.12f, 1.5f);
        ApplyColor(marker, color);
    }

    private void AddPillarMarker(Vector3 position, Color color, string name)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = name;
        RemoveCollider(marker);
        marker.transform.SetParent(_root, false);
        marker.transform.position = position;
        marker.transform.localScale = new Vector3(0.8f, 2f, 0.8f);
        ApplyColor(marker, color);
    }

    private void RemoveCollider(GameObject go)
    {
        var collider = go.GetComponent<Collider>();
        if (collider == null) return;
        if (Application.isPlaying) Destroy(collider);
        else DestroyImmediate(collider);
    }

    private void ApplyColor(GameObject go, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
        go.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = color };
    }

    private void ClearRoot()
    {
        if (_root == null) return;
        if (Application.isPlaying) Destroy(_root.gameObject);
        else DestroyImmediate(_root.gameObject);
        _root = null;
    }

    // Visualiza las salas al seleccionar el objeto en la escena.
    private void OnDrawGizmosSelected()
    {
        if (Rooms == null || config == null) return;
        foreach (var room in Rooms)
        {
            switch (room.role)
            {
                case RoomRole.Entry: Gizmos.color = Color.green; break;
                case RoomRole.Boss: Gizmos.color = Color.red; break;
                case RoomRole.Treasure: Gizmos.color = Color.yellow; break;
                default: Gizmos.color = Color.cyan; break;
            }

            Vector3 min = new Vector3(room.MinX * config.cellSize, 0f, room.MinZ * config.cellSize);
            Vector3 max = new Vector3((room.MaxX + 1) * config.cellSize, 2f, (room.MaxZ + 1) * config.cellSize);
            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
