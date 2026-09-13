using System;
using System.Collections.Generic;

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

        /// <summary>來源公開的部門名稱；可選。</summary>
        public string Department { get; set; }

        /// <summary>來源平台的職務類別；可選。</summary>
        public string Category { get; set; }

        /// <summary>薪資表示方式，例如 range、fixed 或 negotiable；可選。</summary>
        public string CompensationType { get; set; }

        /// <summary>薪資週期，例如 monthly、yearly 或 hourly；可選。</summary>
        public string CompensationPeriod { get; set; }

        /// <summary>薪資下限；null 代表來源未提供，不可以 0 代替未知。</summary>
        public int? CompensationMinimum { get; set; }

        /// <summary>薪資上限；null 代表來源未提供。</summary>
        public int? CompensationMaximum { get; set; }

        /// <summary>薪資幣別，例如 TWD；可選且不得由 Persistence 猜測。</summary>
        public string CompensationCurrency { get; set; }

        /// <summary>來源頁面的完整薪資文字；可選。</summary>
        public string CompensationRawText { get; set; }

        /// <summary>來源頁面的完整工作地點文字；可選。</summary>
        public string LocationRawText { get; set; }

        /// <summary>工作模式，例如 onsite、hybrid 或 remote；可選。</summary>
        public string WorkMode { get; set; }

        /// <summary>聘僱形式，例如全職、兼職或約聘；可選。</summary>
        public string EmploymentType { get; set; }

        /// <summary>上班時段或工時說明；可選。</summary>
        public string WorkingHours { get; set; }

        /// <summary>工作經驗要求原文；可選。</summary>
        public string Experience { get; set; }

        /// <summary>學歷要求原文；可選。</summary>
        public string Education { get; set; }

        /// <summary>工作內容與主要責任，一個項目保存為一筆字串。</summary>
        public List<string> Responsibilities { get; set; } = new List<string>();

        /// <summary>明確列出的工具、框架或技術，一個項目保存為一筆字串。</summary>
        public List<string> Tools { get; set; } = new List<string>();

        /// <summary>技能或能力要求，一個項目保存為一筆字串。</summary>
        public List<string> Skills { get; set; } = new List<string>();

        /// <summary>
        /// 使用者選擇的職缺分類標籤；值使用 JobPostingLabelCatalog 定義的小寫代碼。
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// 使用者選擇的職缺風險標記；值使用 JobPostingLabelCatalog 定義的小寫代碼。
        /// </summary>
        public List<string> RiskFlags { get; set; } = new List<string>();

        /// <summary>
        /// 實際收錄時間。UI 通常不傳，由服務使用目前時間；測試可以傳入固定時間。
        /// </summary>
        public DateTimeOffset? CapturedAt { get; set; }
    }

    /// <summary>
    /// UI 編輯既有職缺時交給 Persistence 的最小輸入。
    /// JobPostingId 用來找到原資料，其餘欄位只更新目前表單可編輯的內容。
    /// </summary>
    public sealed class JobPostingEditRequest
    {
        /// <summary>
        /// 要編輯的既有職缺 ID。更新成功後此 ID 不會改變。
        /// </summary>
        public string JobPostingId { get; set; }

        /// <summary>
        /// 公司顯示名稱，必填。改名代表讓本職缺改連到正規化後同名的公司，
        /// 不會直接改掉可能被其他職缺共用的 Company。
        /// </summary>
        public string CompanyName { get; set; }

        /// <summary>
        /// 職缺顯示名稱，必填。
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 職缺來源平台，必填。
        /// </summary>
        public string SourcePlatform { get; set; }

        /// <summary>
        /// 原始職缺網址；可以不填，有填時必須是 HTTP 或 HTTPS 絕對網址。
        /// </summary>
        public string SourceUrl { get; set; }

        /// <summary>
        /// 完整職缺描述原文；可以不填。
        /// </summary>
        public string RawDescription { get; set; }

        /// <summary>修正後的收錄時間；null 代表保留原值。</summary>
        public DateTimeOffset? CapturedAt { get; set; }

        public string Department { get; set; }
        public string Category { get; set; }
        public string CompensationType { get; set; }
        public string CompensationPeriod { get; set; }
        public int? CompensationMinimum { get; set; }
        public int? CompensationMaximum { get; set; }
        public string CompensationCurrency { get; set; }
        public string CompensationRawText { get; set; }
        public string LocationRawText { get; set; }
        public string WorkMode { get; set; }
        public string EmploymentType { get; set; }
        public string WorkingHours { get; set; }
        public string Experience { get; set; }
        public string Education { get; set; }
        public List<string> Responsibilities { get; set; } = new List<string>();
        public List<string> Tools { get; set; } = new List<string>();
        public List<string> Skills { get; set; } = new List<string>();

        /// <summary>
        /// 編輯後要保存的完整職缺分類標籤集合。
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// 編輯後要保存的完整職缺風險標記集合。
        /// </summary>
        public List<string> RiskFlags { get; set; } = new List<string>();
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
