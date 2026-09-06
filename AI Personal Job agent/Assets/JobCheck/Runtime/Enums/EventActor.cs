namespace JobCheck.Domain
{
    /// <summary>
    /// 造成 ApplicationEvent 發生的角色。
    /// 此欄位描述「誰做了這件事」，不代表由誰把事件輸入系統。
    /// </summary>
    public enum EventActor
    {
        /// <summary>
        /// 使用者本人，例如投遞履歷或主動停止應徵。
        /// </summary>
        Candidate,

        /// <summary>
        /// 招募公司或公司端人員，例如主動聯絡、拒絕或發出 Offer。
        /// </summary>
        Company,

        /// <summary>
        /// 求職平台自動產生的行為，例如通知履歷已被讀取。
        /// </summary>
        Platform,

        /// <summary>
        /// JobCheck 系統建立的技術性事件，例如 migration snapshot 或資料修正。
        /// </summary>
        System
    }
}
