using UnityEngine;

namespace VoxelSmash.Core
{
    // Gold, stars and upgrade maths. Each level starts with a purse; every strike costs its weapon's
    // price (WeaponDef.strikeCost) and every cube smashed pays in (more with a hit combo and the
    // coin-magnet upgrade, double on boss levels). A level pays about one coin per eight cubes whatever
    // the weapon, and the prices are set near what a careless strike earns, so the purse is the margin:
    // aim well (topple, pick the right weapon for what is left) and it grows; waste strikes and it runs
    // dry and the level is lost. Leftover gold is banked on a win and buys strike-size and magnet
    // levels or power hits.
    public static class Economy
    {
        public const int MaxUpgrade = 10, PowerHitsPrice = 120, PowerHits = 3, MaxCombo = 10;
        public const float ComboWindow = 1.1f, CritChance = .12f, CritScale = 1.4f, PowerScale = 1.35f;
        const int SizeBase = 150, MagnetBase = 120, StarBonus = 20;

        public static int CoinsForHit(int cubes, int combo, int magnetLevel, bool boss)
        {
            float coins = cubes / 8f * (1f + .1f * (Mathf.Clamp(combo, 1, MaxCombo) - 1)) * (1f + .1f * magnetLevel);
            return Mathf.Max(cubes > 0 ? 1 : 0, Mathf.RoundToInt(coins * (boss ? 2f : 1f)));
        }

        public static int StarBonusCoins(int stars) => stars * StarBonus;

        // About a fifth of what the level pays out, plus enough for a couple of cheap strikes.
        public static int StartPurse(int totalCubes) => 30 + totalCubes / 40;

        // Gold moved from the bank to keep playing after running out.
        public static int ContinueGold(int totalCubes) => Mathf.Max(40, totalCubes / 40);

        // Par hits scale with the sculpture: roughly one glove hit per 260 cubes.
        public static int Par(int totalCubes) => Mathf.Max(3, Mathf.CeilToInt(totalCubes / 260f));

        public static int Stars(int hits, int totalCubes)
        {
            int par = Par(totalCubes);
            return hits <= par ? 3 : hits <= par * 2 ? 2 : 1;
        }

        public static int SizeCost(int level) => Cost(SizeBase, level);
        public static int MagnetCost(int level) => Cost(MagnetBase, level);
        public static float SizeMultiplier(int level) => 1f + .08f * Mathf.Clamp(level, 0, MaxUpgrade);

        static int Cost(int baseCost, int level) => Mathf.RoundToInt(baseCost * Mathf.Pow(1.55f, level) / 10f) * 10;
    }
}
