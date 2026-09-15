using System;
using System.Collections.Generic;
using System.Linq;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// 從既有 V0.2 Domain 快照建立分析報告。查詢沒有寫入副作用，
    /// 只統計本人應徵，並以事件代表的已發生事實計算漏斗里程碑。
    /// </summary>
    public static class ApplicationAnalyticsQuery
    {
        private static readonly AnalyticsFunnelStage[] FunnelOrder =
        {
            AnalyticsFunnelStage.Applied,
            AnalyticsFunnelStage.CompanyResponded,
            AnalyticsFunnelStage.Interview,
            AnalyticsFunnelStage.WaitingResponse,
            AnalyticsFunnelStage.OfferReceived
        };

        private static readonly AnalyticsOutcome[] OutcomeOrder =
        {
            AnalyticsOutcome.Active,
            AnalyticsOutcome.OfferReceived,
            AnalyticsOutcome.RejectedByCompany,
            AnalyticsOutcome.ClosedByCandidate
        };

        public static PersistenceStorageResult<ApplicationAnalyticsReport> Load(string dataRoot)
        {
            PersistenceStorageResult<JobCheckDataSet> load =
                JobCheckDataRepository.Load(dataRoot);
            if (!load.IsSuccess)
            {
                return new PersistenceStorageResult<ApplicationAnalyticsReport>(
                    null,
                    load.Issues);
            }

            return new PersistenceStorageResult<ApplicationAnalyticsReport>(
                Build(load.Value),
                Array.Empty<PersistenceStorageIssue>());
        }

        public static ApplicationAnalyticsReport Build(JobCheckDataSet dataSet)
        {
            IReadOnlyList<JobPosting> jobPostings = dataSet?.JobPostings
                ?? Array.Empty<JobPosting>();
            IReadOnlyList<Application> applications = dataSet?.Applications
                ?? Array.Empty<Application>();
            IReadOnlyList<ApplicationEvent> applicationEvents = dataSet?.ApplicationEvents
                ?? Array.Empty<ApplicationEvent>();

            var warningCounts = new Dictionary<AnalyticsDataWarningCode, int>();
            var jobsById = jobPostings
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var platformAccumulators = BuildPlatformAccumulators(jobPostings);
            var eventsByApplicationId = applicationEvents
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ApplicationId))
                .GroupBy(item => item.ApplicationId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<ApplicationEvent>)group.ToArray(),
                    StringComparer.Ordinal);

            var funnelCounts = FunnelOrder.ToDictionary(item => item, item => 0);
            var outcomeCounts = OutcomeOrder.ToDictionary(item => item, item => 0);
            var closeReasonCounts = new Dictionary<CandidateCloseReason, int>();
            int includedApplicationCount = 0;
            int activeApplicationCount = 0;
            int noResponseMarkedCount = 0;
            int needsReviewCount = 0;

            foreach (Application application in applications.Where(item => item != null))
            {
                if (application.SourceType != SourceType.MyApplication)
                {
                    Increment(warningCounts, AnalyticsDataWarningCode.NonPersonalApplicationExcluded);
                    continue;
                }

                if (application.IsArchived
                    && application.ArchiveReason == ArchiveReason.Duplicate)
                {
                    Increment(warningCounts, AnalyticsDataWarningCode.DuplicateApplicationExcluded);
                    continue;
                }

                if (application.NeedsReview)
                {
                    needsReviewCount++;
                    Increment(warningCounts, AnalyticsDataWarningCode.NeedsReviewApplicationExcluded);
                    continue;
                }

                if (application.CurrentStage == ApplicationStage.Unknown)
                {
                    Increment(warningCounts, AnalyticsDataWarningCode.UnknownStageApplicationExcluded);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(application.JobPostingId)
                    || !jobsById.TryGetValue(application.JobPostingId, out JobPosting jobPosting))
                {
                    Increment(warningCounts, AnalyticsDataWarningCode.MissingJobPostingExcluded);
                    continue;
                }

                includedApplicationCount++;
                IReadOnlyList<ApplicationEvent> events = eventsByApplicationId.TryGetValue(
                    application.Id ?? string.Empty,
                    out IReadOnlyList<ApplicationEvent> foundEvents)
                    ? foundEvents
                    : Array.Empty<ApplicationEvent>();
                ApplicationEvent[] effectiveEvents = SelectEffectiveEvents(events);
                bool migrationOnly = effectiveEvents.Length == 0
                    && events.Any(item => item.EventType == ApplicationEventType.MigrationSnapshot);
                if (migrationOnly)
                {
                    Increment(warningCounts, AnalyticsDataWarningCode.MigrationSnapshotOnly);
                }

                ApplicationMilestones milestones = CalculateMilestones(effectiveEvents);
                AddFunnelCounts(funnelCounts, milestones);
                if (milestones.NoResponseMarked)
                {
                    noResponseMarkedCount++;
                }

                AnalyticsOutcome outcome = ClassifyOutcome(application.CurrentStage);
                outcomeCounts[outcome]++;
                if (outcome == AnalyticsOutcome.Active)
                {
                    activeApplicationCount++;
                }

                if (outcome == AnalyticsOutcome.ClosedByCandidate
                    && application.CandidateCloseReason.HasValue)
                {
                    CandidateCloseReason reason = application.CandidateCloseReason.Value;
                    closeReasonCounts[reason] = closeReasonCounts.TryGetValue(reason, out int count)
                        ? count + 1
                        : 1;
                }

                string platformKey = NormalizePlatformKey(jobPosting.Source?.Platform);
                if (!platformAccumulators.TryGetValue(
                    platformKey,
                    out PlatformAccumulator platformAccumulator))
                {
                    platformAccumulator = new PlatformAccumulator(
                        NormalizePlatformDisplay(jobPosting.Source?.Platform));
                    platformAccumulators.Add(platformKey, platformAccumulator);
                }

                platformAccumulator.AddApplication(application.JobPostingId, milestones);
            }

            int appliedCount = funnelCounts[AnalyticsFunnelStage.Applied];
            FunnelMetric[] funnel = FunnelOrder
                .Select(stage => new FunnelMetric(
                    stage,
                    funnelCounts[stage],
                    appliedCount == 0
                        ? (double?)null
                        : (double)funnelCounts[stage] / appliedCount))
                .ToArray();
            PlatformPerformanceMetric[] platforms = platformAccumulators.Values
                .OrderByDescending(item => item.AppliedApplicationCount)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.ToMetric())
                .ToArray();
            OutcomeMetric[] outcomes = OutcomeOrder
                .Select(outcome => new OutcomeMetric(outcome, outcomeCounts[outcome]))
                .ToArray();
            CloseReasonMetric[] closeReasons = closeReasonCounts
                .OrderBy(item => item.Key)
                .Select(item => new CloseReasonMetric(item.Key, item.Value))
                .ToArray();
            AnalyticsDataWarning[] warnings = warningCounts
                .Where(item => item.Value > 0)
                .OrderBy(item => item.Key)
                .Select(item => new AnalyticsDataWarning(item.Key, item.Value))
                .ToArray();

            return new ApplicationAnalyticsReport(
                new AnalyticsSummary(
                    jobPostings.Count(item => item != null),
                    includedApplicationCount,
                    activeApplicationCount,
                    noResponseMarkedCount,
                    needsReviewCount),
                funnel,
                platforms,
                outcomes,
                closeReasons,
                warnings);
        }

        private static Dictionary<string, PlatformAccumulator> BuildPlatformAccumulators(
            IEnumerable<JobPosting> jobPostings)
        {
            var result = new Dictionary<string, PlatformAccumulator>(StringComparer.Ordinal);
            foreach (JobPosting jobPosting in jobPostings.Where(item => item != null))
            {
                string platform = jobPosting.Source?.Platform;
                string key = NormalizePlatformKey(platform);
                if (!result.TryGetValue(key, out PlatformAccumulator accumulator))
                {
                    accumulator = new PlatformAccumulator(NormalizePlatformDisplay(platform));
                    result.Add(key, accumulator);
                }

                accumulator.AddJobPosting();
            }

            return result;
        }

        private static ApplicationEvent[] SelectEffectiveEvents(
            IEnumerable<ApplicationEvent> events)
        {
            ApplicationEvent[] snapshot = events.Where(item => item != null).ToArray();
            var supersededIds = new HashSet<string>(
                snapshot
                    .Where(item => item.EventType == ApplicationEventType.DataCorrected)
                    .Select(item => item.SupersedesEventId)
                    .Where(item => !string.IsNullOrWhiteSpace(item)),
                StringComparer.Ordinal);

            return snapshot.Where(item =>
                    item.EventType != ApplicationEventType.MigrationSnapshot
                    && item.EventType != ApplicationEventType.DataCorrected
                    && !supersededIds.Contains(item.Id))
                .ToArray();
        }

        private static ApplicationMilestones CalculateMilestones(
            IEnumerable<ApplicationEvent> events)
        {
            var eventTypes = new HashSet<ApplicationEventType>(
                events.Select(item => item.EventType));
            bool applied = eventTypes.Contains(ApplicationEventType.Applied);
            bool responded = applied && eventTypes.Any(IsCompanyResponse);
            bool interviewed = applied && (eventTypes.Contains(ApplicationEventType.InterviewScheduled)
                || eventTypes.Contains(ApplicationEventType.InterviewCompleted));
            bool waiting = applied
                && eventTypes.Contains(ApplicationEventType.WaitingResponseStarted);
            bool offer = applied && eventTypes.Contains(ApplicationEventType.OfferReceived);
            bool noResponse = eventTypes.Contains(ApplicationEventType.NoResponseMarked);
            return new ApplicationMilestones(
                applied,
                responded,
                interviewed,
                waiting,
                offer,
                noResponse);
        }

        private static bool IsCompanyResponse(ApplicationEventType eventType)
        {
            return eventType == ApplicationEventType.Viewed
                || eventType == ApplicationEventType.Contacted
                || eventType == ApplicationEventType.InterviewScheduled
                || eventType == ApplicationEventType.InterviewCompleted
                || eventType == ApplicationEventType.WaitingResponseStarted
                || eventType == ApplicationEventType.RejectedByCompany
                || eventType == ApplicationEventType.OfferReceived;
        }

        private static void AddFunnelCounts(
            IDictionary<AnalyticsFunnelStage, int> counts,
            ApplicationMilestones milestones)
        {
            if (milestones.Applied) counts[AnalyticsFunnelStage.Applied]++;
            if (milestones.CompanyResponded) counts[AnalyticsFunnelStage.CompanyResponded]++;
            if (milestones.Interview) counts[AnalyticsFunnelStage.Interview]++;
            if (milestones.WaitingResponse) counts[AnalyticsFunnelStage.WaitingResponse]++;
            if (milestones.OfferReceived) counts[AnalyticsFunnelStage.OfferReceived]++;
        }

        private static AnalyticsOutcome ClassifyOutcome(ApplicationStage stage)
        {
            switch (stage)
            {
                case ApplicationStage.OfferReceived:
                    return AnalyticsOutcome.OfferReceived;
                case ApplicationStage.RejectedByCompany:
                    return AnalyticsOutcome.RejectedByCompany;
                case ApplicationStage.ClosedByCandidate:
                    return AnalyticsOutcome.ClosedByCandidate;
                default:
                    return AnalyticsOutcome.Active;
            }
        }

        private static string NormalizePlatformKey(string value)
        {
            return NormalizePlatformDisplay(value).ToUpperInvariant();
        }

        private static string NormalizePlatformDisplay(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "未設定平台";
            }

            return string.Join(
                " ",
                value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static void Increment(
            IDictionary<AnalyticsDataWarningCode, int> counts,
            AnalyticsDataWarningCode code)
        {
            counts[code] = counts.TryGetValue(code, out int current) ? current + 1 : 1;
        }

        private sealed class ApplicationMilestones
        {
            public ApplicationMilestones(
                bool applied,
                bool companyResponded,
                bool interview,
                bool waitingResponse,
                bool offerReceived,
                bool noResponseMarked)
            {
                Applied = applied;
                CompanyResponded = companyResponded;
                Interview = interview;
                WaitingResponse = waitingResponse;
                OfferReceived = offerReceived;
                NoResponseMarked = noResponseMarked;
            }

            public bool Applied { get; }
            public bool CompanyResponded { get; }
            public bool Interview { get; }
            public bool WaitingResponse { get; }
            public bool OfferReceived { get; }
            public bool NoResponseMarked { get; }
        }

        private sealed class PlatformAccumulator
        {
            private readonly HashSet<string> appliedJobPostingIds =
                new HashSet<string>(StringComparer.Ordinal);

            public PlatformAccumulator(string displayName)
            {
                DisplayName = displayName;
            }

            public string DisplayName { get; }
            public int JobPostingCount { get; private set; }
            public int AppliedApplicationCount { get; private set; }
            public int RespondedApplicationCount { get; private set; }
            public int InterviewedApplicationCount { get; private set; }
            public int OfferApplicationCount { get; private set; }

            public void AddJobPosting()
            {
                JobPostingCount++;
            }

            public void AddApplication(string jobPostingId, ApplicationMilestones milestones)
            {
                if (!milestones.Applied)
                {
                    return;
                }

                AppliedApplicationCount++;
                appliedJobPostingIds.Add(jobPostingId);
                if (milestones.CompanyResponded) RespondedApplicationCount++;
                if (milestones.Interview) InterviewedApplicationCount++;
                if (milestones.OfferReceived) OfferApplicationCount++;
            }

            public PlatformPerformanceMetric ToMetric()
            {
                return new PlatformPerformanceMetric(
                    DisplayName,
                    JobPostingCount,
                    appliedJobPostingIds.Count,
                    AppliedApplicationCount,
                    RespondedApplicationCount,
                    InterviewedApplicationCount,
                    OfferApplicationCount);
            }
        }
    }
}
