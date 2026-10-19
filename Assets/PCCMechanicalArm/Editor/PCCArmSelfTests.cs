#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using PCCMechanicalArm;
using PCCMechanicalArm.Editor;

public static class PCCArmSelfTests
{
    private static int assertions;
    private static readonly System.Text.StringBuilder log = new System.Text.StringBuilder();
    private static void Check(bool condition, string text)
    {
        assertions++;
        if (!condition) throw new Exception("FAIL: " + text);
    }
    private static void Note(string text) { log.AppendLine(text); Debug.Log(text); }
    private static PCCSection[] Sections()
    {
        return new[] {
            new PCCSection(.55f, 1.65f, 1.05f, new Vector2(18,0)),
            new PCCSection(.55f, 1.65f, 1.05f, new Vector2(-32,8)),
            new PCCSection(.55f, 1.65f, 1.05f, new Vector2(18,-8)) };
    }
    private static void Converge(PCCSolver s, Vector3 target, int frames = 180, bool extension = true)
    {
        for (int f = 0; f < frames; f++)
        {
            s.Solve(target, 8, .005f, .035f, .18f, extension);
            s.Advance(1f/60f, 220f * Mathf.Deg2Rad, 1.8f);
        }
    }
    [MenuItem("Tools/PCC Mechanical Arm/Run Self Tests")]
    public static void Run()
    {
        assertions = 0; log.Clear();
        try
        {
            Vector3 p; Quaternion r;
            PCCKinematics.EvaluateArc(Vector2.zero, 2f, 1f, out p, out r);
            Check(Vector3.Distance(p, new Vector3(0,0,2)) < 1e-6f, "Straight arc position");
            Check(Quaternion.Angle(r, Quaternion.identity) < .001f, "Straight arc rotation");
            PCCKinematics.EvaluateArc(new Vector2(Mathf.PI / 2f, 0), 2f, 1f, out p, out r);
            float radius = 4f / Mathf.PI;
            Check(Vector3.Distance(p, new Vector3(radius,0,radius)) < 1e-5f, "Quarter circle analytic position");
            Check(Vector3.Distance(r * Vector3.forward, Vector3.right) < 1e-5f, "Quarter circle tangent");
            PCCKinematics.EvaluateArc(new Vector2(1e-7f,-1e-7f), 2f, 1f, out p, out r);
            Check(!float.IsNaN(p.x) && Mathf.Abs(p.z - 2f) < 1e-6f, "Zero curvature numeric limit");
            Note("PASS analytic straight/quarter-circle/small-curvature tests");

            var sections = Sections();
            PCCSolver solver = new PCCSolver(sections);
            for (int s = 0; s < 2; s++)
            {
                Vector3 end, start; Quaternion re, rs;
                solver.EvaluatePose(s, 1f, out end, out re);
                solver.EvaluatePose(s+1, 0f, out start, out rs);
                Check(Vector3.Distance(end,start) < 1e-6f, "Section position continuity");
                Check(Quaternion.Angle(re,rs) < .06f, "Section frame continuity");
            }
            Vector3[] targets = {
                new Vector3(1.2f,.7f,2.8f), new Vector3(-1.5f,.4f,2.5f),
                new Vector3(.2f,1.7f,2.5f), new Vector3(.2f,.2f,4.4f),
                new Vector3(.1f,.1f,1.1f), new Vector3(-.8f,-.5f,2.7f),
                new Vector3(0,0,4.7f) };
            foreach (Vector3 t in targets)
            {
                Converge(solver,t);
                float error = Vector3.Distance(solver.EvaluateTip(),t);
                Note("IK target " + t.ToString("F3") + " error=" + error.ToString("F6"));
                Check(error < .025f, "IK convergence " + t);
            }
            solver = new PCCSolver(Sections());
            float peak = 0f, sum = 0f;
            for(int f=0;f<1200;f++)
            {
                float phase = f / 60f * .65f;
                Vector3 t = new Vector3(Mathf.Sin(phase)*1.15f,.55f+Mathf.Sin(phase*.7f)*.65f,
                    2.75f+Mathf.Sin(phase*.53f)*.95f);
                solver.Solve(t,8,.01f,.035f,.18f,true);
                solver.Advance(1f/60f,220*Mathf.Deg2Rad,1.8f);
                if(f>120) { float er=Vector3.Distance(solver.EvaluateTip(),t); peak=Mathf.Max(peak,er);sum+=er; }
                for(int i=0;i<3;i++)
                {
                    Vector2 bend;float length; solver.GetSectionPose(i,out bend,out length);
                    Check(length>=.55f-1e-5f && length<=1.65f+1e-5f,"Length limits");
                    Check(bend.magnitude<=140*Mathf.Deg2Rad+1e-5f,"Bend limits");
                }
            }
            Note("Moving target (20s simulated): peak error="+peak.ToString("F5")+", mean="+(sum/1079).ToString("F5"));
            Check(peak<.12f,"Moving target tracking");
            solver = new PCCSolver(Sections());
            Converge(solver,new Vector3(20,10,30));
            Check(solver.EvaluateTip().magnitude <= solver.MaximumLength+1e-4f,"Unreachable reach bounded");
            Check(!float.IsNaN(solver.EvaluateTip().x),"Unreachable stable");
            Note("PASS unreachable target remains finite and bounded");

            solver = new PCCSolver(Sections());
            Converge(solver,new Vector3(1,.4f,2.6f),180,false);
            for(int i=0;i<3;i++)
            {
                Vector2 bend;float length;solver.GetSectionPose(i,out bend,out length);
                Check(Mathf.Abs(length-1.05f)<1e-5f,"Fixed extension when disabled");
            }
            Note("PASS extension lock");
            for(int i=0;i<3;i++) solver.SetSectionPose(i,Vector2.zero,.55f);
            Converge(solver,new Vector3(0,0,1.2f));
            float escapeError=Vector3.Distance(solver.EvaluateTip(),new Vector3(0,0,1.2f));
            Note("Straight/minimum-length singularity recovery error="+escapeError);
            Check(escapeError<.03f,"Singularity escape");

            // Geometry tests use actual Unity Mesh API, not a reimplementation.
            int links=30; var geom=new PCCArmMesh(links,8);
            var positions=new Vector3[links+1];var rotations=new Quaternion[links+1];
            for(int i=0;i<=links;i++)
            {
                int section=Mathf.Min(i/10,2);float t=(i-section*10)/10f;
                solver.EvaluatePose(section,t,out positions[i],out rotations[i]);
            }
            geom.Update(positions,rotations,.14f,.0341f,.7f);
            Mesh mesh=geom.Mesh;
            Check(mesh.vertexCount==2464,"Expected vertex count");
            Vector3[] vertices=mesh.vertices;Vector3[] normals=mesh.normals;
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                int[] tris=mesh.GetTriangles(sub);
                for(int t=0;t<tris.Length;t+=3)
                {
                    int a=tris[t],b=tris[t+1],c=tris[t+2];
                    Check(a>=0&&a<vertices.Length&&b>=0&&b<vertices.Length&&c>=0&&c<vertices.Length,"Valid mesh indices");
                    Vector3 n=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                    Check(Vector3.Dot(n,normals[a]+normals[b]+normals[c])>0,"Outward winding consistent with normals");
                }
            }
            for(int i=0;i<vertices.Length;i++) Check(mesh.bounds.Contains(vertices[i]),"Bounds contain mesh");
            Note("PASS procedural mesh indices, winding, normals and bounds; vertices="+mesh.vertexCount);
            UnityEngine.Object.DestroyImmediate(mesh);


            Note("ALL PASS: "+assertions+" assertions. Unity "+Application.unityVersion);


        }
        catch(Exception ex)
        {
            Note(ex.ToString());

            Debug.LogException(ex);

        }
    }
}
#endif

