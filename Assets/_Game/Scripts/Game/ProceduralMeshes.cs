using System.Collections.Generic;
using UnityEngine;

namespace VoxelSmash.Game
{
    // Flat-shaded primitives with three.js conventions (unit sphere radius, cylinder along y centred
    // on the origin, torus in the xy plane) so the reference's weapon recipes port number-for-number.
    public static class ProceduralMeshes
    {
        static readonly Dictionary<string, Mesh> Cache = new();

        public static Mesh Box => Cached("box", () =>
        {
            var b = new Builder();
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++) c[i] = new Vector3((i & 1) - .5f, ((i >> 1) & 1) - .5f, ((i >> 2) & 1) - .5f);
            b.Quad(c[0], c[2], c[3], c[1]); b.Quad(c[4], c[5], c[7], c[6]);
            b.Quad(c[0], c[1], c[5], c[4]); b.Quad(c[2], c[6], c[7], c[3]);
            b.Quad(c[0], c[4], c[6], c[2]); b.Quad(c[1], c[3], c[7], c[5]);
            return b.Build("Box");
        });

        public static Mesh Sphere(int segments = 16, int rings = 10) => Cached($"sphere{segments}x{rings}", () =>
        {
            var b = new Builder();
            Vector3 P(int s, int r)
            {
                float phi = Mathf.PI * r / rings, theta = 2f * Mathf.PI * s / segments;
                return new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
            }
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
                b.Quad(P(s, r), P(s + 1, r), P(s + 1, r + 1), P(s, r + 1));
            return b.Build("Sphere");
        });

        public static Mesh Cylinder(float radiusTop, float radiusBottom, float height, int segments = 16) =>
            Cached($"cyl{radiusTop}/{radiusBottom}/{height}/{segments}", () =>
            {
                var b = new Builder();
                float h = height / 2f;
                for (int s = 0; s < segments; s++)
                {
                    float a0 = 2f * Mathf.PI * s / segments, a1 = 2f * Mathf.PI * (s + 1) / segments;
                    var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                    var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                    Vector3 t0 = d0 * radiusTop + Vector3.up * h, t1 = d1 * radiusTop + Vector3.up * h;
                    Vector3 b0 = d0 * radiusBottom - Vector3.up * h, b1 = d1 * radiusBottom - Vector3.up * h;
                    b.Quad(b0, t0, t1, b1);
                    if (radiusTop > 0f) b.Tri(Vector3.up * h, t1, t0);
                    if (radiusBottom > 0f) b.Tri(-Vector3.up * h, b0, b1);
                }
                return b.Build("Cylinder");
            });

        public static Mesh Cone(float radius, float height, int segments = 16) => Cylinder(0f, radius, height, segments);

        public static Mesh Torus(float radius, float tube, int radialSegments, int tubularSegments) =>
            Cached($"torus{radius}/{tube}/{radialSegments}/{tubularSegments}", () =>
            {
                var b = new Builder();
                Vector3 P(int i, int j)
                {
                    float u = 2f * Mathf.PI * i / tubularSegments, v = 2f * Mathf.PI * j / radialSegments;
                    float ring = radius + tube * Mathf.Cos(v);
                    return new Vector3(ring * Mathf.Cos(u), ring * Mathf.Sin(u), tube * Mathf.Sin(v));
                }
                for (int i = 0; i < tubularSegments; i++)
                for (int j = 0; j < radialSegments; j++)
                    b.Quad(P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1));
                return b.Build("Torus");
            });

        public static Mesh Octahedron(float radius) => Cached($"octa{radius}", () =>
        {
            var b = new Builder();
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var x in new[] { axes[0], axes[1] })
            foreach (var y in new[] { axes[2], axes[3] })
            foreach (var z in new[] { axes[4], axes[5] })
            {
                // Wind each face outward regardless of octant.
                bool flip = (x.x * y.y * z.z) < 0f;
                if (flip) b.Tri(x * radius, z * radius, y * radius);
                else b.Tri(x * radius, y * radius, z * radius);
            }
            return b.Build("Octahedron");
        });

        // Extrudes a star-shaped (origin-visible) outline along +z.
        public static Mesh Prism(string key, Vector2[] outline, float depth) => Cached("prism" + key, () =>
        {
            var b = new Builder();
            int n = outline.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = outline[i], p1 = outline[(i + 1) % n];
                Vector3 f0 = new(p0.x, p0.y, depth), f1 = new(p1.x, p1.y, depth);
                Vector3 k0 = new(p0.x, p0.y, 0), k1 = new(p1.x, p1.y, 0);
                b.Tri(new Vector3(0, 0, depth), f0, f1);
                b.Tri(Vector3.zero, k1, k0);
                b.Quad(k0, k1, f1, f0);
            }
            return b.Build("Prism");
        });

        static Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (!Cache.TryGetValue(key, out var mesh) || mesh == null) Cache[key] = mesh = make();
            return mesh;
        }

        // Unindexed triangles so RecalculateNormals gives the faceted low-poly look. Every face is
        // emitted with cross(b - a, c - a) pointing outward, which is Unity's front face.
        sealed class Builder
        {
            readonly List<Vector3> vertices = new();

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                var indices = new int[vertices.Count];
                for (int i = 0; i < indices.Length; i++) indices[i] = i;
                mesh.SetTriangles(indices, 0);
                var white = new Color[vertices.Count];
                System.Array.Fill(white, Color.white);
                mesh.colors = white;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
