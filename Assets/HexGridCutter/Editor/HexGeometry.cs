// Geometry engine. No scene objects or input meshes are modified.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using HexGridCutter.Internal.LibTessDotNet;
using Mesh = UnityEngine.Mesh;

namespace HexGridCutter.Editor
{
    internal static class HexGeometry
    {
        internal struct Vertex
        {
            public Vector3 P, N;
            public Vector4 T, U0, U1, U2, U3, U4, U5, U6, U7;
            public Color C;
            public Vector4 UV(int channel)
            {
                switch (channel) { case 0: return U0; case 1: return U1; case 2: return U2;
                    case 3: return U3; case 4: return U4; case 5: return U5; case 6: return U6; default: return U7; }
            }
            public void SetUV(int channel, Vector4 value)
            {
                switch (channel) { case 0: U0 = value; break; case 1: U1 = value; break; case 2: U2 = value; break;
                    case 3: U3 = value; break; case 4: U4 = value; break; case 5: U5 = value; break;
                    case 6: U6 = value; break; default: U7 = value; break; }
            }
            public static Vertex Lerp(Vertex a, Vertex b, float t)
            {
                return new Vertex { P = Vector3.LerpUnclamped(a.P,b.P,t), N = Vector3.LerpUnclamped(a.N,b.N,t),
                    T = Vector4.LerpUnclamped(a.T,b.T,t), C = Color.LerpUnclamped(a.C,b.C,t),
                    U0 = Vector4.LerpUnclamped(a.U0,b.U0,t), U1 = Vector4.LerpUnclamped(a.U1,b.U1,t),
                    U2 = Vector4.LerpUnclamped(a.U2,b.U2,t), U3 = Vector4.LerpUnclamped(a.U3,b.U3,t),
                    U4 = Vector4.LerpUnclamped(a.U4,b.U4,t), U5 = Vector4.LerpUnclamped(a.U5,b.U5,t),
                    U6 = Vector4.LerpUnclamped(a.U6,b.U6,t), U7 = Vector4.LerpUnclamped(a.U7,b.U7,t) };
            }
        }
        internal struct Triangle
        {
            public Vertex A, B, C;
            public int Submesh;
            public Triangle(Vertex a, Vertex b, Vertex c, int submesh) { A=a; B=b; C=c; Submesh=submesh; }
            public Vertex At(int i) { return i == 0 ? A : i == 1 ? B : C; }
        }
        internal sealed class Source
        {
            public string Name;
            public List<Triangle> Triangles;
            public Material[] Materials;
            public int[] UVDimensions = new int[8];
            public bool HasColors, HasTangents;
            public Bounds Bounds;
            public MeshRenderer Renderer;
            public int Layer;
            public int CapSubmesh => Materials.Length;
        }
        internal struct Cell : IEquatable<Cell>
        {
            public int Q, R;
            public Cell(int q, int r) { Q=q; R=r; }
            public bool Equals(Cell other) { return Q == other.Q && R == other.R; }
            public override bool Equals(object obj) { return obj is Cell && Equals((Cell)obj); }
            public override int GetHashCode() { unchecked { return Q * 397 ^ R; } }
            public Vector3 Center(float radius) { return new Vector3(1.5f*radius*Q,0f,Mathf.Sqrt(3f)*radius*(R+Q*0.5f)); }
            public override string ToString() { return "q" + Q + "_r" + R; }
        }
        internal static Source Read(MeshFilter filter, Matrix4x4 worldToGrid)
        {
            Mesh mesh = filter.sharedMesh;
            if (!mesh.isReadable) throw new InvalidOperationException(filter.name + ": Mesh 不可读，请在模型 Import Settings 中启用 Read/Write。");
            var renderer = filter.GetComponent<MeshRenderer>();
            if (!renderer) throw new InvalidOperationException(filter.name + ": 缺少 MeshRenderer。");
            var source = new Source { Name=filter.name, Renderer=renderer, Layer=filter.gameObject.layer,
                Triangles=new List<Triangle>(), Materials=new Material[mesh.subMeshCount] };
            var materials=renderer.sharedMaterials;
            for(int s=0;s<source.Materials.Length;s++) source.Materials[s]=materials.Length==0 ? null : materials[Math.Min(s,materials.Length-1)];
            var positions=mesh.vertices; var normals=mesh.normals; var tangents=mesh.tangents; var colors=mesh.colors;
            source.HasTangents=tangents.Length==positions.Length;
            source.HasColors=colors.Length==positions.Length;
            var uvs=new List<Vector4>[8];
            for(int u=0;u<8;u++)
            {
                uvs[u]=new List<Vector4>(); mesh.GetUVs(u,uvs[u]);
                if (uvs[u].Count==positions.Length && positions.Length>0)
                    source.UVDimensions[u]=mesh.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0+u));
            }
            Matrix4x4 transform=worldToGrid*filter.transform.localToWorldMatrix;
            if(Mathf.Abs(transform.determinant)<1e-12f) throw new InvalidOperationException(filter.name+": 缩放为零或变换不可逆。");
            Matrix4x4 normalTransform=transform.inverse.transpose;
            bool mirrored=transform.determinant<0;
            var vertices=new Vertex[positions.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var v=new Vertex { P=transform.MultiplyPoint3x4(positions[i]),
                    N=normals.Length==positions.Length ? normalTransform.MultiplyVector(normals[i]).normalized : Vector3.zero,
                    C=source.HasColors ? colors[i] : Color.white };
                if (!Finite(v.P.x)||!Finite(v.P.y)||!Finite(v.P.z)) throw new InvalidOperationException(filter.name+": 包含无效顶点坐标。");
                if(source.HasTangents)
                {
                    Vector3 t=transform.MultiplyVector(new Vector3(tangents[i].x,tangents[i].y,tangents[i].z));
                    t=(t-v.N*Vector3.Dot(v.N,t)).normalized;
                    v.T=new Vector4(t.x,t.y,t.z,tangents[i].w*(mirrored ? -1f : 1f));
                }
                for(int u=0;u<8;u++) if(source.UVDimensions[u]>0) v.SetUV(u,uvs[u][i]);
                vertices[i]=v;
                if(i==0) source.Bounds=new Bounds(v.P,Vector3.zero); else source.Bounds.Encapsulate(v.P);
            }
            for(int s=0;s<mesh.subMeshCount;s++)
            {
                if(mesh.GetTopology(s)!=MeshTopology.Triangles) throw new InvalidOperationException(filter.name+": 只支持 Triangle 拓扑。");
                int[] indices=mesh.GetTriangles(s);
                for(int i=0;i<indices.Length;i+=3)
                {
                    Vertex a=vertices[indices[i]], b=vertices[indices[i+(mirrored?2:1)]], c=vertices[indices[i+(mirrored?1:2)]];
                    if(normals.Length!=positions.Length) { Vector3 n=Vector3.Cross(b.P-a.P,c.P-a.P).normalized; a.N=b.N=c.N=n; }
                    source.Triangles.Add(new Triangle(a,b,c,s));
                }
            }
            return source;
        }
        internal static bool Finite(float value) { return !float.IsNaN(value)&&!float.IsInfinity(value); }

        internal static List<Cell> Cells(Bounds bounds, float radius, int limit)
        {
            if(!Finite(radius)||radius<=0) throw new ArgumentException("六边形半径必须大于零。");
            double step=1.5*(double)radius, h=Math.Sqrt(3)*radius;
            double q0=Math.Ceiling((bounds.min.x-radius)/step), q1=Math.Floor((bounds.max.x+radius)/step);
            if(q0<int.MinValue+1d||q1>int.MaxValue-1d||q1-q0>limit) throw new InvalidOperationException("网格范围过大，请增大半径或移动网格原点。");
            var result=new List<Cell>();
            for(int q=(int)q0;q<=q1;q++)
            {
                double r0=Math.Ceiling(bounds.min.z/h-q*0.5-0.5), r1=Math.Floor(bounds.max.z/h-q*0.5+0.5);
                if(r0<int.MinValue+1d||r1>int.MaxValue-1d||r1-r0>limit) throw new InvalidOperationException("网格范围过大。");
                for(int r=(int)r0;r<=r1;r++)
                {
                    result.Add(new Cell(q,r));
                    if(result.Count>limit) throw new InvalidOperationException("候选格子超过 "+limit+"，请增大半径或拆分模型。");
                }
            }
            return result;
        }
        internal static bool Overlaps(Bounds b, Vector3 center, float radius)
        {
            float h=0.866025403784f*radius;
            return b.max.x>=center.x-radius && b.min.x<=center.x+radius && b.max.z>=center.z-h && b.min.z<=center.z+h;
        }
        internal static List<Triangle> Cut(Source source, Vector3 center, float radius, bool cap, float eps, float capUVScale)
        {
            var triangles=new List<Triangle>(source.Triangles.Count);
            foreach(var t in source.Triangles)
            {
                var a=t.A; var b=t.B; var c=t.C; a.P-=center; b.P-=center; c.P-=center;
                if(!cap)
                {
                    if(Mathf.Min(a.P.x,Mathf.Min(b.P.x,c.P.x))>radius || Mathf.Max(a.P.x,Mathf.Max(b.P.x,c.P.x))< -radius ||
                       Mathf.Min(a.P.z,Mathf.Min(b.P.z,c.P.z))>radius || Mathf.Max(a.P.z,Mathf.Max(b.P.z,c.P.z))< -radius) continue;
                }
                triangles.Add(new Triangle(a,b,c,t.Submesh));
            }
            float d=radius*Mathf.Sqrt(3f)*0.5f;
            for(int side=0;side<6 && triangles.Count>0;side++)
            {
                float angle=(30f+60f*side)*Mathf.Deg2Rad;
                Vector3 n=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
                triangles=Clip(triangles,n,d,cap,source.CapSubmesh,eps,capUVScale);
            }
            return triangles;
        }
        internal static List<Triangle> Clip(List<Triangle> input, Vector3 normal, float offset, bool cap, int capSubmesh, float eps, float capUVScale)
        {
            bool outside=false, inside=false;
            foreach(var t in input) for(int j=0;j<3;j++)
            {
                float d=Vector3.Dot(normal,t.At(j).P)-offset;
                outside |= d>eps; inside |= d< -eps;
            }
            if(!outside) return input;
            if(!inside) return new List<Triangle>(); // contact only: no volume/area on the retained side
            var output=new List<Triangle>(input.Count);
            var polygon=new Vertex[6];
            foreach(var triangle in input)
            {
                int count=0;
                Vertex previous=triangle.C;
                float prevD=Vector3.Dot(normal,previous.P)-offset;
                if(Mathf.Abs(prevD)<=eps) { previous.P-=normal*prevD; prevD=0f; }
                for(int j=0;j<3;j++)
                {
                    Vertex current=triangle.At(j);
                    float curD=Vector3.Dot(normal,current.P)-offset;
                    if(Mathf.Abs(curD)<=eps) { current.P-=normal*curD; curD=0f; }
                    if((prevD<=0f)!=(curD<=0f))
                    {
                        Vertex intersection=Vertex.Lerp(previous,current,prevD/(prevD-curD));
                        intersection.P-=normal*(Vector3.Dot(normal,intersection.P)-offset);
                        polygon[count++]=intersection;
                    }
                    if(curD<=0f) polygon[count++]=current;
                    previous=current; prevD=curD;
                }
                for(int j=1;j<count-1;j++) Add(output,polygon[0],polygon[j],polygon[j+1],triangle.Submesh,eps);
            }
            if(cap && output.Count>0) Cap(output,normal,offset,capSubmesh,eps,capUVScale);
            return output;
        }
        private static void Add(List<Triangle> output, Vertex a, Vertex b, Vertex c, int submesh, float eps)
        {
            // Cross magnitude is twice the triangle area; reject only numerically negligible faces.
            if(Vector3.Cross(b.P-a.P,c.P-a.P).sqrMagnitude>eps*eps*eps*eps)
                output.Add(new Triangle(a,b,c,submesh));
        }
        private struct Key : IEquatable<Key>
        {
            public long X,Y;
            public Key(long x,long y) { X=x; Y=y; }
            public bool Equals(Key k) { return X==k.X&&Y==k.Y; }
            public override bool Equals(object o) { return o is Key&&Equals((Key)o); }
            public override int GetHashCode() { unchecked { return X.GetHashCode()*397 ^ Y.GetHashCode(); } }
        }
        private struct Edge : IEquatable<Edge>
        {
            public int A,B;
            public Edge(int a,int b) { A=Math.Min(a,b); B=Math.Max(a,b); }
            public bool Equals(Edge e) { return A==e.A&&B==e.B; }
            public override bool Equals(object o) { return o is Edge&&Equals((Edge)o); }
            public override int GetHashCode() { unchecked { return A*397^B; } }
        }
        private sealed class Weld
        {
            public readonly List<Vector2> Points=new List<Vector2>();
            private readonly Dictionary<Key,List<int>> buckets=new Dictionary<Key,List<int>>();
            private readonly float tolerance;
            public Weld(float tolerance) { this.tolerance=tolerance; }
            public int Index(Vector2 p)
            {
                long x=(long)Math.Floor(p.x/tolerance), y=(long)Math.Floor(p.y/tolerance);
                for(int dx=-1;dx<=1;dx++) for(int dy=-1;dy<=1;dy++)
                {
                    List<int> candidates;
                    if(buckets.TryGetValue(new Key(x+dx,y+dy),out candidates))
                        foreach(int i in candidates) if((Points[i]-p).sqrMagnitude<=tolerance*tolerance) return i;
                }
                int index=Points.Count; Points.Add(p); var key=new Key(x,y); List<int> list;
                if(!buckets.TryGetValue(key,out list)) { list=new List<int>(); buckets.Add(key,list); }
                list.Add(index); return index;
            }
        }
        private static void Cap(List<Triangle> triangles, Vector3 normal, float offset, int submesh, float eps, float uvScale)
        {
            Vector3 u=Vector3.Cross(Vector3.up,normal).normalized, v=Vector3.up;
            // u cross v = outward normal; keep the original shell's UV/normals separate from cap vertices.
            var weld=new Weld(eps*2f);
            var edges=new Dictionary<Edge,int>();
            foreach(var t in triangles) for(int j=0;j<3;j++)
            {
                Vector3 a=t.At(j).P, b=t.At((j+1)%3).P;
                if(Mathf.Abs(Vector3.Dot(normal,a)-offset)>eps*2f||Mathf.Abs(Vector3.Dot(normal,b)-offset)>eps*2f) continue;
                int ia=weld.Index(new Vector2(Vector3.Dot(a,u),Vector3.Dot(a,v)));
                int ib=weld.Index(new Vector2(Vector3.Dot(b,u),Vector3.Dot(b,v)));
                if(ia==ib) continue;
                var edge=new Edge(ia,ib); int count; edges.TryGetValue(edge,out count); edges[edge]=count+1;
            }
            var adjacency=new Dictionary<int,List<int>>();
            var remaining=new HashSet<Edge>();
            foreach(var pair in edges)
            {
                if(pair.Value>2) throw new InvalidOperationException("封口失败：切面出现非流形/重复边。请修复网格，或关闭封口。");
                if(pair.Value!=1) continue;
                remaining.Add(pair.Key);
                Connect(adjacency,pair.Key.A,pair.Key.B); Connect(adjacency,pair.Key.B,pair.Key.A);
            }
            if(remaining.Count==0) return;
            foreach(var pair in adjacency)
                if(pair.Value.Count!=2) throw new InvalidOperationException("封口失败：切面轮廓不闭合或相互接触。每个 MeshFilter 的网格必须独立闭合；请修复模型或关闭封口。");
            var tess=new Tess();
            while(remaining.Count>0)
            {
                Edge first=default(Edge); foreach(var e in remaining) { first=e; break; }
                int start=first.A, previous=-1, current=start;
                var contour=new List<ContourVertex>();
                do
                {
                    Vector2 p=weld.Points[current];
                    contour.Add(new ContourVertex { Position=new Vec3(p.x,p.y,0f) });
                    var neighbors=adjacency[current];
                    int next=neighbors[0]==previous ? neighbors[1] : neighbors[0];
                    if(!remaining.Remove(new Edge(current,next))) throw new InvalidOperationException("封口轮廓遍历失败：存在退化或重复边。");
                    previous=current; current=next;
                } while(current!=start);
                if(contour.Count>=3) tess.AddContour(contour);
            }
            // EvenOdd preserves nested contours, e.g. the bore of a hollow pipe.
            tess.Tessellate(WindingRule.EvenOdd,ElementType.Polygons,3,normal:new Vec3(0,0,1));
            for(int i=0;i<tess.ElementCount;i++)
            {
                int a=tess.Elements[i*3], b=tess.Elements[i*3+1], c=tess.Elements[i*3+2];
                if(a<0||b<0||c<0) continue;
                Vertex va=CapVertex(tess.Vertices[a].Position,u,v,normal,offset,uvScale);
                Vertex vb=CapVertex(tess.Vertices[b].Position,u,v,normal,offset,uvScale);
                Vertex vc=CapVertex(tess.Vertices[c].Position,u,v,normal,offset,uvScale);
                if(Vector3.Dot(Vector3.Cross(vb.P-va.P,vc.P-va.P),normal)<0) { var swap=vb; vb=vc; vc=swap; }
                Add(triangles,va,vb,vc,submesh,eps);
            }
        }
        private static void Connect(Dictionary<int,List<int>> graph,int from,int to)
        {
            List<int> list; if(!graph.TryGetValue(from,out list)) { list=new List<int>(); graph.Add(from,list); } list.Add(to);
        }
        private static Vertex CapVertex(Vec3 p,Vector3 u,Vector3 v,Vector3 n,float d,float scale)
        {
            return new Vertex { P=u*p.X+v*p.Y+n*d, N=n, T=new Vector4(u.x,u.y,u.z,1f), C=Color.white,
                U0=new Vector4(p.X*scale,p.Y*scale,0f,0f) };
        }
        internal static Mesh Build(List<Triangle> triangles, Source source, bool cap, string name)
        {
            int count=checked(triangles.Count*3);
            var mesh=new Mesh { name=name, indexFormat=count>65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            var positions=new List<Vector3>(count); var normals=new List<Vector3>(count);
            var tangents=new List<Vector4>(count); var colors=new List<Color>(count);
            var uvs=new List<Vector4>[8];
            for(int u=0;u<8;u++) uvs[u]=new List<Vector4>(source.UVDimensions[u]>0||u==0 ? count:0);
            int submeshCount=source.Materials.Length+(cap?1:0);
            var indices=new List<int>[submeshCount]; for(int s=0;s<submeshCount;s++) indices[s]=new List<int>();
            foreach(var triangle in triangles) for(int j=0;j<3;j++)
            {
                Vertex vertex=triangle.At(j); indices[triangle.Submesh].Add(positions.Count); positions.Add(vertex.P);
                Vector3 n=vertex.N.normalized;
                if(n.sqrMagnitude<0.5f) n=Vector3.Cross(triangle.B.P-triangle.A.P,triangle.C.P-triangle.A.P).normalized;
                normals.Add(n);
                Vector3 t=new Vector3(vertex.T.x,vertex.T.y,vertex.T.z); t=(t-n*Vector3.Dot(n,t)).normalized;
                tangents.Add(new Vector4(t.x,t.y,t.z,vertex.T.w<0?-1f:1f)); colors.Add(vertex.C);
                for(int u=0;u<8;u++) if(source.UVDimensions[u]>0||u==0) uvs[u].Add(vertex.UV(u));
            }
            mesh.SetVertices(positions); mesh.SetNormals(normals);
            for(int u=0;u<8;u++) if(uvs[u].Count>0)
            {
                int dim=Math.Max(u==0?2:0,source.UVDimensions[u]);
                if(dim<=2) { var list=new List<Vector2>(count); foreach(var x in uvs[u]) list.Add(new Vector2(x.x,x.y)); mesh.SetUVs(u,list); }
                else if(dim==3) { var list=new List<Vector3>(count); foreach(var x in uvs[u]) list.Add(new Vector3(x.x,x.y,x.z)); mesh.SetUVs(u,list); }
                else mesh.SetUVs(u,uvs[u]);
            }
            if(source.HasColors) mesh.SetColors(colors);
            mesh.subMeshCount=submeshCount;
            for(int s=0;s<submeshCount;s++) mesh.SetTriangles(indices[s],s,false);
            if(source.HasTangents) mesh.SetTangents(tangents); else mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
