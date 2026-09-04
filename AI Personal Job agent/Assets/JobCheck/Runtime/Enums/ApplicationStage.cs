namespace JobCheck.Domain
{
    /// <summary>
    /// 一筆應徵目前所在的階段。此值是方便查詢與顯示的快取，應由有效事件重新推導或驗證。
    /// </summary>
    public enum ApplicationStage
    {
        /// <summary>
        /// 階段無法確認；只允許用於舊資料轉換或損壞資料修復流程。
        /// </summary>
        Unknown,

        /// <summary>
        /// 已收藏職缺或表示有興趣，但尚未投遞。
        /// </summary>
        Saved,

        /// <summary>
        /// 已投遞履歷。
        /// </summary>
        Applied,

        /// <summary>
        /// 公司或招募平台已讀取履歷。
        /// </summary>
        Viewed,

        /// <summary>
        /// 已收到公司、招募者或平台聯絡。
        /// </summary>
        Contacted,

        /// <summary>
        /// 已安排面試，正在等待面試進行。
        /// </summary>
        InterviewScheduled,

        /// <summary>
        /// 已完成最近一場面試。
        /// </summary>
        InterviewCompleted,

        /// <summary>
        /// 正在等待公司提供後續回覆。
        /// </summary>
        WaitingResponse,

        /// <summary>
        /// 已收到錄取通知或 Offer，但之後仍可能由本人決定是否繼續。
        /// </summary>
        OfferReceived,

        /// <summary>
        /// 公司已拒絕本次應徵，屬於公司端結案結果。
        /// </summary>
        RejectedByCompany,

        /// <summary>
        /// 使用者主動停止本次應徵，屬於本人端結案結果。
        /// </summary>
        ClosedByCandidate
    }
}

