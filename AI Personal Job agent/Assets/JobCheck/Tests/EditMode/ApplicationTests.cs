using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace JobCheck.Domain.Tests
{
    /// <summary>
    /// 驗證 Application 的 ID、必要欄位、狀態邊界、結案原因、封存原因與前次投遞連結。
    /// </summary>
    public class ApplicationTests
    {
        /// <summary>
        /// 確認新 Application ID 符合 app_ 加上 32 位小寫十六進位 UUID 的格式。
        /// </summary>
        [Test]
        public void ApplicationIdGenerator_Create_ReturnsExpectedFormat()
        {
            string applicationId = ApplicationIdGenerator.Create();

            StringAssert.IsMatch("^app_[0-9a-f]{32}$", applicationId);
        }

        /// <summary>
        /// 確認連續建立兩個 Application ID 時，不會得到同一個識別碼。
        /// </summary>
        [Test]
        public void ApplicationIdGenerator_CreateTwice_ReturnsDifferentIds()
        {
            Assert.AreNotEqual(ApplicationIdGenerator.Create(), ApplicationIdGenerator.Create());
        }

        /// <summary>
        /// 確認必要資料齊全的新 Application 不會產生驗證錯誤。
        /// </summary>
        [Test]
        public void ValidateNew_ValidApplication_ReturnsNoErrors()
        {
            IReadOnlyList<ApplicationValidationError> errors =
                ApplicationValidator.ValidateNew(CreateValidApplication());

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// 確認不存在的 Application 會回報 MissingApplication，而不是造成 null reference exception。
        /// </summary>
        [Test]
        public void ValidateNew_NullApplication_ReturnsMissingApplication()
        {
            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(null);

            CollectionAssert.Contains(errors, ApplicationValidationError.MissingApplication);
        }

        /// <summary>
        /// 確認缺少 ID 時會回報 MissingId。
        /// </summary>
        [Test]
        public void ValidateNew_MissingId_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.Id = " ";

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.MissingId);
        }

        /// <summary>
        /// 確認新版 Application 不接受舊格式或錯誤前綴的 ID。
        /// </summary>
        [Test]
        public void ValidateNew_InvalidId_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.Id = "legacy-application-001";

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.InvalidNewId);
        }

        /// <summary>
        /// 確認未知或缺漏的 schema version 不會被靜默接受。
        /// </summary>
        [Test]
        public void ValidateNew_UnsupportedSchemaVersion_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.SchemaVersion = "0.3";

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.UnsupportedSchemaVersion);
        }

        /// <summary>
        /// 確認缺少 JobPosting ID 時會回報錯誤。
        /// 是否真的存在對應職缺，則留給 Repository 的跨集合驗證。
        /// </summary>
        [Test]
        public void ValidateNew_MissingJobPostingId_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.JobPostingId = null;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.MissingJobPostingId);
        }

        /// <summary>
        /// 確認 enum 以外的 SourceType 數值會被回報。
        /// </summary>
        [Test]
        public void ValidateNew_UndefinedSourceType_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.SourceType = (SourceType)999;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.InvalidSourceType);
        }

        /// <summary>
        /// 確認 enum 以外的 ApplicationStage 數值會被回報。
        /// </summary>
        [Test]
        public void ValidateNew_UndefinedStage_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.CurrentStage = (ApplicationStage)999;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.InvalidCurrentStage);
        }

        /// <summary>
        /// 確認新 Application 不得使用 Unknown 階段。
        /// </summary>
        [Test]
        public void ValidateNew_UnknownStage_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.CurrentStage = ApplicationStage.Unknown;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.UnknownStageNotAllowed);
        }

        /// <summary>
        /// 確認 migration 或修復流程可以暫時保留 Unknown 階段。
        /// </summary>
        [Test]
        public void ValidateForMigration_UnknownStage_ReturnsNoErrors()
        {
            Application application = CreateValidApplication();
            application.CurrentStage = ApplicationStage.Unknown;

            IReadOnlyList<ApplicationValidationError> errors =
                ApplicationValidator.ValidateForMigration(application);

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// 確認缺少建立時間時會回報錯誤。
        /// </summary>
        [Test]
        public void ValidateNew_MissingCreatedAt_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.CreatedAt = null;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.MissingCreatedAt);
        }

        /// <summary>
        /// 確認缺少更新時間時會回報錯誤。
        /// </summary>
        [Test]
        public void ValidateNew_MissingUpdatedAt_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.UpdatedAt = null;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.MissingUpdatedAt);
        }

        /// <summary>
        /// 確認更新時間不能早於建立時間。
        /// </summary>
        [Test]
        public void ValidateNew_UpdatedBeforeCreated_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.UpdatedAt = application.CreatedAt.Value.AddMinutes(-1);

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.UpdatedAtBeforeCreatedAt);
        }

        /// <summary>
        /// 確認非本人結案階段不能填入 CandidateCloseReason。
        /// </summary>
        [Test]
        public void ValidateNew_CloseReasonOnActiveStage_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.CandidateCloseReason = CandidateCloseReason.BetterOpportunity;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.CloseReasonWithoutCandidateClosure);
        }

        /// <summary>
        /// 確認被公司拒絕時不能誤填本人結案原因。
        /// </summary>
        [Test]
        public void ValidateNew_CompanyRejectionWithCandidateReason_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.CurrentStage = ApplicationStage.RejectedByCompany;
            application.CandidateCloseReason = CandidateCloseReason.NoResponse;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.CloseReasonWithoutCandidateClosure);
        }

        /// <summary>
        /// 確認無效的 CandidateCloseReason enum 數值會被回報。
        /// </summary>
        [Test]
        public void ValidateNew_UndefinedCandidateCloseReason_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.CurrentStage = ApplicationStage.ClosedByCandidate;
            application.CandidateCloseReason = (CandidateCloseReason)999;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.InvalidCandidateCloseReason);
        }

        /// <summary>
        /// 確認本人結案原因選擇 Other 時必須提供補充說明。
        /// </summary>
        [Test]
        public void ValidateNew_OtherCloseReasonWithoutNote_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.CurrentStage = ApplicationStage.ClosedByCandidate;
            application.CandidateCloseReason = CandidateCloseReason.Other;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.MissingCloseReasonNoteForOther);
        }

        /// <summary>
        /// 確認 Other 搭配補充說明時是合法的本人結案資料。
        /// </summary>
        [Test]
        public void ValidateNew_OtherCloseReasonWithNote_ReturnsNoErrors()
        {
            Application application = CreateValidApplication();
            application.CurrentStage = ApplicationStage.ClosedByCandidate;
            application.CandidateCloseReason = CandidateCloseReason.Other;
            application.CandidateCloseReasonNote = "職涯規劃改變";

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// 確認尚未封存時不能單獨填入 ArchiveReason。
        /// </summary>
        [Test]
        public void ValidateNew_ArchiveReasonWithoutArchive_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.ArchiveReason = ArchiveReason.UserArchived;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.ArchiveReasonWithoutArchive);
        }

        /// <summary>
        /// 確認 IsArchived 為 true 時必須填寫封存原因。
        /// </summary>
        [Test]
        public void ValidateNew_ArchivedWithoutReason_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.CurrentStage = ApplicationStage.RejectedByCompany;
            application.IsArchived = true;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.MissingArchiveReason);
        }

        /// <summary>
        /// 確認仍在進行中的本人應徵不能因一般封存操作而從追蹤列表消失。
        /// </summary>
        [Test]
        public void ValidateNew_ActiveMyApplicationArchived_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.IsArchived = true;
            application.ArchiveReason = ArchiveReason.UserArchived;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.ActiveApplicationCannotBeArchived);
        }

        /// <summary>
        /// 確認明確標記為 Duplicate 的進行中重複資料可以收進整理區。
        /// 完整資料庫階段仍需驗證確實存在另一筆主要紀錄。
        /// </summary>
        [Test]
        public void ValidateNew_ActiveDuplicateApplicationArchived_ReturnsNoErrors()
        {
            Application application = CreateValidApplication();
            application.IsArchived = true;
            application.ArchiveReason = ArchiveReason.Duplicate;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// 確認已結案的本人應徵可以封存，而且封存不會改變原本的結案階段。
        /// </summary>
        [Test]
        public void ValidateNew_ClosedApplicationArchived_ReturnsNoErrors()
        {
            Application application = CreateValidApplication();
            application.CurrentStage = ApplicationStage.ClosedByCandidate;
            application.CandidateCloseReason = CandidateCloseReason.BetterOpportunity;
            application.IsArchived = true;
            application.ArchiveReason = ArchiveReason.UserArchived;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            Assert.IsEmpty(errors);
            Assert.AreEqual(ApplicationStage.ClosedByCandidate, application.CurrentStage);
        }

        /// <summary>
        /// 確認無效的 ArchiveReason enum 數值會被回報。
        /// </summary>
        [Test]
        public void ValidateNew_UndefinedArchiveReason_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.IsArchived = true;
            application.ArchiveReason = (ArchiveReason)999;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.InvalidArchiveReason);
        }

        /// <summary>
        /// 確認 PreviousApplicationId 不能指向自己，避免最短的循環連結。
        /// </summary>
        [Test]
        public void ValidateNew_PreviousApplicationReferencesSelf_ReturnsError()
        {
            Application application = CreateValidApplication();
            application.PreviousApplicationId = application.Id;

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            CollectionAssert.Contains(errors, ApplicationValidationError.PreviousApplicationReferencesSelf);
        }

        /// <summary>
        /// 確認再次投遞可以連結另一筆 Application，單筆 Validator 不會誤判為循環。
        /// </summary>
        [Test]
        public void ValidateNew_PreviousApplicationReferencesDifferentId_ReturnsNoErrors()
        {
            Application application = CreateValidApplication();
            application.PreviousApplicationId = ApplicationIdGenerator.Create();

            IReadOnlyList<ApplicationValidationError> errors = ApplicationValidator.ValidateNew(application);

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// 建立各測試可單獨修改的一筆最小合法 Application。
        /// </summary>
        /// <returns>必要欄位齊全，且時間保留 +08:00 時區的本人應徵紀錄。</returns>
        private static Application CreateValidApplication()
        {
            DateTimeOffset createdAt = new DateTimeOffset(
                2026,
                9,
                6,
                12,
                0,
                0,
                TimeSpan.FromHours(8));

            return new Application
            {
                Id = ApplicationIdGenerator.Create(),
                JobPostingId = JobPostingIdGenerator.Create(),
                SourceType = SourceType.MyApplication,
                CurrentStage = ApplicationStage.Applied,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
        }
    }
}
