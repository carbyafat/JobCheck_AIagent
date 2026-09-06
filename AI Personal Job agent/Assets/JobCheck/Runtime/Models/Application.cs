using System;

namespace JobCheck.Domain
{
    /// <summary>
    /// 代表使用者與一個特定 JobPosting 之間的一次應徵紀錄。
    /// 同一職缺再次投遞時應建立新 Application，不能覆蓋前一次紀錄。
    /// FitScore 的正式型別與範圍尚未定案，因此本階段不先放入模型，避免把舊版的 -1 慣例固化成新規格。
    /// </summary>
    [Serializable]
    public sealed class Application
    {
        /// <summary>
        /// 目前 Application 資料結構的版本。寫入 V0.2 格式時應使用此值。
        /// </summary>
        public const string CurrentSchemaVersion = "0.2";

        /// <summary>
        /// 本次應徵不可變的唯一識別碼，格式為 app_ 加上 32 位小寫十六進位 UUID。
        /// 即使狀態或備註改變也不得重新產生 ID。
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 此筆應徵資料所使用的結構版本。目前正式版本為 0.2。
        /// 這不是應徵進度，也不是修改次數。
        /// </summary>
        public string SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>
        /// 這次應徵所對應的 JobPosting ID，而不是職缺名稱或公司名稱。
        /// 對應職缺是否存在，需要在可存取 JobPosting 集合時做關聯驗證。
        /// </summary>
        public string JobPostingId { get; set; }

        /// <summary>
        /// 此紀錄是本人應徵、外部匯入或僅供參考的外部報告。
        /// 只有 MyApplication 預設納入個人求職統計。
        /// </summary>
        public SourceType SourceType { get; set; }

        /// <summary>
        /// 目前應徵階段，供 UI 顯示與查詢使用。
        /// 此值是事件紀錄的快取；ApplicationEvent 與 reducer 完成後，必須能由事件重新推導或驗證。
        /// </summary>
        public ApplicationStage CurrentStage { get; set; }

        /// <summary>
        /// 建立本次應徵紀錄的時間，必須保留時區資訊。
        /// 使用 nullable 是為了表示尚未通過驗證的缺漏資料；正式保存前不得為 null。
        /// </summary>
        public DateTimeOffset? CreatedAt { get; set; }

        /// <summary>
        /// 最近一次修改本次應徵資料的時間，必須保留時區資訊，且不得早於 CreatedAt。
        /// 使用 nullable 是為了讓 Validator 能明確回報舊資料缺漏。
        /// </summary>
        public DateTimeOffset? UpdatedAt { get; set; }

        /// <summary>
        /// 使用者主動停止應徵時的原因。
        /// 只有 CurrentStage 為 ClosedByCandidate 時才能填寫；公司拒絕不可使用此欄位。
        /// </summary>
        public CandidateCloseReason? CandidateCloseReason { get; set; }

        /// <summary>
        /// 使用 CandidateCloseReason.Other 時必填的補充說明。
        /// 其他結案原因也可以保留補充文字，但不得取代固定原因代碼。
        /// </summary>
        public string CandidateCloseReasonNote { get; set; }

        /// <summary>
        /// 使用者針對本次應徵留下的自由文字備註，不屬於正式流程階段。
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// 是否將這筆應徵標記為收藏。收藏是個人整理狀態，不代表已投遞。
        /// </summary>
        public bool IsFavorite { get; set; }

        /// <summary>
        /// 是否將這筆應徵收進歷史或整理區。封存不會改變 CurrentStage 或結案結果，
        /// 也不得用來隱藏仍需要追蹤的本人應徵。
        /// </summary>
        public bool IsArchived { get; set; }

        /// <summary>
        /// 封存這筆應徵的整理原因。IsArchived 為 true 時必填，未封存時不得填寫。
        /// </summary>
        public ArchiveReason? ArchiveReason { get; set; }

        /// <summary>
        /// 使用者自行指定的後續追蹤時間，必須保留時區資訊。
        /// 此時間是提醒用途，不會自行改變 CurrentStage。
        /// </summary>
        public DateTimeOffset? ManualFollowUpAt { get; set; }

        /// <summary>
        /// 再次投遞同一 JobPosting 時，指向最近一筆既有 Application 的 ID。
        /// 只連結直接前一筆，不保存完整清單，且不得指向自己或形成循環。
        /// </summary>
        public string PreviousApplicationId { get; set; }

        /// <summary>
        /// Migration 保留的 V0.1 原始狀態文字，僅供追查轉換結果。
        /// 新功能不得依賴此欄位判斷目前流程階段。
        /// </summary>
        public string LegacyStatus { get; set; }

        /// <summary>
        /// 是否發現互斥狀態、異常事件順序或其他需要人工確認的資料問題。
        /// true 不代表資料已自動修正。
        /// </summary>
        public bool NeedsReview { get; set; }
    }
}
