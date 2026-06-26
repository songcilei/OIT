using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CloudPointUnitl
{
    //点云最小二乘法求平面法
//http://www.ilikebigbits.com/2015_03_04_plane_from_points.html
    static public void plane_from_points(Vector3[] points, out Vector3 centroid, out Vector3 dir)
    {
        centroid = Vector3.zero;
        dir = Vector3.forward;
        if (points.Length < 3)
        {
            return; // At least three points required
        }

        Vector3 sum = Vector3.zero;
        foreach (var p in points)
        {
            sum += p;
        }
        centroid = sum / (points.Length);

        // Calc full 3x3 covariance matrix, excluding symmetries:
        float xx = 0.0f, xy = 0.0f, xz = 0.0f;
        float yy = 0.0f, yz = 0.0f, zz = 0.0f;
        foreach (var p in points)
        {
            var r = p - centroid;
            xx += r.x * r.x;
            xy += r.x * r.y;
            xz += r.x * r.z;
            yy += r.y * r.y;
            yz += r.y * r.z;
            zz += r.z * r.z;
        }


        var det_x = yy * zz - yz * yz;
        var det_y = xx * zz - xz * xz;
        var det_z = xx * yy - xy * xy;

        var det_max = Mathf.Max(det_x, det_y, det_z);
        if (det_max <= 0.0)
        {
            return; // The points don't span a plane
        }

        // Pick path with best conditioning:


        if (det_max == det_x)
        {
            dir = new Vector3(
                det_x,
                xz * yz - xy * zz,
                xy * yz - xz * yy);

        }
        else if (det_max == det_y)
        {
            dir = new Vector3(xz * yz - xy * zz,
                det_y,
                xy * xz - yz * xx);

        }

        else
        {
            dir = new Vector3(xy * yz - xz * yy,
                xy * xz - yz * xx,
                det_z);

        }
    }
    
    
    
    //// 暴力最近平面方法
    //
    public static Vector3 GetPlane(int count, Vector3[] colors, Vector3 massCenter)
    {

        Vector3   planNormal;
        var normals = getSphereNormals(32, 16);

         Vector3 nBest = Vector3.forward;
         float minDis = float.MaxValue;
         foreach (var n in normals)
         {
             float allDis = 0;
             for (int i = 0; i < count; i++)
             {
                 Vector3 c = (Vector4)colors[i];
                 //  c = c * 2 - vector3.one - Registered at Namecheap.com;

                 allDis += Mathf.Abs(Vector3.Dot(n, c - massCenter));
             }
             if (minDis > allDis)
             {
                 minDis = allDis;
                 nBest = n;
             }

         }
           planNormal = nBest;
           return planNormal;
    }
    
    
    static Vector3[] getSphereNormals(int col, int row)
    {
        Vector3[] posList = new Vector3[col * row];
        for (int i = 0; i < col; i++)
        {
            float p = (float)i / col * 2 * Mathf.PI;
            for (int j = 0; j < row; j++)
            {
                float q = (float)(j + 0.5f) / row * Mathf.PI - Mathf.PI / 2;

                Vector3 pos = new Vector3(Mathf.Cos(p), 0, Mathf.Sin(p));
                pos *= Mathf.Cos(q);
                pos.y = Mathf.Sin(q);
                posList[j * col + i] = pos.normalized;
            }
        }
        return posList;
    }
}
