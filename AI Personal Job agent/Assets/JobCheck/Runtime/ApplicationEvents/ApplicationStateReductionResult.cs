using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JobCheck.Domain
{
    /// <summary>
    /// ApplicationStateReducer 根據事件歷史產生的推算結果。
    /// 此結果只描述計算所得狀態，不會直接修改 Application.CurrentStage 或刪改任何事件。
    /// </summary>
    public sealed class ApplicationStateReductionResult
    {
        /// <summary>
        /// 建立一份不可由呼叫端直接改動異常集合的推算結果。
        /// </summary>
        /// <param name="currentStage">依有效事件推算出的目前應徵階段。</param>
        /// <param name="issues">推算過程發現的異常；null 會視為沒有異常。</param>
        public ApplicationStateReductionResult(
            ApplicationStage currentStage,
            IEnumerable<ApplicationStateReductionIssue> issues)
        {
            CurrentStage = currentStage;

            var issueSnapshot = issues == null
                ? Array.Empty<ApplicationStateReductionIssue>()
                : new List<ApplicationStateReductionIssue>(issues).ToArray();

            Issues = new ReadOnlyCollection<ApplicationStateReductionIssue>(issueSnapshot);
        }

        /// <summary>
        /// 依有效事件推算出的目前階段。
        /// 沒有足夠資料時應回傳 ApplicationStage.Unknown，並在 Issues 說明原因。
        /// </summary>
        public ApplicationStage CurrentStage { get; }

        /// <summary>
        /// 推算過程發現的所有異常代碼。
        /// 集合一定存在且不可由呼叫端修改；空集合代表沒有發現需要人工確認的問題。
        /// </summary>
        public IReadOnlyList<ApplicationStateReductionIssue> Issues { get; }

        /// <summary>
        /// 是否存在任何需要人工確認的推算異常。
        /// 此值由 Issues 自動得出，可直接同步到 Application.NeedsReview。
        /// </summary>
        public bool NeedsReview => Issues.Count > 0;
    }
}
