using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace JobCheck.Domain
{
    /// <summary>不讀寫資料、不呼叫 AI 的確定性履歷／職缺比對器。</summary>
    public static class RequirementMatchEngine
    {
        public static RequirementMatchResult Compare(
            CareerProfile profile,
            JobRequirements requirements,
            DateTimeOffset evaluatedAt)
        {
            var result = new RequirementMatchResult();
            if (requirements == null)
            {
                return result;
            }

            EvaluateSkills(profile, requirements, result.Items);
            EvaluateExperience(profile, requirements, evaluatedAt, result.Items);
            EvaluateEducation(profile, requirements, result.Items);
            EvaluateLanguages(profile, requirements, result.Items);
            return result;
        }

        private static void EvaluateSkills(
            CareerProfile profile,
            JobRequirements requirements,
            ICollection<RequirementMatchItem> output)
        {
            List<SkillRequirement> required = BuildSkillRequirements(requirements);
            foreach (SkillRequirement requirement in required)
            {
                string skillId = RequirementCatalog.NormalizeSkill(
                    string.IsNullOrWhiteSpace(requirement.SkillId)
                        ? requirement.Name
                        : requirement.SkillId);
                string label = string.IsNullOrWhiteSpace(requirement.Name)
                    ? skillId
                    : requirement.Name.Trim();
                if (skillId.Length == 0)
                {
                    output.Add(Item(RequirementCategory.Skill,
                        RequirementMatchStatus.RequirementUnclear,
                        requirement.Importance, "未命名技能", "條件未標準化", null,
                        "職缺技能缺少可辨識名稱。"));
                    continue;
                }

                CareerSkill actual = (profile?.Skills ?? new List<CareerSkill>())
                    .Where(item => item != null)
                    .FirstOrDefault(item => string.Equals(
                        RequirementCatalog.NormalizeSkill(
                            string.IsNullOrWhiteSpace(item.SkillId) ? item.Name : item.SkillId),
                        skillId,
                        StringComparison.Ordinal));
                if (actual == null)
                {
                    output.Add(Item(RequirementCategory.Skill,
                        RequirementMatchStatus.NotEvidenced, requirement.Importance, label,
                        FormatSkillRequirement(requirement), "履歷未列出",
                        "履歷沒有這項技能的可驗證資料。"));
                    continue;
                }

                bool levelMismatch = requirement.MinimumLevel.HasValue
                    && (!actual.Level.HasValue
                        || actual.Level.Value < requirement.MinimumLevel.Value);
                bool monthsMismatch = requirement.MinimumMonths.HasValue
                    && (!actual.ClaimedMonths.HasValue
                        || actual.ClaimedMonths.Value < requirement.MinimumMonths.Value);
                bool missingDetail = (requirement.MinimumLevel.HasValue && !actual.Level.HasValue)
                    || (requirement.MinimumMonths.HasValue && !actual.ClaimedMonths.HasValue);
                RequirementMatchStatus status = missingDetail
                    ? RequirementMatchStatus.NotEvidenced
                    : levelMismatch || monthsMismatch
                        ? RequirementMatchStatus.ConfirmedMismatch
                        : RequirementMatchStatus.Match;
                output.Add(Item(RequirementCategory.Skill, status, requirement.Importance, label,
                    FormatSkillRequirement(requirement), FormatCareerSkill(actual),
                    status == RequirementMatchStatus.Match
                        ? "技能名稱與明確門檻皆符合。"
                        : status == RequirementMatchStatus.NotEvidenced
                            ? "有列出技能，但缺少等級或年資證據。"
                            : "技能已列出，但明確門檻不足。"));
            }
        }

        private static void EvaluateExperience(
            CareerProfile profile,
            JobRequirements requirements,
            DateTimeOffset evaluatedAt,
            ICollection<RequirementMatchItem> output)
        {
            List<ExperienceRequirement> experienceRequirements =
                requirements.ExperienceRequirements == null
                    ? new List<ExperienceRequirement>()
                    : requirements.ExperienceRequirements.Where(item => item != null).ToList();
            if (experienceRequirements.Count == 0
                && RequirementInputParser.TryParseMinimumExperience(
                    requirements.Experience, out int parsedMonths)
                && parsedMonths > 0)
            {
                experienceRequirements.Add(new ExperienceRequirement
                {
                    Name = "總工作年資",
                    MinimumMonths = parsedMonths
                });
            }

            if (experienceRequirements.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(requirements.Experience)
                    && !RequirementInputParser.TryParseMinimumExperience(
                        requirements.Experience, out int _))
                {
                    output.Add(Item(RequirementCategory.Experience,
                        RequirementMatchStatus.RequirementUnclear,
                        RequirementImportance.Required, "工作年資",
                        requirements.Experience.Trim(), null,
                        "職缺只有自由文字年資，尚未轉成月份。"));
                }

                return;
            }

            foreach (ExperienceRequirement requirement in experienceRequirements)
            {
                if (requirement.MinimumMonths <= 0)
                {
                    output.Add(Item(RequirementCategory.Experience,
                        RequirementMatchStatus.RequirementUnclear,
                        requirement.Importance, DisplayExperienceLabel(requirement),
                        "月份未設定", null, "年資門檻必須大於零。"));
                    continue;
                }

                string skillId = RequirementCatalog.NormalizeSkill(requirement.SkillId);
                List<CareerExperience> relevant = (profile?.Experiences
                    ?? new List<CareerExperience>())
                    .Where(item => item != null
                        && (skillId.Length == 0 || HasSkill(item.SkillIds, skillId)))
                    .ToList();
                int months = CalculateCoveredMonths(relevant, evaluatedAt);
                bool hasEvidence = relevant.Any(item => TryGetInterval(
                    item, evaluatedAt, out DateTime _, out DateTime _));
                if (skillId.Length > 0)
                {
                    bool hasClaimedMonths = (profile?.Skills ?? new List<CareerSkill>())
                        .Any(item => item != null
                            && string.Equals(RequirementCatalog.NormalizeSkill(
                                string.IsNullOrWhiteSpace(item.SkillId)
                                    ? item.Name : item.SkillId), skillId,
                                StringComparison.Ordinal)
                            && item.ClaimedMonths.HasValue);
                    int claimed = (profile?.Skills ?? new List<CareerSkill>())
                        .Where(item => item != null
                            && string.Equals(RequirementCatalog.NormalizeSkill(
                                string.IsNullOrWhiteSpace(item.SkillId)
                                    ? item.Name
                                    : item.SkillId), skillId, StringComparison.Ordinal)
                            && item.ClaimedMonths.HasValue)
                        .Select(item => item.ClaimedMonths.Value)
                        .DefaultIfEmpty(0)
                        .Max();
                    months = Math.Max(months, claimed);
                    hasEvidence = hasEvidence || hasClaimedMonths;
                }

                RequirementMatchStatus status = !hasEvidence
                    ? RequirementMatchStatus.NotEvidenced
                    : months >= requirement.MinimumMonths
                        ? RequirementMatchStatus.Match
                        : RequirementMatchStatus.ConfirmedMismatch;
                output.Add(Item(RequirementCategory.Experience, status,
                    requirement.Importance, DisplayExperienceLabel(requirement),
                    FormatMonths(requirement.MinimumMonths),
                    hasEvidence ? FormatMonths(months) : "履歷未提供可計算日期",
                    status == RequirementMatchStatus.Match
                        ? "合併重疊區間後符合年資門檻。"
                        : status == RequirementMatchStatus.NotEvidenced
                            ? "履歷缺少可計算的起訖日期或技能標籤。"
                            : "已有可計算年資，但尚未達到門檻。"));
            }
        }

        private static void EvaluateEducation(
            CareerProfile profile,
            JobRequirements requirements,
            ICollection<RequirementMatchItem> output)
        {
            EducationRequirement requirement = requirements.EducationRequirement;
            if (requirement == null
                && RequirementInputParser.TryParseMinimumEducation(
                    requirements.Education, out DegreeLevel parsedDegree,
                    out bool acceptsInProgress))
            {
                requirement = new EducationRequirement
                {
                    MinimumDegreeLevel = parsedDegree,
                    AcceptsInProgress = acceptsInProgress
                };
            }
            if (requirement == null)
            {
                if (!string.IsNullOrWhiteSpace(requirements.Education)
                    || !string.IsNullOrWhiteSpace(requirements.Major))
                {
                    output.Add(Item(RequirementCategory.Education,
                        RequirementMatchStatus.RequirementUnclear,
                        RequirementImportance.Required, "學歷",
                        JoinNonEmpty(" / ", requirements.Education, requirements.Major), null,
                        "職缺學歷仍是自由文字，尚未轉成等級與科系標籤。"));
                }

                return;
            }

            if (requirement.MinimumDegreeLevel == DegreeLevel.Unrestricted)
            {
                AddUnstructuredMajor(requirements, output);
                return;
            }

            List<CareerEducation> educations = (profile?.Educations
                ?? new List<CareerEducation>()).Where(item => item != null).ToList();
            List<CareerEducation> evidenced = educations
                .Where(item => item.DegreeLevel.HasValue).ToList();
            bool levelMatch = evidenced.Any(item =>
                item.DegreeLevel.Value >= requirement.MinimumDegreeLevel
                && (item.CompletionStatus == EducationCompletionStatus.Completed
                    || (requirement.AcceptsInProgress
                        && item.CompletionStatus == EducationCompletionStatus.InProgress)));
            RequirementMatchStatus levelStatus = evidenced.Count == 0
                ? RequirementMatchStatus.NotEvidenced
                : levelMatch
                    ? RequirementMatchStatus.Match
                    : RequirementMatchStatus.ConfirmedMismatch;
            output.Add(Item(RequirementCategory.Education, levelStatus,
                requirement.Importance, "最低學歷",
                DegreeText(requirement.MinimumDegreeLevel)
                    + (requirement.AcceptsInProgress ? "（在學可）" : "（須完成）"),
                evidenced.Count == 0
                    ? "履歷未標準化學歷"
                    : string.Join("、", evidenced.Select(item =>
                        DegreeText(item.DegreeLevel.Value) + CompletionText(item.CompletionStatus))),
                levelStatus == RequirementMatchStatus.Match
                    ? "學位等級與完成狀態符合。"
                    : levelStatus == RequirementMatchStatus.NotEvidenced
                        ? "履歷尚未提供可比較的學位等級。"
                        : "已有明確學歷資料，但未達門檻。"));

            if (requirement.FieldTags != null && requirement.FieldTags.Count > 0)
            {
                string[] requiredTags = requirement.FieldTags
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(NormalizeText).Distinct().ToArray();
                string[] actualTags = educations
                    .SelectMany(item => item.FieldTags ?? new List<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(NormalizeText).Distinct().ToArray();
                bool fieldMatch = requiredTags.Any(tag => actualTags.Contains(tag));
                RequirementMatchStatus fieldStatus = actualTags.Length == 0
                    ? RequirementMatchStatus.NotEvidenced
                    : fieldMatch
                        ? RequirementMatchStatus.Match
                        : RequirementMatchStatus.ConfirmedMismatch;
                output.Add(Item(RequirementCategory.Education, fieldStatus,
                    requirement.Importance, "科系", string.Join("、", requirement.FieldTags),
                    actualTags.Length == 0 ? "履歷未列科系標籤" : string.Join("、", actualTags),
                    fieldStatus == RequirementMatchStatus.Match
                        ? "科系標籤有明確交集。"
                        : fieldStatus == RequirementMatchStatus.NotEvidenced
                            ? "履歷沒有可比較的科系標籤。"
                            : "已填科系標籤與職缺要求沒有交集。"));
            }
            else
            {
                AddUnstructuredMajor(requirements, output);
            }
        }

        private static void AddUnstructuredMajor(
            JobRequirements requirements,
            ICollection<RequirementMatchItem> output)
        {
            if (!string.IsNullOrWhiteSpace(requirements.Major))
            {
                output.Add(Item(RequirementCategory.Education,
                    RequirementMatchStatus.RequirementUnclear,
                    RequirementImportance.Required, "科系", requirements.Major,
                    null, "科系條件尚未轉成明確標籤。"));
            }
        }

        private static void EvaluateLanguages(
            CareerProfile profile,
            JobRequirements requirements,
            ICollection<RequirementMatchItem> output)
        {
            foreach (JobLanguageRequirement requirement in requirements.Languages
                ?? new List<JobLanguageRequirement>())
            {
                if (requirement == null)
                {
                    continue;
                }

                string languageId = RequirementCatalog.NormalizeLanguage(
                    string.IsNullOrWhiteSpace(requirement.LanguageId)
                        ? requirement.Name
                        : requirement.LanguageId);
                if (languageId.Length == 0 || !requirement.MinimumProficiency.HasValue)
                {
                    output.Add(Item(RequirementCategory.Language,
                        RequirementMatchStatus.RequirementUnclear,
                        requirement.Importance,
                        string.IsNullOrWhiteSpace(requirement.Name) ? "語言" : requirement.Name,
                        string.IsNullOrWhiteSpace(requirement.RawText)
                            ? "程度未標準化"
                            : requirement.RawText,
                        null, "語言或最低程度尚未結構化。"));
                    continue;
                }

                CareerLanguage actual = (profile?.Languages ?? new List<CareerLanguage>())
                    .Where(item => item != null)
                    .FirstOrDefault(item => string.Equals(
                        RequirementCatalog.NormalizeLanguage(
                            string.IsNullOrWhiteSpace(item.LanguageId)
                                ? item.Name
                                : item.LanguageId), languageId, StringComparison.Ordinal));
                RequirementMatchStatus status = actual == null || !actual.Proficiency.HasValue
                    ? RequirementMatchStatus.NotEvidenced
                    : actual.Proficiency.Value >= requirement.MinimumProficiency.Value
                        ? RequirementMatchStatus.Match
                        : RequirementMatchStatus.ConfirmedMismatch;
                output.Add(Item(RequirementCategory.Language, status, requirement.Importance,
                    requirement.Name, requirement.MinimumProficiency.Value.ToString(),
                    actual?.Proficiency?.ToString() ?? "履歷未提供標準程度",
                    status == RequirementMatchStatus.Match
                        ? "語言程度符合。"
                        : status == RequirementMatchStatus.NotEvidenced
                            ? "履歷沒有可比較的語言程度。"
                            : "已填語言程度低於門檻。"));
            }
        }

        public static int CalculateCoveredMonths(
            IEnumerable<CareerExperience> experiences,
            DateTimeOffset evaluatedAt)
        {
            var intervals = new List<Tuple<DateTime, DateTime>>();
            foreach (CareerExperience item in experiences ?? Enumerable.Empty<CareerExperience>())
            {
                if (item != null && TryGetInterval(
                    item, evaluatedAt, out DateTime start, out DateTime end))
                {
                    intervals.Add(Tuple.Create(start, end));
                }
            }

            intervals.Sort((left, right) => left.Item1.CompareTo(right.Item1));
            int total = 0;
            DateTime? currentStart = null;
            DateTime? currentEnd = null;
            foreach (Tuple<DateTime, DateTime> interval in intervals)
            {
                if (!currentStart.HasValue)
                {
                    currentStart = interval.Item1;
                    currentEnd = interval.Item2;
                }
                else if (interval.Item1 <= currentEnd.Value)
                {
                    if (interval.Item2 > currentEnd.Value) currentEnd = interval.Item2;
                }
                else
                {
                    total += MonthsBetween(currentStart.Value, currentEnd.Value);
                    currentStart = interval.Item1;
                    currentEnd = interval.Item2;
                }
            }

            return currentStart.HasValue
                ? total + MonthsBetween(currentStart.Value, currentEnd.Value)
                : 0;
        }

        private static bool TryGetInterval(
            CareerExperience item,
            DateTimeOffset evaluatedAt,
            out DateTime start,
            out DateTime end)
        {
            start = default(DateTime);
            end = default(DateTime);
            if (!TryParseMonth(item.StartDate, out start)) return false;
            if (item.IsCurrent)
            {
                end = new DateTime(evaluatedAt.Year, evaluatedAt.Month, 1);
            }
            else if (!TryParseMonth(item.EndDate, out end))
            {
                return false;
            }

            return end >= start;
        }

        private static bool TryParseMonth(string value, out DateTime result)
        {
            result = default(DateTime);
            if (string.IsNullOrWhiteSpace(value)) return false;
            string[] formats = { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd", "yyyy-MM", "yyyy/MM", "yyyy.MM", "yyyy" };
            if (!DateTime.TryParseExact(value.Trim(), formats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            {
                return false;
            }

            result = new DateTime(parsed.Year, parsed.Month, 1);
            return true;
        }

        private static int MonthsBetween(DateTime start, DateTime end)
        {
            return Math.Max(0, (end.Year - start.Year) * 12 + end.Month - start.Month);
        }

        private static bool HasSkill(IEnumerable<string> skillIds, string expected)
        {
            return (skillIds ?? Enumerable.Empty<string>()).Any(value => string.Equals(
                RequirementCatalog.NormalizeSkill(value), expected, StringComparison.Ordinal));
        }

        private static List<SkillRequirement> BuildSkillRequirements(JobRequirements requirements)
        {
            var result = (requirements.SkillRequirements ?? new List<SkillRequirement>())
                .Where(item => item != null).ToList();
            var known = new HashSet<string>(result.Select(item => RequirementCatalog.NormalizeSkill(
                string.IsNullOrWhiteSpace(item.SkillId) ? item.Name : item.SkillId)));
            foreach (string value in (requirements.Tools ?? new List<string>())
                .Concat(requirements.Skills ?? new List<string>()))
            {
                string canonical = RequirementCatalog.NormalizeSkill(value);
                if (canonical.Length > 0 && known.Add(canonical))
                {
                    result.Add(new SkillRequirement { SkillId = canonical, Name = value });
                }
            }

            return result;
        }

        private static RequirementMatchItem Item(
            RequirementCategory category,
            RequirementMatchStatus status,
            RequirementImportance importance,
            string label,
            string required,
            string actual,
            string explanation)
        {
            return new RequirementMatchItem
            {
                Category = category,
                Status = status,
                Importance = importance,
                Label = label,
                RequiredValue = required,
                ActualValue = actual,
                Explanation = explanation
            };
        }

        private static string DisplayExperienceLabel(ExperienceRequirement value)
        {
            return string.IsNullOrWhiteSpace(value.Name)
                ? string.IsNullOrWhiteSpace(value.SkillId) ? "總工作年資" : value.SkillId + " 年資"
                : value.Name;
        }

        private static string FormatSkillRequirement(SkillRequirement value)
        {
            var parts = new List<string>();
            if (value.MinimumLevel.HasValue) parts.Add("等級 " + value.MinimumLevel.Value);
            if (value.MinimumMonths.HasValue) parts.Add(FormatMonths(value.MinimumMonths.Value));
            return parts.Count == 0 ? "需具備" : string.Join("、", parts);
        }

        private static string FormatCareerSkill(CareerSkill value)
        {
            var parts = new List<string> { value.Name };
            if (value.Level.HasValue) parts.Add("等級 " + value.Level.Value);
            if (value.ClaimedMonths.HasValue) parts.Add(FormatMonths(value.ClaimedMonths.Value));
            return string.Join("、", parts);
        }

        private static string FormatMonths(int months)
        {
            int years = months / 12;
            int remainder = months % 12;
            if (years == 0) return months + " 個月";
            return remainder == 0 ? years + " 年" : years + " 年 " + remainder + " 個月";
        }

        private static string DegreeText(DegreeLevel value)
        {
            switch (value)
            {
                case DegreeLevel.HighSchool: return "高中";
                case DegreeLevel.Associate: return "專科";
                case DegreeLevel.Bachelor: return "學士";
                case DegreeLevel.Master: return "碩士";
                case DegreeLevel.Doctorate: return "博士";
                default: return "不拘";
            }
        }

        private static string CompletionText(EducationCompletionStatus value)
        {
            switch (value)
            {
                case EducationCompletionStatus.Completed: return "（已完成）";
                case EducationCompletionStatus.InProgress: return "（在學）";
                case EducationCompletionStatus.Incomplete: return "（未完成）";
                default: return "（狀態未填）";
            }
        }

        private static string NormalizeText(string value)
        {
            return value.Trim().ToLowerInvariant();
        }

        private static string JoinNonEmpty(string separator, params string[] values)
        {
            return string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value)));
        }
    }
}
