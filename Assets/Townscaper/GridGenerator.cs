using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridGenerator : MonoBehaviour
{
    [SerializeField]
    private int radius;

    [SerializeField] private float cellSize;
    private Grid grid;

    private void Awake()
    {
        //创建Grid
        grid = new Grid(radius,cellSize);
    }

    private void OnDrawGizmos()
    {
        if (grid!=null)
        {
            Gizmos.color = Color.yellow;
            foreach (Vertex_hex vertex in grid.hexes)
            {
                Gizmos.DrawSphere(vertex.coord.worldPosition,0.3f);
            }

            Gizmos.color = Color.yellow;
            foreach (var triangle in grid.triangles)
            {
                Gizmos.DrawLine(triangle.a.coord.worldPosition,triangle.b.coord.worldPosition);
                Gizmos.DrawLine(triangle.b.coord.worldPosition,triangle.c.coord.worldPosition);
                Gizmos.DrawLine(triangle.c.coord.worldPosition,triangle.a.coord.worldPosition);
                Gizmos.DrawSphere((triangle.a.coord.worldPosition + triangle.b.coord.worldPosition+triangle.c.coord.worldPosition)/3,0.05f);
            }

            Gizmos.color = Color.green;
            foreach (Quad quad in grid.quads)//绘制四边形
            {
                Gizmos.DrawLine(quad.a.coord.worldPosition,quad.b.coord.worldPosition);
                Gizmos.DrawLine(quad.b.coord.worldPosition,quad.c.coord.worldPosition);
                Gizmos.DrawLine(quad.c.coord.worldPosition,quad.d.coord.worldPosition);
                Gizmos.DrawLine(quad.d.coord.worldPosition,quad.a.coord.worldPosition);
            }

            Gizmos.color = Color.red;
            foreach (var mid in grid.mids)
            {
                Gizmos.DrawSphere(mid.initialPosition,0.2f);
            }

            Gizmos.color = Color.cyan;
            foreach (var center in grid.centers)//绘制中心点
            {
                Gizmos.DrawSphere(center.initialPosition,0.2f);
            }
            Gizmos.color = Color.white;
            foreach (var subQuad in grid.subQuads)//绘制子四边形
            {
                Gizmos.DrawLine(subQuad.a.initialPosition,subQuad.b.initialPosition);
                Gizmos.DrawLine(subQuad.b.initialPosition,subQuad.c.initialPosition);
                Gizmos.DrawLine(subQuad.c.initialPosition,subQuad.d.initialPosition);
                Gizmos.DrawLine(subQuad.d.initialPosition,subQuad.a.initialPosition);
            }
        }
    }
}
