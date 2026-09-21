using System;
using System.Collections.Generic;

namespace JobCheck.Persistence
{
    /// <summary>
    /// JobPosting 的 V0.2 JSON 搬運格式。
    /// 此類別只描述保存形狀，不判斷薪資、關聯或內容是否合理。
    /// </summary>
    [Serializable]
    public sealed class JobPostingDto
    {
        public string id;
        public string schema_version;
        public string company_id;
        public string title;
        public JobSourceDto source;
        public string captured_at;
        public string department;
        public string category;
        public JobCompensationDto compensation;
        public JobLocationDto location;
        public JobWorkConditionsDto work_conditions;
        public List<string> responsibilities = new List<string>();
        public JobRequirementsDto requirements;
        public JobBenefitsDto benefits;
        public List<string> recruitment_process = new List<string>();
        public List<string> tags = new List<string>();
        public List<string> risk_flags = new List<string>();
        public string raw_description;
        public string legacy_id;
    }

    /// <summary>
    /// 職缺來源的 JSON 格式。
    /// </summary>
    [Serializable]
    public sealed class JobSourceDto
    {
        public string platform;
        public string url;
    }

    /// <summary>
    /// 薪資條件的 JSON 格式。
    /// has_min 與 has_max 用來區分數值 0 和來源未提供，不可由 min/max 是否為 0 猜測。
    /// </summary>
    [Serializable]
    public sealed class JobCompensationDto
    {
        public string type;
        public string period;
        public bool has_min;
        public int min;
        public bool has_max;
        public int max;
        public string currency;
        public string raw_text;
        public string notes;
    }

    /// <summary>
    /// 工作地點的 JSON 格式。
    /// has_remote_allowed 為 false 時代表來源沒有說明，不能把 remote_allowed 的預設 false 當成答案。
    /// </summary>
    [Serializable]
    public sealed class JobLocationDto
    {
        public string work_mode;
        public string city;
        public string district;
        public string address;
        public bool has_remote_allowed;
        public bool remote_allowed;
        public string raw_text;
    }

    /// <summary>
    /// 工作條件的 JSON 格式。
    /// </summary>
    [Serializable]
    public sealed class JobWorkConditionsDto
    {
        public string employment_type;
        public string working_hours;
        public string business_trip;
        public string management_responsibility;
        public string leave_policy;
        public string start_date;
    }

    /// <summary>
    /// 任職需求的 JSON 格式。
    /// </summary>
    [Serializable]
    public sealed class JobRequirementsDto
    {
        public string experience;
        public string education;
        public string major;
        public List<JobLanguageRequirementDto> languages
            = new List<JobLanguageRequirementDto>();
        public List<string> tools = new List<string>();
        public List<string> skills = new List<string>();
        public List<string> other_conditions = new List<string>();
        public List<SkillRequirementDto> skill_requirements = new List<SkillRequirementDto>();
        public List<ExperienceRequirementDto> experience_requirements
            = new List<ExperienceRequirementDto>();
        public EducationRequirementDto education_requirement;
    }

    [Serializable]
    public sealed class SkillRequirementDto
    {
        public string skill_id;
        public string name;
        public string importance;
        public string minimum_level;
        public bool has_minimum_months;
        public int minimum_months;
    }

    [Serializable]
    public sealed class ExperienceRequirementDto
    {
        public string skill_id;
        public string name;
        public int minimum_months;
        public string importance;
    }

    [Serializable]
    public sealed class EducationRequirementDto
    {
        public string minimum_degree_level;
        public bool accepts_in_progress;
        public List<string> field_tags = new List<string>();
        public string importance;
    }

    /// <summary>
    /// 單一語文要求的 JSON 格式。
    /// </summary>
    [Serializable]
    public sealed class JobLanguageRequirementDto
    {
        public string name;
        public string listening;
        public string speaking;
        public string reading;
        public string writing;
        public string raw_text;
        public string language_id;
        public string minimum_proficiency;
        public string importance;
        public List<string> accepted_certifications = new List<string>();
    }

    /// <summary>
    /// 福利分類的 JSON 格式。
    /// </summary>
    [Serializable]
    public sealed class JobBenefitsDto
    {
        public List<string> salary_bonus = new List<string>();
        public List<string> insurance_health = new List<string>();
        public List<string> flexibility = new List<string>();
        public List<string> training = new List<string>();
        public List<string> life = new List<string>();
        public List<string> other = new List<string>();
    }
}
