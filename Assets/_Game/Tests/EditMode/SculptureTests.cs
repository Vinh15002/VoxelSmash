using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VoxelSmash.Core;

namespace VoxelSmash.Tests
{
    public class SculptureTests
    {
        const float S = .1f;

        static List<Voxel> Column(int x, int height, int z = 0)
        {
            var list = new List<Voxel>();
            for (int y = 0; y < height; y++) list.Add(new Voxel(x, y, z, 0));
            return list;
        }

        static Vector3 Center(int x, int y, int z) => new(x * S, (y + .5f) * S, z * S);

        [Test]
        public void StrikeRemovesOnlyCellsInsideRadius()
        {
            var voxels = new List<Voxel>();
            for (int x = 0; x < 40; x++) voxels.Add(new Voxel(x, 0, 0, 0));
            var s = new Sculpture(voxels, S);

            var removed = s.Strike(Center(20, 0, 0), .15f);

            CollectionAssert.AreEquivalent(new[] { 19, 20, 21 }, removed);
            Assert.AreEqual(3, s.Removed);
            Assert.AreEqual(1, s.Hits);
        }

        [Test]
        public void MissReturnsEmptyAndKeepsEverything()
        {
            var s = new Sculpture(Column(0, 10), S);
            Assert.IsEmpty(s.Strike(new Vector3(5, 5, 5), .3f));
            Assert.AreEqual(0, s.Removed);
        }

        [Test]
        public void CuttingAColumnDropsEverythingAbove()
        {
            var s = new Sculpture(Column(0, 40), S);

            var removed = s.Strike(Center(0, 5, 0), .05f);

            Assert.AreEqual(35, removed.Count, "the hit cell plus the 34 unsupported cells above it");
            Assert.IsTrue(s.IsAlive(new Vector3Int(0, 4, 0)));
            Assert.IsFalse(s.IsAlive(new Vector3Int(0, 6, 0)));
        }

        [Test]
        public void BeamStaysWhileOnePillarRemains()
        {
            // Two pillars at x=0 and x=10 joined by a beam on y=10.
            var voxels = new List<Voxel>();
            voxels.AddRange(Column(0, 10));
            voxels.AddRange(Column(10, 10));
            for (int x = 0; x <= 10; x++) voxels.Add(new Voxel(x, 10, 0, 1));
            var s = new Sculpture(voxels, S);

            s.Strike(Center(0, 3, 0), .05f);
            Assert.IsTrue(s.IsAlive(new Vector3Int(5, 10, 0)), "beam still held by the right pillar");

            s.Strike(Center(10, 3, 0), .05f);
            Assert.IsFalse(s.IsAlive(new Vector3Int(5, 10, 0)), "beam drops once both pillars are cut");
        }

        [Test]
        public void SliceModeReachesFurtherVerticallyThanSideways()
        {
            var voxels = new List<Voxel>();
            for (int x = -5; x <= 5; x++)
            for (int y = 0; y <= 20; y++)
                voxels.Add(new Voxel(x, y, 0, 0));
            var s = new Sculpture(voxels, S);

            s.Strike(Center(0, 10, 0), .2f, StrikeMode.Slice);

            Assert.IsFalse(s.IsAlive(new Vector3Int(0, 13, 0)), "slice reaches 3 cells up");
            Assert.IsTrue(s.IsAlive(new Vector3Int(2, 10, 0)), "but not 2 cells sideways");
        }

        [Test]
        public void FinalRemnantsAreClearedAtThreshold()
        {
            var voxels = new List<Voxel>();
            for (int x = 0; x < 100; x++) voxels.Add(new Voxel(x * 3, 0, 0, 0));
            var s = new Sculpture(voxels, S);

            for (int x = 0; x < 96; x++) s.Strike(Center(x * 3, 0, 0), .05f);
            Assert.AreEqual(.96f, s.Progress, 1e-4f);

            s.Strike(Center(96 * 3, 0, 0), .05f);
            Assert.AreEqual(1f, s.Progress);
        }

        [Test]
        public void CellAtFindsTheContainingVoxel()
        {
            var s = new Sculpture(Column(2, 5, -1), S);
            int i = s.CellAt(new Vector3(.21f, .34f, -.09f));
            Assert.AreEqual(new Vector3Int(2, 3, -1), s.cells[i].grid);
            Assert.AreEqual(-1, s.CellAt(new Vector3(1, 1, 1)));
        }
    }
}
