namespace JobCheck.Domain
{
    /// <summary>
    /// 使用者主動停止一筆應徵時的結案原因。此 enum 不適用於公司拒絕的情況。
    /// </summary>
    public enum CandidateCloseReason
    {
        /// <summary>
        /// 薪資低於使用者可接受的範圍。
        /// </summary>
        SalaryTooLow,

        /// <summary>
        /// 公司或職缺屬於使用者不接受的博弈產業。
        /// </summary>
        GamblingIndustry,

        /// <summary>
        /// 工作地點或通勤成本不符合需求。
        /// </summary>
        Commute,

        /// <summary>
        /// 工時、輪班或其他工作時間制度不符合需求。
        /// </summary>
        WorkSchedule,

        /// <summary>
        /// 職缺要求週末值班或週末工作。
        /// </summary>
        WeekendDuty,

        /// <summary>
        /// 實際職務內容與使用者期待的角色不相符。
        /// </summary>
        RoleMismatch,

        /// <summary>
        /// 使用的技術、工具或技術方向與使用者期待不相符。
        /// </summary>
        TechMismatch,

        /// <summary>
        /// 對公司文化、制度、穩定性或其他公司層級因素有疑慮。
        /// </summary>
        CompanyConcern,

        /// <summary>
        /// 使用者選擇了另一個更適合的工作機會。
        /// </summary>
        BetterOpportunity,

        /// <summary>
        /// 因長時間沒有收到公司回覆而決定停止應徵。
        /// </summary>
        NoResponse,

        /// <summary>
        /// 其他未列出的原因；使用此值時必須另外填寫原因說明。
        /// </summary>
        Other
    }
}

