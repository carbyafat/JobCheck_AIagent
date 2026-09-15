using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// V0.2.2 分析的唯讀結果。這些數字由既有 Domain 快照計算，絕不寫回 JSON。
    /// </summary>
    public sealed class ApplicationAnalyticsReport
    {
        public ApplicationAnalyticsReport(
            AnalyticsSummary summary,
            IEnumerable<FunnelMetric> funnel,
            IEnumerable<PlatformPerformanceMetric> platforms,
            IEnumerable<OutcomeMetric> outcomes,
            IEnumerable<CloseReasonMetric> closeReasons,
            IEnumerable<AnalyticsDataWarning> warnings)
        {
            Summary = summary ?? new AnalyticsSummary(0, 0, 0, 0, 0);
            Funnel = Snapshot(funnel);
            Platforms = Snapshot(platforms);
            Outcomes = Snapshot(outcomes);
            CloseReasons = Snapshot(closeReasons);
            Warnings = Snapshot(warnings);
        }

        public AnalyticsSummary Summary { get; }
        public IReadOnlyList<FunnelMetric> Funnel { get; }
        public IReadOnlyList<PlatformPerformanceMetric> Platforms { get; }
        public IReadOnlyList<OutcomeMetric> Outcomes { get; }
        public IReadOnlyList<CloseReasonMetric> CloseReasons { get; }
        public IReadOnlyList<AnalyticsDataWarning> Warnings { get; }

        private static IReadOnlyList<T> Snapshot<T>(IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(
                source == null ? Array.Empty<T>() : source.ToArray());
        }
    }

    public sealed class AnalyticsSummary
    {
        public AnalyticsSummary(
            int jobPostingCount,
            int includedApplicationCount,
            int activeApplicationCount,
            int noResponseMarkedCount,
            int needsReviewCount)
        {
            JobPostingCount = jobPostingCount;
            IncludedApplicationCount = includedApplicationCount;
            ActiveApplicationCount = activeApplicationCount;
            NoResponseMarkedCount = noResponseMarkedCount;
            NeedsReviewCount = needsReviewCount;
        }

        public int JobPostingCount { get; }
        public int IncludedApplicationCount { get; }
        public int ActiveApplicationCount { get; }
        public int NoResponseMarkedCount { get; }
        public int NeedsReviewCount { get; }
    }

    public enum AnalyticsFunnelStage
    {
        Applied,
        CompanyResponded,
        Interview,
        WaitingResponse,
        OfferReceived
    }

    public sealed class FunnelMetric
    {
        public FunnelMetric(AnalyticsFunnelStage stage, int count, double? rateFromApplied)
        {
            Stage = stage;
            Count = count;
            RateFromApplied = rateFromApplied;
        }

        public AnalyticsFunnelStage Stage { get; }
        public int Count { get; }

        /// <summary>
        /// 相對「已投遞」輪次的比率；沒有任何投遞時為 null，UI 應顯示 —。
        /// </summary>
        public double? RateFromApplied { get; }
    }

    public sealed class PlatformPerformanceMetric
    {
        public PlatformPerformanceMetric(
            string platform,
            int jobPostingCount,
            int appliedJobPostingCount,
            int appliedApplicationCount,
            int respondedApplicationCount,
            int interviewedApplicationCount,
            int offerApplicationCount)
        {
            Platform = platform;
            JobPostingCount = jobPostingCount;
            AppliedJobPostingCount = appliedJobPostingCount;
            AppliedApplicationCount = appliedApplicationCount;
            RespondedApplicationCount = respondedApplicationCount;
            InterviewedApplicationCount = interviewedApplicationCount;
            OfferApplicationCount = offerApplicationCount;
        }

        public string Platform { get; }
        public int JobPostingCount { get; }
        public int AppliedJobPostingCount { get; }
        public int AppliedApplicationCount { get; }
        public int RespondedApplicationCount { get; }
        public int InterviewedApplicationCount { get; }
        public int OfferApplicationCount { get; }

        /// <summary>至少投遞過一次的職缺數 ÷ 此平台收錄職缺數。</summary>
        public double? JobPostingApplicationRate => Divide(
            AppliedJobPostingCount,
            JobPostingCount);

        public double? ResponseRate => Divide(
            RespondedApplicationCount,
            AppliedApplicationCount);

        public double? InterviewRate => Divide(
            InterviewedApplicationCount,
            AppliedApplicationCount);

        public double? OfferRate => Divide(
            OfferApplicationCount,
            AppliedApplicationCount);

        public bool HasSmallApplicationSample => AppliedApplicationCount < 5;

        private static double? Divide(int numerator, int denominator)
        {
            return denominator == 0 ? (double?)null : (double)numerator / denominator;
        }
    }

    public enum AnalyticsOutcome
    {
        Active,
        OfferReceived,
        RejectedByCompany,
        ClosedByCandidate
    }

    public sealed class OutcomeMetric
    {
        public OutcomeMetric(AnalyticsOutcome outcome, int count)
        {
            Outcome = outcome;
            Count = count;
        }

        public AnalyticsOutcome Outcome { get; }
        public int Count { get; }
    }

    public sealed class CloseReasonMetric
    {
        public CloseReasonMetric(CandidateCloseReason reason, int count)
        {
            Reason = reason;
            Count = count;
        }

        public CandidateCloseReason Reason { get; }
        public int Count { get; }
    }

    public enum AnalyticsDataWarningCode
    {
        NonPersonalApplicationExcluded,
        DuplicateApplicationExcluded,
        NeedsReviewApplicationExcluded,
        UnknownStageApplicationExcluded,
        MissingJobPostingExcluded,
        MigrationSnapshotOnly
    }

    public sealed class AnalyticsDataWarning
    {
        public AnalyticsDataWarning(AnalyticsDataWarningCode code, int count)
        {
            Code = code;
            Count = count;
        }

        public AnalyticsDataWarningCode Code { get; }
        public int Count { get; }
    }
}
