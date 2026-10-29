using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VoxelSmash.Core;

namespace VoxelSmash.Tests
{
    public class VoxelBoxesTests
    {
        const int N = 8;

        static bool[] Filled(System.Func<int, int, int, bool> fill)
        {
            var occupied = new bool[N * N * N];
            for (int z = 0; z < N; z++)
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                occupied[VoxelBoxes.Index(x, y, z, N)] = fill(x, y, z);
            return occupied;
        }

        static int Volume(List<VoxelBoxes.Box> boxes) => boxes.Sum(b => b.size.x * b.size.y * b.size.z);

        [Test]
        public void FullBlockIsOneBox()
        {
            var boxes = new List<VoxelBoxes.Box>();
            VoxelBoxes.Merge(Filled((x, y, z) => true), N, boxes);
            Assert.AreEqual(1, boxes.Count);
            Assert.AreEqual(new Vector3Int(8, 8, 8), boxes[0].size);
        }

        [Test]
        public void EmptyBlockHasNoBoxes()
        {
            var boxes = new List<VoxelBoxes.Box>();
            VoxelBoxes.Merge(Filled((x, y, z) => false), N, boxes);
            Assert.IsEmpty(boxes);
        }

        [Test]
        public void LShapeNeedsTwoBoxesAndKeepsVolume()
        {
            var boxes = new List<VoxelBoxes.Box>();
            var occupied = Filled((x, y, z) => y == 0 || x == 0);
            int cells = occupied.Count(o => o);
            VoxelBoxes.Merge(occupied, N, boxes);
            Assert.AreEqual(2, boxes.Count);
            Assert.AreEqual(cells, Volume(boxes));
        }

        [Test]
        public void BoxesNeverOverlapAndCoverEveryCell()
        {
            var random = new System.Random(7);
            var source = Filled((x, y, z) => random.NextDouble() < .6);
            var boxes = new List<VoxelBoxes.Box>();
            VoxelBoxes.Merge((bool[])source.Clone(), N, boxes);

            var covered = new bool[source.Length];
            foreach (var b in boxes)
                for (int z = 0; z < b.size.z; z++)
                for (int y = 0; y < b.size.y; y++)
                for (int x = 0; x < b.size.x; x++)
                {
                    int i = VoxelBoxes.Index(b.min.x + x, b.min.y + y, b.min.z + z, N);
                    Assert.IsTrue(source[i], "box covers an empty cell");
                    Assert.IsFalse(covered[i], "boxes overlap");
                    covered[i] = true;
                }
            CollectionAssert.AreEqual(source, covered);
        }
    }
}
