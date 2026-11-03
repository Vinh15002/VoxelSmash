using System.Collections.Generic;
using UnityEngine;
using VoxelSmash.Core;

namespace VoxelSmash.Game
{
    // Draws a sculpture as 8x8x8 chunk meshes with hidden faces culled, one submesh per voxel colour
    // (each with its PixelLand cube material). Only chunks touched by a strike are rebuilt. Each chunk's greedy-merged BoxColliders are both the static physics
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
        VoxelMaterials materials;
        readonly Dictionary<Vector3Int, Chunk> chunks = new();
        readonly HashSet<Vector3Int> dirty = new();
        readonly List<Vector3> vertices = new();
        readonly List<Vector3> normals = new();
        readonly List<Vector2> uvs = new();
        // Triangles per colour material, reused between rebuilds.
        readonly Dictionary<Material, List<int>> groups = new();
        readonly List<Material> used = new();
        Color[] linearPalette;
        PhysicsMaterial physicsMaterial;
        readonly bool[] occupancy = new bool[ChunkSize * ChunkSize * ChunkSize];
        readonly List<VoxelBoxes.Box> merged = new();

        public Sculpture Sculpture => sculpture;

        public static SculptureView Create(Sculpture sculpture, VoxelModel model, VoxelMaterials materials,
            PhysicsMaterial physicsMaterial, int layer, Transform parent)
        {
            var go = new GameObject("Sculpture") { layer = layer };
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<SculptureView>();
            view.sculpture = sculpture;
            view.materials = materials;
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
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            chunk.go.layer = gameObject.layer;
            chunk.origin = key * ChunkSize;
            chunks[key] = chunk;
            return chunk;
        }

        void Rebuild(Chunk chunk)
        {
            vertices.Clear(); normals.Clear(); uvs.Clear(); used.Clear();
            foreach (var g in groups.Values) g.Clear();
            float h = sculpture.size * .5f;
            foreach (int i in chunk.cells)
            {
                var cell = sculpture.cells[i];
                if (!cell.alive) continue;
                var center = sculpture.Position(i);
                var material = materials.For(linearPalette[cell.color] * cell.shade);
                if (!groups.TryGetValue(material, out var triangles)) groups[material] = triangles = new List<int>();
                if (triangles.Count == 0) used.Add(material);
                foreach (var f in Faces)
                {
                    if (sculpture.IsAlive(cell.grid + f)) continue;
                    if (f.y < 0 && cell.grid.y == 0) continue; // never seen through the table
                    AddFace(center, f, h, triangles);
                }
            }
            chunk.mesh.Clear();
            bool empty = vertices.Count == 0;
            chunk.go.SetActive(!empty);
            if (empty) return;
            chunk.mesh.SetVertices(vertices);
            chunk.mesh.SetNormals(normals);
            chunk.mesh.SetUVs(0, uvs);
            // A cell whose faces are all hidden still registered its material; drop empty groups.
            used.RemoveAll(m => groups[m].Count == 0);
            chunk.mesh.subMeshCount = used.Count;
            for (int m = 0; m < used.Count; m++) chunk.mesh.SetTriangles(groups[used[m]], m, false);
            chunk.go.GetComponent<MeshRenderer>().sharedMaterials = used.ToArray();
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

        static readonly Vector2Int[] Corners = { new(-1, -1), new(-1, 1), new(1, 1), new(1, -1) };

        // One quad with uv 0..1 across the face, like PixelLand's cube mesh.
        void AddFace(Vector3 center, Vector3Int face, float h, List<int> triangles)
        {
            Vector3 n = face;
            // Bottom-left, top-left, top-right, bottom-right as seen from outside: clockwise for Unity.
            Vector3 up = face.y != 0 ? Vector3.forward : Vector3.up;
            var right = Vector3.Cross(up, -n);
            Vector3 c = center + n * h;
            int start = vertices.Count;
            foreach (var corner in Corners)
            {
                vertices.Add(c + (right * corner.x + up * corner.y) * h);
                normals.Add(n);
                uvs.Add(new Vector2((corner.x + 1) * .5f, (corner.y + 1) * .5f));
            }
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        void OnDestroy()
        {
            foreach (var c in chunks.Values) Destroy(c.mesh);
        }
    }
}
