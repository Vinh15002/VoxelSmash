using NUnit.Framework;
using UnityEngine;
using VoxelSmash.Game;

namespace VoxelSmash.Tests
{
    public class UiKitTests
    {
        [Test]
        public void HandFingertipIsWhereThePivotSays()
        {
            const int size = 128;
            var hand = UiKit.Hand(size);
            var tip = UiKit.HandTip * size;
            Assert.Greater(hand.GetPixel((int)tip.x, (int)tip.y - 4).a, .9f, "just below the tip is solid");
            Assert.Less(hand.GetPixel((int)tip.x, size - 1).a, .1f, "nothing above the tip");
            Assert.Less(hand.GetPixel(size - 2, size - 2).a, .1f, "corners are clear");
            Object.DestroyImmediate(hand);
        }

        [Test]
        public void GearHasAHoleAndTeeth()
        {
            const int size = 128;
            var gear = UiKit.Gear(size);
            Assert.Less(gear.GetPixel(size / 2, size / 2).a, .1f, "centre hole");
            Assert.Greater(gear.GetPixel(size / 2 + 32, size / 2).a, .9f, "ring");
            Assert.Greater(gear.GetPixel(size / 2 + 56, size / 2).a, .5f, "tooth on the x axis");
            Object.DestroyImmediate(gear);
        }
    }
}
