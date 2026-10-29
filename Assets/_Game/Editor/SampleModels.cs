using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VoxelSmash.Core;

namespace VoxelSmash.EditorTools
{
    // Procedural sample sculptures until the Blender/MagicaVoxel pipeline (Phase 5) brings real art.
    // Four abstract toys plus four pieces of scrap that suit the junkyard backdrop, interleaved.
    public static class SampleModels
    {
        public static readonly Color32[] Palette =
        {
            new(120, 36, 210, 255),  // 0 purple
            new(236, 72, 153, 255),  // 1 pink
            new(250, 204, 21, 255),  // 2 gold
            new(239, 68, 68, 255),   // 3 red
            new(250, 245, 230, 255), // 4 cream
            new(34, 197, 94, 255),   // 5 green
            new(59, 130, 246, 255),  // 6 blue
            new(249, 115, 22, 255),  // 7 orange
            new(71, 85, 105, 255),   // 8 slate
            new(30, 30, 36, 255),    // 9 black
            new(148, 92, 52, 255),   // 10 brown
            new(255, 255, 255, 255), // 11 white
            new(196, 46, 40, 255),   // 12 car red
            new(150, 72, 32, 255),   // 13 rust
            new(104, 50, 26, 255),   // 14 dark rust
            new(206, 212, 218, 255), // 15 chrome
            new(122, 186, 222, 255), // 16 glass
            new(52, 78, 104, 255),   // 17 dark glass
            new(42, 42, 46, 255),    // 18 tyre
            new(236, 236, 228, 255), // 19 appliance white
            new(176, 116, 66, 255),  // 20 wood
            new(112, 70, 38, 255),   // 21 dark wood
            new(64, 88, 82, 255),    // 22 screen
            new(132, 206, 176, 255), // 23 screen glow
            new(38, 98, 180, 255),   // 24 drum blue
            new(232, 180, 24, 255),  // 25 hazard yellow
            new(126, 132, 140, 255), // 26 steel
            new(255, 236, 140, 255)  // 27 lamp
        };

        const byte Purple = 0, Pink = 1, Gold = 2, Red = 3, Cream = 4, Green = 5, Blue = 6, Orange = 7, Slate = 8, Black = 9,
            Brown = 10, White = 11, CarRed = 12, Rust = 13, DarkRust = 14, Chrome = 15, Glass = 16, DarkGlass = 17, Tyre = 18,
            Appliance = 19, Wood = 20, DarkWood = 21, Screen = 22, ScreenGlow = 23, DrumBlue = 24, Hazard = 25, Steel = 26, Lamp = 27;

        sealed class Builder
        {
            readonly Dictionary<Vector3Int, byte> cells = new();

            public void Set(int x, int y, int z, byte c) => cells[new Vector3Int(x, y, z)] = c;

            public void Box(int x0, int y0, int z0, int sx, int sy, int sz, byte c)
            {
                for (int x = x0; x < x0 + sx; x++)
                for (int y = y0; y < y0 + sy; y++)
                for (int z = z0; z < z0 + sz; z++) Set(x, y, z, c);
            }

            // Recolours existing cells inside a box (decals, stripes, panels); never adds cells.
            public void Paint(int x0, int y0, int z0, int sx, int sy, int sz, System.Func<int, int, int, byte?> color)
            {
                for (int x = x0; x < x0 + sx; x++)
                for (int y = y0; y < y0 + sy; y++)
                for (int z = z0; z < z0 + sz; z++)
                {
                    var key = new Vector3Int(x, y, z);
                    if (cells.ContainsKey(key) && color(x, y, z) is byte c) cells[key] = c;
                }
            }

            public void Cylinder(int cx, int cz, float r, int y0, int height, byte c) =>
                Cylinder(cx, cz, r, y0, height, (_, _, _) => c);

            public void Cylinder(int cx, int cz, float r, int y0, int height, System.Func<int, int, int, byte> color)
            {
                int ri = Mathf.CeilToInt(r);
                for (int x = -ri; x <= ri; x++)
                for (int z = -ri; z <= ri; z++)
                    if (x * x + z * z <= r * r)
                        for (int y = y0; y < y0 + height; y++) Set(cx + x, y, cz + z, color(x, y - y0, z));
            }

            // A wheel or disc facing along z.
            public void Disc(int cx, int cy, int z0, int depth, float r, System.Func<float, byte> color)
            {
                int ri = Mathf.CeilToInt(r);
                for (int x = -ri; x <= ri; x++)
                for (int y = -ri; y <= ri; y++)
                {
                    float d = Mathf.Sqrt(x * x + y * y);
                    if (d > r || cy + y < 0) continue;
                    for (int z = z0; z < z0 + depth; z++) Set(cx + x, cy + y, z, color(d));
                }
            }

            public void Dome(int cx, int cy, int cz, float r, System.Func<int, int, int, byte> color)
            {
                int ri = Mathf.CeilToInt(r);
                for (int x = -ri; x <= ri; x++)
                for (int y = 0; y <= ri; y++)
                for (int z = -ri; z <= ri; z++)
                    if (x * x + y * y + z * z <= r * r) Set(cx + x, cy + y, cz + z, color(x, y, z));
            }

            // Face-connected so the support check keeps every cell attached (diagonal steps get a filler).
            public void Line(Vector3 from, Vector3 to, byte c)
            {
                int steps = Mathf.CeilToInt((to - from).magnitude * 2f);
                var previous = Vector3Int.RoundToInt(from);
                for (int i = 0; i <= steps; i++)
                {
                    var p = Vector3Int.RoundToInt(Vector3.Lerp(from, to, i / (float)steps));
                    if (p.x != previous.x && p.y != previous.y) Set(previous.x, p.y, p.z, c);
                    Set(p.x, p.y, p.z, c);
                    previous = p;
                }
            }

            public VoxelModel Finish(string name, int seed)
            {
                // Centre on x/z and rest the lowest layer on y = 0.
                int minX = cells.Keys.Min(k => k.x), maxX = cells.Keys.Max(k => k.x);
                int minZ = cells.Keys.Min(k => k.z), maxZ = cells.Keys.Max(k => k.z), minY = cells.Keys.Min(k => k.y);
                var offset = new Vector3Int(-(minX + maxX) / 2, -minY, -(minZ + maxZ) / 2);
                var random = new System.Random(seed);
                var model = ScriptableObject.CreateInstance<VoxelModel>();
                model.name = name;
                model.displayName = name;
                model.voxelSize = .1f;
                model.palette = Palette;
                // Per-voxel brightness jitter keeps flat faces from looking like plastic.
                model.voxels = cells.OrderBy(k => k.Key.y).ThenBy(k => k.Key.x).ThenBy(k => k.Key.z)
                    .Select(k => { var g = k.Key + offset; return new Voxel(g.x, g.y, g.z, k.Value, .91f + (float)random.NextDouble() * .13f); })
                    .ToArray();
                return model;
            }
        }

        // Cheap deterministic per-cell noise in [0, 1).
        static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ seed * 2654435761u);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xffffff) / 16777216f;
            }
        }

        // Rust patches: low-frequency blotches so stains cluster instead of speckling.
        static byte? Rusty(int x, int y, int z, int seed, float amount)
        {
            float blotch = Hash(x / 3, y / 3, z / 3, seed);
            if (blotch > amount) return null;
            return Hash(x, y, z, seed + 1) < .55f ? Rust : DarkRust;
        }

        public static VoxelModel[] All() => new[]
        {
            CandyTower(), RustyCar(), Mushroom(), RetroTv(), TwinArch(), OldFridge(), Robot(), OilDrums()
        };

        // Echoes the reference's first level: purple feet, striped candy legs, a block body and a gold accent.
        static VoxelModel CandyTower()
        {
            var b = new Builder();
            b.Box(-9, 0, -4, 8, 4, 8, Purple);
            b.Box(2, 0, -4, 8, 4, 8, Purple);
            b.Box(-7, 4, -2, 4, 10, 4, Pink);
            b.Box(4, 4, -2, 4, 10, 4, Pink);
            b.Paint(-7, 4, -2, 15, 10, 4, (x, y, z) => (y + x + z) % 4 < 2 ? Cream : null);
            b.Box(-9, 14, -5, 19, 6, 10, Purple);
            b.Box(-1, 20, -4, 9, 8, 8, Purple);
            for (int x = -1; x < 8; x += 2) b.Box(x, 28, -4, 1, 2, 8, Purple);
            b.Box(4, 22, 3, 4, 5, 1, Gold);
            b.Box(-8, 16, 4, 17, 1, 1, Gold);
            return b.Finish("Candy Tower", 1);
        }

        // A rusted sedan, nose toward +x; wheels carry the body so knocking them out drops it.
        static VoxelModel RustyCar()
        {
            var b = new Builder();
            b.Box(-17, 3, -7, 34, 6, 14, CarRed);
            b.Box(-17, 9, -7, 9, 1, 14, CarRed);           // boot lid
            b.Box(8, 9, -7, 9, 1, 14, CarRed);             // bonnet
            b.Box(-8, 9, -6, 16, 7, 12, CarRed);           // cabin
            b.Paint(-17, 3, -7, 34, 13, 14, (x, y, z) => Rusty(x, y, z, 7, .26f));
            b.Paint(-7, 10, -7, 6, 5, 14, (x, y, z) => z == -6 || z == 5 ? Glass : null);
            b.Paint(0, 10, -7, 7, 5, 14, (x, y, z) => z == -6 || z == 5 ? Glass : null);
            b.Paint(7, 10, -5, 1, 5, 10, (_, y, _) => y > 13 ? DarkGlass : Glass);
            b.Paint(-8, 10, -5, 1, 5, 10, (_, _, _) => DarkGlass);
            b.Box(17, 3, -7, 1, 2, 14, Chrome);            // bumpers
            b.Box(-18, 3, -7, 1, 2, 14, Chrome);
            b.Paint(16, 5, -4, 1, 3, 8, (_, y, z) => (y + z) % 2 == 0 ? Black : Slate); // grille
            b.Box(17, 6, -6, 1, 2, 3, Lamp);
            b.Box(17, 6, 3, 1, 2, 3, Lamp);
            b.Box(-18, 6, -6, 1, 2, 2, Red);
            b.Box(-18, 6, 4, 1, 2, 2, Red);
            foreach (int wx in new[] { -11, 11 })
            foreach (int wz in new[] { -8, 5 })
                b.Disc(wx, 3, wz, 3, 3.6f, d => d < 1.6f ? Chrome : Tyre);
            return b.Finish("Rusty Car", 5);
        }

        static VoxelModel Mushroom()
        {
            var b = new Builder();
            b.Cylinder(0, 0, 9.5f, 0, 1, Green);
            b.Cylinder(0, 0, 3.6f, 1, 10, (x, y, z) => y < 2 ? Cream : Hash(x, y, z, 4) < .15f ? Brown : Cream);
            b.Box(-1, 1, -4, 3, 4, 1, Brown);
            b.Dome(0, 11, 0, 9f, (x, y, z) => (x * 7 + y * 3 + z * 5) % 11 == 0 || (x * x + z * z) % 23 == 0 ? White : Red);
            return b.Finish("Spotted Mushroom", 2);
        }

        // A wooden-cabinet television on short legs with rabbit-ear antennas; the screen faces the camera.
        static VoxelModel RetroTv()
        {
            var b = new Builder();
            foreach (int x in new[] { -10, 8 })
            foreach (int z in new[] { -6, 4 })
                b.Box(x, 0, z, 2, 3, 2, DarkWood);
            b.Box(-11, 3, -7, 22, 17, 14, Wood);
            b.Paint(-11, 3, -7, 22, 17, 14, (x, y, z) => y % 3 == 0 && Hash(x, y, z, 11) < .6f ? DarkWood : null);
            b.Paint(-9, 6, 6, 13, 11, 1, (x, y, _) =>
                x == -9 || x == 3 || y == 6 || y == 16 ? Black
                : x - y == -12 || x - y == -11 ? ScreenGlow
                : Screen);
            b.Box(5, 13, 6, 4, 4, 1, Steel);
            b.Box(6, 14, 7, 2, 2, 1, Chrome);               // channel knob
            b.Box(6, 8, 7, 2, 2, 1, Chrome);                // volume knob
            b.Paint(5, 5, 6, 4, 6, 1, (x, y, _) => (x + y) % 2 == 0 ? Black : Slate);  // speaker grille
            b.Box(-2, 20, -2, 4, 1, 3, Slate);
            b.Line(new Vector3(-1, 21, 0), new Vector3(-8, 30, 0), Steel);
            b.Line(new Vector3(1, 21, 0), new Vector3(9, 29, 0), Steel);
            b.Box(-9, 30, -1, 2, 2, 2, Chrome);
            b.Box(9, 29, -1, 2, 2, 2, Chrome);
            return b.Finish("Retro TV", 6);
        }

        // Two pillars sharing a beam: cut one and the beam still stands, cut both and it all drops.
        static VoxelModel TwinArch()
        {
            var b = new Builder();
            b.Box(-9, 0, -3, 4, 14, 6, Blue);
            b.Box(5, 0, -3, 4, 14, 6, Blue);
            b.Box(-11, 14, -4, 22, 4, 8, Gold);
            b.Box(-3, 18, -2, 6, 4, 4, Orange);
            for (int x = -8; x <= 8; x++)
            {
                int arch = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, 25f - x * x * .4f)));
                if (x > -5 && x < 5) b.Box(x, 14 - arch, -3, 1, arch, 6, Blue);
            }
            b.Paint(-11, 0, -4, 22, 14, 8, (x, y, z) => (x + y) % 6 == 0 ? Slate : null);
            return b.Finish("Twin Arch", 3);
        }

        // A tall stained refrigerator with a freezer door, chrome handles and a few fridge magnets.
        static VoxelModel OldFridge()
        {
            var b = new Builder();
            b.Box(-6, 0, -5, 2, 2, 2, Black);
            b.Box(4, 0, -5, 2, 2, 2, Black);
            b.Box(-6, 0, 3, 2, 2, 2, Black);
            b.Box(4, 0, 3, 2, 2, 2, Black);
            b.Box(-7, 2, -6, 14, 32, 12, Appliance);
            b.Paint(-7, 2, -6, 14, 32, 12, (x, y, z) => y == 23 && z == 5 ? Slate : null);           // door gap
            b.Paint(-7, 2, -6, 14, 32, 12, (x, y, z) => y < 9 ? Rusty(x, y, z, 21, .45f) : Rusty(x, y, z, 23, .08f));
            b.Box(4, 10, 6, 1, 10, 1, Chrome);
            b.Box(4, 25, 6, 1, 6, 1, Chrome);
            b.Box(-5, 18, 5, 2, 2, 1, Red);
            b.Box(-2, 15, 5, 2, 2, 1, Gold);
            b.Box(-4, 12, 5, 3, 2, 1, Green);
            b.Box(-5, 28, 5, 3, 3, 1, Blue);
            b.Box(-7, 34, -6, 14, 1, 12, Slate);
            return b.Finish("Old Fridge", 7);
        }

        static VoxelModel Robot()
        {
            var b = new Builder();
            b.Box(-5, 0, -3, 4, 3, 6, Black);
            b.Box(2, 0, -3, 4, 3, 6, Black);
            b.Box(-4, 3, -2, 3, 7, 4, Slate);
            b.Box(3, 3, -2, 3, 7, 4, Slate);
            b.Box(-6, 10, -4, 13, 11, 8, Blue);
            b.Box(-3, 14, 3, 7, 4, 1, Gold);
            b.Paint(-3, 14, 3, 7, 4, 1, (x, y, _) => (x + y) % 3 == 0 ? Orange : null);
            b.Paint(-6, 10, -4, 13, 11, 8, (x, y, z) => (x == -6 || x == 6) && y % 3 == 1 && z % 3 == 0 ? Chrome : null);
            b.Box(-10, 12, -2, 4, 3, 4, Slate);
            b.Box(7, 12, -2, 4, 3, 4, Slate);
            b.Box(-10, 5, -2, 3, 7, 3, Orange);
            b.Box(8, 5, -2, 3, 7, 3, Orange);
            b.Box(-4, 21, -3, 9, 7, 7, White);
            b.Box(-2, 24, 3, 2, 2, 1, Black);
            b.Box(2, 24, 3, 2, 2, 1, Black);
            b.Set(-2, 25, 3, ScreenGlow);
            b.Set(2, 25, 3, ScreenGlow);
            b.Box(-1, 22, 3, 3, 1, 1, Slate);
            b.Box(0, 28, 0, 1, 3, 1, Steel);
            b.Box(-1, 31, -1, 3, 2, 3, Red);
            return b.Finish("Tin Robot", 4);
        }

        // Steel drums stacked on a pallet: three below, two above resting in the gaps.
        static VoxelModel OilDrums()
        {
            var b = new Builder();
            for (int x = -16; x <= 16; x++)
            {
                if ((x + 16) % 4 == 3) continue;
                b.Box(x, 0, -6, 1, 1, 12, Wood);
            }
            b.Box(-16, 1, -6, 33, 1, 12, DarkWood);
            byte[] colors = { DrumBlue, Red, Hazard, Green, DrumBlue };
            (int x, int y)[] drums = { (-10, 2), (0, 2), (10, 2), (-5, 14), (5, 14) };
            for (int i = 0; i < drums.Length; i++)
            {
                var (dx, dy) = drums[i];
                byte body = colors[i];
                int seed = 40 + i;
                b.Cylinder(dx, 0, 4.6f, dy, 12, (x, y, z) =>
                    y == 0 || y == 11 ? Steel
                    : y == 4 || y == 8 ? (byte)(body == Hazard ? Black : Steel)
                    : Rusty(dx + x, dy + y, z, seed, .14f) ?? body);
                b.Set(dx + 2, dy + 12, 0, Steel);                // bung
            }
            return b.Finish("Oil Drums", 8);
        }
    }
}
