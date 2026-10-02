using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JobCheck.Domain;
using JobCheck.Persistence;
using SimpleFileBrowser;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 單筆職缺的離線 AI 輔助面板：預覽送出範圍、匯出 request、驗證 result 並另存分析。
/// 不呼叫外部 API，也不修改履歷、職缺、求職條件或應徵資料。
/// </summary>
public sealed class Panel_AiJobFitAssist : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] private JobCheckUiTheme theme;

    [Header("Content")]
    [SerializeField] private TMP_Text textDataPreview;
    [SerializeField] private TMP_Text textStatus;
    [SerializeField] private TMP_InputField inputResultJson;
    [SerializeField] private TMP_Text textResultPreview;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Actions")]
    [SerializeField] private Button buttonExportRequest;
    [SerializeField] private Button buttonCopyInstructions;
    [SerializeField] private Button buttonChooseResult;
    [SerializeField] private Button buttonValidateResult;
    [SerializeField] private Button buttonSaveResult;
    [SerializeField] private Button buttonClose;

    private string activeDataRoot;
    private string personalDataRoot;
    private bool personalProfileActive;
    private string currentJobId;
    private RequirementMatchResult requirementMatch;
    private RequirementScore requirementScore;
    private JobPreferenceMatchResult preferenceMatch;
    private JobPreferenceScore preferenceScore;
    private AiJobFitRequestPackageDto currentRequest;
    private AiJobFitResultPackageDto validatedResult;

    private void Awake()
    {
        Bind(buttonExportRequest, ExportRequest);
        Bind(buttonCopyInstructions, CopyInstructions);
        Bind(buttonChooseResult, ChooseResultFile);
        Bind(buttonValidateResult, ValidatePastedResult);
        Bind(buttonSaveResult, SaveValidatedResult);
        Bind(buttonClose, Close);
        if (inputResultJson != null)
        {
            inputResultJson.characterLimit = AiJobFitResultValidator.MaximumJsonCharacters;
            inputResultJson.onValueChanged.RemoveListener(OnResultTextChanged);
            inputResultJson.onValueChanged.AddListener(OnResultTextChanged);
        }
        ApplyTheme();
    }

    public void Open(
        string dataRoot,
        string profileRoot,
        bool isPersonalProfile,
        string jobId,
        RequirementMatchResult currentRequirementMatch,
        RequirementScore currentRequirementScore,
        JobPreferenceMatchResult currentPreferenceMatch,
        JobPreferenceScore currentPreferenceScore)
    {
        activeDataRoot = dataRoot;
        personalDataRoot = profileRoot;
        personalProfileActive = isPersonalProfile;
        currentJobId = jobId;
        requirementMatch = currentRequirementMatch;
        requirementScore = currentRequirementScore;
        preferenceMatch = currentPreferenceMatch;
        preferenceScore = currentPreferenceScore;
        validatedResult = null;
        if (inputResultJson != null) inputResultJson.text = string.Empty;
        if (buttonSaveResult != null) buttonSaveResult.interactable = false;
        if (textResultPreview != null) textResultPreview.text = "尚未匯入 AI 分析結果。";
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ApplyTheme();
        BuildRequestPreview();
        LoadLatestSavedResult();
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void Close()
    {
        validatedResult = null;
        gameObject.SetActive(false);
    }

    public void ExportRequest()
    {
        if (!EnsureRequestReady()) return;
        try
        {
            string directory = GetExportDirectory();
            Directory.CreateDirectory(directory);
            string destination = Path.Combine(
                directory,
                "JobCheck-AI-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-"
                + currentRequest.request_id.Substring(0, 8)
                + AiExchangeContract.RequestFileExtension);
            PersistenceStorageResult<AiJobFitRequestExportSummary> exported =
                AiJobFitExchangeFileService.ExportRequest(currentRequest, destination);
            if (!exported.IsSuccess)
            {
                SetStatus("匯出失敗：" + FirstIssue(exported.Issues), true);
                return;
            }

            string registrationMessage = string.Empty;
            if (personalProfileActive)
            {
                PersistenceStorageResult<AiJobFitRequestPackageDto> registered =
                    AiJobFitAnalysisRepository.RegisterRequest(
                        personalDataRoot,
                        currentRequest);
                if (!registered.IsSuccess)
                {
                    registrationMessage = "\n需求檔已匯出，但本機登記失敗；重新開啟程式後將無法保存這次回傳："
                        + FirstIssue(registered.Issues);
                }
            }

            GUIUtility.systemCopyBuffer = BuildUsageInstructions();
            SetStatus(
                "AI 分析需求已匯出：\n" + exported.Value.Path
                + "\n使用說明已複製。請把 request JSON 上傳給 AI，將回傳內容存成 "
                + AiExchangeContract.ResultFileExtension + "，或直接貼到下方。"
                + (personalProfileActive
                    ? registrationMessage
                    : "\n目前是 Demo 資料區：可以預覽回傳，但不能保存 AI 分析。"),
                !string.IsNullOrEmpty(registrationMessage));
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException || exception is NotSupportedException)
        {
            SetStatus("無法建立 AI 匯出資料夾：" + exception.Message, true);
        }
    }

    public void CopyInstructions()
    {
        if (!EnsureRequestReady()) return;
        GUIUtility.systemCopyBuffer = BuildUsageInstructions();
        SetStatus("使用說明已複製。先匯出 request JSON，再將檔案與說明交給 AI。", false);
    }

    public void ChooseResultFile()
    {
        if (FileBrowser.IsOpen || inputResultJson == null) return;
        FileBrowser.SetFilters(false,
            new FileBrowser.Filter(
                "JobCheck AI result",
                AiExchangeContract.ResultFileExtension));
        FileBrowser.ShowLoadDialog(
            paths =>
            {
                if (this == null || !gameObject.activeInHierarchy
                    || paths == null || paths.Length != 1) return;
                PersistenceStorageResult<string> loaded =
                    AiJobFitExchangeFileService.ReadResultText(paths[0]);
                if (!loaded.IsSuccess)
                {
                    SetStatus("讀取失敗：" + FirstIssue(loaded.Issues), true);
                    return;
                }

                inputResultJson.text = loaded.Value;
                ValidatePastedResult();
            },
            () => { },
            FileBrowser.PickMode.Files,
            false,
            GetInitialBrowseDirectory(),
            null,
            "選擇 JobCheck AI 分析結果",
            "選擇");
    }

    public void ValidatePastedResult()
    {
        validatedResult = null;
        if (buttonSaveResult != null) buttonSaveResult.interactable = false;
        if (inputResultJson == null || string.IsNullOrWhiteSpace(inputResultJson.text))
        {
            SetStatus("請貼上 AI 回傳 JSON，或選擇結果檔。", true);
            return;
        }

        string normalized = AiJobFitResultValidator.RemoveMarkdownFence(inputResultJson.text);
        if (!PersistenceJsonSerializer.TryDeserialize(
            normalized,
            out AiExchangeEnvelopeProbeDto probe,
            out string probeError))
        {
            SetStatus("無法讀取 JSON：" + probeError, true);
            return;
        }

        string expectedRequestId = ResolveExpectedRequestId(probe);
        if (expectedRequestId == null)
            return;
        AiJobFitResultValidationResult validation =
            AiJobFitResultValidator.ParseAndValidate(
                normalized,
                expectedRequestId,
                currentJobId);
        if (!validation.IsSuccess)
        {
            SetStatus("驗證失敗：\n" + string.Join("\n", validation.Issues.Take(6)), true);
            return;
        }

        validatedResult = validation.Value;
        if (textResultPreview != null)
            textResultPreview.text = FormatResult(validatedResult, "待儲存的 AI 分析");
        if (buttonSaveResult != null)
            buttonSaveResult.interactable = personalProfileActive;
        SetStatus(personalProfileActive
            ? "驗證成功。請確認下方內容，再按「儲存分析」。"
            : "驗證成功。Demo 資料區只提供預覽，不保存分析。", false);
    }

    public void SaveValidatedResult()
    {
        if (!personalProfileActive || validatedResult == null) return;
        PersistenceStorageResult<AiJobFitAnalysisSaveSummary> saved =
            AiJobFitAnalysisRepository.SaveResult(personalDataRoot, validatedResult);
        if (!saved.IsSuccess)
        {
            SetStatus("儲存失敗，職缺與履歷均未修改：" + FirstIssue(saved.Issues), true);
            return;
        }

        if (buttonSaveResult != null) buttonSaveResult.interactable = false;
        SetStatus("AI 分析已另存：\n" + saved.Value.Path
            + "\n原始職缺、履歷與求職條件均未修改。", false);
    }

    private void BuildRequestPreview()
    {
        currentRequest = null;
        PersistenceStorageResult<JobPostingReadOnlyList> jobs =
            JobPostingReadOnlyQuery.Load(activeDataRoot);
        if (!jobs.IsSuccess)
        {
            SetStatus("無法載入職缺資料：" + FirstIssue(jobs.Issues), true);
            SetPreview("目前無法建立 AI 分析需求。");
            return;
        }

        JobPostingReadOnlyItem selected = jobs.Value.Items.FirstOrDefault(item =>
            item?.JobPosting != null
            && string.Equals(item.JobPosting.Id, currentJobId, StringComparison.Ordinal));
        if (selected == null)
        {
            SetStatus("找不到目前職缺的正式資料。", true);
            SetPreview("目前無法建立 AI 分析需求。");
            return;
        }

        PersistenceStorageResult<CareerProfile> profile =
            CareerProfileRepository.Load(personalDataRoot);
        if (!profile.IsSuccess)
        {
            SetStatus("無法載入個人履歷：" + FirstIssue(profile.Issues), true);
            SetPreview("請先完成並儲存個人履歷，再使用 AI 輔助。");
            return;
        }

        AiJobFitRequestCreationResult created = AiJobFitRequestFactory.Create(
            selected.Company,
            selected.JobPosting,
            profile.Value,
            requirementMatch,
            requirementScore,
            preferenceMatch,
            preferenceScore,
            DateTimeOffset.UtcNow);
        if (!created.IsSuccess)
        {
            SetStatus("無法建立 AI 分析需求：" + string.Join("\n", created.Issues), true);
            SetPreview("目前無法建立 AI 分析需求。");
            return;
        }

        currentRequest = created.Package;
        SetPreview(FormatDataPreview(created.Package));
        SetStatus("請先確認送出範圍。JobCheck 不會自動連線或上傳資料。", false);
    }

    private void LoadLatestSavedResult()
    {
        if (!personalProfileActive || string.IsNullOrWhiteSpace(currentJobId)) return;
        PersistenceStorageResult<AiJobFitResultPackageDto> latest =
            AiJobFitAnalysisRepository.LoadLatestResult(personalDataRoot, currentJobId);
        if (latest.IsSuccess && textResultPreview != null)
            textResultPreview.text = FormatResult(latest.Value, "最近儲存的 AI 分析");
    }

    private string ResolveExpectedRequestId(AiExchangeEnvelopeProbeDto probe)
    {
        if (probe == null || string.IsNullOrWhiteSpace(probe.request_id))
        {
            SetStatus("AI 結果缺少 request_id。", true);
            return null;
        }

        if (currentRequest != null
            && string.Equals(currentRequest.request_id, probe.request_id, StringComparison.Ordinal))
        {
            return currentRequest.request_id;
        }

        if (!personalProfileActive)
        {
            SetStatus("Demo 模式只接受本次開啟面板後匯出的 request_id。", true);
            return null;
        }

        PersistenceStorageResult<AiJobFitRequestPackageDto> registered =
            AiJobFitAnalysisRepository.LoadRequest(personalDataRoot, probe.request_id);
        if (!registered.IsSuccess)
        {
            SetStatus("找不到這次 AI 分析需求的本機登記：" + FirstIssue(registered.Issues), true);
            return null;
        }
        if (!string.Equals(registered.Value.job_id, currentJobId, StringComparison.Ordinal))
        {
            SetStatus("這份 AI 結果屬於其他職缺，不能匯入目前頁面。", true);
            return null;
        }

        return registered.Value.request_id;
    }

    private bool EnsureRequestReady()
    {
        if (currentRequest != null) return true;
        SetStatus("目前無法建立 AI 分析需求；請確認職缺與履歷資料。", true);
        return false;
    }

    private void OnResultTextChanged(string _)
    {
        validatedResult = null;
        if (buttonSaveResult != null) buttonSaveResult.interactable = false;
    }

    private void ApplyTheme()
    {
        if (theme == null) return;
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = theme.BodyFont != null ? theme.BodyFont : text.font;
            text.color = theme.TextPrimary;
        }
        StylePrimary(buttonExportRequest);
        StylePrimary(buttonSaveResult);
        StyleSecondary(buttonCopyInstructions);
        StyleSecondary(buttonChooseResult);
        StyleSecondary(buttonValidateResult);
        StyleSecondary(buttonClose);
    }

    private void StylePrimary(Button button)
    {
        if (button == null || theme == null) return;
        Image image = button.targetGraphic as Image;
        if (image != null)
        {
            image.color = theme.Primary;
            image.sprite = theme.ButtonBackgroundSprite;
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        }
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.color = theme.TextOnPrimary;
    }

    private void StyleSecondary(Button button)
    {
        if (button == null || theme == null) return;
        Image image = button.targetGraphic as Image;
        if (image != null)
        {
            image.color = theme.SurfaceMuted;
            image.sprite = theme.ButtonBackgroundSprite;
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        }
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.color = theme.TextPrimary;
    }

    private string FormatDataPreview(AiJobFitRequestPackageDto package)
    {
        CareerProfileDto resume = package.payload.resume;
        var builder = new StringBuilder();
        builder.AppendLine("【本次送出範圍】");
        builder.AppendLine("公司：" + Safe(package.payload.company?.name));
        builder.AppendLine("職缺：" + Safe(package.payload.job?.title));
        builder.AppendLine("履歷摘要：" + (string.IsNullOrWhiteSpace(resume?.summary) ? "未填寫" : "會送出"));
        builder.AppendLine("履歷區塊：技能 " + Count(resume?.skills)
            + "、工作經歷 " + Count(resume?.experiences)
            + "、專案 " + Count(resume?.projects)
            + "、學歷 " + Count(resume?.educations)
            + "、語言 " + Count(resume?.languages)
            + "、公開連結 " + Count(resume?.links));
        builder.AppendLine("規則比對：履歷→職缺 " + FormatScore(package.payload.resume_to_job)
            + "；職缺→求職條件 " + FormatScore(package.payload.job_to_preferences));
        builder.AppendLine();
        builder.AppendLine("會包含：" + string.Join("、", package.payload.data_scope));
        builder.AppendLine("不包含：" + string.Join("、", package.payload.excluded_data));
        builder.AppendLine();
        builder.Append("JobCheck 只產生檔案，不會自動連線。請自行選擇要交給哪個 AI 服務。");
        return Escape(builder.ToString());
    }

    private static string FormatResult(AiJobFitResultPackageDto package, string heading)
    {
        if (package?.result == null) return "尚無可顯示的 AI 分析。";
        AiJobFitAnalysisDto value = package.result;
        var builder = new StringBuilder();
        builder.AppendLine("【" + heading + "】");
        builder.AppendLine("建議：" + RecommendationLabel(value.recommendation)
            + "　信心：" + ConfidenceLabel(value.confidence));
        if (package.producer != null
            && (!string.IsNullOrWhiteSpace(package.producer.provider)
                || !string.IsNullOrWhiteSpace(package.producer.model)))
        {
            builder.AppendLine("來源：" + Safe(package.producer.provider)
                + (string.IsNullOrWhiteSpace(package.producer.model)
                    ? string.Empty : " / " + package.producer.model));
        }
        builder.AppendLine();
        builder.AppendLine(value.summary);
        AppendInsights(builder, "優勢", value.strengths);
        AppendInsights(builder, "差距", value.gaps);
        AppendInsights(builder, "風險", value.risks);
        AppendStrings(builder, "缺少資訊", value.missing_information);
        AppendStrings(builder, "面試確認問題", value.interview_questions);
        AppendStrings(builder, "建議行動", value.suggested_actions);
        return Escape(builder.ToString().TrimEnd());
    }

    private static void AppendInsights(
        StringBuilder builder,
        string heading,
        IEnumerable<AiInsightDto> values)
    {
        AiInsightDto[] items = values?.Where(item => item != null).ToArray()
            ?? Array.Empty<AiInsightDto>();
        if (items.Length == 0) return;
        builder.AppendLine();
        builder.AppendLine("【" + heading + "】");
        foreach (AiInsightDto item in items)
        {
            builder.Append("• ").Append(item.title).Append("：").AppendLine(item.detail);
            if (item.evidence_refs != null && item.evidence_refs.Count > 0)
                builder.Append("  依據：").AppendLine(string.Join("、", item.evidence_refs));
        }
    }

    private static void AppendStrings(
        StringBuilder builder,
        string heading,
        IEnumerable<string> values)
    {
        string[] items = values?.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()
            ?? Array.Empty<string>();
        if (items.Length == 0) return;
        builder.AppendLine();
        builder.AppendLine("【" + heading + "】");
        foreach (string item in items) builder.Append("• ").AppendLine(item);
    }

    private static string BuildUsageInstructions()
    {
        return "請讀取我提供的 JobCheck AI request JSON。"
            + "依照 payload.instructions 分析，且只回傳一個符合 response_template 的 JSON 物件；"
            + "不要加 Markdown 程式碼圍欄或前後說明。";
    }

    private static string GetExportDirectory()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string root = string.IsNullOrWhiteSpace(documents)
            ? UnityEngine.Application.persistentDataPath
            : documents;
        return Path.Combine(root, "JobCheck", "AI");
    }

    private static string GetInitialBrowseDirectory()
    {
        string directory = GetExportDirectory();
        if (Directory.Exists(directory)) return directory;
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Directory.Exists(documents)
            ? documents
            : UnityEngine.Application.persistentDataPath;
    }

    private void SetPreview(string value)
    {
        if (textDataPreview != null) textDataPreview.text = Escape(value);
    }

    private void SetStatus(string value, bool error)
    {
        if (textStatus == null) return;
        textStatus.text = Escape(value);
        textStatus.color = error && theme != null ? theme.Danger
            : theme != null ? theme.TextSecondary : Color.black;
    }

    private static string FirstIssue(IEnumerable<PersistenceStorageIssue> issues)
    {
        PersistenceStorageIssue first = issues?.FirstOrDefault();
        return first == null ? "未知錯誤" : first.Message;
    }

    private static string FormatScore(AiRuleComparisonSnapshotDto value)
    {
        return value != null && value.has_score ? value.score + " 分" : "無法計分";
    }

    private static int Count<T>(ICollection<T> values) => values?.Count ?? 0;
    private static string Safe(string value) => string.IsNullOrWhiteSpace(value) ? "未提供" : value;

    private static string RecommendationLabel(string value)
    {
        switch (value)
        {
            case "apply": return "建議投遞";
            case "skip": return "建議略過";
            default: return "建議再確認";
        }
    }

    private static string ConfidenceLabel(string value)
    {
        switch (value)
        {
            case "high": return "高";
            case "low": return "低";
            default: return "中";
        }
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
