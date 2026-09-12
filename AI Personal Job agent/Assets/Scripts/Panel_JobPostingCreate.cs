using System;
using System.Collections.Generic;
using System.Text;
using JobCheck.Domain;
using JobCheck.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 新增／編輯職缺的最小輸入表單。只收集文字與顯示結果，資料規則全部交給 JobPostingCommandService。
/// </summary>
public sealed class Panel_JobPostingCreate : MonoBehaviour
{
    [Header("Fields")]
    [SerializeField] private TMP_InputField inputCompanyName;
    [SerializeField] private TMP_InputField inputTitle;
    [SerializeField] private TMP_InputField inputSourcePlatform;
    [SerializeField] private TMP_InputField inputSourceUrl;
    [SerializeField] private TMP_InputField inputRawDescription;
    [SerializeField] private TMP_InputField inputTags;
    [SerializeField] private TMP_InputField inputRiskFlags;

    [Header("Actions")]
    [SerializeField] private TMP_Text textTitle;
    [SerializeField] private Button buttonSave;
    [SerializeField] private Button buttonCancel;
    [SerializeField] private TMP_Text textMessage;

    private AllJobPage owner;
    private string editingJobPostingId;
    private readonly HashSet<string> existingUnknownTags =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> existingUnknownRiskFlags =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        AutoBindReferences();
        BindButtons();
    }

    /// <summary>
    /// 清空上一次輸入並顯示表單。
    /// </summary>
    public void Show(AllJobPage page)
    {
        owner = page;
        editingJobPostingId = null;
        AutoBindReferences();
        BindButtons();
        ClearFields();
        SetPanelLabels("新增職缺", "儲存");
        SetMessage(string.Empty, false);
        gameObject.SetActive(true);

        if (inputCompanyName != null)
        {
            inputCompanyName.Select();
            inputCompanyName.ActivateInputField();
        }
    }

    /// <summary>
    /// 以既有資料開啟編輯模式。此表單只修改目前支援的五個欄位。
    /// </summary>
    public void ShowForEdit(AllJobPage page, JobDetailData data)
    {
        owner = page;
        editingJobPostingId = data != null ? data.id : null;
        AutoBindReferences();
        BindButtons();
        SetText(inputCompanyName, data != null && data.company != null ? data.company.name : null);
        SetText(inputTitle, data != null && data.job != null ? data.job.title : null);
        SetText(inputSourcePlatform, data != null && data.source != null ? data.source.platform : null);
        SetText(inputSourceUrl, data != null && data.source != null ? data.source.url : null);
        SetText(inputRawDescription, data != null && data.job != null ? data.job.raw_text : null);
        SetLabelsForEdit(inputTags, data != null ? data.tags : null, false, existingUnknownTags);
        SetLabelsForEdit(
            inputRiskFlags,
            data != null ? data.risk_flags : null,
            true,
            existingUnknownRiskFlags);
        SetPanelLabels("編輯職缺", "儲存變更");
        SetMessage(string.Empty, false);
        gameObject.SetActive(true);

        if (inputCompanyName != null)
        {
            inputCompanyName.Select();
            inputCompanyName.ActivateInputField();
        }
    }

    public void Submit()
    {
        if (owner == null)
        {
            SetMessage("找不到職缺列表控制器。", true);
            return;
        }

        bool isEditing = !string.IsNullOrEmpty(editingJobPostingId);
        if (!TryReadLabels(
            inputTags,
            false,
            existingUnknownTags,
            out List<string> tags,
            out string invalidTag))
        {
            SetMessage("無法儲存職缺：\n• 未知標籤：" + invalidTag, true);
            return;
        }

        if (!TryReadLabels(
            inputRiskFlags,
            true,
            existingUnknownRiskFlags,
            out List<string> riskFlags,
            out string invalidRiskFlag))
        {
            SetMessage("無法儲存職缺：\n• 未知風險標記：" + invalidRiskFlag, true);
            return;
        }

        PersistenceStorageResult<JobPostingWriteSummary> result = isEditing
            ? owner.UpdateV02JobPosting(new JobPostingEditRequest
            {
                JobPostingId = editingJobPostingId,
                CompanyName = GetText(inputCompanyName),
                Title = GetText(inputTitle),
                SourcePlatform = GetText(inputSourcePlatform),
                SourceUrl = GetText(inputSourceUrl),
                RawDescription = GetText(inputRawDescription),
                Tags = tags,
                RiskFlags = riskFlags
            })
            : owner.CreateV02JobPosting(new JobPostingCreateRequest
            {
                CompanyName = GetText(inputCompanyName),
                Title = GetText(inputTitle),
                SourcePlatform = GetText(inputSourcePlatform),
                SourceUrl = GetText(inputSourceUrl),
                RawDescription = GetText(inputRawDescription),
                Tags = tags,
                RiskFlags = riskFlags
            });
        if (result.IsSuccess)
        {
            gameObject.SetActive(false);
            return;
        }

        var message = new StringBuilder(isEditing ? "無法儲存職缺：" : "無法新增職缺：");
        foreach (PersistenceStorageIssue issue in result.Issues)
        {
            message.Append('\n').Append("• ").Append(issue.Message);
        }

        SetMessage(message.ToString(), true);
    }

    public void Cancel()
    {
        ClearFields();
        SetMessage(string.Empty, false);
        gameObject.SetActive(false);
    }

    private void AutoBindReferences()
    {
        inputCompanyName = inputCompanyName ?? FindInput("Input_CompanyName");
        inputTitle = inputTitle ?? FindInput("Input_Title");
        inputSourcePlatform = inputSourcePlatform ?? FindInput("Input_SourcePlatform");
        inputSourceUrl = inputSourceUrl ?? FindInput("Input_SourceUrl");
        inputRawDescription = inputRawDescription ?? FindInput("Input_RawDescription");
        inputTags = inputTags ?? FindInput("Input_Tags");
        inputRiskFlags = inputRiskFlags ?? FindInput("Input_RiskFlags");
        EnsureLabelInputs();
        if (textTitle == null)
        {
            Transform title = transform.Find("Text_Title");
            textTitle = title == null ? null : title.GetComponent<TMP_Text>();
        }
        buttonSave = buttonSave ?? FindButton("Button_SaveJobPosting");
        buttonCancel = buttonCancel ?? FindButton("Button_CancelJobPosting");

        if (textMessage == null)
        {
            Transform child = transform.Find("Text_Message");
            textMessage = child == null ? null : child.GetComponent<TMP_Text>();
        }
    }

    private void BindButtons()
    {
        if (buttonSave != null)
        {
            buttonSave.onClick.RemoveListener(Submit);
            buttonSave.onClick.AddListener(Submit);
        }

        if (buttonCancel != null)
        {
            buttonCancel.onClick.RemoveListener(Cancel);
            buttonCancel.onClick.AddListener(Cancel);
        }
    }

    private TMP_InputField FindInput(string childName)
    {
        Transform child = transform.Find(childName);
        return child == null ? null : child.GetComponent<TMP_InputField>();
    }

    private Button FindButton(string childName)
    {
        Transform child = transform.Find(childName);
        return child == null ? null : child.GetComponent<Button>();
    }

    private void ClearFields()
    {
        SetText(inputCompanyName, string.Empty);
        SetText(inputTitle, string.Empty);
        SetText(inputSourcePlatform, string.Empty);
        SetText(inputSourceUrl, string.Empty);
        SetText(inputRawDescription, string.Empty);
        SetText(inputTags, string.Empty);
        SetText(inputRiskFlags, string.Empty);
        existingUnknownTags.Clear();
        existingUnknownRiskFlags.Clear();
    }

    private void SetPanelLabels(string title, string saveButtonLabel)
    {
        if (textTitle != null)
        {
            textTitle.text = title;
        }

        if (buttonSave != null)
        {
            TMP_Text label = buttonSave.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = saveButtonLabel;
            }
        }
    }

    private void SetMessage(string message, bool isError)
    {
        if (textMessage == null)
        {
            return;
        }

        textMessage.text = message;
        textMessage.color = isError
            ? new Color(0.75f, 0.12f, 0.12f, 1f)
            : new Color(0.15f, 0.45f, 0.2f, 1f);
    }

    private static string GetText(TMP_InputField input)
    {
        return input == null ? null : input.text;
    }

    private static void SetText(TMP_InputField input, string value)
    {
        if (input != null)
        {
            input.SetTextWithoutNotify(value);
        }
    }

    /// <summary>
    /// 舊 Prefab 尚未含標籤欄位時，以既有單行輸入框為範本建立兩個欄位。
    /// 重複選項來自 Domain catalog，因此不需要在 Prefab 內複製十二組按鈕。
    /// </summary>
    private void EnsureLabelInputs()
    {
        if (inputSourceUrl == null)
        {
            return;
        }

        inputTags = inputTags ?? CreateLabelInput(
            "Input_Tags",
            -60f,
            "標籤（Unity、C#、.NET、Web、遠端工作、遊戲、教育；逗號分隔）");
        inputRiskFlags = inputRiskFlags ?? CreateLabelInput(
            "Input_RiskFlags",
            -135f,
            "風險（週末值班、薪資不透明、通勤距離較長、職務內容不明確、博弈產業）");

        RectTransform rawRect = inputRawDescription != null
            ? inputRawDescription.GetComponent<RectTransform>()
            : null;
        if (rawRect != null)
        {
            rawRect.anchoredPosition = new Vector2(0f, -240f);
            rawRect.sizeDelta = new Vector2(rawRect.sizeDelta.x, 110f);
        }
    }

    private TMP_InputField CreateLabelInput(string objectName, float y, string hint)
    {
        TMP_InputField input = Instantiate(inputSourceUrl, transform);
        input.name = objectName;
        input.SetTextWithoutNotify(string.Empty);
        input.lineType = TMP_InputField.LineType.SingleLine;

        RectTransform rect = input.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(1050f, 60f);

        TMP_Text placeholder = input.placeholder as TMP_Text;
        if (placeholder != null)
        {
            placeholder.text = hint;
        }

        return input;
    }

    private static void SetLabelsForEdit(
        TMP_InputField input,
        IEnumerable<string> values,
        bool isRiskFlag,
        ISet<string> unknownValues)
    {
        unknownValues.Clear();
        var displayValues = new List<string>();
        if (values != null)
        {
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                bool known = isRiskFlag
                    ? JobPostingLabelCatalog.IsKnownRiskFlag(value)
                    : JobPostingLabelCatalog.IsKnownTag(value);
                if (!known)
                {
                    unknownValues.Add(value.Trim());
                }

                displayValues.Add(isRiskFlag
                    ? JobPostingLabelCatalog.GetRiskFlagDisplayName(value)
                    : JobPostingLabelCatalog.GetTagDisplayName(value));
            }
        }

        SetText(input, string.Join("、", displayValues));
    }

    private static bool TryReadLabels(
        TMP_InputField input,
        bool isRiskFlag,
        ISet<string> allowedUnknownValues,
        out List<string> values,
        out string invalidValue)
    {
        values = new List<string>();
        invalidValue = null;
        string text = GetText(input);
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        string[] parts = text.Split(
            new[] { ',', '，', '、', ';', '；', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string part in parts)
        {
            string candidate = part.Trim();
            string value;
            bool resolved = isRiskFlag
                ? JobPostingLabelCatalog.TryResolveRiskFlag(candidate, out value)
                : JobPostingLabelCatalog.TryResolveTag(candidate, out value);
            if (!resolved && allowedUnknownValues.Contains(candidate))
            {
                value = candidate;
                resolved = true;
            }

            if (!resolved)
            {
                invalidValue = candidate;
                return false;
            }

            if (seen.Add(value))
            {
                values.Add(value);
            }
        }

        return true;
    }
}
