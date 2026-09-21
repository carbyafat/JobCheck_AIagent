using System;
using System.Linq;
using System.Text;
using JobCheck.Domain;

/// <summary>將 V0.2.7 規則比對結果轉成職缺詳細頁可讀文字。</summary>
public static class JobRequirementMatchTextFormatter
{
    public static string Format(RequirementMatchResult match, RequirementScore score)
    {
        if (match == null || score == null)
        {
            return "履歷比對目前無法使用。";
        }

        var builder = new StringBuilder();
        builder.AppendLine(score.Score.HasValue
            ? "規則符合度：" + score.Score.Value + " 分"
            : "規則符合度：尚無可判斷條件");
        builder.Append("符合 ").Append(score.MatchCount).Append(" / ")
            .Append(score.AssessableCount)
            .Append("｜未證明 ").Append(score.NotEvidencedCount)
            .Append("｜不符合 ").Append(score.ConfirmedMismatchCount)
            .Append("｜條件不明 ").Append(score.UnclearCount);

        foreach (RequirementMatchItem item in match.Items.Where(item => item != null))
        {
            builder.AppendLine();
            builder.Append(StatusText(item.Status)).Append(" ")
                .Append(CategoryText(item.Category)).Append("－")
                .Append(string.IsNullOrWhiteSpace(item.Label) ? "未命名條件" : item.Label);
            if (!string.IsNullOrWhiteSpace(item.RequiredValue))
            {
                builder.Append("｜需求：").Append(item.RequiredValue);
            }

            if (!string.IsNullOrWhiteSpace(item.ActualValue))
            {
                builder.Append("｜履歷：").Append(item.ActualValue);
            }

            if (!string.IsNullOrWhiteSpace(item.Explanation))
            {
                builder.Append("｜").Append(item.Explanation);
            }
        }

        return builder.ToString();
    }

    private static string StatusText(RequirementMatchStatus status)
    {
        switch (status)
        {
            case RequirementMatchStatus.Match: return "[符合]";
            case RequirementMatchStatus.ConfirmedMismatch: return "[不符合]";
            case RequirementMatchStatus.NotEvidenced: return "[未證明]";
            case RequirementMatchStatus.RequirementUnclear: return "[條件不明]";
            default: throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    private static string CategoryText(RequirementCategory category)
    {
        switch (category)
        {
            case RequirementCategory.Skill: return "技能";
            case RequirementCategory.Experience: return "年資";
            case RequirementCategory.Education: return "學歷";
            case RequirementCategory.Language: return "語言";
            default: throw new ArgumentOutOfRangeException(nameof(category));
        }
    }
}
