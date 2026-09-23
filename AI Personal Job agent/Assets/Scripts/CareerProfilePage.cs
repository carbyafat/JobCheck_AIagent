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

/// <summary>個人履歷母資料頁；負責載入資料並呈現，不介入職缺頁流程。</summary>
public sealed class CareerProfilePage : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private string personalDataRootPath = "../personal_data";

    [Header("Display")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private TMP_Text displayText;

    [Header("Theme")]
    [SerializeField] private JobCheckUiTheme theme;

    public CareerProfile CurrentProfile { get; private set; }

    private Button editButton;
    private Button transferButton;
    private GameObject editorRoot;
    private GameObject transferRoot;
    private ScrollRect editorScroll;
    private ScrollRect transferScroll;
    private TMP_Text editorStatus;
    private TMP_Text transferMessage;
    private Button importButton;
    private Button replaceButton;
    private string previewedPackagePath;
    private readonly Dictionary<string, TMP_Text> displayFields =
        new Dictionary<string, TMP_Text>(StringComparer.Ordinal);
    private readonly Dictionary<string, TMP_InputField> editorFields =
        new Dictionary<string, TMP_InputField>(StringComparer.Ordinal);
    private List<CareerSkill> skillDraft;
    private Transform skillListRoot;
    private GameObject skillFormRoot;
    private TMP_InputField skillNameInput;
    private TMP_InputField skillNotesInput;
    private TMP_Dropdown skillYearsDropdown;
    private TMP_Dropdown skillMonthsDropdown;
    private int editingSkillIndex = -1;

    private const string ProfileUiCharacters =
        "個人履歷最後更新自我介紹連結技能工作經歷專案學歷語言能力尚未填寫新增讀取失敗未知錯誤編輯儲存取消至今年月日時分公司職務開始結束內容名稱技術網址說明機構項目備註程度母語首頁職缺側欄寬度搬遷匯出匯入選擇檔案取代本機關閉預覽成功檔案尚未加密請妥善保管等級學位已畢業在學未完成科系標籤月數逗號分隔請輸入整數使用時間年自訂移除這筆重複請先填寫選填儲存技能暫不評級無技能";

    private Color AppBackground => theme != null
        ? theme.AppBackground
        : new Color(0.94f, 0.94f, 0.94f, 1f);
    private Color Surface => theme != null ? theme.Surface : Color.white;
    private Color SurfaceMuted => theme != null
        ? theme.SurfaceMuted
        : new Color(0.92f, 0.9f, 0.9f, 1f);
    private Color Border => theme != null
        ? theme.Border
        : new Color(0.82f, 0.8f, 0.8f, 1f);
    private Color Primary => theme != null
        ? theme.Primary
        : new Color(0.55f, 0.32f, 0.34f, 1f);
    private Color PrimaryHover => theme != null ? theme.PrimaryHover : Primary * 1.08f;
    private Color PrimaryPressed => theme != null ? theme.PrimaryPressed : Primary * 0.82f;
    private Color Danger => theme != null
        ? theme.Danger
        : new Color(0.66f, 0.27f, 0.26f, 1f);
    private Color TextPrimary => theme != null
        ? theme.TextPrimary
        : new Color(0.18f, 0.18f, 0.18f, 1f);
    private Color TextSecondary => theme != null
        ? theme.TextSecondary
        : new Color(0.4f, 0.4f, 0.4f, 1f);
    private Color TextOnPrimary => theme != null ? theme.TextOnPrimary : Color.white;

    private enum ButtonTone
    {
        Primary,
        Secondary,
        Danger
    }

    private void OnEnable()
    {
        EnsureDisplayUi();
        EnsureEditorUi();
        EnsureTransferUi();
        Refresh();
    }

    public void Refresh()
    {
        PersistenceStorageResult<CareerProfile> result =
            CareerProfileRepository.Load(ResolveProjectRelativePath(personalDataRootPath));
        if (!result.IsSuccess)
        {
            CurrentProfile = null;
            ShowDisplayError("讀取失敗\n" + FormatIssues(result.Issues));
            return;
        }

        CurrentProfile = result.Value;
        PrepareGlyphs(CurrentProfile);
        RenderProfile(CurrentProfile);
    }

    public void OpenEditor()
    {
        EnsureEditorUi();
        if (CurrentProfile == null)
        {
            Refresh();
        }

        if (CurrentProfile == null)
        {
            SetEditorStatus("目前資料無法編輯，請先排除讀取錯誤。", true);
            return;
        }

        editorFields["summary"].text = CurrentProfile.Summary ?? string.Empty;
        skillDraft = CloneSkills(CurrentProfile.Skills);
        RenderSkillDraft();
        CloseSkillForm();
        SetEditorStatus("", false);
        if (editorScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            editorScroll.verticalNormalizedPosition = 1f;
        }
        displayText.gameObject.SetActive(false);
        editButton.gameObject.SetActive(false);
        if (transferButton != null) transferButton.gameObject.SetActive(false);
        editorRoot.SetActive(true);
    }

    public void CancelEditor()
    {
        skillDraft = null;
        CloseSkillForm();
        if (editorRoot != null)
        {
            editorRoot.SetActive(false);
        }

        if (displayText != null)
        {
            displayText.gameObject.SetActive(true);
        }

        if (editButton != null)
        {
            editButton.gameObject.SetActive(true);
        }

        if (transferButton != null)
        {
            transferButton.gameObject.SetActive(true);
        }
    }

    public void SaveEditor()
    {
        if (CurrentProfile == null)
        {
            SetEditorStatus("沒有可儲存的履歷資料。", true);
            return;
        }

        if (skillFormRoot != null && skillFormRoot.activeSelf && !ApplySkillForm())
            return;

        DateTimeOffset now = DateTimeOffset.Now;
        CareerProfile candidate = CreateSummaryEditCandidate(
            CurrentProfile, editorFields["summary"].text, skillDraft, now);

        PersistenceStorageResult<CareerProfile> result = CareerProfileRepository.Save(
            ResolveProjectRelativePath(personalDataRootPath),
            candidate);
        if (!result.IsSuccess)
        {
            SetEditorStatus("儲存失敗：" + FormatIssues(result.Issues), true);
            return;
        }

        CurrentProfile = result.Value;
        PrepareGlyphs(CurrentProfile);
        RenderProfile(CurrentProfile);
        CancelEditor();
    }

    private static CareerProfile CreateSummaryEditCandidate(
        CareerProfile current, string summary, IList<CareerSkill> skills, DateTimeOffset now)
    {
        return new CareerProfile
        {
            Id = current.Id,
            SchemaVersion = current.SchemaVersion,
            Summary = summary,
            CreatedAt = current.CreatedAt ?? now,
            UpdatedAt = now,
            Links = current.Links,
            Skills = skills == null ? current.Skills : new List<CareerSkill>(skills),
            Experiences = current.Experiences,
            Projects = current.Projects,
            Educations = current.Educations,
            Languages = current.Languages
        };
    }

    private static List<CareerSkill> CloneSkills(IEnumerable<CareerSkill> source)
    {
        return (source ?? Enumerable.Empty<CareerSkill>())
            .Where(item => item != null)
            .Select(item => new CareerSkill
            {
                Id = item.Id,
                Name = item.Name,
                Notes = item.Notes,
                SkillId = item.SkillId,
                Level = item.Level, // 舊資料保留，但目前不顯示、不用於比對。
                ClaimedMonths = item.ClaimedMonths
            })
            .ToList();
    }

    private static string FormatSkillDuration(int? months)
    {
        if (!months.HasValue) return null;
        int years = months.Value / 12;
        int remainder = months.Value % 12;
        if (years == 0) return "使用 " + remainder + " 個月";
        return remainder == 0
            ? "使用 " + years + " 年"
            : "使用 " + years + " 年 " + remainder + " 個月";
    }

    private void RenderSkillDraft()
    {
        if (skillListRoot == null) return;
        foreach (Transform child in skillListRoot.Cast<Transform>().ToArray())
        {
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        TMP_FontAsset font = ResolveFontAsset();
        if (skillDraft == null || skillDraft.Count == 0)
        {
            TMP_Text empty = CreateText(skillListRoot, "Empty", "尚未新增技能", font, 20,
                new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 36f),
                TextAlignmentOptions.MidlineLeft);
            empty.color = TextSecondary;
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            return;
        }

        for (int index = 0; index < skillDraft.Count; index++)
        {
            int skillIndex = index;
            CareerSkill skill = skillDraft[index];
            GameObject row = CreateUiObject("Skill_" + index, skillListRoot,
                typeof(Image), typeof(HorizontalLayoutGroup));
            row.GetComponent<Image>().color = Surface;
            ApplySlicedSprite(row.GetComponent<Image>());
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 12, 8, 8);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            row.AddComponent<LayoutElement>().preferredHeight = 68f;

            TMP_Text label = CreateText(row.transform, "Summary", Join("｜",
                skill.Name, FormatSkillDuration(skill.ClaimedMonths), skill.Notes), font, 21,
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 52f),
                TextAlignmentOptions.MidlineLeft);
            label.color = TextPrimary;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.minWidth = 120f;
            CreateLayoutButton(row.transform, "Button_Edit", "編輯", font, 100f,
                ButtonTone.Secondary).onClick.AddListener(() => OpenSkillForm(skillIndex));
            CreateLayoutButton(row.transform, "Button_Remove", "移除", font, 100f,
                ButtonTone.Secondary).onClick.AddListener(() => RemoveSkill(skillIndex));
        }
    }

    private void OpenSkillForm(int index)
    {
        editingSkillIndex = index;
        CareerSkill item = index >= 0 && skillDraft != null && index < skillDraft.Count
            ? skillDraft[index] : null;
        skillNameInput.text = item?.Name ?? string.Empty;
        skillNotesInput.text = item?.Notes ?? string.Empty;
        int? months = item?.ClaimedMonths;
        if (months.HasValue) EnsureSkillYearOption(months.Value / 12);
        skillYearsDropdown.SetValueWithoutNotify(months.HasValue
            ? months.Value / 12 + 1 : 0);
        skillMonthsDropdown.SetValueWithoutNotify(months.HasValue
            ? Mathf.Clamp(months.Value % 12, 0, 11) : 0);
        skillMonthsDropdown.interactable = months.HasValue;
        skillFormRoot.SetActive(true);
        SetEditorStatus("", false);
    }

    private void EnsureSkillYearOption(int years)
    {
        while (skillYearsDropdown.options.Count <= years + 1)
        {
            int nextYear = skillYearsDropdown.options.Count - 1;
            skillYearsDropdown.options.Add(new TMP_Dropdown.OptionData(nextYear + " 年"));
        }
    }

    private void CloseSkillForm()
    {
        editingSkillIndex = -1;
        if (skillFormRoot != null) skillFormRoot.SetActive(false);
    }

    private void RemoveSkill(int index)
    {
        if (skillDraft == null || index < 0 || index >= skillDraft.Count) return;
        skillDraft.RemoveAt(index);
        CloseSkillForm();
        RenderSkillDraft();
        SetEditorStatus("技能已從本次編輯移除；按整頁儲存才會寫入。", false);
    }

    private bool ApplySkillForm()
    {
        string name = skillNameInput.text.Trim();
        string skillId = RequirementCatalog.NormalizeSkill(name);
        if (skillId.Length == 0)
        {
            SetEditorStatus("請先填寫技能名稱。", true);
            return false;
        }

        if (skillDraft.Where((item, index) => index != editingSkillIndex)
            .Any(item => RequirementCatalog.AreSameSkill(item.Name, name)))
        {
            SetEditorStatus("這項技能已存在，請編輯原有項目。", true);
            return false;
        }

        int? months = skillYearsDropdown.value == 0
            ? (int?)null
            : (skillYearsDropdown.value - 1) * 12 + skillMonthsDropdown.value;
        CareerSkill existing = editingSkillIndex >= 0 && editingSkillIndex < skillDraft.Count
            ? skillDraft[editingSkillIndex] : null;
        CareerSkill saved = new CareerSkill
        {
            Id = existing?.Id ?? CareerProfileIdGenerator.CreateSkillId(),
            Name = name,
            SkillId = existing != null
                && string.Equals(existing.Name, name, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(existing.SkillId)
                    ? existing.SkillId : skillId,
            Notes = skillNotesInput.text.Trim(),
            ClaimedMonths = months,
            Level = existing?.Level // 只保留舊資料，不以隱藏欄位評分。
        };
        if (existing == null) skillDraft.Add(saved);
        else skillDraft[editingSkillIndex] = saved;
        CloseSkillForm();
        RenderSkillDraft();
        SetEditorStatus("技能已暫存；按整頁儲存才會寫入。", false);
        return true;
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
            item => Join("｜", item.Name, FormatSkillDuration(item.ClaimedMonths), item.Notes),
            "尚未新增技能");
        AppendListSection(builder, "工作經歷", profile.Experiences,
            item => Join("｜",
                Join(" / ", item.Organization, item.Role),
                FormatPeriod(item.StartDate, item.EndDate, item.IsCurrent),
                item.Description,
                item.SkillIds == null || item.SkillIds.Count == 0
                    ? null : "技能 " + string.Join("、", item.SkillIds)),
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
                item.Notes, DegreeLabel(item.DegreeLevel),
                CompletionLabel(item.CompletionStatus),
                item.FieldTags == null || item.FieldTags.Count == 0
                    ? null : string.Join("、", item.FieldTags)),
            "尚未新增學歷");
        AppendListSection(builder, "語言能力", profile.Languages,
            item => Join("｜", item.Name, item.Level, item.Notes,
                item.Proficiency.HasValue ? "等級 " + (int)item.Proficiency.Value : null),
            "尚未新增語言能力");
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

    private static string DegreeLabel(DegreeLevel? level)
    {
        switch (level)
        {
            case DegreeLevel.HighSchool: return "高中";
            case DegreeLevel.Associate: return "專科";
            case DegreeLevel.Bachelor: return "學士";
            case DegreeLevel.Master: return "碩士";
            case DegreeLevel.Doctorate: return "博士";
            default: return string.Empty;
        }
    }

    private static string CompletionLabel(EducationCompletionStatus status)
    {
        switch (status)
        {
            case EducationCompletionStatus.Completed: return "已畢業";
            case EducationCompletionStatus.InProgress: return "在學";
            case EducationCompletionStatus.Incomplete: return "未完成";
            default: return string.Empty;
        }
    }

    private static string FormatIssues(IReadOnlyList<PersistenceStorageIssue> issues)
    {
        return issues == null || issues.Count == 0
            ? "未知錯誤"
            : string.Join("\n", issues.Select(item => item.Message));
    }

    private void RenderProfile(CareerProfile profile)
    {
        EnsureDisplayUi();
        if (profile == null || displayFields.Count == 0)
        {
            ShowDisplayError("尚未建立履歷資料。");
            return;
        }

        displayFields["title"].text = "個人履歷";
        displayFields["updated"].text = profile.UpdatedAt.HasValue
            ? "最後更新：" + profile.UpdatedAt.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
            : "最後更新：—";
        displayFields["summary"].text = ValueOrEmpty(profile.Summary, "尚未填寫自我介紹");
        displayFields["links"].text = ListDisplay(profile.Links,
            item => Join("｜", item.Label, item.Url), "尚未新增連結");
        displayFields["skills"].text = ListDisplay(profile.Skills,
            item => Join("｜", item.Name, FormatSkillDuration(item.ClaimedMonths), item.Notes),
            "尚未新增技能");
        displayFields["experiences"].text = ListDisplay(profile.Experiences,
            item => Join("｜", Join(" / ", item.Organization, item.Role),
                FormatPeriod(item.StartDate, item.EndDate, item.IsCurrent), item.Description,
                item.SkillIds == null || item.SkillIds.Count == 0
                    ? null : "技能 " + string.Join("、", item.SkillIds)),
            "尚未新增工作經歷");
        displayFields["projects"].text = ListDisplay(profile.Projects,
            item => Join("｜", item.Name,
                item.Technologies == null ? null : string.Join("、", item.Technologies),
                item.Url, item.Description), "尚未新增專案經歷");
        displayFields["educations"].text = ListDisplay(profile.Educations,
            item => Join("｜", Join(" / ", item.Institution, item.Program),
                FormatPeriod(item.StartDate, item.EndDate, false), item.Notes,
                DegreeLabel(item.DegreeLevel), CompletionLabel(item.CompletionStatus),
                item.FieldTags == null || item.FieldTags.Count == 0
                    ? null : string.Join("、", item.FieldTags)),
            "尚未新增學歷");
        displayFields["languages"].text = ListDisplay(profile.Languages,
            item => Join("｜", item.Name, item.Level, item.Notes,
                item.Proficiency.HasValue ? "等級 " + (int)item.Proficiency.Value : null),
            "尚未新增語言能力");
    }

    private void ShowDisplayError(string message)
    {
        EnsureDisplayUi();
        if (displayFields.Count == 0)
        {
            return;
        }

        displayFields["title"].text = "個人履歷";
        displayFields["updated"].text = string.Empty;
        foreach (string key in new[]
                 { "summary", "links", "skills", "experiences", "projects", "educations", "languages" })
        {
            displayFields[key].text = key == "summary" ? message : string.Empty;
        }
    }

    private static string ValueOrEmpty(string value, string empty)
    {
        return string.IsNullOrWhiteSpace(value) ? empty : value.Trim();
    }

    private static string ListDisplay<T>(
        IEnumerable<T> items,
        Func<T, string> format,
        string empty)
        where T : class
    {
        string[] rows = items == null
            ? Array.Empty<string>()
            : items.Where(item => item != null)
                .Select(format)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        return rows.Length == 0 ? empty : string.Join("\n", rows.Select(row => "• " + row));
    }

    private void PrepareGlyphs(CareerProfile profile)
    {
        TMP_FontAsset font = ResolveFontAsset();
        if (font == null)
        {
            return;
        }

        string source = ProfileUiCharacters + (profile == null ? string.Empty : FormatProfile(profile));
        if (!font.TryAddCharacters(source, out string missingCharacters)
            && !string.IsNullOrEmpty(missingCharacters))
        {
            Debug.LogWarning("Yozai-Light SDF 缺少下列字元：" + missingCharacters);
        }
    }

    private TMP_FontAsset ResolveFontAsset()
    {
        if (theme != null && theme.BodyFont != null)
        {
            return theme.BodyFont;
        }

        return fontAsset != null ? fontAsset : displayText?.font;
    }

    private void EnsureDisplayUi()
    {
        if (displayFields.Count > 0 || displayText == null)
        {
            return;
        }

        TMP_FontAsset font = ResolveFontAsset();
        if (font == null)
        {
            Debug.LogError("CareerProfilePage 缺少 TMP 字型資產。");
            return;
        }

        PrepareGlyphs(null);
        displayText.enabled = false;
        RectTransform panel = displayText.rectTransform;
        Image pageBackground = displayText.GetComponent<Image>();
        if (pageBackground != null)
        {
            pageBackground.color = AppBackground;
        }

        float pageTitleSize = theme != null ? theme.PageTitleSize : 48f;
        float supportingSize = theme != null ? theme.SupportingTextSize : 20f;
        displayFields.Add("title", CreateText(panel, "Title", "個人履歷", font,
            Mathf.RoundToInt(pageTitleSize), new Vector2(0f, 1f),
            new Vector2(0f, -4f), new Vector2(560f, 62f),
            TextAlignmentOptions.MidlineLeft));
        displayFields["title"].fontStyle = FontStyles.Bold;
        displayFields.Add("updated", CreateText(panel, "UpdatedAt", "最後更新：—", font,
            Mathf.RoundToInt(supportingSize), new Vector2(0f, 1f),
            new Vector2(0f, -64f), new Vector2(620f, 34f),
            TextAlignmentOptions.MidlineLeft));
        displayFields["updated"].color = TextSecondary;

        GameObject scrollObject = CreateUiObject(
            "DisplayScroll",
            panel,
            typeof(Image),
            typeof(RectMask2D),
            typeof(ScrollRect));
        RectTransform viewport = scrollObject.GetComponent<RectTransform>();
        Stretch(viewport, new Vector2(0f, 0f), new Vector2(0f, -116f));
        scrollObject.GetComponent<Image>().color = AppBackground;

        GameObject contentObject = CreateUiObject(
            "Content",
            viewport,
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        int contentPadding = Mathf.RoundToInt(theme != null ? theme.SpaceXs : 8f);
        layout.padding = new RectOffset(contentPadding, contentPadding, contentPadding, 24);
        layout.spacing = theme != null ? theme.SpaceMd : 16f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;

        CreateDisplayCard("summary", "自我介紹", font, content, 84f);
        CreateDisplayCard("skills", "技能", font, content, 72f);
        CreateDisplayCard("links", "連結", font, content, 64f);
        CreateDisplayCard("languages", "語言能力", font, content, 64f);
        CreateDisplayCard("experiences", "工作經歷", font, content, 118f);
        CreateDisplayCard("projects", "專案經歷", font, content, 104f);
        CreateDisplayCard("educations", "學歷", font, content, 82f);
    }

    private void CreateDisplayCard(
        string key,
        string title,
        TMP_FontAsset font,
        Transform parent,
        float minimumValueHeight)
    {
        GameObject cardObject = CreateUiObject(
            "Card_" + key,
            parent,
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter),
            typeof(Outline));
        Image card = cardObject.GetComponent<Image>();
        card.color = Surface;
        ApplySlicedSprite(card);
        Outline outline = cardObject.GetComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        VerticalLayoutGroup cardLayout = cardObject.GetComponent<VerticalLayoutGroup>();
        int horizontalPadding = Mathf.RoundToInt(theme != null ? theme.SpaceLg : 24f);
        int verticalPadding = Mathf.RoundToInt(theme != null ? theme.SpaceMd : 16f);
        cardLayout.padding = new RectOffset(
            horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
        cardLayout.spacing = theme != null ? theme.SpaceSm : 12f;
        cardLayout.childAlignment = TextAnchor.UpperLeft;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;
        ContentSizeFitter cardFitter = cardObject.GetComponent<ContentSizeFitter>();
        cardFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        cardFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text heading = CreateText(cardObject.transform, "Title_" + key, title, font,
            Mathf.RoundToInt(theme != null ? theme.SectionTitleSize : 28f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 40f),
            TextAlignmentOptions.MidlineLeft);
        heading.fontStyle = FontStyles.Bold;
        LayoutElement headingLayout = heading.gameObject.AddComponent<LayoutElement>();
        headingLayout.minHeight = 40f;
        headingLayout.preferredHeight = 40f;

        TMP_Text value = CreateText(cardObject.transform, "Value_" + key, string.Empty, font,
            Mathf.RoundToInt(theme != null ? theme.BodySize : 22f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, minimumValueHeight),
            TextAlignmentOptions.TopLeft);
        value.color = TextSecondary;
        value.enableWordWrapping = true;
        LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>();
        valueLayout.minHeight = minimumValueHeight;
        valueLayout.preferredHeight = -1f;
        displayFields.Add(key, value);
    }

    private void EnsureTransferUi()
    {
        if (transferRoot != null)
        {
            return;
        }

        TMP_FontAsset font = ResolveFontAsset();
        if (font == null)
        {
            Debug.LogError("CareerProfilePage 缺少 TMP 字型資產。");
            return;
        }

        transferButton = CreateButton(transform, "Button_ProfileTransfer", "履歷搬遷", font,
            new Vector2(1f, 1f), new Vector2(-360f, -58f), new Vector2(200f, 56f),
            ButtonTone.Secondary);
        transferButton.onClick.AddListener(OpenTransfer);

        transferRoot = CreateUiObject("CareerProfileTransfer", transform, typeof(Image));
        RectTransform overlay = transferRoot.GetComponent<RectTransform>();
        Stretch(overlay, Vector2.zero, Vector2.zero);
        Image overlayImage = transferRoot.GetComponent<Image>();
        overlayImage.color = theme != null ? theme.Overlay : new Color(0f, 0f, 0f, 0.65f);
        overlayImage.raycastTarget = true;

        GameObject cardObject = CreateUiObject(
            "Card", overlay, typeof(Image), typeof(JobCheckModalCardSizer));
        RectTransform card = cardObject.GetComponent<RectTransform>();
        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = Surface;
        ApplySlicedSprite(cardImage);
        JobCheckModalCardSizer sizer = cardObject.GetComponent<JobCheckModalCardSizer>();
        sizer.Configure(
            1400f,
            740f,
            theme != null ? theme.ModalMaxWidth : 1400f,
            theme != null ? theme.ModalMaxHeight : 900f,
            theme != null ? theme.ModalViewportRatio : 0.85f);

        TMP_Text title = CreateText(card, "Title", "履歷資料搬遷", font,
            Mathf.RoundToInt(theme != null ? theme.ModalTitleSize : 36f),
            new Vector2(0f, 1f), new Vector2(32f, -18f), new Vector2(720f, 64f),
            TextAlignmentOptions.MidlineLeft);
        title.fontStyle = FontStyles.Bold;
        Button close = CreateButton(card, "Button_CloseTop", "關閉", font,
            new Vector2(1f, 1f), new Vector2(-72f, -44f), new Vector2(112f, 48f),
            ButtonTone.Secondary);

        GameObject messagePanel = CreateUiObject(
            "MessageViewport", card, typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        RectTransform messageRect = messagePanel.GetComponent<RectTransform>();
        Stretch(messageRect, new Vector2(32f, 104f), new Vector2(-32f, -98f));
        messagePanel.GetComponent<Image>().color = SurfaceMuted;
        ApplySlicedSprite(messagePanel.GetComponent<Image>());

        transferMessage = CreateText(messageRect, "Message", string.Empty, font,
            Mathf.RoundToInt(theme != null ? theme.BodySize : 22f),
            new Vector2(0f, 1f), Vector2.zero, Vector2.zero,
            TextAlignmentOptions.TopLeft);
        RectTransform messageContent = transferMessage.rectTransform;
        messageContent.anchorMin = new Vector2(0f, 1f);
        messageContent.anchorMax = new Vector2(1f, 1f);
        messageContent.pivot = new Vector2(0.5f, 1f);
        messageContent.anchoredPosition = new Vector2(0f, -24f);
        messageContent.sizeDelta = new Vector2(-48f, 0f);
        transferMessage.margin = new Vector4(8f, 0f, 8f, 24f);
        transferMessage.enableWordWrapping = true;
        transferMessage.overflowMode = TextOverflowModes.Overflow;
        ContentSizeFitter messageFitter =
            transferMessage.gameObject.AddComponent<ContentSizeFitter>();
        messageFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        messageFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        transferScroll = messagePanel.GetComponent<ScrollRect>();
        transferScroll.viewport = messageRect;
        transferScroll.content = messageContent;
        transferScroll.horizontal = false;
        transferScroll.vertical = true;
        transferScroll.movementType = ScrollRect.MovementType.Clamped;
        transferScroll.scrollSensitivity = 45f;

        GameObject footer = CreateUiObject("Footer", card, typeof(Image));
        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = Vector2.zero;
        footerRect.sizeDelta = new Vector2(0f, 88f);
        footer.GetComponent<Image>().color = Surface;

        GameObject actions = CreateUiObject(
            "Actions", footer.transform, typeof(HorizontalLayoutGroup));
        RectTransform actionsRect = actions.GetComponent<RectTransform>();
        Stretch(actionsRect, new Vector2(24f, 16f), new Vector2(-24f, -16f));
        HorizontalLayoutGroup actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
        actionsLayout.padding = new RectOffset(0, 0, 0, 0);
        actionsLayout.spacing = theme != null ? theme.SpaceSm : 12f;
        actionsLayout.childAlignment = TextAnchor.MiddleRight;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = true;
        actionsLayout.childForceExpandWidth = false;
        actionsLayout.childForceExpandHeight = true;

        Button export = CreateLayoutButton(
            actions.transform, "Button_Export", "匯出履歷", font, 170f, ButtonTone.Secondary);
        Button choose = CreateLayoutButton(
            actions.transform, "Button_ChooseImport", "選擇匯入檔", font, 200f,
            ButtonTone.Secondary);
        importButton = CreateLayoutButton(
            actions.transform, "Button_Import", "匯入履歷", font, 170f,
            ButtonTone.Primary);
        replaceButton = CreateLayoutButton(
            actions.transform, "Button_Replace", "取代本機履歷", font, 220f,
            ButtonTone.Danger);

        close.onClick.AddListener(CloseTransfer);
        export.onClick.AddListener(ChooseExportPath);
        choose.onClick.AddListener(ChooseImportFile);
        importButton.onClick.AddListener(() => ApplyImport(false));
        replaceButton.onClick.AddListener(() => ApplyImport(true));
        InvalidateTransferPreview();
        transferRoot.SetActive(false);
    }

    public void OpenTransfer()
    {
        EnsureTransferUi();
        if (transferRoot == null)
        {
            return;
        }

        if (editorRoot != null) editorRoot.SetActive(false);
        if (displayText != null) displayText.gameObject.SetActive(false);
        if (editButton != null) editButton.gameObject.SetActive(false);
        if (transferButton != null) transferButton.gameObject.SetActive(false);
        InvalidateTransferPreview();
        SetTransferMessage(
            "履歷搬遷與職缺資料搬遷是兩種不同檔案。\n\n"
            + "• 匯出會建立 .jobcheck-profile.json，不修改目前履歷。\n"
            + "• 匯入前會先顯示來源時間與處理方式。\n"
            + "• 本機已有不同履歷時，只有按下「取代本機履歷」才會套用，"
            + "而且會先備份原始 profile.json。\n"
            + "• 搬運檔尚未加密，請妥善保管。",
            false);
        transferRoot.SetActive(true);
    }

    public void CloseTransfer()
    {
        if (transferRoot != null) transferRoot.SetActive(false);
        if (displayText != null) displayText.gameObject.SetActive(true);
        if (editButton != null) editButton.gameObject.SetActive(true);
        if (transferButton != null) transferButton.gameObject.SetActive(true);
        InvalidateTransferPreview();
        Refresh();
    }

    public void ChooseExportPath()
    {
        if (FileBrowser.IsOpen)
        {
            return;
        }

        string initialDirectory = GetProfileExportDirectory();
        try
        {
            Directory.CreateDirectory(initialDirectory);
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException || exception is NotSupportedException)
        {
            SetTransferMessage("無法開啟履歷匯出資料夾：" + exception.Message, true);
            return;
        }
        FileBrowser.SetFilters(false,
            new FileBrowser.Filter("JobCheck profile",
                CareerProfilePortablePackageDto.FileExtension));
        FileBrowser.ShowSaveDialog(
            paths =>
            {
                if (this == null || paths == null || paths.Length != 1)
                {
                    return;
                }

                ExportProfile(EnsureProfileExtension(paths[0]));
            },
            () => { },
            FileBrowser.PickMode.Files,
            false,
            initialDirectory,
            "JobCheck-Profile-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")
                + CareerProfilePortablePackageDto.FileExtension,
            "選擇履歷匯出位置",
            "匯出");
    }

    public void ChooseImportFile()
    {
        if (FileBrowser.IsOpen)
        {
            return;
        }

        string initialDirectory = GetProfileExportDirectory();
        if (!Directory.Exists(initialDirectory))
        {
            string documents = Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);
            initialDirectory = Directory.Exists(documents)
                ? documents
                : UnityEngine.Application.persistentDataPath;
        }

        FileBrowser.SetFilters(false,
            new FileBrowser.Filter("JobCheck profile",
                CareerProfilePortablePackageDto.FileExtension));
        FileBrowser.ShowLoadDialog(
            paths =>
            {
                if (this == null || paths == null || paths.Length != 1)
                {
                    return;
                }

                PreviewImport(paths[0]);
            },
            () => { },
            FileBrowser.PickMode.Files,
            false,
            initialDirectory,
            null,
            "選擇履歷搬運檔",
            "預覽");
    }

    private void ExportProfile(string destinationPath)
    {
        PersistenceStorageResult<CareerProfilePortableExportSummary> result =
            CareerProfilePortableExportService.Export(
                ResolveProjectRelativePath(personalDataRootPath), destinationPath);
        if (!result.IsSuccess)
        {
            SetTransferMessage("履歷匯出失敗，原始資料未變更。\n"
                + FormatIssues(result.Issues), true);
            return;
        }

        SetTransferMessage("履歷匯出完成：\n" + result.Value.Path + "\n\n"
            + "匯出時間：" + result.Value.ExportedAt.ToLocalTime()
                .ToString("yyyy/MM/dd HH:mm") + "\n"
            + "檔案尚未加密；複製到其他裝置後，請從履歷頁選擇匯入。", false);
    }

    private void PreviewImport(string packagePath)
    {
        InvalidateTransferPreview();
        PersistenceStorageResult<CareerProfilePortableImportPreview> result =
            CareerProfilePortableImportService.Preview(
                packagePath, ResolveProjectRelativePath(personalDataRootPath));
        if (!result.IsSuccess)
        {
            SetTransferMessage("無法預覽這份履歷搬運檔。\n"
                + FormatIssues(result.Issues), true);
            return;
        }

        CareerProfilePortableImportPreview preview = result.Value;
        previewedPackagePath = preview.Path;
        string action;
        switch (preview.Disposition)
        {
            case CareerProfileImportDisposition.Create:
                action = "本機尚無履歷；可按「匯入履歷」建立。";
                importButton.interactable = true;
                break;
            case CareerProfileImportDisposition.Identical:
                action = "搬運檔與本機履歷相同，無須匯入。";
                break;
            default:
                action = "本機已有不同履歷。普通匯入已停用；"
                    + "若確認要使用搬運檔，請按「取代本機履歷」。原檔會先備份。";
                replaceButton.interactable = true;
                break;
        }

        SetTransferMessage("預覽成功：\n" + preview.Path + "\n\n"
            + "匯出時間：" + preview.ExportedAt.ToLocalTime()
                .ToString("yyyy/MM/dd HH:mm") + "\n"
            + "履歷摘要：" + ProfileSummary(preview.Profile) + "\n\n" + action,
            preview.Disposition == CareerProfileImportDisposition.ReplaceRequired);
    }

    private void ApplyImport(bool replaceExisting)
    {
        if (string.IsNullOrWhiteSpace(previewedPackagePath))
        {
            return;
        }

        PersistenceStorageResult<CareerProfilePortableImportSummary> result =
            CareerProfilePortableImportService.Import(
                previewedPackagePath,
                ResolveProjectRelativePath(personalDataRootPath),
                replaceExisting);
        InvalidateTransferPreview();
        if (!result.IsSuccess)
        {
            SetTransferMessage("履歷匯入未完成，本機資料未被靜默覆蓋。\n"
                + FormatIssues(result.Issues), true);
            return;
        }

        Refresh();
        if (result.Value.Disposition == CareerProfileImportDisposition.Identical)
        {
            SetTransferMessage("搬運檔與本機履歷相同，沒有寫入任何資料。", false);
            return;
        }

        string backup = string.IsNullOrWhiteSpace(result.Value.BackupPath)
            ? string.Empty
            : "\n原履歷備份：" + result.Value.BackupPath;
        SetTransferMessage("履歷匯入完成並已重新載入核對。" + backup, false);
    }

    private void InvalidateTransferPreview()
    {
        previewedPackagePath = null;
        if (importButton != null) importButton.interactable = false;
        if (replaceButton != null) replaceButton.interactable = false;
    }

    private void SetTransferMessage(string message, bool error)
    {
        if (transferMessage == null)
        {
            return;
        }

        transferMessage.text = message ?? string.Empty;
        transferMessage.color = error
            ? (theme != null ? theme.Danger : new Color(0.7f, 0.12f, 0.12f, 1f))
            : TextPrimary;
        if (transferScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            transferScroll.verticalNormalizedPosition = 1f;
        }
    }

    private static string ProfileSummary(CareerProfile profile)
    {
        if (profile == null)
        {
            return "無法讀取";
        }

        return "技能 " + profile.Skills.Count
            + "、工作經歷 " + profile.Experiences.Count
            + "、專案 " + profile.Projects.Count
            + "、學歷 " + profile.Educations.Count
            + "、語言 " + profile.Languages.Count;
    }

    private static string EnsureProfileExtension(string path)
    {
        return path.EndsWith(CareerProfilePortablePackageDto.FileExtension,
            StringComparison.OrdinalIgnoreCase)
            ? path
            : path + CareerProfilePortablePackageDto.FileExtension;
    }

    private static string GetProfileExportDirectory()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string root = string.IsNullOrWhiteSpace(documents)
            ? UnityEngine.Application.persistentDataPath
            : documents;
        return Path.Combine(root, "JobCheck", "ProfileExports");
    }

    private void EnsureEditorUi()
    {
        if (editorRoot != null)
        {
            return;
        }

        TMP_FontAsset font = ResolveFontAsset();
        if (font == null)
        {
            Debug.LogError("CareerProfilePage 缺少 TMP 字型資產。");
            return;
        }
        editButton = CreateButton(transform, "Button_EditProfile", "編輯履歷", font,
            new Vector2(1f, 1f), new Vector2(-140f, -58f), new Vector2(180f, 56f));
        editButton.onClick.AddListener(OpenEditor);

        editorRoot = CreateUiObject("CareerProfileEditor", transform, typeof(Image));
        RectTransform panel = editorRoot.GetComponent<RectTransform>();
        Stretch(panel, new Vector2(34f, 30f), new Vector2(-34f, -30f));
        editorRoot.GetComponent<Image>().color = AppBackground;

        TMP_Text title = CreateText(editorRoot.transform, "Title", "編輯個人履歷", font,
            Mathf.RoundToInt(theme != null ? theme.PageTitleSize : 48f),
            new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(720f, 64f),
            TextAlignmentOptions.MidlineLeft);
        title.fontStyle = FontStyles.Bold;

        GameObject scrollObject = CreateUiObject(
            "EditorScroll", panel, typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        RectTransform viewport = scrollObject.GetComponent<RectTransform>();
        Stretch(viewport, new Vector2(24f, 104f), new Vector2(-24f, -96f));
        scrollObject.GetComponent<Image>().color = AppBackground;

        GameObject contentObject = CreateUiObject(
            "Content", viewport, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(4, 4, 4, 24);
        contentLayout.spacing = theme != null ? theme.SpaceMd : 16f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        ContentSizeFitter contentFitter = contentObject.GetComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        editorScroll = scrollObject.GetComponent<ScrollRect>();
        editorScroll.viewport = viewport;
        editorScroll.content = content;
        editorScroll.horizontal = false;
        editorScroll.vertical = true;
        editorScroll.movementType = ScrollRect.MovementType.Clamped;
        editorScroll.scrollSensitivity = 45f;

        CreateEditorField("summary", "自我介紹", "可多行，說明背景、能力與職涯方向。",
            "介紹背景、能力與職涯方向", font, content, 480f);
        CreateSkillEditorUi(content, font);

        GameObject footer = CreateUiObject("Footer", editorRoot.transform, typeof(Image));
        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = Vector2.zero;
        footerRect.sizeDelta = new Vector2(0f, 88f);
        footer.GetComponent<Image>().color = Surface;

        editorStatus = CreateText(footer.transform, "Status", "", font,
            Mathf.RoundToInt(theme != null ? theme.SupportingTextSize : 20f),
            new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(720f, 52f),
            TextAlignmentOptions.MidlineLeft);
        Button cancel = CreateButton(footer.transform, "Button_Cancel", "取消", font,
            new Vector2(1f, 0.5f), new Vector2(-300f, 0f), new Vector2(180f, 56f),
            ButtonTone.Secondary);
        Button save = CreateButton(footer.transform, "Button_Save", "儲存", font,
            new Vector2(1f, 0.5f), new Vector2(-96f, 0f), new Vector2(180f, 56f));
        cancel.onClick.AddListener(CancelEditor);
        save.onClick.AddListener(SaveEditor);
        editorRoot.SetActive(false);
    }

    private void CreateSkillEditorUi(Transform parent, TMP_FontAsset font)
    {
        GameObject cardObject = CreateUiObject("Field_skills", parent,
            typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(Outline));
        cardObject.GetComponent<Image>().color = SurfaceMuted;
        ApplySlicedSprite(cardObject.GetComponent<Image>());
        Outline outline = cardObject.GetComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(1f, -1f);
        VerticalLayoutGroup cardLayout = cardObject.GetComponent<VerticalLayoutGroup>();
        int padding = Mathf.RoundToInt(theme != null ? theme.SpaceMd : 16f);
        cardLayout.padding = new RectOffset(padding, padding, padding, padding);
        cardLayout.spacing = theme != null ? theme.SpaceSm : 12f;
        cardLayout.childAlignment = TextAnchor.UpperLeft;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;
        cardObject.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text title = CreateText(cardObject.transform, "Label_skills", "技能", font,
            Mathf.RoundToInt(theme != null ? theme.SectionTitleSize : 28f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 38f),
            TextAlignmentOptions.MidlineLeft);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
        TMP_Text help = CreateText(cardObject.transform, "Help_skills",
            "逐筆新增技能；使用時間與備註可留空。", font,
            Mathf.RoundToInt(theme != null ? theme.SupportingTextSize : 20f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 32f),
            TextAlignmentOptions.MidlineLeft);
        help.color = TextSecondary;
        help.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;

        GameObject list = CreateUiObject("SkillList", cardObject.transform,
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        skillListRoot = list.transform;
        VerticalLayoutGroup listLayout = list.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 8f;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        list.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        CreateLayoutButton(cardObject.transform, "Button_AddSkill", "新增技能", font,
            170f, ButtonTone.Primary).onClick.AddListener(() => OpenSkillForm(-1));

        skillFormRoot = CreateUiObject("SkillForm", cardObject.transform,
            typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        skillFormRoot.GetComponent<Image>().color = Surface;
        VerticalLayoutGroup formLayout = skillFormRoot.GetComponent<VerticalLayoutGroup>();
        formLayout.padding = new RectOffset(16, 16, 16, 16);
        formLayout.spacing = 8f;
        formLayout.childControlWidth = true;
        formLayout.childControlHeight = true;
        formLayout.childForceExpandWidth = true;
        formLayout.childForceExpandHeight = false;
        skillFormRoot.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        skillNameInput = CreateLabeledSkillInput(skillFormRoot.transform, "Name",
            "技能名稱", "輸入技能名稱（可自訂）", font, 56f, true);
        skillYearsDropdown = CreateLabeledSkillDropdown(skillFormRoot.transform,
            "Years", "使用年數（選填）", font,
            new[] { "未填寫" }.Concat(Enumerable.Range(0, 41)
                .Select(years => years + " 年")).ToArray());
        skillMonthsDropdown = CreateLabeledSkillDropdown(skillFormRoot.transform,
            "Months", "額外月數", font,
            Enumerable.Range(0, 12).Select(months => months + " 個月").ToArray());
        skillYearsDropdown.onValueChanged.AddListener(index =>
            skillMonthsDropdown.interactable = index > 0);
        skillNotesInput = CreateLabeledSkillInput(skillFormRoot.transform, "Notes",
            "備註（選填）", "例如：主要用於遊戲專案", font, 96f, false);

        GameObject actions = CreateUiObject("SkillActions", skillFormRoot.transform,
            typeof(HorizontalLayoutGroup));
        HorizontalLayoutGroup actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
        actionsLayout.spacing = 12f;
        actionsLayout.childAlignment = TextAnchor.MiddleRight;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = true;
        actionsLayout.childForceExpandWidth = false;
        actionsLayout.childForceExpandHeight = true;
        actions.AddComponent<LayoutElement>().preferredHeight = 56f;
        CreateLayoutButton(actions.transform, "Button_CancelSkill", "取消", font,
            120f, ButtonTone.Secondary).onClick.AddListener(CloseSkillForm);
        CreateLayoutButton(actions.transform, "Button_SaveSkill", "儲存這筆", font,
            150f, ButtonTone.Primary).onClick.AddListener(() => ApplySkillForm());
        skillFormRoot.SetActive(false);
    }

    private TMP_InputField CreateLabeledSkillInput(
        Transform parent, string key, string label, string placeholder,
        TMP_FontAsset font, float height, bool singleLine)
    {
        TMP_Text heading = CreateText(parent, "Label_" + key, label, font, 20,
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 30f),
            TextAlignmentOptions.MidlineLeft);
        heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        TMP_InputField input = CreateInput(parent, "Input_" + key, placeholder, font,
            Vector2.zero, new Vector2(0f, height));
        input.lineType = singleLine
            ? TMP_InputField.LineType.SingleLine : TMP_InputField.LineType.MultiLineNewline;
        input.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return input;
    }

    private TMP_Dropdown CreateLabeledSkillDropdown(
        Transform parent, string key, string label, TMP_FontAsset font, string[] options)
    {
        TMP_Text heading = CreateText(parent, "Label_" + key, label, font, 20,
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 30f),
            TextAlignmentOptions.MidlineLeft);
        heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

        GameObject root = CreateUiObject("Dropdown_" + key, parent,
            typeof(Image), typeof(TMP_Dropdown));
        Image background = root.GetComponent<Image>();
        background.color = SurfaceMuted;
        ApplySlicedSprite(background);
        root.AddComponent<LayoutElement>().preferredHeight = 56f;
        TMP_Text caption = CreateText(root.transform, "Caption", string.Empty, font, 21,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            TextAlignmentOptions.MidlineLeft);
        Stretch(caption.rectTransform, new Vector2(12f, 4f), new Vector2(-42f, -4f));
        TMP_Text arrow = CreateText(root.transform, "Arrow", "▼", font, 18,
            new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(32f, 44f),
            TextAlignmentOptions.Center);
        arrow.raycastTarget = false;

        GameObject template = CreateUiObject("Template", root.transform,
            typeof(Image), typeof(ScrollRect));
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, -4f);
        templateRect.sizeDelta = new Vector2(0f, 260f);
        template.GetComponent<Image>().color = Surface;

        GameObject viewport = CreateUiObject("Viewport", template.transform,
            typeof(Image), typeof(RectMask2D));
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        viewport.GetComponent<Image>().color = Surface;

        GameObject content = CreateUiObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        GameObject item = CreateUiObject("Item", content.transform,
            typeof(Image), typeof(Toggle));
        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 1f);
        itemRect.anchorMax = new Vector2(1f, 1f);
        itemRect.pivot = new Vector2(0.5f, 1f);
        itemRect.anchoredPosition = Vector2.zero;
        itemRect.sizeDelta = new Vector2(0f, 48f);
        Image itemImage = item.GetComponent<Image>();
        itemImage.color = Surface;
        item.GetComponent<Toggle>().targetGraphic = itemImage;
        TMP_Text itemText = CreateText(item.transform, "ItemText", string.Empty, font, 20,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            TextAlignmentOptions.MidlineLeft);
        Stretch(itemText.rectTransform, new Vector2(12f, 4f), new Vector2(-12f, -4f));

        ScrollRect scroll = template.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 34f;

        TMP_Dropdown dropdown = root.GetComponent<TMP_Dropdown>();
        dropdown.targetGraphic = background;
        dropdown.template = templateRect;
        dropdown.captionText = caption;
        dropdown.itemText = itemText;
        dropdown.ClearOptions();
        dropdown.AddOptions(options.ToList());
        dropdown.RefreshShownValue();
        template.SetActive(false);
        return dropdown;
    }

    private void CreateEditorField(
        string key,
        string title,
        string help,
        string placeholder,
        TMP_FontAsset font,
        Transform parent,
        float inputHeight)
    {
        GameObject cardObject = CreateUiObject(
            "Field_" + key,
            parent,
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter),
            typeof(Outline));
        Image card = cardObject.GetComponent<Image>();
        card.color = SurfaceMuted;
        ApplySlicedSprite(card);
        Outline outline = cardObject.GetComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(1f, -1f);
        VerticalLayoutGroup layout = cardObject.GetComponent<VerticalLayoutGroup>();
        int padding = Mathf.RoundToInt(theme != null ? theme.SpaceMd : 16f);
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = theme != null ? theme.SpaceXs : 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = cardObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text label = CreateText(cardObject.transform, "Label_" + key, title, font,
            Mathf.RoundToInt(theme != null ? theme.SectionTitleSize : 28f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 38f),
            TextAlignmentOptions.MidlineLeft);
        label.fontStyle = FontStyles.Bold;
        label.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
        TMP_Text helper = CreateText(cardObject.transform, "Help_" + key, help, font,
            Mathf.RoundToInt(theme != null ? theme.SupportingTextSize : 20f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 30f),
            TextAlignmentOptions.MidlineLeft);
        helper.color = TextSecondary;
        helper.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

        TMP_InputField input = CreateInput(cardObject.transform, "Input_" + key,
            placeholder, font, Vector2.zero, new Vector2(0f, inputHeight));
        LayoutElement inputLayout = input.gameObject.AddComponent<LayoutElement>();
        inputLayout.minHeight = inputHeight;
        inputLayout.preferredHeight = inputHeight;
        editorFields.Add(key, input);
    }

    private TMP_InputField CreateInput(
        Transform parent,
        string name,
        string placeholder,
        TMP_FontAsset font,
        Vector2 topLeft,
        Vector2 size)
    {
        GameObject root = CreateUiObject(name, parent, typeof(Image), typeof(TMP_InputField));
        RectTransform rect = root.GetComponent<RectTransform>();
        TopLeft(rect, topLeft, size);
        Image background = root.GetComponent<Image>();
        background.color = Surface;
        ApplySlicedSprite(background);
        root.AddComponent<RectMask2D>();

        TMP_Text placeholderText = CreateText(root.transform, "Placeholder", placeholder, font, 20,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAlignmentOptions.TopLeft);
        Stretch(placeholderText.rectTransform, new Vector2(10f, 7f), new Vector2(-10f, -7f));
        placeholderText.color = TextSecondary;
        TMP_Text valueText = CreateText(root.transform, "Text", "", font, 22,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAlignmentOptions.TopLeft);
        Stretch(valueText.rectTransform, new Vector2(10f, 7f), new Vector2(-10f, -7f));
        valueText.richText = true;

        TMP_InputField input = root.GetComponent<TMP_InputField>();
        input.textViewport = rect;
        input.textComponent = valueText;
        input.placeholder = placeholderText;
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.richText = true;
        input.isRichTextEditingAllowed = false;
        input.targetGraphic = root.GetComponent<Image>();
        return input;
    }

    private Button CreateButton(
        Transform parent,
        string name,
        string label,
        TMP_FontAsset font,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        ButtonTone tone = ButtonTone.Primary)
    {
        GameObject root = CreateUiObject(name, parent, typeof(Image), typeof(Button));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = root.GetComponent<Image>();
        Color normal = tone == ButtonTone.Secondary
            ? Surface
            : tone == ButtonTone.Danger ? Danger : Primary;
        Color highlighted = tone == ButtonTone.Secondary
            ? SurfaceMuted
            : tone == ButtonTone.Danger ? new Color(
                Mathf.Min(1f, Danger.r * 1.12f),
                Mathf.Min(1f, Danger.g * 1.12f),
                Mathf.Min(1f, Danger.b * 1.12f), 1f) : PrimaryHover;
        Color pressed = tone == ButtonTone.Secondary
            ? Border
            : tone == ButtonTone.Danger ? Danger * 0.82f : PrimaryPressed;
        image.color = normal;
        ApplySlicedSprite(image);
        if (tone == ButtonTone.Secondary)
        {
            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);
        }
        Button button = root.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.pressedColor = pressed;
        colors.selectedColor = normal;
        colors.disabledColor = new Color(
            SurfaceMuted.r, SurfaceMuted.g, SurfaceMuted.b, 0.65f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        TMP_Text text = CreateText(root.transform, "Text", label, font, 24,
            new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(20f, 12f), TextAlignmentOptions.Center);
        text.color = tone == ButtonTone.Secondary ? TextPrimary : TextOnPrimary;
        return button;
    }

    private Button CreateLayoutButton(
        Transform parent,
        string name,
        string label,
        TMP_FontAsset font,
        float width,
        ButtonTone tone)
    {
        Button button = CreateButton(parent, name, label, font,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 56f), tone);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.minHeight = 56f;
        layout.preferredHeight = 56f;
        return button;
    }

    private TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        TMP_FontAsset font,
        int fontSize,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        TextAlignmentOptions alignment)
    {
        GameObject root = CreateUiObject(name, parent, typeof(TextMeshProUGUI));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor.y > 0.99f
            ? new Vector2(anchor.x, 1f)
            : new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TMP_Text text = root.GetComponent<TMP_Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = TextPrimary;
        text.text = value;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private void ApplySlicedSprite(Image image)
    {
        if (image == null || theme == null || theme.ButtonBackgroundSprite == null)
        {
            return;
        }

        image.sprite = theme.ButtonBackgroundSprite;
        image.type = Image.Type.Sliced;
    }

    private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
    {
        var types = new List<Type> { typeof(RectTransform), typeof(CanvasRenderer) };
        types.AddRange(components);
        var result = new GameObject(name, types.ToArray());
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

    private static void TopLeft(RectTransform rect, Vector2 topLeft, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = size;
    }

    private void SetEditorStatus(string message, bool error)
    {
        EnsureEditorUi();
        editorStatus.text = message ?? string.Empty;
        editorStatus.color = error
            ? (theme != null ? theme.Danger : new Color(0.75f, 0.12f, 0.12f, 1f))
            : TextPrimary;
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
