using System.Collections.Generic;
using UnityEngine;

namespace VoxelSmash.Game
{
    // PixelLand's cube look: one TCP2 Hybrid material per palette colour (Target_Voxel_*_Base), each
    // with its own shadow tint. That shader has no vertex colours, so a voxel colour uses the PixelLand
    // material whose _BaseColor matches it; any other colour gets a copy of the nearest one, re-tinted.
    // Colours are snapped to 5 bits per channel to keep the set small.
    public sealed class VoxelMaterials
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        readonly Material[] palette;
        readonly Color[] paletteColors;
        readonly Dictionary<int, Material> cache = new();

        public VoxelMaterials(Material[] pixelLandMaterials)
        {
            palette = pixelLandMaterials;
            paletteColors = new Color[palette.Length];
            for (int i = 0; i < palette.Length; i++) paletteColors[i] = palette[i].GetColor(BaseColor);
        }

        // `linear` is a linear-space colour, as the palette and debris use.
        public Material For(Color linear)
        {
            Color32 c = linear.gamma;
            int key = (c.r >> 3) << 10 | (c.g >> 3) << 5 | c.b >> 3;
            if (cache.TryGetValue(key, out var material)) return material;
            Color gamma = linear.gamma;
            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < paletteColors.Length; i++)
            {
                var d = paletteColors[i] - gamma;
                float distance = d.r * d.r + d.g * d.g + d.b * d.b;
                if (distance < best) { best = distance; nearest = i; }
            }
            // Within one 5-bit step of a PixelLand colour: use its material as-is so cubes share it.
            if (best < 3f * (8f / 255f) * (8f / 255f)) material = palette[nearest];
            else
            {
                material = new Material(palette[nearest]) { name = $"{palette[nearest].name} {key:x4}" };
                material.SetColor(BaseColor, gamma);
            }
            cache[key] = material;
            return material;
        }
    }
}
