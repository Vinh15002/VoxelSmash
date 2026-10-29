using System.Collections.Generic;
using UnityEngine;

namespace VoxelSmash.Core
{
    // Greedy merge of an occupancy block into as few axis-aligned boxes as possible:
    // grow along x, then z, then y. Used for the sculpture's static physics colliders.
    public static class VoxelBoxes
    {
        public struct Box
        {
            public Vector3Int min, size;
        }

        // occupied is indexed x + y*n + z*n*n and is consumed (cleared) by the merge.
        public static void Merge(bool[] occupied, int n, List<Box> result)
        {
            result.Clear();
            for (int z = 0; z < n; z++)
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                if (!occupied[Index(x, y, z, n)]) continue;
                int nx = 1, nz = 1, ny = 1;
                while (x + nx < n && occupied[Index(x + nx, y, z, n)]) nx++;
                while (z + nz < n && Row(occupied, n, x, nx, y, z + nz)) nz++;
                while (y + ny < n && Layer(occupied, n, x, nx, y + ny, z, nz)) ny++;
                for (int dz = 0; dz < nz; dz++)
                for (int dy = 0; dy < ny; dy++)
                for (int dx = 0; dx < nx; dx++)
                    occupied[Index(x + dx, y + dy, z + dz, n)] = false;
                result.Add(new Box { min = new Vector3Int(x, y, z), size = new Vector3Int(nx, ny, nz) });
            }
        }

        public static int Index(int x, int y, int z, int n) => x + y * n + z * n * n;

        static bool Row(bool[] occupied, int n, int x, int nx, int y, int z)
        {
            for (int dx = 0; dx < nx; dx++)
                if (!occupied[Index(x + dx, y, z, n)]) return false;
            return true;
        }

        static bool Layer(bool[] occupied, int n, int x, int nx, int y, int z, int nz)
        {
            for (int dz = 0; dz < nz; dz++)
                if (!Row(occupied, n, x, nx, y, z + dz)) return false;
            return true;
        }
    }
}
