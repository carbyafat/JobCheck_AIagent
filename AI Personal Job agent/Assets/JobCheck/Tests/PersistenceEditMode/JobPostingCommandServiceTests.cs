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
            Assert.That(
                result.Issues.Single().Message,
                Does.Contain("來源網址必須是有效的 HTTP 或 HTTPS 網址。"));
            Assert.That(result.Issues.Single().Message, Does.Not.Contain("InvalidSourceUrl"));
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
        public void Create_PersistsStructuredRealJobFieldsAfterReload()
        {
            DateTimeOffset historicalCapturedAt = CapturedAt.AddMonths(-2);
            var request = CreateStructuredRequest(historicalCapturedAt);

            PersistenceStorageResult<JobPostingWriteSummary> result =
                JobPostingCommandService.Create(root, request);

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            JobPosting job = Load().JobPostings.Single();
            Assert.AreEqual(historicalCapturedAt, job.CapturedAt);
            Assert.AreEqual("研發部", job.Department);
            Assert.AreEqual("軟體工程", job.Category);
            Assert.AreEqual("range", job.Compensation.Type);
            Assert.AreEqual("monthly", job.Compensation.Period);
            Assert.AreEqual(50000, job.Compensation.Minimum);
            Assert.AreEqual(80000, job.Compensation.Maximum);
            Assert.AreEqual("TWD", job.Compensation.Currency);
            Assert.AreEqual("月薪 50,000 至 80,000 元", job.Compensation.RawText);
            Assert.AreEqual("台北市信義區", job.Location.RawText);
            Assert.AreEqual("hybrid", job.Location.WorkMode);
            Assert.AreEqual("全職", job.WorkConditions.EmploymentType);
            Assert.AreEqual("日班", job.WorkConditions.WorkingHours);
            Assert.AreEqual("兩年以上", job.Requirements.Experience);
            Assert.AreEqual("大學", job.Requirements.Education);
            CollectionAssert.AreEqual(
                new[] { "維護 Unity 專案", "撰寫測試" },
                job.Responsibilities);
            CollectionAssert.AreEqual(new[] { "Unity", "Git" }, job.Requirements.Tools);
            CollectionAssert.AreEqual(new[] { "溝通", "除錯" }, job.Requirements.Skills);
        }

        [Test]
        public void Create_ExplicitYearsAndDegree_BecomeAssessableRequirements()
        {
            var request = new JobPostingCreateRequest
            {
                CompanyName = "測試公司",
                Title = "工程師",
                SourcePlatform = "測試來源",
                Experience = "3 年以上",
                Education = "學士以上（在學可）"
            };

            PersistenceStorageResult<JobPostingWriteSummary> result =
                JobPostingCommandService.Create(root, request);

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            JobRequirements requirements = Load().JobPostings.Single().Requirements;
            Assert.That(requirements.ExperienceRequirements.Single().MinimumMonths,
                Is.EqualTo(36));
            Assert.That(requirements.EducationRequirement.MinimumDegreeLevel,
                Is.EqualTo(DegreeLevel.Bachelor));
            Assert.That(requirements.EducationRequirement.AcceptsInProgress, Is.True);
        }

        [Test]
        public void Create_WithoutStructuredDetails_KeepsOptionalObjectsNull()
        {
            PersistenceStorageResult<JobPostingWriteSummary> result = Create();

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            JobPosting job = Load().JobPostings.Single();
            Assert.IsNull(job.Compensation);
            Assert.IsNull(job.Location);
            Assert.IsNull(job.WorkConditions);
            Assert.IsNull(job.Requirements);
            Assert.IsEmpty(job.Responsibilities);
        }

        [Test]
        public void Create_CompensationMaximumBelowMinimum_FailsWithoutFiles()
        {
            JobPostingCreateRequest request = CreateStructuredRequest(CapturedAt);
            request.CompensationMinimum = 80000;
            request.CompensationMaximum = 50000;

            PersistenceStorageResult<JobPostingWriteSummary> result =
                JobPostingCommandService.Create(root, request);

            Assert.IsFalse(result.IsSuccess);
            Assert.That(
                result.Issues.Single().Message,
                Does.Contain("薪資上限不可低於薪資下限。"));
            Assert.That(
                result.Issues.Single().Message,
                Does.Not.Contain("CompensationMaximumBelowMinimum"));
            AssertEntityDirectoriesAreEmpty();
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
        public void Update_StructuredFields_PreservesNestedFieldsNotExposedByRequest()
        {
            string jobId = JobPostingCommandService.Create(
                root,
                CreateStructuredRequest(CapturedAt)).Value.JobPostingId;
            JobPosting seeded = Load().JobPostings.Single();
            seeded.Compensation.Notes = "含績效獎金";
            seeded.Location.City = "台北市";
            seeded.Location.RemoteAllowed = true;
            seeded.WorkConditions.BusinessTrip = "偶爾出差";
            seeded.Requirements.Major = "資訊相關";
            seeded.Requirements.OtherConditions.Add("需附作品集");
            PersistenceStorageResult<JobPostingWriteSummary> seedResult =
                JobCheckDataRepository.UpdateJobPosting(root, null, seeded);
            Assert.IsTrue(seedResult.IsSuccess, FormatIssues(seedResult));

            DateTimeOffset correctedCapturedAt = CapturedAt.AddDays(-10);
            var request = new JobPostingEditRequest
            {
                JobPostingId = jobId,
                CompanyName = "測試公司",
                Title = "資深 Unity 工程師",
                SourcePlatform = "104",
                SourceUrl = "https://example.com/jobs/1",
                CapturedAt = correctedCapturedAt,
                CompensationType = "fixed",
                CompensationPeriod = "monthly",
                CompensationMinimum = 70000,
                CompensationMaximum = 70000,
                CompensationCurrency = "TWD",
                CompensationRawText = "月薪 70,000 元",
                LocationRawText = "新北市板橋區",
                WorkMode = "onsite",
                EmploymentType = "全職",
                WorkingHours = "日班",
                Experience = "三年以上",
                Education = "專科以上",
                Responsibilities = new List<string> { "帶領專案" },
                Tools = new List<string> { "Unity" },
                Skills = new List<string> { "系統設計" }
            };

            PersistenceStorageResult<JobPostingWriteSummary> result =
                JobPostingCommandService.Update(root, request);

            Assert.IsTrue(result.IsSuccess, FormatIssues(result));
            JobPosting job = Load().JobPostings.Single();
            Assert.AreEqual(correctedCapturedAt, job.CapturedAt);
            Assert.AreEqual("含績效獎金", job.Compensation.Notes);
            Assert.AreEqual("台北市", job.Location.City);
            Assert.AreEqual(true, job.Location.RemoteAllowed);
            Assert.AreEqual("偶爾出差", job.WorkConditions.BusinessTrip);
            Assert.AreEqual("資訊相關", job.Requirements.Major);
            CollectionAssert.AreEqual(
                new[] { "需附作品集" },
                job.Requirements.OtherConditions);
            CollectionAssert.AreEqual(new[] { "帶領專案" }, job.Responsibilities);
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

        private static JobPostingCreateRequest CreateStructuredRequest(
            DateTimeOffset capturedAt)
        {
            return new JobPostingCreateRequest
            {
                CompanyName = "測試公司",
                Title = "Unity 工程師",
                SourcePlatform = "104",
                SourceUrl = "https://example.com/jobs/1",
                RawDescription = "完整職缺原文",
                CapturedAt = capturedAt,
                Department = " 研發部 ",
                Category = " 軟體工程 ",
                CompensationType = " range ",
                CompensationPeriod = " monthly ",
                CompensationMinimum = 50000,
                CompensationMaximum = 80000,
                CompensationCurrency = " TWD ",
                CompensationRawText = " 月薪 50,000 至 80,000 元 ",
                LocationRawText = " 台北市信義區 ",
                WorkMode = " hybrid ",
                EmploymentType = " 全職 ",
                WorkingHours = " 日班 ",
                Experience = " 兩年以上 ",
                Education = " 大學 ",
                Responsibilities = new List<string>
                {
                    " 維護 Unity 專案 ",
                    "撰寫測試",
                    "維護 Unity 專案"
                },
                Tools = new List<string> { " Unity ", "Git", "unity" },
                Skills = new List<string> { "溝通", " 除錯 " }
            };
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
