using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace JobCheck.Domain
{
    /// <summary>只解析使用者明確輸入的門檻；不從任意職缺描述猜測條件。</summary>
    public static class RequirementInputParser
    {
        private static readonly Regex MonthsPattern = new Regex(
            @"^\s*(?:至少|滿)?\s*(\d+)\s*(年|個月|月)\s*(?:以上)?\s*$",
            RegexOptions.CultureInvariant);

        public static bool TryParseMinimumExperience(string text, out int months)
        {
            months = 0;
            if (string.Equals(text?.Trim(), "不拘", StringComparison.Ordinal)
                || string.Equals(text?.Trim(), "經驗不拘", StringComparison.Ordinal))
            {
                return true;
            }

            Match match = MonthsPattern.Match(text ?? string.Empty);
            if (!match.Success
                || !int.TryParse(match.Groups[1].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out int number)
                || number <= 0)
            {
                return false;
            }

            try
            {
                months = checked(number * (match.Groups[2].Value == "年" ? 12 : 1));
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        public static bool TryParseMinimumEducation(
            string text,
            out DegreeLevel level,
            out bool acceptsInProgress)
        {
            level = DegreeLevel.Unrestricted;
            acceptsInProgress = false;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string normalized = text.Trim().Replace(" ", string.Empty)
                .Replace("（", "(").Replace("）", ")");
            if (normalized.EndsWith("(在學可)", StringComparison.Ordinal))
            {
                acceptsInProgress = true;
                normalized = normalized.Substring(0, normalized.Length - 5);
            }

            if (normalized.EndsWith("以上", StringComparison.Ordinal))
                normalized = normalized.Substring(0, normalized.Length - 2);
            switch (normalized)
            {
                case "不拘":
                case "學歷不拘": level = DegreeLevel.Unrestricted; return true;
                case "高中":
                case "高中職": level = DegreeLevel.HighSchool; return true;
                case "專科": level = DegreeLevel.Associate; return true;
                case "大學":
                case "學士": level = DegreeLevel.Bachelor; return true;
                case "碩士": level = DegreeLevel.Master; return true;
                case "博士": level = DegreeLevel.Doctorate; return true;
                default: return false;
            }
        }

        public static bool TryParseDegree(string text, out DegreeLevel level)
        {
            level = DegreeLevel.Unrestricted;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (!TryParseMinimumEducation(text, out level, out bool acceptsInProgress))
                return false;
            return level != DegreeLevel.Unrestricted && !acceptsInProgress;
        }

        public static bool TryParseCompletion(
            string text,
            out EducationCompletionStatus status)
        {
            status = EducationCompletionStatus.Unknown;
            switch (text?.Trim())
            {
                case null:
                case "": return true;
                case "已畢業":
                case "已完成": return SetCompletion(
                    EducationCompletionStatus.Completed, out status);
                case "在學": return SetCompletion(
                    EducationCompletionStatus.InProgress, out status);
                case "未完成": return SetCompletion(
                    EducationCompletionStatus.Incomplete, out status);
                default: return false;
            }
        }

        public static bool TryParseSkillLevel(string text, out SkillLevel? level)
        {
            level = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (int.TryParse(text.Trim(), out int number)
                && number >= 1 && number <= 5)
            {
                level = (SkillLevel)number;
                return true;
            }

            return false;
        }

        public static bool TryParseLanguageLevel(
            string text,
            out LanguageProficiency? level)
        {
            level = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (int.TryParse(text.Trim(), out int number)
                && number >= 1 && number <= 6)
            {
                level = (LanguageProficiency)number;
                return true;
            }

            return false;
        }

        public static bool TryParseOptionalMonths(string text, out int? months)
        {
            months = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (!int.TryParse(text.Trim(), NumberStyles.None,
                CultureInfo.InvariantCulture, out int number) || number < 0)
                return false;
            months = number;
            return true;
        }

        private static bool SetCompletion(
            EducationCompletionStatus value,
            out EducationCompletionStatus status)
        {
            status = value;
            return true;
        }
    }
}
