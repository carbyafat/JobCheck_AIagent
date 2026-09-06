using System;

namespace JobCheck.Domain
{
    /// <summary>
    /// 負責產生新版 JobPosting 使用的唯一識別碼。
    /// 此型別只建立新 ID，不負責驗證或替換 migration 保留下來的舊版 job ID。
    /// </summary>
    public static class JobPostingIdGenerator
    {
        /// <summary>
        /// 新版 JobPosting ID 固定使用的型別前綴，避免它與 Company、Application 等其他 ID 混用。
        /// </summary>
        public const string Prefix = "job_";

        /// <summary>
        /// 建立新的 JobPosting ID，格式為 job_ 加上 32 位小寫十六進位 UUID。
        /// 每次呼叫都會產生新值；已指派給職缺的 ID 不應因職稱、公司或來源內容變更而更換。
        /// Migration 遇到符合舊資料安全規則的既有 job ID 時，應保留原值而不是呼叫此方法。
        /// </summary>
        /// <returns>例如 job_0123456789abcdef0123456789abcdef 的唯一識別碼。</returns>
        public static string Create()
        {
            return Prefix + Guid.NewGuid().ToString("N").ToLowerInvariant();
        }
    }
}
