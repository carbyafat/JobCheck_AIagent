using System;

namespace JobCheck.Persistence
{
    /// <summary>
    /// UI 建立一筆職缺時交給 Persistence 的最小輸入。
    /// 這不是 Domain 模型，也不允許 UI 指定 CompanyId、JobPostingId 或 schema version。
    /// </summary>
    public sealed class JobPostingCreateRequest
    {
        /// <summary>
        /// 公司顯示名稱，必填。寫入服務會先正規化名稱，再決定重用既有公司或建立新公司。
        /// </summary>
        public string CompanyName { get; set; }

        /// <summary>
        /// 職缺顯示名稱，必填，例如 Unity 工程師。
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 職缺來源平台，必填，例如 104、LinkedIn 或公司官網。
        /// </summary>
        public string SourcePlatform { get; set; }

        /// <summary>
        /// 原始職缺頁面網址；來源沒有網址時可以不填，有填時必須是 HTTP 或 HTTPS 絕對網址。
        /// </summary>
        public string SourceUrl { get; set; }

        /// <summary>
        /// 使用者貼入的完整職缺內容；可以不填，第一版不嘗試自動拆解成薪資或技能欄位。
        /// </summary>
        public string RawDescription { get; set; }

        /// <summary>
        /// 實際收錄時間。UI 通常不傳，由服務使用目前時間；測試可以傳入固定時間。
        /// </summary>
        public DateTimeOffset? CapturedAt { get; set; }
    }

    /// <summary>
    /// 成功建立職缺後回傳給 UI 的識別資訊。
    /// </summary>
    public sealed class JobPostingWriteSummary
    {
        public JobPostingWriteSummary(
            string companyId,
            string jobPostingId,
            bool companyCreated)
        {
            CompanyId = companyId;
            JobPostingId = jobPostingId;
            CompanyCreated = companyCreated;
        }

        /// <summary>
        /// 新職缺所連結的公司 ID。
        /// </summary>
        public string CompanyId { get; }

        /// <summary>
        /// 新建立的職缺 ID。
        /// </summary>
        public string JobPostingId { get; }

        /// <summary>
        /// true 表示本次同時建立新公司；false 表示重用正規化後同名的既有公司。
        /// </summary>
        public bool CompanyCreated { get; }
    }
}
