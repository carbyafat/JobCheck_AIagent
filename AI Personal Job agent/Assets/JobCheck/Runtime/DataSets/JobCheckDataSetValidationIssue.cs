namespace JobCheck.Domain
{
    /// <summary>
    /// 一筆可定位的跨集合驗證問題。
    /// </summary>
    public sealed class JobCheckDataSetValidationIssue
    {
        /// <summary>
        /// 建立一筆驗證問題；不在 Domain 層組合中文訊息，讓 UI 可自行顯示或翻譯。
        /// </summary>
        public JobCheckDataSetValidationIssue(
            JobCheckDataSetValidationError error,
            JobCheckDataSetEntityType entityType,
            string entityId,
            string fieldName,
            string relatedId,
            ApplicationStateReductionIssue? stateReductionIssue = null,
            ApplicationStage? expectedStage = null)
        {
            Error = error;
            EntityType = entityType;
            EntityId = entityId;
            FieldName = fieldName;
            RelatedId = relatedId;
            StateReductionIssue = stateReductionIssue;
            ExpectedStage = expectedStage;
        }

        /// <summary>
        /// 供程式穩定判斷問題種類的錯誤代碼。
        /// </summary>
        public JobCheckDataSetValidationError Error { get; }

        /// <summary>
        /// 發生問題的資料種類。
        /// </summary>
        public JobCheckDataSetEntityType EntityType { get; }

        /// <summary>
        /// 發生問題的資料 ID；重複 ID 問題會填入該重複值。
        /// </summary>
        public string EntityId { get; }

        /// <summary>
        /// 發生問題的欄位名稱，例如 CompanyId 或 PreviousApplicationId。
        /// </summary>
        public string FieldName { get; }

        /// <summary>
        /// 欄位試圖連結的 ID；不涉及關聯時為 null。
        /// </summary>
        public string RelatedId { get; }

        /// <summary>
        /// 事件歷史需要人工確認時，由 ApplicationStateReducer 回報的詳細原因。
        /// 非事件推算問題時為 null。
        /// </summary>
        public ApplicationStateReductionIssue? StateReductionIssue { get; }

        /// <summary>
        /// CurrentStage 快取不一致時，事件歷史推算出的預期階段。
        /// 其他問題或無法可靠推算時為 null。
        /// </summary>
        public ApplicationStage? ExpectedStage { get; }
    }
}
