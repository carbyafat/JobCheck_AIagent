namespace JobCheck.Domain
{
    /// <summary>
    /// 應徵或報告資料的來源類型，用來決定資料是否能納入個人求職統計。
    /// </summary>
    public enum SourceType
    {
        /// <summary>
        /// 使用者自己的應徵紀錄，預設可以納入個人統計。
        /// </summary>
        MyApplication,

        /// <summary>
        /// 從其他系統匯入的應徵紀錄，必須經過確認才能納入個人統計。
        /// </summary>
        ImportedApplication,

        /// <summary>
        /// 僅供參考的外部報告或資料，不得直接納入個人統計。
        /// </summary>
        ExternalReport
    }
}

