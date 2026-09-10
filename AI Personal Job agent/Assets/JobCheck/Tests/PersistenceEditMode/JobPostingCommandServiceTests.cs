using System;
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
        public void SuccessfulCreate_LeavesNoTemporaryFiles()
        {
            PersistenceStorageResult<JobPostingWriteSummary> result = Create();

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            Assert.IsEmpty(Directory.GetFiles(root, "*.tmp-*", SearchOption.AllDirectories));
        }

        private PersistenceStorageResult<JobPostingWriteSummary> Create(
            string companyName = "測試公司",
            string title = "Unity 工程師",
            string platform = "104",
            string url = "https://example.com/jobs/1",
            string rawDescription = "職缺內容")
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
                    CapturedAt = CapturedAt
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
