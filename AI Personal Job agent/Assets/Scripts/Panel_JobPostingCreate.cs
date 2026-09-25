using System;
using System.Collections.Generic;
using System.Globalization;
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
    [SerializeField] private TMP_InputField inputCapturedAt;
    [SerializeField] private TMP_InputField inputDepartment;
    [SerializeField] private TMP_InputField inputCategory;
    [SerializeField] private TMP_InputField inputCompensationType;
    [SerializeField] private TMP_InputField inputCompensationPeriod;
    [SerializeField] private TMP_InputField inputCompensationMinimum;
    [SerializeField] private TMP_InputField inputCompensationMaximum;
    [SerializeField] private TMP_InputField inputCompensationCurrency;
    [SerializeField] private TMP_InputField inputCompensationRawText;
    [SerializeField] private TMP_InputField inputLocationRawText;
    [SerializeField] private TMP_InputField inputWorkMode;
    [SerializeField] private TMP_InputField inputEmploymentType;
    [SerializeField] private TMP_InputField inputWorkingHours;
    [SerializeField] private TMP_InputField inputExperience;
    [SerializeField] private TMP_InputField inputEducation;
    [SerializeField] private TMP_InputField inputResponsibilities;
    [SerializeField] private TMP_InputField inputTools;
    [SerializeField] private TMP_InputField inputSkills;

    [Header("Single Select Fields")]
    [SerializeField] private TMP_Dropdown dropdownCompensationType;
    [SerializeField] private TMP_Dropdown dropdownCompensationPeriod;
    [SerializeField] private TMP_Dropdown dropdownWorkMode;
    [SerializeField] private TMP_Dropdown dropdownEmploymentType;
    [Tooltip("Dropdown 目前值與展開選項的字體大小。")]
    [SerializeField] private float dropdownFontSize = 24f;

    [Header("Multiple Select Fields")]
    [SerializeField] private JobPostingMultiSelectField multiSelectTags;
    [SerializeField] private JobPostingMultiSelectField multiSelectRiskFlags;

    [Header("Actions")]
    [SerializeField] private TMP_Text textTitle;
    [SerializeField] private Button buttonSave;
    [SerializeField] private Button buttonCancel;
    [SerializeField] private Button buttonCloseTop;
    [SerializeField] private TMP_Text textMessage;

    private AllJobPage owner;
    private string editingJobPostingId;
    private Action closeCallback;
    private readonly HashSet<string> existingUnknownTags =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> existingUnknownRiskFlags =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> compensationTypeValues = new List<string>();
    private readonly List<string> compensationPeriodValues = new List<string>();
    private readonly List<string> workModeValues = new List<string>();
    private readonly List<string> employmentTypeValues = new List<string>();
    private const float DefaultDropdownFontSize = 24f;

    private void Awake()
    {
        AutoBindReferences();
        SetupStructuredFields();
        BindButtons();
    }

    /// <summary>
    /// 清空上一次輸入並顯示表單。
    /// </summary>
    public void Show(AllJobPage page)
    {
        owner = page;
        editingJobPostingId = null;
        closeCallback = null;
        AutoBindReferences();
        SetupStructuredFields();
        BindButtons();
        ClearFields();
        SetText(inputCompensationPeriod, "monthly");
        SetDropdownValue(dropdownCompensationPeriod, compensationPeriodValues, "monthly");
        SetText(inputCompensationCurrency, "TWD");
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
    /// 以既有資料開啟編輯模式，並回填目前表單可維護的職缺欄位。
    /// </summary>
    public void ShowForEdit(AllJobPage page, JobDetailData data, Action onClosed = null)
    {
        owner = page;
        editingJobPostingId = data != null ? data.id : null;
        closeCallback = onClosed;
        AutoBindReferences();
        SetupStructuredFields();
        BindButtons();
        SetText(inputCompanyName, data != null && data.company != null ? data.company.name : null);
        SetText(inputTitle, data != null && data.job != null ? data.job.title : null);
        SetText(inputSourcePlatform, data != null && data.source != null ? data.source.platform : null);
        SetText(inputSourceUrl, data != null && data.source != null ? data.source.url : null);
        SetText(inputRawDescription, data != null && data.job != null ? data.job.raw_text : null);
        SetText(inputCapturedAt, FormatDateForInput(data != null && data.source != null ? data.source.captured_at : null));
        SetText(inputDepartment, data != null && data.job != null ? data.job.department : null);
        SetText(inputCategory, data != null && data.job != null ? data.job.category : null);
        SetText(inputCompensationType, data != null && data.compensation != null ? data.compensation.type : null);
        SetText(inputCompensationPeriod, data != null && data.compensation != null ? data.compensation.period : null);
        SetDropdownValue(
            dropdownCompensationType,
            compensationTypeValues,
            data != null && data.compensation != null ? data.compensation.type : null);
        SetDropdownValue(
            dropdownCompensationPeriod,
            compensationPeriodValues,
            data != null && data.compensation != null ? data.compensation.period : null);
        SetText(
            inputCompensationMinimum,
            data != null && data.compensation != null && data.compensation.has_min
                ? data.compensation.min.ToString(CultureInfo.InvariantCulture)
                : null);
        SetText(
            inputCompensationMaximum,
            data != null && data.compensation != null && data.compensation.has_max
                ? data.compensation.max.ToString(CultureInfo.InvariantCulture)
                : null);
        SetText(inputCompensationCurrency, data != null && data.compensation != null ? data.compensation.currency : null);
        SetText(inputCompensationRawText, data != null && data.compensation != null ? data.compensation.raw_text : null);
        SetText(inputLocationRawText, data != null && data.location != null ? data.location.raw_text : null);
        SetText(inputWorkMode, data != null && data.location != null ? data.location.work_mode : null);
        SetText(inputEmploymentType, data != null && data.work_conditions != null ? data.work_conditions.employment_type : null);
        SetDropdownValue(
            dropdownWorkMode,
            workModeValues,
            data != null && data.location != null ? data.location.work_mode : null);
        SetDropdownValue(
            dropdownEmploymentType,
            employmentTypeValues,
            data != null && data.work_conditions != null ? data.work_conditions.employment_type : null);
        SetText(inputWorkingHours, data != null && data.work_conditions != null ? data.work_conditions.working_hours : null);
        SetText(inputExperience, data != null && data.requirements != null ? data.requirements.experience : null);
        SetText(inputEducation, data != null && data.requirements != null ? data.requirements.education : null);
        SetLines(inputResponsibilities, data != null ? data.responsibilities : null);
        SetLines(inputTools, data != null && data.requirements != null ? data.requirements.tools : null);
        SetLines(inputSkills, data != null && data.requirements != null ? data.requirements.skills : null);
        SetLabelsForEdit(inputTags, data != null ? data.tags : null, false, existingUnknownTags);
        SetLabelsForEdit(
            inputRiskFlags,
            data != null ? data.risk_flags : null,
            true,
            existingUnknownRiskFlags);
        SetMultiSelectForEdit(
            multiSelectTags,
            data != null ? data.tags : null,
            false,
            existingUnknownTags);
        SetMultiSelectForEdit(
            multiSelectRiskFlags,
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
        if (!TryReadOptionalDate(inputCapturedAt, out DateTimeOffset? capturedAt, out string dateError))
        {
            SetMessage("無法儲存職缺：\n• " + dateError, true);
            return;
        }

        if (!TryReadOptionalInt(inputCompensationMinimum, "薪資下限", out int? compensationMinimum, out string minimumError))
        {
            SetMessage("無法儲存職缺：\n• " + minimumError, true);
            return;
        }

        if (!TryReadOptionalInt(inputCompensationMaximum, "薪資上限", out int? compensationMaximum, out string maximumError))
        {
            SetMessage("無法儲存職缺：\n• " + maximumError, true);
            return;
        }

        if (compensationMinimum.HasValue
            && compensationMaximum.HasValue
            && compensationMaximum.Value < compensationMinimum.Value)
        {
            SetMessage(
                "無法儲存職缺：\n• 薪資上限不可低於薪資下限。"
                + "（目前下限 " + compensationMinimum.Value
                + "、上限 " + compensationMaximum.Value + "）",
                true);
            return;
        }

        if (!TryReadSelectedLabels(
            multiSelectTags,
            inputTags,
            false,
            existingUnknownTags,
            out List<string> tags,
            out string invalidTag))
        {
            SetMessage("無法儲存職缺：\n• 未知標籤：" + invalidTag, true);
            return;
        }

        if (!TryReadSelectedLabels(
            multiSelectRiskFlags,
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
                CapturedAt = capturedAt,
                Department = GetText(inputDepartment),
                Category = GetText(inputCategory),
                CompensationType = GetStructuredValue(dropdownCompensationType, compensationTypeValues, inputCompensationType),
                CompensationPeriod = GetStructuredValue(dropdownCompensationPeriod, compensationPeriodValues, inputCompensationPeriod),
                CompensationMinimum = compensationMinimum,
                CompensationMaximum = compensationMaximum,
                CompensationCurrency = GetText(inputCompensationCurrency),
                CompensationRawText = GetText(inputCompensationRawText),
                LocationRawText = GetText(inputLocationRawText),
                WorkMode = GetStructuredValue(dropdownWorkMode, workModeValues, inputWorkMode),
                EmploymentType = GetStructuredValue(dropdownEmploymentType, employmentTypeValues, inputEmploymentType),
                WorkingHours = GetText(inputWorkingHours),
                Experience = GetText(inputExperience),
                Education = GetText(inputEducation),
                Responsibilities = GetLines(inputResponsibilities),
                Tools = GetLines(inputTools),
                Skills = GetLines(inputSkills),
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
                CapturedAt = capturedAt,
                Department = GetText(inputDepartment),
                Category = GetText(inputCategory),
                CompensationType = GetStructuredValue(dropdownCompensationType, compensationTypeValues, inputCompensationType),
                CompensationPeriod = GetStructuredValue(dropdownCompensationPeriod, compensationPeriodValues, inputCompensationPeriod),
                CompensationMinimum = compensationMinimum,
                CompensationMaximum = compensationMaximum,
                CompensationCurrency = GetText(inputCompensationCurrency),
                CompensationRawText = GetText(inputCompensationRawText),
                LocationRawText = GetText(inputLocationRawText),
                WorkMode = GetStructuredValue(dropdownWorkMode, workModeValues, inputWorkMode),
                EmploymentType = GetStructuredValue(dropdownEmploymentType, employmentTypeValues, inputEmploymentType),
                WorkingHours = GetText(inputWorkingHours),
                Experience = GetText(inputExperience),
                Education = GetText(inputEducation),
                Responsibilities = GetLines(inputResponsibilities),
                Tools = GetLines(inputTools),
                Skills = GetLines(inputSkills),
                Tags = tags,
                RiskFlags = riskFlags
            });
        if (result.IsSuccess)
        {
            CloseAndComplete();
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
        CloseAndComplete();
    }

    private void CloseAndComplete()
    {
        Action callback = closeCallback;
        closeCallback = null;
        gameObject.SetActive(false);
        callback?.Invoke();
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
        inputCapturedAt = inputCapturedAt ?? FindInput("Input_CapturedAt");
        inputDepartment = inputDepartment ?? FindInput("Input_Department");
        inputCategory = inputCategory ?? FindInput("Input_Category");
        inputCompensationType = inputCompensationType ?? FindInput("Input_CompensationType");
        inputCompensationPeriod = inputCompensationPeriod ?? FindInput("Input_CompensationPeriod");
        inputCompensationMinimum = inputCompensationMinimum ?? FindInput("Input_CompensationMinimum");
        inputCompensationMaximum = inputCompensationMaximum ?? FindInput("Input_CompensationMaximum");
        inputCompensationCurrency = inputCompensationCurrency ?? FindInput("Input_CompensationCurrency");
        inputCompensationRawText = inputCompensationRawText ?? FindInput("Input_CompensationRawText");
        inputLocationRawText = inputLocationRawText ?? FindInput("Input_LocationRawText");
        inputWorkMode = inputWorkMode ?? FindInput("Input_WorkMode");
        inputEmploymentType = inputEmploymentType ?? FindInput("Input_EmploymentType");
        inputWorkingHours = inputWorkingHours ?? FindInput("Input_WorkingHours");
        inputExperience = inputExperience ?? FindInput("Input_Experience");
        inputEducation = inputEducation ?? FindInput("Input_Education");
        inputResponsibilities = inputResponsibilities ?? FindInput("Input_Responsibilities");
        inputTools = inputTools ?? FindInput("Input_Tools");
        inputSkills = inputSkills ?? FindInput("Input_Skills");
        dropdownCompensationType = dropdownCompensationType ?? FindDropdown("Dropdown_CompensationType");
        dropdownCompensationPeriod = dropdownCompensationPeriod ?? FindDropdown("Dropdown_CompensationPeriod");
        dropdownWorkMode = dropdownWorkMode ?? FindDropdown("Dropdown_WorkMode");
        dropdownEmploymentType = dropdownEmploymentType ?? FindDropdown("Dropdown_EmploymentType");
        multiSelectTags = multiSelectTags ?? FindMultiSelect("MultiSelect_Tags");
        multiSelectRiskFlags = multiSelectRiskFlags ?? FindMultiSelect("MultiSelect_RiskFlags");
        if (textTitle == null)
        {
            Transform title = transform.Find("Text_Title");
            textTitle = title == null ? null : title.GetComponent<TMP_Text>();
        }
        buttonSave = buttonSave ?? FindButton("Button_SaveJobPosting");
        buttonCancel = buttonCancel ?? FindButton("Button_CancelJobPosting");
        buttonCloseTop = buttonCloseTop ?? FindButton("Button_CloseTop");

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

        if (buttonCloseTop != null)
        {
            buttonCloseTop.onClick.RemoveListener(Cancel);
            buttonCloseTop.onClick.AddListener(Cancel);
        }
    }

    private TMP_InputField FindInput(string childName)
    {
        TMP_InputField[] inputs = GetComponentsInChildren<TMP_InputField>(true);
        foreach (TMP_InputField input in inputs)
        {
            if (input.name == childName)
            {
                return input;
            }
        }

        return null;
    }

    private Button FindButton(string childName)
    {
        Transform child = transform.Find(childName);
        return child == null ? null : child.GetComponent<Button>();
    }

    private TMP_Dropdown FindDropdown(string childName)
    {
        TMP_Dropdown[] dropdowns = GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            if (dropdown.name == childName)
            {
                return dropdown;
            }
        }

        return null;
    }

    private JobPostingMultiSelectField FindMultiSelect(string childName)
    {
        JobPostingMultiSelectField[] fields = GetComponentsInChildren<JobPostingMultiSelectField>(true);
        foreach (JobPostingMultiSelectField field in fields)
        {
            if (field.name == childName)
            {
                return field;
            }
        }

        return null;
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
        SetText(inputCapturedAt, string.Empty);
        SetText(inputDepartment, string.Empty);
        SetText(inputCategory, string.Empty);
        SetText(inputCompensationType, string.Empty);
        SetText(inputCompensationPeriod, string.Empty);
        SetText(inputCompensationMinimum, string.Empty);
        SetText(inputCompensationMaximum, string.Empty);
        SetText(inputCompensationCurrency, string.Empty);
        SetText(inputCompensationRawText, string.Empty);
        SetText(inputLocationRawText, string.Empty);
        SetText(inputWorkMode, string.Empty);
        SetText(inputEmploymentType, string.Empty);
        SetText(inputWorkingHours, string.Empty);
        SetText(inputExperience, string.Empty);
        SetText(inputEducation, string.Empty);
        SetText(inputResponsibilities, string.Empty);
        SetText(inputTools, string.Empty);
        SetText(inputSkills, string.Empty);
        SetDropdownValue(dropdownCompensationType, compensationTypeValues, null);
        SetDropdownValue(dropdownCompensationPeriod, compensationPeriodValues, null);
        SetDropdownValue(dropdownWorkMode, workModeValues, null);
        SetDropdownValue(dropdownEmploymentType, employmentTypeValues, null);
        multiSelectTags?.SetSelectedValues(null);
        multiSelectRiskFlags?.SetSelectedValues(null);
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

    private static bool TryReadOptionalDate(
        TMP_InputField input,
        out DateTimeOffset? value,
        out string error)
    {
        value = null;
        error = null;
        string text = GetText(input);
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!DateTime.TryParseExact(
            text.Trim(),
            "yyyy.MM.dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime date))
        {
            error = "收錄日期請使用 yyyy.MM.dd，例如 2026.09.13。";
            return false;
        }

        date = DateTime.SpecifyKind(date.Date.AddHours(12), DateTimeKind.Local);
        value = new DateTimeOffset(date);
        return true;
    }

    private static bool TryReadOptionalInt(
        TMP_InputField input,
        string label,
        out int? value,
        out string error)
    {
        value = null;
        error = null;
        string text = GetText(input);
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        string normalized = text.Trim().Replace(",", string.Empty);
        if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed < 0)
        {
            error = label + "必須是大於或等於 0 的整數。";
            return false;
        }

        value = parsed;
        return true;
    }

    private static List<string> GetLines(TMP_InputField input)
    {
        var values = new List<string>();
        string text = GetText(input);
        if (string.IsNullOrWhiteSpace(text))
        {
            return values;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        foreach (string line in lines)
        {
            string value = line.Trim();
            if (value.Length > 0 && seen.Add(value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static void SetLines(TMP_InputField input, IEnumerable<string> values)
    {
        SetText(input, values == null ? string.Empty : string.Join(Environment.NewLine, values));
    }

    private static string FormatDateForInput(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsed))
        {
            return string.Empty;
        }

        return parsed.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
    }

    private void SetupStructuredFields()
    {
        SetRequirementHint(inputExperience, "3 年以上（填數字）或不拘");
        SetRequirementHint(inputEducation, "學士以上、碩士以上、不拘；在學可加註（在學可）");
        SetupDropdown(
            dropdownCompensationType,
            compensationTypeValues,
            new[] { "未設定", "範圍", "固定金額", "最低薪資", "面議" },
            new[] { "", "range", "fixed", "minimum", "negotiable" });
        SetupDropdown(
            dropdownCompensationPeriod,
            compensationPeriodValues,
            new[] { "未設定", "月薪", "年薪", "時薪", "日薪" },
            new[] { "", "monthly", "yearly", "hourly", "daily" });
        SetupDropdown(
            dropdownWorkMode,
            workModeValues,
            new[] { "未設定", "現場", "混合", "遠端" },
            new[] { "", "onsite", "hybrid", "remote" });
        SetupDropdown(
            dropdownEmploymentType,
            employmentTypeValues,
            new[] { "未設定", "全職", "兼職", "約聘", "派遣", "實習", "直聘" },
            new[] { "", "全職", "兼職", "約聘", "派遣", "實習", "直聘" });
        multiSelectTags?.Configure(JobPostingLabelCatalog.TagOptions);
        multiSelectRiskFlags?.Configure(JobPostingLabelCatalog.RiskFlagOptions);
    }

    private static void SetRequirementHint(TMP_InputField input, string hint)
    {
        if (input?.placeholder is TMP_Text placeholder)
            placeholder.text = hint;
    }

    private void SetupDropdown(
        TMP_Dropdown dropdown,
        List<string> values,
        IEnumerable<string> labels,
        IEnumerable<string> stableValues)
    {
        values.Clear();
        values.AddRange(stableValues);
        if (dropdown == null)
        {
            return;
        }

        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(labels));
        ApplyDropdownFontSize(dropdown);
        dropdown.SetValueWithoutNotify(0);
        dropdown.RefreshShownValue();
    }

    /// <summary>
    /// 同時放大 Dropdown 收合狀態與展開清單的文字，並確保選項列高度不會裁切文字。
    /// </summary>
    private void ApplyDropdownFontSize(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        float fontSize = dropdownFontSize > 0f
            ? dropdownFontSize
            : DefaultDropdownFontSize;
        if (dropdown.captionText != null)
        {
            dropdown.captionText.fontSize = fontSize;
        }

        if (dropdown.itemText == null)
        {
            return;
        }

        dropdown.itemText.fontSize = fontSize;
        RectTransform itemRect = dropdown.itemText.transform.parent as RectTransform;
        if (itemRect != null)
        {
            itemRect.sizeDelta = new Vector2(
                itemRect.sizeDelta.x,
                Mathf.Max(itemRect.sizeDelta.y, fontSize + 12f));
        }
    }

    private static string GetDropdownValue(TMP_Dropdown dropdown, IReadOnlyList<string> values)
    {
        if (dropdown == null || dropdown.value < 0 || dropdown.value >= values.Count)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(values[dropdown.value]) ? null : values[dropdown.value];
    }

    private static string GetStructuredValue(
        TMP_Dropdown dropdown,
        IReadOnlyList<string> values,
        TMP_InputField fallbackInput)
    {
        return dropdown == null ? GetText(fallbackInput) : GetDropdownValue(dropdown, values);
    }

    private static void SetDropdownValue(TMP_Dropdown dropdown, List<string> values, string value)
    {
        if (dropdown == null)
        {
            return;
        }

        int index = 0;
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index == 0 && !string.IsNullOrWhiteSpace(value))
        {
            values.Add(value);
            dropdown.options.Add(new TMP_Dropdown.OptionData(value));
            index = values.Count - 1;
        }

        dropdown.SetValueWithoutNotify(index);
        dropdown.RefreshShownValue();
    }

    private static void SetMultiSelectForEdit(
        JobPostingMultiSelectField field,
        IEnumerable<string> values,
        bool isRiskFlag,
        ISet<string> unknownValues)
    {
        if (field == null)
        {
            return;
        }

        unknownValues.Clear();
        var knownValues = new List<string>();
        if (values != null)
        {
            foreach (string value in values)
            {
                bool known = isRiskFlag
                    ? JobPostingLabelCatalog.IsKnownRiskFlag(value)
                    : JobPostingLabelCatalog.IsKnownTag(value);
                if (known)
                {
                    knownValues.Add(value);
                }
                else if (!string.IsNullOrWhiteSpace(value))
                {
                    unknownValues.Add(value.Trim());
                }
            }
        }

        field.SetSelectedValues(knownValues);
    }

    private static bool TryReadSelectedLabels(
        JobPostingMultiSelectField field,
        TMP_InputField fallbackInput,
        bool isRiskFlag,
        ISet<string> allowedUnknownValues,
        out List<string> values,
        out string invalidValue)
    {
        if (field == null)
        {
            return TryReadLabels(
                fallbackInput,
                isRiskFlag,
                allowedUnknownValues,
                out values,
                out invalidValue);
        }

        values = field.GetSelectedValues();
        foreach (string unknownValue in allowedUnknownValues)
        {
            if (!values.Contains(unknownValue))
            {
                values.Add(unknownValue);
            }
        }

        invalidValue = null;
        return true;
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
