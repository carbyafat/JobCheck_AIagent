using System;

namespace JobCheck.Domain
{
    /// <summary>
    /// 負責產生新版 Application 使用的唯一識別碼。
    /// 此型別只建立 ID，不負責判斷同一職缺是否已存在其他應徵。
    /// </summary>
    public static class ApplicationIdGenerator
    {
        /// <summary>
        /// Application ID 固定使用的型別前綴，避免它與 Company、JobPosting 或 Event ID 混用。
        /// </summary>
        public const string Prefix = "app_";

        /// <summary>
        /// 建立新的 Application ID，格式為 app_ 加上 32 位小寫十六進位 UUID。
        /// 同一職缺再次投遞時應呼叫此方法建立新 ID，再用 PreviousApplicationId 連結前一次應徵。
        /// </summary>
        /// <returns>例如 app_0123456789abcdef0123456789abcdef 的唯一識別碼。</returns>
        public static string Create()
        {
            return Prefix + Guid.NewGuid().ToString("N").ToLowerInvariant();
        }
    }
}
