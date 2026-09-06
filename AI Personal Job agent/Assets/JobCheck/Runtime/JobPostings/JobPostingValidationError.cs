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
        MissingCapturedAt,

        /// <summary>
        /// 薪資下限或上限小於 0，不能作為有效薪資數值。
        /// </summary>
        NegativeCompensationAmount,

        /// <summary>
        /// 薪資上限低於薪資下限，範圍互相矛盾。
        /// </summary>
        CompensationMaximumBelowMinimum,

        /// <summary>
        /// Responsibilities 包含 null、空字串或純空白項目。
        /// </summary>
        InvalidResponsibilityEntry,

        /// <summary>
        /// RecruitmentProcess 包含 null、空字串或純空白步驟。
        /// </summary>
        InvalidRecruitmentProcessEntry,

        /// <summary>
        /// Tags 包含 null、空字串或純空白標籤。
        /// </summary>
        InvalidTagEntry,

        /// <summary>
        /// RiskFlags 包含 null、空字串或純空白風險標記。
        /// </summary>
        InvalidRiskFlagEntry,

        /// <summary>
        /// Requirements 的工具、技能或其他條件集合包含空白項目。
        /// </summary>
        InvalidRequirementEntry,

        /// <summary>
        /// Languages 包含 null，或一筆完全沒有語言名稱、能力與原始文字的空物件。
        /// </summary>
        InvalidLanguageRequirement,

        /// <summary>
        /// Benefits 的任一分類包含 null、空字串或純空白項目。
        /// </summary>
        InvalidBenefitEntry
    }
}
