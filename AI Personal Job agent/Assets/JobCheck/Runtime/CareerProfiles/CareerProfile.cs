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
    }

    [Serializable]
    public sealed class CareerLanguage
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Level { get; set; }
        public string Notes { get; set; }
    }
}
