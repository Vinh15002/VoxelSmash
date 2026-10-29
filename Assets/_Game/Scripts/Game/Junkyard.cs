using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelSmash.Game
{
    // Builds the scrapyard backdrop as one static mesh of coloured boxes for the VoxelLit shader:
    // a concrete smashing pad on dirt, junk heaps, a crane with a magnet, shipping containers,
    // crushed-car bales, recycling bins, tyre stacks, a corrugated fence and a shed beyond it.
    // Generated once by the editor setup; the layout is deterministic for a given seed.
    public static class Junkyard
    {
        // Matches the table's top surface, which debris actually lands on.
        public const float GroundY = -.02f;
        public const float PadHalfWidth = 6f, PadNear = 7f, PadFar = -5f;

        static readonly Color32 Dirt = new(124, 106, 84, 255), Concrete = new(156, 156, 150, 255),
            Hazard = new(236, 184, 30, 255), Black = new(34, 34, 38, 255), Rust = new(150, 76, 36, 255),
            Steel = new(128, 136, 146, 255), CraneYellow = new(240, 180, 26, 255), Wood = new(150, 104, 62, 255);

        static readonly Color32[] Junk =
        {
            new(150, 76, 36, 255), new(110, 56, 30, 255), new(128, 136, 146, 255), new(92, 98, 108, 255),
            new(70, 110, 160, 255), new(178, 54, 46, 255), new(220, 170, 40, 255), new(80, 128, 70, 255),
            new(214, 214, 206, 255), new(42, 42, 46, 255), new(160, 120, 80, 255)
        };

        sealed class Writer
        {
            public readonly List<Vector3> vertices = new(), normals = new();
            public readonly List<Color> colors = new();
            public readonly List<Vector2> uvs = new(), sizes = new();
            public readonly List<int> triangles = new();

            static readonly Vector3[] Axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

            // An oriented box. Bottom faces of upright boxes are skipped (they sit on something).
            public void Box(Vector3 center, Vector3 size, Quaternion rotation, Color32 color, bool edges = true, bool topOnly = false)
            {
                var half = size * .5f;
                Color linear = ((Color)color).linear;
                bool upright = rotation == Quaternion.identity;
                foreach (var n in Axes)
                {
                    if (topOnly && n != Vector3.up) continue;
                    if (upright && n == Vector3.down) continue;
                    var up = Mathf.Abs(n.y) > .5f ? Vector3.forward : Vector3.up;
                    var right = Vector3.Cross(up, -n);
                    float hn = Mathf.Abs(Vector3.Dot(half, n)), hu = Mathf.Abs(Vector3.Dot(half, up)), hr = Mathf.Abs(Vector3.Dot(half, right));
                    var c = center + rotation * (n * hn);
                    Vector3 r = rotation * right * hr, u = rotation * up * hu, normal = rotation * n;
                    int start = vertices.Count;
                    vertices.Add(c - r - u); vertices.Add(c - r + u); vertices.Add(c + r + u); vertices.Add(c + r - u);
                    var faceSize = edges ? new Vector2(hr * 2f, hu * 2f) : Vector2.zero;
                    uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, hu * 2f)); uvs.Add(new Vector2(hr * 2f, hu * 2f)); uvs.Add(new Vector2(hr * 2f, 0));
                    for (int k = 0; k < 4; k++) { normals.Add(normal); colors.Add(linear); sizes.Add(faceSize); }
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
            }

            public void Box(Vector3 center, Vector3 size, Color32 color, bool edges = true) => Box(center, size, Quaternion.identity, color, edges);

            // A thin beam between two points.
            public void Beam(Vector3 a, Vector3 b, float thickness, Color32 color) =>
                Box((a + b) * .5f, new Vector3(thickness, thickness, (b - a).magnitude), Quaternion.LookRotation(b - a), color);
        }

        public static Mesh Build(int seed = 11)
        {
            var w = new Writer();
            var random = new System.Random(seed);
            float Rand() => (float)random.NextDouble();
            Color32 Vary(Color32 c, float amount)
            {
                float k = 1f + (Rand() - .5f) * amount;
                return new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), 255);
            }

            Ground(w, Rand, Vary);
            Fence(w, Rand, Vary);
            Shed(w, new Vector3(9f, 0f, -33f));
            Heap(w, Rand, Vary, new Vector2(-6f, -10f), 3.2f, 2.6f);
            Heap(w, Rand, Vary, new Vector2(5.5f, -12.5f), 4.6f, 3.8f);
            Heap(w, Rand, Vary, new Vector2(-15f, -16f), 4.2f, 3.2f);
            Heap(w, Rand, Vary, new Vector2(16f, -17f), 4.2f, 3.4f);
            Heap(w, Rand, Vary, new Vector2(0f, -20f), 3.6f, 2.6f);
            Heap(w, Rand, Vary, new Vector2(-19f, 3f), 3.6f, 2.6f);
            Heap(w, Rand, Vary, new Vector2(19f, 7f), 3.2f, 2.4f);
            Crane(w, new Vector3(-9f, 0f, -17f), 7f);
            Container(w, Vary, new Vector3(-17f, 0f, -6f), 80f, new Color32(176, 56, 44, 255));
            Container(w, Vary, new Vector3(-16.6f, 2.6f, -6.3f), 76f, new Color32(48, 104, 170, 255));
            Container(w, Vary, new Vector3(18f, 0f, -5f), -72f, new Color32(64, 130, 82, 255));
            Bales(w, Rand, Vary, new Vector3(9.5f, 0f, -7.5f), -16f);
            Bales(w, Rand, Vary, new Vector3(-11.5f, 0f, 9f), 30f);
            Bins(w, new Vector3(2.7f, 0f, -6.2f));
            Tyres(w, new Vector3(-3.6f, 0f, -6.6f), 4);
            Tyres(w, new Vector3(-4.9f, 0f, -6.1f), 2);
            Tyres(w, new Vector3(12f, 0f, 4f), 3);
            Sign(w, new Vector3(11f, 0f, -20.5f));

            var mesh = new Mesh { name = "Junkyard", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(w.vertices);
            mesh.SetNormals(w.normals);
            mesh.SetColors(w.colors);
            mesh.SetUVs(0, w.uvs);
            mesh.SetUVs(2, w.sizes);
            mesh.SetTriangles(w.triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Ground(Writer w, System.Func<float> rand, System.Func<Color32, float, Color32> vary)
        {
            // Concrete pad in 1 m slabs with a hazard-striped border, where the sculptures stand.
            for (float x = -PadHalfWidth; x < PadHalfWidth; x += 1f)
            for (float z = PadFar; z < PadNear; z += 1f)
            {
                bool border = x < -PadHalfWidth + .5f || x > PadHalfWidth - 1.5f || z < PadFar + .5f || z > PadNear - 1.5f;
                var color = border ? (((int)(x + z + 100f)) % 2 == 0 ? Hazard : Black) : vary(Concrete, .08f);
                w.Box(new Vector3(x + .5f, GroundY - .05f, z + .5f), new Vector3(1f, .1f, 1f), Quaternion.identity, color, true, true);
            }
            // Packed dirt everywhere else, mottled with darker oil patches.
            for (float x = -44f; x < 44f; x += 2f)
            for (float z = -44f; z < 44f; z += 2f)
            {
                if (x >= -PadHalfWidth && x + 2f <= PadHalfWidth && z >= PadFar && z + 2f <= PadNear) continue;
                var color = rand() < .1f ? vary(new Color32(92, 82, 70, 255), .1f) : vary(Dirt, .14f);
                w.Box(new Vector3(x + 1f, GroundY - .06f, z + 1f), new Vector3(2f, .1f, 2f), Quaternion.identity, color, false, true);
            }
        }

        // Corrugated sheet fence around the yard; each 2 m panel leans a little and has its own paint.
        static void Fence(Writer w, System.Func<float> rand, System.Func<Color32, float, Color32> vary)
        {
            Color32[] paints = { new(120, 128, 134, 255), new(150, 82, 44, 255), new(96, 120, 96, 255), new(110, 114, 124, 255) };
            void Run(Vector3 from, Vector3 to)
            {
                var dir = (to - from).normalized;
                var rot = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up));
                int panels = Mathf.RoundToInt((to - from).magnitude / 2f);
                for (int i = 0; i < panels; i++)
                {
                    var center = from + dir * (i * 2f + 1f);
                    var lean = rot * Quaternion.Euler((rand() - .5f) * 5f, 0f, (rand() - .5f) * 3f);
                    var paint = vary(paints[(int)(rand() * paints.Length)], .12f);
                    float height = 2.6f + (rand() - .5f) * .3f;
                    for (int k = 0; k < 10; k++)
                    {
                        // Alternate ridges stand proud of the sheet to read as corrugation.
                        var offset = lean * new Vector3(-.9f + k * .2f, 0f, k % 2 == 0 ? .04f : 0f);
                        w.Box(center + offset + Vector3.up * height * .5f, new Vector3(.2f, height, .06f), lean, k % 2 == 0 ? paint : vary(paint, .2f));
                    }
                    w.Box(center + rot * new Vector3(1f, 0f, -.1f) + Vector3.up * 1.4f, new Vector3(.12f, 2.8f, .12f), Steel);
                }
            }
            Run(new Vector3(-26f, 0f, -24f), new Vector3(26f, 0f, -24f));
            Run(new Vector3(-26f, 0f, 22f), new Vector3(-26f, 0f, -24f));
            Run(new Vector3(26f, 0f, -24f), new Vector3(26f, 0f, 22f));
            Run(new Vector3(26f, 0f, 22f), new Vector3(-26f, 0f, 22f));
        }

        // A mound of earth covered in a shell of tumbled scrap pieces.
        static void Heap(Writer w, System.Func<float> rand, System.Func<Color32, float, Color32> vary, Vector2 c, float radius, float height)
        {
            const int layers = 6;
            for (int k = 0; k < layers; k++)
            {
                float t = k / (float)layers, half = radius * (1f - t) * .92f, y = height * t * .9f;
                var size = new Vector3(half * 2f, height * .9f / layers + .05f, half * 2f);
                var color = vary(new Color32(84, 74, 64, 255), .1f);
                w.Box(new Vector3(c.x, y + size.y * .5f, c.y), size, Quaternion.identity, color, false);
                w.Box(new Vector3(c.x, y + size.y * .5f, c.y), size, Quaternion.Euler(0f, 45f, 0f), color, false);
            }
            int count = (int)(radius * radius * 13f);
            for (int i = 0; i < count; i++)
            {
                float r = radius * Mathf.Sqrt(rand()), a = rand() * Mathf.PI * 2f;
                float surface = height * Mathf.Pow(Mathf.Max(0f, 1f - r / radius), 1.15f);
                var pos = new Vector3(c.x + Mathf.Cos(a) * r, surface - rand() * .25f, c.y + Mathf.Sin(a) * r);
                var rot = Quaternion.Euler(rand() * 360f, rand() * 360f, rand() * 360f);
                float kind = rand();
                if (kind < .12f)      // tyre
                    w.Box(pos, new Vector3(.7f, .25f, .7f), rot, Black);
                else if (kind < .3f)  // plank or pipe
                    w.Box(pos, new Vector3(.12f + rand() * .1f, .12f, 1f + rand() * 1.2f), rot, rand() < .5f ? vary(Wood, .2f) : vary(Steel, .2f));
                else if (kind < .38f) // white goods
                    w.Box(pos, new Vector3(.6f, .8f, .6f), rot, vary(new Color32(222, 222, 214, 255), .1f));
                else
                {
                    float s = .25f + rand() * .45f;
                    w.Box(pos, new Vector3(s, s * (.5f + rand()), s * (.6f + rand() * .8f)), rot, vary(Junk[(int)(rand() * Junk.Length)], .18f));
                }
            }
        }

        // Lattice tower crane with a jib reaching toward the yard and an electromagnet on a cable.
        static void Crane(Writer w, Vector3 baseCenter, float magnetX)
        {
            const float mast = 13f, width = 1.1f, jibY = 13.5f;
            float h = width * .5f;
            Vector3[] corners = { new(-h, 0, -h), new(h, 0, -h), new(h, 0, h), new(-h, 0, h) };
            w.Box(baseCenter + Vector3.up * .3f, new Vector3(3f, .6f, 3f), new Color32(90, 90, 92, 255));
            foreach (var corner in corners)
                w.Box(baseCenter + corner + Vector3.up * (mast * .5f + .6f), new Vector3(.14f, mast, .14f), CraneYellow);
            for (float y = .6f; y < mast; y += 1.4f)
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = baseCenter + corners[i] + Vector3.up * y, b = baseCenter + corners[(i + 1) % 4] + Vector3.up * y;
                    w.Beam(a, b, .08f, CraneYellow);
                    w.Beam(a, b + Vector3.up * 1.4f, .07f, CraneYellow);
                }
            var top = baseCenter + Vector3.up * jibY;
            w.Box(top + new Vector3(-.4f, -.6f, 0f), new Vector3(1.6f, 1.2f, 1.4f), new Color32(60, 70, 84, 255));   // cab
            w.Box(top + new Vector3(-.2f, -.55f, .71f), new Vector3(1f, .6f, .02f), new Color32(130, 190, 220, 255));
            float jibEnd = magnetX - baseCenter.x + 2f;
            w.Box(top + new Vector3(jibEnd * .5f, .3f, 0f), new Vector3(jibEnd + 1f, .5f, .6f), CraneYellow);
            for (float x = .5f; x < jibEnd; x += 1f)
                w.Beam(top + new Vector3(x, .55f, -.3f), top + new Vector3(x + 1f, .55f, .3f), .06f, CraneYellow);
            w.Box(top + new Vector3(-3f, .3f, 0f), new Vector3(4f, .4f, .6f), CraneYellow);                            // counter-jib
            w.Box(top + new Vector3(-4.4f, -.3f, 0f), new Vector3(1.2f, 1f, 1f), new Color32(80, 80, 84, 255));       // counterweight
            w.Beam(top + Vector3.up * 2.2f, top + new Vector3(jibEnd, .5f, 0f), .05f, Steel);
            w.Beam(top + Vector3.up * 2.2f, top + new Vector3(-4.4f, .5f, 0f), .05f, Steel);
            w.Box(top + Vector3.up * 1.4f, new Vector3(.3f, 1.8f, .3f), CraneYellow);
            var hook = new Vector3(magnetX, 0f, baseCenter.z);
            w.Box(hook + Vector3.up * (jibY - 2.6f), new Vector3(.04f, 5.2f, .04f), Black);
            for (int i = 0; i < 2; i++)
                w.Box(hook + Vector3.up * (jibY - 5.4f), new Vector3(1.6f, .4f, 1.6f), Quaternion.Euler(0f, i * 45f, 0f), new Color32(54, 56, 62, 255));
            w.Box(hook + Vector3.up * (jibY - 5.1f), new Vector3(.5f, .3f, .5f), Hazard);
        }

        // 6 m ribbed shipping container.
        static void Container(Writer w, System.Func<Color32, float, Color32> vary, Vector3 corner, float yaw, Color32 paint)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var center = corner + Vector3.up * 1.3f;
            w.Box(center, new Vector3(6f, 2.6f, 2.4f), rot, paint);
            for (int i = 0; i < 20; i++)
            {
                var c = vary(paint, .12f);
                float x = -2.85f + i * .3f;
                w.Box(center + rot * new Vector3(x, 0f, 1.23f), new Vector3(.14f, 2.4f, .06f), rot, c);
                w.Box(center + rot * new Vector3(x, 0f, -1.23f), new Vector3(.14f, 2.4f, .06f), rot, c);
            }
            w.Box(center + rot * new Vector3(3.02f, 0f, 0f), new Vector3(.06f, 2.5f, 2.3f), rot, vary(paint, .2f));
            w.Box(center + rot * new Vector3(3.06f, 0f, -.4f), new Vector3(.04f, 2.2f, .06f), rot, Steel);
            w.Box(center + rot * new Vector3(3.06f, 0f, .4f), new Vector3(.04f, 2.2f, .06f), rot, Steel);
        }

        // Pyramid of crushed-car bales: layered stripes of flattened bodywork.
        static void Bales(Writer w, System.Func<float> rand, System.Func<Color32, float, Color32> vary, Vector3 origin, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            int[] rows = { 3, 2, 1 };
            for (int row = 0; row < rows.Length; row++)
            for (int i = 0; i < rows[row]; i++)
            {
                var local = new Vector3((i - (rows[row] - 1) * .5f) * 1.5f, row * .85f, 0f);
                var twist = rot * Quaternion.Euler(0f, (rand() - .5f) * 10f, 0f);
                for (int layer = 0; layer < 4; layer++)
                {
                    var paint = rand() < .4f ? vary(Rust, .2f) : vary(Junk[(int)(rand() * Junk.Length)], .15f);
                    w.Box(origin + rot * local + Vector3.up * (.11f + layer * .2f), new Vector3(1.4f + (rand() - .5f) * .1f, .2f, .9f + (rand() - .5f) * .1f), twist, paint);
                }
            }
        }

        // Wheelie bins in recycling colours.
        static void Bins(Writer w, Vector3 origin)
        {
            Color32[] bins = { new(46, 150, 72, 255), new(40, 110, 200, 255), new(236, 190, 40, 255), new(110, 114, 120, 255) };
            for (int i = 0; i < bins.Length; i++)
            {
                var c = origin + Vector3.right * i * .85f;
                w.Box(c + Vector3.up * .58f, new Vector3(.7f, 1f, .75f), bins[i]);
                w.Box(c + new Vector3(0f, 1.1f, -.03f), new Vector3(.76f, .08f, .84f), Quaternion.Euler(-8f, 0f, 0f), (Color32)Color.Lerp(bins[i], Color.black, .25f));
                w.Box(c + new Vector3(-.28f, .1f, -.3f), new Vector3(.08f, .2f, .2f), Black);
                w.Box(c + new Vector3(.28f, .1f, -.3f), new Vector3(.08f, .2f, .2f), Black);
                w.Box(c + new Vector3(0f, .6f, .38f), new Vector3(.3f, .3f, .02f), new Color32(240, 240, 236, 255));
            }
        }

        // Stack of tyres, each an octagonal ring.
        static void Tyres(Writer w, Vector3 origin, int count)
        {
            for (int t = 0; t < count; t++)
            {
                var c = origin + Vector3.up * (.13f + t * .26f);
                for (int k = 0; k < 8; k++)
                {
                    var rot = Quaternion.Euler(0f, k * 45f + t * 11f, 0f);
                    w.Box(c + rot * new Vector3(0f, 0f, .33f), new Vector3(.3f, .25f, .2f), rot, Black);
                }
                w.Box(c, new Vector3(.32f, .2f, .32f), Quaternion.Euler(0f, t * 11f, 0f), new Color32(120, 124, 130, 255));
            }
        }

        // Billboard with a voxel recycling triangle.
        static void Sign(Writer w, Vector3 origin)
        {
            w.Box(origin + new Vector3(-1.6f, 2f, 0f), new Vector3(.2f, 4f, .2f), Steel);
            w.Box(origin + new Vector3(1.6f, 2f, 0f), new Vector3(.2f, 4f, .2f), Steel);
            w.Box(origin + new Vector3(0f, 4.6f, 0f), new Vector3(4.4f, 2.4f, .15f), new Color32(44, 140, 76, 255));
            var c = origin + new Vector3(0f, 4.55f, .1f);
            for (int side = 0; side < 3; side++)
            {
                float a0 = Mathf.PI / 2f + side * Mathf.PI * 2f / 3f, a1 = a0 + Mathf.PI * 2f / 3f;
                var p0 = c + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * .9f;
                var p1 = c + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * .9f;
                for (int k = 1; k < 7; k++) w.Box(Vector3.Lerp(p0, p1, k / 8f), Vector3.one * .16f, new Color32(240, 250, 240, 255));
                var tip = Vector3.Lerp(p0, p1, 7.5f / 8f);
                w.Box(tip, new Vector3(.32f, .32f, .08f), Quaternion.Euler(0f, 0f, a1 * Mathf.Rad2Deg + 45f), new Color32(240, 250, 240, 255));
            }
        }

        // Corrugated warehouse beyond the fence.
        static void Shed(Writer w, Vector3 origin)
        {
            w.Box(origin + Vector3.up * 4f, new Vector3(20f, 8f, 10f), new Color32(112, 128, 140, 255));
            for (float x = -9.75f; x < 10f; x += .5f)
                w.Box(origin + new Vector3(x, 4f, 5.03f), new Vector3(.2f, 8f, .06f), new Color32(100, 116, 128, 255));
            w.Box(origin + new Vector3(0f, 8.6f, 2.5f), new Vector3(20.6f, .3f, 5.6f), Quaternion.Euler(14f, 0f, 0f), new Color32(150, 86, 60, 255));
            w.Box(origin + new Vector3(0f, 8.6f, -2.5f), new Vector3(20.6f, .3f, 5.6f), Quaternion.Euler(-14f, 0f, 0f), new Color32(132, 76, 54, 255));
            w.Box(origin + new Vector3(0f, 2.2f, 5.06f), new Vector3(5f, 4.4f, .04f), new Color32(70, 74, 82, 255));
        }
    }
}
