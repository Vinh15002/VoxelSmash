using NUnit.Framework;
using UnityEngine;
using VoxelSmash.Game;

namespace VoxelSmash.Tests
{
    // Every primitive must face outward, or the lit weapon models render inside-out.
    public class ProceduralMeshTests
    {
        static void AssertOutward(Mesh mesh, System.Func<Vector3, Vector3> outwardAt)
        {
            var v = mesh.vertices;
            var t = mesh.triangles;
            int checkedFaces = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                var n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-10f) continue; // degenerate pole triangles
                var center = (a + b + c) / 3f;
                Assert.Greater(Vector3.Dot(n, outwardAt(center)), 0f, $"{mesh.name} face {i / 3} points inward");
                checkedFaces++;
            }
            Assert.Greater(checkedFaces, 0);
        }

        [Test] public void BoxFacesOutward() => AssertOutward(ProceduralMeshes.Box, p => p);
        [Test] public void SphereFacesOutward() => AssertOutward(ProceduralMeshes.Sphere(), p => p);
        [Test] public void OctahedronFacesOutward() => AssertOutward(ProceduralMeshes.Octahedron(.45f), p => p);

        [Test]
        public void CylinderAndConeFaceOutward()
        {
            // Outward from the axis on the sides, along y on the caps.
            System.Func<Vector3, Vector3> outward = p => Mathf.Abs(Mathf.Abs(p.y) - .5f) < 1e-4f ? new Vector3(0, p.y, 0) : new Vector3(p.x, 0, p.z);
            AssertOutward(ProceduralMeshes.Cylinder(.3f, .3f, 1f), outward);
            AssertOutward(ProceduralMeshes.Cone(.3f, 1f), p => Mathf.Abs(p.y + .5f) < 1e-4f ? Vector3.down : new Vector3(p.x, 0, p.z));
        }

        [Test]
        public void TorusFacesAwayFromItsTube()
        {
            AssertOutward(ProceduralMeshes.Torus(1f, .2f, 8, 24), p =>
            {
                var ring = new Vector3(p.x, p.y, 0).normalized; // nearest point on the tube's centre line
                return p - ring;
            });
        }

        [Test]
        public void PrismFacesOutward()
        {
            var outline = new Vector2[8];
            for (int i = 0; i < 8; i++) outline[i] = new Vector2(Mathf.Cos(i * Mathf.PI / 4), Mathf.Sin(i * Mathf.PI / 4));
            AssertOutward(ProceduralMeshes.Prism("test-octagon", outline, .2f), p =>
                Mathf.Abs(p.z - .2f) < 1e-4f ? Vector3.forward : Mathf.Abs(p.z) < 1e-4f ? Vector3.back : new Vector3(p.x, p.y, 0));
        }
    }
}
