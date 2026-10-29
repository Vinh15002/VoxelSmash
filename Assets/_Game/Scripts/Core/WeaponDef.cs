using System;
using UnityEngine;

namespace VoxelSmash.Core
{
    public enum WeaponType { Glove, Knife, Star, Bomb, Hammer, Axe, Mace, Lightning, Bullets, Poison, Chain, Rocket }

    public enum WeaponSound { Glove, Slice, Hammer, Explosion, Zap, Shot, Poison }

    // One hit of a weapon's pattern, offset in sculpture-local units from the tapped cell.
    [Serializable]
    public struct AttackStep
    {
        public float delay, dx, dy, scale;

        public AttackStep(float delay, float dx, float dy, float scale)
        {
            this.delay = delay; this.dx = dx; this.dy = dy; this.scale = scale;
        }
    }

    [CreateAssetMenu(menuName = "Voxel Smash/Weapon")]
    public sealed class WeaponDef : ScriptableObject
    {
        public string displayName;
        public WeaponType type;
        public StrikeMode mode;
        public float radius = .43f;
        public float power = 4.8f;
        public float cooldown = .27f;
        // Swing length; the hit lands at the contact phase (see Attack.Swing).
        public float duration = .29f;
        // Usable from level unlockLevel + 1.
        public int unlockLevel;
        public WeaponSound sound;
        public float shake = .065f;
        public AttackStep[] pattern = { new(0f, 0f, 0f, 1f) };

        public bool Explosive => type is WeaponType.Bomb or WeaponType.Rocket;

        public bool AvailableAt(int level) => level >= unlockLevel + 1;
    }
}
