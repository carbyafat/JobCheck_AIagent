namespace JobCheck.Domain
{
    /// <summary>
    /// 完整資料集合進行關聯驗證時可能回報的穩定錯誤代碼。
    /// 顯示文字由外層決定，程式邏輯應使用這些代碼判斷。
    /// </summary>
    public enum JobCheckDataSetValidationError
    {
        /// <summary>
        /// Company 集合中有兩筆以上資料使用相同 ID。
        /// </summary>
        DuplicateCompanyId,

        /// <summary>
        /// JobPosting 集合中有兩筆以上資料使用相同 ID。
        /// </summary>
        DuplicateJobPostingId,

        /// <summary>
        /// Application 集合中有兩筆以上資料使用相同 ID。
        /// </summary>
        DuplicateApplicationId,

        /// <summary>
        /// ApplicationEvent 集合中有兩筆以上資料使用相同 ID。
        /// </summary>
        DuplicateApplicationEventId,

        /// <summary>
        /// JobPosting.CompanyId 找不到對應的 Company。
        /// </summary>
        JobPostingCompanyNotFound,

        /// <summary>
        /// Application.JobPostingId 找不到對應的 JobPosting。
        /// </summary>
        ApplicationJobPostingNotFound,

        /// <summary>
        /// ApplicationEvent.ApplicationId 找不到對應的 Application。
        /// </summary>
        ApplicationEventApplicationNotFound,

        /// <summary>
        /// Application.PreviousApplicationId 找不到對應的前次應徵。
        /// </summary>
        PreviousApplicationNotFound,

        /// <summary>
        /// Application.PreviousApplicationId 指向自己。
        /// </summary>
        PreviousApplicationReferencesSelf,

        /// <summary>
        /// 前次應徵與目前應徵不屬於同一個 JobPosting。
        /// </summary>
        PreviousApplicationBelongsToDifferentJobPosting,

        /// <summary>
        /// PreviousApplicationId 連結形成兩筆以上的循環。
        /// </summary>
        PreviousApplicationCycle,

        /// <summary>
        /// Application 的事件歷史無法由 Reducer 無歧義地推算目前階段。
        /// 詳細原因保存在驗證問題的 StateReductionIssue。
        /// </summary>
        ApplicationEventHistoryNeedsReview,

        /// <summary>
        /// Application.CurrentStage 快取與事件歷史推算出的階段不一致。
        /// Validator 只回報問題，不會自動覆寫快取。
        /// </summary>
        ApplicationCurrentStageMismatch
    }
}
