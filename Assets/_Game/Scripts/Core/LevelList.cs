using UnityEngine;

namespace VoxelSmash.Core
{
    [CreateAssetMenu(menuName = "Voxel Smash/Level List")]
    public sealed class LevelList : ScriptableObject
    {
        public const int BossEvery = 5;

        public VoxelModel[] levels;
        // Oversized sculptures for every fifth level; regular models fill the levels in between.
        public VoxelModel[] bosses;
        // The campaign is longer than the model list; models repeat in order.
        public int campaignLength = 42;

        public int Count => levels?.Length ?? 0;
        public int Length => Mathf.Max(Count, campaignLength);

        public bool IsBoss(int number) => bosses is { Length: > 0 } && number % BossEvery == 0;

        public VoxelModel ModelFor(int number)
        {
            if (IsBoss(number)) return bosses[(number / BossEvery - 1) % bosses.Length];
            // Count only the regular levels before this one so the regular rotation never skips a model.
            int regular = bosses is { Length: > 0 } ? number - 1 - (number - 1) / BossEvery : number - 1;
            return levels[regular % Count];
        }
    }
}
