using System.Collections.Generic;

namespace JobCheck.Domain
{
    /// <summary>
    /// ApplicationStateReducer 單次推算所需的輸入資料。
    /// 此物件只描述推算範圍；Reducer 不得修改傳入的事件，也不得假設事件已經排序。
    /// </summary>
    public sealed class ApplicationStateReducerInput
    {
        /// <summary>
        /// 建立一份階段推算要求。
        /// 參數允許暫時帶入 null，讓 Reducer 能以異常結果回報資料缺漏，而不是直接拋出例外。
        /// </summary>
        /// <param name="applicationId">要推算目前階段的 Application ID。</param>
        /// <param name="events">可能尚未排序的 ApplicationEvent 集合。</param>
        public ApplicationStateReducerInput(
            string applicationId,
            IReadOnlyList<ApplicationEvent> events)
        {
            ApplicationId = applicationId;
            Events = events;
        }

        /// <summary>
        /// 本次要推算的 Application ID。
        /// Reducer 必須檢查每筆事件都屬於此 ID，不能把其他應徵紀錄混入計算。
        /// </summary>
        public string ApplicationId { get; }

        /// <summary>
        /// 用來重建目前階段的完整事件集合。
        /// 呼叫端不必預先排序；Reducer 應建立自己的排序結果，不得改動此集合或其中的事件。
        /// </summary>
        public IReadOnlyList<ApplicationEvent> Events { get; }
    }
}
