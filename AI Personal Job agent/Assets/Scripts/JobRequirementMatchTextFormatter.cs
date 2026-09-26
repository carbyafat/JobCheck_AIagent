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

    public static string Format(
        RequirementMatchResult requirementMatch,
        RequirementScore requirementScore,
        JobPreferenceMatchResult preferenceMatch,
        JobPreferenceScore preferenceScore)
    {
        var builder = new StringBuilder();
        builder.AppendLine("【履歷是否符合職缺】");
        builder.Append(Format(requirementMatch, requirementScore));
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("【職缺是否符合求職條件】");
        AppendPreferenceResult(builder, preferenceMatch, preferenceScore);
        return builder.ToString();
    }

    /// <summary>顯示目前實際使用的權重、項目得分與總分算式。</summary>
    public static string FormatCalculation(
        RequirementMatchResult requirementMatch,
        RequirementScore requirementScore,
        JobPreferenceMatchResult preferenceMatch,
        JobPreferenceScore preferenceScore)
    {
        var builder = new StringBuilder();
        AppendRequirementCalculation(builder, requirementScore);
        builder.AppendLine();
        builder.AppendLine();
        AppendPreferenceCalculation(builder, preferenceMatch, preferenceScore);
        return builder.ToString();
    }

    private static void AppendRequirementCalculation(
        StringBuilder builder,
        RequirementScore score)
    {
        builder.AppendLine("【履歷是否符合職缺】");
        builder.AppendLine("類別權重：技能 40／年資 30／學歷 15／語言 15");
        builder.AppendLine("類別得分＝類別權重 × 符合數 ÷ 該類別可判斷數");
        builder.AppendLine("條件不明不進入分母；未證明與確認不符合皆為 0 分。");

        if (score == null || !score.Score.HasValue)
        {
            builder.Append("目前尚無可計算項目。");
            return;
        }

        RequirementCategoryScore[] categories = score.Categories
            .Where(item => item != null && item.AssessableCount > 0)
            .ToArray();
        foreach (RequirementCategoryScore category in categories)
        {
            builder.AppendLine();
            builder.Append(CategoryText(category.Category)).Append("：")
                .Append(category.ConfiguredWeight).Append(" × ")
                .Append(category.MatchCount).Append(" ÷ ")
                .Append(category.AssessableCount).Append(" = ")
                .Append(Number(category.EarnedWeight));
        }

        int availableWeight = categories.Sum(item => item.ConfiguredWeight);
        double earnedWeight = categories.Sum(item => item.EarnedWeight);
        builder.AppendLine();
        builder.Append("總分：")
            .Append(Number(earnedWeight)).Append(" ÷ ")
            .Append(availableWeight).Append(" × 100 = ")
            .Append(score.Score.Value).Append(" 分");
    }

    private static void AppendPreferenceCalculation(
        StringBuilder builder,
        JobPreferenceMatchResult match,
        JobPreferenceScore score)
    {
        builder.AppendLine("【職缺是否符合求職條件】");
        builder.AppendLine("條件權重：必要 ×2／偏好 ×1");
        builder.AppendLine("結果係數：符合 ×1／部分符合 ×0.5／不符合 ×0");
        builder.AppendLine("職缺未提供與待 AI 判斷項目不進入分母。");

        if (match == null || score == null || !score.Score.HasValue)
        {
            builder.Append("目前尚無可計算項目。");
            return;
        }

        JobPreferenceMatchItem[] assessable = match.Items.Where(item =>
            item != null
            && item.Importance != PreferenceImportance.Indifferent
            && (item.Status == JobPreferenceMatchStatus.Match
                || item.Status == JobPreferenceMatchStatus.PartialMatch
                || item.Status == JobPreferenceMatchStatus.ConfirmedMismatch))
            .ToArray();
        int availableWeight = 0;
        double earnedWeight = 0d;
        foreach (JobPreferenceMatchItem item in assessable)
        {
            int weight = item.Importance == PreferenceImportance.Required ? 2 : 1;
            double factor = PreferenceFactor(item.Status);
            double earned = weight * factor;
            availableWeight += weight;
            earnedWeight += earned;

            builder.AppendLine();
            builder.Append(PreferenceCategoryText(item.Category)).Append("－")
                .Append(string.IsNullOrWhiteSpace(item.Label) ? "未命名條件" : item.Label)
                .Append("（").Append(ImportanceText(item.Importance)).Append("）：")
                .Append(weight).Append(" × ").Append(Number(factor))
                .Append(" = ").Append(Number(earned));
        }

        builder.AppendLine();
        builder.Append("總分：")
            .Append(Number(earnedWeight)).Append(" ÷ ")
            .Append(availableWeight).Append(" × 100 = ")
            .Append(score.Score.Value).Append(" 分");
        builder.AppendLine();
        builder.Append("未計分：職缺未提供 ").Append(score.UnknownCount)
            .Append(" 項／待 AI 判斷 ").Append(score.AiContextCount).Append(" 項。");
        if (score.RequiredMismatches.Count > 0)
        {
            builder.AppendLine();
            builder.Append("必要條件不符合會另外警示；目前尚未套用分數上限。");
        }
    }

    private static double PreferenceFactor(JobPreferenceMatchStatus status)
    {
        switch (status)
        {
            case JobPreferenceMatchStatus.Match: return 1d;
            case JobPreferenceMatchStatus.PartialMatch: return 0.5d;
            case JobPreferenceMatchStatus.ConfirmedMismatch: return 0d;
            default: throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    private static string ImportanceText(PreferenceImportance importance)
    {
        switch (importance)
        {
            case PreferenceImportance.Required: return "必要";
            case PreferenceImportance.Preferred: return "偏好";
            case PreferenceImportance.Indifferent: return "不在意";
            default: throw new ArgumentOutOfRangeException(nameof(importance));
        }
    }

    private static string Number(double value)
    {
        return value.ToString("0.##");
    }

    private static void AppendPreferenceResult(
        StringBuilder builder,
        JobPreferenceMatchResult match,
        JobPreferenceScore score)
    {
        if (match == null || score == null)
        {
            builder.Append("求職條件比對目前無法使用。");
            return;
        }

        if (score.RequiredMismatches.Count > 0)
        {
            builder.AppendLine("結論：有必要條件不符合");
        }
        else if (score.Score.HasValue)
        {
            builder.AppendLine("結論：未發現必要條件衝突");
        }
        else
        {
            builder.AppendLine("結論：尚無足夠資料判斷");
        }

        builder.Append(score.Score.HasValue
            ? "求職適合度：" + score.Score.Value + " 分"
            : "求職適合度：尚無可判斷條件");
        builder.Append("｜符合 ").Append(score.MatchCount)
            .Append("｜部分符合 ").Append(score.PartialMatchCount)
            .Append("｜不符合 ").Append(score.ConfirmedMismatchCount)
            .Append("｜職缺未提供 ").Append(score.UnknownCount);

        foreach (JobPreferenceMatchItem item in match.Items.Where(item =>
            item != null && item.Status != JobPreferenceMatchStatus.AiContextOnly))
        {
            AppendPreferenceItem(builder, item);
        }

        JobPreferenceMatchItem[] aiContexts = match.Items.Where(item =>
            item != null && item.Status == JobPreferenceMatchStatus.AiContextOnly).ToArray();
        if (aiContexts.Length == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine();
        builder.Append("【保留給後續 AI 判斷】");
        foreach (JobPreferenceMatchItem item in aiContexts)
        {
            AppendPreferenceItem(builder, item);
        }
    }

    private static void AppendPreferenceItem(
        StringBuilder builder,
        JobPreferenceMatchItem item)
    {
        builder.AppendLine();
        builder.Append(PreferenceStatusText(item.Status)).Append(" ")
            .Append(PreferenceCategoryText(item.Category)).Append("－")
            .Append(string.IsNullOrWhiteSpace(item.Label) ? "未命名條件" : item.Label);
        if (!string.IsNullOrWhiteSpace(item.PreferredValue))
        {
            builder.Append("｜希望：").Append(item.PreferredValue);
        }
        if (!string.IsNullOrWhiteSpace(item.JobValue))
        {
            builder.Append("｜職缺：").Append(item.JobValue);
        }
        if (!string.IsNullOrWhiteSpace(item.Explanation))
        {
            builder.Append("｜").Append(item.Explanation);
        }
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

    private static string PreferenceStatusText(JobPreferenceMatchStatus status)
    {
        switch (status)
        {
            case JobPreferenceMatchStatus.Match: return "[符合]";
            case JobPreferenceMatchStatus.PartialMatch: return "[部分符合]";
            case JobPreferenceMatchStatus.ConfirmedMismatch: return "[不符合]";
            case JobPreferenceMatchStatus.JobDataUnknown: return "[職缺未提供]";
            case JobPreferenceMatchStatus.AiContextOnly: return "[待 AI]";
            default: throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    private static string PreferenceCategoryText(JobPreferenceCategory category)
    {
        switch (category)
        {
            case JobPreferenceCategory.TargetRole: return "職務";
            case JobPreferenceCategory.Industry: return "產業";
            case JobPreferenceCategory.Salary: return "薪資";
            case JobPreferenceCategory.Region: return "地區";
            case JobPreferenceCategory.EmploymentType: return "聘僱形式";
            case JobPreferenceCategory.WorkMode: return "工作模式";
            case JobPreferenceCategory.WorkSchedule: return "上班時段";
            case JobPreferenceCategory.Commute: return "通勤";
            case JobPreferenceCategory.Overtime: return "加班";
            case JobPreferenceCategory.Travel: return "出差／外派";
            case JobPreferenceCategory.Relocation: return "搬遷";
            default: throw new ArgumentOutOfRangeException(nameof(category));
        }
    }
}
