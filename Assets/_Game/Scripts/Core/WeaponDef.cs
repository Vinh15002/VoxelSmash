using System;
using UnityEngine;

namespace VoxelSmash.Core
{
    public enum WeaponType { Glove, Knife, Star, Bomb, Hammer, Axe, Mace, Lightning, Bullets, Poison, Chain, Rocket }

    public enum WeaponSound { Glove, Slice, Hammer, Explosion, Zap, Shot, Poison }

    // Swing: comes in from below-right and swings. Thrown: flies straight in, pointed along its flight,
    // and vanishes on impact. Dropped: falls onto the target from above and vanishes on impact.
    // Knock: held by the grip, raised high, then snapped down onto the target with a small rebound.
    public enum WeaponMotion { Swing, Thrown, Dropped, Knock }

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
        // Gold paid from the level's purse for each strike (see Economy).
        public int strikeCost = 20;
        public float shake = .065f;
        public AttackStep[] pattern = { new(0f, 0f, 0f, 1f) };
        // Imported art (WeaponArtImporter): the swung model, authored with Unity's +z away from the
        // camera, and a particle burst played at every hit. Without a model the procedural one from
        // WeaponModels is used.
        public GameObject model;
        public GameObject impactEffect;
        public float impactEffectScale = 1f;
        // How the model reaches the target, the camera-space axis Attack.Swing's rotation turns it
        // around, and whether it is shown at all (lightning is only its effect).
        public WeaponMotion motion;
        public Vector3 swingAxis = Vector3.forward;
        public bool showInHand = true;
        // From the model's origin (its grip) to the face that lands on the target, in camera space.
        public Vector3 strikeOffset;
        // Streak behind the model, from its "TrailTip" child when it has one.
        public Color trailColor = new(1f, .85f, .5f, 1f);
        // Recorded sounds, one picked per play; without impact sounds `sound` is synthesised.
        public AudioClip[] swingSounds;
        public AudioClip[] impactSounds;

        public bool Explosive => type is WeaponType.Bomb or WeaponType.Rocket;

        public bool AvailableAt(int level) => level >= unlockLevel + 1;
    }
}
