using System.Collections.Generic;
using UnityEngine;

// Planta del interior del castillo a partir de la seed de la partida (función pura, se puede testear).
// Rejilla de celdas de 3 m: vestíbulo de entrada, salas temáticas unidas por galerías anchas y,
// en el extremo opuesto, la sala del trono del ninja maldito.
public static class CastleInteriorLayout
{
    public enum Theme { Vestibule, Nave, Library, Armory, Crypt, Cloister, Refectory, Throne }

    public class Room
    {
        public RoomRect rect;
        public Theme theme;
        public int troops;
        public int elites;
        public Vector2 Center => rect.CenterCell;
    }

    public class Result
    {
        public int size;
        public bool[,] floor;
        public readonly List<Room> rooms = new List<Room>();
        public readonly List<CorridorPath> corridors = new List<CorridorPath>();
        public Room Entry => rooms.Find(r => r.theme == Theme.Vestibule);
        public Room Throne => rooms.Find(r => r.theme == Theme.Throne);
        public bool IsFloor(int x, int z) => x >= 0 && z >= 0 && x < size && z < size && floor[x, z];
    }

    public const float Cell = 3f;
    public const int Size = 28;
    public const int CorridorWidth = 2;
    public const float WallHeight = 5f;

    public static Result Generate(int seed)
    {
        var rng = new DeterministicRng(SeedUtil.Combine(seed, SeedUtil.FromString("castillo_interior")));
        var r = new Result { size = Size, floor = new bool[Size, Size] };

        // Entrada y trono en esquinas opuestas (elegidas por la seed); el resto, repartidas.
        bool flip = rng.Chance(0.5f);
        var entry = new RoomRect { origin = new Vector2Int(flip ? Size - 8 : 1, 1), size = new Vector2Int(7, 6), role = RoomRole.Entry };
        var throne = new RoomRect { origin = new Vector2Int(flip ? 1 : Size - 12, Size - 12), size = new Vector2Int(11, 11), role = RoomRole.Boss };
        var rects = new List<RoomRect> { entry, throne };
        int target = rng.NextInt(5, 8);
        for (int i = 0; i < target; i++)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                int w = rng.NextInt(5, 9), h = rng.NextInt(5, 9);
                var c = new RoomRect { origin = new Vector2Int(rng.NextInt(1, Size - w - 1), rng.NextInt(1, Size - h - 1)), size = new Vector2Int(w, h), role = RoomRole.Normal };
                bool ok = true;
                foreach (RoomRect o in rects) if (c.Overlaps(o, 2)) { ok = false; break; }
                if (!ok) continue;
                rects.Add(c);
                break;
            }
        }

        var themes = new List<Theme> { Theme.Nave, Theme.Library, Theme.Armory, Theme.Crypt, Theme.Cloister, Theme.Refectory };
        rng.Shuffle(themes);
        int t = 0;
        foreach (RoomRect rect in rects)
        {
            var room = new Room { rect = rect };
            if (rect.role == RoomRole.Entry) room.theme = Theme.Vestibule;
            else if (rect.role == RoomRole.Boss) room.theme = Theme.Throne;
            else
            {
                room.theme = themes[t % themes.Count];
                t++;
                int area = rect.size.x * rect.size.y;
                room.troops = Mathf.Clamp(area / 12, 3, 6) + rng.NextInt(0, 2);
                // Desde aquí aparecen las clases malditas: casi todas las salas traen al menos una élite.
                room.elites = rng.Chance(0.8f) ? 1 + (rng.Chance(0.3f) ? 1 : 0) : 0;
            }
            if (room.theme == Theme.Throne) room.elites = 4;
            r.rooms.Add(room);
        }

        foreach (Room room in r.rooms)
            for (int x = room.rect.MinX; x <= room.rect.MaxX; x++)
                for (int z = room.rect.MinZ; z <= room.rect.MaxZ; z++)
                    r.floor[x, z] = true;

        r.corridors.AddRange(CorridorBuilder.BuildCorridors(rects, rng, 2));
        foreach (CorridorPath p in r.corridors)
        {
            Carve(r, p.From, p.Mid);
            Carve(r, p.Mid, p.To);
        }
        return r;
    }

    private static void Carve(Result r, Vector2 a, Vector2 b)
    {
        int x0 = Mathf.RoundToInt(a.x), z0 = Mathf.RoundToInt(a.y), x1 = Mathf.RoundToInt(b.x), z1 = Mathf.RoundToInt(b.y);
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(z1 - z0));
        for (int i = 0; i <= steps; i++)
        {
            float k = steps == 0 ? 0f : i / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, k)), z = Mathf.RoundToInt(Mathf.Lerp(z0, z1, k));
            for (int dx = 0; dx < CorridorWidth; dx++)
                for (int dz = 0; dz < CorridorWidth; dz++)
                {
                    int cx = Mathf.Clamp(x + dx, 1, r.size - 2), cz = Mathf.Clamp(z + dz, 1, r.size - 2);
                    r.floor[cx, cz] = true;
                }
        }
    }

    // ¿Se puede llegar a todas las salas desde la entrada? (para los tests)
    public static bool AllRoomsReachable(Result r)
    {
        Room entry = r.Entry;
        if (entry == null) return false;
        var seen = new bool[r.size, r.size];
        var queue = new Queue<Vector2Int>();
        var start = new Vector2Int(entry.rect.MinX, entry.rect.MinZ);
        queue.Enqueue(start);
        seen[start.x, start.y] = true;
        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            foreach (Vector2Int d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                Vector2Int n = c + d;
                if (!r.IsFloor(n.x, n.y) || seen[n.x, n.y]) continue;
                seen[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }
        foreach (Room room in r.rooms)
            if (!seen[room.rect.MinX, room.rect.MinZ]) return false;
        return true;
    }

    public static string ThemeName(Theme t)
    {
        switch (t)
        {
            case Theme.Vestibule: return "Vestíbulo";
            case Theme.Nave: return "Nave de los bancos";
            case Theme.Library: return "Biblioteca helada";
            case Theme.Armory: return "Armería";
            case Theme.Crypt: return "Cripta";
            case Theme.Cloister: return "Claustro";
            case Theme.Refectory: return "Refectorio";
            default: return "Sala del trono";
        }
    }
}
