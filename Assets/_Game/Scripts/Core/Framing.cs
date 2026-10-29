using UnityEngine;

namespace VoxelSmash.Core
{
    public struct TargetFrame
    {
        public float height, width, targetY;
    }

    public static class Framing
    {
        public const float StartYaw = .40f;

        // World-space box the camera must fit, measured from the starting yaw as seen by the camera.
        public static TargetFrame For(VoxelModel model)
        {
            if (model == null || model.voxels.Length == 0) return new TargetFrame { height = 4.6f, width = 3.55f, targetY = 1.9f };
            float s = model.voxelSize, a = StartYaw - Mathf.Atan(.10f), co = Mathf.Cos(a), si = Mathf.Sin(a);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue, maxY = 0f;
            foreach (var v in model.voxels)
            {
                float x = (v.x * co + v.z * si) * s, z = (-v.x * si + v.z * co) * s;
                minX = Mathf.Min(minX, x - s / 2); maxX = Mathf.Max(maxX, x + s / 2);
                minZ = Mathf.Min(minZ, z - s / 2); maxZ = Mathf.Max(maxZ, z + s / 2);
                maxY = Mathf.Max(maxY, (v.y + 1) * s);
            }
            return new TargetFrame
            {
                height = maxY * 1.22f + (maxZ - minZ) * .18f + .18f,
                width = (maxX - minX) * 1.22f + .18f,
                targetY = maxY / 2
            };
        }

        // Debris is pushed beyond this horizontal radius so it never hides the sculpture.
        public static float DebrisClearance(VoxelModel model)
        {
            float radius = 0f;
            foreach (var v in model.voxels)
                radius = Mathf.Max(radius, new Vector2(v.x * model.voxelSize, v.z * model.voxelSize).magnitude);
            return radius + .50f;
        }
    }
}
