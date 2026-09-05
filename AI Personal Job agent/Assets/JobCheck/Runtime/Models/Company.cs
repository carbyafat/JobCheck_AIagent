using System;
using System.Collections.Generic;

namespace JobCheck.Domain
{
    /// <summary>
    /// 代表一間公司的公司層級資料。
    /// 薪資、職稱、工時等只屬於特定職缺的內容，應放在 JobPosting，不得放入此物件。
    /// </summary>
    [Serializable]
    public sealed class Company
    {
        /// <summary>
        /// 目前 Company 資料結構的版本。寫入 V0.2 格式時應使用此值。
        /// </summary>
        public const string CurrentSchemaVersion = "0.2";

        /// <summary>
        /// 公司不可變的唯一識別碼。新資料格式為 cmp_ 加上 32 位小寫十六進位 UUID；
        /// 不得直接使用公司名稱作為 ID，也不得因名稱修改而更換。
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 此筆公司資料所使用的結構版本。目前正式版本為 0.2。
        /// 這不是應用程式版本，也不是公司資料的修改次數。
        /// </summary>
        public string SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>
        /// 公司對外使用的顯示名稱。此欄位為必填；
        /// 判斷兩筆資料是否為同一家公司時，必須先經過公司名稱正規化，不可直接比較原始文字。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 公司所屬產業，例如軟體服務或零售業。無法確認時可不填，
        /// 不應將特定職缺的職務類別填入此欄位。
        /// </summary>
        public string Industry { get; set; }

        /// <summary>
        /// 使用者針對公司整體留下的備註。
        /// 特定面試、薪資或某一職缺的內容應保存在對應的 Application 或 JobPosting。
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// 適用於公司整體的風險標記，例如公司資訊不透明。
        /// 特定職缺才有的風險不得放入此集合；集合預設為空而不是 null。
        /// </summary>
        public List<string> RiskFlags { get; set; } = new List<string>();

        /// <summary>
        /// 此筆公司資料首次建立的時間，必須保留時區資訊。
        /// null 表示舊資料或匯入資料沒有可靠的建立時間，不得自行捏造。
        /// </summary>
        public DateTimeOffset? CreatedAt { get; set; }

        /// <summary>
        /// 此筆公司資料最近一次修改的時間，必須保留時區資訊。
        /// null 表示沒有可靠的修改時間；不得早於 CreatedAt。
        /// </summary>
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
