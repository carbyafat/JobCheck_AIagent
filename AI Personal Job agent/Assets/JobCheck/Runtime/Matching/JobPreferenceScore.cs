using System;
using System.Collections.Generic;
using System.Linq;

namespace JobCheck.Domain
{
    [Serializable]
    public sealed class JobPreferenceScore
    {
        /// <summary>沒有任何可判斷條件時為 null，不把資料不足顯示成 0 分。</summary>
        public int? Score { get; set; }
        public int AssessableCount { get; set; }
        public int MatchCount { get; set; }
        public int PartialMatchCount { get; set; }
        public int ConfirmedMismatchCount { get; set; }
        public int UnknownCount { get; set; }
        public int AiContextCount { get; set; }
        public List<JobPreferenceMatchItem> RequiredMismatches { get; set; }
            = new List<JobPreferenceMatchItem>();
    }

    /// <summary>必要條件權重較高；資料未知與 AI 上下文不參與計分。</summary>
    public static class JobPreferenceScoreCalculator
    {
        public static JobPreferenceScore Calculate(JobPreferenceMatchResult result)
        {
            var score = new JobPreferenceScore();
            List<JobPreferenceMatchItem> items = result?.Items?
                .Where(item => item != null).ToList()
                ?? new List<JobPreferenceMatchItem>();

            score.UnknownCount = items.Count(item =>
                item.Status == JobPreferenceMatchStatus.JobDataUnknown);
            score.AiContextCount = items.Count(item =>
                item.Status == JobPreferenceMatchStatus.AiContextOnly);

            List<JobPreferenceMatchItem> assessable = items.Where(item =>
                item.Status == JobPreferenceMatchStatus.Match
                || item.Status == JobPreferenceMatchStatus.PartialMatch
                || item.Status == JobPreferenceMatchStatus.ConfirmedMismatch)
                .Where(item => item.Importance != PreferenceImportance.Indifferent)
                .ToList();
            score.AssessableCount = assessable.Count;
            score.MatchCount = assessable.Count(item =>
                item.Status == JobPreferenceMatchStatus.Match);
            score.PartialMatchCount = assessable.Count(item =>
                item.Status == JobPreferenceMatchStatus.PartialMatch);
            score.ConfirmedMismatchCount = assessable.Count(item =>
                item.Status == JobPreferenceMatchStatus.ConfirmedMismatch);
            score.RequiredMismatches = assessable.Where(item =>
                item.Importance == PreferenceImportance.Required
                && item.Status == JobPreferenceMatchStatus.ConfirmedMismatch)
                .ToList();

            int availableWeight = 0;
            double earnedWeight = 0d;
            foreach (JobPreferenceMatchItem item in assessable)
            {
                int weight = item.Importance == PreferenceImportance.Required ? 2 : 1;
                availableWeight += weight;
                if (item.Status == JobPreferenceMatchStatus.Match)
                {
                    earnedWeight += weight;
                }
                else if (item.Status == JobPreferenceMatchStatus.PartialMatch)
                {
                    earnedWeight += weight * 0.5d;
                }
            }

            score.Score = availableWeight == 0
                ? (int?)null
                : (int)Math.Round(earnedWeight * 100d / availableWeight,
                    MidpointRounding.AwayFromZero);
            return score;
        }
    }
}
