namespace JobCheck.Persistence
{
    /// <summary>
    /// DTO 與 Domain 轉換期間可能發生的穩定錯誤代碼。
    /// 這些代碼只描述格式與型別轉換，不取代 Domain Validator 的業務規則。
    /// </summary>
    public enum PersistenceConversionError
    {
        /// <summary>
        /// 呼叫端沒有提供要轉換的 DTO 或 Domain 根物件。
        /// </summary>
        MissingSourceObject,

        /// <summary>
        /// JSON 結構缺少轉換所需的巢狀物件，例如 JobPosting.source。
        /// </summary>
        MissingRequiredObject,

        /// <summary>
        /// required 時間字串缺漏、無效或沒有明確時區。
        /// </summary>
        InvalidDateTime,

        /// <summary>
        /// enum 字串無法對應到已定義的 Domain enum，或 Domain enum 是未定義數值。
        /// </summary>
        InvalidEnum,

        /// <summary>
        /// DTO 的 has_* 為 false，卻同時帶有會被忽略的非預設值。
        /// </summary>
        ValuePresentWithoutPresenceFlag
    }
}
