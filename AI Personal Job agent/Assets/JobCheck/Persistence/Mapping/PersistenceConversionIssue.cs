namespace JobCheck.Persistence
{
    /// <summary>
    /// 一筆可定位到 DTO 欄位的轉換問題。
    /// </summary>
    public sealed class PersistenceConversionIssue
    {
        /// <summary>
        /// 建立轉換問題；人類顯示文字由外層依 Error 與 FieldPath 決定。
        /// </summary>
        public PersistenceConversionIssue(
            PersistenceConversionError error,
            string fieldPath,
            string rawValue)
        {
            Error = error;
            FieldPath = fieldPath;
            RawValue = rawValue;
        }

        /// <summary>
        /// 穩定的格式或型別轉換錯誤代碼。
        /// </summary>
        public PersistenceConversionError Error { get; }

        /// <summary>
        /// 發生問題的 snake_case 欄位路徑，例如 events[0].occurred_at。
        /// </summary>
        public string FieldPath { get; }

        /// <summary>
        /// 無法轉換的原始字串；缺少物件或非字串衝突時可為 null。
        /// </summary>
        public string RawValue { get; }
    }
}
