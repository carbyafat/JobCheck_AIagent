using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace JobCheck.Domain.Tests
{
    /// <summary>
    /// 驗證完整資料集合中的 ID 唯一性、核心關聯與前次投遞鏈規則。
    /// </summary>
    public class JobCheckDataSetTests
    {
        private static readonly DateTimeOffset BaseTime = new DateTimeOffset(
            2026,
            9,
            6,
            9,
            0,
            0,
            TimeSpan.FromHours(8));

        /// <summary>
        /// 確認所有跨模型關聯都成立時不會產生問題。
        /// </summary>
        [Test]
        public void Validate_CompleteDataSet_ReturnsValidResult()
        {
            TestData data = CreateValidData();

            JobCheckDataSetValidationResult result = Validate(data);

            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.Issues);
        }

        /// <summary>
        /// 確認兩家公司使用相同 ID 時會回報公司 ID 重複。
        /// </summary>
        [Test]
        public void Validate_DuplicateCompanyId_ReturnsIssue()
        {
            TestData data = CreateValidData();
            data.Companies.Add(new Company { Id = data.Company.Id, Name = "另一筆公司資料" });

            AssertHasError(Validate(data), JobCheckDataSetValidationError.DuplicateCompanyId);
        }

        /// <summary>
        /// 確認兩個職缺使用相同 ID 時會回報職缺 ID 重複。
        /// </summary>
        [Test]
        public void Validate_DuplicateJobPostingId_ReturnsIssue()
        {
            TestData data = CreateValidData();
            data.JobPostings.Add(new JobPosting
            {
                Id = data.JobPosting.Id,
                CompanyId = data.Company.Id
            });

            AssertHasError(Validate(data), JobCheckDataSetValidationError.DuplicateJobPostingId);
        }

        /// <summary>
        /// 確認兩筆應徵使用相同 ID 時會回報應徵 ID 重複。
        /// </summary>
        [Test]
        public void Validate_DuplicateApplicationId_ReturnsIssue()
        {
            TestData data = CreateValidData();
            data.Applications.Add(new Application
            {
                Id = data.Application.Id,
                JobPostingId = data.JobPosting.Id
            });

            AssertHasError(Validate(data), JobCheckDataSetValidationError.DuplicateApplicationId);
        }

        /// <summary>
        /// 確認兩筆事件使用相同 ID 時會回報事件 ID 重複。
        /// </summary>
        [Test]
        public void Validate_DuplicateApplicationEventId_ReturnsIssue()
        {
            TestData data = CreateValidData();
            data.ApplicationEvents.Add(new ApplicationEvent
            {
                Id = data.ApplicationEvent.Id,
                ApplicationId = data.Application.Id
            });

            AssertHasError(
                Validate(data),
                JobCheckDataSetValidationError.DuplicateApplicationEventId);
        }

        /// <summary>
        /// 確認職缺指向不存在的公司時，可定位到 JobPosting.CompanyId 與目標 ID。
        /// </summary>
        [Test]
        public void Validate_MissingJobPostingCompany_ReturnsLocatedIssue()
        {
            TestData data = CreateValidData();
            data.JobPosting.CompanyId = "cmp_missing";

            JobCheckDataSetValidationIssue issue = AssertHasError(
                Validate(data),
                JobCheckDataSetValidationError.JobPostingCompanyNotFound);

            Assert.AreEqual(JobCheckDataSetEntityType.JobPosting, issue.EntityType);
            Assert.AreEqual(data.JobPosting.Id, issue.EntityId);
            Assert.AreEqual(nameof(JobPosting.CompanyId), issue.FieldName);
            Assert.AreEqual("cmp_missing", issue.RelatedId);
        }

        /// <summary>
        /// 確認應徵指向不存在的職缺時會回報關聯錯誤。
        /// </summary>
        [Test]
        public void Validate_MissingApplicationJobPosting_ReturnsIssue()
        {
            TestData data = CreateValidData();
            data.Application.JobPostingId = "job_missing";

            AssertHasError(
                Validate(data),
                JobCheckDataSetValidationError.ApplicationJobPostingNotFound);
        }

        /// <summary>
        /// 確認事件指向不存在的應徵時會回報關聯錯誤。
        /// </summary>
        [Test]
        public void Validate_MissingApplicationEventOwner_ReturnsIssue()
        {
            TestData data = CreateValidData();
            data.ApplicationEvent.ApplicationId = "app_missing";

            AssertHasError(
                Validate(data),
                JobCheckDataSetValidationError.ApplicationEventApplicationNotFound);
        }

        /// <summary>
        /// 確認 PreviousApplicationId 指向不存在的應徵時會回報錯誤。
        /// </summary>
        [Test]
        public void Validate_PreviousApplicationNotFound_ReturnsIssue()
        {
            TestData data = CreateValidData();
            data.Application.PreviousApplicationId = "app_missing";

            AssertHasError(
                Validate(data),
                JobCheckDataSetValidationError.PreviousApplicationNotFound);
        }

        /// <summary>
        /// 確認 PreviousApplicationId 指向自己時會回報最短循環問題。
        /// </summary>
        [Test]
        public void Validate_PreviousApplicationReferencesSelf_ReturnsIssue()
        {
            TestData data = CreateValidData();
            data.Application.PreviousApplicationId = data.Application.Id;

            AssertHasError(
                Validate(data),
                JobCheckDataSetValidationError.PreviousApplicationReferencesSelf);
        }

        /// <summary>
        /// 確認前次投遞屬於另一個職缺時，不會被當成同一職缺的重複投遞鏈。
        /// </summary>
        [Test]
        public void Validate_PreviousApplicationForDifferentJobPosting_ReturnsIssue()
        {
            TestData data = CreateValidData();
            var otherPosting = new JobPosting
            {
                Id = JobPostingIdGenerator.Create(),
                CompanyId = data.Company.Id
            };
            var previous = new Application
            {
                Id = ApplicationIdGenerator.Create(),
                JobPostingId = otherPosting.Id
            };
            data.JobPostings.Add(otherPosting);
            data.Applications.Add(previous);
            data.Application.PreviousApplicationId = previous.Id;

            AssertHasError(
                Validate(data),
                JobCheckDataSetValidationError.PreviousApplicationBelongsToDifferentJobPosting);
        }

        /// <summary>
        /// 確認兩筆應徵彼此指為前次投遞時會回報完整集合才能發現的循環。
        /// </summary>
        [Test]
        public void Validate_TwoApplicationPreviousCycle_ReturnsIssue()
        {
            TestData data = CreateValidData();
            var previous = new Application
            {
                Id = ApplicationIdGenerator.Create(),
                JobPostingId = data.JobPosting.Id,
                PreviousApplicationId = data.Application.Id
            };
            data.Application.PreviousApplicationId = previous.Id;
            data.Applications.Add(previous);

            AssertHasError(
                Validate(data),
                JobCheckDataSetValidationError.PreviousApplicationCycle);
        }

        /// <summary>
        /// 確認 JobCheckDataSet 會固定集合成員，不會因來源 List 後續新增資料而改變。
        /// </summary>
        [Test]
        public void Constructor_SourceListChanges_DoesNotChangeDataSetSnapshot()
        {
            TestData data = CreateValidData();
            JobCheckDataSet dataSet = data.ToDataSet();

            data.Companies.Add(new Company { Id = CompanyIdGenerator.Create(), Name = "後加公司" });

            Assert.AreEqual(1, dataSet.Companies.Count);
        }

        /// <summary>
        /// 確認事件推算階段與 CurrentStage 不一致時，會回報可定位的快取錯誤與預期階段。
        /// </summary>
        [Test]
        public void Validate_CurrentStageDoesNotMatchEvents_ReturnsExpectedStage()
        {
            TestData data = CreateValidData();
            data.Application.CurrentStage = ApplicationStage.Saved;

            JobCheckDataSetValidationIssue issue = AssertHasError(
                Validate(data),
                JobCheckDataSetValidationError.ApplicationCurrentStageMismatch);

            Assert.AreEqual(nameof(Application.CurrentStage), issue.FieldName);
            Assert.AreEqual(ApplicationStage.Applied, issue.ExpectedStage);
        }

        /// <summary>
        /// 確認沒有任何事件的 Application 會保留 Reducer 的 EmptyEventHistory 原因。
        /// </summary>
        [Test]
        public void Validate_ApplicationWithoutEvents_ReturnsReducerIssue()
        {
            TestData data = CreateValidData();
            data.ApplicationEvents.Clear();

            AssertHasReductionIssue(
                Validate(data),
                ApplicationStateReductionIssue.EmptyEventHistory);
        }

        /// <summary>
        /// 確認必要時間缺漏的事件不會被拿來推算，並保留 InvalidEventData 原因。
        /// </summary>
        [Test]
        public void Validate_InvalidApplicationEvent_ReturnsReducerIssue()
        {
            TestData data = CreateValidData();
            data.ApplicationEvent.OccurredAt = null;

            AssertHasReductionIssue(
                Validate(data),
                ApplicationStateReductionIssue.InvalidEventData);
        }

        /// <summary>
        /// 確認兩個不同階段事件發生時間相同時，跨集合結果仍會保留 AmbiguousEventOrder。
        /// </summary>
        [Test]
        public void Validate_AmbiguousEventOrder_ReturnsReducerIssue()
        {
            TestData data = CreateValidData();
            data.ApplicationEvents.Add(CreateStageEvent(
                data.Application.Id,
                ApplicationEventType.Viewed,
                0));

            AssertHasReductionIssue(
                Validate(data),
                ApplicationStateReductionIssue.AmbiguousEventOrder);
        }

        /// <summary>
        /// 確認多筆 Application 只使用各自的事件推算，不會把另一筆應徵的進度混進來。
        /// </summary>
        [Test]
        public void Validate_TwoApplications_UsesEachApplicationOwnEvents()
        {
            TestData data = CreateValidData();
            var secondApplication = new Application
            {
                Id = ApplicationIdGenerator.Create(),
                JobPostingId = data.JobPosting.Id,
                CurrentStage = ApplicationStage.Viewed
            };
            data.Applications.Add(secondApplication);
            data.ApplicationEvents.Add(CreateStageEvent(
                secondApplication.Id,
                ApplicationEventType.Applied,
                1));
            data.ApplicationEvents.Add(CreateStageEvent(
                secondApplication.Id,
                ApplicationEventType.Viewed,
                2));

            JobCheckDataSetValidationResult result = Validate(data);

            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.Issues);
        }

        /// <summary>
        /// 確認事件輸入順序混亂時，Validator 與 Reducer 不會重新排列呼叫端的清單。
        /// </summary>
        [Test]
        public void Validate_UnorderedEvents_DoesNotReorderSourceList()
        {
            TestData data = CreateValidData();
            ApplicationEvent later = CreateStageEvent(
                data.Application.Id,
                ApplicationEventType.Viewed,
                10);
            ApplicationEvent earlier = data.ApplicationEvent;
            data.Application.CurrentStage = ApplicationStage.Viewed;
            data.ApplicationEvents.Clear();
            data.ApplicationEvents.Add(later);
            data.ApplicationEvents.Add(earlier);

            JobCheckDataSetValidator.Validate(data.ToDataSet());

            Assert.AreSame(later, data.ApplicationEvents[0]);
            Assert.AreSame(earlier, data.ApplicationEvents[1]);
        }

        /// <summary>
        /// 確認發現快取不一致時只回報問題，不會改寫 CurrentStage 或 NeedsReview。
        /// </summary>
        [Test]
        public void Validate_StageMismatch_DoesNotModifyApplication()
        {
            TestData data = CreateValidData();
            data.Application.CurrentStage = ApplicationStage.Saved;
            data.Application.NeedsReview = false;

            JobCheckDataSetValidator.Validate(data.ToDataSet());

            Assert.AreEqual(ApplicationStage.Saved, data.Application.CurrentStage);
            Assert.IsFalse(data.Application.NeedsReview);
        }

        /// <summary>
        /// 確認相同事件 ID 進入 Reducer 時，除了集合 ID 重複也保留 DuplicateEventId 推算原因。
        /// </summary>
        [Test]
        public void Validate_DuplicateEventId_PreservesReducerIssue()
        {
            TestData data = CreateValidData();
            ApplicationEvent duplicate = CreateStageEvent(
                data.Application.Id,
                ApplicationEventType.Viewed,
                1);
            duplicate.Id = data.ApplicationEvent.Id;
            data.ApplicationEvents.Add(duplicate);

            JobCheckDataSetValidationResult result = Validate(data);

            AssertHasError(
                result,
                JobCheckDataSetValidationError.DuplicateApplicationEventId);
            AssertHasReductionIssue(
                result,
                ApplicationStateReductionIssue.DuplicateEventId);
        }

        /// <summary>
        /// 執行跨集合驗證並回傳結果。
        /// </summary>
        private static JobCheckDataSetValidationResult Validate(TestData data)
        {
            return JobCheckDataSetValidator.Validate(data.ToDataSet());
        }

        /// <summary>
        /// 取得指定錯誤的第一筆問題；不存在時由 NUnit 顯示完整錯誤集合。
        /// </summary>
        private static JobCheckDataSetValidationIssue AssertHasError(
            JobCheckDataSetValidationResult result,
            JobCheckDataSetValidationError error)
        {
            JobCheckDataSetValidationIssue issue = result.Issues.FirstOrDefault(
                item => item.Error == error);

            Assert.IsNotNull(
                issue,
                "Expected error {0}, but got: {1}",
                error,
                string.Join(", ", result.Issues.Select(item => item.Error.ToString())));
            Assert.IsFalse(result.IsValid);
            return issue;
        }

        /// <summary>
        /// 取得指定 Reducer 原因對應的跨集合問題。
        /// </summary>
        private static JobCheckDataSetValidationIssue AssertHasReductionIssue(
            JobCheckDataSetValidationResult result,
            ApplicationStateReductionIssue reductionIssue)
        {
            JobCheckDataSetValidationIssue issue = result.Issues.FirstOrDefault(
                item =>
                    item.Error
                        == JobCheckDataSetValidationError.ApplicationEventHistoryNeedsReview
                    && item.StateReductionIssue == reductionIssue);

            Assert.IsNotNull(
                issue,
                "Expected reducer issue {0}, but got: {1}",
                reductionIssue,
                string.Join(
                    ", ",
                    result.Issues.Select(item =>
                        item.StateReductionIssue.HasValue
                            ? item.StateReductionIssue.Value.ToString()
                            : item.Error.ToString())));
            Assert.IsFalse(result.IsValid);
            return issue;
        }

        /// <summary>
        /// 建立具有一條 Company → JobPosting → Application → Event 關聯的最小集合。
        /// </summary>
        private static TestData CreateValidData()
        {
            var company = new Company
            {
                Id = CompanyIdGenerator.Create(),
                Name = "測試公司"
            };
            var jobPosting = new JobPosting
            {
                Id = JobPostingIdGenerator.Create(),
                CompanyId = company.Id
            };
            var application = new Application
            {
                Id = ApplicationIdGenerator.Create(),
                JobPostingId = jobPosting.Id,
                CurrentStage = ApplicationStage.Applied
            };
            ApplicationEvent applicationEvent = CreateStageEvent(
                application.Id,
                ApplicationEventType.Applied,
                0);

            return new TestData(company, jobPosting, application, applicationEvent);
        }

        /// <summary>
        /// 建立一筆具有完整必要欄位、可供 Reducer 使用的階段事件。
        /// </summary>
        private static ApplicationEvent CreateStageEvent(
            string applicationId,
            ApplicationEventType eventType,
            int occurredAfterMinutes)
        {
            DateTimeOffset occurredAt = BaseTime.AddMinutes(occurredAfterMinutes);
            return new ApplicationEvent
            {
                Id = ApplicationEventIdGenerator.Create(),
                ApplicationId = applicationId,
                EventType = eventType,
                OccurredAt = occurredAt,
                RecordedAt = occurredAt,
                Actor = EventActor.Candidate
            };
        }

        /// <summary>
        /// 集中保存測試用物件與可修改清單，讓每個案例只改動自己關心的欄位。
        /// </summary>
        private sealed class TestData
        {
            public TestData(
                Company company,
                JobPosting jobPosting,
                Application application,
                ApplicationEvent applicationEvent)
            {
                Company = company;
                JobPosting = jobPosting;
                Application = application;
                ApplicationEvent = applicationEvent;
                Companies = new List<Company> { company };
                JobPostings = new List<JobPosting> { jobPosting };
                Applications = new List<Application> { application };
                ApplicationEvents = new List<ApplicationEvent> { applicationEvent };
            }

            public Company Company { get; }
            public JobPosting JobPosting { get; }
            public Application Application { get; }
            public ApplicationEvent ApplicationEvent { get; }
            public List<Company> Companies { get; }
            public List<JobPosting> JobPostings { get; }
            public List<Application> Applications { get; }
            public List<ApplicationEvent> ApplicationEvents { get; }

            public JobCheckDataSet ToDataSet()
            {
                return new JobCheckDataSet(
                    Companies,
                    JobPostings,
                    Applications,
                    ApplicationEvents);
            }
        }
    }
}
