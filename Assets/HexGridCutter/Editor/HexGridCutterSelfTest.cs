using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexGridCutter.Editor
{
    public static class HexGridCutterSelfTest
    {
        [Serializable] private sealed class Report { public string unityVersion; public int passed; public List<string> tests=new List<string>(); public string error; }
        private static Report report;
        private static void Assert(bool value,string message) { if(!value) throw new Exception(message); }
        private static void Near(double actual,double expected,double tolerance,string label)
        { Assert(Math.Abs(actual-expected)<=tolerance,label+": expected "+expected+", got "+actual); }
        private static void Test(string name,Action test) { test(); report.passed++; report.tests.Add(name); Debug.Log("PASS: "+name); }
        private static double Volume(List<HexGeometry.Triangle> triangles)
        { double volume=0; foreach(var t in triangles) volume+=Vector3.Dot(t.A.P,Vector3.Cross(t.B.P,t.C.P))/6d; return volume; }
        private static double Area(List<HexGeometry.Triangle> triangles,int submesh)
        { double area=0; foreach(var t in triangles) if(t.Submesh==submesh) area+=Vector3.Cross(t.B.P-t.A.P,t.C.P-t.A.P).magnitude/2d; return area; }
        private static HexGeometry.Vertex V(Vector3 p)
        { return new HexGeometry.Vertex { P=p,N=Vector3.up,T=new Vector4(1,0,0,1),C=Color.white,U0=new Vector4(p.x,p.z,0,0),U1=new Vector4(p.y,p.x,0,0) }; }
        private static void Face(List<HexGeometry.Triangle> list,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward,int submesh=0)
        {
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0) { Vector3 swap=b; b=d; d=swap; }
            var va=V(a);var vb=V(b);var vc=V(c);var vd=V(d); va.N=vb.N=vc.N=vd.N=outward;
            list.Add(new HexGeometry.Triangle(va,vb,vc,submesh)); list.Add(new HexGeometry.Triangle(va,vc,vd,submesh));
        }
        private static HexGeometry.Source Box(Vector3 min,Vector3 max)
        {
            var s=new HexGeometry.Source { Name="TestBox",Materials=new Material[1],Triangles=new List<HexGeometry.Triangle>(),Bounds=new Bounds((min+max)/2,max-min) };
            s.UVDimensions[0]=s.UVDimensions[1]=2;
            Vector3 a=new Vector3(min.x,min.y,min.z),b=new Vector3(max.x,min.y,min.z),c=new Vector3(max.x,max.y,min.z),d=new Vector3(min.x,max.y,min.z);
            Vector3 e=new Vector3(min.x,min.y,max.z),f=new Vector3(max.x,min.y,max.z),g=new Vector3(max.x,max.y,max.z),h=new Vector3(min.x,max.y,max.z);
            Face(s.Triangles,a,b,c,d,Vector3.back);Face(s.Triangles,e,f,g,h,Vector3.forward);
            Face(s.Triangles,a,e,h,d,Vector3.left);Face(s.Triangles,b,f,g,c,Vector3.right);
            Face(s.Triangles,a,b,f,e,Vector3.down);Face(s.Triangles,d,c,g,h,Vector3.up);return s;
        }
        private static HexGeometry.Source Tube()
        {
            var s=new HexGeometry.Source { Name="HollowSquareTube",Materials=new Material[1],Triangles=new List<HexGeometry.Triangle>(),Bounds=new Bounds(Vector3.zero,Vector3.one*4) };
            Vector2[] outer={new Vector2(-2,-2),new Vector2(2,-2),new Vector2(2,2),new Vector2(-2,2)};
            Vector2[] inner={new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)};
            Func<float,Vector2,Vector3> p=(x,yz)=>new Vector3(x,yz.x,yz.y);
            for(int j=0;j<4;j++)
            {
                int k=(j+1)%4; Vector2 mid=(outer[j]+outer[k])*0.5f;Vector3 n=new Vector3(0,mid.x,mid.y).normalized;
                Face(s.Triangles,p(-2,outer[j]),p(2,outer[j]),p(2,outer[k]),p(-2,outer[k]),n);
                Face(s.Triangles,p(-2,inner[j]),p(2,inner[j]),p(2,inner[k]),p(-2,inner[k]),-n);
                Face(s.Triangles,p(-2,outer[j]),p(-2,outer[k]),p(-2,inner[k]),p(-2,inner[j]),Vector3.left);
                Face(s.Triangles,p(2,outer[j]),p(2,outer[k]),p(2,inner[k]),p(2,inner[j]),Vector3.right);
            }
            return s;
        }
        [MenuItem("Tools/Hex Grid Cutter/运行自检")]
        public static void RunMenu()
        {
            try { RunAll(); EditorUtility.DisplayDialog("自检通过",report.passed+" 项测试全部通过。详见 Console。","确定"); }
            catch(Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("自检失败",e.Message,"确定"); }
        }
        // CLI: -batchmode -nographics -executeMethod HexGridCutter.Editor.HexGridCutterSelfTest.RunBatch
        public static void RunBatch()
        {
            int code=0;
            try
            {
                RunAll();
                string package=Arg("-hexPackage");
                if(!string.IsNullOrEmpty(package)) AssetDatabase.ExportPackage("Assets/HexGridCutter",package,ExportPackageOptions.Recurse);
            }
            catch(Exception e) { code=1; if(report==null) report=new Report(); report.error=e.ToString(); Debug.LogException(e); }
            finally
            {
                string path=Arg("-hexReport"); if(!string.IsNullOrEmpty(path)) File.WriteAllText(path,JsonUtility.ToJson(report,true));
                EditorApplication.Exit(code);
            }
        }
        private static string Arg(string name)
        { string[] args=Environment.GetCommandLineArgs(); for(int i=0;i<args.Length-1;i++) if(args[i]==name) return args[i+1]; return null; }
        private static void RunAll()
        {
            report=new Report { unityVersion=Application.unityVersion };
            const float eps=0.00001f;
            Test("Flat-top axial coordinate spacing",()=>{
                Near(new HexGeometry.Cell(1,0).Center(2).x,3,1e-6,"q x");
                Near(new HexGeometry.Cell(0,1).Center(2).z,Math.Sqrt(3)*2,1e-6,"r z");
            });
            Test("Closed box clip: volume, cap area, outward normals",()=>{
                var s=Box(-Vector3.one,Vector3.one); var cut=HexGeometry.Clip(s.Triangles,Vector3.right,0,true,1,eps,1);
                Near(Volume(cut),4,1e-4,"half cube volume"); Near(Area(cut,1),4,1e-4,"cap area");
                foreach(var t in cut) if(t.Submesh==1) Assert(Vector3.Dot(Vector3.Cross(t.B.P-t.A.P,t.C.P-t.A.P),Vector3.right)>0,"inward cap");
            });
            Test("Toggle OFF: surface-only cuts, no cap submesh triangles",()=>{
                var s=Box(-Vector3.one,Vector3.one);var cut=HexGeometry.Cut(s,Vector3.zero,0.7f,false,eps,1);
                Assert(cut.Count>0,"empty result"); foreach(var t in cut) Assert(t.Submesh==0,"unexpected cap");
            });
            Test("Six-sided clipping makes a full hexagonal prism",()=>{
                var s=Box(new Vector3(-3,-1,-3),new Vector3(3,1,3)); var cut=HexGeometry.Cut(s,Vector3.zero,1,true,eps,1);
                Near(Volume(cut),3*Math.Sqrt(3),0.001,"hex prism volume");
                foreach(var t in cut) for(int j=0;j<3;j++) for(int k=0;k<6;k++)
                {
                    float angle=(30+60*k)*Mathf.Deg2Rad; Vector3 n=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    Assert(Vector3.Dot(n,t.At(j).P)<=Mathf.Sqrt(3)*0.5f+eps*4,"outside hex");
                }
            });
            Test("All hex cells conserve source volume including negative q/r",()=>{
                var s=Box(new Vector3(-2,-1,-2),new Vector3(2,1,2)); double total=0;
                foreach(var cell in HexGeometry.Cells(s.Bounds,1,1000)) total+=Volume(HexGeometry.Cut(s,cell.Center(1),1,true,eps,1));
                Near(total,32,0.003,"sum of cell volumes");
            });
            Test("Hollow cross-section preserves inner hole",()=>{
                var s=Tube(); var cut=HexGeometry.Clip(s.Triangles,Vector3.right,0,true,1,eps,1);
                Near(Area(cut,1),12,0.001,"annular cap area"); Near(Volume(cut),24,0.001,"half hollow tube volume");
                foreach(var t in cut) if(t.Submesh==1) { Vector3 center=(t.A.P+t.B.P+t.C.P)/3;
                    Assert(!(Mathf.Abs(center.y)<0.999f&&Mathf.Abs(center.z)<0.999f),"hole filled"); }
            });
            Test("Concave L-shaped cross-section is capped without filling the notch",()=>{
                Vector2[] polygon={new Vector2(0,0),new Vector2(3,0),new Vector2(3,1),new Vector2(1,1),new Vector2(1,3),new Vector2(0,3)};
                var triangles=new List<HexGeometry.Triangle>();
                Func<float,Vector2,Vector3> p=(x,yz)=>new Vector3(x,yz.x,yz.y);
                for(int i=0;i<polygon.Length;i++)
                {
                    int j=(i+1)%polygon.Length;Vector2 delta=polygon[j]-polygon[i];
                    Face(triangles,p(-2,polygon[i]),p(2,polygon[i]),p(2,polygon[j]),p(-2,polygon[j]),new Vector3(0,delta.y,-delta.x).normalized);
                }
                for(int i=1;i<polygon.Length-1;i++)
                {
                    triangles.Add(new HexGeometry.Triangle(V(p(2,polygon[0])),V(p(2,polygon[i])),V(p(2,polygon[i+1])),0));
                    triangles.Add(new HexGeometry.Triangle(V(p(-2,polygon[0])),V(p(-2,polygon[i+1])),V(p(-2,polygon[i])),0));
                }
                var cut=HexGeometry.Clip(triangles,Vector3.right,0,true,1,eps,1);
                Near(Area(cut,1),5,0.001,"concave cap area");Near(Volume(cut),10,0.001,"concave half prism volume");
            });
            Test("Oblique cuts through vertices and edges remain closed",()=>{
                var s=Box(-Vector3.one,Vector3.one);
                foreach(var n in new[]{new Vector3(1,0,1).normalized,new Vector3(1,0,-1).normalized})
                { var cut=HexGeometry.Clip(s.Triangles,n,0,true,1,eps,1);Near(Volume(cut),4,0.001,"diagonal half volume");Near(Area(cut,1),4*Math.Sqrt(2),0.001,"diagonal cap area"); }
            });
            Test("Rotated translated boxes conserve volume across the full grid",()=>{
                var random=new System.Random(20261028);
                for(int i=0;i<10;i++)
                {
                    var s=Box(-Vector3.one,Vector3.one);var rotated=new List<HexGeometry.Triangle>();
                    Quaternion rotation=Quaternion.Euler((float)random.NextDouble()*70,(float)random.NextDouble()*90,(float)random.NextDouble()*40);
                    Vector3 offset=new Vector3((float)random.NextDouble()*2-1,0,(float)random.NextDouble()*2-1);
                    bool first=true;
                    foreach(var t in s.Triangles)
                    {
                        var a=t.A;var b=t.B;var c=t.C;a.P=rotation*a.P+offset;b.P=rotation*b.P+offset;c.P=rotation*c.P+offset;
                        a.N=rotation*a.N;b.N=rotation*b.N;c.N=rotation*c.N;rotated.Add(new HexGeometry.Triangle(a,b,c,0));
                        if(first) { s.Bounds=new Bounds(a.P,Vector3.zero);first=false; }s.Bounds.Encapsulate(a.P);s.Bounds.Encapsulate(b.P);s.Bounds.Encapsulate(c.P);
                    }
                    s.Triangles=rotated;double total=0;float r=0.6f+(float)random.NextDouble()*0.6f;
                    foreach(var cell in HexGeometry.Cells(s.Bounds,r,1000)) total+=Volume(HexGeometry.Cut(s,cell.Center(r),r,true,eps,1));
                    Near(total,8,0.005,"rotated volume "+i);
                }
            });
            Test("Disconnected closed shells produce separate caps",()=>{
                var a=Box(new Vector3(-1,-3,-1),new Vector3(1,-2,1));var b=Box(new Vector3(-1,2,-1),new Vector3(1,3,1));
                a.Triangles.AddRange(b.Triangles);var cut=HexGeometry.Clip(a.Triangles,Vector3.right,0,true,1,eps,1);
                Near(Area(cut,1),4,0.001,"two caps");Near(Volume(cut),4,0.001,"two half boxes");
            });
            Test("Open surface rejected when cap boundary cannot close",()=>{
                var s=new List<HexGeometry.Triangle> { new HexGeometry.Triangle(V(new Vector3(-1,0,-1)),V(new Vector3(1,0,-1)),V(new Vector3(0,0,1)),0) };
                bool failed=false; try { HexGeometry.Clip(s,Vector3.right,0,true,1,eps,1); } catch(InvalidOperationException) { failed=true; }
                Assert(failed,"invalid cap silently accepted");
            });
            Test("Open plane without caps preserves area and UV interpolation",()=>{
                var s=new HexGeometry.Source { Materials=new Material[1],Triangles=new List<HexGeometry.Triangle>(),Bounds=new Bounds(Vector3.zero,new Vector3(8,0,8)) };
                Face(s.Triangles,new Vector3(-4,0,-4),new Vector3(4,0,-4),new Vector3(4,0,4),new Vector3(-4,0,4),Vector3.up);
                var cut=HexGeometry.Cut(s,Vector3.zero,1,false,eps,1);Near(Area(cut,0),1.5*Math.Sqrt(3),0.001,"hex area");
                foreach(var t in cut) for(int j=0;j<3;j++) { var v=t.At(j);Near(v.U0.x,v.P.x,1e-5,"UV x");Near(v.U0.y,v.P.z,1e-5,"UV z"); }
            });
            Test("Exact face contact does not duplicate zero-volume solid",()=>{
                var s=Box(-Vector3.one,Vector3.one);var cut=HexGeometry.Clip(s.Triangles,Vector3.right,-1,true,1,eps,1);
                Assert(cut.Count==0,"coplanar contact retained");var unchanged=HexGeometry.Clip(s.Triangles,Vector3.right,1,true,1,eps,1);
                Near(Volume(unchanged),8,1e-4,"unchanged cube");
            });
            Test("Mesh output preserves submeshes, UVs, normals and bounds",()=>{
                var s=Box(-Vector3.one,Vector3.one);var cut=HexGeometry.Cut(s,Vector3.zero,0.7f,true,eps,1);
                Mesh mesh=HexGeometry.Build(cut,s,true,"TestMesh");
                try { Assert(mesh.subMeshCount==2,"submesh count");Assert(mesh.uv.Length==mesh.vertexCount&&mesh.uv2.Length==mesh.vertexCount,"UV lengths");
                    Assert(mesh.normals.Length==mesh.vertexCount&&mesh.tangents.Length==mesh.vertexCount,"normal/tangent lengths"); }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            });
            Test("Mirrored nonuniform transform preserves outward winding",()=>{
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
                try {
                    go.transform.position=new Vector3(3,2,-1);go.transform.rotation=Quaternion.Euler(0,23,0);go.transform.localScale=new Vector3(-2,3,4);
                    var s=HexGeometry.Read(go.GetComponent<MeshFilter>(),Matrix4x4.identity);
                    Near(Volume(s.Triangles),24,0.001,"transformed cube volume");
                    foreach(var t in s.Triangles) Assert(Vector3.Dot(Vector3.Cross(t.B.P-t.A.P,t.C.P-t.A.P),t.A.N)>0,"normal/winding mismatch");
                } finally { UnityEngine.Object.DestroyImmediate(go); }
            });
            Test("UInt32 index buffer for large output meshes",()=>{
                var s=Box(-Vector3.one,Vector3.one);var list=new List<HexGeometry.Triangle>();
                for(int i=0;i<22000;i++) list.Add(s.Triangles[0]); Mesh mesh=HexGeometry.Build(list,s,false,"LargeTest");
                try { Assert(mesh.vertexCount==66000&&mesh.indexFormat==IndexFormat.UInt32,"wrong index format"); }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            });
            Test("Prefab/mesh assets persist, identity pivot, metadata and safe repeated export",Integration);
            Test("Invalid output paths and excessive grid counts are rejected",()=>{
                foreach(string path in new[]{"../bad","Assets/../bad","C:/bad","Assets/Bad:Name"})
                { bool failed=false;try { HexExporter.ValidateOutput(path); } catch(ArgumentException) { failed=true; } Assert(failed,"unsafe path accepted"); }
                bool limited=false;try { HexGeometry.Cells(new Bounds(Vector3.zero,Vector3.one*100),0.01f,100); } catch(InvalidOperationException) { limited=true; }Assert(limited,"cell limit ignored");
            });
            Debug.Log("HexGridCutter: "+report.passed+" tests passed on Unity "+Application.unityVersion);
        }
        private static void Integration()
        {
            string root=AssetDatabase.GenerateUniqueAssetPath("Assets/HexGridCutter_SelfTestOutput");
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var s=HexGeometry.Read(primitive.GetComponent<MeshFilter>(),Matrix4x4.identity);
                var cells=HexGeometry.Cells(s.Bounds,0.7f,100);
                var options=new HexExporter.Options { Name="Integration",Output=root,Radius=0.7f,Cap=true,Origin=new Vector3(5,2,7),Yaw=33 };
                string folder=HexExporter.Export(new List<HexGeometry.Source>{s},cells,options,false);
                string[] guids=AssetDatabase.FindAssets("t:Prefab",new[]{folder});Assert(guids.Length>0,"no prefabs");
                foreach(string guid in guids)
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    Assert(prefab.transform.position==Vector3.zero&&prefab.transform.rotation==Quaternion.identity,"prefab pivot not identity");
                    Assert(prefab.GetComponent<HexCellData>()!=null,"metadata absent");
                    foreach(var mf in prefab.GetComponentsInChildren<MeshFilter>()) Assert(AssetDatabase.Contains(mf.sharedMesh),"transient mesh reference");
                }
                string second=HexExporter.Export(new List<HexGeometry.Source>{s},cells,options,false);Assert(second!=folder,"overwrote previous run");
                Assert(AssetDatabase.FindAssets("t:Prefab",new[]{folder}).Length==guids.Length,"previous batch changed");
                var broken=new HexGeometry.Source {Name="Open",Materials=new Material[1],Bounds=new Bounds(Vector3.zero,new Vector3(6,0,6)),Triangles=new List<HexGeometry.Triangle>()};
                Face(broken.Triangles,new Vector3(-3,0,-3),new Vector3(3,0,-3),new Vector3(3,0,3),new Vector3(-3,0,3),Vector3.up);
                int before=AssetDatabase.GetSubFolders(root).Length;bool failed=false;
                try { HexExporter.Export(new List<HexGeometry.Source>{broken},new List<HexGeometry.Cell>{new HexGeometry.Cell(0,0)},options,false); }
                catch(InvalidOperationException) { failed=true; }
                Assert(failed&&AssetDatabase.GetSubFolders(root).Length==before,"failed export not rolled back");
            }
            finally { UnityEngine.Object.DestroyImmediate(primitive);if(AssetDatabase.IsValidFolder(root))AssetDatabase.DeleteAsset(root); }
        }
    }
}
