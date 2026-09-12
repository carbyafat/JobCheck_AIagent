using System;
using System.Collections.Generic;

namespace JobCheck.Domain
{
    /// <summary>
    /// 一個可由 UI 選擇的職缺標籤或風險標記。
    /// Value 是寫入 JSON 的穩定值，DisplayName 是顯示給使用者看的中文名稱。
    /// </summary>
    public sealed class JobPostingLabelOption
    {
        public JobPostingLabelOption(string value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        /// <summary>
        /// 寫入 JobPosting 與 JSON 的小寫 snake_case 值。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Unity UI 使用的中文顯示名稱。
        /// </summary>
        public string DisplayName { get; }
    }

    /// <summary>
    /// V0.2 第一版允許使用者選擇的 Tag 與 RiskFlag 受控清單。
    /// 清單只限制 UI 新增的值；讀取舊資料時仍須保留未列在這裡的既有值。
    /// </summary>
    public static class JobPostingLabelCatalog
    {
        /// <summary>
        /// 用於分類與搜尋的 Unity 職缺標籤。
        /// </summary>
        public const string TagUnity = "unity";

        /// <summary>
        /// 用於分類與搜尋的 C# 職缺標籤。
        /// </summary>
        public const string TagCSharp = "csharp";

        /// <summary>
        /// 用於分類與搜尋的 .NET 職缺標籤。
        /// </summary>
        public const string TagDotNet = "dotnet";

        /// <summary>
        /// 用於分類與搜尋的 Web 職缺標籤。
        /// </summary>
        public const string TagWeb = "web";

        /// <summary>
        /// 表示職缺提供遠端工作形式的分類標籤。
        /// </summary>
        public const string TagRemote = "remote";

        /// <summary>
        /// 用於分類與搜尋的遊戲產業或遊戲開發標籤。
        /// </summary>
        public const string TagGame = "game";

        /// <summary>
        /// 用於分類與搜尋的教育相關職缺標籤。
        /// </summary>
        public const string TagEducation = "education";

        /// <summary>
        /// 表示職缺可能要求週末值班或週末工作的風險標記。
        /// </summary>
        public const string RiskWeekendDuty = "weekend_duty";

        /// <summary>
        /// 表示薪資範圍未公開或描述不明確的風險標記。
        /// </summary>
        public const string RiskSalaryOpaque = "salary_opaque";

        /// <summary>
        /// 表示工作地點可能造成較高通勤成本的風險標記。
        /// </summary>
        public const string RiskLongCommute = "long_commute";

        /// <summary>
        /// 表示實際工作角色或責任範圍描述不清楚的風險標記。
        /// </summary>
        public const string RiskRoleAmbiguous = "role_ambiguous";

        /// <summary>
        /// 表示公司或職缺涉及博弈產業，需要在決定投遞前提醒使用者。
        /// </summary>
        public const string RiskGamblingIndustry = "gambling_industry";

        private static readonly JobPostingLabelOption[] TagOptionValues =
        {
            new JobPostingLabelOption(TagUnity, "Unity"),
            new JobPostingLabelOption(TagCSharp, "C#"),
            new JobPostingLabelOption(TagDotNet, ".NET"),
            new JobPostingLabelOption(TagWeb, "Web"),
            new JobPostingLabelOption(TagRemote, "遠端工作"),
            new JobPostingLabelOption(TagGame, "遊戲"),
            new JobPostingLabelOption(TagEducation, "教育")
        };

        private static readonly JobPostingLabelOption[] RiskFlagOptionValues =
        {
            new JobPostingLabelOption(RiskWeekendDuty, "週末值班"),
            new JobPostingLabelOption(RiskSalaryOpaque, "薪資不透明"),
            new JobPostingLabelOption(RiskLongCommute, "通勤距離較長"),
            new JobPostingLabelOption(RiskRoleAmbiguous, "職務內容不明確"),
            new JobPostingLabelOption(RiskGamblingIndustry, "博弈產業")
        };

        /// <summary>
        /// UI 可主動加入的職缺分類標籤。
        /// </summary>
        public static IReadOnlyList<JobPostingLabelOption> TagOptions => TagOptionValues;

        /// <summary>
        /// UI 可主動加入的職缺風險標記。
        /// </summary>
        public static IReadOnlyList<JobPostingLabelOption> RiskFlagOptions => RiskFlagOptionValues;

        /// <summary>
        /// 將 Tag 的持久化值轉成中文顯示名稱；未知值原樣顯示，避免隱藏舊資料。
        /// </summary>
        public static string GetTagDisplayName(string value)
        {
            return GetDisplayName(TagOptionValues, value);
        }

        /// <summary>
        /// 將 RiskFlag 的持久化值轉成中文顯示名稱；未知值原樣顯示，避免隱藏舊資料。
        /// </summary>
        public static string GetRiskFlagDisplayName(string value)
        {
            return GetDisplayName(RiskFlagOptionValues, value);
        }

        /// <summary>
        /// 判斷指定值是否為 V0.2 UI 已知的 Tag。
        /// </summary>
        public static bool IsKnownTag(string value)
        {
            return ContainsValue(TagOptionValues, value);
        }

        /// <summary>
        /// 判斷指定值是否為 V0.2 UI 已知的 RiskFlag。
        /// </summary>
        public static bool IsKnownRiskFlag(string value)
        {
            return ContainsValue(RiskFlagOptionValues, value);
        }

        /// <summary>
        /// 將使用者輸入的 Tag 英文值或中文名稱解析成穩定持久化值。
        /// </summary>
        public static bool TryResolveTag(string input, out string value)
        {
            return TryResolve(TagOptionValues, input, out value);
        }

        /// <summary>
        /// 將使用者輸入的 RiskFlag 英文值或中文名稱解析成穩定持久化值。
        /// </summary>
        public static bool TryResolveRiskFlag(string input, out string value)
        {
            return TryResolve(RiskFlagOptionValues, input, out value);
        }

        private static string GetDisplayName(
            IReadOnlyList<JobPostingLabelOption> options,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            foreach (JobPostingLabelOption option in options)
            {
                if (string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
                {
                    return option.DisplayName;
                }
            }

            return value;
        }

        private static bool ContainsValue(
            IReadOnlyList<JobPostingLabelOption> options,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (JobPostingLabelOption option in options)
            {
                if (string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolve(
            IReadOnlyList<JobPostingLabelOption> options,
            string input,
            out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string candidate = input.Trim();
            foreach (JobPostingLabelOption option in options)
            {
                if (string.Equals(option.Value, candidate, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(option.DisplayName, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    value = option.Value;
                    return true;
                }
            }

            return false;
        }
    }
}
