using System.Collections.Generic;
using UnityEngine;

// Camino en L entre dos salas: From -> Mid (codo) -> To, en coordenadas de celda.
public struct CorridorPath
{
    public Vector2 From;
    public Vector2 Mid;
    public Vector2 To;
}

public static class CorridorBuilder
{
    // Conecta todas las salas con el árbol de expansión mínima (Kruskal)
    // y añade conexiones extra para crear rutas alternativas.
    public static List<CorridorPath> BuildCorridors(List<RoomRect> rooms, DeterministicRng rng, int extraLinks)
    {
        var paths = new List<CorridorPath>();
        int n = rooms.Count;
        if (n < 2) return paths;

        var edges = new List<Edge>();
        for (int a = 0; a < n; a++)
        {
            for (int b = a + 1; b < n; b++)
            {
                edges.Add(new Edge
                {
                    a = a,
                    b = b,
                    dist = Vector2.Distance(rooms[a].CenterCell, rooms[b].CenterCell)
                });
            }
        }
        edges.Sort((x, y) => x.dist.CompareTo(y.dist));

        var treeEdges = new List<Edge>();
        var parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;

        foreach (var edge in edges)
        {
            int ra = Find(parent, edge.a);
            int rb = Find(parent, edge.b);
            if (ra == rb) continue;

            parent[ra] = rb;
            treeEdges.Add(edge);
            if (treeEdges.Count >= n - 1) break;
        }

        // Extras: los enlaces más cortos que no estén ya en el árbol (crean bucles).
        int added = 0;
        foreach (var edge in edges)
        {
            if (added >= extraLinks) break;
            if (!treeEdges.Contains(edge))
            {
                treeEdges.Add(edge);
                added++;
            }
        }

        foreach (var edge in treeEdges)
        {
            Vector2 from = rooms[edge.a].CenterCell;
            Vector2 to = rooms[edge.b].CenterCell;

            paths.Add(new CorridorPath
            {
                From = from,
                Mid = new Vector2(to.x, from.y),
                To = to
            });
        }
        return paths;
    }

    private struct Edge
    {
        public int a;
        public int b;
        public float dist;
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }
        return i;
    }
}
