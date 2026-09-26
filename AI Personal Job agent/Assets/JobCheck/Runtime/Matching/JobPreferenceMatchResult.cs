using System;
using System.Collections.Generic;

namespace JobCheck.Domain
{
    public enum JobPreferenceCategory
    {
        TargetRole = 0,
        Industry = 1,
        Salary = 2,
        Region = 3,
        EmploymentType = 4,
        WorkMode = 5,
        WorkSchedule = 6,
        Commute = 7,
        Overtime = 8,
        Travel = 9,
        Relocation = 10
    }

    public enum JobPreferenceMatchStatus
    {
        Match = 0,
        PartialMatch = 1,
        ConfirmedMismatch = 2,
        JobDataUnknown = 3,
        AiContextOnly = 4
    }

    [Serializable]
    public sealed class JobPreferenceMatchItem
    {
        public JobPreferenceCategory Category { get; set; }
        public JobPreferenceMatchStatus Status { get; set; }
        public PreferenceImportance Importance { get; set; }
        public string Label { get; set; }
        public string PreferredValue { get; set; }
        public string JobValue { get; set; }
        public string Explanation { get; set; }
    }

    [Serializable]
    public sealed class JobPreferenceMatchResult
    {
        public List<JobPreferenceMatchItem> Items { get; set; }
            = new List<JobPreferenceMatchItem>();
    }
}
