using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JobCheck.Domain;
using JobCheck.Persistence;
using UnityEngine;
using UnityEngine.UI;

/// <summary>個人履歷母資料頁；負責載入資料並呈現，不介入職缺頁流程。</summary>
public sealed class CareerProfilePage : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private string personalDataRootPath = "../personal_data";

    [Header("Display")]
    [SerializeField] private Text displayText;

    public CareerProfile CurrentProfile { get; private set; }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        PersistenceStorageResult<CareerProfile> result =
            CareerProfileRepository.Load(ResolveProjectRelativePath(personalDataRootPath));
        if (!result.IsSuccess)
        {
            CurrentProfile = null;
            SetDisplay("個人履歷\n\n讀取失敗\n" + FormatIssues(result.Issues));
            return;
        }

        CurrentProfile = result.Value;
        SetDisplay(FormatProfile(CurrentProfile));
    }

    public static string FormatProfile(CareerProfile profile)
    {
        if (profile == null)
        {
            return "個人履歷\n\n尚未建立履歷資料。";
        }

        var builder = new StringBuilder();
        builder.AppendLine("個人履歷");
        builder.AppendLine(profile.UpdatedAt.HasValue
            ? "最後更新：" + profile.UpdatedAt.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
            : "最後更新：—");
        builder.AppendLine();
        AppendTextSection(builder, "自我介紹", profile.Summary, "尚未填寫自我介紹");
        AppendListSection(builder, "連結", profile.Links,
            item => Join("｜", item.Label, item.Url), "尚未新增連結");
        AppendListSection(builder, "技能", profile.Skills,
            item => Join("｜", item.Name, item.Notes), "尚未新增技能");
        AppendListSection(builder, "工作經歷", profile.Experiences,
            item => Join("｜",
                Join(" / ", item.Organization, item.Role),
                FormatPeriod(item.StartDate, item.EndDate, item.IsCurrent),
                item.Description),
            "尚未新增工作經歷");
        AppendListSection(builder, "專案經歷", profile.Projects,
            item => Join("｜",
                item.Name,
                item.Technologies == null ? null : string.Join("、", item.Technologies),
                item.Url,
                item.Description),
            "尚未新增專案經歷");
        AppendListSection(builder, "學歷", profile.Educations,
            item => Join("｜",
                Join(" / ", item.Institution, item.Program),
                FormatPeriod(item.StartDate, item.EndDate, false),
                item.Notes),
            "尚未新增學歷");
        AppendListSection(builder, "語言能力", profile.Languages,
            item => Join("｜", item.Name, item.Level, item.Notes), "尚未新增語言能力");
        return builder.ToString().TrimEnd();
    }

    private static void AppendTextSection(
        StringBuilder builder,
        string title,
        string value,
        string emptyText)
    {
        builder.AppendLine("【" + title + "】");
        builder.AppendLine(string.IsNullOrWhiteSpace(value) ? emptyText : value.Trim());
        builder.AppendLine();
    }

    private static void AppendListSection<T>(
        StringBuilder builder,
        string title,
        IEnumerable<T> source,
        Func<T, string> format,
        string emptyText)
        where T : class
    {
        builder.AppendLine("【" + title + "】");
        string[] rows = source == null
            ? Array.Empty<string>()
            : source.Where(item => item != null)
                .Select(format)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        if (rows.Length == 0)
        {
            builder.AppendLine(emptyText);
        }
        else
        {
            foreach (string row in rows)
            {
                builder.AppendLine("• " + row);
            }
        }

        builder.AppendLine();
    }

    private static string FormatPeriod(string start, string end, bool current)
    {
        string right = current ? "至今" : end;
        return Join(" ～ ", start, right);
    }

    private static string Join(string separator, params string[] values)
    {
        return string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string FormatIssues(IReadOnlyList<PersistenceStorageIssue> issues)
    {
        return issues == null || issues.Count == 0
            ? "未知錯誤"
            : string.Join("\n", issues.Select(item => item.Message));
    }

    private void SetDisplay(string value)
    {
        if (displayText != null)
        {
            displayText.text = value;
        }
    }

    private static string ResolveProjectRelativePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }
}
