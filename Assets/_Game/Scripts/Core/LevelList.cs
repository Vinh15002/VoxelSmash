using UnityEngine;

namespace VoxelSmash.Core
{
    [CreateAssetMenu(menuName = "Voxel Smash/Level List")]
    public sealed class LevelList : ScriptableObject
    {
        public VoxelModel[] levels;
        // The reference campaign has 42 levels; until every model is imported the samples repeat.
        public int campaignLength = 42;

        public int Count => levels?.Length ?? 0;
        public int Length => Mathf.Max(Count, campaignLength);
        public VoxelModel ModelFor(int number) => levels[(number - 1) % Count];
    }
}
