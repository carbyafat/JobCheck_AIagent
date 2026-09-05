namespace JobCheck.Domain
{
    /// <summary>
    /// 應徵流程中發生的事件類型。事件代表已發生的歷史事實，不應以目前狀態覆蓋或刪除。
    /// </summary>
    public enum ApplicationEventType
    {
        /// <summary>
        /// 使用者已收藏職缺或表示有興趣。
        /// </summary>
        Saved,

        /// <summary>
        /// 使用者已向公司投遞履歷。
        /// </summary>
        Applied,

        /// <summary>
        /// 公司或招募平台已讀取履歷。
        /// </summary>
        Viewed,

        /// <summary>
        /// 公司、招募者或平台已主動聯絡使用者。
        /// </summary>
        Contacted,

        /// <summary>
        /// 已安排面試時間，但面試尚未完成。
        /// </summary>
        InterviewScheduled,

        /// <summary>
        /// 已完成一場面試；同一筆應徵可以有多筆面試完成事件。
        /// </summary>
        InterviewCompleted,

        /// <summary>
        /// 已進入等待公司回覆的階段。
        /// </summary>
        WaitingResponseStarted,

        /// <summary>
        /// 公司已拒絕本次應徵。
        /// </summary>
        RejectedByCompany,

        /// <summary>
        /// 使用者主動停止或放棄本次應徵。
        /// </summary>
        ClosedByCandidate,

        /// <summary>
        /// 使用者已收到公司的錄取通知或 Offer。
        /// </summary>
        OfferReceived,

        /// <summary>
        /// 使用者將本次應徵標記為長時間沒有回覆；此事件不覆蓋原本階段。
        /// </summary>
        NoResponseMarked,

        /// <summary>
        /// 舊資料轉換時建立的狀態快照，不代表可以確認的真實流程事件。
        /// </summary>
        MigrationSnapshot,

        /// <summary>
        /// 用來修正既有事件的系統事件，必須指出被修正的事件。
        /// </summary>
        DataCorrected
    }
}

