using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelSmash.Core;
using VoxelSmash.Game;

namespace VoxelSmash.EditorTools
{
    // Turns the imported weapon pack (Assets/WeaponsExport 1/Weapons_With_Effects) into game-ready
    // prefabs and links them to the WeaponDef assets. The pack's own scripts, shaders and some shared
    // materials did not come along, so each copy is stripped to renderers and particle systems: missing
    // scripts, colliders, animators, lights and hidden objects go, built-in particle materials become a
    // URP one, and models are rotated and sized for the screen (Unity axes, +z away from the camera, head up).
    // It also sets each weapon's motion, trail and recorded sounds. Safe to re-run.
    public static class WeaponArtImporter
    {
        const string Source = "Assets/WeaponsExport 1/Weapons_With_Effects/";
        const string ModelDir = "Assets/_Game/Prefabs/Weapons", EffectDir = "Assets/_Game/Prefabs/Effects";
        const string WeaponDir = "Assets/_Game/Data/Weapons";
        const string FallbackParticlePath = "Assets/_Game/Materials/FxParticleDefault.mat";

        const string AudioDir = "Assets/_Game/Audio/Weapons";
        const string Metal = "Bomb/UnityAssets/Assets/Material/MAT_ToolMetal", Wood = "Bomb/UnityAssets/Assets/Material/MAT_ToolOrange";
        const string Acid = "AcidBall/UnityAssets/Assets/Material/MAT_AcidBall";
        const string Destruction = "_SharedDestruction/UnityAssets/Assets/GameObject/VoxelChunkDestructionImpect 1";
        const string Sparks = "BladeSaw/UnityAssets/Assets/GameObject/VFX_SawImpact";
        const string Cylinder = "Cylinder.fbx", Sphere = "Sphere.fbx";

        // `anchor` is where the weapon's origin sits inside its bounds (0..1 per axis); the swing
        // brings the origin to the impact point, so heads sit near it. `trail` is where the streak
        // starts, in the same terms, unless `trailOn` names a part (relative to the model) to follow.
        sealed class Art
        {
            public WeaponType type;
            public string model;
            public Vector3 rotation;
            public float size;
            public Vector3 anchor = new(.5f, .5f, .5f);
            public Vector3 trail = new(.5f, .5f, 0f);
            public string trailOn;
            // Knock weapons: the striking face, in the same terms as `anchor`.
            public Vector3 strike;
            public Color trailColor = Color.white;
            public WeaponMotion motion;
            public Vector3 swingAxis = Vector3.forward;
            public bool showInHand = true;
            public string[] effects;
            public float effectScale = 1f;
            // Per-effect offsets (parallel to `effects`), e.g. to stand the lightning bolt on the impact.
            public Vector3[] effectOffsets;
            // Optional reshaping of the model before it is sized.
            public System.Action<Transform> adjust;
            // Clip name prefixes in AudioDir (Kenney's CC0 packs: Impact Sounds, RPG Audio, Sci-fi Sounds).
            public string[] swingSounds = { }, impactSounds = { };
        }

        // The pack's wrecking ball hangs 5 m below its pivot on a hair-thin chain; at weapon size the
        // ball would be a speck, so the chain is shortened and thickened.
        static void ShortenChain(Transform model)
        {
            var ball = model.Find("SwingPivot/New Spike");
            var chain = model.Find("SwingPivot/Chain");
            ball.localPosition = new Vector3(0f, -1.7f, 0f);
            chain.localPosition = new Vector3(0f, -.85f, 0f);
            chain.localScale = new Vector3(.14f, .85f, .14f);
        }

        // The mace is a flail: the wrecking ball's chain and ball under a wooden grip, swinging to and fro.
        static void MakeFlail(Transform model)
        {
            ShortenChain(model);
            Part(model, "Grip", Cylinder, Wood, new Vector3(0f, 5.75f, 0f), new Vector3(.26f, .75f, .26f));
            Part(model, "Pommel", Sphere, Metal, new Vector3(0f, 6.55f, 0f), Vector3.one * .36f);
            var pendulum = model.Find("SwingPivot").gameObject.AddComponent<Pendulum>();
            pendulum.amplitude = 45f;
            pendulum.frequency = 2.4f;
        }

        // The acid ball becomes a stoppered flask, which shatters where it lands.
        static void MakeFlask(Transform model)
        {
            Part(model, "Neck", Cylinder, Acid, new Vector3(0f, .2f, 0f), new Vector3(.11f, .06f, .11f));
            Part(model, "Lip", Cylinder, Metal, new Vector3(0f, .265f, 0f), new Vector3(.14f, .012f, .14f));
            Part(model, "Cork", Cylinder, Wood, new Vector3(0f, .3f, 0f), new Vector3(.1f, .035f, .1f));
        }

        // The shuriken spins about its own face as it flies.
        static void Spin(Transform model) => model.gameObject.AddComponent<Spinner>().axis = Vector3.forward;

        static void Part(Transform parent, string name, string mesh, string material, Vector3 position, Vector3 scale)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(mesh);
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Source + material + ".mat");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
        }

        static readonly Art[] Table =
        {
            new() { type = WeaponType.Glove, model = "Fist/UnityAssets/Assets/GameObject/MODEL_FirstPersonFist", size = 2.2f,
                anchor = new(.5f, .5f, 1f), trailColor = new(1f, .25f, .2f),
                effects = new[] { "Fist/UnityAssets/Assets/GameObject/Hit_Fist_Normal" }, effectScale = .6f,
                impactSounds = new[] { "impactPunch_heavy" } },
            new() { type = WeaponType.Knife, model = "Knife/UnityAssets/Assets/GameObject/KnifeProjectile", rotation = new(0f, 90f, 0f),
                size = 1.1f, anchor = new(.5f, .5f, 1f), motion = WeaponMotion.Thrown, trailColor = new(.6f, .85f, 1f),
                effects = new[] { Sparks }, swingSounds = new[] { "drawKnife" }, impactSounds = new[] { "knifeSlice" } },
            new() { type = WeaponType.Star, model = "BladeSaw/UnityAssets/Assets/GameObject/MODEL_BladeSaw", rotation = new(70f, 0f, 0f),
                size = .9f, trail = new(.5f, .5f, .5f), motion = WeaponMotion.Thrown, trailColor = new(.3f, .9f, 1f), adjust = Spin,
                effects = new[] { Sparks }, swingSounds = new[] { "drawKnife" }, impactSounds = new[] { "impactMetal_light" } },
            new() { type = WeaponType.Bomb, model = "Bomb/UnityAssets/Assets/GameObject/MODEL_Bomb", size = .8f,
                anchor = new(.5f, .4f, .5f), trail = new(.5f, 1f, .5f), motion = WeaponMotion.Dropped, trailColor = new(1f, .55f, .15f),
                effects = new[] { "Bomb/UnityAssets/Assets/GameObject/BombImpect" }, effectScale = .5f,
                impactSounds = new[] { "explosionCrunch", "lowFrequency_explosion" } },
            // Lying in the screen plane, head on the left with its face down, held by the end of the
            // handle: it knocks down onto the target (WeaponMotion.Knock pivots about the grip).
            new() { type = WeaponType.Hammer, model = "Hammer/UnityAssets/Assets/GameObject/MODEL_CuteHammer",
                rotation = (Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, -90f, 0f)).eulerAngles, size = 1.3f,
                motion = WeaponMotion.Knock, anchor = new(.97f, .6f, .5f), strike = new(.12f, 0f, .5f),
                trail = new(.12f, .6f, .5f), trailColor = new(1f, .8f, .3f),
                effects = new[] { "Hammer/UnityAssets/Assets/GameObject/Hit_Hammer_Normal" }, effectScale = .6f,
                impactSounds = new[] { "impactMetal_heavy", "impactPlate_heavy" } },
            new() { type = WeaponType.Mace, model = "SpikedBall/UnityAssets/Assets/GameObject/SpikedBallTool", size = 1.7f,
                anchor = new(.5f, .2f, .5f), adjust = MakeFlail, trailOn = "SwingPivot/New Spike", trailColor = new(1f, .45f, .35f),
                effects = new[] { Destruction }, effectScale = .5f,
                swingSounds = new[] { "metalLatch" }, impactSounds = new[] { "impactMetal_medium" } },
            new() { type = WeaponType.Lightning, showInHand = false, effects = new[]
                { "Thunder/UnityAssets/Assets/GameObject/ThunderImpact", "Thunder/UnityAssets/Assets/GameObject/MODEL_ThunderBolt" },
                effectOffsets = new[] { Vector3.zero, new Vector3(0f, 2.3f, 0f) }, effectScale = .7f,
                swingSounds = new[] { "forceField" }, impactSounds = new[] { "lowFrequency_explosion" } },
            new() { type = WeaponType.Bullets, model = "MissileRain/UnityAssets/Assets/GameObject/MissileRainProjectile", size = .9f,
                motion = WeaponMotion.Thrown, trailColor = new(1f, .6f, .2f),
                effects = new[] { "MissileRain/UnityAssets/Assets/GameObject/MissileRainImpactVfx" },
                swingSounds = new[] { "thrusterFire" }, impactSounds = new[] { "explosionCrunch" } },
            new() { type = WeaponType.Poison, model = "AcidBall/UnityAssets/Assets/GameObject/MODEL_AcidBall", size = .7f,
                trail = new(.5f, 1f, .5f), motion = WeaponMotion.Dropped, adjust = MakeFlask, trailColor = new(.5f, 1f, .25f),
                effects = new[] { "AcidBall/UnityAssets/Assets/GameObject/VFX_AcidImpact", Sparks }, effectScale = .6f,
                impactSounds = new[] { "impactGlass_heavy" } },
            new() { type = WeaponType.Chain, model = "SpikedBall/UnityAssets/Assets/GameObject/SpikedBallTool", size = 1.35f,
                anchor = new(.5f, .25f, .5f), adjust = ShortenChain, trailOn = "SwingPivot/New Spike", trailColor = new(.8f, .85f, 1f),
                effects = new[] { Destruction }, effectScale = .6f, impactSounds = new[] { "impactMetal_heavy", "impactPlate_heavy" } },
            new() { type = WeaponType.Rocket, model = "Missile/UnityAssets/Assets/GameObject/MissileProjectile", size = 1.1f,
                motion = WeaponMotion.Thrown, trailColor = new(1f, .65f, .2f),
                effects = new[] { "Bomb/UnityAssets/Assets/GameObject/BombImpect" }, effectScale = .6f,
                swingSounds = new[] { "thrusterFire" }, impactSounds = new[] { "lowFrequency_explosion", "explosionCrunch" } },
        };

        // Renderers whose material reference was lost in the export, by object name.
        static readonly (string name, string material)[] LostMaterials =
        {
            ("Body", "MissileRain/UnityAssets/Assets/Material/MissileBody"),
            ("Nose", "MissileRain/UnityAssets/Assets/Material/MissileNose"),
            ("Fin", "MissileRain/UnityAssets/Assets/Material/MissileFins"),
            ("Exhaust", "MissileRain/UnityAssets/Assets/Material/MissileExhaust"),
        };

        [MenuItem("Voxel Smash/Import Weapon Art")]
        public static void Run()
        {
            Directory.CreateDirectory(ModelDir);
            Directory.CreateDirectory(EffectDir);
            ConfigureAudio();
            var weapons = AssetDatabase.FindAssets("t:WeaponDef", new[] { WeaponDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponDef>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            foreach (var art in Table)
            {
                var strike = Vector3.zero;
                var model = art.model != null ? BuildModel(art, out strike) : null;
                var effect = BuildEffect(art);
                foreach (var w in weapons.Where(w => w.type == art.type))
                {
                    w.model = model;
                    w.impactEffect = effect;
                    w.impactEffectScale = art.effectScale;
                    w.motion = art.motion;
                    w.swingAxis = art.swingAxis;
                    w.showInHand = art.showInHand;
                    w.strikeOffset = art.motion == WeaponMotion.Knock ? strike : Vector3.zero;
                    w.trailColor = art.trailColor;
                    w.swingSounds = Clips(art.swingSounds);
                    w.impactSounds = Clips(art.impactSounds);
                    EditorUtility.SetDirty(w);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[VoxelSmash] Weapon art imported");
        }

        static AudioClip[] Clips(string[] prefixes) =>
            AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => prefixes.Any(prefix => Path.GetFileName(p).StartsWith(prefix)))
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();

        // Short one-shots: mono, decompressed on load so they start without delay.
        static void ConfigureAudio()
        {
            foreach (var path in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                if (importer.forceToMono && settings.loadType == AudioClipLoadType.DecompressOnLoad) continue;
                importer.forceToMono = true;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = .6f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        static GameObject BuildModel(Art art, out Vector3 strike)
        {
            var root = new GameObject(art.type.ToString());
            var body = Instantiate(art.model, root.transform);
            Clean(body, effect: false);
            art.adjust?.Invoke(body.transform);
            body.transform.localRotation = Quaternion.Euler(art.rotation);
            var bounds = MeshBounds(root);
            float scale = art.size / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            body.transform.localScale *= scale;
            bounds = MeshBounds(root);
            var anchor = bounds.min + Vector3.Scale(bounds.size, art.anchor);
            body.transform.localPosition -= anchor;
            strike = bounds.min + Vector3.Scale(bounds.size, art.strike) - anchor;
            var tip = new GameObject("TrailTip").transform;
            if (art.trailOn != null) tip.SetParent(body.transform.Find(art.trailOn), false);
            else
            {
                bounds = MeshBounds(root);
                tip.SetParent(root.transform, false);
                tip.localPosition = bounds.min + Vector3.Scale(bounds.size, art.trail);
            }
            return Save(root, $"{ModelDir}/{art.type}.prefab");
        }

        static GameObject BuildEffect(Art art)
        {
            var root = new GameObject(art.type + "Impact");
            for (int i = 0; i < art.effects.Length; i++)
            {
                var effect = Instantiate(art.effects[i], root.transform);
                if (art.effectOffsets != null) effect.transform.localPosition += art.effectOffsets[i];
                Clean(effect, effect: true);
            }
            return Save(root, $"{EffectDir}/{art.type}Impact.prefab");
        }

        static GameObject Instantiate(string path, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Source + path + ".prefab");
            if (!prefab) throw new FileNotFoundException("Weapon art missing", Source + path + ".prefab");
            var go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            return go;
        }

        static void Clean(GameObject go, bool effect)
        {
            var fallback = FallbackParticleMaterial();
            foreach (var t in go.GetComponentsInChildren<Transform>(true).Reverse())
            {
                if (!t) continue;
                if (!t.gameObject.activeSelf) { Object.DestroyImmediate(t.gameObject); continue; }
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                foreach (var c in t.GetComponents<Component>())
                    if (c is Collider or Rigidbody or Animator or Light) Object.DestroyImmediate(c);
                var renderer = t.GetComponent<Renderer>();
                if (renderer && !renderer.enabled && renderer is not ParticleSystemRenderer)
                {
                    Object.DestroyImmediate(renderer);
                    if (t.TryGetComponent<MeshFilter>(out var filter)) Object.DestroyImmediate(filter);
                    continue;
                }
                if (renderer) renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Fix(m, t.name, fallback)).ToArray();
                if (renderer) renderer.shadowCastingMode = effect || renderer is ParticleSystemRenderer or TrailRenderer
                    ? ShadowCastingMode.Off : ShadowCastingMode.On;
                if (t.TryGetComponent<ParticleSystem>(out var ps))
                {
                    var main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.stopAction = ParticleSystemStopAction.None;
                    if (effect)
                    {
                        main.playOnAwake = false;
                        main.loop = false;
                        // The pack's scripts called Emit() on some systems; make every one a self-contained burst.
                        var emission = ps.emission;
                        emission.enabled = true;
                        emission.rateOverTime = 0f;
                        if (emission.burstCount == 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                    }
                }
            }
        }

        static Material Fix(Material m, string objectName, Material fallback)
        {
            if (!m)
            {
                foreach (var (name, material) in LostMaterials)
                    if (objectName.Contains(name)) return AssetDatabase.LoadAssetAtPath<Material>(Source + material + ".mat");
                return fallback;
            }
            var shader = m.shader ? m.shader.name : "";
            bool builtIn = shader.StartsWith("Legacy Shaders/") || shader.StartsWith("Particles/") || shader.Contains("InternalError");
            return builtIn ? fallback : m;
        }

        static Material FallbackParticleMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(FallbackParticlePath);
            if (m) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            BaseShaderGUI.SetupMaterialBlendMode(m);
            AssetDatabase.CreateAsset(m, FallbackParticlePath);
            return m;
        }

        static Bounds MeshBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>().Where(r => r is MeshRenderer or SkinnedMeshRenderer).ToArray();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static GameObject Save(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
