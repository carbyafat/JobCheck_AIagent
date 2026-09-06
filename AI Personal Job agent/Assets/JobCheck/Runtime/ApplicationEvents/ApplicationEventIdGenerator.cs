using System;

namespace JobCheck.Domain
{
    /// <summary>
    /// 負責產生新版 ApplicationEvent 使用的唯一識別碼。
    /// 此型別只建立 ID，不負責建立事件內容或修改 Application 階段。
    /// </summary>
    public static class ApplicationEventIdGenerator
    {
        /// <summary>
        /// ApplicationEvent ID 固定使用的型別前綴，避免它與其他 Domain ID 混用。
        /// </summary>
        public const string Prefix = "evt_";

        /// <summary>
        /// 建立新的 ApplicationEvent ID，格式為 evt_ 加上 32 位小寫十六進位 UUID。
        /// 每一筆事件都必須有自己的 ID；不得因事件內容相似而重用。
        /// </summary>
        /// <returns>例如 evt_0123456789abcdef0123456789abcdef 的唯一識別碼。</returns>
        public static string Create()
        {
            return Prefix + Guid.NewGuid().ToString("N").ToLowerInvariant();
        }
    }
}
