using UnityEngine;
using UnityEngine.Rendering;

namespace PCCMechanicalArm
{
    /// <summary>A single dynamic mesh: rigid annular shells plus telescoping inner rods.</summary>
    public sealed class PCCArmMesh : System.IDisposable
    {
        private readonly Mesh mesh;
        private readonly Vector3[] vertices, normals;
        private readonly Vector3[] radial;
        private readonly int sides, nodes, links;
        private int cursor;
        public Mesh Mesh => mesh;

        public PCCArmMesh(int links, int sides)
        {
            if (links < 1 || links > 192 || sides < 3 || sides > 12)
                throw new System.ArgumentOutOfRangeException(nameof(links));
            this.links = links; this.nodes = links + 1; this.sides = sides;
            int vertexCount = nodes * sides * 8 + links * sides * 2;
            vertices = new Vector3[vertexCount]; normals = new Vector3[vertexCount];
            radial = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = 2f * Mathf.PI * i / sides;
                radial[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            }
            int[] shells = new int[nodes * sides * 24];
            int[] cores = new int[links * sides * 6];
            int shellIndex = 0, coreIndex = 0, v = 0;
            for (int i = 0; i < nodes; i++)
            {
                AppendSurface(shells, ref shellIndex, v, false); v += sides * 2; // outer
                AppendSurface(shells, ref shellIndex, v, true); v += sides * 2;  // inner
                AppendSurface(shells, ref shellIndex, v, false); v += sides * 2; // front
                AppendSurface(shells, ref shellIndex, v, true); v += sides * 2;  // back
            }
            for (int i = 0; i < links; i++)
            {
                AppendSurface(cores, ref coreIndex, v, false); v += sides * 2;
            }
            mesh = new Mesh { name = "PCC Mechanical Arm (Runtime)", indexFormat = IndexFormat.UInt16 };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(shells, 0, false);
            mesh.SetTriangles(cores, 1, false);
        }

        private void AppendSurface(int[] indices, ref int index, int start, bool reverse)
        {
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int a = start + i, b = start + next;
                int c = start + sides + i, d = start + sides + next;
                if (!reverse)
                {
                    indices[index++] = a; indices[index++] = b; indices[index++] = c;
                    indices[index++] = b; indices[index++] = d; indices[index++] = c;
                }
                else
                {
                    indices[index++] = a; indices[index++] = c; indices[index++] = b;
                    indices[index++] = b; indices[index++] = c; indices[index++] = d;
                }
            }
        }

        public void Update(Vector3[] positions, Quaternion[] rotations, float radius,
            float shellThickness, float tipScale)
        {
            cursor = 0;
            Vector3 minimum = positions[0], maximum = positions[0];
            for (int i = 0; i < nodes; i++)
            {
                float r = radius * Mathf.Lerp(1f, tipScale, (float)i / links);
                WriteShell(positions[i], rotations[i], r, shellThickness);
                minimum = Vector3.Min(minimum, positions[i]);
                maximum = Vector3.Max(maximum, positions[i]);
            }
            for (int i = 0; i < links; i++)
            {
                Vector3 chord = positions[i + 1] - positions[i];
                float length = chord.magnitude;
                Quaternion rotation = length > 1e-7f
                    ? Quaternion.FromToRotation(Vector3.forward, chord / length) : rotations[i];
                float r = radius * 0.47f * Mathf.Lerp(1f, tipScale, (i + 0.5f) / links);
                WriteCylinder((positions[i] + positions[i + 1]) * 0.5f, rotation, r,
                    length + shellThickness * 0.3f, false);
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            float padding = radius + shellThickness;
            mesh.bounds = new Bounds((minimum + maximum) * 0.5f,
                maximum - minimum + Vector3.one * (padding * 2f));
        }

        private void WriteShell(Vector3 center, Quaternion rotation, float radius, float thickness)
        {
            WriteCylinder(center, rotation, radius, thickness, false);
            WriteCylinder(center, rotation, radius * 0.50f, thickness, true);
            WriteCap(center, rotation, radius, thickness * 0.5f, true);
            WriteCap(center, rotation, radius, -thickness * 0.5f, false);
        }

        private void WriteCylinder(Vector3 center, Quaternion rotation, float radius,
            float length, bool inward)
        {
            for (int end = 0; end < 2; end++)
            for (int i = 0; i < sides; i++)
            {
                Vector3 v = radial[i] * radius + Vector3.forward * ((end - 0.5f) * length);
                vertices[cursor] = center + rotation * v;
                normals[cursor++] = rotation * (inward ? -radial[i] : radial[i]);
            }
        }

        private void WriteCap(Vector3 center, Quaternion rotation, float radius, float z, bool front)
        {
            // First ring is outer, second inner; same winding as a cylindrical strip gives +Z.
            Vector3 normal = rotation * (front ? Vector3.forward : Vector3.back);
            for (int ring = 0; ring < 2; ring++)
            for (int i = 0; i < sides; i++)
            {
                Vector3 v = radial[i] * (radius * (ring == 0 ? 1f : 0.5f));
                v.z = z;
                vertices[cursor] = center + rotation * v;
                normals[cursor++] = normal;
            }
        }

        public void Dispose()
        {
            if (mesh != null) Object.Destroy(mesh);
        }
    }
}

