using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SubQuad
{
    public readonly Vertex_hex a;
    public readonly Vertex_mid b;
    public readonly Vertex_center c;
    public readonly Vertex_mid d;
    
    public SubQuad(Vertex_hex a,Vertex_mid b,Vertex_center c,Vertex_mid d,List<SubQuad> subQuads)
    {
        this.a = a;
        this.b = b;
        this.c = c;
        this.d = d;

        subQuads.Add(this);
    }

    /// <summary>
    /// 旋转原有点  并计算平均值
    /// </summary>
    public void CalculateRelaxoffset()
    {
        Vector3 center = (a.currentPosition + b.currentPosition + c.currentPosition + d.currentPosition)/4;
        Vector3 vector_a = (a.currentPosition
                            + Quaternion.AngleAxis(-90,Vector3.up)*(b.currentPosition - center) + center
                            + Quaternion.AngleAxis(-180,Vector3.up)*(c.currentPosition - center) + center
                            + Quaternion.AngleAxis(-270,Vector3.up)*(d.currentPosition - center) + center)/4;
        Vector3 vector_b = Quaternion.AngleAxis(90, Vector3.up) * (vector_a - center) + center;
        Vector3 vector_c = Quaternion.AngleAxis(180, Vector3.up) * (vector_a - center) + center;
        Vector3 vector_d = Quaternion.AngleAxis(270, Vector3.up) * (vector_a - center) + center;

        a.offset += (vector_a - a.currentPosition)*0.1f;
        b.offset += (vector_b - b.currentPosition)*0.1f;
        c.offset += (vector_c - c.currentPosition)*0.1f;
        d.offset += (vector_d - d.currentPosition)*0.1f;
    }

}
