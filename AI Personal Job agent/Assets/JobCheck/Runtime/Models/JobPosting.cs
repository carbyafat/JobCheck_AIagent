using System;
using System.Collections.Generic;

namespace JobCheck.Domain
{
    /// <summary>
    /// 代表一個由特定公司發布的職缺。
    /// 此物件只保存職缺本身的資料，不保存收藏、投遞、結案原因或 Fit 評分等個人求職狀態。
    /// </summary>
    [Serializable]
    public sealed class JobPosting
    {
        /// <summary>
        /// 目前 JobPosting 資料結構的版本。寫入 V0.2 格式時應使用此值。
        /// </summary>
        public const string CurrentSchemaVersion = "0.2";

        /// <summary>
        /// 職缺不可變的唯一識別碼。新資料格式為 job_ 加上 32 位小寫十六進位 UUID。
        /// Migration 會保留符合舊資料安全規則的既有 job ID，因此舊資料不一定具有新版格式。
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 此筆職缺資料所使用的結構版本。目前正式版本為 0.2。
        /// 這不是應用程式版本，也不是職缺資料的修改次數。
        /// </summary>
        public string SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>
        /// 發布此職缺的 Company ID，而不是公司顯示名稱。
        /// 此欄位為必填；對應公司是否存在，要在可存取 Company 集合時做關聯驗證。
        /// </summary>
        public string CompanyId { get; set; }

        /// <summary>
        /// 職缺的顯示職稱，例如 Unity 工程師。此欄位為必填。
        /// 公司名稱、部門名稱或使用者自己的應徵狀態不得混入此欄位。
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 此職缺的來源平台與原始網址。此欄位為必填，
        /// 但來源內容是否合法會由後續 JobPostingValidator 負責判斷。
        /// </summary>
        public JobSource Source { get; set; }

        /// <summary>
        /// 系統擷取或匯入此職缺資料的時間，必須保留時區資訊。此欄位為必填。
        /// 使用 nullable 是為了能表示尚未通過驗證的缺漏資料；正式保存前不得為 null。
        /// </summary>
        public DateTimeOffset? CapturedAt { get; set; }

        /// <summary>
        /// 發布職缺的部門名稱。來源沒有部門資訊時可以不填。
        /// </summary>
        public string Department { get; set; }

        /// <summary>
        /// 來源平台提供的職務類別；不等同公司產業，也不取代 Title。
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 此職缺公開的薪資資訊。沒有可靠薪資資料時保持 null，不得自行以 0 補值。
        /// </summary>
        public JobCompensation Compensation { get; set; }

        /// <summary>
        /// 此職缺的工作地點與遠端形式。沒有可靠資訊時保持 null。
        /// </summary>
        public JobLocation Location { get; set; }

        /// <summary>
        /// 此職缺的聘僱形式、工時、出差與到職條件。
        /// </summary>
        public JobWorkConditions WorkConditions { get; set; }

        /// <summary>
        /// 工作內容與主要責任；預設空集合代表來源沒有列出，不代表沒有工作內容。
        /// </summary>
        public List<string> Responsibilities { get; set; } = new List<string>();

        /// <summary>
        /// 此職缺要求的經歷、學歷、語文、工具與技能。
        /// </summary>
        public JobRequirements Requirements { get; set; }

        /// <summary>
        /// 此職缺公開的福利制度。
        /// </summary>
        public JobBenefits Benefits { get; set; }

        /// <summary>
        /// 來源有明確順序的招募流程；預設空集合代表未知，不得自行推測面試輪次。
        /// </summary>
        public List<string> RecruitmentProcess { get; set; } = new List<string>();

        /// <summary>
        /// 用於搜尋與分類的職缺標籤；不得混入個人收藏或應徵狀態。
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// 只適用於此職缺的風險標記，例如薪資資訊矛盾；公司層級風險應放在 Company。
        /// </summary>
        public List<string> RiskFlags { get; set; } = new List<string>();

        /// <summary>
        /// 來源頁面的完整職缺描述原文，供 migration、重新解析與問題追查。
        /// </summary>
        public string RawDescription { get; set; }

        /// <summary>
        /// V0.1 或外部來源的舊識別碼，僅供追查與 mapping；不得取代不可變的 Id。
        /// </summary>
        public string LegacyId { get; set; }
    }
}
