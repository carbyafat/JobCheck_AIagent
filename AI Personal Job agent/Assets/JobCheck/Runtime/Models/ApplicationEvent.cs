using System;

namespace JobCheck.Domain
{
    /// <summary>
    /// 代表一筆已經發生的應徵流程事件。
    /// 事件建立後應視為不可變歷史；需要修正時新增 DataCorrected，而不是覆蓋原事件。
    /// </summary>
    [Serializable]
    public sealed class ApplicationEvent
    {
        /// <summary>
        /// 目前 ApplicationEvent 資料結構的版本。寫入 V0.2 格式時應使用此值。
        /// </summary>
        public const string CurrentSchemaVersion = "0.2";

        /// <summary>
        /// 舊資料只有日期、沒有可靠時間時使用的 time_precision 固定值。
        /// null 表示 OccurredAt 包含可採信的日期與時間。
        /// </summary>
        public const string DateTimePrecision = "date";

        /// <summary>
        /// 事件不可變的唯一識別碼，格式為 evt_ 加上 32 位小寫十六進位 UUID。
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 此筆事件資料所使用的結構版本。目前正式版本為 0.2。
        /// 這不是事件種類或修改次數。
        /// </summary>
        public string SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>
        /// 此事件所屬的 Application ID。
        /// 對應 Application 是否存在，需要在可存取完整資料集合時做關聯驗證。
        /// </summary>
        public string ApplicationId { get; set; }

        /// <summary>
        /// 已發生的求職流程事件種類，例如 Applied、Viewed 或 InterviewCompleted。
        /// </summary>
        public ApplicationEventType EventType { get; set; }

        /// <summary>
        /// 事件實際發生的時間，必須保留時區資訊。
        /// 使用 nullable 是為了讓 Validator 能明確回報缺漏資料；正式保存前不得為 null。
        /// </summary>
        public DateTimeOffset? OccurredAt { get; set; }

        /// <summary>
        /// 此事件被輸入 JobCheck 的時間，必須保留時區資訊。
        /// 補登歷史事件時可以晚於 OccurredAt，但不得早於所屬 Application 的建立時間。
        /// </summary>
        public DateTimeOffset? RecordedAt { get; set; }

        /// <summary>
        /// 造成事件發生的角色，而不是負責將資料輸入系統的人。
        /// </summary>
        public EventActor Actor { get; set; }

        /// <summary>
        /// 此事件的自由文字補充，例如聯絡內容或面試輪次。
        /// 不得只靠 Notes 取代正式的 EventType。
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// 可追查事件來源的參考資料，例如平台訊息 ID、匯入檔案名稱或原始資料索引。
        /// 此欄位不是 Application 或 JobPosting 的關聯 ID。
        /// </summary>
        public string SourceReference { get; set; }

        /// <summary>
        /// Migration 保留的 V0.1 原始狀態文字，只供追查轉換結果。
        /// 新流程不得依賴此欄位判斷目前階段。
        /// </summary>
        public string LegacyStatus { get; set; }

        /// <summary>
        /// 舊資料時間的精確度。只有日期而沒有可靠時間時填入 date；
        /// 有可靠日期與時間時保持 null，不得自行捏造時間精度。
        /// </summary>
        public string TimePrecision { get; set; }

        /// <summary>
        /// DataCorrected 事件所要排除的原事件 ID。
        /// 修正後的事實應另外建立一筆正常事件；此欄位不得用來直接改寫舊事件。
        /// </summary>
        public string SupersedesEventId { get; set; }

        /// <summary>
        /// MigrationSnapshot 在轉換當下保存的 Application 階段。
        /// 只有 MigrationSnapshot 可以填寫；一般流程事件的階段由 EventType 推導。
        /// </summary>
        public ApplicationStage? SnapshotStage { get; set; }
    }
}
