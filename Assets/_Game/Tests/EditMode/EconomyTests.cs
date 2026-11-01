using NUnit.Framework;
using UnityEngine;
using VoxelSmash.Core;

namespace VoxelSmash.Tests
{
    public class EconomyTests
    {
        [Test]
        public void CombosMagnetAndBossesPayMore()
        {
            int plain = Economy.CoinsForHit(160, 1, 0, false);
            Assert.AreEqual(20, plain);
            Assert.Greater(Economy.CoinsForHit(160, 5, 0, false), plain);
            Assert.Greater(Economy.CoinsForHit(160, 1, 3, false), plain);
            Assert.AreEqual(plain * 2, Economy.CoinsForHit(160, 1, 0, true));
            Assert.AreEqual(1, Economy.CoinsForHit(2, 1, 0, false), "every hit that breaks something pays");
            Assert.AreEqual(0, Economy.CoinsForHit(0, 1, 0, false));
        }

        [Test]
        public void StarsFollowParHits()
        {
            int total = 2600, par = Economy.Par(total);
            Assert.AreEqual(10, par);
            Assert.AreEqual(3, Economy.Stars(par, total));
            Assert.AreEqual(2, Economy.Stars(par + 1, total));
            Assert.AreEqual(1, Economy.Stars(par * 2 + 1, total));
            Assert.AreEqual(3, Economy.Par(100), "tiny models still allow a few hits");
        }

        [Test]
        public void UpgradesGetPricierAndCap()
        {
            Assert.AreEqual(150, Economy.SizeCost(0));
            Assert.Greater(Economy.SizeCost(1), Economy.SizeCost(0));
            Assert.AreEqual(1.8f, Economy.SizeMultiplier(Economy.MaxUpgrade), 1e-4f);
            Assert.AreEqual(1.8f, Economy.SizeMultiplier(99), 1e-4f, "levels past the cap don't grow");
        }

        [Test]
        public void EveryFifthLevelIsABossAndRegularModelsDontSkip()
        {
            var list = ScriptableObject.CreateInstance<LevelList>();
            var regular = new VoxelModel[3];
            var bosses = new VoxelModel[2];
            for (int i = 0; i < 3; i++) regular[i] = ScriptableObject.CreateInstance<VoxelModel>();
            for (int i = 0; i < 2; i++) bosses[i] = ScriptableObject.CreateInstance<VoxelModel>();
            list.levels = regular;
            list.bosses = bosses;
            Assert.IsTrue(list.IsBoss(5));
            Assert.IsFalse(list.IsBoss(4));
            Assert.AreSame(bosses[0], list.ModelFor(5));
            Assert.AreSame(bosses[1], list.ModelFor(10));
            Assert.AreSame(bosses[0], list.ModelFor(15));
            // Levels 1-4 use regular 0,1,2,0; level 6 continues with regular 1.
            Assert.AreSame(regular[0], list.ModelFor(4));
            Assert.AreSame(regular[1], list.ModelFor(6));
        }
    }
}
