using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    /// <summary>
    /// 驗證 V0.2.2 分析只使用真實事件、排除不可統計資料，且不寫回來源集合。
    /// </summary>
    public sealed class ApplicationAnalyticsQueryTests
    {
        private static readonly DateTimeOffset Time =
            new DateTimeOffset(2026, 9, 16, 9, 0, 0, TimeSpan.FromHours(8));

        [Test]
        public void Build_EmptyDataSet_ReturnsZeroesAndUndefinedRates()
        {
            ApplicationAnalyticsReport report = ApplicationAnalyticsQuery.Build(
                new JobCheckDataSet(null, null, null, null));

            Assert.That(report.Summary.JobPostingCount, Is.Zero);
            Assert.That(report.Summary.IncludedApplicationCount, Is.Zero);
            Assert.That(report.Funnel, Has.Count.EqualTo(5));
            Assert.That(report.Funnel.All(item => item.Count == 0), Is.True);
            Assert.That(report.Funnel.All(item => item.RateFromApplied == null), Is.True);
            Assert.That(report.Platforms, Is.Empty);
            Assert.That(report.Warnings, Is.Empty);
        }

        [Test]
        public void Build_UsesEventsToPreservePreviouslyReachedMilestones()
        {
            JobPosting job = CreateJob("job_1", "104");
            Application application = CreateApplication(
                "app_1",
                job.Id,
                ApplicationStage.WaitingResponse);
            ApplicationAnalyticsReport report = Build(
                new[] { job },
                new[] { application },
                new[]
                {
                    CreateEvent(application.Id, "1", ApplicationEventType.Applied),
                    CreateEvent(application.Id, "2", ApplicationEventType.InterviewScheduled),
                    CreateEvent(application.Id, "3", ApplicationEventType.WaitingResponseStarted)
                });

            AssertFunnel(report, AnalyticsFunnelStage.Applied, 1);
            AssertFunnel(report, AnalyticsFunnelStage.CompanyResponded, 1);
            AssertFunnel(report, AnalyticsFunnelStage.Interview, 1);
            AssertFunnel(report, AnalyticsFunnelStage.WaitingResponse, 1);
            AssertFunnel(report, AnalyticsFunnelStage.OfferReceived, 0);
        }

        [Test]
        public void Build_InterviewAfterApplied_CountsAsCompanyResponseWithoutViewedEvent()
        {
            JobPosting job = CreateJob("job_1", "104");
            Application application = CreateApplication(
                "app_1",
                job.Id,
                ApplicationStage.InterviewScheduled);
            ApplicationAnalyticsReport report = Build(
                new[] { job },
                new[] { application },
                new[]
                {
                    CreateEvent(application.Id, "1", ApplicationEventType.Applied),
                    CreateEvent(application.Id, "2", ApplicationEventType.InterviewScheduled)
                });

            AssertFunnel(report, AnalyticsFunnelStage.CompanyResponded, 1);
            AssertFunnel(report, AnalyticsFunnelStage.Interview, 1);
        }

        [Test]
        public void Build_LaterCurrentStageWithoutAppliedEvent_DoesNotInventFunnelHistory()
        {
            JobPosting job = CreateJob("job_1", "104");
            Application application = CreateApplication(
                "app_1",
                job.Id,
                ApplicationStage.OfferReceived);
            ApplicationAnalyticsReport report = Build(
                new[] { job },
                new[] { application },
                new[] { CreateEvent(application.Id, "1", ApplicationEventType.OfferReceived) });

            AssertFunnel(report, AnalyticsFunnelStage.Applied, 0);
            AssertFunnel(report, AnalyticsFunnelStage.OfferReceived, 0);
            AssertOutcome(report, AnalyticsOutcome.OfferReceived, 1);
        }

        [Test]
        public void Build_ReapplicationCountsRoundsButJobPostingApplicationRateUsesUniqueJobs()
        {
            JobPosting job = CreateJob("job_1", "104");
            Application first = CreateApplication("app_1", job.Id, ApplicationStage.RejectedByCompany);
            Application second = CreateApplication("app_2", job.Id, ApplicationStage.Applied);
            ApplicationAnalyticsReport report = Build(
                new[] { job },
                new[] { first, second },
                new[]
                {
                    CreateEvent(first.Id, "1", ApplicationEventType.Applied),
                    CreateEvent(first.Id, "2", ApplicationEventType.RejectedByCompany),
                    CreateEvent(second.Id, "3", ApplicationEventType.Applied)
                });

            PlatformPerformanceMetric platform = report.Platforms.Single();
            Assert.That(report.Summary.IncludedApplicationCount, Is.EqualTo(2));
            Assert.That(platform.AppliedApplicationCount, Is.EqualTo(2));
            Assert.That(platform.AppliedJobPostingCount, Is.EqualTo(1));
            Assert.That(platform.JobPostingApplicationRate, Is.EqualTo(1d));
            Assert.That(platform.ResponseRate, Is.EqualTo(0.5d));
        }

        [Test]
        public void Build_ExcludesNonPersonalDuplicateNeedsReviewUnknownAndOrphanApplications()
        {
            JobPosting job = CreateJob("job_1", "104");
            Application included = CreateApplication("app_ok", job.Id, ApplicationStage.Applied);
            Application imported = CreateApplication("app_imported", job.Id, ApplicationStage.Applied);
            imported.SourceType = SourceType.ImportedApplication;
            Application duplicate = CreateApplication("app_duplicate", job.Id, ApplicationStage.Applied);
            duplicate.IsArchived = true;
            duplicate.ArchiveReason = ArchiveReason.Duplicate;
            Application review = CreateApplication("app_review", job.Id, ApplicationStage.Applied);
            review.NeedsReview = true;
            Application unknown = CreateApplication("app_unknown", job.Id, ApplicationStage.Unknown);
            Application orphan = CreateApplication("app_orphan", "missing_job", ApplicationStage.Applied);

            ApplicationAnalyticsReport report = Build(
                new[] { job },
                new[] { included, imported, duplicate, review, unknown, orphan },
                new[] { CreateEvent(included.Id, "1", ApplicationEventType.Applied) });

            Assert.That(report.Summary.IncludedApplicationCount, Is.EqualTo(1));
            Assert.That(report.Summary.NeedsReviewCount, Is.EqualTo(1));
            AssertWarning(report, AnalyticsDataWarningCode.NonPersonalApplicationExcluded, 1);
            AssertWarning(report, AnalyticsDataWarningCode.DuplicateApplicationExcluded, 1);
            AssertWarning(report, AnalyticsDataWarningCode.NeedsReviewApplicationExcluded, 1);
            AssertWarning(report, AnalyticsDataWarningCode.UnknownStageApplicationExcluded, 1);
            AssertWarning(report, AnalyticsDataWarningCode.MissingJobPostingExcluded, 1);
        }

        [Test]
        public void Build_NoResponseMarkIsIndicatorAndDoesNotCloseApplication()
        {
            JobPosting job = CreateJob("job_1", "104");
            Application application = CreateApplication(
                "app_1",
                job.Id,
                ApplicationStage.WaitingResponse);
            ApplicationAnalyticsReport report = Build(
                new[] { job },
                new[] { application },
                new[]
                {
                    CreateEvent(application.Id, "1", ApplicationEventType.Applied),
                    CreateEvent(application.Id, "2", ApplicationEventType.NoResponseMarked)
                });

            Assert.That(report.Summary.NoResponseMarkedCount, Is.EqualTo(1));
            Assert.That(report.Summary.ActiveApplicationCount, Is.EqualTo(1));
            AssertOutcome(report, AnalyticsOutcome.ClosedByCandidate, 0);
        }

        [Test]
        public void Build_CandidateClosureCountsStructuredReason()
        {
            JobPosting job = CreateJob("job_1", "公司官網");
            Application application = CreateApplication(
                "app_1",
                job.Id,
                ApplicationStage.ClosedByCandidate);
            application.CandidateCloseReason = CandidateCloseReason.NoResponse;

            ApplicationAnalyticsReport report = Build(
                new[] { job },
                new[] { application },
                Array.Empty<ApplicationEvent>());

            AssertOutcome(report, AnalyticsOutcome.ClosedByCandidate, 1);
            Assert.That(report.CloseReasons.Single().Reason, Is.EqualTo(CandidateCloseReason.NoResponse));
            Assert.That(report.CloseReasons.Single().Count, Is.EqualTo(1));
        }

        [Test]
        public void Build_NormalizesPlatformWhitespaceAndCase()
        {
            JobPosting firstJob = CreateJob("job_1", " LinkedIn ");
            JobPosting secondJob = CreateJob("job_2", "linkedin");
            Application first = CreateApplication("app_1", firstJob.Id, ApplicationStage.Applied);
            Application second = CreateApplication("app_2", secondJob.Id, ApplicationStage.Applied);
            ApplicationAnalyticsReport report = Build(
                new[] { firstJob, secondJob },
                new[] { first, second },
                new[]
                {
                    CreateEvent(first.Id, "1", ApplicationEventType.Applied),
                    CreateEvent(second.Id, "2", ApplicationEventType.Applied)
                });

            Assert.That(report.Platforms, Has.Count.EqualTo(1));
            Assert.That(report.Platforms[0].Platform, Is.EqualTo("LinkedIn"));
            Assert.That(report.Platforms[0].JobPostingCount, Is.EqualTo(2));
            Assert.That(report.Platforms[0].AppliedApplicationCount, Is.EqualTo(2));
        }

        [Test]
        public void Build_DataCorrectionRemovesSupersededEventFromAnalytics()
        {
            JobPosting job = CreateJob("job_1", "104");
            Application application = CreateApplication("app_1", job.Id, ApplicationStage.Applied);
            ApplicationEvent applied = CreateEvent(
                application.Id,
                "1",
                ApplicationEventType.Applied);
            ApplicationEvent incorrectOffer = CreateEvent(
                application.Id,
                "2",
                ApplicationEventType.OfferReceived);
            ApplicationEvent correction = CreateEvent(
                application.Id,
                "3",
                ApplicationEventType.DataCorrected);
            correction.Actor = EventActor.System;
            correction.SupersedesEventId = incorrectOffer.Id;

            ApplicationAnalyticsReport report = Build(
                new[] { job },
                new[] { application },
                new[] { applied, incorrectOffer, correction });

            AssertFunnel(report, AnalyticsFunnelStage.Applied, 1);
            AssertFunnel(report, AnalyticsFunnelStage.OfferReceived, 0);
        }

        [Test]
        public void Build_MigrationSnapshotDoesNotInventFunnelEventsAndAddsWarning()
        {
            JobPosting job = CreateJob("job_1", "Demo");
            Application application = CreateApplication(
                "app_1",
                job.Id,
                ApplicationStage.WaitingResponse);
            ApplicationEvent snapshot = CreateEvent(
                application.Id,
                "1",
                ApplicationEventType.MigrationSnapshot);
            snapshot.Actor = EventActor.System;
            snapshot.SnapshotStage = ApplicationStage.WaitingResponse;

            ApplicationAnalyticsReport report = Build(
                new[] { job },
                new[] { application },
                new[] { snapshot });

            AssertFunnel(report, AnalyticsFunnelStage.Applied, 0);
            AssertFunnel(report, AnalyticsFunnelStage.WaitingResponse, 0);
            AssertWarning(report, AnalyticsDataWarningCode.MigrationSnapshotOnly, 1);
        }

        [Test]
        public void Load_MissingRootForwardsRepositoryFailureWithoutCreatingDirectory()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "JobCheckAnalyticsTests_" + Guid.NewGuid().ToString("N"));

            PersistenceStorageResult<ApplicationAnalyticsReport> result =
                ApplicationAnalyticsQuery.Load(root);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(
                result.Issues.Select(item => item.Error),
                Does.Contain(PersistenceStorageError.DataRootNotFound));
            Assert.That(Directory.Exists(root), Is.False);
        }

        private static ApplicationAnalyticsReport Build(
            IEnumerable<JobPosting> jobs,
            IEnumerable<Application> applications,
            IEnumerable<ApplicationEvent> events)
        {
            return ApplicationAnalyticsQuery.Build(new JobCheckDataSet(
                Array.Empty<Company>(),
                jobs,
                applications,
                events));
        }

        private static JobPosting CreateJob(string id, string platform)
        {
            return new JobPosting
            {
                Id = id,
                CompanyId = "company_1",
                Title = "工程師",
                Source = new JobSource { Platform = platform },
                CapturedAt = Time
            };
        }

        private static Application CreateApplication(
            string id,
            string jobPostingId,
            ApplicationStage stage)
        {
            return new Application
            {
                Id = id,
                JobPostingId = jobPostingId,
                SourceType = SourceType.MyApplication,
                CurrentStage = stage,
                CreatedAt = Time,
                UpdatedAt = Time
            };
        }

        private static ApplicationEvent CreateEvent(
            string applicationId,
            string suffix,
            ApplicationEventType eventType)
        {
            return new ApplicationEvent
            {
                Id = "event_" + suffix,
                ApplicationId = applicationId,
                EventType = eventType,
                OccurredAt = Time,
                RecordedAt = Time,
                Actor = EventActor.Candidate
            };
        }

        private static void AssertFunnel(
            ApplicationAnalyticsReport report,
            AnalyticsFunnelStage stage,
            int expected)
        {
            Assert.That(report.Funnel.Single(item => item.Stage == stage).Count, Is.EqualTo(expected));
        }

        private static void AssertOutcome(
            ApplicationAnalyticsReport report,
            AnalyticsOutcome outcome,
            int expected)
        {
            Assert.That(report.Outcomes.Single(item => item.Outcome == outcome).Count,
                Is.EqualTo(expected));
        }

        private static void AssertWarning(
            ApplicationAnalyticsReport report,
            AnalyticsDataWarningCode code,
            int expected)
        {
            Assert.That(report.Warnings.Single(item => item.Code == code).Count,
                Is.EqualTo(expected));
        }
    }
}
