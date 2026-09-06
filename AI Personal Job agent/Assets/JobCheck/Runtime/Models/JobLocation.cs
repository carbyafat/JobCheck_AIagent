using System;

namespace JobCheck.Domain
{
    /// <summary>
    /// 描述職缺的工作地點與遠端形式，不代表公司的登記地址。
    /// </summary>
    [Serializable]
    public sealed class JobLocation
    {
        /// <summary>
        /// 工作模式，例如 onsite、hybrid 或 remote；未能確認時保持 null。
        /// </summary>
        public string WorkMode { get; set; }

        /// <summary>
        /// 工作地點的城市或縣市。
        /// </summary>
        public string City { get; set; }

        /// <summary>
        /// 工作地點的行政區。
        /// </summary>
        public string District { get; set; }

        /// <summary>
        /// 來源公開的詳細地址；不得自行補入未公開資訊。
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 是否明確允許遠端工作。null 表示來源沒有說明，不等同 false。
        /// </summary>
        public bool? RemoteAllowed { get; set; }

        /// <summary>
        /// 來源頁面的完整地點文字，供顯示與追查解析結果。
        /// </summary>
        public string RawText { get; set; }
    }
}
