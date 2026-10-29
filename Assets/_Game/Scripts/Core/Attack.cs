using UnityEngine;

namespace VoxelSmash.Core
{
    public struct SwingState
    {
        public float phase, reach, rotation;
        public bool contact, done;
    }

    public static class Attack
    {
        static bool Slow(WeaponType t) => t is WeaponType.Hammer or WeaponType.Axe or WeaponType.Knife or WeaponType.Chain;

        // Reach eases in to the target, holds briefly after contact, then pulls back. Heavy swings
        // connect later in the motion; bladed and hammer swings wind up with a z rotation.
        public static SwingState Swing(WeaponDef weapon, float elapsed)
        {
            float duration = weapon.duration > 0f ? weapon.duration : .29f;
            float phase = Mathf.Min(1f, elapsed / duration);
            float contact = Slow(weapon.type) ? .55f : .40f;
            float reach;
            if (phase < contact) reach = Mathf.Sin(phase / contact * Mathf.PI / 2f);
            else if (phase < contact + .10f) reach = 1f;
            else reach = Mathf.Max(0f, 1f - (phase - contact - .10f) / (1f - contact - .10f));
            float rotation = weapon.type switch
            {
                WeaponType.Knife => -.70f * (1f - reach),
                WeaponType.Hammer or WeaponType.Axe or WeaponType.Chain => -1.05f * (1f - reach),
                WeaponType.Star => phase * 9f,
                _ => 0f
            };
            return new SwingState { phase = phase, reach = reach, rotation = rotation, contact = phase >= contact, done = phase >= 1f };
        }
    }
}
