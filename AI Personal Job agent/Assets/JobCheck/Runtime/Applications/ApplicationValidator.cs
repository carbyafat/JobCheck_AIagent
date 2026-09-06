using System;
using System.Collections.Generic;

namespace JobCheck.Domain
{
    /// <summary>
    /// 驗證 Application 的必要欄位與單筆資料內可以判斷的共通規則。
    /// JobPosting 是否存在、同一職缺是否已有 active 應徵，以及 PreviousApplicationId 是否形成長循環，
    /// 必須由可存取完整資料集合的 Repository 驗證。
    /// </summary>
    public static class ApplicationValidator
    {
        /// <summary>
        /// 驗證一筆新建立的 Application。新資料不得使用 Unknown 階段。
        /// </summary>
        /// <param name="application">要驗證的新應徵；可以傳入 null 以取得 MissingApplication。</param>
        /// <returns>所有已發現的錯誤代碼；空集合表示此 Validator 沒有發現錯誤。</returns>
        public static IReadOnlyList<ApplicationValidationError> ValidateNew(Application application)
        {
            return Validate(application, allowUnknownStage: false);
        }

        /// <summary>
        /// 驗證 migration 或損壞資料修復所建立的 Application。
        /// 此入口允許暫時使用 Unknown 階段，但其他必要欄位及關聯規則仍需成立。
        /// </summary>
        /// <param name="application">要驗證的轉換或修復結果；可以傳入 null 以取得 MissingApplication。</param>
        /// <returns>所有已發現的錯誤代碼；空集合表示此 Validator 沒有發現錯誤。</returns>
        public static IReadOnlyList<ApplicationValidationError> ValidateForMigration(Application application)
        {
            return Validate(application, allowUnknownStage: true);
        }

        /// <summary>
        /// 收集新資料與 migration 共用的錯誤，並依呼叫情境決定是否接受 Unknown 階段。
        /// </summary>
        /// <param name="application">要驗證的 Application。</param>
        /// <param name="allowUnknownStage">是否允許 migration 或修復流程使用 Unknown。</param>
        /// <returns>所有已發現的錯誤代碼。</returns>
        private static List<ApplicationValidationError> Validate(
            Application application,
            bool allowUnknownStage)
        {
            var errors = new List<ApplicationValidationError>();

            if (application == null)
            {
                errors.Add(ApplicationValidationError.MissingApplication);
                return errors;
            }

            if (string.IsNullOrWhiteSpace(application.Id))
            {
                errors.Add(ApplicationValidationError.MissingId);
            }
            else if (!IsValidNewId(application.Id))
            {
                errors.Add(ApplicationValidationError.InvalidNewId);
            }

            if (!string.Equals(
                application.SchemaVersion,
                Application.CurrentSchemaVersion,
                StringComparison.Ordinal))
            {
                errors.Add(ApplicationValidationError.UnsupportedSchemaVersion);
            }

            if (string.IsNullOrWhiteSpace(application.JobPostingId))
            {
                errors.Add(ApplicationValidationError.MissingJobPostingId);
            }

            if (!Enum.IsDefined(typeof(SourceType), application.SourceType))
            {
                errors.Add(ApplicationValidationError.InvalidSourceType);
            }

            bool hasDefinedStage = Enum.IsDefined(typeof(ApplicationStage), application.CurrentStage);
            if (!hasDefinedStage)
            {
                errors.Add(ApplicationValidationError.InvalidCurrentStage);
            }
            else if (!allowUnknownStage && application.CurrentStage == ApplicationStage.Unknown)
            {
                errors.Add(ApplicationValidationError.UnknownStageNotAllowed);
            }

            if (!application.CreatedAt.HasValue)
            {
                errors.Add(ApplicationValidationError.MissingCreatedAt);
            }

            if (!application.UpdatedAt.HasValue)
            {
                errors.Add(ApplicationValidationError.MissingUpdatedAt);
            }

            if (application.CreatedAt.HasValue
                && application.UpdatedAt.HasValue
                && application.UpdatedAt.Value < application.CreatedAt.Value)
            {
                errors.Add(ApplicationValidationError.UpdatedAtBeforeCreatedAt);
            }

            ValidateCandidateCloseReason(application, errors);
            ValidateArchiveReason(application, errors);

            if (!string.IsNullOrWhiteSpace(application.PreviousApplicationId)
                && string.Equals(
                    application.PreviousApplicationId,
                    application.Id,
                    StringComparison.Ordinal))
            {
                errors.Add(ApplicationValidationError.PreviousApplicationReferencesSelf);
            }

            return errors;
        }

        /// <summary>
        /// 檢查本人結案原因是否只用於 ClosedByCandidate，並處理 Other 的必填說明。
        /// </summary>
        /// <param name="application">要檢查的 Application。</param>
        /// <param name="errors">要加入錯誤代碼的清單。</param>
        private static void ValidateCandidateCloseReason(
            Application application,
            ICollection<ApplicationValidationError> errors)
        {
            if (!application.CandidateCloseReason.HasValue)
            {
                return;
            }

            CandidateCloseReason closeReason = application.CandidateCloseReason.Value;
            bool hasDefinedReason = Enum.IsDefined(typeof(CandidateCloseReason), closeReason);
            if (!hasDefinedReason)
            {
                errors.Add(ApplicationValidationError.InvalidCandidateCloseReason);
            }

            if (application.CurrentStage != ApplicationStage.ClosedByCandidate)
            {
                errors.Add(ApplicationValidationError.CloseReasonWithoutCandidateClosure);
            }

            if (hasDefinedReason
                && closeReason == CandidateCloseReason.Other
                && string.IsNullOrWhiteSpace(application.CandidateCloseReasonNote))
            {
                errors.Add(ApplicationValidationError.MissingCloseReasonNoteForOther);
            }
        }

        /// <summary>
        /// 檢查封存原因是否為已定義值、是否與封存旗標一致，
        /// 並避免仍在進行中的本人應徵因封存而從追蹤列表消失。
        /// </summary>
        /// <param name="application">要檢查的 Application。</param>
        /// <param name="errors">要加入錯誤代碼的清單。</param>
        private static void ValidateArchiveReason(
            Application application,
            ICollection<ApplicationValidationError> errors)
        {
            if (application.IsArchived && !application.ArchiveReason.HasValue)
            {
                errors.Add(ApplicationValidationError.MissingArchiveReason);
                return;
            }

            if (!application.ArchiveReason.HasValue)
            {
                return;
            }

            ArchiveReason archiveReason = application.ArchiveReason.Value;
            bool hasDefinedReason = Enum.IsDefined(typeof(ArchiveReason), archiveReason);
            if (!hasDefinedReason)
            {
                errors.Add(ApplicationValidationError.InvalidArchiveReason);
            }

            if (!application.IsArchived)
            {
                errors.Add(ApplicationValidationError.ArchiveReasonWithoutArchive);
            }

            if (application.IsArchived
                && application.SourceType == SourceType.MyApplication
                && !IsTerminalStage(application.CurrentStage)
                && (!hasDefinedReason || archiveReason != ArchiveReason.Duplicate))
            {
                errors.Add(ApplicationValidationError.ActiveApplicationCannotBeArchived);
            }
        }

        /// <summary>
        /// 判斷階段是否已由公司或本人明確結案。
        /// OfferReceived 仍需要使用者決定是否接受，因此不視為 terminal stage。
        /// </summary>
        /// <param name="stage">要檢查的應徵階段。</param>
        /// <returns>公司拒絕或本人結案時為 true。</returns>
        private static bool IsTerminalStage(ApplicationStage stage)
        {
            return stage == ApplicationStage.RejectedByCompany
                || stage == ApplicationStage.ClosedByCandidate;
        }

        /// <summary>
        /// 判斷 ID 是否符合新 Application 的固定格式。
        /// </summary>
        /// <param name="id">要檢查的 Application ID。</param>
        /// <returns>符合 app_ 加上 32 位小寫十六進位 UUID 時為 true。</returns>
        private static bool IsValidNewId(string id)
        {
            if (!id.StartsWith(ApplicationIdGenerator.Prefix, StringComparison.Ordinal)
                || id.Length != 36)
            {
                return false;
            }

            string uuidPart = id.Substring(ApplicationIdGenerator.Prefix.Length);
            return string.Equals(uuidPart, uuidPart.ToLowerInvariant(), StringComparison.Ordinal)
                && Guid.TryParseExact(uuidPart, "N", out _);
        }
    }
}
