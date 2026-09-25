using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using JobCheck.Domain;
using JobCheck.Persistence;
using NUnit.Framework;

namespace JobCheck.Tests
{
    /// <summary>
    /// 驗證 V0.2 寫入入口的事件追加、重複投遞、整理欄位與失敗不落盤。
    /// </summary>
    public sealed class ApplicationCommandServiceTests
    {
        private static readonly DateTimeOffset FirstTime =
            new DateTimeOffset(2026, 9, 9, 9, 0, 0, TimeSpan.FromHours(8));

        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(
                Path.GetTempPath(),
                "JobCheckCommandTests_" + Guid.NewGuid().ToString("N"));
            PersistenceStorageResult<PersistenceWriteSummary> write =
                JobCheckDataRepository.WriteSnapshot(root, CreateBaseDataSet());
            Assert.IsTrue(write.IsSuccess, FormatIssues(write));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void RecordSaved_CreatesApplicationAndEvent()
        {
            PersistenceStorageResult<ApplicationWriteSummary> result = Record(
                ApplicationEventType.Saved,
                EventActor.Candidate,
                FirstTime);

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            JobCheckDataSet data = Load();
            Assert.AreEqual(1, data.Applications.Count);
            Assert.AreEqual(ApplicationStage.Saved, data.Applications[0].CurrentStage);
            Assert.AreEqual(ApplicationEventType.Saved, data.ApplicationEvents[0].EventType);
        }

        [Test]
        public void RecordHistoricalFirstEvent_UsesOccurredAtAsApplicationCreatedAt()
        {
            DateTimeOffset historicalDate = FirstTime.AddMonths(-3);

            PersistenceStorageResult<ApplicationWriteSummary> result = Record(
                ApplicationEventType.Applied,
                EventActor.Candidate,
                historicalDate);

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            JobCheckDataSet data = Load();
            Application application = data.Applications.Single();
            ApplicationEvent applicationEvent = data.ApplicationEvents.Single();
            Assert.AreEqual(historicalDate, application.CreatedAt);
            Assert.AreEqual(historicalDate, applicationEvent.OccurredAt);
            Assert.GreaterOrEqual(applicationEvent.RecordedAt, application.CreatedAt);
        }

        [Test]
        public void RecordFutureFirstEvent_IsRejectedWithoutCreatingFile()
        {
            DateTimeOffset futureDate = new DateTimeOffset(
                2100,
                1,
                1,
                12,
                0,
                0,
                TimeSpan.FromHours(8));

            PersistenceStorageResult<ApplicationWriteSummary> result = Record(
                ApplicationEventType.Applied,
                EventActor.Candidate,
                futureDate);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsEmpty(Directory.GetFiles(
                Path.Combine(root, JobCheckDataRepository.ApplicationsDirectoryName)));
        }

        [Test]
        public void RecordApplied_AfterSaved_UsesSameApplication()
        {
            Record(ApplicationEventType.Saved, EventActor.Candidate, FirstTime);
            string originalId = Load().Applications.Single().Id;

            Record(ApplicationEventType.Applied, EventActor.Candidate, FirstTime.AddHours(1));

            JobCheckDataSet data = Load();
            Assert.AreEqual(1, data.Applications.Count);
            Assert.AreEqual(originalId, data.Applications.Single().Id);
            Assert.AreEqual(ApplicationStage.Applied, data.Applications.Single().CurrentStage);
            Assert.AreEqual(2, data.ApplicationEvents.Count);
        }

        [Test]
        public void RecordEarlierHistoricalEvent_MovesApplicationCreatedAtBackward()
        {
            Record(ApplicationEventType.Applied, EventActor.Candidate, FirstTime);
            DateTimeOffset earlier = FirstTime.AddDays(-1);

            PersistenceStorageResult<ApplicationWriteSummary> result = Record(
                ApplicationEventType.Saved,
                EventActor.Candidate,
                earlier);

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            JobCheckDataSet data = Load();
            Assert.AreEqual(earlier, data.Applications.Single().CreatedAt);
            Assert.AreEqual(ApplicationStage.Applied, data.Applications.Single().CurrentStage);
            Assert.AreEqual(
                new[] { ApplicationEventType.Saved, ApplicationEventType.Applied },
                data.ApplicationEvents
                    .OrderBy(item => item.OccurredAt)
                    .Select(item => item.EventType)
                    .ToArray());
        }

        [Test]
        public void RecordApplied_AfterAlreadyApplied_IsRejectedWithoutCreatingApplication()
        {
            Record(ApplicationEventType.Applied, EventActor.Candidate, FirstTime);

            PersistenceStorageResult<ApplicationWriteSummary> result = Record(
                ApplicationEventType.Applied,
                EventActor.Candidate,
                FirstTime.AddDays(1));

            JobCheckDataSet data = Load();
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(1, data.Applications.Count);
            Assert.AreEqual(1, data.ApplicationEvents.Count);
        }

        [Test]
        public void RecordApplied_AfterRejected_CreatesLinkedReapplication()
        {
            Record(ApplicationEventType.Applied, EventActor.Candidate, FirstTime);
            Record(
                ApplicationEventType.RejectedByCompany,
                EventActor.Company,
                FirstTime.AddHours(1));
            string firstId = Load().Applications.Single().Id;

            PersistenceStorageResult<ApplicationWriteSummary> result = Record(
                ApplicationEventType.Applied,
                EventActor.Candidate,
                FirstTime.AddDays(1));

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            JobCheckDataSet data = Load();
            Assert.AreEqual(2, data.Applications.Count);
            Application newest = data.Applications.Single(item => item.Id != firstId);
            Assert.AreEqual(firstId, newest.PreviousApplicationId);
            Assert.AreEqual(ApplicationStage.Applied, newest.CurrentStage);
            Assert.AreEqual(FirstTime.AddDays(1), newest.CreatedAt);
        }

        [Test]
        public void RecordApplied_ReapplicationBeforePreviousLastEvent_IsRejected()
        {
            Record(ApplicationEventType.Applied, EventActor.Candidate, FirstTime);
            Record(
                ApplicationEventType.RejectedByCompany,
                EventActor.Company,
                FirstTime.AddDays(1));

            PersistenceStorageResult<ApplicationWriteSummary> result = Record(
                ApplicationEventType.Applied,
                EventActor.Candidate,
                FirstTime.AddDays(-1));

            Assert.IsFalse(result.IsSuccess);
            JobCheckDataSet data = Load();
            Assert.AreEqual(1, data.Applications.Count);
            Assert.AreEqual(2, data.ApplicationEvents.Count);
        }

        [Test]
        public void RecordInterviewScheduled_PersistsScheduledFor()
        {
            Record(ApplicationEventType.Applied, EventActor.Candidate, FirstTime);
            DateTimeOffset appointment = FirstTime.AddDays(2);

            PersistenceStorageResult<ApplicationWriteSummary> result =
                ApplicationCommandService.RecordEvent(
                    root,
                    "demo_job_001",
                    ApplicationEventType.InterviewScheduled,
                    EventActor.Company,
                    FirstTime.AddHours(1),
                    appointment);

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            ApplicationEvent item = Load().ApplicationEvents.Single(
                value => value.EventType == ApplicationEventType.InterviewScheduled);
            Assert.AreEqual(appointment, item.ScheduledFor);
        }

        [Test]
        public void RecordNonInterview_WithScheduledFor_FailsWithoutChangingFile()
        {
            Record(ApplicationEventType.Saved, EventActor.Candidate, FirstTime);
            string path = GetOnlyApplicationPath();
            string before = File.ReadAllText(path);

            PersistenceStorageResult<ApplicationWriteSummary> result =
                ApplicationCommandService.RecordEvent(
                    root,
                    "demo_job_001",
                    ApplicationEventType.Applied,
                    EventActor.Candidate,
                    FirstTime.AddHours(1),
                    FirstTime.AddDays(1));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(before, File.ReadAllText(path));
        }

        [Test]
        public void CloseByCandidate_WithoutReason_FailsWithoutChangingFile()
        {
            Record(ApplicationEventType.Applied, EventActor.Candidate, FirstTime);
            string path = GetOnlyApplicationPath();
            string before = File.ReadAllText(path);

            PersistenceStorageResult<ApplicationWriteSummary> result = Record(
                ApplicationEventType.ClosedByCandidate,
                EventActor.Candidate,
                FirstTime.AddHours(1));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(before, File.ReadAllText(path));
        }

        [Test]
        public void CloseByCandidate_WithReason_PersistsReason()
        {
            Record(ApplicationEventType.Applied, EventActor.Candidate, FirstTime);

            PersistenceStorageResult<ApplicationWriteSummary> result =
                ApplicationCommandService.RecordEvent(
                    root,
                    "demo_job_001",
                    ApplicationEventType.ClosedByCandidate,
                    EventActor.Candidate,
                    FirstTime.AddHours(1),
                    closeReason: CandidateCloseReason.BetterOpportunity);

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            Application application = Load().Applications.Single();
            Assert.AreEqual(ApplicationStage.ClosedByCandidate, application.CurrentStage);
            Assert.AreEqual(CandidateCloseReason.BetterOpportunity, application.CandidateCloseReason);
        }

        [Test]
        public void EventAfterTerminal_ExceptAppliedReapplication_IsRejected()
        {
            Record(ApplicationEventType.Applied, EventActor.Candidate, FirstTime);
            Record(ApplicationEventType.RejectedByCompany, EventActor.Company, FirstTime.AddHours(1));

            PersistenceStorageResult<ApplicationWriteSummary> result = Record(
                ApplicationEventType.Contacted,
                EventActor.Company,
                FirstTime.AddHours(2));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(2, Load().ApplicationEvents.Count);
        }

        [Test]
        public void SetFavorite_PersistsWithoutAddingEvent()
        {
            Record(ApplicationEventType.Saved, EventActor.Candidate, FirstTime);

            PersistenceStorageResult<ApplicationWriteSummary> result =
                ApplicationCommandService.SetFavorite(root, "demo_job_001", true);

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            JobCheckDataSet data = Load();
            Assert.IsTrue(data.Applications.Single().IsFavorite);
            Assert.AreEqual(1, data.ApplicationEvents.Count);
        }

        [Test]
        public void SetNotes_WhitespaceClearsValue()
        {
            Record(ApplicationEventType.Saved, EventActor.Candidate, FirstTime);
            ApplicationCommandService.SetNotes(root, "demo_job_001", "  待確認文化  ");
            Assert.AreEqual("待確認文化", Load().Applications.Single().Notes);

            ApplicationCommandService.SetNotes(root, "demo_job_001", "   ");
            Assert.IsNull(Load().Applications.Single().Notes);
        }

        [Test]
        public void SetManualFollowUp_PersistsAndCanClear()
        {
            Record(ApplicationEventType.Applied, EventActor.Candidate, FirstTime);
            DateTimeOffset followUp = FirstTime.AddDays(5);
            ApplicationCommandService.SetManualFollowUp(root, "demo_job_001", followUp);
            Assert.AreEqual(followUp, Load().Applications.Single().ManualFollowUpAt);

            ApplicationCommandService.SetManualFollowUp(root, "demo_job_001", null);
            Assert.IsNull(Load().Applications.Single().ManualFollowUpAt);
        }

        [Test]
        public void ArchiveActiveApplication_IsRejected()
        {
            Record(ApplicationEventType.Applied, EventActor.Candidate, FirstTime);

            PersistenceStorageResult<ApplicationWriteSummary> result =
                ApplicationCommandService.SetArchived(
                    root,
                    "demo_job_001",
                    true,
                    ArchiveReason.UserArchived);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(Load().Applications.Single().IsArchived);
        }

        [Test]
        public void UpdateWithoutApplication_IsRejected()
        {
            PersistenceStorageResult<ApplicationWriteSummary> result =
                ApplicationCommandService.SetFavorite(root, "demo_job_001", true);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsEmpty(Directory.GetFiles(
                Path.Combine(root, JobCheckDataRepository.ApplicationsDirectoryName)));
        }

        [Test]
        public void RecordUnknownJob_IsRejected()
        {
            PersistenceStorageResult<ApplicationWriteSummary> result =
                ApplicationCommandService.RecordEvent(
                    root,
                    "missing_job",
                    ApplicationEventType.Saved,
                    EventActor.Candidate,
                    FirstTime);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsEmpty(Directory.GetFiles(
                Path.Combine(root, JobCheckDataRepository.ApplicationsDirectoryName)));
        }

        [Test]
        public void SuccessfulWrites_LeaveNoTemporaryFiles()
        {
            Record(ApplicationEventType.Saved, EventActor.Candidate, FirstTime);
            ApplicationCommandService.SetFavorite(root, "demo_job_001", true);

            Assert.IsEmpty(Directory.GetFiles(root, "*.tmp-*", SearchOption.AllDirectories));
        }

        [Test]
        public void ReadOnlyQuery_ReturnsCurrentApplicationEventHistory()
        {
            Record(ApplicationEventType.Saved, EventActor.Candidate, FirstTime);
            Record(ApplicationEventType.Applied, EventActor.Candidate, FirstTime.AddHours(1));

            PersistenceStorageResult<JobPostingReadOnlyList> result =
                JobPostingReadOnlyQuery.Load(root);

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            Assert.AreEqual(2, result.Value.Items.Single().ApplicationEvents.Count);
        }

        [Test]
        public void ReadOnlyQuery_ReturnsAllLinkedApplicationRoundsWithoutDuplicatingJob()
        {
            Record(ApplicationEventType.Applied, EventActor.Candidate, FirstTime);
            Record(
                ApplicationEventType.RejectedByCompany,
                EventActor.Company,
                FirstTime.AddHours(1));
            Record(ApplicationEventType.Applied, EventActor.Candidate, FirstTime.AddDays(1));

            PersistenceStorageResult<JobPostingReadOnlyList> result =
                JobPostingReadOnlyQuery.Load(root);

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            JobPostingReadOnlyItem item = result.Value.Items.Single();
            Assert.AreEqual(1, result.Value.Items.Count);
            Assert.AreEqual(2, item.Applications.Count);
            Assert.AreEqual(3, item.ApplicationEvents.Count);
            Assert.AreEqual(
                item.Applications[0].Id,
                item.Applications[1].PreviousApplicationId);
            Assert.AreEqual(item.Applications[1].Id, item.CurrentApplication.Id);
        }

        [Test]
        public void ScheduledForDtoField_IsOptionalForExistingV02Json()
        {
            var field = typeof(ApplicationEventDto).GetField("scheduled_for");

            Assert.IsNotNull(field);
            Assert.IsTrue(Attribute.IsDefined(field, typeof(OptionalFieldAttribute)));
        }

        private PersistenceStorageResult<ApplicationWriteSummary> Record(
            ApplicationEventType eventType,
            EventActor actor,
            DateTimeOffset time)
        {
            return ApplicationCommandService.RecordEvent(
                root,
                "demo_job_001",
                eventType,
                actor,
                time);
        }

        private JobCheckDataSet Load()
        {
            PersistenceStorageResult<JobCheckDataSet> result = JobCheckDataRepository.Load(root);
            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            return result.Value;
        }

        private string GetOnlyApplicationPath()
        {
            return Directory.GetFiles(
                Path.Combine(root, JobCheckDataRepository.ApplicationsDirectoryName),
                "*.json").Single();
        }

        private static JobCheckDataSet CreateBaseDataSet()
        {
            var company = new Company
            {
                Id = "cmp_0123456789abcdef0123456789abcdef",
                Name = "測試公司"
            };
            var job = new JobPosting
            {
                Id = "demo_job_001",
                CompanyId = company.Id,
                Title = "Unity 工程師",
                Source = new JobSource
                {
                    Platform = "demo",
                    Url = "https://example.com/job"
                },
                CapturedAt = FirstTime
            };
            return new JobCheckDataSet(
                new[] { company },
                new[] { job },
                Array.Empty<Application>(),
                Array.Empty<ApplicationEvent>());
        }

        private static string FormatIssues<T>(PersistenceStorageResult<T> result)
            where T : class
        {
            return string.Join(" | ", result.Issues.Select(item =>
                item.Error + ":" + item.FieldPath + ":" + item.Message));
        }
    }
}
