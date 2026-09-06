using System.Collections.Generic;

namespace JobCheck.Domain
{
    /// <summary>
    /// 完整資料集合的跨模型驗證結果。
    /// </summary>
    public sealed class JobCheckDataSetValidationResult
    {
        /// <summary>
        /// 建立不會受 Validator 內部清單後續修改影響的結果。
        /// </summary>
        public JobCheckDataSetValidationResult(IEnumerable<JobCheckDataSetValidationIssue> issues)
        {
            Issues = (issues == null
                ? new List<JobCheckDataSetValidationIssue>()
                : new List<JobCheckDataSetValidationIssue>(issues)).AsReadOnly();
        }

        /// <summary>
        /// 所有已發現且可定位的問題；空集合代表本層關聯規則全部通過。
        /// </summary>
        public IReadOnlyList<JobCheckDataSetValidationIssue> Issues { get; }

        /// <summary>
        /// 沒有任何跨集合問題時為 true。
        /// </summary>
        public bool IsValid => Issues.Count == 0;
    }
}
