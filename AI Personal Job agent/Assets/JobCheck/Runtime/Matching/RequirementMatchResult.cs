using System;
using System.Collections.Generic;

namespace JobCheck.Domain
{
    public enum RequirementMatchStatus
    {
        Match = 0,
        ConfirmedMismatch = 1,
        NotEvidenced = 2,
        RequirementUnclear = 3
    }

    public enum RequirementCategory
    {
        Skill = 0,
        Experience = 1,
        Education = 2,
        Language = 3
    }

    [Serializable]
    public sealed class RequirementMatchItem
    {
        public RequirementCategory Category { get; set; }
        public RequirementMatchStatus Status { get; set; }
        public RequirementImportance Importance { get; set; }
        public string Label { get; set; }
        public string RequiredValue { get; set; }
        public string ActualValue { get; set; }
        public string Explanation { get; set; }
    }

    [Serializable]
    public sealed class RequirementMatchResult
    {
        public List<RequirementMatchItem> Items { get; set; }
            = new List<RequirementMatchItem>();
    }
}
