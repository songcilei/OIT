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
        grid = new Grid(radius,cellSize);
    }

    private void OnDrawGizmos()
    {
        if (grid!=null)
        {
            foreach (Vertex_hex vertex in grid.hexes)
            {
                Gizmos.DrawSphere(vertex.coord.worldPosition,0.3f);
            }
        }
    }
}
