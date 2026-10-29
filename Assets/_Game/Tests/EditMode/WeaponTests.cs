using NUnit.Framework;
using UnityEngine;
using VoxelSmash.Core;

namespace VoxelSmash.Tests
{
    public class WeaponTests
    {
        static WeaponDef Weapon(WeaponType type, float duration, int unlock = 0)
        {
            var w = ScriptableObject.CreateInstance<WeaponDef>();
            w.type = type; w.duration = duration; w.unlockLevel = unlock;
            return w;
        }

        [Test]
        public void FastSwingConnectsAtFortyPercent()
        {
            var glove = Weapon(WeaponType.Glove, .29f);
            Assert.IsFalse(Attack.Swing(glove, .29f * .39f).contact);
            var s = Attack.Swing(glove, .29f * .42f);
            Assert.IsTrue(s.contact);
            Assert.AreEqual(1f, s.reach, 1e-4f, "held at full reach just after contact");
        }

        [Test]
        public void HeavySwingConnectsLaterAndWindsUp()
        {
            var hammer = Weapon(WeaponType.Hammer, .68f);
            Assert.IsFalse(Attack.Swing(hammer, .68f * .5f).contact);
            Assert.IsTrue(Attack.Swing(hammer, .68f * .56f).contact);
            Assert.AreEqual(-1.05f, Attack.Swing(hammer, 0f).rotation, 1e-4f);
        }

        [Test]
        public void SwingReturnsAndFinishes()
        {
            var s = Attack.Swing(Weapon(WeaponType.Mace, .29f), 1f);
            Assert.IsTrue(s.done);
            Assert.AreEqual(0f, s.reach, 1e-4f);
        }

        [Test]
        public void UnlockMeansAvailableFromTheNextLevel()
        {
            var bomb = Weapon(WeaponType.Bomb, .29f, unlock: 5);
            Assert.IsFalse(bomb.AvailableAt(5));
            Assert.IsTrue(bomb.AvailableAt(6));
        }
    }
}
