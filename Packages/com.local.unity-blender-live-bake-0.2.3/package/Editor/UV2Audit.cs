using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace LiveBakeBridge.Editor {
 public sealed class UV2AuditReport {
  public int triangles,degenerate,outOfRange,nonFinite,overlaps;public bool missing,unsupported;public List<int> badFaces=new List<int>();public string error="";
  public bool Valid=>!missing&&!unsupported&&nonFinite==0&&outOfRange==0&&degenerate==0&&overlaps==0&&string.IsNullOrEmpty(error);
  public string Summary=>!string.IsNullOrEmpty(error)?error:unsupported?"不支持的网格拓扑":missing?"缺少 UV2":$"UV2: 退化 {degenerate}，越界顶点 {outOfRange}，非有限 {nonFinite}，重叠冲突面 {overlaps}";
 }
 public sealed class UV2AuditEntry {public MeshFilter filter;public UV2AuditReport report;}
 public static class UV2Audit {
  struct P {public double x,y;public P(double a,double b){x=a;y=b;}}
  static double Cross(P a,P b,P c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
  static double Area(List<P> poly){if(poly.Count<3)return 0;double sum=0;var origin=poly[0];for(int i=1;i<poly.Count-1;i++)sum+=Cross(origin,poly[i],poly[i+1]);return Math.Abs(sum)*.5;}
  static double Intersect(P[] a,P[] b){var poly=new List<P>(a);var clip=Cross(b[0],b[1],b[2])>0?b:b.Reverse().ToArray();for(int j=0;j<3;j++){var source=poly;poly=new List<P>();if(source.Count==0)break;var p=clip[j];var q=clip[(j+1)%3];var prev=source[source.Count-1];double dp=Cross(p,q,prev);foreach(var cur in source){double dc=Cross(p,q,cur);if((dc>=0)!=(dp>=0)){double t=dp/(dp-dc);poly.Add(new P(prev.x+t*(cur.x-prev.x),prev.y+t*(cur.y-prev.y)));}if(dc>=0)poly.Add(cur);prev=cur;dp=dc;}}return Area(poly);}
  public static List<UV2AuditEntry> Scan(){var reports=new Dictionary<Mesh,UV2AuditReport>();var entries=new List<UV2AuditEntry>();foreach(var mf in UV2Repair.SyncFilters()){if(!reports.TryGetValue(mf.sharedMesh,out var r)){r=Inspect(mf.sharedMesh);reports[mf.sharedMesh]=r;}if(!r.Valid)entries.Add(new UV2AuditEntry{filter=mf,report=r});}return entries;}
  public static UV2AuditReport Inspect(Mesh mesh){var r=new UV2AuditReport();try{
   using(var all=MeshUtility.AcquireReadOnlyMeshData(mesh)){
    var d=all[0];if(!d.HasVertexAttribute(VertexAttribute.TexCoord1)){r.missing=true;return r;}
    using(var uv=new NativeArray<Vector2>(d.vertexCount,Allocator.Temp)){
     d.GetUVs(1,uv);foreach(var v in uv){if(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.x)||float.IsInfinity(v.y))r.nonFinite++;else if(v.x< -1e-6||v.x>1+1e-6||v.y< -1e-6||v.y>1+1e-6)r.outOfRange++;}
     if(r.nonFinite>0||r.outOfRange>0)return r;
     var tris=new List<P[]>();var grid=new Dictionary<int,List<int>>();
     for(int sub=0;sub<d.subMeshCount;sub++){
      var sd=d.GetSubMesh(sub);if(sd.topology!=MeshTopology.Triangles){r.unsupported=true;return r;}
      using(var indices=new NativeArray<int>(sd.indexCount,Allocator.Temp)){
       d.GetIndices(indices,sub,true);
       for(int k=0;k<indices.Length;k+=3){int face=r.triangles++;var a=uv[indices[k]];var b=uv[indices[k+1]];var c=uv[indices[k+2]];var tri=new[]{new P(a.x,a.y),new P(b.x,b.y),new P(c.x,c.y)};
        if(Math.Abs(Cross(tri[0],tri[1],tri[2]))*.5<1e-12){r.degenerate++;r.badFaces.Add(face);continue;}
        // Keep only disjoint triangles in the retained set; conflicting later faces get their own charts.

        int x0=Mathf.Clamp((int)(tri.Min(p=>p.x)*32),0,31),x1=Mathf.Clamp((int)(tri.Max(p=>p.x)*32),0,31),y0=Mathf.Clamp((int)(tri.Min(p=>p.y)*32),0,31),y1=Mathf.Clamp((int)(tri.Max(p=>p.y)*32),0,31);
        var candidates=new HashSet<int>();for(int x=x0;x<=x1;x++)for(int y=y0;y<=y1;y++)if(grid.TryGetValue(x*32+y,out var bucket))foreach(int t in bucket)candidates.Add(t);
        bool overlap=false;foreach(int t in candidates)if(Intersect(tri,tris[t])>1e-10){overlap=true;break;}
        if(overlap){r.overlaps++;r.badFaces.Add(face);continue;}
        int n=tris.Count;tris.Add(tri);for(int x=x0;x<=x1;x++)for(int y=y0;y<=y1;y++){int key=x*32+y;if(!grid.TryGetValue(key,out var bucket)){bucket=new List<int>();grid[key]=bucket;}bucket.Add(n);}
       }
      }
     }
    }
   }
  }catch(Exception e){r.error=e.Message;}return r;}
  // Preserve every vertex stream and triangle. Split only degenerate/overlapping UV face corners,
  // then reserve the rightmost 12% of UV space for isolated triangle charts.
  public static Mesh RepairCharts(Mesh source,UV2AuditReport report){
   var faces=new HashSet<int>(report.badFaces);
   for(int attempt=0;attempt<5;attempt++){
    var union=new UV2AuditReport{degenerate=report.degenerate,overlaps=report.overlaps,badFaces=faces.OrderBy(x=>x).ToList()};
    var copy=RescueDegenerate(source,union);var check=Inspect(copy);
    if(check.Valid)return copy;
    UnityEngine.Object.DestroyImmediate(copy);
    if(check.missing||check.nonFinite>0||check.outOfRange>0||check.unsupported||!string.IsNullOrEmpty(check.error))throw new InvalidOperationException(check.Summary);
    int previous=faces.Count;foreach(int f in check.badFaces)faces.Add(f);
    if(faces.Count==previous)break;
   }
   throw new InvalidOperationException("Unable to obtain strict valid UV2 after isolated-chart repair; original references unchanged.");
  }
  public static Mesh RescueDegenerate(Mesh source,UV2AuditReport report){
   if(report.Valid||report.badFaces.Count==0)return source;
   if(report.missing||report.nonFinite>0||report.outOfRange>0||report.unsupported||!string.IsNullOrEmpty(report.error))throw new InvalidOperationException("Remaining UV2 errors cannot be rescued by isolated charts: "+report.Summary);
   var bad=new HashSet<int>(report.badFaces);var append=new List<int>();var subIndices=new List<int[]>();int face=0;
   for(int sub=0;sub<source.subMeshCount;sub++){var ids=source.GetTriangles(sub);for(int k=0;k<ids.Length;k+=3,face++)if(bad.Contains(face))for(int c=0;c<3;c++){append.Add(ids[k+c]);ids[k+c]=source.vertexCount+append.Count-1;}subIndices.Add(ids);}
   using(var read=MeshUtility.AcquireReadOnlyMeshData(source)){
    var input=read[0];var write=Mesh.AllocateWritableMeshData(1);bool consumed=false;Mesh result=null;
    try{
     int count=source.vertexCount+append.Count;var output=write[0];output.SetVertexBufferParams(count,source.GetVertexAttributes());
     for(int stream=0;stream<source.vertexBufferCount;stream++){
      int stride=source.GetVertexBufferStride(stream);var src=input.GetVertexData<byte>(stream);var dst=output.GetVertexData<byte>(stream);NativeArray<byte>.Copy(src,0,dst,0,src.Length);
      for(int i=0;i<append.Count;i++)NativeArray<byte>.Copy(src,append[i]*stride,dst,(source.vertexCount+i)*stride,stride);
     }
     int total=subIndices.Sum(a=>a.Length);output.SetIndexBufferParams(total,IndexFormat.UInt32);var ind=output.GetIndexData<uint>();int pos=0;foreach(var ids in subIndices)foreach(int index in ids)ind[pos++]=(uint)index;
     output.subMeshCount=subIndices.Count;pos=0;for(int i=0;i<subIndices.Count;i++){output.SetSubMesh(i,new SubMeshDescriptor(pos,subIndices[i].Length,MeshTopology.Triangles),MeshUpdateFlags.DontRecalculateBounds);pos+=subIndices[i].Length;}
     result=new Mesh{name=source.name+"_RescuedUV2"};Mesh.ApplyAndDisposeWritableMeshData(write,result,MeshUpdateFlags.DontRecalculateBounds);consumed=true;result.bounds=source.bounds;
     var uv=result.uv2;for(int i=0;i<source.vertexCount;i++)uv[i]=new Vector2(uv[i].x*.86f,uv[i].y);int cols=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(bad.Count*.10))),rows=(bad.Count+cols-1)/cols;
     for(int i=0;i<bad.Count;i++){
      float width=.10f/cols,height=.98f/rows,x=.89f+(i%cols)*width,y=.01f+(i/cols)*height;
      uv[source.vertexCount+i*3]=new Vector2(x+width*.18f,y+height*.18f);uv[source.vertexCount+i*3+1]=new Vector2(x+width*.82f,y+height*.18f);uv[source.vertexCount+i*3+2]=new Vector2(x+width*.18f,y+height*.82f);
     }
     result.uv2=uv;return result;
    }catch{if(result)UnityEngine.Object.DestroyImmediate(result);throw;}finally{if(!consumed)write.Dispose();}
   }
  }
 }
}


