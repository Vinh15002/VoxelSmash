using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelSmash.Core;

namespace VoxelSmash.Game
{
    // Draws a sculpture as 8x8x8 chunk meshes with hidden faces culled. Only chunks touched by a
    // strike are rebuilt. Each chunk's greedy-merged BoxColliders are both the static physics
    // the debris lands on and the tap raycast target.
    public sealed class SculptureView : MonoBehaviour
    {
        const int ChunkSize = 8;

        sealed class Chunk
        {
            public GameObject go;
            public Mesh mesh;
            public Vector3Int origin;
            public readonly List<BoxCollider> boxes = new();
            public readonly List<int> cells = new();
        }

        static readonly Vector3Int[] Faces =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1)
        };

        Sculpture sculpture;
        Material material;
        readonly Dictionary<Vector3Int, Chunk> chunks = new();
        readonly HashSet<Vector3Int> dirty = new();
        readonly List<Vector3> vertices = new();
        readonly List<Vector3> normals = new();
        readonly List<Color> colors = new();
        readonly List<Vector2> uvs = new(), faceSizes = new();
        readonly List<int> triangles = new();
        Color[] linearPalette;
        PhysicsMaterial physicsMaterial;
        readonly bool[] occupancy = new bool[ChunkSize * ChunkSize * ChunkSize];
        readonly List<VoxelBoxes.Box> merged = new();

        public Sculpture Sculpture => sculpture;

        public static SculptureView Create(Sculpture sculpture, VoxelModel model, Material material,
            PhysicsMaterial physicsMaterial, int layer, Transform parent)
        {
            var go = new GameObject("Sculpture") { layer = layer };
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<SculptureView>();
            view.sculpture = sculpture;
            view.material = material;
            view.physicsMaterial = physicsMaterial;
            view.linearPalette = new Color[model.palette.Length];
            for (int i = 0; i < model.palette.Length; i++) view.linearPalette[i] = ((Color)model.palette[i]).linear;
            for (int i = 0; i < sculpture.Total; i++) view.ChunkFor(sculpture.cells[i].grid).cells.Add(i);
            foreach (var c in view.chunks) view.Rebuild(c.Value);
            return view;
        }

        public void SetYaw(float yaw) => transform.localRotation = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);

        public Vector3 WorldPosition(int cell) => transform.TransformPoint(sculpture.Position(cell));

        public Color CellColor(int cell)
        {
            var c = sculpture.cells[cell];
            return linearPalette[c.color] * c.shade;
        }

        // Step half a voxel into the surface so the hit lands inside the struck cell.
        public int CellFromHit(RaycastHit hit) =>
            sculpture.CellAt(transform.InverseTransformPoint(hit.point - hit.normal * (sculpture.size * .5f * transform.lossyScale.x)));

        public void OnRemoved(List<int> removed)
        {
            foreach (int i in removed)
            {
                var g = sculpture.cells[i].grid;
                // A newly exposed neighbour in the next chunk needs its face drawn, and corner
                // occlusion reads diagonal neighbours too, so touch every adjacent chunk.
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++) dirty.Add(ChunkKey(g + new Vector3Int(dx, dy, dz)));
            }
            foreach (var key in dirty)
                if (chunks.TryGetValue(key, out var chunk)) Rebuild(chunk);
            dirty.Clear();
        }

        static Vector3Int ChunkKey(Vector3Int g) =>
            new(Mathf.FloorToInt(g.x / (float)ChunkSize), Mathf.FloorToInt(g.y / (float)ChunkSize), Mathf.FloorToInt(g.z / (float)ChunkSize));

        Chunk ChunkFor(Vector3Int g)
        {
            var key = ChunkKey(g);
            if (chunks.TryGetValue(key, out var chunk)) return chunk;
            chunk = new Chunk { go = new GameObject($"Chunk {key}"), mesh = new Mesh { name = $"Chunk {key}" } };
            chunk.mesh.MarkDynamic();
            chunk.go.transform.SetParent(transform, false);
            chunk.go.AddComponent<MeshFilter>().sharedMesh = chunk.mesh;
            var renderer = chunk.go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            chunk.go.layer = gameObject.layer;
            chunk.origin = key * ChunkSize;
            chunks[key] = chunk;
            return chunk;
        }

        void Rebuild(Chunk chunk)
        {
            vertices.Clear(); normals.Clear(); colors.Clear(); triangles.Clear(); uvs.Clear(); faceSizes.Clear();
            float h = sculpture.size * .5f;
            foreach (int i in chunk.cells)
            {
                var cell = sculpture.cells[i];
                if (!cell.alive) continue;
                var center = sculpture.Position(i);
                var color = linearPalette[cell.color] * cell.shade;
                foreach (var f in Faces)
                {
                    if (sculpture.IsAlive(cell.grid + f)) continue;
                    if (f.y < 0 && cell.grid.y == 0) continue; // never seen through the table
                    AddFace(cell.grid, center, f, h, color);
                }
            }
            chunk.mesh.Clear();
            bool empty = vertices.Count == 0;
            chunk.go.SetActive(!empty);
            if (empty) return;
            chunk.mesh.SetVertices(vertices);
            chunk.mesh.SetNormals(normals);
            chunk.mesh.SetColors(colors);
            chunk.mesh.SetUVs(0, uvs);
            chunk.mesh.SetUVs(2, faceSizes);
            chunk.mesh.SetTriangles(triangles, 0);
            chunk.mesh.RecalculateBounds();
            RebuildColliders(chunk);
        }

        void RebuildColliders(Chunk chunk)
        {
            System.Array.Clear(occupancy, 0, occupancy.Length);
            foreach (int i in chunk.cells)
            {
                var cell = sculpture.cells[i];
                if (!cell.alive) continue;
                var g = cell.grid - chunk.origin;
                occupancy[VoxelBoxes.Index(g.x, g.y, g.z, ChunkSize)] = true;
            }
            VoxelBoxes.Merge(occupancy, ChunkSize, merged);
            float s = sculpture.size;
            for (int b = 0; b < merged.Count; b++)
            {
                if (b == chunk.boxes.Count)
                {
                    var added = chunk.go.AddComponent<BoxCollider>();
                    added.sharedMaterial = physicsMaterial;
                    chunk.boxes.Add(added);
                }
                var box = chunk.boxes[b];
                Vector3 min = chunk.origin + merged[b].min, size = merged[b].size;
                box.enabled = true;
                // Cells are centred on integer x/z but sit on y; shrink slightly so debris never snags on seams.
                box.center = new Vector3((min.x + (size.x - 1) * .5f) * s, (min.y + size.y * .5f) * s, (min.z + (size.z - 1) * .5f) * s);
                box.size = size * s - Vector3.one * .0036f;
            }
            for (int b = merged.Count; b < chunk.boxes.Count; b++) chunk.boxes[b].enabled = false;
        }

        // Per-corner occlusion levels (0 = tucked into a corner, 3 = open), the classic voxel AO.
        static readonly float[] Occlusion = { .56f, .72f, .87f, 1f };
        static readonly Vector2Int[] Corners = { new(-1, -1), new(-1, 1), new(1, 1), new(1, -1) };

        void AddFace(Vector3Int grid, Vector3 center, Vector3Int face, float h, Color color)
        {
            Vector3 n = face;
            // Bottom-left, top-left, top-right, bottom-right as seen from outside: clockwise for Unity.
            var upI = face.y != 0 ? new Vector3Int(0, 0, 1) : new Vector3Int(0, 1, 0);
            var rightI = Vector3Int.RoundToInt(Vector3.Cross(upI, -n));
            Vector3 up = upI, right = rightI;
            Vector3 c = center + n * h;
            int start = vertices.Count;
            var outside = grid + face;
            Span<int> ao = stackalloc int[4];
            for (int k = 0; k < 4; k++)
            {
                var corner = Corners[k];
                vertices.Add(c + (right * corner.x + up * corner.y) * h);
                int side1 = Solid(outside + rightI * corner.x) ? 1 : 0, side2 = Solid(outside + upI * corner.y) ? 1 : 0;
                int diagonal = Solid(outside + rightI * corner.x + upI * corner.y) ? 1 : 0;
                ao[k] = side1 + side2 == 2 ? 0 : 3 - side1 - side2 - diagonal;
                var shaded = color * Occlusion[ao[k]];
                shaded.a = 1f;
                normals.Add(n);
                colors.Add(shaded);
                uvs.Add(new Vector2((corner.x + 1) * h, (corner.y + 1) * h));
                faceSizes.Add(new Vector2(h * 2f, h * 2f));
            }
            // Split the quad along the brighter diagonal so occlusion interpolates without a crease.
            if (ao[0] + ao[2] >= ao[1] + ao[3])
            {
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
            else
            {
                triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start + 3);
                triangles.Add(start + 1); triangles.Add(start + 3); triangles.Add(start);
            }
        }

        // The table counts as solid so the bottom layer gets a contact shadow.
        bool Solid(Vector3Int g) => g.y < 0 || sculpture.IsAlive(g);

        void OnDestroy()
        {
            foreach (var c in chunks.Values) Destroy(c.mesh);
        }
    }
}
