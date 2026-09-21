using System;
using System.Collections.Generic;
using System.Linq;

namespace JobCheck.Domain
{
    [Serializable]
    public sealed class RequirementCategoryScore
    {
        public RequirementCategory Category { get; set; }
        public int ConfiguredWeight { get; set; }
        public int AssessableCount { get; set; }
        public int MatchCount { get; set; }
        public double EarnedWeight { get; set; }
    }

    [Serializable]
    public sealed class RequirementScore
    {
        /// <summary>沒有任何可判斷條件時為 null，不顯示假 0 分。</summary>
        public int? Score { get; set; }
        public int AssessableCount { get; set; }
        public int MatchCount { get; set; }
        public int NotEvidencedCount { get; set; }
        public int ConfirmedMismatchCount { get; set; }
        public int UnclearCount { get; set; }
        public double StrictMatchRate { get; set; }
        public List<RequirementCategoryScore> Categories { get; set; }
            = new List<RequirementCategoryScore>();
        public List<RequirementMatchItem> HardRequirementWarnings { get; set; }
            = new List<RequirementMatchItem>();
    }

    /// <summary>將比對結果即時計分；結果不寫回履歷或職缺資料。</summary>
    public static class RequirementScoreCalculator
    {
        public static RequirementScore Calculate(RequirementMatchResult result)
        {
            var score = new RequirementScore();
            List<RequirementMatchItem> items = result?.Items?
                .Where(item => item != null).ToList()
                ?? new List<RequirementMatchItem>();

            score.UnclearCount = items.Count(item =>
                item.Status == RequirementMatchStatus.RequirementUnclear);
            List<RequirementMatchItem> assessable = items.Where(item =>
                item.Status != RequirementMatchStatus.RequirementUnclear).ToList();
            score.AssessableCount = assessable.Count;
            score.MatchCount = assessable.Count(item =>
                item.Status == RequirementMatchStatus.Match);
            score.NotEvidencedCount = assessable.Count(item =>
                item.Status == RequirementMatchStatus.NotEvidenced);
            score.ConfirmedMismatchCount = assessable.Count(item =>
                item.Status == RequirementMatchStatus.ConfirmedMismatch);
            score.StrictMatchRate = score.AssessableCount == 0
                ? 0d
                : (double)score.MatchCount / score.AssessableCount;
            score.HardRequirementWarnings = assessable.Where(item =>
                item.Importance == RequirementImportance.Required
                && item.Status != RequirementMatchStatus.Match).ToList();

            int activeWeight = 0;
            double earnedWeight = 0d;
            foreach (RequirementCategory category in Enum.GetValues(
                typeof(RequirementCategory)))
            {
                int configuredWeight = Weight(category);
                List<RequirementMatchItem> categoryItems = assessable
                    .Where(item => item.Category == category).ToList();
                int matches = categoryItems.Count(item =>
                    item.Status == RequirementMatchStatus.Match);
                double earned = categoryItems.Count == 0
                    ? 0d
                    : configuredWeight * (double)matches / categoryItems.Count;
                score.Categories.Add(new RequirementCategoryScore
                {
                    Category = category,
                    ConfiguredWeight = configuredWeight,
                    AssessableCount = categoryItems.Count,
                    MatchCount = matches,
                    EarnedWeight = earned
                });
                if (categoryItems.Count > 0)
                {
                    activeWeight += configuredWeight;
                    earnedWeight += earned;
                }
            }

            score.Score = activeWeight == 0
                ? (int?)null
                : (int)Math.Round(earnedWeight * 100d / activeWeight,
                    MidpointRounding.AwayFromZero);
            return score;
        }

        private static int Weight(RequirementCategory category)
        {
            switch (category)
            {
                case RequirementCategory.Skill: return 40;
                case RequirementCategory.Experience: return 30;
                case RequirementCategory.Education: return 15;
                case RequirementCategory.Language: return 15;
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }
    }
}
