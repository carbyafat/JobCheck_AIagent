namespace JobCheck.Domain
{
    /// <summary>
    /// JobPosting 驗證時可能回報的穩定錯誤代碼。
    /// UI 與 migration 應依代碼判斷錯誤種類，顯示文字則由外層決定，避免把中文訊息當成程式邏輯。
    /// </summary>
    public enum JobPostingValidationError
    {
        /// <summary>
        /// 傳入的 JobPosting 物件本身不存在。
        /// </summary>
        MissingJobPosting,

        /// <summary>
        /// 職缺 ID 為 null、空字串或純空白。
        /// </summary>
        MissingId,

        /// <summary>
        /// 新建立職缺的 ID 不符合 job_ 加上 32 位小寫十六進位 UUID。
        /// </summary>
        InvalidNewId,

        /// <summary>
        /// Migration 想保留的舊 ID 含有路徑分隔符或 ..，不可安全用作資料識別。
        /// </summary>
        UnsafeLegacyId,

        /// <summary>
        /// schema version 缺漏或不是目前支援的 0.2。
        /// </summary>
        UnsupportedSchemaVersion,

        /// <summary>
        /// 沒有指定發布此職缺的 Company ID。
        /// </summary>
        MissingCompanyId,

        /// <summary>
        /// 職缺名稱為 null、空字串或純空白。
        /// </summary>
        MissingTitle,

        /// <summary>
        /// 沒有提供職缺來源物件。
        /// </summary>
        MissingSource,

        /// <summary>
        /// 職缺來源存在，但沒有提供平台或來源名稱。
        /// </summary>
        MissingSourcePlatform,

        /// <summary>
        /// 職缺來源網址有值，但不是有效的 HTTP 或 HTTPS 絕對網址。
        /// </summary>
        InvalidSourceUrl,

        /// <summary>
        /// 沒有提供職缺擷取或匯入時間。
        /// </summary>
        MissingCapturedAt
    }
}
