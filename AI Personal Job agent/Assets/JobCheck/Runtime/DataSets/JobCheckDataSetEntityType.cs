namespace JobCheck.Domain
{
    /// <summary>
    /// 跨集合驗證問題所屬的 Domain 資料種類。
    /// </summary>
    public enum JobCheckDataSetEntityType
    {
        /// <summary>
        /// 公司資料 Company。
        /// </summary>
        Company,

        /// <summary>
        /// 職缺資料 JobPosting。
        /// </summary>
        JobPosting,

        /// <summary>
        /// 單次應徵資料 Application。
        /// </summary>
        Application,

        /// <summary>
        /// 應徵歷程事件 ApplicationEvent。
        /// </summary>
        ApplicationEvent
    }
}
