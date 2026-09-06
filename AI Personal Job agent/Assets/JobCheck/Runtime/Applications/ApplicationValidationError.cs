namespace JobCheck.Domain
{
    /// <summary>
    /// Application 驗證時可能回報的穩定錯誤代碼。
    /// UI 與 migration 應依代碼判斷錯誤種類，顯示文字則由外層決定。
    /// </summary>
    public enum ApplicationValidationError
    {
        /// <summary>
        /// 傳入的 Application 物件本身不存在。
        /// </summary>
        MissingApplication,

        /// <summary>
        /// Application ID 為 null、空字串或純空白。
        /// </summary>
        MissingId,

        /// <summary>
        /// 新 Application 的 ID 不符合 app_ 加上 32 位小寫十六進位 UUID。
        /// </summary>
        InvalidNewId,

        /// <summary>
        /// schema version 缺漏或不是目前支援的 0.2。
        /// </summary>
        UnsupportedSchemaVersion,

        /// <summary>
        /// 沒有指定這次應徵所對應的 JobPosting ID。
        /// </summary>
        MissingJobPostingId,

        /// <summary>
        /// SourceType 數值不是目前定義的來源類型。
        /// </summary>
        InvalidSourceType,

        /// <summary>
        /// CurrentStage 數值不是目前定義的應徵階段。
        /// </summary>
        InvalidCurrentStage,

        /// <summary>
        /// 新建立的 Application 使用了只允許 migration 或修復流程使用的 Unknown 階段。
        /// </summary>
        UnknownStageNotAllowed,

        /// <summary>
        /// 沒有提供 Application 的建立時間。
        /// </summary>
        MissingCreatedAt,

        /// <summary>
        /// 沒有提供 Application 的最近更新時間。
        /// </summary>
        MissingUpdatedAt,

        /// <summary>
        /// UpdatedAt 早於 CreatedAt，時間順序不合理。
        /// </summary>
        UpdatedAtBeforeCreatedAt,

        /// <summary>
        /// CandidateCloseReason 數值不是目前定義的本人結案原因。
        /// </summary>
        InvalidCandidateCloseReason,

        /// <summary>
        /// CurrentStage 不是 ClosedByCandidate，卻填入了本人結案原因。
        /// </summary>
        CloseReasonWithoutCandidateClosure,

        /// <summary>
        /// 本人結案原因選擇 Other，卻沒有填寫補充說明。
        /// </summary>
        MissingCloseReasonNoteForOther,

        /// <summary>
        /// ArchiveReason 數值不是目前定義的封存原因。
        /// </summary>
        InvalidArchiveReason,

        /// <summary>
        /// Application 已設為封存，卻沒有填寫封存原因。
        /// </summary>
        MissingArchiveReason,

        /// <summary>
        /// Application 尚未封存，卻填入了封存原因。
        /// </summary>
        ArchiveReasonWithoutArchive,

        /// <summary>
        /// 仍在進行中的本人應徵被設為封存，可能導致後續追蹤漏球。
        /// 明確標記為 Duplicate 的重複資料不受此限制。
        /// </summary>
        ActiveApplicationCannotBeArchived,

        /// <summary>
        /// PreviousApplicationId 指向這筆 Application 自己。
        /// 更長的循環仍需由持有完整集合的 Repository 驗證。
        /// </summary>
        PreviousApplicationReferencesSelf
    }
}
