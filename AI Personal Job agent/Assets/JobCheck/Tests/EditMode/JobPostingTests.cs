using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace JobCheck.Domain.Tests
{
    /// <summary>
    /// 驗證 JobPosting 的新 ID、必要欄位、來源資料，以及 migration 保留舊 ID 的安全界線。
    /// </summary>
    public class JobPostingTests
    {
        /// <summary>
        /// 確認新 JobPosting ID 符合 job_ 加上 32 位小寫十六進位 UUID 的格式。
        /// </summary>
        [Test]
        public void JobPostingIdGenerator_Create_ReturnsExpectedFormat()
        {
            string jobPostingId = JobPostingIdGenerator.Create();

            StringAssert.IsMatch("^job_[0-9a-f]{32}$", jobPostingId);
        }

        /// <summary>
        /// 確認連續建立兩個 JobPosting ID 時，不會得到同一個識別碼。
        /// </summary>
        [Test]
        public void JobPostingIdGenerator_CreateTwice_ReturnsDifferentIds()
        {
            string firstId = JobPostingIdGenerator.Create();
            string secondId = JobPostingIdGenerator.Create();

            Assert.AreNotEqual(firstId, secondId);
        }

        /// <summary>
        /// 確認必要資料齊全的新職缺不會產生驗證錯誤。
        /// </summary>
        [Test]
        public void ValidateNew_ValidJobPosting_ReturnsNoErrors()
        {
            IReadOnlyList<JobPostingValidationError> errors =
                JobPostingValidator.ValidateNew(CreateValidJobPosting());

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// 確認不存在的 JobPosting 會回報 MissingJobPosting，而不是造成 null reference exception。
        /// </summary>
        [Test]
        public void ValidateNew_NullJobPosting_ReturnsMissingJobPosting()
        {
            IReadOnlyList<JobPostingValidationError> errors = JobPostingValidator.ValidateNew(null);

            CollectionAssert.Contains(errors, JobPostingValidationError.MissingJobPosting);
        }

        /// <summary>
        /// 確認缺少 ID 時會回報 MissingId。
        /// </summary>
        [Test]
        public void ValidateNew_MissingId_ReturnsMissingId()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Id = " ";

            IReadOnlyList<JobPostingValidationError> errors = JobPostingValidator.ValidateNew(jobPosting);

            CollectionAssert.Contains(errors, JobPostingValidationError.MissingId);
        }

        /// <summary>
        /// 確認新職缺不能使用不符合新版格式的 ID。
        /// </summary>
        [Test]
        public void ValidateNew_LegacyId_ReturnsInvalidNewId()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Id = "legacy-job-001";

            IReadOnlyList<JobPostingValidationError> errors = JobPostingValidator.ValidateNew(jobPosting);

            CollectionAssert.Contains(errors, JobPostingValidationError.InvalidNewId);
        }

        /// <summary>
        /// 確認 migration 可以保留不符合新版格式、但不含危險路徑內容的既有 job ID。
        /// </summary>
        [Test]
        public void ValidateForMigration_SafeLegacyId_ReturnsNoErrors()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Id = "legacy-job-001";

            IReadOnlyList<JobPostingValidationError> errors =
                JobPostingValidator.ValidateForMigration(jobPosting);

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// 確認 migration 不會接受含路徑分隔符或目錄跳脫符號的舊 ID。
        /// </summary>
        /// <param name="unsafeId">可能被誤作檔案路徑的不安全舊 ID。</param>
        [TestCase("legacy/job")]
        [TestCase("legacy\\job")]
        [TestCase("legacy..job")]
        public void ValidateForMigration_UnsafeLegacyId_ReturnsUnsafeLegacyId(string unsafeId)
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Id = unsafeId;

            IReadOnlyList<JobPostingValidationError> errors =
                JobPostingValidator.ValidateForMigration(jobPosting);

            CollectionAssert.Contains(errors, JobPostingValidationError.UnsafeLegacyId);
        }

        /// <summary>
        /// 確認未知或缺漏的 schema version 不會被靜默接受。
        /// </summary>
        [Test]
        public void ValidateNew_UnsupportedSchemaVersion_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.SchemaVersion = "0.3";

            IReadOnlyList<JobPostingValidationError> errors = JobPostingValidator.ValidateNew(jobPosting);

            CollectionAssert.Contains(errors, JobPostingValidationError.UnsupportedSchemaVersion);
        }

        /// <summary>
        /// 確認缺少 Company ID 時會回報錯誤。
        /// 是否真的存在對應 Company，則留給 Repository 的跨集合驗證。
        /// </summary>
        [Test]
        public void ValidateNew_MissingCompanyId_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.CompanyId = null;

            IReadOnlyList<JobPostingValidationError> errors = JobPostingValidator.ValidateNew(jobPosting);

            CollectionAssert.Contains(errors, JobPostingValidationError.MissingCompanyId);
        }

        /// <summary>
        /// 確認缺少職稱時會回報錯誤。
        /// </summary>
        [Test]
        public void ValidateNew_MissingTitle_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Title = "";

            IReadOnlyList<JobPostingValidationError> errors = JobPostingValidator.ValidateNew(jobPosting);

            CollectionAssert.Contains(errors, JobPostingValidationError.MissingTitle);
        }

        /// <summary>
        /// 確認缺少完整來源物件時會回報錯誤。
        /// </summary>
        [Test]
        public void ValidateNew_MissingSource_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Source = null;

            IReadOnlyList<JobPostingValidationError> errors = JobPostingValidator.ValidateNew(jobPosting);

            CollectionAssert.Contains(errors, JobPostingValidationError.MissingSource);
        }

        /// <summary>
        /// 確認來源物件存在但沒有平台名稱時，仍會回報錯誤。
        /// </summary>
        [Test]
        public void ValidateNew_MissingSourcePlatform_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Source.Platform = " ";

            IReadOnlyList<JobPostingValidationError> errors = JobPostingValidator.ValidateNew(jobPosting);

            CollectionAssert.Contains(errors, JobPostingValidationError.MissingSourcePlatform);
        }

        /// <summary>
        /// 確認有填寫的來源網址必須是 HTTP 或 HTTPS 絕對網址。
        /// </summary>
        [Test]
        public void ValidateNew_InvalidSourceUrl_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Source.Url = "not-a-url";

            IReadOnlyList<JobPostingValidationError> errors = JobPostingValidator.ValidateNew(jobPosting);

            CollectionAssert.Contains(errors, JobPostingValidationError.InvalidSourceUrl);
        }

        /// <summary>
        /// 確認來源確實沒有網址時可以留空，不會因系統自行捏造網址而通過驗證。
        /// </summary>
        [Test]
        public void ValidateNew_MissingOptionalSourceUrl_ReturnsNoErrors()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Source.Url = null;

            IReadOnlyList<JobPostingValidationError> errors = JobPostingValidator.ValidateNew(jobPosting);

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// 確認缺少擷取時間時會回報錯誤。
        /// </summary>
        [Test]
        public void ValidateNew_MissingCapturedAt_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.CapturedAt = null;

            IReadOnlyList<JobPostingValidationError> errors = JobPostingValidator.ValidateNew(jobPosting);

            CollectionAssert.Contains(errors, JobPostingValidationError.MissingCapturedAt);
        }

        /// <summary>
        /// 確認新 JobPosting 的多值 optional 欄位預設為空集合，避免使用端逐一判斷 null。
        /// </summary>
        [Test]
        public void JobPosting_NewInstance_InitializesDetailCollections()
        {
            var jobPosting = new JobPosting();

            Assert.IsNotNull(jobPosting.Responsibilities);
            Assert.IsNotNull(jobPosting.RecruitmentProcess);
            Assert.IsNotNull(jobPosting.Tags);
            Assert.IsNotNull(jobPosting.RiskFlags);
            Assert.IsEmpty(jobPosting.Responsibilities);
            Assert.IsEmpty(jobPosting.RecruitmentProcess);
        }

        /// <summary>
        /// 確認 Requirements 的語文、工具、技能與其他條件集合都能安全直接加入內容。
        /// </summary>
        [Test]
        public void JobRequirements_NewInstance_InitializesCollections()
        {
            var requirements = new JobRequirements();

            Assert.IsNotNull(requirements.Languages);
            Assert.IsNotNull(requirements.Tools);
            Assert.IsNotNull(requirements.Skills);
            Assert.IsNotNull(requirements.OtherConditions);
        }

        /// <summary>
        /// 確認 Benefits 的所有分類預設為空集合。
        /// </summary>
        [Test]
        public void JobBenefits_NewInstance_InitializesCollections()
        {
            var benefits = new JobBenefits();

            Assert.IsNotNull(benefits.SalaryBonus);
            Assert.IsNotNull(benefits.InsuranceHealth);
            Assert.IsNotNull(benefits.Flexibility);
            Assert.IsNotNull(benefits.Training);
            Assert.IsNotNull(benefits.Life);
            Assert.IsNotNull(benefits.Other);
        }

        /// <summary>
        /// 確認符合 V0.1 demo 形狀的完整詳細職缺可以通過 Domain 驗證。
        /// </summary>
        [Test]
        public void ValidateNew_FullyDetailedJobPosting_ReturnsNoErrors()
        {
            JobPosting jobPosting = CreateDetailedJobPosting();

            IReadOnlyList<JobPostingValidationError> errors =
                JobPostingValidator.ValidateNew(jobPosting);

            Assert.IsEmpty(errors);
            Assert.AreEqual(45000, jobPosting.Compensation.Minimum);
            Assert.AreEqual("信義區", jobPosting.Location.District);
            Assert.AreEqual("TypeScript", jobPosting.Requirements.Tools[1]);
            Assert.AreEqual("技術面談", jobPosting.RecruitmentProcess[1]);
        }

        /// <summary>
        /// 確認所有詳細物件與集合都缺漏時仍合法；optional 缺漏應由 migration report 列 warning。
        /// </summary>
        [Test]
        public void ValidateNew_MissingOptionalDetails_ReturnsNoErrors()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Responsibilities = null;
            jobPosting.RecruitmentProcess = null;
            jobPosting.Tags = null;
            jobPosting.RiskFlags = null;

            IReadOnlyList<JobPostingValidationError> errors =
                JobPostingValidator.ValidateNew(jobPosting);

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// 確認待遇面議或只公開最低薪時，薪資上限可以保持 null。
        /// </summary>
        [Test]
        public void ValidateNew_CompensationWithoutMaximum_ReturnsNoErrors()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Compensation = new JobCompensation
            {
                Type = "negotiable",
                Minimum = 40000,
                Maximum = null,
                Currency = "TWD"
            };

            IReadOnlyList<JobPostingValidationError> errors =
                JobPostingValidator.ValidateNew(jobPosting);

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// 確認薪資下限為負數時會回報錯誤。
        /// </summary>
        [Test]
        public void ValidateNew_NegativeCompensationMinimum_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Compensation = new JobCompensation { Minimum = -1 };

            IReadOnlyList<JobPostingValidationError> errors =
                JobPostingValidator.ValidateNew(jobPosting);

            CollectionAssert.Contains(
                errors,
                JobPostingValidationError.NegativeCompensationAmount);
        }

        /// <summary>
        /// 確認薪資上限為負數時也會回報錯誤。
        /// </summary>
        [Test]
        public void ValidateNew_NegativeCompensationMaximum_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Compensation = new JobCompensation { Maximum = -1 };

            IReadOnlyList<JobPostingValidationError> errors =
                JobPostingValidator.ValidateNew(jobPosting);

            CollectionAssert.Contains(
                errors,
                JobPostingValidationError.NegativeCompensationAmount);
        }

        /// <summary>
        /// 確認薪資上限低於下限時會回報反向範圍。
        /// </summary>
        [Test]
        public void ValidateNew_CompensationMaximumBelowMinimum_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Compensation = new JobCompensation
            {
                Minimum = 60000,
                Maximum = 50000
            };

            IReadOnlyList<JobPostingValidationError> errors =
                JobPostingValidator.ValidateNew(jobPosting);

            CollectionAssert.Contains(
                errors,
                JobPostingValidationError.CompensationMaximumBelowMinimum);
        }

        /// <summary>
        /// 確認工作內容集合不能混入空白項目。
        /// </summary>
        [Test]
        public void ValidateNew_BlankResponsibility_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Responsibilities.Add(" ");

            AssertContainsError(
                jobPosting,
                JobPostingValidationError.InvalidResponsibilityEntry);
        }

        /// <summary>
        /// 確認招募流程不能包含沒有內容的步驟。
        /// </summary>
        [Test]
        public void ValidateNew_BlankRecruitmentStep_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.RecruitmentProcess.Add(null);

            AssertContainsError(
                jobPosting,
                JobPostingValidationError.InvalidRecruitmentProcessEntry);
        }

        /// <summary>
        /// 確認職缺標籤不能包含空字串。
        /// </summary>
        [Test]
        public void ValidateNew_BlankTag_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Tags.Add(string.Empty);

            AssertContainsError(jobPosting, JobPostingValidationError.InvalidTagEntry);
        }

        /// <summary>
        /// 確認職缺風險標記不能包含純空白內容。
        /// </summary>
        [Test]
        public void ValidateNew_BlankRiskFlag_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.RiskFlags.Add("  ");

            AssertContainsError(jobPosting, JobPostingValidationError.InvalidRiskFlagEntry);
        }

        /// <summary>
        /// 確認工具、技能與其他條件集合不能混入空白項目。
        /// </summary>
        [Test]
        public void ValidateNew_BlankRequirementEntry_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Requirements = new JobRequirements();
            jobPosting.Requirements.Tools.Add(null);

            AssertContainsError(
                jobPosting,
                JobPostingValidationError.InvalidRequirementEntry);
        }

        /// <summary>
        /// 確認 Languages 集合不能包含 null 語文物件。
        /// </summary>
        [Test]
        public void ValidateNew_NullLanguageRequirement_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Requirements = new JobRequirements();
            jobPosting.Requirements.Languages.Add(null);

            AssertContainsError(
                jobPosting,
                JobPostingValidationError.InvalidLanguageRequirement);
        }

        /// <summary>
        /// 確認完全沒有名稱、能力或原始文字的語文條件不會被接受。
        /// </summary>
        [Test]
        public void ValidateNew_EmptyLanguageRequirement_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Requirements = new JobRequirements();
            jobPosting.Requirements.Languages.Add(new JobLanguageRequirement());

            AssertContainsError(
                jobPosting,
                JobPostingValidationError.InvalidLanguageRequirement);
        }

        /// <summary>
        /// 確認任一福利分類不能混入空白項目。
        /// </summary>
        [Test]
        public void ValidateNew_BlankBenefit_ReturnsError()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Benefits = new JobBenefits();
            jobPosting.Benefits.Training.Add(" ");

            AssertContainsError(jobPosting, JobPostingValidationError.InvalidBenefitEntry);
        }

        /// <summary>
        /// 驗證指定職缺並確認包含預期錯誤代碼。
        /// </summary>
        private static void AssertContainsError(
            JobPosting jobPosting,
            JobPostingValidationError expectedError)
        {
            IReadOnlyList<JobPostingValidationError> errors =
                JobPostingValidator.ValidateNew(jobPosting);

            CollectionAssert.Contains(errors, expectedError);
        }

        /// <summary>
        /// 建立具有 demo 職缺主要詳細欄位的合法 JobPosting。
        /// </summary>
        private static JobPosting CreateDetailedJobPosting()
        {
            JobPosting jobPosting = CreateValidJobPosting();
            jobPosting.Department = "研發部";
            jobPosting.Category = "軟體工程師";
            jobPosting.Compensation = new JobCompensation
            {
                Type = "range",
                Period = "monthly",
                Minimum = 45000,
                Maximum = 65000,
                Currency = "TWD",
                RawText = "月薪 45,000~65,000 元",
                Notes = "依經驗調整"
            };
            jobPosting.Location = new JobLocation
            {
                WorkMode = "onsite",
                City = "台北市",
                District = "信義區",
                Address = "台北市信義區測試路100號",
                RemoteAllowed = false,
                RawText = "台北市信義區測試路100號"
            };
            jobPosting.WorkConditions = new JobWorkConditions
            {
                EmploymentType = "全職",
                WorkingHours = "日班",
                BusinessTrip = "無需出差",
                ManagementResponsibility = "不需負擔管理責任",
                LeavePolicy = "依公司規定",
                StartDate = "一個月內"
            };
            jobPosting.Responsibilities.Add("開發與維護 Unity 專案");
            jobPosting.Requirements = new JobRequirements
            {
                Experience = "一年以上",
                Education = "專科以上",
                Major = "不拘"
            };
            jobPosting.Requirements.Languages.Add(new JobLanguageRequirement
            {
                Name = "英文",
                Reading = "中等"
            });
            jobPosting.Requirements.Tools.Add("Unity");
            jobPosting.Requirements.Tools.Add("TypeScript");
            jobPosting.Requirements.Skills.Add("軟體程式設計");
            jobPosting.Requirements.OtherConditions.Add("具備溝通能力");
            jobPosting.Benefits = new JobBenefits();
            jobPosting.Benefits.SalaryBonus.Add("績效獎金");
            jobPosting.Benefits.InsuranceHealth.Add("勞健保");
            jobPosting.Benefits.Flexibility.Add("彈性上下班");
            jobPosting.Benefits.Training.Add("內部教育訓練");
            jobPosting.Benefits.Life.Add("部門聚餐");
            jobPosting.Benefits.Other.Add("設備補助");
            jobPosting.RecruitmentProcess.Add("初步面談");
            jobPosting.RecruitmentProcess.Add("技術面談");
            jobPosting.Tags.Add("Unity");
            jobPosting.RiskFlags.Add("薪資上限未保證");
            jobPosting.RawDescription = "完整職缺描述原文";
            jobPosting.LegacyId = "demo_job_001";
            return jobPosting;
        }

        /// <summary>
        /// 建立各測試可單獨修改的一筆最小合法新職缺。
        /// </summary>
        /// <returns>必要欄位齊全，且擷取時間保留 +08:00 時區的 JobPosting。</returns>
        private static JobPosting CreateValidJobPosting()
        {
            return new JobPosting
            {
                Id = JobPostingIdGenerator.Create(),
                CompanyId = CompanyIdGenerator.Create(),
                Title = "Unity 工程師",
                Source = new JobSource
                {
                    Platform = "104",
                    Url = "https://www.104.com.tw/job/example"
                },
                CapturedAt = new DateTimeOffset(
                    2026,
                    9,
                    6,
                    12,
                    0,
                    0,
                    TimeSpan.FromHours(8))
            };
        }
    }
}
