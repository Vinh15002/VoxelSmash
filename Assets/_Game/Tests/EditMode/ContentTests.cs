using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VoxelSmash.Core;
using VoxelSmash.EditorTools;
using VoxelSmash.Game;

namespace VoxelSmash.Tests
{
    public class ContentTests
    {
        static readonly Vector3Int[] Neighbours =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1)
        };

        // Every voxel must be face-connected to the ground, or the first strike drops it unexplained.
        [Test]
        public void SampleModelsAreFullySupported()
        {
            foreach (var model in SampleModels.All())
            {
                var cells = new HashSet<Vector3Int>(model.voxels.Select(v => new Vector3Int(v.x, v.y, v.z)));
                var reached = new HashSet<Vector3Int>(cells.Where(c => c.y == 0));
                var queue = new Queue<Vector3Int>(reached);
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    foreach (var n in Neighbours)
                        if (cells.Contains(c + n) && reached.Add(c + n)) queue.Enqueue(c + n);
                }
                Assert.AreEqual(cells.Count, reached.Count, $"{model.displayName} has floating voxels");
                Assert.Less(model.voxels.Max(v => v.color), model.palette.Length, $"{model.displayName} uses a missing colour");
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void JunkyardStaysInsideItsBudget()
        {
            var mesh = Junkyard.Build();
            Assert.Greater(mesh.vertexCount, 10000);
            Assert.Less(mesh.vertexCount, 400000, "backdrop too heavy for mobile");
            // The longest plank is 2.2 m, so a tumbled one can bed up to ~1.1 m into the dirt.
            Assert.Greater(mesh.bounds.min.y, -1.3f, "scrap may bed into the ground, not sink through it");
            Object.DestroyImmediate(mesh);
        }
    }
}
