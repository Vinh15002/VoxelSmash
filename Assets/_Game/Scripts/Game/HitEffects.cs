using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelSmash.Core;
using Random = UnityEngine.Random;

namespace VoxelSmash.Game
{
    // Impact bursts (spark cubes, voxel dust, flash and shock ring, or a full explosion), the per-weapon
    // special effects (lightning, bullets, poison cloud, wrecking-ball ring, rocket trail) and the win
    // confetti. Pieces are pooled.
    public sealed class HitEffects : MonoBehaviour
    {
        const int MaxSparks = 1600, Batch = 1023;

        struct Spark
        {
            public Vector3 pos, vel, rot;
            public float age, life, size, spin, gravity, drag;
            public Color color;
        }

        static readonly Color[] ConfettiColors =
        {
            new Color32(0xff, 0x4d, 0x6d, 255), new Color32(0xff, 0xc9, 0x2b, 255), new Color32(0x3d, 0xd6, 0x6b, 255),
            new Color32(0x2f, 0x9b, 0xff, 255), new Color32(0xb1, 0x5c, 0xff, 255), new Color32(0xff, 0xff, 0xff, 255)
        };

        sealed class Piece
        {
            public GameObject go;
            public MeshFilter filter;
            public MeshRenderer renderer;
            public LineRenderer line;
            public readonly MaterialPropertyBlock block = new();
            public Vector3 origin;
            public float delay;
        }

        sealed class Effect
        {
            public float age, life;
            public readonly List<Piece> pieces = new();
            // Temporary parents (bullet bodies) destroyed with the effect after pieces return to the pool.
            public readonly List<GameObject> owned = new();
            public Light light;
            public Action<Effect, float> update;
        }

        static readonly int ColorId = Shader.PropertyToID("_Color"), BaseColorId = Shader.PropertyToID("_BaseColor");

        Material alpha, additive, sparkMaterial;
        Mesh sparkMesh;
        readonly List<Spark> sparks = new();
        readonly List<Effect> effects = new();
        readonly Stack<Piece> meshPool = new(), linePool = new();
        readonly Stack<Light> lightPool = new();
        readonly Matrix4x4[] matrices = new Matrix4x4[Batch];
        readonly Vector4[] colors = new Vector4[Batch];
        readonly List<MaterialPropertyBlock> sparkBlocks = new();

        public bool Detail { get; set; } = true;

        public void Init(Material alphaMaterial, Material additiveMaterial, Material instancedCubeMaterial)
        {
            alpha = alphaMaterial;
            additive = additiveMaterial;
            sparkMaterial = instancedCubeMaterial;
            sparkMesh = ProceduralMeshes.Box;
        }

        public void Clear()
        {
            sparks.Clear();
            foreach (var e in effects) Dispose(e);
            effects.Clear();
        }

        // ---- Impact burst ----------------------------------------------------------------------

        public void Impact(Vector3 point, bool explosion, IReadOnlyList<Color> palette, Vector3 direction)
        {
            int count = explosion ? (Detail ? 290 : 130) : (Detail ? 145 : 70);
            for (int i = 0; i < count; i++)
            {
                var dir = new Vector3(Random.value - .5f, Random.value - .30f, Random.value - .5f).normalized;
                if (!explosion) dir = (dir + direction * .65f).normalized;
                float speed = explosion ? 3f + Random.value * 7f : 1.7f + Random.value * 4.3f;
                // Palette colours arrive linear (straight from the voxel colours); the fire tones are sRGB.
                Color color = explosion
                    ? ((Color)(i % 4 == 0 ? new Color32(0x38, 0x3a, 0x40, 255) : i % 3 == 0 ? new Color32(0xff, 0x6a, 0x16, 255) : new Color32(0xff, 0xc3, 0x33, 255))).linear
                    : (i % 7 == 0 ? Color.white : palette[i % palette.Count]);
                if (sparks.Count >= MaxSparks) sparks.RemoveAt(0);
                sparks.Add(new Spark
                {
                    pos = point, vel = dir * speed, rot = Random.insideUnitSphere * 180f,
                    life = (explosion ? .75f : .45f) + Random.value * (explosion ? 1.15f : .85f),
                    size = .022f + Random.value * (explosion ? .052f : .04f),
                    spin = 3f + Random.value * 9f, color = color, gravity = 6.8f, drag = .6f
                });
            }

            Dust(point, palette, direction, explosion);
            var e = NewEffect(explosion ? 1.35f : .2f, point);
            var flash = MeshPiece(e, ProceduralMeshes.Sphere(), additive, point);
            if (!explosion)
            {
                // A bright shock ring facing the camera sells the contact.
                var shock = MeshPiece(e, ProceduralMeshes.Torus(1f, .05f, 6, 40), additive, point);
                shock.go.transform.rotation = Quaternion.FromToRotation(Vector3.forward, direction);
                e.update = (fx, t) =>
                {
                    Fade(flash, Color.white, .55f * (1f - t / .10f), .05f + t * 2.4f);
                    Fade(shock, new Color32(0xff, 0xf4, 0xd0, 255), .7f * (1f - t / .2f), .06f + t * 2.2f);
                };
                return;
            }
            var fire = MeshPiece(e, ProceduralMeshes.Sphere(20, 14), additive, point);
            var ring = MeshPiece(e, ProceduralMeshes.Torus(1f, .024f, 6, 64), additive, point);
            ring.go.transform.rotation = Quaternion.FromToRotation(Vector3.forward, direction);
            var puffs = new List<Piece>();
            for (int i = 0; i < 11; i++)
            {
                var puff = MeshPiece(e, ProceduralMeshes.Sphere(9, 7), alpha, point);
                puff.origin = new Vector3((Random.value - .5f) * .8f, Random.value * .7f, (Random.value - .5f) * .8f);
                puff.delay = i % 2;
                puffs.Add(puff);
            }
            e.light = AcquireLight(point, new Color32(0xff, 0xa1, 0x28, 255), 12f, 7f);
            e.update = (fx, t) =>
            {
                Fade(flash, new Color32(0xff, 0xbe, 0x32, 255), .9f * (1f - t / .22f), .12f + t * 5f);
                Fade(fire, new Color32(0xff, 0x69, 0x20, 255), .7f * (1f - t / .4f), .12f + Mathf.Min(t, .35f) * 3.9f);
                Fade(ring, new Color32(0xff, 0xdc, 0x78, 255), .85f * (1f - t / .55f), .1f + t * 6f);
                foreach (var puff in puffs)
                {
                    puff.go.transform.position = point + puff.origin * (1f + t * 1.6f) + Vector3.up * (t * .8f);
                    Fade(puff, puff.delay > 0 ? new Color32(0x4c, 0x47, 0x45, 255) : new Color32(0x77, 0x71, 0x6b, 255), .37f * (1f - t / 1.35f), .12f + t * .38f);
                }
                fx.light.intensity = Mathf.Max(0f, 12f * (1f - t / .25f));
            };
        }

        // Translucent voxel puffs tinted by the broken material, drifting up and out from the hit.
        void Dust(Vector3 point, IReadOnlyList<Color> palette, Vector3 direction, bool explosion)
        {
            int count = (explosion ? 4 : 6) * (Detail ? 2 : 1);
            const float life = .9f;
            var e = NewEffect(life, point);
            var puffs = new List<(Piece piece, Vector3 drift, Vector3 spin, Color color, float size)>();
            for (int i = 0; i < count; i++)
            {
                var piece = MeshPiece(e, ProceduralMeshes.Box, alpha, point);
                piece.go.transform.rotation = Random.rotation;
                var drift = (Random.insideUnitSphere * .55f + direction * .35f + Vector3.up * .25f) * (explosion ? 2.2f : 1f);
                // Palette colours are linear; dust is that colour washed toward a warm grey.
                var tint = Color.Lerp(palette.Count > 0 ? palette[i % palette.Count].gamma : Color.grey, new Color(.80f, .76f, .70f), .45f);
                puffs.Add((piece, drift, Random.insideUnitSphere * 140f, tint, (explosion ? .2f : .1f) + Random.value * .09f));
            }
            e.update = (fx, t) =>
            {
                float k = t / life, ease = 1f - (1f - k) * (1f - k);
                foreach (var (piece, drift, spin, color, size) in puffs)
                {
                    piece.go.transform.position = point + drift * ease;
                    piece.go.transform.Rotate(spin * Mathf.Min(Time.deltaTime, .035f), Space.Self);
                    Fade(piece, color, .5f * (1f - k) * Mathf.Min(1f, t / .05f), size * (.6f + ease * 1.6f));
                }
            };
        }

        // Voxel confetti that bursts up from where the sculpture stood and flutters down.
        public void Confetti(Vector3 point)
        {
            int count = Detail ? 260 : 120;
            for (int i = 0; i < count; i++)
            {
                if (sparks.Count >= MaxSparks) sparks.RemoveAt(0);
                var dir = new Vector3(Random.value - .5f, .9f + Random.value * .8f, Random.value - .5f);
                sparks.Add(new Spark
                {
                    pos = point + Random.insideUnitSphere * .3f, vel = dir * (3.2f + Random.value * 3.4f),
                    rot = Random.insideUnitSphere * 180f, life = 2.6f + Random.value * 1.4f,
                    size = .045f + Random.value * .03f, spin = 6f + Random.value * 10f,
                    color = ConfettiColors[i % ConfettiColors.Length].linear, gravity = 2.4f, drag = 1.9f
                });
            }
        }

        // ---- Weapon specials -------------------------------------------------------------------

        public void Special(WeaponType type, Vector3 point, Vector3 direction)
        {
            switch (type)
            {
                case WeaponType.Lightning: Lightning(point); break;
                case WeaponType.Bullets: Bullets(point, direction); break;
                case WeaponType.Poison: Poison(point); break;
                case WeaponType.Chain: ChainRing(point, direction); break;
                case WeaponType.Rocket: RocketTrail(point, direction); break;
            }
        }

        void Lightning(Vector3 point)
        {
            var e = NewEffect(.4f, point);
            var bolt = new Vector3[10];
            for (int i = 0; i < 10; i++)
                bolt[i] = point + new Vector3((i == 9 ? 0f : (Random.value - .5f) * .40f) - (9 - i) * .06f, (9 - i) * .34f, 0f);
            var lines = new List<Piece>
            {
                LinePiece(e, bolt, additive, new Color32(0xa2, 0x52, 0xff, 128), .11f),
                LinePiece(e, bolt, additive, new Color32(0xff, 0xf4, 0xff, 255), .04f)
            };
            for (int i = 0; i < 4; i++)
            {
                var start = bolt[2 + i * 2];
                float side = i % 2 == 1 ? 1f : -1f;
                lines.Add(LinePiece(e, new[] { start, start + new Vector3(.35f * side, -.18f, .06f), start + new Vector3(.60f * side, -.40f, .07f) },
                    additive, new Color32(0xc6, 0x81, 0xff, 255), .016f));
            }
            e.light = AcquireLight(point + Vector3.up * .5f, new Color32(0xbd, 0x70, 0xff, 255), 6f, 5f);
            e.update = (fx, t) =>
            {
                bool visible = Mathf.FloorToInt(t * 70f) % 3 != 0;
                foreach (var l in lines) l.line.enabled = visible;
                fx.light.enabled = visible;
            };
        }

        void Bullets(Vector3 point, Vector3 direction)
        {
            var e = NewEffect(.55f, point);
            var facing = Quaternion.FromToRotation(Vector3.forward, direction);
            var bullets = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                var root = new GameObject("Bullet").transform;
                root.SetParent(transform, false);
                e.owned.Add(root.gameObject);
                var shaft = MeshPiece(e, ProceduralMeshes.Cylinder(.055f, .055f, .22f, 12), alpha, Vector3.zero);
                Tint(shaft, new Color32(0xff, 0xca, 0x28, 255), 1f);
                shaft.go.transform.SetParent(root, false);
                shaft.go.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var nose = MeshPiece(e, ProceduralMeshes.Cone(.055f, .15f, 12), alpha, Vector3.zero);
                Tint(nose, new Color32(0xd7, 0x16, 0x22, 255), 1f);
                nose.go.transform.SetParent(root, false);
                nose.go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                nose.go.transform.localPosition = new Vector3(0, 0, -.17f);
                var tail = MeshPiece(e, ProceduralMeshes.Cone(.06f, .55f, 8), additive, Vector3.zero);
                Tint(tail, new Color32(0xff, 0xd5, 0x34, 255), .8f);
                tail.go.transform.SetParent(root, false);
                tail.go.transform.localRotation = Quaternion.Euler(90, 0, 0);
                tail.go.transform.localPosition = new Vector3(0, 0, .33f);
                root.rotation = facing;
                bullets.Add(root);
            }
            e.update = (fx, t) =>
            {
                for (int i = 0; i < bullets.Count; i++)
                {
                    float delay = i * .10f, k = Mathf.Clamp01((t - delay) / .20f);
                    var from = direction * 2.8f + new Vector3((i - 1) * .26f, .3f, 0f);
                    bullets[i].gameObject.SetActive(t >= delay && k < 1f);
                    bullets[i].position = point + from * (1f - k);
                }
            };
        }

        void Poison(Vector3 point)
        {
            var e = NewEffect(1.3f, point);
            var bubbles = new List<Piece>();
            for (int i = 0; i < 16; i++)
            {
                var p = MeshPiece(e, ProceduralMeshes.Sphere(10, 7), alpha, point);
                p.origin = new Vector3((Random.value - .5f) * .9f, (Random.value - .5f) * .5f, (Random.value - .5f) * .5f);
                p.delay = .07f + Random.value * .10f; // reused as the bubble radius
                bubbles.Add(p);
            }
            e.light = AcquireLight(point, new Color32(0x9c, 0xff, 0x44, 255), 1.5f, 3f);
            e.update = (fx, t) =>
            {
                for (int i = 0; i < bubbles.Count; i++)
                {
                    var b = bubbles[i];
                    b.go.transform.position = point + b.origin + Vector3.up * (t * .5f);
                    Fade(b, i % 3 == 0 ? new Color32(0xd9, 0xff, 0x58, 255) : new Color32(0x76, 0xdc, 0x21, 255),
                        .48f * Mathf.Max(0f, 1f - t / fx.life), b.delay * (1f + t * .4f));
                }
            };
        }

        void ChainRing(Vector3 point, Vector3 direction)
        {
            var e = NewEffect(.30f, point);
            var ring = MeshPiece(e, ProceduralMeshes.Torus(.2f, .012f, 5, 36), alpha, point);
            ring.go.transform.rotation = Quaternion.FromToRotation(Vector3.forward, direction);
            e.update = (fx, t) => Fade(ring, new Color32(0xc7, 0xd3, 0xe8, 255), .8f * (1f - t / fx.life), 1f + t * 5f);
        }

        void RocketTrail(Vector3 point, Vector3 direction)
        {
            var e = NewEffect(.35f, point);
            var trail = LinePiece(e, new[] { point + direction * 1.8f, point }, additive, new Color32(0xff, 0xbe, 0x24, 204), .07f);
            e.update = (fx, t) => trail.line.widthMultiplier = .07f * (1f - t / fx.life);
        }

        // ---- Plumbing ----------------------------------------------------------------------------

        Effect NewEffect(float life, Vector3 point)
        {
            var e = new Effect { life = life };
            effects.Add(e);
            return e;
        }

        Piece MeshPiece(Effect e, Mesh mesh, Material material, Vector3 position)
        {
            var p = meshPool.Count > 0 ? meshPool.Pop() : null;
            if (p == null)
            {
                p = new Piece { go = new GameObject("Fx Mesh", typeof(MeshFilter), typeof(MeshRenderer)) };
                p.go.transform.SetParent(transform, false);
                p.filter = p.go.GetComponent<MeshFilter>();
                p.renderer = p.go.GetComponent<MeshRenderer>();
                p.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                p.renderer.receiveShadows = false;
            }
            p.go.SetActive(true);
            p.go.transform.SetParent(transform, false);
            p.go.transform.SetPositionAndRotation(position, Quaternion.identity);
            p.go.transform.localScale = Vector3.zero;
            p.filter.sharedMesh = mesh;
            p.renderer.sharedMaterial = material;
            p.origin = Vector3.zero;
            p.delay = 0f;
            e.pieces.Add(p);
            return p;
        }

        Piece LinePiece(Effect e, Vector3[] points, Material material, Color32 color, float width)
        {
            var p = linePool.Count > 0 ? linePool.Pop() : null;
            if (p == null)
            {
                p = new Piece { go = new GameObject("Fx Line") };
                p.go.transform.SetParent(transform, false);
                p.line = p.go.AddComponent<LineRenderer>();
                p.line.useWorldSpace = true;
                p.line.numCapVertices = 2;
                p.line.numCornerVertices = 2;
                p.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                p.line.alignment = LineAlignment.View;
            }
            p.go.SetActive(true);
            p.line.enabled = true;
            p.line.sharedMaterial = material;
            p.line.positionCount = points.Length;
            p.line.SetPositions(points);
            p.line.widthMultiplier = width;
            Color c = color;
            p.line.startColor = p.line.endColor = c;
            p.block.SetColor(ColorId, Color.white);
            p.line.SetPropertyBlock(p.block);
            e.pieces.Add(p);
            return p;
        }

        Light AcquireLight(Vector3 position, Color color, float intensity, float range)
        {
            var light = lightPool.Count > 0 ? lightPool.Pop() : new GameObject("Fx Light").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.gameObject.SetActive(true);
            light.enabled = true;
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.transform.position = position;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }

        void Tint(Piece p, Color color, float opacity)
        {
            color.a = Mathf.Clamp01(opacity);
            p.block.SetColor(ColorId, color);
            p.renderer.SetPropertyBlock(p.block);
            if (p.go.transform.localScale == Vector3.zero) p.go.transform.localScale = Vector3.one;
        }

        void Fade(Piece p, Color color, float opacity, float scale)
        {
            color.a = Mathf.Clamp01(opacity);
            p.block.SetColor(ColorId, color);
            p.renderer.SetPropertyBlock(p.block);
            p.go.transform.localScale = Vector3.one * scale;
        }

        void Dispose(Effect e)
        {
            foreach (var p in e.pieces)
            {
                p.go.transform.SetParent(transform, false);
                p.go.SetActive(false);
                (p.line ? linePool : meshPool).Push(p);
            }
            if (e.light) { e.light.gameObject.SetActive(false); lightPool.Push(e.light); }
            foreach (var go in e.owned) Destroy(go);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, .035f);
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var e = effects[i];
                e.age += dt;
                e.update?.Invoke(e, e.age);
                if (e.age < e.life) continue;
                Dispose(e);
                effects.RemoveAt(i);
            }
            UpdateSparks(dt);
        }

        void UpdateSparks(float dt)
        {
            int n = 0;
            for (int i = 0; i < sparks.Count; i++)
            {
                var s = sparks[i];
                s.age += dt;
                if (s.age >= s.life) continue;
                s.vel.y -= s.gravity * dt;
                s.vel *= Mathf.Exp(-s.drag * dt);
                s.pos += s.vel * dt;
                if (s.pos.y < .035f)
                {
                    s.pos.y = .035f;
                    s.vel.y = Mathf.Abs(s.vel.y) * .20f;
                    s.vel.x *= .75f; s.vel.z *= .75f;
                }
                s.rot += new Vector3(s.spin, 0f, s.spin * .7f) * (dt * Mathf.Rad2Deg);
                sparks[n++] = s;
            }
            sparks.RemoveRange(n, sparks.Count - n);
            for (int start = 0, b = 0; start < sparks.Count; start += Batch, b++)
            {
                int count = Mathf.Min(Batch, sparks.Count - start);
                for (int k = 0; k < count; k++)
                {
                    var s = sparks[start + k];
                    float fade = Mathf.Min(1f, (s.life - s.age) / .25f);
                    matrices[k] = Matrix4x4.TRS(s.pos, Quaternion.Euler(s.rot), Vector3.one * (s.size * fade));
                    colors[k] = s.color;
                }
                if (sparkBlocks.Count <= b) sparkBlocks.Add(new MaterialPropertyBlock());
                sparkBlocks[b].SetVectorArray(BaseColorId, colors);
                var rp = new RenderParams(sparkMaterial) { matProps = sparkBlocks[b], shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off };
                Graphics.RenderMeshInstanced(rp, sparkMesh, 0, matrices, count);
            }
        }
    }
}
