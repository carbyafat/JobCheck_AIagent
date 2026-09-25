using System;
using System.Collections.Generic;

namespace JobCheck.Domain
{
    /// <summary>
    /// 使用者自用的職涯母資料。它不等同於對外投遞的單一版履歷，允許任何區塊暫時留白。
    /// </summary>
    [Serializable]
    public sealed class CareerProfile
    {
        public const string CurrentSchemaVersion = "0.2";

        public string Id { get; set; }
        public string SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string Summary { get; set; }
        public List<CareerProfileLink> Links { get; set; } = new List<CareerProfileLink>();
        public List<CareerSkill> Skills { get; set; } = new List<CareerSkill>();
        public List<CareerExperience> Experiences { get; set; } = new List<CareerExperience>();
        public List<CareerProject> Projects { get; set; } = new List<CareerProject>();
        public List<CareerEducation> Educations { get; set; } = new List<CareerEducation>();
        public List<CareerLanguage> Languages { get; set; } = new List<CareerLanguage>();
        public JobSearchPreferences JobPreferences { get; set; } = new JobSearchPreferences();
        public DateTimeOffset? CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }

    [Serializable]
    public sealed class CareerProfileLink
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Url { get; set; }
    }

    [Serializable]
    public sealed class CareerSkill
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Notes { get; set; }
        public string SkillId { get; set; }
        public SkillLevel? Level { get; set; }
        public int? ClaimedMonths { get; set; }
    }

    [Serializable]
    public sealed class CareerExperience
    {
        public string Id { get; set; }
        public string Organization { get; set; }
        public string Role { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public bool IsCurrent { get; set; }
        public string Description { get; set; }
        public List<string> SkillIds { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class CareerProject
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Technologies { get; set; } = new List<string>();
        public string Url { get; set; }
    }

    [Serializable]
    public sealed class CareerEducation
    {
        public string Id { get; set; }
        public string Institution { get; set; }
        public string Program { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string Notes { get; set; }
        public DegreeLevel? DegreeLevel { get; set; }
        public EducationCompletionStatus CompletionStatus { get; set; }
            = EducationCompletionStatus.Unknown;
        public List<string> FieldTags { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class CareerLanguage
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Level { get; set; }
        public string Notes { get; set; }
        public string LanguageId { get; set; }
        public LanguageProficiency? Proficiency { get; set; }
        public List<string> Certifications { get; set; } = new List<string>();
    }

    public enum PreferenceImportance
    {
        Required,
        Preferred,
        Indifferent
    }

    public enum SalaryPeriod
    {
        Monthly,
        Annual,
        Hourly
    }

    /// <summary>使用者希望尋找的工作條件；與履歷能力及單筆職缺資料分開。</summary>
    [Serializable]
    public sealed class JobSearchPreferences
    {
        public List<string> TargetRoles { get; set; } = new List<string>();
        public List<string> Industries { get; set; } = new List<string>();
        public List<string> AcceptedRegions { get; set; } = new List<string>();
        public List<string> EmploymentTypes { get; set; } = new List<string>();
        public List<string> WorkModes { get; set; } = new List<string>();
        public List<string> WorkSchedules { get; set; } = new List<string>();
        public SalaryPeriod SalaryPeriod { get; set; } = SalaryPeriod.Monthly;
        public int? MinimumSalary { get; set; }
        public int? DesiredSalary { get; set; }
        public int? MaximumCommuteMinutes { get; set; }
        public string OvertimePreference { get; set; }
        public string TravelPreference { get; set; }
        public string RelocationPreference { get; set; }
        public string Notes { get; set; }
        public PreferenceImportance TargetImportance { get; set; }
            = PreferenceImportance.Required;
        public PreferenceImportance SalaryImportance { get; set; }
            = PreferenceImportance.Required;
        public PreferenceImportance ArrangementImportance { get; set; }
            = PreferenceImportance.Preferred;
        public PreferenceImportance ScheduleImportance { get; set; }
            = PreferenceImportance.Preferred;
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
