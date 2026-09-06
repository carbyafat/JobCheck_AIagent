using System;

namespace JobCheck.Domain
{
    /// <summary>
    /// 描述職缺對單一語言的能力要求。各能力可分別缺漏，不得自行補成「不拘」。
    /// </summary>
    [Serializable]
    public sealed class JobLanguageRequirement
    {
        /// <summary>
        /// 語言名稱，例如英文或日文。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 聽力要求。
        /// </summary>
        public string Listening { get; set; }

        /// <summary>
        /// 口說要求。
        /// </summary>
        public string Speaking { get; set; }

        /// <summary>
        /// 閱讀要求。
        /// </summary>
        public string Reading { get; set; }

        /// <summary>
        /// 寫作要求。
        /// </summary>
        public string Writing { get; set; }

        /// <summary>
        /// 來源頁面的完整語文需求文字，供顯示及追查。
        /// </summary>
        public string RawText { get; set; }
    }
}
