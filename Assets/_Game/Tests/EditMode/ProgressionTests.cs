using NUnit.Framework;
using VoxelSmash.Core;

namespace VoxelSmash.Tests
{
    public class ProgressionTests
    {
        [Test]
        public void OnlyTheNextLevelIsPlayable()
        {
            var p = new Progression(10);
            Assert.IsTrue(p.CanPlay(1));
            Assert.IsFalse(p.CanPlay(2));
            Assert.IsTrue(p.Complete(1));
            Assert.IsTrue(p.CanPlay(2));
            Assert.IsFalse(p.CanPlay(3));
            Assert.AreEqual(2, p.ResumeLevel);
        }

        [Test]
        public void ReplayIsNotAFirstFinish()
        {
            var p = new Progression(10);
            Assert.IsTrue(p.Complete(1));
            Assert.IsFalse(p.Complete(1));
            Assert.IsFalse(p.Complete(3), "locked levels cannot be completed");
        }

        [Test]
        public void ResumeStaysOnTheLastLevel()
        {
            var p = new Progression(3, new[] { 1, 2, 3 });
            Assert.AreEqual(3, p.Completed);
            Assert.AreEqual(3, p.ResumeLevel);
        }

        [Test]
        public void EveryFifthFirstFinishIsAMilestone()
        {
            var p = new Progression(10, new[] { 1, 2, 3, 4 });
            bool first = p.Complete(5);
            Assert.IsTrue(p.IsMilestone(first));
            Assert.IsFalse(p.IsMilestone(p.Complete(5)));
            Assert.IsFalse(p.IsMilestone(p.Complete(6)));
        }

        [Test]
        public void SaveRoundTripsAndDropsJunk()
        {
            var p = Progression.Parse(10, "3,1,x,2,99,-4");
            Assert.AreEqual("1,2,3", p.Serialize());
            Assert.AreEqual(3, p.Completed);
        }

        [Test]
        public void LegacyNextLevelMigrates()
        {
            var p = Progression.Parse(42, "", legacyNextLevel: 6);
            Assert.AreEqual(5, p.Completed);
            Assert.AreEqual(6, p.ResumeLevel);
            Assert.AreEqual(0, Progression.Parse(42, null).Completed);
        }
    }
}
