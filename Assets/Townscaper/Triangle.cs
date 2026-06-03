using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Triangle
{
    public readonly Vertex_hex a;
    public readonly Vertex_hex b;
    public readonly Vertex_hex c;
    public readonly Vertex_hex[] vertices;
    public readonly Edge ab;
    public readonly Edge bc;
    public readonly Edge ac;

    public readonly Edge[] edges;
    public Triangle(Vertex_hex a,Vertex_hex b, Vertex_hex c,List<Edge> edges,List<Triangle> triangles)
    {
        this.a = a;
        this.b = b;
        this.c = c;
        //创建边框
        ab = Edge.FindEdge(a, b, edges);
        bc = Edge.FindEdge(b, c, edges);
        ac = Edge.FindEdge(a, c, edges);
        vertices = new Vertex_hex[] { a, b, c };
        if (ab == null)
        {
            ab = new Edge(a, b, edges);
        }

        if (bc == null)
        {
            bc = new Edge(b, c, edges);
        }

        if (ac == null)
        {
            ac = new Edge(a, c, edges);
        }

        this.edges = new Edge[] { ab, bc, ac };
        triangles.Add(this);
    }

    public static void Triangles_Ring(int radius,List<Vertex_hex> vertices,List<Edge> edges,List<Triangle> triangles)
    {
        List<Vertex_hex> inner = Vertex_hex.GrabRing(radius - 1, vertices);
        List<Vertex_hex> outer = Vertex_hex.GrabRing(radius, vertices);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < radius; j++)
            {
                //创建两个顶点在外边 一个顶点在内圈的三角形
                Vertex_hex a = outer[i * radius + j];
                Vertex_hex b = outer[(i * radius + j + 1) % outer.Count];
                Vertex_hex c = inner[(i * (radius - 1) + j) % inner.Count];
                new Triangle(a, b, c,edges,triangles);
                //创建一个顶点在外圈，两个顶点在内圈的三角形
                if (j>0)
                {
                    Vertex_hex d = inner[i * (radius - 1) + j - 1];
                    new Triangle(a, c, d,edges,triangles);
                }
            }
        }
    }

    public static void Triangles_Hex(List<Vertex_hex> vertices,List<Edge> edges,List<Triangle>triangles)
    {
        for (int i = 1; i < Grid.radius; i++)
        {
            Triangles_Ring(i,vertices,edges,triangles);
        }
    }
    
    //相邻三角形
    public bool isNeighbor(Triangle target)
    {
        HashSet<Edge> intersection = new HashSet<Edge>(edges);
        intersection.IntersectWith(target.edges);
        return intersection.Count == 1;
    }

    public List<Triangle> FindAllNeighborTriangles(List<Triangle> triangles)
    {
        List<Triangle> result = new List<Triangle>();
        foreach (var triangle in triangles)
        {
            if (this.isNeighbor(triangle))
            {
                result.Add(triangle);
            }
        }
        return result;
    }

    public Edge NeighborEdge(Triangle neighbor)
    {
        HashSet<Edge> intersection = new HashSet<Edge>(edges);
        intersection.IntersectWith(neighbor.edges);
        return intersection.Single();
    }

    public Vertex_hex IsolatedVertex_Self(Triangle neighbor)
    {
        HashSet<Vertex_hex> exception = new HashSet<Vertex_hex>(vertices);
        exception.ExceptWith(NeighborEdge(neighbor).hexes);
        return exception.Single();
    }
    
    public Vertex_hex IsolatedVertex_Neighbor(Triangle neighbor)
    {
        HashSet<Vertex_hex> exception = new HashSet<Vertex_hex>(neighbor.vertices);
        exception.ExceptWith(NeighborEdge(neighbor).hexes);
        return exception.Single();
    }
    public void MergeNeighborTriangles(Triangle neighbor,List<Edge> edges,List<Triangle> triangles,List<Quad> quads)
    {
        Vertex_hex a = IsolatedVertex_Self(neighbor);
        Vertex_hex b = vertices[(Array.IndexOf(vertices, a) + 1) % 3];
        Vertex_hex c = IsolatedVertex_Neighbor(neighbor);
        Vertex_hex d = neighbor.vertices[(Array.IndexOf(neighbor.vertices, c) + 1) % 3];
        Quad quad = new Quad(a, b, c, d, edges,quads);
        edges.Remove(NeighborEdge(neighbor));
        triangles.Remove(this);
        triangles.Remove(neighbor);
    }

    public static bool HasNeighborTriangles(List<Triangle> triangles)
    {
        foreach (var a in triangles)
        {
            foreach (var b in triangles)
            {
                if (a.isNeighbor(b))
                {
                    return true;
                }
            }
        }
        return false;
    }
    
    public static void RandomlyMergeTriangles(List<Edge> edges,List<Triangle> triangles,List<Quad> quads)
    {
        int randomIndex = UnityEngine.Random.Range(0, triangles.Count);
        List<Triangle> neighbors = triangles[randomIndex].FindAllNeighborTriangles(triangles);
        if (neighbors.Count!=0)
        {
            int randomNeighborIndex = UnityEngine.Random.Range(0, neighbors.Count);
            triangles[randomIndex].MergeNeighborTriangles(neighbors[randomNeighborIndex],edges,triangles,quads);
        }
    }
}
