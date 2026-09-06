namespace JobCheck.Domain
{
    /// <summary>
    /// ApplicationStateReducer 在事件歷史中發現、需要人工確認的穩定異常代碼。
    /// 異常不代表 Reducer 可以自行改寫歷史；UI 或 migration 應依代碼提供後續處理方式。
    /// </summary>
    public enum ApplicationStateReductionIssue
    {
        /// <summary>
        /// 沒有提供 Reducer 的輸入物件。
        /// </summary>
        MissingInput,

        /// <summary>
        /// 沒有指定要推算的 Application ID。
        /// </summary>
        MissingApplicationId,

        /// <summary>
        /// 沒有任何事件可以用來推算目前階段。
        /// </summary>
        EmptyEventHistory,

        /// <summary>
        /// 事件集合存在，但扣除無效、被修正或不影響階段的事件後，沒有足夠資料推算階段。
        /// </summary>
        NoEffectiveStageEvent,

        /// <summary>
        /// 事件集合中包含 null，無法判斷該筆歷史內容。
        /// </summary>
        NullEvent,

        /// <summary>
        /// 某筆事件的 ApplicationId 與本次推算目標不同。
        /// </summary>
        EventBelongsToDifferentApplication,

        /// <summary>
        /// 事件本身未通過 ApplicationEventValidator，不能直接視為可靠歷史。
        /// </summary>
        InvalidEventData,

        /// <summary>
        /// 事件集合中出現相同的事件 ID，無法確認是否為重複資料。
        /// </summary>
        DuplicateEventId,

        /// <summary>
        /// DataCorrected 指向的原事件不在本次事件集合中。
        /// </summary>
        CorrectionTargetNotFound,

        /// <summary>
        /// DataCorrected 指向另一筆 DataCorrected；目前版本不允許形成修正鏈。
        /// </summary>
        CorrectionTargetsCorrection,

        /// <summary>
        /// 多筆會影響階段的事件具有相同時間資訊，無法確認真實先後順序。
        /// </summary>
        AmbiguousEventOrder,

        /// <summary>
        /// 較後發生的事件使流程回到較早階段，但沒有明確的新一輪流程依據。
        /// </summary>
        StageRegression,

        /// <summary>
        /// 同一筆應徵同時出現互斥的公司拒絕與本人主動結案結果。
        /// </summary>
        ConflictingTerminalEvents,

        /// <summary>
        /// 應徵已經結案後仍出現一般進度事件，無法直接判定是否應重新開啟流程。
        /// </summary>
        EventAfterTerminalState,

        /// <summary>
        /// MigrationSnapshot 保存 Unknown，代表舊資料不足以確認目前階段。
        /// </summary>
        UnknownSnapshotStage
    }
}
