using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridGenerator : MonoBehaviour
{
    [SerializeField]
    private int radius;

    [SerializeField] private float cellSize;
    [SerializeField] private int relaxTimes;
    private Grid grid;

    private void Awake()
    {
        //创建Grid
        grid = new Grid(radius,cellSize,relaxTimes);
    }

    private void OnDrawGizmos()
    {
        if (grid!=null)
        {
            Gizmos.color = Color.yellow;
            foreach (Vertex_hex vertex in grid.hexes)
            {
                Gizmos.DrawSphere(vertex.currentPosition,0.3f);
            }

            Gizmos.color = Color.yellow;
            foreach (var triangle in grid.triangles)
            {
                Gizmos.DrawLine(triangle.a.currentPosition,triangle.b.currentPosition);
                Gizmos.DrawLine(triangle.b.currentPosition,triangle.c.currentPosition);
                Gizmos.DrawLine(triangle.c.currentPosition,triangle.a.currentPosition);
                // Gizmos.DrawSphere((triangle.a.coord.worldPosition + triangle.b.coord.worldPosition+triangle.c.coord.worldPosition)/3,0.05f);
            }

            Gizmos.color = Color.green;
            foreach (Quad quad in grid.quads)//绘制四边形
            {
                Gizmos.DrawLine(quad.a.currentPosition,quad.b.currentPosition);
                Gizmos.DrawLine(quad.b.currentPosition,quad.c.currentPosition);
                Gizmos.DrawLine(quad.c.currentPosition,quad.d.currentPosition);
                Gizmos.DrawLine(quad.a.currentPosition,quad.d.currentPosition);
            }

            Gizmos.color = Color.red;
            foreach (var mid in grid.mids)
            {
                Gizmos.DrawSphere(mid.currentPosition,0.2f);
            }

            Gizmos.color = Color.cyan;
            foreach (var center in grid.centers)//绘制中心点
            {
                Gizmos.DrawSphere(center.currentPosition,0.2f);
            }
            Gizmos.color = Color.white;
            foreach (var subQuad in grid.subQuads)//绘制子四边形
            {
                Gizmos.DrawLine(subQuad.a.currentPosition,subQuad.b.currentPosition);
                Gizmos.DrawLine(subQuad.b.currentPosition,subQuad.c.currentPosition);
                Gizmos.DrawLine(subQuad.c.currentPosition,subQuad.d.currentPosition);
                Gizmos.DrawLine(subQuad.d.currentPosition,subQuad.a.currentPosition);
            }
        }
    }
}
