using System;
using System.Text;
using JobCheck.Persistence;
using CandidateCloseReason = JobCheck.Domain.CandidateCloseReason;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>V0.2.2 唯讀應徵分析頁。資料只由目前選取的資料區載入。</summary>
public sealed class Panel_Analytics : MonoBehaviour
{
    [SerializeField] private TMP_Text textProfile;
    [SerializeField] private TMP_Text textReport;
    [SerializeField] private Button buttonClose;
    [SerializeField] private ScrollRect scrollRect;

    private void Awake()
    {
        if (buttonClose != null)
        {
            buttonClose.onClick.RemoveListener(Close);
            buttonClose.onClick.AddListener(Close);
        }
    }

    public void Open(string dataRoot, string profileName)
    {
        gameObject.SetActive(true);
        if (textProfile != null) textProfile.text = "資料區：" + profileName;
        if (textReport != null)
        {
            PersistenceStorageResult<ApplicationAnalyticsReport> result =
                ApplicationAnalyticsQuery.Load(dataRoot);
            textReport.text = result.IsSuccess
                ? FormatReport(result.Value)
                : "無法讀取分析資料。請先確認資料區是否可讀取。";
        }
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public static string FormatReport(ApplicationAnalyticsReport report)
    {
        if (report == null) return "目前沒有可顯示的分析資料。";
        var builder = new StringBuilder(2048);
        AnalyticsSummary summary = report.Summary;
        builder.AppendLine("<b>應徵概況</b>");
        builder.AppendLine("收錄職缺：" + summary.JobPostingCount);
        builder.AppendLine("納入統計的本人應徵輪次：" + summary.IncludedApplicationCount);
        builder.AppendLine("進行中：" + summary.ActiveApplicationCount + "　已標記長期未回覆：" + summary.NoResponseMarkedCount);
        if (summary.JobPostingCount == 0 && summary.IncludedApplicationCount == 0)
            builder.AppendLine("目前沒有職缺或應徵資料；新增職缺並記錄投遞後才會出現分析。比率不適用（—）。");
        else if (summary.IncludedApplicationCount == 0)
            builder.AppendLine("目前沒有可納入的本人應徵輪次；下列比率會顯示「—」。");

        builder.AppendLine("\n<b>應徵漏斗</b>（各階段是曾經到達的輪次，不要求互斥）");
        foreach (FunnelMetric metric in report.Funnel)
            builder.AppendLine(FunnelName(metric.Stage) + "：" + metric.Count + "　相對投遞 " + Rate(metric.RateFromApplied));

        builder.AppendLine("\n<b>平台成效</b>（回應／面試／Offer 均以該平台投遞輪次為分母）");
        if (report.Platforms.Count == 0) builder.AppendLine("尚無平台資料。");
        foreach (PlatformPerformanceMetric metric in report.Platforms)
        {
            builder.AppendLine("<b>" + Safe(metric.Platform) + "</b>　職缺 " + metric.JobPostingCount
                + "／已投遞職缺 " + metric.AppliedJobPostingCount
                + "／投遞輪次 " + metric.AppliedApplicationCount);
            builder.AppendLine("　職缺投遞率 " + Rate(metric.JobPostingApplicationRate)
                + "　回應率 " + Rate(metric.ResponseRate)
                + "　面試率 " + Rate(metric.InterviewRate)
                + "　Offer 率 " + Rate(metric.OfferRate)
                + (metric.HasSmallApplicationSample ? "　<color=#9B6500>樣本少於 5 輪，僅供參考</color>" : ""));
        }

        builder.AppendLine("\n<b>目前結果</b>（每輪只歸入一類）");
        foreach (OutcomeMetric metric in report.Outcomes)
            builder.AppendLine(OutcomeName(metric.Outcome) + "：" + metric.Count);
        builder.AppendLine("\n<b>我方結束原因</b>");
        if (report.CloseReasons.Count == 0) builder.AppendLine("目前沒有我方結束原因。");
        foreach (CloseReasonMetric metric in report.CloseReasons)
            builder.AppendLine(CloseReasonName(metric.Reason) + "：" + metric.Count);

        builder.AppendLine("\n<b>資料品質提醒</b>");
        if (report.Warnings.Count == 0) builder.AppendLine("沒有需要提醒的排除資料。");
        foreach (AnalyticsDataWarning warning in report.Warnings)
            builder.AppendLine(WarningName(warning.Code) + "：" + warning.Count);
        builder.AppendLine("\n統計僅供整理求職進度；資料不足時請勿用比率判斷平台優劣。");
        return builder.ToString();
    }

    private static string Rate(double? value) => value.HasValue ? (value.Value * 100).ToString("0.#") + "%" : "—";
    private static string Safe(string value) => (value ?? "未填平台").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    private static string FunnelName(AnalyticsFunnelStage stage)
    {
        switch (stage)
        {
            case AnalyticsFunnelStage.Applied: return "已投遞";
            case AnalyticsFunnelStage.CompanyResponded: return "公司有回應";
            case AnalyticsFunnelStage.Interview: return "面試";
            case AnalyticsFunnelStage.WaitingResponse: return "等待回覆";
            case AnalyticsFunnelStage.OfferReceived: return "取得 Offer";
            default: return stage.ToString();
        }
    }

    private static string OutcomeName(AnalyticsOutcome outcome)
    {
        switch (outcome)
        {
            case AnalyticsOutcome.Active: return "進行中";
            case AnalyticsOutcome.OfferReceived: return "取得 Offer";
            case AnalyticsOutcome.RejectedByCompany: return "公司拒絕";
            case AnalyticsOutcome.ClosedByCandidate: return "我方結束";
            default: return outcome.ToString();
        }
    }

    private static string CloseReasonName(CandidateCloseReason reason)
    {
        switch (reason)
        {
            case CandidateCloseReason.SalaryTooLow: return "薪資太低";
            case CandidateCloseReason.GamblingIndustry: return "博弈產業";
            case CandidateCloseReason.Commute: return "通勤不符";
            case CandidateCloseReason.WorkSchedule: return "工時不符";
            case CandidateCloseReason.WeekendDuty: return "週末值班";
            case CandidateCloseReason.RoleMismatch: return "職務不符";
            case CandidateCloseReason.TechMismatch: return "技術方向不符";
            case CandidateCloseReason.CompanyConcern: return "公司疑慮";
            case CandidateCloseReason.BetterOpportunity: return "選擇其他機會";
            case CandidateCloseReason.NoResponse: return "長期無回覆";
            case CandidateCloseReason.Other: return "其他";
            default: return reason.ToString();
        }
    }

    private static string WarningName(AnalyticsDataWarningCode code)
    {
        switch (code)
        {
            case AnalyticsDataWarningCode.NonPersonalApplicationExcluded: return "非本人應徵已排除";
            case AnalyticsDataWarningCode.DuplicateApplicationExcluded: return "重複輪次已排除";
            case AnalyticsDataWarningCode.NeedsReviewApplicationExcluded: return "待人工確認已排除";
            case AnalyticsDataWarningCode.UnknownStageApplicationExcluded: return "未知階段已排除";
            case AnalyticsDataWarningCode.MissingJobPostingExcluded: return "找不到對應職缺已排除";
            case AnalyticsDataWarningCode.MigrationSnapshotOnly: return "僅有遷移快照，漏斗不推測歷史";
            default: return code.ToString();
        }
    }
}
