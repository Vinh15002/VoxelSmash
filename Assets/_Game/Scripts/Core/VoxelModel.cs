using System;
using UnityEngine;

namespace VoxelSmash.Core
{
    [Serializable]
    public struct Voxel
    {
        public short x, y, z;
        public byte color;
        // Per-voxel brightness multiplier (~0.94-1.04) that breaks up flat colour areas.
        public float shade;

        public Voxel(int x, int y, int z, byte color, float shade = 1f)
        {
            this.x = (short)x; this.y = (short)y; this.z = (short)z;
            this.color = color; this.shade = shade;
        }

        public Vector3Int Grid => new Vector3Int(x, y, z);
    }

    // One smashable sculpture. Grid y = 0 is the layer resting on the table; x/z are centred on 0.
    [CreateAssetMenu(menuName = "Voxel Smash/Voxel Model")]
    public sealed class VoxelModel : ScriptableObject
    {
        public string displayName;
        public float voxelSize = .1f;
        public Color32[] palette = Array.Empty<Color32>();
        public Voxel[] voxels = Array.Empty<Voxel>();
    }
}
