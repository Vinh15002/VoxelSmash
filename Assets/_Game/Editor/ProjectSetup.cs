using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VoxelSmash.Core;
using VoxelSmash.Game;

namespace VoxelSmash.EditorTools
{
    // Rebuilds generated content and the game scene. Safe to re-run: assets are overwritten in place.
    // Batch: Unity -batchmode -quit -executeMethod VoxelSmash.EditorTools.ProjectSetup.Run
    public static class ProjectSetup
    {
        const string Root = "Assets/_Game";
        const string ScenePath = Root + "/Scenes/Game.unity";
        const string ApkPath = "Builds/VoxelSmash.apk", WebGLPath = "Builds/WebGL";

        [MenuItem("Voxel Smash/Rebuild Content And Scene")]
        public static void Run()
        {
            ConfigurePlayer();
            EnsureLayers(GameController.SculptureLayer, GameController.DebrisLayer, "Table", GameController.PreviewLayer);
            CreateLevels();
            CreateWeapons();
            WeaponArtImporter.Run();
            CreateMaterials();
            AssetDatabase.SaveAssets();
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[VoxelSmash] Setup complete");
        }

        [MenuItem("Voxel Smash/Build Android APK")]
        public static void BuildAndroid()
        {
            ConfigurePlayer();
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath)!);
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            Debug.Log($"[VoxelSmash] Build {report.summary.result}: {report.summary.totalSize / 1048576f:F1} MB");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        // Release WebGL build for CrazyGames: Brotli-compressed, their template (needed for the SDK on
        // localhost) and explicitly thrown exceptions: with none at all, any throw, even a handled one,
        // halts the page.
        // Upload the four files in Build/ directly: CrazyGames rejects archives, and matches the
        // loader, framework, data and wasm by their shared name, so files are not named as hashes.
        // Batch: Unity -batchmode -quit -buildTarget WebGL -executeMethod VoxelSmash.EditorTools.ProjectSetup.BuildWebGL
        [MenuItem("Voxel Smash/Build WebGL (CrazyGames)")]
        public static void BuildWebGL() => BuildWebGL(false);

        // Development build with full exception stack traces, for diagnosing browser-only failures.
        [MenuItem("Voxel Smash/Build WebGL (debug)")]
        public static void BuildWebGLDebug() => BuildWebGL(true);

        static void BuildWebGL(bool debug)
        {
            ConfigurePlayer();
            PlayerSettings.WebGL.template = "PROJECT:CrazyGames";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.exceptionSupport = debug ? WebGLExceptionSupport.FullWithStacktrace : WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            PlayerSettings.runInBackground = false;
            string path = debug ? WebGLPath + "Debug" : WebGLPath;
            if (Directory.Exists(path)) Directory.Delete(path, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.WebGL,
                options = debug ? BuildOptions.Development : BuildOptions.None
            });
            Debug.Log($"[VoxelSmash] WebGL build {report.summary.result}: {report.summary.totalSize / 1048576f:F1} MB");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        // Windows player used only to record store media (MediaCapture): run it with -capture <dir> ...
        [MenuItem("Voxel Smash/Build Media Capture Player")]
        public static void BuildCapture()
        {
            ConfigurePlayer();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Capture/ScrapyardSmash.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            Debug.Log($"[VoxelSmash] Capture build {report.summary.result}");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "VoxelSmash";
            PlayerSettings.productName = GameInfo.Name;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.voxelsmash.prototype");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            // Unity 6.3 no longer builds x86_64 for Android; LDPlayer runs ARM64 through its translation layer.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        // Put each named layer in the first free user slot (8+) unless it already exists.
        static void EnsureLayers(params string[] names)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            foreach (var name in names)
            {
                if (LayerMask.NameToLayer(name) >= 0) continue;
                for (int i = 8; i < layers.arraySize; i++)
                {
                    var slot = layers.GetArrayElementAtIndex(i);
                    if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                    slot.stringValue = name;
                    break;
                }
            }
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        const string WeaponDir = Root + "/Data/Weapons";

        // Numbers from the reference's WEAPONS table; durations default to .29 s where it had none. The axe
        // was dropped; bomb and poison fall onto the target, so they take longer.
        static readonly (string name, WeaponType type, float radius, float power, float cooldown, float duration, int unlock, StrikeMode mode, WeaponSound sound)[] WeaponTable =
        {
            ("Boxing glove", WeaponType.Glove, .43f, 4.8f, .27f, .29f, 0, StrikeMode.Sphere, WeaponSound.Glove),
            ("Knife", WeaponType.Knife, .48f, 4.0f, .62f, .56f, 0, StrikeMode.Slice, WeaponSound.Slice),
            ("Shuriken", WeaponType.Star, .58f, 6.2f, .32f, .29f, 0, StrikeMode.Sphere, WeaponSound.Slice),
            ("Bomb", WeaponType.Bomb, 1.05f, 12f, .75f, .70f, 5, StrikeMode.Sphere, WeaponSound.Explosion),
            ("Hammer", WeaponType.Hammer, .52f, 5.7f, .76f, .68f, 0, StrikeMode.Sphere, WeaponSound.Hammer),
            ("Mace", WeaponType.Mace, .71f, 7.6f, .44f, .29f, 7, StrikeMode.Sphere, WeaponSound.Hammer),
            ("Lightning", WeaponType.Lightning, .97f, 10f, .68f, .46f, 8, StrikeMode.Beam, WeaponSound.Zap),
            ("Bullet burst", WeaponType.Bullets, .35f, 5.6f, .64f, .40f, 9, StrikeMode.Sphere, WeaponSound.Shot),
            ("Poison", WeaponType.Poison, .58f, 4.4f, .95f, .70f, 10, StrikeMode.Sphere, WeaponSound.Poison),
            ("Wrecking ball", WeaponType.Chain, .88f, 9f, .95f, .78f, 11, StrikeMode.Sphere, WeaponSound.Hammer),
            ("Rocket", WeaponType.Rocket, 1.18f, 12f, 1.0f, .55f, 12, StrikeMode.Sphere, WeaponSound.Explosion)
        };

        static void CreateWeapons()
        {
            Directory.CreateDirectory(WeaponDir);
            for (int i = 0; i < WeaponTable.Length; i++)
            {
                var row = WeaponTable[i];
                var w = ScriptableObject.CreateInstance<WeaponDef>();
                w.displayName = row.name;
                w.type = row.type;
                w.radius = row.radius;
                w.power = row.power;
                w.cooldown = row.cooldown;
                w.duration = row.duration;
                w.unlockLevel = row.unlock;
                w.mode = row.mode;
                w.sound = row.sound;
                w.strikeCost = StrikeCost(row.type);
                w.shake = w.Explosive ? .24f : .065f;
                w.pattern = row.type switch
                {
                    WeaponType.Bullets => new[] { new AttackStep(0f, 0f, 0f, 1f), new AttackStep(.10f, -.16f, .12f, .85f), new AttackStep(.20f, .16f, -.12f, .85f) },
                    WeaponType.Poison => new[] { new AttackStep(0f, 0f, 0f, 1f), new AttackStep(.30f, 0f, 0f, .80f), new AttackStep(.60f, 0f, -.10f, .80f) },
                    _ => new[] { new AttackStep(0f, 0f, 0f, 1f) }
                };
                Save(w, WeaponPath(i, row.name));
            }
        }

        // Gold per strike: about what a carelessly aimed strike earns on average (measured over the
        // campaign: a level pays ~1 coin per 8 cubes), slightly more, so only good aim grows the purse.
        static int StrikeCost(WeaponType type) => type switch
        {
            WeaponType.Glove => 20,
            WeaponType.Knife => 30,
            WeaponType.Star => 38,
            WeaponType.Hammer => 28,
            WeaponType.Mace => 55,
            WeaponType.Bomb => 100,
            WeaponType.Lightning => 130,
            WeaponType.Bullets => 16,
            WeaponType.Poison => 32,
            WeaponType.Chain => 85,
            WeaponType.Rocket => 130,
            _ => 30
        };

        static string WeaponPath(int index, string name) => $"{WeaponDir}/{index + 1:00}_{name.Replace(" ", "")}.asset";

        // The 200-level campaign: the scrapyard's own procedural models plus the imported PixelLand
        // set. Regular levels open with two scrapyard pieces, then mix one in every 12 PixelLand models
        // (those ramp from small to large); bosses start with the scrapyard vehicles.
        const int CampaignLength = 200, ScrapEvery = 12;

        static LevelList CreateLevels()
        {
            string dir = Root + "/Data/Models";
            Directory.CreateDirectory(dir);
            var models = SampleModels.All();
            var paths = new System.Collections.Generic.HashSet<string>(PixelLandImporter.AssetPaths());
            for (int i = 0; i < models.Length; i++)
            {
                string path = $"{dir}/{i + 1:00}_{models[i].name.Replace(" ", "")}.asset";
                paths.Add(path);
                models[i] = Save(models[i], path);
            }
            var bosses = SampleModels.Bosses();
            for (int i = 0; i < bosses.Length; i++)
            {
                string path = $"{dir}/Boss{i + 1:00}_{bosses[i].name.Replace(" ", "")}.asset";
                paths.Add(path);
                bosses[i] = Save(bosses[i], path);
            }
            var (pixelRegular, pixelBosses) = PixelLandImporter.Load();
            var regular = new System.Collections.Generic.List<VoxelModel> { models[0], models[1] };
            int scrap = 2;
            for (int i = 0; i < pixelRegular.Length; i++)
            {
                regular.Add(pixelRegular[i]);
                if ((i + 1) % ScrapEvery == 0 && scrap < models.Length) regular.Add(models[scrap++]);
            }
            while (scrap < models.Length) regular.Add(models[scrap++]);
            var list = ScriptableObject.CreateInstance<LevelList>();
            list.levels = regular.ToArray();
            list.bosses = bosses.Concat(pixelBosses).ToArray();
            list.campaignLength = CampaignLength;
            Debug.Log($"[VoxelSmash] Campaign: {list.levels.Length} regular + {list.bosses.Length} boss models for {CampaignLength} levels");
            // Drop models left over from an earlier ordering so the folder matches the level list.
            foreach (var guid in AssetDatabase.FindAssets("t:VoxelModel", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!paths.Contains(path)) AssetDatabase.DeleteAsset(path);
            }
            return Save(list, Root + "/Data/Levels.asset");
        }

        // Overwrite by copying into an existing asset so GUIDs (and scene references) stay stable.
        static T Save<T>(T asset, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }
            EditorUtility.CopySerialized(asset, existing);
            existing.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static void CreateMaterials()
        {
            string dir = Root + "/Materials";
            Directory.CreateDirectory(dir);
            AssetDatabase.DeleteAsset(dir + "/Table.mat"); // the junkyard ground replaced the plain table
            var shader = Shader.Find("VoxelSmash/VoxelLit");
            // Edge widths are in each mesh's uv units: metres for sculpture and junkyard faces,
            // the unit cube for instanced fragments.
            // Sculptures and fragments use PixelLand's rounded cube-face texture (its toon "base" look).
            var face = FaceTexture();
            var voxel = Save(new Material(shader), dir + "/Voxel.mat");
            Edges(voxel, .009f, .12f);
            Face(voxel, face);
            var fragmentMat = new Material(shader) { enableInstancing = true };
            var fragments = Save(fragmentMat, dir + "/VoxelFragments.mat");
            fragments.enableInstancing = true;
            Edges(fragments, .09f, .12f);
            Face(fragments, face);
            var junkyard = Save(new Material(shader), dir + "/Junkyard.mat");
            Edges(junkyard, .035f, .22f);
            var sky = Save(new Material(Shader.Find("Skybox/Procedural")), dir + "/Sky.mat");
            sky.SetFloat("_SunSize", .03f);
            sky.SetFloat("_AtmosphereThickness", .75f);
            sky.SetColor("_SkyTint", new Color32(0x9c, 0xb8, 0xd8, 0xff));
            sky.SetColor("_GroundColor", new Color32(0x7a, 0x70, 0x64, 0xff));
            sky.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(sky);
            // Effects output premultiplied colour: "alpha" blends One/OneMinusSrcAlpha, "additive" One/One.
            var fx = Shader.Find("VoxelSmash/FxUnlit");
            var fxAlpha = Save(new Material(fx), dir + "/FxAlpha.mat");
            fxAlpha.SetFloat("_SrcBlend", (float)BlendMode.One);
            fxAlpha.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            var fxAdditive = Save(new Material(fx), dir + "/FxAdditive.mat");
            fxAdditive.SetFloat("_SrcBlend", (float)BlendMode.One);
            fxAdditive.SetFloat("_DstBlend", (float)BlendMode.One);
            EditorUtility.SetDirty(fxAlpha);
            EditorUtility.SetDirty(fxAdditive);
        }

        const string FaceTexturePath = Root + "/Textures/VoxelFace.png";

        static Texture2D FaceTexture()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(FaceTexturePath);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(FaceTexturePath);
        }

        static void Face(Material m, Texture2D texture)
        {
            m.SetTexture("_FaceMap", texture);
            m.SetFloat("_FaceStrength", 1f);
            EditorUtility.SetDirty(m);
        }

        static void Edges(Material m, float width, float dark)
        {
            m.SetFloat("_EdgeWidth", width);
            m.SetFloat("_EdgeDark", dark);
            EditorUtility.SetDirty(m);
        }

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            // Meshes don't copy cleanly onto an existing asset; replace it (only this scene refers to it).
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load only after NewScene: it unloads unreferenced assets, leaving earlier handles destroyed.
            var levels = AssetDatabase.LoadAssetAtPath<LevelList>(Root + "/Data/Levels.asset");
            var voxelPalette = AssetDatabase.FindAssets("t:Material", new[] { Root + "/Materials/PixelLand" })
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            var fragments = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/VoxelFragments.mat");
            var junkyardMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Junkyard.mat");
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Sky.mat");
            var fxAlpha = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/FxAlpha.mat");
            var fxAdditive = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/FxAdditive.mat");
            var weapons = new WeaponDef[WeaponTable.Length];
            for (int i = 0; i < weapons.Length; i++) weapons[i] = AssetDatabase.LoadAssetAtPath<WeaponDef>(WeaponPath(i, WeaponTable[i].name));

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = .1f;
            var rig = camGo.AddComponent<CameraRig>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color32(0xff, 0xf0, 0xd3, 0xff);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.position = new Vector3(-3, 7, 6);
            sun.transform.LookAt(new Vector3(0, 1.5f, 0));
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f, .58f, .62f);
            RenderSettings.skybox = skyMat;
            // Haze matched to the sky's horizon pushes the far yard back.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color32(0xc4, 0xcf, 0xd8, 0xff);
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 62f;

            // Invisible floor that debris lands on; taps ignore it because they raycast the Sculpture layer only.
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.layer = LayerMask.NameToLayer("Table");
            table.transform.position = new Vector3(0, Junkyard.GroundY - .12f, 0);
            table.transform.localScale = new Vector3(88, .24f, 88);
            Object.DestroyImmediate(table.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(table.GetComponent<MeshFilter>());

            var yard = new GameObject("Junkyard", typeof(MeshFilter), typeof(MeshRenderer)) { isStatic = true };
            yard.GetComponent<MeshFilter>().sharedMesh = SaveMesh(Junkyard.Build(), Root + "/Data/Junkyard.asset");
            var yardRenderer = yard.GetComponent<MeshRenderer>();
            yardRenderer.sharedMaterial = junkyardMat;
            yardRenderer.shadowCastingMode = ShadowCastingMode.Off;
            yardRenderer.receiveShadows = true;

            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetAsLastSibling();

            var hud = new GameObject("HUD").AddComponent<HudView>();
            var game = new GameObject("Game").AddComponent<GameController>();
            var so = new SerializedObject(game);
            so.FindProperty("levels").objectReferenceValue = levels;
            var palette = so.FindProperty("voxelPalette");
            palette.arraySize = voxelPalette.Length;
            for (int i = 0; i < voxelPalette.Length; i++) palette.GetArrayElementAtIndex(i).objectReferenceValue = voxelPalette[i];
            so.FindProperty("fragmentMaterial").objectReferenceValue = fragments;
            so.FindProperty("cameraRig").objectReferenceValue = rig;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("fxAlphaMaterial").objectReferenceValue = fxAlpha;
            so.FindProperty("fxAdditiveMaterial").objectReferenceValue = fxAdditive;
            var weaponsProp = so.FindProperty("weapons");
            weaponsProp.arraySize = weapons.Length;
            for (int i = 0; i < weapons.Length; i++) weaponsProp.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!levels || voxelPalette.Length == 0 || !fragments || !junkyardMat || !skyMat || !fxAlpha || !fxAdditive || System.Array.Exists(weapons, w => !w)) throw new System.InvalidOperationException("[VoxelSmash] Generated assets missing; scene not wired");

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
