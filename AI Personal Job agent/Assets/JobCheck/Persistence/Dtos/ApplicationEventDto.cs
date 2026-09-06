using System;

namespace JobCheck.Persistence
{
    /// <summary>
    /// ApplicationEvent 的 V0.2 JSON 搬運格式。
    /// enum 與 DateTimeOffset 先保存為字串，交由 Mapper 明確轉換並回報錯誤。
    /// </summary>
    [Serializable]
    public sealed class ApplicationEventDto
    {
        public string id;
        public string schema_version;
        public string application_id;
        public string event_type;
        public string occurred_at;
        public string recorded_at;
        public string actor;
        public string notes;
        public string source_reference;
        public string legacy_status;
        public string time_precision;
        public string supersedes_event_id;
        public string snapshot_stage;
    }
}
