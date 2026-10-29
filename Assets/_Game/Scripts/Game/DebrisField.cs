using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace VoxelSmash.Game
{
    // Loose cubes knocked off the sculpture. The first cubes of each strike get a pooled Rigidbody
    // (capped by quality) so they tumble and pile up; the rest are cheap particles integrated in a
    // Burst job against the table and the sculpture's occupancy grid. Bodies hand back to the
    // cheap path once they rest outside the clearance ring or after a few seconds.
    public sealed class DebrisField : MonoBehaviour
    {
        public const int MaxFragments = 13000;
        const int Batch = 1023;
        const float CubeSize = .0964f, BodyLifetime = 4.5f;

        public struct Particle
        {
            public float3 pos, vel, spin;
            public quaternion rot;
            public float age;
            public int body;
            public bool sleep;
            public Unity.Mathematics.Random random;
        }

        sealed class Body
        {
            public Rigidbody rb;
            public int particle = -1;
        }

        Material material;
        Mesh cube;
        PhysicsMaterial physicsMaterial;
        int bodyLayer;
        NativeArray<Particle> particles;
        NativeArray<float4x4> matrices;
        int count;
        readonly Vector4[] colors = new Vector4[MaxFragments];
        readonly Matrix4x4[] batchMatrices = new Matrix4x4[Batch];
        readonly Vector4[] batchColors = new Vector4[Batch];
        readonly List<MaterialPropertyBlock> blocks = new();
        readonly List<Body> bodies = new();
        readonly Stack<Body> freeBodies = new();
        int activeBodies;
        uint seed = 1;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public float ClearRadius { get; set; } = 2f;
        // 260 on capable phones, 110 on low quality (Phase 6 wires this to the quality toggle).
        public int MaxBodies { get; set; } = 260;
        public OccupancyGrid Occupancy { get; set; }
        public Transform Sculpture { get; set; }
        public int Count => count;
        public int ActiveBodies => activeBodies;

        public void Init(Material fragmentMaterial, PhysicsMaterial physics, int layer)
        {
            material = fragmentMaterial;
            physicsMaterial = physics;
            bodyLayer = layer;
            // The shader multiplies vertex colour by the instance colour, so the cube needs white vertices.
            cube = Instantiate(Resources.GetBuiltinResource<Mesh>("Cube.fbx"));
            var white = new Color[cube.vertexCount];
            System.Array.Fill(white, Color.white);
            cube.colors = white;
            // uv0 already spans 0..1 per face; a unit face size turns on the shader's edge bevel.
            var faceSize = new Vector2[cube.vertexCount];
            System.Array.Fill(faceSize, Vector2.one);
            cube.SetUVs(2, faceSize);
            particles = new NativeArray<Particle>(MaxFragments, Allocator.Persistent);
            matrices = new NativeArray<float4x4>(MaxFragments, Allocator.Persistent);
        }

        public void Clear()
        {
            foreach (var b in bodies)
                if (b.particle >= 0) Release(b);
            count = 0;
        }

        public void Spawn(Vector3 position, Color color, Vector3 impact, float power)
        {
            if (count >= MaxFragments) return;
            var vel = LaunchVelocity(position, impact, power);
            var spin = Random.insideUnitSphere * 12f;
            var rot = Random.rotation;
            var p = new Particle
            {
                pos = position, vel = vel, spin = spin, rot = rot, body = -1,
                random = new Unity.Mathematics.Random(seed++ * 747796405u + 1u)
            };
            if (activeBodies < MaxBodies)
            {
                var b = Acquire();
                b.particle = count;
                p.body = bodies.IndexOf(b);
                b.rb.transform.SetPositionAndRotation(position, rot);
                b.rb.position = position;
                b.rb.rotation = rot;
                b.rb.linearVelocity = vel;
                b.rb.angularVelocity = spin;
                b.rb.WakeUp();
            }
            particles[count] = p;
            colors[count] = color;
            count++;
        }

        Body Acquire()
        {
            activeBodies++;
            if (freeBodies.Count > 0)
            {
                var pooled = freeBodies.Pop();
                pooled.rb.gameObject.SetActive(true);
                return pooled;
            }
            var go = new GameObject("Debris Body") { layer = bodyLayer };
            go.transform.SetParent(transform, false);
            go.AddComponent<BoxCollider>().size = Vector3.one * CubeSize;
            go.GetComponent<BoxCollider>().sharedMaterial = physicsMaterial;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = .11f;
            rb.linearDamping = .12f;
            rb.angularDamping = .25f;
            // cannon-es sleeps under .18 m/s; Unity's threshold is mass-normalised kinetic energy.
            rb.sleepThreshold = .5f * .18f * .18f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var body = new Body { rb = rb };
            bodies.Add(body);
            return body;
        }

        void Release(Body b)
        {
            b.particle = -1;
            b.rb.gameObject.SetActive(false);
            freeBodies.Push(b);
            activeBodies--;
        }

        static Vector2 Outward(float x, float z, float fallbackX = 0f, float fallbackZ = 0f)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            if (r < .12f) { x = fallbackX; z = fallbackZ; r = Mathf.Sqrt(x * x + z * z); }
            if (r < .12f) { float a = Random.value * Mathf.PI * 2f; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
            return new Vector2(x / r, z / r);
        }

        // Outward blast with a sideways tangent so cubes fan out instead of piling in front.
        static Vector3 LaunchVelocity(Vector3 p, Vector3 impact, float power)
        {
            var dir = Outward(p.x, p.z, impact.x, impact.z);
            float tangent = (Random.value - .5f) * 1.6f;
            float speed = Mathf.Max(4.2f, power * .70f) * (.93f + Random.value * .20f);
            return new Vector3(dir.x * speed - dir.y * tangent,
                2.45f + Random.value * 1.5f + Mathf.Max(0f, power - 6f) * .12f,
                dir.y * speed + dir.x * tangent);
        }

        void LateUpdate()
        {
            if (count == 0) return;
            SyncBodies();
            var grid = Occupancy;
            var job = new IntegrateJob
            {
                particles = particles,
                matrices = matrices,
                occupancy = grid?.cells ?? default,
                hasGrid = grid != null && grid.cells.IsCreated,
                gridMin = grid?.min ?? 0,
                gridSize = grid?.size ?? 1,
                voxelSize = grid?.voxelSize ?? .1f,
                worldToSculpture = Sculpture ? (float4x4)Sculpture.worldToLocalMatrix : float4x4.identity,
                clearRadius = ClearRadius,
                dt = Mathf.Min(Time.deltaTime, .035f),
                cubeSize = CubeSize
            };
            job.Schedule(count, 128).Complete();
            Draw();
        }

        // Pull rigidbody state into the particle array, push resting bodies clear of the target and
        // retire bodies that have settled (or lived long enough) back to the cheap path.
        void SyncBodies()
        {
            for (int b = 0; b < bodies.Count; b++)
            {
                var body = bodies[b];
                int index = body.particle;
                if (index < 0) continue;
                var p = particles[index];
                var rb = body.rb;
                var t = rb.transform;
                p.pos = t.position;
                p.rot = t.rotation;
                var push = ClearingVelocity(rb.position, rb.linearVelocity, ClearRadius);
                if (push.HasValue) { rb.WakeUp(); rb.linearVelocity = push.Value; }
                float r = new Vector2(p.pos.x, p.pos.z).magnitude;
                if ((rb.IsSleeping() && r >= ClearRadius) || p.age > BodyLifetime)
                {
                    p.vel = rb.linearVelocity;
                    p.spin = rb.angularVelocity;
                    p.sleep = p.pos.y < .13f && math.length(p.vel) < .3f;
                    p.body = -1;
                    Release(body);
                }
                particles[index] = p;
            }
        }

        public static Vector3? ClearingVelocity(Vector3 position, Vector3 velocity, float radius)
        {
            float r = new Vector2(position.x, position.z).magnitude;
            if (r >= radius || position.y > .23f) return null;
            var dir = Outward(position.x, position.z);
            float speed = Mathf.Sqrt(2f * .66f * 9.81f * Mathf.Max(.1f, radius - r)) + 1.15f;
            float radial = velocity.x * dir.x + velocity.z * dir.y;
            if (radial >= speed) return null;
            return new Vector3(velocity.x + dir.x * (speed - radial), velocity.y, velocity.z + dir.y * (speed - radial));
        }

        void Draw()
        {
            if (material == null) return;
            for (int start = 0, b = 0; start < count; start += Batch, b++)
            {
                int n = Mathf.Min(Batch, count - start);
                NativeArray<Matrix4x4>.Copy(matrices.Reinterpret<Matrix4x4>(), start, batchMatrices, 0, n);
                System.Array.Copy(colors, start, batchColors, 0, n);
                if (blocks.Count <= b) blocks.Add(new MaterialPropertyBlock());
                var block = blocks[b];
                block.SetVectorArray(BaseColor, batchColors);
                var rp = new RenderParams(material)
                {
                    matProps = block,
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    receiveShadows = true
                };
                Graphics.RenderMeshInstanced(rp, cube, 0, batchMatrices, n);
            }
        }

        void OnDestroy()
        {
            if (particles.IsCreated) particles.Dispose();
            if (matrices.IsCreated) matrices.Dispose();
        }

        [BurstCompile]
        struct IntegrateJob : IJobParallelFor
        {
            const float Gravity = 9.81f, Floor = .05f;

            public NativeArray<Particle> particles;
            [WriteOnly] public NativeArray<float4x4> matrices;
            [ReadOnly] public NativeArray<byte> occupancy;
            public bool hasGrid;
            public int3 gridMin, gridSize;
            public float voxelSize, clearRadius, dt, cubeSize;
            public float4x4 worldToSculpture;

            public void Execute(int i)
            {
                var p = particles[i];
                p.age += dt;
                if (p.body < 0) Integrate(ref p);
                matrices[i] = float4x4.TRS(p.pos, p.rot, cubeSize);
                particles[i] = p;
            }

            void Integrate(ref Particle p)
            {
                // Slide resting cubes outside the clearance ring so they never hide the target.
                float r = math.length(p.pos.xz);
                if (r < clearRadius && p.pos.y <= .23f)
                {
                    float2 dir = r < .12f ? RandomDirection(ref p.random) : p.pos.xz / r;
                    float speed = math.sqrt(2f * .66f * Gravity * math.max(.1f, clearRadius - r)) + 1.15f;
                    float radial = math.dot(p.vel.xz, dir);
                    if (radial < speed) { p.vel.xz += dir * (speed - radial); p.sleep = false; }
                }
                if (p.sleep) return;
                p.vel.y -= Gravity * dt;
                p.vel *= math.exp(-.16f * dt);
                p.pos += p.vel * dt;
                if (Occupied(p.pos))
                {
                    p.pos -= p.vel * dt;
                    p.vel.xz *= -.28f;
                    p.vel.y = math.max(.5f, math.abs(p.vel.y) * .23f);
                }
                if (p.pos.y < Floor)
                {
                    p.pos.y = Floor;
                    p.vel.y = math.abs(p.vel.y) * .23f;
                    float friction = math.max(0f, 1f - .66f * Gravity * dt / math.max(.3f, math.length(p.vel.xz)));
                    p.vel.xz *= friction;
                    p.spin *= .75f;
                    if (math.length(p.vel) < .25f) { p.sleep = true; p.vel = 0f; }
                }
                if (!p.sleep)
                {
                    float angle = math.length(p.spin) * dt;
                    if (angle > 1e-5f) p.rot = math.normalize(math.mul(quaternion.AxisAngle(math.normalize(p.spin), angle), p.rot));
                }
                if (math.length(p.pos) > 16f) { p.sleep = true; p.pos.y = -1f; }
            }

            static float2 RandomDirection(ref Unity.Mathematics.Random random)
            {
                float a = random.NextFloat(math.PI * 2f);
                return new float2(math.cos(a), math.sin(a));
            }

            bool Occupied(float3 world)
            {
                if (!hasGrid) return false;
                float3 local = math.transform(worldToSculpture, world);
                var g = new int3((int)math.round(local.x / voxelSize), (int)math.floor(local.y / voxelSize), (int)math.round(local.z / voxelSize)) - gridMin;
                if (math.any(g < 0) || math.any(g >= gridSize)) return false;
                return occupancy[OccupancyGrid.Index(g, gridSize)] != 0;
            }
        }
    }
}
