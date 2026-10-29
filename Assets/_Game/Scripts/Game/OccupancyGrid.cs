using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VoxelSmash.Core;

namespace VoxelSmash.Game
{
    // Dense alive/dead copy of the sculpture grid that Burst jobs can read for cheap debris collisions.
    public sealed class OccupancyGrid : IDisposable
    {
        public NativeArray<byte> cells;
        public int3 min, size;
        public float voxelSize;

        public OccupancyGrid(Sculpture sculpture)
        {
            voxelSize = sculpture.size;
            int3 lo = int.MaxValue, hi = int.MinValue;
            foreach (var c in sculpture.cells)
            {
                var g = new int3(c.grid.x, c.grid.y, c.grid.z);
                lo = math.min(lo, g);
                hi = math.max(hi, g);
            }
            if (sculpture.Total == 0) lo = hi = 0;
            min = lo;
            size = hi - lo + 1;
            cells = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            for (int i = 0; i < sculpture.Total; i++) Set(sculpture.cells[i].grid, 1);
        }

        public void Remove(Sculpture sculpture, List<int> removed)
        {
            foreach (int i in removed) Set(sculpture.cells[i].grid, 0);
        }

        void Set(UnityEngine.Vector3Int g, byte value) => cells[Index(new int3(g.x, g.y, g.z) - min, size)] = value;

        public static int Index(int3 p, int3 size) => p.x + p.y * size.x + p.z * size.x * size.y;

        public void Dispose()
        {
            if (cells.IsCreated) cells.Dispose();
        }
    }
}
