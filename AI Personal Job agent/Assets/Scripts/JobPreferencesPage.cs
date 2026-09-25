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
    private const int RoleLimit = 5;
    private const int IndustryLimit = 5;
    private const int RegionLimit = 8;
    private const int CommonTagLimit = 4;
    private const int ScheduleLimit = 3;

    private static readonly string[] Industries =
    {
        "資訊軟體業", "網際網路業", "電子商務", "遊戲產業", "數位內容",
        "金融科技", "半導體業", "教育服務", "顧問服務", "其他"
    };

    private static readonly string[] Regions =
    {
        "台北市", "新北市", "桃園市", "新竹市", "新竹縣", "台中市",
        "彰化縣", "嘉義市", "台南市", "高雄市", "基隆市", "宜蘭縣",
        "花蓮縣", "其他地區"
    };

    private static readonly string[] EmploymentTypes =
        { "正職", "兼職", "約聘／契約", "接案" };
    private static readonly string[] WorkModes =
        { "現場", "混合", "遠端" };
    private static readonly string[] WorkSchedules =
        { "一般日班", "晚班", "大夜班", "輪班", "假日／週末班", "彈性工時", "時段不限" };
    private static readonly string[] ImportanceLabels = { "必要", "偏好", "不在意" };

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

#if UNITY_EDITOR
    public void BuildEditorPreview(JobCheckUiTheme value)
    {
        theme = value;
        EnsureUi();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void OnEnable()
    {
        if (!HasSerializedUi())
        {
            Debug.LogError("求職條件頁缺少場景 UI 引用；請確認 SampleScene 內的 Page_JobPreferences 已完整序列化。", this);
            return;
        }

        if (!built)
        {
            built = true;
            BindRuntimeEvents();
        }

        if (UnityEngine.Application.isPlaying) LoadPreferences();
    }

    private bool HasSerializedUi()
    {
        return scroll != null &&
               statusText != null &&
               saveButton != null &&
               cancelButton != null &&
               targetRoles != null &&
               targetRoles.Root != null;
    }

    private void EnsureUi()
    {
        if (built) return;
        if (scroll != null && targetRoles != null && targetRoles.Root != null)
        {
            built = true;
            BindRuntimeEvents();
            return;
        }
        built = true;

        Image pageBackground = gameObject.GetComponent<Image>();
        if (pageBackground == null) pageBackground = gameObject.AddComponent<Image>();
        pageBackground.color = theme != null ? theme.AppBackground : new Color32(244, 241, 240, 255);
        pageBackground.raycastTarget = true;

        TMP_Text title = CreateText(transform, "Title", "求職條件", 46, FontStyles.Bold);
        AnchorTop(title.rectTransform, new Vector2(32f, -22f), new Vector2(-64f, 60f));
        TMP_Text helper = CreateText(transform, "Help",
            "設定你願意接受的工作條件，供職缺比對與未來 AI 建議使用。", 20);
        helper.color = TextSecondary;
        AnchorTop(helper.rectTransform, new Vector2(34f, -78f), new Vector2(-68f, 34f));

        GameObject viewportObject = CreateUiObject("ScrollView", transform,
            typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        Stretch(viewport, new Vector2(28f, 94f), new Vector2(-44f, -116f));
        viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

        GameObject contentObject = CreateUiObject("Content", viewportObject.transform,
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(6, 6, 6, 20);
        contentLayout.spacing = 16f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        ContentSizeFitter contentFitter = contentObject.GetComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 42f;
        CreateScrollbar(scroll, viewportObject.transform);

        CreateTargetSection(content);
        CreateSalarySection(content);
        CreateArrangementSection(content);
        CreateScheduleSection(content);
        CreateOtherSection(content);

        GameObject footer = CreateUiObject("Footer", transform, typeof(Image));
        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = Vector2.zero;
        footerRect.sizeDelta = new Vector2(0f, 88f);
        footer.GetComponent<Image>().color = Surface;
        statusText = CreateText(footer.transform, "Status", string.Empty, 19);
        Stretch(statusText.rectTransform, new Vector2(26f, 20f), new Vector2(-360f, -18f));
        saveButton = CreateButton(footer.transform, "Button_Save", "儲存", true);
        AnchorBottomRight(saveButton.GetComponent<RectTransform>(), new Vector2(-90f, 42f), new Vector2(140f, 52f));
        cancelButton = CreateButton(footer.transform, "Button_Cancel", "取消", false);
        AnchorBottomRight(cancelButton.GetComponent<RectTransform>(), new Vector2(-246f, 42f), new Vector2(140f, 52f));
        BindRuntimeEvents();
    }

    private void CreateTargetSection(Transform parent)
    {
        Transform card = CreateSection(parent, "Section_Target", "目標職務",
            "職務名稱可自行輸入；產業類別從清單選擇。輸入建議此版本暫不提供。",
            out targetImportanceDropdown);
        targetRoles = CreateInputTagEditor(card, "TargetRoles", "職務名稱",
            "輸入職務名稱後按 Enter 或新增", RoleLimit);
        industries = CreateDropdownTagEditor(card, "Industries", "產業類別",
            "選擇產業類別", Industries, IndustryLimit);
    }

    private void CreateSalarySection(Transform parent)
    {
        Transform card = CreateSection(parent, "Section_Salary", "薪資條件",
            "最低可接受薪資屬於硬條件；留空代表尚未設定。", out salaryImportanceDropdown);
        salaryPeriodDropdown = CreateLabeledDropdown(card, "SalaryPeriod", "計薪方式",
            new[] { "月薪", "年薪", "時薪" });
        minimumSalaryInput = CreateLabeledInput(card, "MinimumSalary", "最低可接受薪資",
            "例如：40000", true, 54f);
        desiredSalaryInput = CreateLabeledInput(card, "DesiredSalary", "期望薪資",
            "例如：60000", true, 54f);
    }

    private void CreateArrangementSection(Transform parent)
    {
        Transform card = CreateSection(parent, "Section_Arrangement", "工作方式",
            "選擇可接受的聘用方式、工作模式與地區。", out arrangementImportanceDropdown);
        employmentTypes = CreateDropdownTagEditor(card, "EmploymentTypes", "聘用類型",
            "選擇聘用類型", EmploymentTypes, CommonTagLimit);
        workModes = CreateDropdownTagEditor(card, "WorkModes", "工作模式",
            "選擇工作模式", WorkModes, CommonTagLimit);
        regions = CreateDropdownTagEditor(card, "Regions", "接受地區",
            "選擇接受地區", Regions, RegionLimit);
        commuteDropdown = CreateLabeledDropdown(card, "Commute", "最長通勤時間",
            new[] { "未設定", "15 分鐘以內", "30 分鐘以內", "45 分鐘以內",
                "60 分鐘以內", "90 分鐘以內", "不限" });
    }

    private void CreateScheduleSection(Transform parent)
    {
        Transform card = CreateSection(parent, "Section_Schedule", "工時與限制",
            "上班時段最多選擇 3 種；「時段不限」會取代其他時段。", out scheduleImportanceDropdown);
        schedules = CreateDropdownTagEditor(card, "Schedules", "可接受上班時段",
            "選擇上班時段", WorkSchedules, ScheduleLimit, "時段不限");
        overtimeDropdown = CreateLabeledDropdown(card, "Overtime", "加班接受程度",
            new[] { "未設定", "不接受", "偶爾可以", "可以", "不限制" });
        travelDropdown = CreateLabeledDropdown(card, "Travel", "出差",
            new[] { "未設定", "不接受", "偶爾可以", "可以" });
        relocationDropdown = CreateLabeledDropdown(card, "Relocation", "是否接受搬遷",
            new[] { "未設定", "不接受", "可以討論", "接受" });
    }

    private void CreateOtherSection(Transform parent)
    {
        Transform card = CreateSection(parent, "Section_Other", "其他條件",
            "填寫無法用上述欄位表達的條件。", out TMP_Dropdown unused, false);
        notesInput = CreateLabeledInput(card, "Notes", "補充說明（選填）",
            "例如：希望有完整新人引導、重視學習機會。", false, 120f);
    }

    private Transform CreateSection(Transform parent, string name, string title, string help,
        out TMP_Dropdown importance, bool includeImportance = true)
    {
        GameObject card = CreateUiObject(name, parent, typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(Outline));
        card.GetComponent<Image>().color = SurfaceMuted;
        ApplySliced(card.GetComponent<Image>());
        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(1f, -1f);
        VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 18, 20);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        card.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        CreateLayoutText(card.transform, "Title", title, 28, 38f, FontStyles.Bold);
        TMP_Text helper = CreateLayoutText(card.transform, "Help", help, 19, 30f);
        helper.color = TextSecondary;
        importance = includeImportance
            ? CreateLabeledDropdown(card.transform, "Importance", "重要程度", ImportanceLabels)
            : null;
        return card.transform;
    }

    private TagEditor CreateInputTagEditor(Transform parent, string name, string label,
        string placeholder, int limit)
    {
        CreateLayoutText(parent, "Label_" + name, label, 20, 30f, FontStyles.Bold);
        GameObject row = CreateUiObject("InputRow_" + name, parent, typeof(HorizontalLayoutGroup));
        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;
        row.AddComponent<LayoutElement>().preferredHeight = 54f;
        TMP_InputField input = CreateInput(row.transform, "Input_" + name, placeholder, true, 54f);
        LayoutElement inputLayout = input.gameObject.AddComponent<LayoutElement>();
        inputLayout.flexibleWidth = 1f;
        inputLayout.minWidth = 220f;
        Button add = CreateLayoutButton(row.transform, "Button_Add" + name, "新增", 110f, true);
        TagEditor editor = CreateTagArea(parent, name, limit);
        editor.Input = input;
        editor.AddButton = add;
        return editor;
    }

    private TagEditor CreateDropdownTagEditor(Transform parent, string name, string label,
        string placeholder, IEnumerable<string> options, int limit, string exclusive = null)
    {
        CreateLayoutText(parent, "Label_" + name, label, 20, 30f, FontStyles.Bold);
        TMP_Dropdown dropdown = CreateDropdown(parent, "Dropdown_" + name,
            new[] { placeholder }.Concat(options).ToArray());
        TagEditor editor = CreateTagArea(parent, name, limit);
        editor.Dropdown = dropdown;
        editor.ExclusiveValue = exclusive;
        return editor;
    }

    private TagEditor CreateTagArea(Transform parent, string name, int limit)
    {
        GameObject area = CreateUiObject("Tags_" + name, parent, typeof(PreferenceTagFlowLayout));
        PreferenceTagFlowLayout flow = area.GetComponent<PreferenceTagFlowLayout>();
        flow.Padding = new RectOffset(0, 0, 2, 2);
        flow.HorizontalSpacing = 8f;
        flow.VerticalSpacing = 8f;
        flow.RowHeight = 44f;
        TMP_Text count = CreateLayoutText(parent, "Count_" + name, "0 / " + limit, 18, 24f);
        count.alignment = TextAlignmentOptions.MidlineRight;
        count.color = TextSecondary;
        return new TagEditor { Root = area.transform, Count = count, Limit = limit };
    }

    private void AddInputTag(TagEditor editor)
    {
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

    private TMP_Dropdown CreateLabeledDropdown(Transform parent, string name, string label,
        string[] options)
    {
        CreateLayoutText(parent, "Label_" + name, label, 20, 30f, FontStyles.Bold);
        return CreateDropdown(parent, "Dropdown_" + name, options);
    }

    private TMP_Dropdown CreateDropdown(Transform parent, string name, string[] options)
    {
        GameObject root = CreateUiObject(name, parent, typeof(Image), typeof(JobCheckPopupDropdown));
        root.GetComponent<Image>().color = Surface;
        ApplySliced(root.GetComponent<Image>());
        root.AddComponent<LayoutElement>().preferredHeight = 54f;
        TMP_Text caption = CreateText(root.transform, "Caption", string.Empty, 20);
        Stretch(caption.rectTransform, new Vector2(12f, 4f), new Vector2(-42f, -4f));
        TMP_Text arrow = CreateText(root.transform, "Arrow", "▼", 17);
        AnchorMiddleRight(arrow.rectTransform, new Vector2(-22f, 0f), new Vector2(32f, 42f));

        GameObject template = CreateUiObject("Template", root.transform,
            typeof(Image), typeof(ScrollRect));
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.sizeDelta = new Vector2(0f, 270f);
        template.GetComponent<Image>().color = Surface;
        GameObject viewport = CreateUiObject("Viewport", template.transform,
            typeof(Image), typeof(RectMask2D));
        Stretch(viewport.GetComponent<RectTransform>(), new Vector2(3f, 3f), new Vector2(-3f, -3f));
        viewport.GetComponent<Image>().color = Surface;
        GameObject content = CreateUiObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 48f);
        GameObject item = CreateUiObject("Item", content.transform, typeof(Image), typeof(Toggle));
        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 1f);
        itemRect.anchorMax = new Vector2(1f, 1f);
        itemRect.pivot = new Vector2(0.5f, 1f);
        itemRect.sizeDelta = new Vector2(0f, 48f);
        item.GetComponent<Toggle>().targetGraphic = item.GetComponent<Image>();
        TMP_Text itemText = CreateText(item.transform, "ItemText", string.Empty, 20);
        Stretch(itemText.rectTransform, new Vector2(12f, 3f), new Vector2(-12f, -3f));
        ScrollRect templateScroll = template.GetComponent<ScrollRect>();
        templateScroll.viewport = viewport.GetComponent<RectTransform>();
        templateScroll.content = contentRect;
        templateScroll.horizontal = false;
        templateScroll.vertical = true;

        TMP_Dropdown dropdown = root.GetComponent<TMP_Dropdown>();
        dropdown.targetGraphic = root.GetComponent<Image>();
        dropdown.captionText = caption;
        dropdown.itemText = itemText;
        dropdown.template = templateRect;
        dropdown.AddOptions(options.ToList());
        dropdown.RefreshShownValue();
        template.SetActive(false);
        templateRect.SetParent(transform, false);
        templateRect.anchorMin = new Vector2(0.5f, 0.5f);
        templateRect.anchorMax = new Vector2(0.5f, 0.5f);
        templateRect.pivot = new Vector2(0.5f, 0f);
        return dropdown;
    }

    private TMP_InputField CreateLabeledInput(Transform parent, string name, string label,
        string placeholder, bool singleLine, float height)
    {
        CreateLayoutText(parent, "Label_" + name, label, 20, 30f, FontStyles.Bold);
        TMP_InputField input = CreateInput(parent, "Input_" + name, placeholder, singleLine, height);
        input.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return input;
    }

    private TMP_InputField CreateInput(Transform parent, string name, string placeholder,
        bool singleLine, float height)
    {
        GameObject root = CreateUiObject(name, parent, typeof(Image), typeof(TMP_InputField),
            typeof(RectMask2D));
        root.GetComponent<Image>().color = Surface;
        ApplySliced(root.GetComponent<Image>());
        TMP_Text placeholderText = CreateText(root.transform, "Placeholder", placeholder, 19);
        placeholderText.color = TextSecondary;
        Stretch(placeholderText.rectTransform, new Vector2(12f, 8f), new Vector2(-12f, -8f));
        TMP_Text valueText = CreateText(root.transform, "Text", string.Empty, 21);
        Stretch(valueText.rectTransform, new Vector2(12f, 8f), new Vector2(-12f, -8f));
        TMP_InputField input = root.GetComponent<TMP_InputField>();
        input.textViewport = root.GetComponent<RectTransform>();
        input.textComponent = valueText;
        input.placeholder = placeholderText;
        input.lineType = singleLine
            ? TMP_InputField.LineType.SingleLine : TMP_InputField.LineType.MultiLineNewline;
        return input;
    }

    private void CreateScrollbar(ScrollRect owner, Transform parent)
    {
        GameObject bar = CreateUiObject("Scrollbar Vertical", parent, typeof(Image), typeof(Scrollbar));
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.anchoredPosition = new Vector2(16f, 0f);
        barRect.sizeDelta = new Vector2(12f, 0f);
        bar.GetComponent<Image>().color = SurfaceMuted;
        GameObject handle = CreateUiObject("Handle", bar.transform, typeof(Image));
        Stretch(handle.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        handle.GetComponent<Image>().color = Primary;
        Scrollbar scrollbar = bar.GetComponent<Scrollbar>();
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        owner.verticalScrollbar = scrollbar;
        owner.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private Button CreateLayoutButton(Transform parent, string name, string label,
        float width, bool primary)
    {
        Button button = CreateButton(parent, name, label, primary);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = width;
        layout.preferredHeight = 52f;
        return button;
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

    private TMP_Text CreateLayoutText(Transform parent, string name, string value, int size,
        float height, FontStyles style = FontStyles.Normal)
    {
        TMP_Text text = CreateText(parent, name, value, size, style);
        text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return text;
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

    private static void SetTags(TagEditor editor, IEnumerable<string> values)
    {
        editor.Values.Clear();
        editor.Values.AddRange((values ?? Enumerable.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(editor.Limit));
        JobPreferencesPage page = editor.Root.GetComponentInParent<JobPreferencesPage>();
        page.RenderTags(editor);
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

    private static void AnchorTop(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void AnchorBottomRight(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void AnchorMiddleRight(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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

/// <summary>依可用寬度自動換行的標籤布局，並將所需高度回報給外層 ScrollView。</summary>
public sealed class PreferenceTagFlowLayout : LayoutGroup
{
    public RectOffset Padding { get => padding; set => padding = value; }
    public float HorizontalSpacing { get; set; } = 8f;
    public float VerticalSpacing { get; set; } = 8f;
    public float RowHeight { get; set; } = 44f;

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        SetLayoutInputForAxis(padding.horizontal, padding.horizontal, -1f, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        SetLayoutInputForAxis(CalculateRequiredHeight(), CalculateRequiredHeight(), -1f, 1);
    }

    public override void SetLayoutHorizontal() => Arrange();
    public override void SetLayoutVertical() => Arrange();

    private float CalculateRequiredHeight()
    {
        if (rectChildren.Count == 0) return 4f;
        float available = Mathf.Max(1f, rectTransform.rect.width - padding.horizontal);
        float used = 0f;
        int rows = 1;
        foreach (RectTransform child in rectChildren)
        {
            float width = Mathf.Min(available, LayoutUtility.GetPreferredWidth(child));
            if (used > 0f && used + HorizontalSpacing + width > available)
            {
                rows++;
                used = 0f;
            }
            used += (used > 0f ? HorizontalSpacing : 0f) + width;
        }
        return padding.vertical + rows * RowHeight + (rows - 1) * VerticalSpacing;
    }

    private void Arrange()
    {
        float available = Mathf.Max(1f, rectTransform.rect.width - padding.horizontal);
        float x = padding.left;
        float y = padding.top;
        foreach (RectTransform child in rectChildren)
        {
            float width = Mathf.Min(available, LayoutUtility.GetPreferredWidth(child));
            if (x > padding.left && x + width > padding.left + available)
            {
                x = padding.left;
                y += RowHeight + VerticalSpacing;
            }
            SetChildAlongAxis(child, 0, x, width);
            SetChildAlongAxis(child, 1, y, RowHeight);
            x += width + HorizontalSpacing;
        }
    }
}
