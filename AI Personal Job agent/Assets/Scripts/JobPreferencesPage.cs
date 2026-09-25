using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JobCheck.Domain;
using JobCheck.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>V0.2.8 求職條件單欄編輯頁。</summary>
public sealed class JobPreferencesPage : MonoBehaviour
{
    [SerializeField] private string personalDataRootPath = "../personal_data";
    [SerializeField] private JobCheckUiTheme theme;

    private bool built;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_InputField minimumSalaryInput;
    [SerializeField] private TMP_InputField desiredSalaryInput;
    [SerializeField] private TMP_InputField notesInput;
    [SerializeField] private TMP_Dropdown salaryPeriodDropdown;
    [SerializeField] private TMP_Dropdown commuteDropdown;
    [SerializeField] private TMP_Dropdown overtimeDropdown;
    [SerializeField] private TMP_Dropdown travelDropdown;
    [SerializeField] private TMP_Dropdown relocationDropdown;
    [SerializeField] private TMP_Dropdown targetImportanceDropdown;
    [SerializeField] private TMP_Dropdown salaryImportanceDropdown;
    [SerializeField] private TMP_Dropdown arrangementImportanceDropdown;
    [SerializeField] private TMP_Dropdown scheduleImportanceDropdown;
    [SerializeField] private TagEditor targetRoles;
    [SerializeField] private TagEditor industries;
    [SerializeField] private TagEditor regions;
    [SerializeField] private TagEditor employmentTypes;
    [SerializeField] private TagEditor workModes;
    [SerializeField] private TagEditor schedules;
    private CareerProfile currentProfile;

    private Color Surface => theme != null ? theme.Surface : Color.white;
    private Color SurfaceMuted => theme != null ? theme.SurfaceMuted : new Color32(238, 231, 229, 255);
    private Color Border => theme != null ? theme.Border : new Color32(180, 174, 175, 255);
    private Color Primary => theme != null ? theme.Primary : new Color32(126, 70, 80, 255);
    private Color PrimaryHover => theme != null ? theme.PrimaryHover : Primary;
    private Color PrimaryPressed => theme != null ? theme.PrimaryPressed : Primary;
    private Color TextPrimary => theme != null ? theme.TextPrimary : new Color32(46, 40, 41, 255);
    private Color TextSecondary => theme != null ? theme.TextSecondary : new Color32(98, 88, 91, 255);
    private Color TextOnPrimary => theme != null ? theme.TextOnPrimary : Color.white;
    private TMP_FontAsset Font => theme != null ? theme.BodyFont : null;

    public void Initialize(JobCheckUiTheme value)
    {
        theme = value;
    }

    private void OnEnable()
    {
        if (!HasSerializedUi(out string missingReferences))
        {
            Debug.LogError(
                "求職條件頁缺少場景 UI 引用；請確認 SampleScene 內的 Page_JobPreferences 已完整序列化：\n"
                + missingReferences,
                this);
            return;
        }

        if (!built)
        {
            built = true;
            BindRuntimeEvents();
        }

        if (UnityEngine.Application.isPlaying) LoadPreferences();
    }

    private bool HasSerializedUi(out string missingReferences)
    {
        var missing = new List<string>();
        RequireReference(scroll, nameof(scroll), missing);
        RequireReference(statusText, nameof(statusText), missing);
        RequireReference(saveButton, nameof(saveButton), missing);
        RequireReference(cancelButton, nameof(cancelButton), missing);
        RequireReference(minimumSalaryInput, nameof(minimumSalaryInput), missing);
        RequireReference(desiredSalaryInput, nameof(desiredSalaryInput), missing);
        RequireReference(notesInput, nameof(notesInput), missing);
        RequireReference(salaryPeriodDropdown, nameof(salaryPeriodDropdown), missing);
        RequireReference(commuteDropdown, nameof(commuteDropdown), missing);
        RequireReference(overtimeDropdown, nameof(overtimeDropdown), missing);
        RequireReference(travelDropdown, nameof(travelDropdown), missing);
        RequireReference(relocationDropdown, nameof(relocationDropdown), missing);
        RequireReference(targetImportanceDropdown, nameof(targetImportanceDropdown), missing);
        RequireReference(salaryImportanceDropdown, nameof(salaryImportanceDropdown), missing);
        RequireReference(arrangementImportanceDropdown, nameof(arrangementImportanceDropdown), missing);
        RequireReference(scheduleImportanceDropdown, nameof(scheduleImportanceDropdown), missing);
        ValidateTagEditor(targetRoles, nameof(targetRoles), missing);
        ValidateTagEditor(industries, nameof(industries), missing);
        ValidateTagEditor(regions, nameof(regions), missing);
        ValidateTagEditor(employmentTypes, nameof(employmentTypes), missing);
        ValidateTagEditor(workModes, nameof(workModes), missing);
        ValidateTagEditor(schedules, nameof(schedules), missing);
        missingReferences = string.Join("、", missing);
        return missing.Count == 0;
    }

    private static void RequireReference(UnityEngine.Object value, string name, List<string> missing)
    {
        if (value == null) missing.Add(name);
    }

    private static void ValidateTagEditor(TagEditor editor, string name, List<string> missing)
    {
        if (editor == null)
        {
            missing.Add(name);
            return;
        }

        if (editor.Root == null) missing.Add(name + ".Root");
        if (editor.Count == null) missing.Add(name + ".Count");
        if (editor.Limit <= 0) missing.Add(name + ".Limit");

        bool hasManualInput = editor.Input != null || editor.AddButton != null;
        if (editor.Input == null != (editor.AddButton == null))
            missing.Add(name + ".Input 與 " + name + ".AddButton 必須成對設定");
        if (!hasManualInput && editor.Dropdown == null)
            missing.Add(name + " 至少需要 Input/AddButton 或 Dropdown");
    }

    private void AddInputTag(TagEditor editor)
    {
        if (editor == null || editor.Input == null) return;
        string value = editor.Input.text.Trim();
        if (AddTag(editor, value)) editor.Input.text = string.Empty;
    }

    private void BindRuntimeEvents()
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveAllListeners();
            saveButton.onClick.AddListener(SavePreferences);
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(LoadPreferences);
        }
        BindTagEditor(targetRoles);
        BindTagEditor(industries);
        BindTagEditor(regions);
        BindTagEditor(employmentTypes);
        BindTagEditor(workModes);
        BindTagEditor(schedules);
    }

    private void BindTagEditor(TagEditor editor)
    {
        if (editor == null) return;
        if (editor.AddButton != null && editor.Input != null)
        {
            editor.AddButton.onClick.RemoveAllListeners();
            editor.AddButton.onClick.AddListener(() => AddInputTag(editor));
            editor.Input.onSubmit.RemoveAllListeners();
            editor.Input.onSubmit.AddListener(_ => AddInputTag(editor));
        }
        if (editor.Dropdown != null)
        {
            editor.Dropdown.onValueChanged.RemoveAllListeners();
            editor.Dropdown.onValueChanged.AddListener(index =>
            {
                if (index <= 0) return;
                AddTag(editor, editor.Dropdown.options[index].text);
                editor.Dropdown.SetValueWithoutNotify(0);
                editor.Dropdown.RefreshShownValue();
            });
        }
    }

    private bool AddTag(TagEditor editor, string value)
    {
        value = (value ?? string.Empty).Trim();
        if (value.Length == 0) return false;
        if (editor.Values.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
        {
            SetStatus("這個項目已經加入。", true);
            return false;
        }
        if (!string.IsNullOrEmpty(editor.ExclusiveValue))
        {
            if (string.Equals(value, editor.ExclusiveValue, StringComparison.Ordinal))
                editor.Values.Clear();
            else
                editor.Values.RemoveAll(item => string.Equals(
                    item, editor.ExclusiveValue, StringComparison.Ordinal));
        }
        if (editor.Values.Count >= editor.Limit)
        {
            SetStatus("已達 " + editor.Limit + " 個項目的上限。", true);
            return false;
        }
        editor.Values.Add(value);
        RenderTags(editor);
        SetStatus(string.Empty, false);
        return true;
    }

    private void RenderTags(TagEditor editor)
    {
        if (editor == null || editor.Root == null || editor.Count == null) return;
        foreach (Transform child in editor.Root.Cast<Transform>().ToArray())
            Destroy(child.gameObject);
        for (int index = 0; index < editor.Values.Count; index++)
        {
            string value = editor.Values[index];
            Button chip = CreateButton(editor.Root, "Tag_" + index, value + "  ×", false);
            LayoutElement layout = chip.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = Mathf.Clamp(54f + value.Length * 23f, 116f, 330f);
            layout.preferredHeight = 44f;
            chip.onClick.AddListener(() =>
            {
                editor.Values.Remove(value);
                RenderTags(editor);
            });
        }
        editor.Count.text = editor.Values.Count + " / " + editor.Limit;
        LayoutRebuilder.ForceRebuildLayoutImmediate(editor.Root as RectTransform);
        LayoutRebuilder.MarkLayoutForRebuild(editor.Root.parent as RectTransform);
    }

    private void LoadPreferences()
    {
        if (!built) return;
        PersistenceStorageResult<CareerProfile> result = CareerProfileRepository.Load(
            ResolveProjectRelativePath(personalDataRootPath));
        if (!result.IsSuccess || result.Value == null)
        {
            currentProfile = null;
            SetStatus("讀取求職條件失敗。", true);
            return;
        }
        currentProfile = result.Value;
        JobSearchPreferences value = currentProfile.JobPreferences ?? new JobSearchPreferences();
        SetTags(targetRoles, value.TargetRoles);
        SetTags(industries, value.Industries);
        SetTags(regions, value.AcceptedRegions);
        SetTags(employmentTypes, value.EmploymentTypes);
        SetTags(workModes, value.WorkModes);
        SetTags(schedules, value.WorkSchedules);
        salaryPeriodDropdown.SetValueWithoutNotify((int)value.SalaryPeriod);
        minimumSalaryInput.text = FormatNumber(value.MinimumSalary);
        desiredSalaryInput.text = FormatNumber(value.DesiredSalary);
        commuteDropdown.SetValueWithoutNotify(CommuteIndex(value.MaximumCommuteMinutes));
        SetDropdownByText(overtimeDropdown, value.OvertimePreference);
        SetDropdownByText(travelDropdown, value.TravelPreference);
        SetDropdownByText(relocationDropdown, value.RelocationPreference);
        notesInput.text = value.Notes ?? string.Empty;
        targetImportanceDropdown.SetValueWithoutNotify((int)value.TargetImportance);
        salaryImportanceDropdown.SetValueWithoutNotify((int)value.SalaryImportance);
        arrangementImportanceDropdown.SetValueWithoutNotify((int)value.ArrangementImportance);
        scheduleImportanceDropdown.SetValueWithoutNotify((int)value.ScheduleImportance);
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        SetStatus(value.UpdatedAt.HasValue
            ? "最後更新：" + value.UpdatedAt.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
            : "尚未儲存求職條件。", false);
    }

    private void SavePreferences()
    {
        if (currentProfile == null)
        {
            SetStatus("沒有可儲存的個人資料。", true);
            return;
        }
        if (!TryParseOptionalNumber(minimumSalaryInput.text, out int? minimum)
            || !TryParseOptionalNumber(desiredSalaryInput.text, out int? desired))
        {
            SetStatus("薪資請輸入 0 以上的整數，或留空。", true);
            return;
        }
        if (minimum.HasValue && desired.HasValue && desired.Value < minimum.Value)
        {
            SetStatus("期望薪資不可低於最低可接受薪資。", true);
            return;
        }
        DateTimeOffset now = DateTimeOffset.Now;
        currentProfile.JobPreferences = new JobSearchPreferences
        {
            TargetRoles = new List<string>(targetRoles.Values),
            Industries = new List<string>(industries.Values),
            AcceptedRegions = new List<string>(regions.Values),
            EmploymentTypes = new List<string>(employmentTypes.Values),
            WorkModes = new List<string>(workModes.Values),
            WorkSchedules = new List<string>(schedules.Values),
            SalaryPeriod = (SalaryPeriod)salaryPeriodDropdown.value,
            MinimumSalary = minimum,
            DesiredSalary = desired,
            MaximumCommuteMinutes = CommuteMinutes(commuteDropdown.value),
            OvertimePreference = DropdownValue(overtimeDropdown),
            TravelPreference = DropdownValue(travelDropdown),
            RelocationPreference = DropdownValue(relocationDropdown),
            Notes = notesInput.text.Trim(),
            TargetImportance = (PreferenceImportance)targetImportanceDropdown.value,
            SalaryImportance = (PreferenceImportance)salaryImportanceDropdown.value,
            ArrangementImportance = (PreferenceImportance)arrangementImportanceDropdown.value,
            ScheduleImportance = (PreferenceImportance)scheduleImportanceDropdown.value,
            UpdatedAt = now
        };
        currentProfile.UpdatedAt = now;
        PersistenceStorageResult<CareerProfile> result = CareerProfileRepository.Save(
            ResolveProjectRelativePath(personalDataRootPath), currentProfile);
        if (!result.IsSuccess)
        {
            SetStatus("儲存求職條件失敗。", true);
            return;
        }
        currentProfile = result.Value;
        SetStatus("求職條件已儲存。", false);
    }

    private Button CreateButton(Transform parent, string name, string label, bool primary)
    {
        GameObject root = CreateUiObject(name, parent, typeof(Image), typeof(Button));
        Image image = root.GetComponent<Image>();
        image.color = primary ? Primary : Surface;
        ApplySliced(image);
        if (!primary)
        {
            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);
        }
        Button button = root.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = primary ? Primary : Surface;
        colors.highlightedColor = primary ? PrimaryHover : SurfaceMuted;
        colors.pressedColor = primary ? PrimaryPressed : Border;
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        TMP_Text text = CreateText(root.transform, "Text", label, 20);
        text.color = primary ? TextOnPrimary : TextPrimary;
        Stretch(text.rectTransform, new Vector2(8f, 4f), new Vector2(-8f, -4f));
        return button;
    }

    private TMP_Text CreateText(Transform parent, string name, string value, int size,
        FontStyles style = FontStyles.Normal)
    {
        GameObject root = CreateUiObject(name, parent, typeof(TextMeshProUGUI));
        TMP_Text text = root.GetComponent<TMP_Text>();
        text.font = Font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = TextPrimary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = true;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    private void ApplySliced(Image image)
    {
        if (theme == null || theme.ButtonBackgroundSprite == null) return;
        image.sprite = theme.ButtonBackgroundSprite;
        image.type = Image.Type.Sliced;
    }

    private void SetStatus(string message, bool error)
    {
        if (statusText == null) return;
        statusText.text = message ?? string.Empty;
        statusText.color = error
            ? (theme != null ? theme.Danger : Color.red)
            : TextSecondary;
    }

    private void SetTags(TagEditor editor, IEnumerable<string> values)
    {
        editor.Values.Clear();
        editor.Values.AddRange((values ?? Enumerable.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(editor.Limit));
        RenderTags(editor);
    }

    private static string FormatNumber(int? value) => value.HasValue ? value.Value.ToString() : string.Empty;

    private static bool TryParseOptionalNumber(string text, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!int.TryParse(text.Trim(), out int parsed) || parsed < 0) return false;
        value = parsed;
        return true;
    }

    private static int CommuteIndex(int? minutes)
    {
        if (!minutes.HasValue) return 0;
        if (minutes.Value <= 15) return 1;
        if (minutes.Value <= 30) return 2;
        if (minutes.Value <= 45) return 3;
        if (minutes.Value <= 60) return 4;
        if (minutes.Value <= 90) return 5;
        return 6;
    }

    private static int? CommuteMinutes(int index)
    {
        int[] values = { 0, 15, 30, 45, 60, 90, int.MaxValue };
        return index <= 0 ? (int?)null : values[Mathf.Clamp(index, 1, values.Length - 1)];
    }

    private static string DropdownValue(TMP_Dropdown dropdown)
    {
        return dropdown == null || dropdown.value <= 0
            ? null : dropdown.options[dropdown.value].text;
    }

    private static void SetDropdownByText(TMP_Dropdown dropdown, string value)
    {
        int index = string.IsNullOrWhiteSpace(value) ? 0
            : dropdown.options.FindIndex(option => string.Equals(
                option.text, value, StringComparison.Ordinal));
        dropdown.SetValueWithoutNotify(Mathf.Max(0, index));
        dropdown.RefreshShownValue();
    }

    private static string ResolveProjectRelativePath(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }

    private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
    {
        var types = new List<Type> { typeof(RectTransform), typeof(CanvasRenderer) };
        types.AddRange(components);
        var result = new GameObject(name, types.Distinct().ToArray());
        result.layer = parent.gameObject.layer;
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect, Vector2 minOffset, Vector2 maxOffset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = minOffset;
        rect.offsetMax = maxOffset;
    }

    [Serializable]
    private sealed class TagEditor
    {
        public Transform Root;
        public TMP_Text Count;
        public TMP_InputField Input;
        public TMP_Dropdown Dropdown;
        public Button AddButton;
        public int Limit;
        public string ExclusiveValue;
        public readonly List<string> Values = new List<string>();
    }
}
