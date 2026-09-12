using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JobCheck.Domain;
using JobCheck.Persistence;
using NUnit.Framework;

namespace JobCheck.Tests
{
    /// <summary>
    /// 驗證新增職缺入口的公司重用、輸入清理、必要欄位與失敗不落盤。
    /// </summary>
    public sealed class JobPostingCommandServiceTests
    {
        private static readonly DateTimeOffset CapturedAt =
            new DateTimeOffset(2026, 9, 10, 14, 0, 0, TimeSpan.FromHours(8));

        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(
                Path.GetTempPath(),
                "JobCheckJobPostingCommandTests_" + Guid.NewGuid().ToString("N"));
            PersistenceStorageResult<PersistenceWriteSummary> write =
                JobCheckDataRepository.WriteSnapshot(root, EmptyDataSet());
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
        public void Create_NewCompany_WritesCompanyAndJob()
        {
            PersistenceStorageResult<JobPostingWriteSummary> result = Create();

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            Assert.IsTrue(result.Value.CompanyCreated);
            Assert.That(result.Value.CompanyId, Does.Match("^cmp_[0-9a-f]{32}$"));
            Assert.That(result.Value.JobPostingId, Does.Match("^job_[0-9a-f]{32}$"));

            JobCheckDataSet data = Load();
            Assert.AreEqual(1, data.Companies.Count);
            Assert.AreEqual(1, data.JobPostings.Count);
            Assert.AreEqual(result.Value.CompanyId, data.JobPostings.Single().CompanyId);
        }

        [Test]
        public void Create_EquivalentCompanyName_ReusesExistingCompany()
        {
            Create(companyName: "測試　公司");
            string companyId = Load().Companies.Single().Id;

            PersistenceStorageResult<JobPostingWriteSummary> result =
                Create(companyName: "  測試 公司  ", title: "後端工程師");

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            Assert.IsFalse(result.Value.CompanyCreated);
            Assert.AreEqual(companyId, result.Value.CompanyId);
            JobCheckDataSet data = Load();
            Assert.AreEqual(1, data.Companies.Count);
            Assert.AreEqual(2, data.JobPostings.Count);
        }

        [Test]
        public void Create_TrimsTextAndPreservesRawDescription()
        {
            PersistenceStorageResult<JobPostingWriteSummary> result = Create(
                companyName: "  測試公司  ",
                title: "  Unity 工程師  ",
                platform: "  104  ",
                url: "  https://example.com/jobs/1  ",
                rawDescription: "  完整職缺內容  ");

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            JobCheckDataSet data = Load();
            Assert.AreEqual("測試公司", data.Companies.Single().Name);
            Assert.AreEqual("Unity 工程師", data.JobPostings.Single().Title);
            Assert.AreEqual("104", data.JobPostings.Single().Source.Platform);
            Assert.AreEqual("https://example.com/jobs/1", data.JobPostings.Single().Source.Url);
            Assert.AreEqual("完整職缺內容", data.JobPostings.Single().RawDescription);
        }

        [TestCase(null, "Unity 工程師", "104", "company_name")]
        [TestCase("測試公司", " ", "104", "title")]
        [TestCase("測試公司", "Unity 工程師", "", "source.platform")]
        public void Create_MissingRequiredField_FailsWithoutFiles(
            string companyName,
            string title,
            string platform,
            string expectedField)
        {
            PersistenceStorageResult<JobPostingWriteSummary> result = Create(
                companyName,
                title,
                platform);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(expectedField, result.Issues.Single().FieldPath);
            AssertEntityDirectoriesAreEmpty();
        }

        [Test]
        public void Create_InvalidUrl_FailsWithoutCompanyOrJob()
        {
            PersistenceStorageResult<JobPostingWriteSummary> result =
                Create(url: "not-a-web-url");

            Assert.IsFalse(result.IsSuccess);
            Assert.That(result.Issues.Single().Message, Does.Contain("InvalidSourceUrl"));
            AssertEntityDirectoriesAreEmpty();
        }

        [Test]
        public void Create_WithoutUrl_IsAllowed()
        {
            PersistenceStorageResult<JobPostingWriteSummary> result = Create(url: null);

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            Assert.IsNull(Load().JobPostings.Single().Source.Url);
        }

        [Test]
        public void Create_DoesNotCreateApplication()
        {
            PersistenceStorageResult<JobPostingWriteSummary> result = Create();

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            JobCheckDataSet data = Load();
            Assert.IsEmpty(data.Applications);
            Assert.IsEmpty(data.ApplicationEvents);
        }

        [Test]
        public void Create_PersistsTagsAndRiskFlagsAfterReload()
        {
            PersistenceStorageResult<JobPostingWriteSummary> result = Create(
                tags: new[] { " unity ", "csharp", "UNITY" },
                riskFlags: new[] { "salary_opaque", " gambling_industry " });

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            JobPosting job = Load().JobPostings.Single();
            CollectionAssert.AreEqual(
                new[] { "unity", "csharp" },
                job.Tags);
            CollectionAssert.AreEqual(
                new[] { "salary_opaque", "gambling_industry" },
                job.RiskFlags);
        }

        [Test]
        public void SuccessfulCreate_LeavesNoTemporaryFiles()
        {
            PersistenceStorageResult<JobPostingWriteSummary> result = Create();

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            Assert.IsEmpty(Directory.GetFiles(root, "*.tmp-*", SearchOption.AllDirectories));
        }

        [Test]
        public void Update_ChangesEditableFieldsAndPreservesIdentityAndApplicationHistory()
        {
            PersistenceStorageResult<JobPostingWriteSummary> created = Create();
            string jobId = created.Value.JobPostingId;
            DateTimeOffset? capturedAt = Load().JobPostings.Single().CapturedAt;
            PersistenceStorageResult<ApplicationWriteSummary> applied =
                ApplicationCommandService.RecordEvent(
                    root,
                    jobId,
                    ApplicationEventType.Applied,
                    EventActor.Candidate,
                    capturedAt.Value.AddMinutes(5));
            Assert.IsTrue(applied.IsSuccess, FormatIssues(applied));

            PersistenceStorageResult<JobPostingWriteSummary> result = Update(
                jobId,
                companyName: "  新公司  ",
                title: "  資深 Unity 工程師  ",
                platform: "  公司官網  ",
                url: "  https://example.com/jobs/updated  ",
                rawDescription: "  更新後原文  ");

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            Assert.AreEqual(jobId, result.Value.JobPostingId);
            JobCheckDataSet data = Load();
            JobPosting job = data.JobPostings.Single();
            Assert.AreEqual(jobId, job.Id);
            Assert.AreEqual(capturedAt, job.CapturedAt);
            Assert.AreEqual("資深 Unity 工程師", job.Title);
            Assert.AreEqual("公司官網", job.Source.Platform);
            Assert.AreEqual("https://example.com/jobs/updated", job.Source.Url);
            Assert.AreEqual("更新後原文", job.RawDescription);
            Assert.AreEqual(applied.Value.ApplicationId, data.Applications.Single().Id);
            Assert.AreEqual(1, data.ApplicationEvents.Count);
        }

        [Test]
        public void Update_ToExistingEquivalentCompany_ReusesCompany()
        {
            string firstJobId = Create(companyName: "第一公司").Value.JobPostingId;
            string secondJobId = Create(companyName: "目標　公司", title: "第二職缺")
                .Value.JobPostingId;
            JobCheckDataSet before = Load();
            string targetCompanyId = before.JobPostings
                .Single(item => item.Id == secondJobId)
                .CompanyId;

            PersistenceStorageResult<JobPostingWriteSummary> result = Update(
                firstJobId,
                companyName: "  目標 公司  ");

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            Assert.IsFalse(result.Value.CompanyCreated);
            Assert.AreEqual(targetCompanyId, result.Value.CompanyId);
            JobCheckDataSet after = Load();
            Assert.AreEqual(2, after.Companies.Count);
            Assert.AreEqual(
                targetCompanyId,
                after.JobPostings.Single(item => item.Id == firstJobId).CompanyId);
        }

        [Test]
        public void Update_ReplacesLabelsAndPreservesUnknownLegacyValue()
        {
            string jobId = Create(
                tags: new[] { "unity" },
                riskFlags: new[] { "salary_opaque" }).Value.JobPostingId;

            PersistenceStorageResult<JobPostingWriteSummary> result = Update(
                jobId,
                tags: new[] { "csharp", "Legacy_Custom_Tag" },
                riskFlags: new[] { "weekend_duty" });

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            JobPosting job = Load().JobPostings.Single();
            CollectionAssert.AreEqual(
                new[] { "csharp", "Legacy_Custom_Tag" },
                job.Tags);
            CollectionAssert.AreEqual(new[] { "weekend_duty" }, job.RiskFlags);
        }

        [Test]
        public void Update_ToUnknownCompany_CreatesCompanyWithoutRenamingOriginal()
        {
            string jobId = Create(companyName: "原公司").Value.JobPostingId;

            PersistenceStorageResult<JobPostingWriteSummary> result = Update(
                jobId,
                companyName: "新公司");

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            Assert.IsTrue(result.Value.CompanyCreated);
            JobCheckDataSet data = Load();
            Assert.AreEqual(2, data.Companies.Count);
            Assert.That(data.Companies.Select(item => item.Name), Contains.Item("原公司"));
            Assert.That(data.Companies.Select(item => item.Name), Contains.Item("新公司"));
            Assert.AreEqual(result.Value.CompanyId, data.JobPostings.Single().CompanyId);
        }

        [Test]
        public void Update_InvalidUrl_DoesNotModifyJobOrCreateCompany()
        {
            string jobId = Create().Value.JobPostingId;
            JobPosting before = Load().JobPostings.Single();

            PersistenceStorageResult<JobPostingWriteSummary> result = Update(
                jobId,
                companyName: "不應建立的公司",
                title: "不應保存的職稱",
                url: "invalid-url");

            Assert.IsFalse(result.IsSuccess);
            JobCheckDataSet after = Load();
            Assert.AreEqual(1, after.Companies.Count);
            Assert.AreEqual(before.CompanyId, after.JobPostings.Single().CompanyId);
            Assert.AreEqual(before.Title, after.JobPostings.Single().Title);
            Assert.AreEqual(before.Source.Url, after.JobPostings.Single().Source.Url);
        }

        [Test]
        public void Update_UnknownJobId_FailsWithoutFilesChanged()
        {
            Create();

            PersistenceStorageResult<JobPostingWriteSummary> result =
                Update("job_00000000000000000022222222222222");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("job_posting_id", result.Issues.Single().FieldPath);
            JobCheckDataSet data = Load();
            Assert.AreEqual(1, data.Companies.Count);
            Assert.AreEqual(1, data.JobPostings.Count);
        }

        [Test]
        public void SuccessfulUpdate_LeavesNoTemporaryFiles()
        {
            string jobId = Create().Value.JobPostingId;

            PersistenceStorageResult<JobPostingWriteSummary> result =
                Update(jobId, rawDescription: "更新");

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            Assert.IsEmpty(Directory.GetFiles(root, "*.tmp-*", SearchOption.AllDirectories));
        }

        private PersistenceStorageResult<JobPostingWriteSummary> Create(
            string companyName = "測試公司",
            string title = "Unity 工程師",
            string platform = "104",
            string url = "https://example.com/jobs/1",
            string rawDescription = "職缺內容",
            IEnumerable<string> tags = null,
            IEnumerable<string> riskFlags = null)
        {
            return JobPostingCommandService.Create(
                root,
                new JobPostingCreateRequest
                {
                    CompanyName = companyName,
                    Title = title,
                    SourcePlatform = platform,
                    SourceUrl = url,
                    RawDescription = rawDescription,
                    Tags = tags == null ? new List<string>() : new List<string>(tags),
                    RiskFlags = riskFlags == null
                        ? new List<string>()
                        : new List<string>(riskFlags),
                    CapturedAt = CapturedAt
                });
        }

        private PersistenceStorageResult<JobPostingWriteSummary> Update(
            string jobPostingId,
            string companyName = "測試公司",
            string title = "Unity 工程師",
            string platform = "104",
            string url = "https://example.com/jobs/1",
            string rawDescription = "職缺內容",
            IEnumerable<string> tags = null,
            IEnumerable<string> riskFlags = null)
        {
            return JobPostingCommandService.Update(
                root,
                new JobPostingEditRequest
                {
                    JobPostingId = jobPostingId,
                    CompanyName = companyName,
                    Title = title,
                    SourcePlatform = platform,
                    SourceUrl = url,
                    RawDescription = rawDescription,
                    Tags = tags == null ? new List<string>() : new List<string>(tags),
                    RiskFlags = riskFlags == null
                        ? new List<string>()
                        : new List<string>(riskFlags)
                });
        }

        private JobCheckDataSet Load()
        {
            PersistenceStorageResult<JobCheckDataSet> result = JobCheckDataRepository.Load(root);
            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            return result.Value;
        }

        private void AssertEntityDirectoriesAreEmpty()
        {
            Assert.IsEmpty(Directory.GetFiles(
                Path.Combine(root, JobCheckDataRepository.CompaniesDirectoryName)));
            Assert.IsEmpty(Directory.GetFiles(
                Path.Combine(root, JobCheckDataRepository.JobsDirectoryName)));
        }

        private static JobCheckDataSet EmptyDataSet()
        {
            return new JobCheckDataSet(
                Array.Empty<Company>(),
                Array.Empty<JobPosting>(),
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
