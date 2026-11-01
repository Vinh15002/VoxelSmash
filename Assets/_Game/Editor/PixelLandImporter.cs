using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using VoxelSmash.Core;

namespace VoxelSmash.EditorTools
{
    // Imports the hand-picked PixelLand voxel levels (Editor/Content/PixelLandLevels.json lists file,
    // display name and size) as VoxelModel assets. Orientation matches PixelLand's own loader: json
    // [x,y,z] -> (x, z, y), then root.modelEulerAngles. Voxels not face-connected to the bottom layer
    // are dropped, since Scrapyard Smash drops unsupported cubes on the first strike.
    // The source project path can be overridden with the PIXELLAND_LEVELS environment variable.
    public static class PixelLandImporter
    {
        const string ManifestPath = "Assets/_Game/Editor/Content/PixelLandLevels.json";
        const string OutDir = "Assets/_Game/Data/Models/PixelLand";
        const string DefaultSource = "C:/UnityMainProject/PixelLand02062026/Assets/Resources/Levels";

        // PixelLand's ColorType palette (ColorVisualDatabase.cs); voxel colour ids index into it.
        static readonly string[] Hex =
        {
            "302F2F", "5C5D67", "89909C", "C2CEE1", "F7F7F7", "FEDEB6", "C6795F", "5A1515", "904B43", "A60D1B",
            "ED293A", "FF5F6C", "F46929", "FCBE16", "FFF200", "02676B", "0D9861", "45C337", "B1F153", "112492",
            "3639E5", "509CFF", "53F7FF", "461C83", "7937D6", "BC86F6", "FF54F1", "FCCBDF", "FF98CD", "CDF4FF"
        };

        [Serializable] sealed class Entry { public string file, name; public int voxels; }
        [Serializable] sealed class Manifest { public string source; public Entry[] regular, bosses; }

        static readonly Vector3Int[] Neighbours =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back
        };

        static string SourceDir => Environment.GetEnvironmentVariable("PIXELLAND_LEVELS") ?? DefaultSource;

        [MenuItem("Voxel Smash/Import PixelLand Levels")]
        public static void ImportMenu()
        {
            Load(forceImport: true);
            AssetDatabase.SaveAssets();
        }

        // Regular and boss models in manifest order. Imports when the source project is present (or
        // when forced); otherwise reuses the assets already in the project.
        public static (VoxelModel[] regular, VoxelModel[] bosses) Load(bool forceImport = false)
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            bool canImport = Directory.Exists(SourceDir);
            if (forceImport && !canImport) throw new DirectoryNotFoundException($"PixelLand levels not found at {SourceDir}");
            Directory.CreateDirectory(OutDir);
            return (Models(manifest.regular, "PL", canImport), Models(manifest.bosses, "PLBoss", canImport));
        }

        public static IEnumerable<string> AssetPaths()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            return manifest.regular.Select((e, i) => PathFor("PL", i, e)).Concat(manifest.bosses.Select((e, i) => PathFor("PLBoss", i, e)));
        }

        static string PathFor(string prefix, int index, Entry e) =>
            $"{OutDir}/{prefix}{index + 1:000}_{new string(e.name.Where(char.IsLetterOrDigit).ToArray())}.asset";

        static VoxelModel[] Models(Entry[] entries, string prefix, bool import)
        {
            var models = new VoxelModel[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                string path = PathFor(prefix, i, entries[i]);
                if (import)
                {
                    var model = Convert(Path.Combine(SourceDir, entries[i].file), entries[i].name);
                    var existing = AssetDatabase.LoadAssetAtPath<VoxelModel>(path);
                    if (existing == null) AssetDatabase.CreateAsset(model, path);
                    else
                    {
                        EditorUtility.CopySerialized(model, existing);
                        existing.name = Path.GetFileNameWithoutExtension(path);
                        EditorUtility.SetDirty(existing);
                        model = existing;
                    }
                    models[i] = model;
                }
                else models[i] = AssetDatabase.LoadAssetAtPath<VoxelModel>(path)
                                 ?? throw new FileNotFoundException($"{path} missing and PixelLand source unavailable");
            }
            return models;
        }

        public static VoxelModel Convert(string jsonPath, string displayName)
        {
            var level = (Dictionary<string, object>)Json.Parse(File.ReadAllText(jsonPath));
            var euler = Vector3.zero;
            if (level.TryGetValue("root", out var rootObj) && rootObj is Dictionary<string, object> root &&
                root.TryGetValue("modelEulerAngles", out var e) && e is List<object> angles && angles.Count == 3)
                euler = new Vector3(F(angles[0]), F(angles[1]), F(angles[2]));
            var rotation = Quaternion.Euler(euler);

            var cells = new Dictionary<Vector3Int, byte>();
            foreach (Dictionary<string, object> layer in (List<object>)level["layers"])
            {
                if (!int.TryParse(Str(layer["color"]), out int color) || color < 0 || color >= Hex.Length) continue;
                foreach (List<object> v in (List<object>)layer["voxels"])
                {
                    var p = Vector3Int.RoundToInt(rotation * new Vector3(F(v[0]), F(v[2]), F(v[1])));
                    cells[p] = (byte)color;
                }
            }

            // Keep what stands on the bottom layer.
            int floor = cells.Keys.Min(k => k.y);
            var supported = new HashSet<Vector3Int>(cells.Keys.Where(k => k.y == floor));
            var queue = new Queue<Vector3Int>(supported);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var n in Neighbours)
                    if (cells.ContainsKey(c + n) && supported.Add(c + n)) queue.Enqueue(c + n);
            }

            int minX = supported.Min(k => k.x), maxX = supported.Max(k => k.x);
            int minZ = supported.Min(k => k.z), maxZ = supported.Max(k => k.z);
            var offset = new Vector3Int(-(minX + maxX) / 2, -floor, -(minZ + maxZ) / 2);
            // Seed from the file name with a stable hash so re-imports produce identical shading.
            var random = new System.Random(Path.GetFileName(jsonPath).Aggregate(17, (h, ch) => unchecked(h * 31 + ch)));
            var model = ScriptableObject.CreateInstance<VoxelModel>();
            model.name = displayName;
            model.displayName = displayName;
            model.voxelSize = .1f;
            model.palette = Hex.Select(h => (Color32)new Color(
                int.Parse(h.Substring(0, 2), NumberStyles.HexNumber) / 255f,
                int.Parse(h.Substring(2, 2), NumberStyles.HexNumber) / 255f,
                int.Parse(h.Substring(4, 2), NumberStyles.HexNumber) / 255f)).ToArray();
            model.voxels = supported.OrderBy(k => k.y).ThenBy(k => k.x).ThenBy(k => k.z)
                .Select(k => { var g = k + offset; return new Voxel(g.x, g.y, g.z, cells[k], .91f + (float)random.NextDouble() * .13f); })
                .ToArray();
            return model;
        }

        static float F(object o) => System.Convert.ToSingle(o, CultureInfo.InvariantCulture);
        static string Str(object o) => System.Convert.ToString(o, CultureInfo.InvariantCulture);

        // Minimal JSON reader: objects -> Dictionary, arrays -> List, numbers -> double. Enough for
        // PixelLand levels, which JsonUtility can't read (nested arrays).
        static class Json
        {
            public static object Parse(string text)
            {
                int i = 0;
                return Value(text, ref i);
            }

            static void Skip(string s, ref int i)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            static object Value(string s, ref int i)
            {
                Skip(s, ref i);
                switch (s[i])
                {
                    case '{':
                        var obj = new Dictionary<string, object>();
                        i++;
                        Skip(s, ref i);
                        if (s[i] == '}') { i++; return obj; }
                        while (true)
                        {
                            Skip(s, ref i);
                            string key = (string)Value(s, ref i);
                            Skip(s, ref i);
                            i++; // ':'
                            obj[key] = Value(s, ref i);
                            Skip(s, ref i);
                            if (s[i++] == '}') return obj;
                        }
                    case '[':
                        var list = new List<object>();
                        i++;
                        Skip(s, ref i);
                        if (s[i] == ']') { i++; return list; }
                        while (true)
                        {
                            list.Add(Value(s, ref i));
                            Skip(s, ref i);
                            if (s[i++] == ']') return list;
                        }
                    case '"':
                        var sb = new StringBuilder();
                        i++;
                        while (s[i] != '"')
                        {
                            if (s[i] == '\\') { i++; sb.Append(s[i] == 'n' ? '\n' : s[i]); }
                            else sb.Append(s[i]);
                            i++;
                        }
                        i++;
                        return sb.ToString();
                    case 't': i += 4; return true;
                    case 'f': i += 5; return false;
                    case 'n': i += 4; return null;
                    default:
                        int start = i;
                        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                        return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
                }
            }
        }
    }
}
