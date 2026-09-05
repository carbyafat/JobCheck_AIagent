using System;

namespace JobCheck.Domain
{
    /// <summary>
    /// 負責產生新版 Company 使用的唯一識別碼。
    /// 此型別只建立 ID，不負責判斷公司名稱是否代表同一家公司。
    /// </summary>
    public static class CompanyIdGenerator
    {
        /// <summary>
        /// Company ID 固定使用的型別前綴，可避免它與 JobPosting、Application 等其他 ID 混用。
        /// </summary>
        public const string Prefix = "cmp_";

        /// <summary>
        /// 建立新的 Company ID，格式為 cmp_ 加上 32 位小寫十六進位 UUID。
        /// 每次呼叫都會產生新值；已指派給公司的 ID 不應重新產生或因公司改名而更換。
        /// </summary>
        /// <returns>例如 cmp_0123456789abcdef0123456789abcdef 的唯一識別碼。</returns>
        public static string Create()
        {
            return Prefix + Guid.NewGuid().ToString("N").ToLowerInvariant();
        }
    }
}
