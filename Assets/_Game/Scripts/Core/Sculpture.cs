using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelSmash.Core
{
    public enum StrikeMode { Sphere, Slice, Beam }

    // Deterministic voxel damage and support graph. Renderer-independent so it can be unit tested.
    public sealed class Sculpture
    {
        public struct Cell
        {
            public Vector3Int grid;
            public byte color;
            public float shade;
            public bool alive;
        }

        public const float ClearThreshold = .97f;

        static readonly Vector3Int[] Neighbors =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1)
        };

        public readonly float size;
        public readonly Cell[] cells;
        readonly Dictionary<Vector3Int, int> index;
        readonly int baseY;
        // Scratch buffers reused by every strike's support pass.
        readonly bool[] supported;
        readonly int[] queue;

        public int Total => cells.Length;
        public int Removed { get; private set; }
        public int Hits { get; private set; }
        public float Progress => Total == 0 ? 0f : (float)Removed / Total;

        public Sculpture(VoxelModel model) : this(model.voxels, model.voxelSize) { }

        public Sculpture(IReadOnlyList<Voxel> voxels, float voxelSize)
        {
            size = voxelSize;
            cells = new Cell[voxels.Count];
            index = new Dictionary<Vector3Int, int>(voxels.Count);
            baseY = int.MaxValue;
            for (int i = 0; i < voxels.Count; i++)
            {
                var v = voxels[i];
                cells[i] = new Cell { grid = v.Grid, color = v.color, shade = v.shade, alive = true };
                index[v.Grid] = i;
                baseY = Math.Min(baseY, v.y);
            }
            supported = new bool[cells.Length];
            queue = new int[cells.Length];
        }

        public Vector3 Position(int i)
        {
            var g = cells[i].grid;
            return new Vector3(g.x * size, (g.y + .5f) * size, g.z * size);
        }

        public Vector3Int GridAt(Vector3 local) =>
            new(Mathf.RoundToInt(local.x / size), Mathf.FloorToInt(local.y / size), Mathf.RoundToInt(local.z / size));

        public bool IsAlive(Vector3Int g) => index.TryGetValue(g, out int i) && cells[i].alive;

        // Index of the living cell containing a sculpture-local point, or -1.
        public int CellAt(Vector3 local) =>
            index.TryGetValue(GridAt(local), out int i) && cells[i].alive ? i : -1;

        public List<int> Strike(Vector3 point, float radius, StrikeMode mode = StrikeMode.Sphere)
        {
            Hits++;
            float r2 = radius * radius;
            var removed = new List<int>();
            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i].alive) continue;
                var d = Position(i) - point;
                float dist = mode switch
                {
                    StrikeMode.Slice => d.x * d.x * 3f + d.y * d.y * .32f + d.z * d.z * .32f,
                    StrikeMode.Beam => d.x * d.x + d.y * d.y * .20f + d.z * d.z * .70f,
                    _ => d.sqrMagnitude
                };
                if (dist <= r2) Remove(i, removed);
            }
            if (removed.Count == 0) return removed;

            // Cubes no longer connected to the table lose their supporting bonds.
            Array.Clear(supported, 0, supported.Length);
            int head = 0, tail = 0;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].alive && cells[i].grid.y == baseY) { supported[i] = true; queue[tail++] = i; }
            while (head < tail)
            {
                var g = cells[queue[head++]].grid;
                foreach (var d in Neighbors)
                {
                    if (index.TryGetValue(g + d, out int n) && cells[n].alive && !supported[n])
                    {
                        supported[n] = true;
                        queue[tail++] = n;
                    }
                }
            }
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].alive && !supported[i]) Remove(i, removed);

            // Clear the final small remnants for the completion animation.
            if (Progress >= ClearThreshold)
                for (int i = 0; i < cells.Length; i++)
                    if (cells[i].alive) Remove(i, removed);
            return removed;
        }

        void Remove(int i, List<int> removed)
        {
            cells[i].alive = false;
            Removed++;
            removed.Add(i);
        }
    }
}
