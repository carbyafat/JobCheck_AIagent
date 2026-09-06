using System;

namespace JobCheck.Domain
{
    /// <summary>
    /// 描述職缺公開的薪資條件。整個物件為 optional；不知道的數值應保持 null，不得以 0 代替未知。
    /// </summary>
    [Serializable]
    public sealed class JobCompensation
    {
        /// <summary>
        /// 薪資表示方式，例如 range、fixed 或 negotiable；保留來源語意，不由 UI 自行猜測。
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 薪資週期，例如 hourly、monthly 或 yearly。來源未提供時可以不填。
        /// </summary>
        public string Period { get; set; }

        /// <summary>
        /// 薪資下限。null 表示來源沒有可靠數值；0 是實際數值，不可拿來表示未知。
        /// </summary>
        public int? Minimum { get; set; }

        /// <summary>
        /// 薪資上限。待遇面議或只公開最低薪時通常為 null。
        /// </summary>
        public int? Maximum { get; set; }

        /// <summary>
        /// 薪資幣別，例如 TWD、USD。Domain 不自行推測缺漏的幣別。
        /// </summary>
        public string Currency { get; set; }

        /// <summary>
        /// 來源頁面上的原始薪資文字，供 UI 顯示與 migration 結果追查。
        /// </summary>
        public string RawText { get; set; }

        /// <summary>
        /// 薪資條件的補充說明，例如含獎金、試用期或來源解析備註。
        /// </summary>
        public string Notes { get; set; }
    }
}
