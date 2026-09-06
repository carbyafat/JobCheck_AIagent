using System;
using System.Collections.Generic;

namespace JobCheck.Persistence
{
    /// <summary>
    /// Application 與其事件歷史的 V0.2 JSON 搬運格式。
    /// 第一版將 events 內嵌，避免應徵主檔與事件分開寫入後只成功一半。
    /// </summary>
    [Serializable]
    public sealed class ApplicationDto
    {
        public string id;
        public string schema_version;
        public string job_posting_id;
        public string source_type;
        public string current_stage;
        public string created_at;
        public string updated_at;
        public string candidate_close_reason;
        public string candidate_close_reason_note;
        public string notes;
        public bool is_favorite;
        public bool is_archived;
        public string archive_reason;
        public string manual_follow_up_at;
        public string previous_application_id;
        public string legacy_status;
        public bool needs_review;
        public List<ApplicationEventDto> events = new List<ApplicationEventDto>();
    }
}
