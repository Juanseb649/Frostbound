using System.Collections.Generic;
using UnityEngine;

public enum RoomRole { Normal, Entry, Treasure, Boss }

// Sala rectangular en coordenadas del grid lógico (celdas).
[System.Serializable]
public struct RoomRect
{
    public Vector2Int origin; // celda mínima (x, z)
    public Vector2Int size;   // tamaño en celdas
    public RoomRole role;

    public int MinX => origin.x;
    public int MinZ => origin.y;
    public int MaxX => origin.x + size.x - 1;
    public int MaxZ => origin.y + size.y - 1;

    // Centro de la sala como índice de celda (puede ser .5).
    public Vector2 CenterCell => new Vector2(origin.x + (size.x - 1) * 0.5f, origin.y + (size.y - 1) * 0.5f);

    public bool Overlaps(RoomRect other, int gap)
    {
        return MinX - gap <= other.MaxX && MaxX + gap >= other.MinX &&
               MinZ - gap <= other.MaxZ && MaxZ + gap >= other.MinZ;
    }

    public bool ContainsCell(int x, int z)
    {
        return x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
    }
}

public static class RoomPlacer
{
    // Reparte salas sin solaparse probando posiciones aleatorias (muestreo con rechazo).
    public static List<RoomRect> PlaceRooms(DeterministicRng rng, int targetCount, Vector2Int sizeMin, Vector2Int sizeMax, int extent, int gap, int maxAttemptsPerRoom = 200)
    {
        var rooms = new List<RoomRect>();
        extent = Mathf.Max(extent, 8);

        for (int i = 0; i < targetCount; i++)
        {
            for (int attempt = 0; attempt < maxAttemptsPerRoom; attempt++)
            {
                int w = rng.NextInt(Mathf.Max(1, sizeMin.x), Mathf.Max(2, sizeMax.x + 1));
                int h = rng.NextInt(Mathf.Max(1, sizeMin.y), Mathf.Max(2, sizeMax.y + 1));
                int x = rng.NextInt(0, Mathf.Max(1, extent - w));
                int z = rng.NextInt(0, Mathf.Max(1, extent - h));

                var candidate = new RoomRect
                {
                    origin = new Vector2Int(x, z),
                    size = new Vector2Int(w, h),
                    role = RoomRole.Normal
                };

                bool fits = true;
                foreach (var existing in rooms)
                {
                    if (candidate.Overlaps(existing, gap))
                    {
                        fits = false;
                        break;
                    }
                }

                if (fits)
                {
                    rooms.Add(candidate);
                    break;
                }
            }
        }
        return rooms;
    }

    // Marca la entrada (aleatoria) y el jefe (la sala más lejana a la entrada).
    public static void AssignRoles(List<RoomRect> rooms, DeterministicRng rng)
    {
        if (rooms.Count == 0) return;

        if (rooms.Count == 1)
        {
            SetRole(rooms, 0, RoomRole.Entry);
            return;
        }

        int entryIndex = rng.NextInt(0, rooms.Count);
        SetRole(rooms, entryIndex, RoomRole.Entry);

        Vector2 entryCenter = rooms[entryIndex].CenterCell;
        int bossIndex = entryIndex == 0 ? 1 : 0;
        float bestDistance = -1f;
        for (int i = 0; i < rooms.Count; i++)
        {
            if (i == entryIndex) continue;
            float d = Vector2.Distance(rooms[i].CenterCell, entryCenter);
            if (d > bestDistance)
            {
                bestDistance = d;
                bossIndex = i;
            }
        }
        SetRole(rooms, bossIndex, RoomRole.Boss);

        // Una sala normal pasa a ser la del tesoro si hay suficientes salas.
        if (rooms.Count >= 4)
        {
            var candidates = new List<int>();
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].role == RoomRole.Normal) candidates.Add(i);
            }

            if (candidates.Count > 0)
            {
                SetRole(rooms, candidates[rng.NextInt(0, candidates.Count)], RoomRole.Treasure);
            }
        }
    }

    private static void SetRole(List<RoomRect> rooms, int index, RoomRole role)
    {
        var room = rooms[index];
        room.role = role;
        rooms[index] = room;
    }
}
