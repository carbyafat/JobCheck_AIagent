using System;
using System.Collections.Generic;

namespace JobCheck.Domain
{
    /// <summary>
    /// 描述職缺要求的經歷、學歷、語文、工具與能力條件。
    /// </summary>
    [Serializable]
    public sealed class JobRequirements
    {
        /// <summary>
        /// 工作經驗要求，例如不拘或三年以上。
        /// </summary>
        public string Experience { get; set; }

        /// <summary>
        /// 學歷要求。
        /// </summary>
        public string Education { get; set; }

        /// <summary>
        /// 科系或主修要求。
        /// </summary>
        public string Major { get; set; }

        /// <summary>
        /// 語文能力條件；預設空集合代表來源沒有列出語文需求。
        /// </summary>
        public List<JobLanguageRequirement> Languages { get; set; }
            = new List<JobLanguageRequirement>();

        /// <summary>
        /// 明確列出的工具、框架或技術名稱。
        /// </summary>
        public List<string> Tools { get; set; } = new List<string>();

        /// <summary>
        /// 技能或能力要求，例如系統分析或跨部門溝通。
        /// </summary>
        public List<string> Skills { get; set; } = new List<string>();

        /// <summary>
        /// 無法歸入固定欄位的其他應徵條件。
        /// </summary>
        public List<string> OtherConditions { get; set; } = new List<string>();
    }
}
