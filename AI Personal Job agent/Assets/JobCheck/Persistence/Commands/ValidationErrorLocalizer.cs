using System.Collections.Generic;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// 將 Domain 的穩定錯誤代碼轉成人類可閱讀的繁體中文。
    /// Domain 仍以 enum 供程式判斷；UI 與 Command Service 不直接顯示 enum 名稱。
    /// </summary>
    public static class ValidationErrorLocalizer
    {
        private static readonly IReadOnlyDictionary<JobPostingValidationError, string>
            JobPostingMessages = new Dictionary<JobPostingValidationError, string>
            {
                { JobPostingValidationError.MissingJobPosting, "沒有提供職缺資料。" },
                { JobPostingValidationError.MissingId, "缺少職缺 ID。" },
                { JobPostingValidationError.InvalidNewId, "職缺 ID 格式不正確。" },
                { JobPostingValidationError.UnsafeLegacyId, "舊職缺 ID 含有不安全的字元。" },
                { JobPostingValidationError.UnsupportedSchemaVersion, "職缺資料版本不受支援。" },
                { JobPostingValidationError.MissingCompanyId, "職缺沒有指定公司。" },
                { JobPostingValidationError.MissingTitle, "職缺名稱不可空白。" },
                { JobPostingValidationError.MissingSource, "缺少職缺來源資料。" },
                { JobPostingValidationError.MissingSourcePlatform, "來源平台不可空白。" },
                { JobPostingValidationError.InvalidSourceUrl, "來源網址必須是有效的 HTTP 或 HTTPS 網址。" },
                { JobPostingValidationError.MissingCapturedAt, "缺少職缺收錄時間。" },
                { JobPostingValidationError.NegativeCompensationAmount, "薪資金額不可小於零。" },
                { JobPostingValidationError.CompensationMaximumBelowMinimum, "薪資上限不可低於薪資下限。" },
                { JobPostingValidationError.InvalidResponsibilityEntry, "工作內容包含空白項目。" },
                { JobPostingValidationError.InvalidRecruitmentProcessEntry, "招募流程包含空白步驟。" },
                { JobPostingValidationError.InvalidTagEntry, "職缺標籤包含空白項目。" },
                { JobPostingValidationError.InvalidRiskFlagEntry, "風險標記包含空白項目。" },
                { JobPostingValidationError.InvalidRequirementEntry, "職缺條件包含空白項目。" },
                { JobPostingValidationError.InvalidLanguageRequirement, "語言條件資料不完整。" },
                { JobPostingValidationError.InvalidBenefitEntry, "福利資料包含空白項目。" }
            };

        private static readonly IReadOnlyDictionary<ApplicationValidationError, string>
            ApplicationMessages = new Dictionary<ApplicationValidationError, string>
            {
                { ApplicationValidationError.MissingApplication, "沒有提供應徵資料。" },
                { ApplicationValidationError.MissingId, "缺少應徵紀錄 ID。" },
                { ApplicationValidationError.InvalidNewId, "應徵紀錄 ID 格式不正確。" },
                { ApplicationValidationError.UnsupportedSchemaVersion, "應徵資料版本不受支援。" },
                { ApplicationValidationError.MissingJobPostingId, "應徵紀錄沒有指定職缺。" },
                { ApplicationValidationError.InvalidSourceType, "應徵來源類型無效。" },
                { ApplicationValidationError.InvalidCurrentStage, "目前應徵狀態無效。" },
                { ApplicationValidationError.UnknownStageNotAllowed, "新應徵紀錄不可使用未知狀態。" },
                { ApplicationValidationError.MissingCreatedAt, "缺少應徵紀錄建立時間。" },
                { ApplicationValidationError.MissingUpdatedAt, "缺少應徵紀錄更新時間。" },
                { ApplicationValidationError.UpdatedAtBeforeCreatedAt, "應徵紀錄更新時間不可早於建立時間。" },
                { ApplicationValidationError.InvalidCandidateCloseReason, "本人結案原因無效。" },
                { ApplicationValidationError.MissingCandidateCloseReason, "本人結案時必須選擇原因。" },
                { ApplicationValidationError.CloseReasonWithoutCandidateClosure, "尚未由本人結案，不可填寫結案原因。" },
                { ApplicationValidationError.MissingCloseReasonNoteForOther, "選擇其他結案原因時必須填寫說明。" },
                { ApplicationValidationError.InvalidArchiveReason, "封存原因無效。" },
                { ApplicationValidationError.MissingArchiveReason, "封存應徵紀錄時必須選擇原因。" },
                { ApplicationValidationError.ArchiveReasonWithoutArchive, "尚未封存，不可填寫封存原因。" },
                { ApplicationValidationError.ActiveApplicationCannotBeArchived, "進行中的應徵紀錄不可封存。" },
                { ApplicationValidationError.PreviousApplicationReferencesSelf, "前次應徵紀錄不可指向自己。" }
            };

        private static readonly IReadOnlyDictionary<ApplicationEventValidationError, string>
            ApplicationEventMessages = new Dictionary<ApplicationEventValidationError, string>
            {
                { ApplicationEventValidationError.MissingApplicationEvent, "沒有提供應徵事件。" },
                { ApplicationEventValidationError.MissingId, "缺少事件 ID。" },
                { ApplicationEventValidationError.InvalidNewId, "事件 ID 格式不正確。" },
                { ApplicationEventValidationError.UnsupportedSchemaVersion, "事件資料版本不受支援。" },
                { ApplicationEventValidationError.MissingApplicationId, "事件沒有指定應徵紀錄。" },
                { ApplicationEventValidationError.InvalidEventType, "事件類型無效。" },
                { ApplicationEventValidationError.InvalidActor, "事件角色無效。" },
                { ApplicationEventValidationError.MissingOccurredAt, "缺少事件實際發生時間。" },
                { ApplicationEventValidationError.MissingRecordedAt, "缺少事件記錄時間。" },
                { ApplicationEventValidationError.RecordedBeforeApplicationCreated, "事件記錄時間不可早於應徵紀錄建立時間。" },
                { ApplicationEventValidationError.ScheduledForOnNonInterviewEvent, "只有安排面試事件可以填寫面試時間。" },
                { ApplicationEventValidationError.InvalidTimePrecision, "事件時間精確度設定無效。" },
                { ApplicationEventValidationError.MigrationSnapshotNotAllowed, "一般操作不可建立資料移轉快照事件。" },
                { ApplicationEventValidationError.MissingSnapshotStage, "資料移轉快照缺少應徵狀態。" },
                { ApplicationEventValidationError.InvalidSnapshotStage, "資料移轉快照的應徵狀態無效。" },
                { ApplicationEventValidationError.SnapshotStageOnNonMigrationEvent, "非資料移轉事件不可填寫快照狀態。" },
                { ApplicationEventValidationError.UnknownSnapshotStageNotAllowed, "此事件不可使用未知的快照狀態。" },
                { ApplicationEventValidationError.MissingSupersedesEventId, "資料修正事件沒有指定要取代的原事件。" },
                { ApplicationEventValidationError.SupersedesEventIdOnNonCorrectionEvent, "非資料修正事件不可指定被取代事件。" },
                { ApplicationEventValidationError.SupersedesSelf, "資料修正事件不可取代自己。" },
                { ApplicationEventValidationError.SystemEventRequiresSystemActor, "系統事件必須由系統角色建立。" }
            };

        private static readonly IReadOnlyDictionary<ApplicationStateReductionIssue, string>
            ReductionIssueMessages = new Dictionary<ApplicationStateReductionIssue, string>
            {
                { ApplicationStateReductionIssue.MissingInput, "沒有提供狀態推算資料。" },
                { ApplicationStateReductionIssue.MissingApplicationId, "缺少要推算的應徵紀錄 ID。" },
                { ApplicationStateReductionIssue.EmptyEventHistory, "沒有事件可用來推算目前狀態。" },
                { ApplicationStateReductionIssue.NoEffectiveStageEvent, "沒有足夠的有效事件可推算目前狀態。" },
                { ApplicationStateReductionIssue.NullEvent, "事件歷程包含空白資料。" },
                { ApplicationStateReductionIssue.EventBelongsToDifferentApplication, "事件屬於另一筆應徵紀錄。" },
                { ApplicationStateReductionIssue.InvalidEventData, "事件資料未通過驗證。" },
                { ApplicationStateReductionIssue.DuplicateEventId, "事件歷程包含重複的事件 ID。" },
                { ApplicationStateReductionIssue.CorrectionTargetNotFound, "找不到資料修正事件指定的原事件。" },
                { ApplicationStateReductionIssue.CorrectionTargetsCorrection, "資料修正事件不可再指向另一筆修正事件。" },
                { ApplicationStateReductionIssue.AmbiguousEventOrder, "多筆事件時間相同，無法判斷正確順序。" },
                { ApplicationStateReductionIssue.StageRegression, "事件會讓應徵流程回到較早階段。" },
                { ApplicationStateReductionIssue.ConflictingTerminalEvents, "應徵歷程同時存在互相衝突的結案結果。" },
                { ApplicationStateReductionIssue.EventAfterTerminalState, "應徵結案後仍出現一般進度事件。" },
                { ApplicationStateReductionIssue.UnknownSnapshotStage, "舊資料不足以確認目前應徵狀態。" }
            };

        public static string ToChinese(JobPostingValidationError error)
        {
            string message;
            return JobPostingMessages.TryGetValue(error, out message)
                ? message
                : "職缺資料不符合規則。";
        }

        public static bool HasTranslation(JobPostingValidationError error)
        {
            return JobPostingMessages.ContainsKey(error);
        }

        public static string ToChinese(ApplicationValidationError error)
        {
            string message;
            return ApplicationMessages.TryGetValue(error, out message)
                ? message
                : "應徵資料不符合規則。";
        }

        public static bool HasTranslation(ApplicationValidationError error)
        {
            return ApplicationMessages.ContainsKey(error);
        }

        public static string ToChinese(ApplicationEventValidationError error)
        {
            string message;
            return ApplicationEventMessages.TryGetValue(error, out message)
                ? message
                : "應徵事件不符合規則。";
        }

        public static bool HasTranslation(ApplicationEventValidationError error)
        {
            return ApplicationEventMessages.ContainsKey(error);
        }

        public static string ToChinese(ApplicationStateReductionIssue issue)
        {
            string message;
            return ReductionIssueMessages.TryGetValue(issue, out message)
                ? message
                : "事件順序無法安全套用。";
        }

        public static bool HasTranslation(ApplicationStateReductionIssue issue)
        {
            return ReductionIssueMessages.ContainsKey(issue);
        }
    }
}
