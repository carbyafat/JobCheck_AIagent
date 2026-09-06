using System;
using System.Collections.Generic;

namespace JobCheck.Persistence
{
    /// <summary>
    /// Company 的 V0.2 JSON 搬運格式。
    /// 欄位名稱刻意使用 snake_case，避免 JsonUtility 需要額外命名轉換。
    /// </summary>
    [Serializable]
    public sealed class CompanyDto
    {
        public string id;
        public string schema_version;
        public string name;
        public string industry;
        public string notes;
        public List<string> risk_flags = new List<string>();
        public string created_at;
        public string updated_at;
    }
}
