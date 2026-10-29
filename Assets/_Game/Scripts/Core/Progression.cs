using System.Collections.Generic;
using System.Linq;

namespace VoxelSmash.Core
{
    // Campaign progress ported from the reference's progression.js: each finished level is stored on
    // its own, and a level is playable once every level before it is finished.
    public sealed class Progression
    {
        public const int MilestoneEvery = 5, MilestoneHits = 3;

        readonly SortedSet<int> finished = new();

        public int Total { get; }

        public Progression(int total, IEnumerable<int> finishedLevels = null)
        {
            Total = total;
            if (finishedLevels == null) return;
            foreach (int n in finishedLevels)
                if (n >= 1 && n <= total) finished.Add(n);
        }

        // Contiguous finished levels counted from level 1.
        public int Completed
        {
            get
            {
                int n = 0;
                while (n < Total && finished.Contains(n + 1)) n++;
                return n;
            }
        }

        public int ResumeLevel => System.Math.Max(1, System.Math.Min(Total, Completed + 1));
        public IEnumerable<int> FinishedLevels => finished;

        public bool IsFinished(int number) => finished.Contains(number);
        public bool CanPlay(int number) => number >= 1 && number <= Total && number <= Completed + 1;

        // Records a finish; true when this was the level's first finish.
        public bool Complete(int number) => CanPlay(number) && finished.Add(number);

        // A first finish that lands on every fifth cleared level grants bonus power hits.
        public bool IsMilestone(bool firstFinish) => firstFinish && Completed % MilestoneEvery == 0;

        public string Serialize() => string.Join(",", finished);

        // Reads the saved list; a legacy "next level" number (earlier builds) means 1..next-1 are finished.
        public static Progression Parse(int total, string saved, int legacyNextLevel = 0)
        {
            if (string.IsNullOrEmpty(saved))
                return new Progression(total, Enumerable.Range(1, System.Math.Max(0, legacyNextLevel - 1)));
            var levels = new List<int>();
            foreach (var part in saved.Split(','))
                if (int.TryParse(part, out int n)) levels.Add(n);
            return new Progression(total, levels);
        }
    }
}
