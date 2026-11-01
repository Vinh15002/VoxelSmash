using System;
using UnityEngine;

namespace VoxelSmash.Core
{
    [Serializable]
    public struct Voxel
    {
        public short x, y, z;
        public byte color;
        // Per-voxel brightness multiplier (~0.9-1.05) that breaks up flat colour areas.
        public float shade;

        public Voxel(int x, int y, int z, byte color, float shade = 1f)
        {
            this.x = (short)x; this.y = (short)y; this.z = (short)z;
            this.color = color; this.shade = shade;
        }

        public Vector3Int Grid => new Vector3Int(x, y, z);
    }

    // One smashable sculpture. Grid y = 0 is the layer resting on the table; x/z are centred on 0.
    // Voxels are stored packed (8 bytes each: x, y, z as int16, colour, quantised shade) so two hundred
    // models stay small on disk; they are unpacked once on first use.
    [CreateAssetMenu(menuName = "Voxel Smash/Voxel Model")]
    public sealed class VoxelModel : ScriptableObject
    {
        const int Stride = 8;
        const float ShadeMin = .8f, ShadeRange = .4f;

        public string displayName;
        public float voxelSize = .1f;
        public Color32[] palette = Array.Empty<Color32>();
        [SerializeField, HideInInspector] byte[] packed = Array.Empty<byte>();
        [NonSerialized] Voxel[] unpacked;

        public int VoxelCount => packed.Length / Stride;

        public Voxel[] voxels
        {
            get => unpacked ??= Unpack(packed);
            set
            {
                packed = Pack(value);
                unpacked = null;
            }
        }

        // Data can change under the cache (editor copies, re-imports): unpack again next time.
        void OnValidate() => unpacked = null;

        static byte[] Pack(Voxel[] voxels)
        {
            var bytes = new byte[voxels.Length * Stride];
            for (int i = 0; i < voxels.Length; i++)
            {
                var v = voxels[i];
                int o = i * Stride;
                BitConverter.TryWriteBytes(new Span<byte>(bytes, o, 2), v.x);
                BitConverter.TryWriteBytes(new Span<byte>(bytes, o + 2, 2), v.y);
                BitConverter.TryWriteBytes(new Span<byte>(bytes, o + 4, 2), v.z);
                bytes[o + 6] = v.color;
                bytes[o + 7] = (byte)Mathf.RoundToInt(Mathf.Clamp01((v.shade - ShadeMin) / ShadeRange) * 255f);
            }
            return bytes;
        }

        static Voxel[] Unpack(byte[] bytes)
        {
            var voxels = new Voxel[bytes.Length / Stride];
            for (int i = 0; i < voxels.Length; i++)
            {
                int o = i * Stride;
                voxels[i] = new Voxel(BitConverter.ToInt16(bytes, o), BitConverter.ToInt16(bytes, o + 2), BitConverter.ToInt16(bytes, o + 4),
                    bytes[o + 6], ShadeMin + bytes[o + 7] / 255f * ShadeRange);
            }
            return voxels;
        }
    }
}
