using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grid
{
    public static int radius;
    public static float cellSize;
    public readonly List<Vertex_hex> hexes = new List<Vertex_hex>();
    public readonly List<Vertex_mid> mids = new List<Vertex_mid>();
    public readonly List<Vertex_center> centers = new List<Vertex_center>();
    
    public readonly List<Edge> edges = new List<Edge>();
    public readonly List<Triangle> triangles = new List<Triangle>();
    public readonly List<Quad> quads = new List<Quad>();

    public readonly List<SubQuad> subQuads = new List<SubQuad>();
    
    public Grid(int radius,float cellSize)
    {
        Grid.radius = radius;
        Grid.cellSize = cellSize;
        //这里是将空数组传入 在里面增加  这里主要用于计算顶点  这里很特殊的 是  这个生成的hex grid 是一圈圈 圈层推出来的 不是先定义
        //六边形中心点 然后再构建六边形的顶点边，这个做法是先定义hex grid 的中心点，然后按radiu 一圈圈的推出来
        Vertex_hex.Hex(hexes);
        //这里也是一圈圈推出来的六边形三角面
        Triangle.Triangles_Hex(hexes,mids,centers,edges,triangles);//这里是将空数组传入 在里面增加  这里主要用于计算三角面 和 所有的边框
        while (Triangle.HasNeighborTriangles(triangles))//循环判断是否有相邻的三角形
        {
            Triangle.RandomlyMergeTriangles(mids,centers,edges,triangles,quads);//随机合并相邻的三角形
        }

        //三角形细分
        foreach (var triangle in triangles)
        {
            triangle.Subdivide(subQuads);
        }
        //四边形细分
        foreach (var quad in quads)
        {
            quad.Subdivide(subQuads);
        }
    }
}
