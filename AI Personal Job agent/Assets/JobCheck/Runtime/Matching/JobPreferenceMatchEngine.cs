using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JobCheck.Domain
{
    /// <summary>不呼叫 AI、不寫回資料的確定性求職條件比對器。</summary>
    public static class JobPreferenceMatchEngine
    {
        public static JobPreferenceMatchResult Compare(
            JobSearchPreferences preferences,
            Company company,
            JobPosting jobPosting)
        {
            var result = new JobPreferenceMatchResult();
            if (preferences == null || jobPosting == null)
            {
                return result;
            }

            EvaluateTextList(result.Items, JobPreferenceCategory.TargetRole,
                "目標職務", preferences.TargetRoles, jobPosting.Title,
                preferences.TargetImportance, ValuesEquivalent);
            EvaluateTextList(result.Items, JobPreferenceCategory.Industry,
                "產業類別", preferences.Industries, company?.Industry,
                preferences.TargetImportance, ValuesEquivalent);
            EvaluateSalary(result.Items, preferences, jobPosting.Compensation);
            EvaluateTextList(result.Items, JobPreferenceCategory.Region,
                "工作地區", preferences.AcceptedRegions, jobPosting.Location?.City,
                preferences.ArrangementImportance, ValuesEquivalent);
            EvaluateTextList(result.Items, JobPreferenceCategory.EmploymentType,
                "聘僱形式", preferences.EmploymentTypes,
                jobPosting.WorkConditions?.EmploymentType,
                preferences.ArrangementImportance, EmploymentTypesEquivalent);
            EvaluateTextList(result.Items, JobPreferenceCategory.WorkMode,
                "工作模式", preferences.WorkModes, ResolveWorkMode(jobPosting.Location),
                preferences.ArrangementImportance, WorkModesEquivalent);
            EvaluateSchedule(result.Items, preferences, jobPosting.WorkConditions?.WorkingHours);
            AddAiContexts(result.Items, preferences);
            return result;
        }

        private static void EvaluateSalary(
            ICollection<JobPreferenceMatchItem> output,
            JobSearchPreferences preferences,
            JobCompensation compensation)
        {
            if (preferences.SalaryImportance == PreferenceImportance.Indifferent
                || (!preferences.MinimumSalary.HasValue
                    && !preferences.DesiredSalary.HasValue))
            {
                return;
            }

            string preferred = FormatPreferredSalary(preferences);
            if (compensation == null
                || string.Equals(compensation.Type, "negotiable",
                    StringComparison.OrdinalIgnoreCase)
                || (!compensation.Minimum.HasValue && !compensation.Maximum.HasValue))
            {
                output.Add(Item(JobPreferenceCategory.Salary,
                    JobPreferenceMatchStatus.JobDataUnknown,
                    preferences.SalaryImportance, "薪資", preferred,
                    FormatJobSalary(compensation), "職缺沒有可比較的明確薪資數值。"));
                return;
            }

            string expectedPeriod = SalaryPeriodCode(preferences.SalaryPeriod);
            if (!string.Equals(NormalizeCode(compensation.Period), expectedPeriod,
                    StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(compensation.Currency)
                    && !string.Equals(compensation.Currency.Trim(), "TWD",
                        StringComparison.OrdinalIgnoreCase)))
            {
                output.Add(Item(JobPreferenceCategory.Salary,
                    JobPreferenceMatchStatus.JobDataUnknown,
                    preferences.SalaryImportance, "薪資", preferred,
                    FormatJobSalary(compensation), "薪資週期或幣別不同，本版不自行換算。"));
                return;
            }

            int? jobMinimum = compensation.Minimum;
            int? jobMaximum = compensation.Maximum;
            if (!jobMaximum.HasValue
                && string.Equals(NormalizeCode(compensation.Type), "fixed",
                    StringComparison.Ordinal))
            {
                jobMaximum = jobMinimum;
            }

            JobPreferenceMatchStatus status;
            string explanation;
            if (preferences.MinimumSalary.HasValue && jobMaximum.HasValue
                && jobMaximum.Value < preferences.MinimumSalary.Value)
            {
                status = JobPreferenceMatchStatus.ConfirmedMismatch;
                explanation = "職缺公開薪資上限低於最低可接受薪資。";
            }
            else if (preferences.DesiredSalary.HasValue && jobMinimum.HasValue
                && jobMinimum.Value >= preferences.DesiredSalary.Value)
            {
                status = JobPreferenceMatchStatus.Match;
                explanation = "職缺薪資下限已達期望薪資。";
            }
            else if (preferences.DesiredSalary.HasValue && jobMaximum.HasValue
                && jobMaximum.Value >= preferences.DesiredSalary.Value)
            {
                status = JobPreferenceMatchStatus.PartialMatch;
                explanation = "職缺薪資區間涵蓋期望薪資，但實際待遇尚未確定。";
            }
            else if (preferences.MinimumSalary.HasValue && jobMinimum.HasValue
                && jobMinimum.Value >= preferences.MinimumSalary.Value)
            {
                status = preferences.DesiredSalary.HasValue
                    ? JobPreferenceMatchStatus.PartialMatch
                    : JobPreferenceMatchStatus.Match;
                explanation = preferences.DesiredSalary.HasValue
                    ? "職缺達到最低底線，但尚未達期望薪資。"
                    : "職缺薪資已達最低可接受薪資。";
            }
            else if (preferences.MinimumSalary.HasValue && jobMaximum.HasValue
                && jobMaximum.Value >= preferences.MinimumSalary.Value)
            {
                status = JobPreferenceMatchStatus.PartialMatch;
                explanation = "職缺薪資區間涵蓋最低底線，但實際待遇尚未確定。";
            }
            else if (!preferences.MinimumSalary.HasValue
                && preferences.DesiredSalary.HasValue && jobMaximum.HasValue
                && jobMaximum.Value < preferences.DesiredSalary.Value)
            {
                status = JobPreferenceMatchStatus.PartialMatch;
                explanation = "職缺公開薪資低於期望值，但未設定不可接受的最低底線。";
            }
            else
            {
                status = JobPreferenceMatchStatus.JobDataUnknown;
                explanation = "公開數值不足以確認是否達到薪資底線。";
            }

            output.Add(Item(JobPreferenceCategory.Salary, status,
                preferences.SalaryImportance, "薪資", preferred,
                FormatJobSalary(compensation), explanation));
        }

        private static void EvaluateSchedule(
            ICollection<JobPreferenceMatchItem> output,
            JobSearchPreferences preferences,
            string actual)
        {
            List<string> expected = Clean(preferences.WorkSchedules);
            if (preferences.ScheduleImportance == PreferenceImportance.Indifferent
                || expected.Count == 0)
            {
                return;
            }

            if (expected.Any(value => ValuesEquivalent(value, "時段不限")))
            {
                output.Add(Item(JobPreferenceCategory.WorkSchedule,
                    JobPreferenceMatchStatus.Match, preferences.ScheduleImportance,
                    "上班時段", Join(expected), Display(actual),
                    "已設定為時段不限。"));
                return;
            }

            EvaluateTextList(output, JobPreferenceCategory.WorkSchedule,
                "上班時段", expected, actual, preferences.ScheduleImportance,
                SchedulesEquivalent);
        }

        private static void EvaluateTextList(
            ICollection<JobPreferenceMatchItem> output,
            JobPreferenceCategory category,
            string label,
            IEnumerable<string> expectedValues,
            string actual,
            PreferenceImportance importance,
            Func<string, string, bool> comparer)
        {
            List<string> expected = Clean(expectedValues);
            if (importance == PreferenceImportance.Indifferent || expected.Count == 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(actual))
            {
                output.Add(Item(category, JobPreferenceMatchStatus.JobDataUnknown,
                    importance, label, Join(expected), "職缺未提供",
                    "職缺資料不足，不能視為不符合。"));
                return;
            }

            bool matches = expected.Any(value => comparer(value, actual));
            output.Add(Item(category,
                matches ? JobPreferenceMatchStatus.Match
                    : JobPreferenceMatchStatus.ConfirmedMismatch,
                importance, label, Join(expected), actual.Trim(),
                matches ? "職缺資料符合已設定條件。" : "職缺資料未符合已設定條件。"));
        }

        private static void AddAiContexts(
            ICollection<JobPreferenceMatchItem> output,
            JobSearchPreferences preferences)
        {
            if (preferences.MaximumCommuteMinutes.HasValue)
            {
                output.Add(Item(JobPreferenceCategory.Commute,
                    JobPreferenceMatchStatus.AiContextOnly,
                    PreferenceImportance.Indifferent, "通勤時間",
                    "最多 " + preferences.MaximumCommuteMinutes.Value + " 分鐘",
                    "需由地址與交通方式估算", "本版不以地址猜測通勤時間。"));
            }

            AddAiContext(output, JobPreferenceCategory.Overtime, "加班偏好",
                preferences.OvertimePreference);
            AddAiContext(output, JobPreferenceCategory.Travel, "出差／外派偏好",
                preferences.TravelPreference);
            AddAiContext(output, JobPreferenceCategory.Relocation, "搬遷偏好",
                preferences.RelocationPreference);
        }

        private static void AddAiContext(
            ICollection<JobPreferenceMatchItem> output,
            JobPreferenceCategory category,
            string label,
            string preferredValue)
        {
            if (string.IsNullOrWhiteSpace(preferredValue))
            {
                return;
            }

            output.Add(Item(category, JobPreferenceMatchStatus.AiContextOnly,
                PreferenceImportance.Indifferent, label, preferredValue.Trim(),
                "待分析職缺原文", "保留作為後續 AI 判斷上下文，不進入規則分數。"));
        }

        private static JobPreferenceMatchItem Item(
            JobPreferenceCategory category,
            JobPreferenceMatchStatus status,
            PreferenceImportance importance,
            string label,
            string preferredValue,
            string jobValue,
            string explanation)
        {
            return new JobPreferenceMatchItem
            {
                Category = category,
                Status = status,
                Importance = importance,
                Label = label,
                PreferredValue = preferredValue,
                JobValue = jobValue,
                Explanation = explanation
            };
        }

        private static bool ValuesEquivalent(string expected, string actual)
        {
            string left = NormalizeText(expected);
            string right = NormalizeText(actual);
            if (left.Length == 0 || right.Length == 0) return false;
            if (string.Equals(left, right, StringComparison.Ordinal)) return true;
            return Math.Min(left.Length, right.Length) >= 2
                && (left.Contains(right) || right.Contains(left));
        }

        private static bool EmploymentTypesEquivalent(string expected, string actual) =>
            string.Equals(CanonicalEmploymentType(expected), CanonicalEmploymentType(actual),
                StringComparison.Ordinal);

        private static bool WorkModesEquivalent(string expected, string actual) =>
            string.Equals(CanonicalWorkMode(expected), CanonicalWorkMode(actual),
                StringComparison.Ordinal);

        private static bool SchedulesEquivalent(string expected, string actual)
        {
            string left = CanonicalSchedule(expected);
            string normalizedActual = NormalizeText(actual);
            return ScheduleAliases(left).Any(alias => normalizedActual.Contains(alias));
        }

        private static string CanonicalEmploymentType(string value)
        {
            string normalized = NormalizeText(value);
            if (normalized.Contains("正職") || normalized.Contains("全職")
                || normalized.Contains("fulltime")) return "fulltime";
            if (normalized.Contains("兼職") || normalized.Contains("parttime")) return "parttime";
            if (normalized.Contains("約聘") || normalized.Contains("契約")
                || normalized.Contains("contract")) return "contract";
            if (normalized.Contains("接案") || normalized.Contains("freelance")) return "freelance";
            return normalized;
        }

        private static string CanonicalWorkMode(string value)
        {
            string normalized = NormalizeText(value);
            if (normalized.Contains("遠端") || normalized.Contains("remote")) return "remote";
            if (normalized.Contains("混合") || normalized.Contains("hybrid")) return "hybrid";
            if (normalized.Contains("現場") || normalized.Contains("onsite")
                || normalized.Contains("辦公室")) return "onsite";
            return normalized;
        }

        private static string CanonicalSchedule(string value)
        {
            string normalized = NormalizeText(value);
            if (normalized.Contains("大夜")) return "overnight";
            if (normalized.Contains("晚班")) return "evening";
            if (normalized.Contains("輪班")) return "shift";
            if (normalized.Contains("假日") || normalized.Contains("週末")) return "weekend";
            if (normalized.Contains("彈性")) return "flexible";
            if (normalized.Contains("日班") || normalized.Contains("白班")) return "day";
            return normalized;
        }

        private static IEnumerable<string> ScheduleAliases(string canonical)
        {
            switch (canonical)
            {
                case "day": return new[] { "一般日班", "日班", "白班" };
                case "evening": return new[] { "晚班", "小夜" };
                case "overnight": return new[] { "大夜", "夜班" };
                case "shift": return new[] { "輪班", "排班" };
                case "weekend": return new[] { "假日", "週末" };
                case "flexible": return new[] { "彈性", "彈性工時" };
                default: return new[] { canonical };
            }
        }

        private static List<string> Clean(IEnumerable<string> values) =>
            (values ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string normalized = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
            return new string(normalized.Where(char.IsLetterOrDigit).ToArray());
        }

        private static string NormalizeCode(string value) =>
            (value ?? string.Empty).Trim().ToLowerInvariant();

        private static string ResolveWorkMode(JobLocation location)
        {
            if (location == null) return null;
            if (!string.IsNullOrWhiteSpace(location.WorkMode)) return location.WorkMode;
            return location.RemoteAllowed == true ? "remote" : null;
        }

        private static string SalaryPeriodCode(SalaryPeriod period)
        {
            switch (period)
            {
                case SalaryPeriod.Monthly: return "monthly";
                case SalaryPeriod.Annual: return "yearly";
                case SalaryPeriod.Hourly: return "hourly";
                default: throw new ArgumentOutOfRangeException(nameof(period));
            }
        }

        private static string FormatPreferredSalary(JobSearchPreferences preferences)
        {
            string suffix = SalaryPeriodSuffix(preferences.SalaryPeriod);
            if (preferences.MinimumSalary.HasValue && preferences.DesiredSalary.HasValue)
            {
                return "最低 " + preferences.MinimumSalary.Value.ToString("N0")
                    + "；期望 " + preferences.DesiredSalary.Value.ToString("N0") + suffix;
            }
            if (preferences.MinimumSalary.HasValue)
            {
                return "最低 " + preferences.MinimumSalary.Value.ToString("N0") + suffix;
            }
            return "期望 " + preferences.DesiredSalary.Value.ToString("N0") + suffix;
        }

        private static string FormatJobSalary(JobCompensation value)
        {
            if (value == null) return "職缺未提供";
            if (!string.IsNullOrWhiteSpace(value.RawText)) return value.RawText.Trim();
            if (string.Equals(value.Type, "negotiable", StringComparison.OrdinalIgnoreCase))
                return "待遇面議";
            string range = value.Minimum.HasValue ? value.Minimum.Value.ToString("N0") : "?";
            if (value.Maximum.HasValue) range += "–" + value.Maximum.Value.ToString("N0");
            return range + "／" + (string.IsNullOrWhiteSpace(value.Period)
                ? "週期未知" : value.Period.Trim());
        }

        private static string SalaryPeriodSuffix(SalaryPeriod period)
        {
            switch (period)
            {
                case SalaryPeriod.Monthly: return "／月";
                case SalaryPeriod.Annual: return "／年";
                case SalaryPeriod.Hourly: return "／時";
                default: throw new ArgumentOutOfRangeException(nameof(period));
            }
        }

        private static string Join(IEnumerable<string> values) => string.Join("、", values);
        private static string Display(string value) =>
            string.IsNullOrWhiteSpace(value) ? "職缺未提供" : value.Trim();
    }
}
