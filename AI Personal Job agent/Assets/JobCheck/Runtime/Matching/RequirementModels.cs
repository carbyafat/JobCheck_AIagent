using System;
using System.Collections.Generic;

namespace JobCheck.Domain
{
    public enum RequirementImportance
    {
        Required = 0,
        Preferred = 1
    }

    public enum SkillLevel
    {
        Beginner = 1,
        Basic = 2,
        Intermediate = 3,
        Advanced = 4,
        Expert = 5
    }

    public enum DegreeLevel
    {
        Unrestricted = 0,
        HighSchool = 1,
        Associate = 2,
        Bachelor = 3,
        Master = 4,
        Doctorate = 5
    }

    public enum EducationCompletionStatus
    {
        Unknown = 0,
        InProgress = 1,
        Completed = 2,
        Incomplete = 3
    }

    public enum LanguageProficiency
    {
        Beginner = 1,
        Elementary = 2,
        Intermediate = 3,
        UpperIntermediate = 4,
        Advanced = 5,
        Proficient = 6
    }

    [Serializable]
    public sealed class SkillRequirement
    {
        public string SkillId { get; set; }
        public string Name { get; set; }
        public RequirementImportance Importance { get; set; } = RequirementImportance.Required;
        public SkillLevel? MinimumLevel { get; set; }
        public int? MinimumMonths { get; set; }
    }

    [Serializable]
    public sealed class ExperienceRequirement
    {
        /// <summary>空白代表總工作年資；有值時代表特定技能相關年資。</summary>
        public string SkillId { get; set; }
        public string Name { get; set; }
        public int MinimumMonths { get; set; }
        public RequirementImportance Importance { get; set; } = RequirementImportance.Required;
    }

    [Serializable]
    public sealed class EducationRequirement
    {
        public DegreeLevel MinimumDegreeLevel { get; set; }
        public bool AcceptsInProgress { get; set; }
        public List<string> FieldTags { get; set; } = new List<string>();
        public RequirementImportance Importance { get; set; } = RequirementImportance.Required;
    }
}
