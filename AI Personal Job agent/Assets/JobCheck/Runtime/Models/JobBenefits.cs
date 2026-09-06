using System;
using System.Collections.Generic;

namespace JobCheck.Domain
{
    /// <summary>
    /// 依用途分類保存特定職缺公開的福利內容。
    /// </summary>
    [Serializable]
    public sealed class JobBenefits
    {
        /// <summary>
        /// 薪資以外的獎金或津貼。
        /// </summary>
        public List<string> SalaryBonus { get; set; } = new List<string>();

        /// <summary>
        /// 保險、醫療與健康相關福利。
        /// </summary>
        public List<string> InsuranceHealth { get; set; } = new List<string>();

        /// <summary>
        /// 彈性工時、遠端制度等彈性安排。
        /// </summary>
        public List<string> Flexibility { get; set; } = new List<string>();

        /// <summary>
        /// 教育訓練、課程補助與職涯成長資源。
        /// </summary>
        public List<string> Training { get; set; } = new List<string>();

        /// <summary>
        /// 聚餐、休閒與日常生活福利。
        /// </summary>
        public List<string> Life { get; set; } = new List<string>();

        /// <summary>
        /// 無法歸入其他分類的福利項目。
        /// </summary>
        public List<string> Other { get; set; } = new List<string>();
    }
}
