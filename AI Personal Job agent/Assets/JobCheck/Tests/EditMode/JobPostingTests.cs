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
