using System;

namespace JobCheck.Domain
{
    /// <summary>
    /// 表示職缺資料的原始來源。
    /// 此物件描述「從哪裡取得職缺」，不是使用者如何應徵，也不是 Application 的 SourceType。
    /// </summary>
    [Serializable]
    public sealed class JobSource
    {
        /// <summary>
        /// 提供職缺的平台或來源名稱，例如 104、LinkedIn 或公司官網。
        /// 此欄位用於來源追蹤與平台成效統計，不應填入公司名稱。
        /// </summary>
        public string Platform { get; set; }

        /// <summary>
        /// 原始職缺頁面的網址。
        /// 若來源確實沒有可用網址可以不填，但不得捏造網址；網址是否有效由後續 Validator 檢查。
        /// </summary>
        public string Url { get; set; }
    }
}
