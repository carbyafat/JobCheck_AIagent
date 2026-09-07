using System;
using System.Collections.Generic;

namespace JobCheck.Persistence
{
    /// <summary>
    /// 只供 V0.1 demo migration 使用的舊職缺格式。
    /// 未列出的解析 metadata 會由 JSON reader 忽略，不會混入新版 Domain。
    /// </summary>
    [Serializable]
    public sealed class LegacyV01JobDto
    {
        public string schema_version;
        public string id;
        public string parse_status;
        public LegacyV01SourceDto source;
        public LegacyV01CompanyDto company;
        public LegacyV01JobInfoDto job;
        public LegacyV01CompensationDto compensation;
        public LegacyV01LocationDto location;
        public LegacyV01WorkConditionsDto work_conditions;
        public List<string> responsibilities = new List<string>();
        public LegacyV01RequirementsDto requirements;
        public LegacyV01BenefitsDto benefits;
        public List<string> recruitment_process = new List<string>();
        public LegacyV01EmbeddedTrackingDto tracking;
    }

    [Serializable]
    public sealed class LegacyV01SourceDto
    {
        public string platform;
        public string url;
        public string captured_at;
        public List<string> raw_images = new List<string>();
    }

    [Serializable]
    public sealed class LegacyV01CompanyDto
    {
        public string name;
        public string industry;
        public string raw_text;
    }

    [Serializable]
    public sealed class LegacyV01JobInfoDto
    {
        public string title;
        public string department;
        public string category;
        public string updated_date;
        public string openings;
        public string raw_text;
    }

    [Serializable]
    public sealed class LegacyV01CompensationDto
    {
        public string type;
        public string period;
        public int? min;
        public int? max;
        public string currency;
        public string raw_text;
        public string notes;
    }

    [Serializable]
    public sealed class LegacyV01LocationDto
    {
        public string work_mode;
        public string city;
        public string district;
        public string address;
        public bool remote_allowed;
        public string raw_text;
    }

    [Serializable]
    public sealed class LegacyV01WorkConditionsDto
    {
        public string employment_type;
        public string working_hours;
        public string business_trip;
        public string management_responsibility;
        public string leave_policy;
        public string start_date;
    }

    [Serializable]
    public sealed class LegacyV01RequirementsDto
    {
        public string experience;
        public string education;
        public string major;
        public List<LegacyV01LanguageDto> languages = new List<LegacyV01LanguageDto>();
        public List<string> tools = new List<string>();
        public List<string> skills = new List<string>();
        public List<string> other_conditions = new List<string>();
    }

    [Serializable]
    public sealed class LegacyV01LanguageDto
    {
        public string name;
        public string listening;
        public string speaking;
        public string reading;
        public string writing;
        public string raw_text;
    }

    [Serializable]
    public sealed class LegacyV01BenefitsDto
    {
        public List<string> salary_bonus = new List<string>();
        public List<string> insurance_health = new List<string>();
        public List<string> flexibility = new List<string>();
        public List<string> training = new List<string>();
        public List<string> life = new List<string>();
        public List<string> other = new List<string>();
    }

    /// <summary>
    /// 舊職缺檔內嵌的 tracking；獨立 job_tracking 檔存在時以獨立檔為準。
    /// </summary>
    [Serializable]
    public sealed class LegacyV01EmbeddedTrackingDto
    {
        public string status;
        public int? priority;
        public bool favorite;
        public string applied_at;
        public string last_updated_at;
        public string notes;
    }

    /// <summary>
    /// V0.1 job_tracking/*.tracking.json 的格式。
    /// </summary>
    [Serializable]
    public sealed class LegacyV01TrackingDto
    {
        public string job_id;
        public string status;
        public string last_action_at;
        public string manual_expire_at;
        public bool favorite;
        public int fit_score = -1;
    }
}
