using System;
using System.Collections.Generic;
using System.Globalization;
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
    private List<CareerProfileLink> linkDraft;
    private Transform linkListRoot;
    private GameObject linkFormRoot;
    private TMP_InputField linkLabelInput;
    private TMP_InputField linkUrlInput;
    private int editingLinkIndex = -1;
    private List<CareerSkill> skillDraft;
    private Transform skillListRoot;
    private GameObject skillFormRoot;
    private TMP_InputField skillNameInput;
    private TMP_InputField skillNotesInput;
    private TMP_Dropdown skillYearsDropdown;
    private TMP_Dropdown skillMonthsDropdown;
    private int editingSkillIndex = -1;
    private List<CareerLanguage> languageDraft;
    private Transform languageListRoot;
    private GameObject languageFormRoot;
    private TMP_Dropdown languageNameDropdown;
    private TMP_Dropdown languageLevelDropdown;
    private int editingLanguageIndex = -1;
    private int editingLanguageInitialLevelOption;
    private List<CareerExperience> experienceDraft;
    private Transform experienceListRoot;
    private GameObject experienceFormRoot;
    private TMP_InputField experienceOrganizationInput;
    private TMP_InputField experienceRoleInput;
    private TMP_InputField experienceStartYearInput;
    private TMP_Dropdown experienceStartMonthDropdown;
    private TMP_InputField experienceEndYearInput;
    private TMP_Dropdown experienceEndMonthDropdown;
    private Toggle experienceCurrentToggle;
    private TMP_InputField experienceDescriptionInput;
    private GameObject experienceEndFields;
    private GameObject experienceSkillChoices;
    private TMP_Text experienceSkillSummary;
    private readonly HashSet<string> selectedExperienceSkillIds =
        new HashSet<string>(StringComparer.Ordinal);
    private int editingExperienceIndex = -1;
    private List<CareerProject> projectDraft;
    private Transform projectListRoot;
    private GameObject projectFormRoot;
    private TMP_InputField projectNameInput;
    private TMP_InputField projectDescriptionInput;
    private TMP_InputField projectUrlInput;
    private GameObject projectSkillChoices;
    private TMP_Text projectSkillSummary;
    private readonly HashSet<string> selectedProjectTechnologies =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private int editingProjectIndex = -1;
    private List<CareerEducation> educationDraft;
    private Transform educationListRoot;
    private GameObject educationFormRoot;
    private TMP_InputField educationInstitutionInput;
    private TMP_InputField educationProgramInput;
    private TMP_Dropdown educationDegreeDropdown;
    private TMP_Dropdown educationStatusDropdown;
    private TMP_InputField educationStartYearInput;
    private TMP_Dropdown educationStartMonthDropdown;
    private TMP_InputField educationEndYearInput;
    private TMP_Dropdown educationEndMonthDropdown;
    private TMP_InputField educationFieldTagsInput;
    private TMP_InputField educationNotesInput;
    private int editingEducationIndex = -1;

    private static readonly string[] LanguageNames = { "中文", "英文", "日文" };
    private static readonly string[] LanguageIds = { "zh", "en", "ja" };
    private static readonly LanguageProficiency[] LanguageLevels =
    {
        LanguageProficiency.None,
        LanguageProficiency.Beginner,
        LanguageProficiency.Intermediate,
        LanguageProficiency.Proficient
    };

    private const string ProfileUiCharacters =
        "個人履歷最後更新自我介紹連結技能工作經歷專案學歷語言能力尚未填寫新增讀取失敗未知錯誤編輯儲存取消至今年月日時分公司職務開始結束內容名稱技術網址說明機構項目備註程度母語首頁職缺側欄寬度搬遷匯出匯入選擇檔案取代本機關閉預覽成功檔案尚未加密請妥善保管等級學位已畢業在學未完成科系標籤月數逗號分隔請輸入整數使用時間年自訂移除這筆重複請先填寫選填儲存技能暫不評級無技能個人網站作品集或其他參考網址功用有效的必填儲存連結貼上本機路徑中文英文日文不會略懂中等精通請選擇已有語言逐筆選擇語言與程度未新增的語言不會自動判為不會請選擇語言和程度這項語言已存在編輯原有項目語言能力已暫存公司組織目前仍在職選擇關聯技能展開收合尚無技能可先到技能區新增請填寫有效年月開始年月不得晚於結束年月不能晚於現在工作經歷已暫存專案經歷簡述成果專案已暫存學校學程高中專科學士碩士博士就學狀態逐筆填寫可供職缺比對例如某某大學資訊工程學系資訊管理其他需要補充的學歷資訊兩欄都留空學歷已暫存";

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
        linkDraft = CloneLinks(CurrentProfile.Links);
        RenderLinkDraft();
        CloseLinkForm();
        skillDraft = CloneSkills(CurrentProfile.Skills);
        RenderSkillDraft();
        CloseSkillForm();
        experienceDraft = CloneExperiences(CurrentProfile.Experiences);
        RenderExperienceDraft();
        CloseExperienceForm();
        projectDraft = CloneProjects(CurrentProfile.Projects);
        RenderProjectDraft();
        CloseProjectForm();
        educationDraft = CloneEducations(CurrentProfile.Educations);
        RenderEducationDraft();
        CloseEducationForm();
        languageDraft = CloneLanguages(CurrentProfile.Languages);
        RenderLanguageDraft();
        CloseLanguageForm();
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
        linkDraft = null;
        CloseLinkForm();
        skillDraft = null;
        CloseSkillForm();
        experienceDraft = null;
        CloseExperienceForm();
        projectDraft = null;
        CloseProjectForm();
        educationDraft = null;
        CloseEducationForm();
        languageDraft = null;
        CloseLanguageForm();
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
        if (linkFormRoot != null && linkFormRoot.activeSelf && !ApplyLinkForm())
            return;
        if (languageFormRoot != null && languageFormRoot.activeSelf && !ApplyLanguageForm())
            return;
        if (experienceFormRoot != null && experienceFormRoot.activeSelf && !ApplyExperienceForm())
            return;
        if (projectFormRoot != null && projectFormRoot.activeSelf && !ApplyProjectForm())
            return;
        if (educationFormRoot != null && educationFormRoot.activeSelf && !ApplyEducationForm())
            return;

        DateTimeOffset now = DateTimeOffset.Now;
        CareerProfile candidate = CreateSummaryEditCandidate(
            CurrentProfile, editorFields["summary"].text, linkDraft, skillDraft,
            experienceDraft, projectDraft, educationDraft, languageDraft, now);

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
        CareerProfile current, string summary, IList<CareerProfileLink> links,
        IList<CareerSkill> skills, IList<CareerExperience> experiences,
        IList<CareerProject> projects, IList<CareerEducation> educations,
        IList<CareerLanguage> languages, DateTimeOffset now)
    {
        return new CareerProfile
        {
            Id = current.Id,
            SchemaVersion = current.SchemaVersion,
            Summary = summary,
            CreatedAt = current.CreatedAt ?? now,
            UpdatedAt = now,
            Links = links == null ? current.Links : new List<CareerProfileLink>(links),
            Skills = skills == null ? current.Skills : new List<CareerSkill>(skills),
            Experiences = experiences == null
                ? current.Experiences : new List<CareerExperience>(experiences),
            Projects = projects == null ? current.Projects : new List<CareerProject>(projects),
            Educations = educations == null ? current.Educations : new List<CareerEducation>(educations),
            Languages = languages == null ? current.Languages : new List<CareerLanguage>(languages),
            JobPreferences = current.JobPreferences
        };
    }

    private static List<CareerProfileLink> CloneLinks(IEnumerable<CareerProfileLink> source)
    {
        return (source ?? Enumerable.Empty<CareerProfileLink>())
            .Where(item => item != null)
            .Select(item => new CareerProfileLink
            {
                Id = item.Id,
                Label = item.Label,
                Url = item.Url
            })
            .ToList();
    }

    private void RenderLinkDraft()
    {
        if (linkListRoot == null) return;
        foreach (Transform child in linkListRoot.Cast<Transform>().ToArray())
        {
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        TMP_FontAsset font = ResolveFontAsset();
        if (linkDraft == null || linkDraft.Count == 0)
        {
            TMP_Text empty = CreateText(linkListRoot, "Empty", "尚未新增連結", font, 20,
                new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 36f),
                TextAlignmentOptions.MidlineLeft);
            empty.color = TextSecondary;
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            return;
        }

        for (int index = 0; index < linkDraft.Count; index++)
        {
            int linkIndex = index;
            CareerProfileLink link = linkDraft[index];
            GameObject row = CreateUiObject("Link_" + index, linkListRoot,
                typeof(Image), typeof(HorizontalLayoutGroup));
            Image image = row.GetComponent<Image>();
            image.color = Surface;
            ApplySlicedSprite(image);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 12, 8, 8);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            row.AddComponent<LayoutElement>().preferredHeight = 68f;

            TMP_Text label = CreateText(row.transform, "Summary",
                Join("｜", link.Label, link.Url), font, 21,
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 52f),
                TextAlignmentOptions.MidlineLeft);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.minWidth = 120f;
            CreateLayoutButton(row.transform, "Button_Edit", "編輯", font, 100f,
                ButtonTone.Secondary).onClick.AddListener(() => OpenLinkForm(linkIndex));
            CreateLayoutButton(row.transform, "Button_Remove", "移除", font, 100f,
                ButtonTone.Secondary).onClick.AddListener(() => RemoveLink(linkIndex));
        }
    }

    private void OpenLinkForm(int index)
    {
        editingLinkIndex = index;
        CareerProfileLink item = index >= 0 && linkDraft != null && index < linkDraft.Count
            ? linkDraft[index] : null;
        linkLabelInput.text = item?.Label ?? string.Empty;
        linkUrlInput.text = item?.Url ?? string.Empty;
        linkFormRoot.SetActive(true);
        SetEditorStatus("", false);
    }

    private void CloseLinkForm()
    {
        editingLinkIndex = -1;
        if (linkFormRoot != null) linkFormRoot.SetActive(false);
    }

    private void RemoveLink(int index)
    {
        if (linkDraft == null || index < 0 || index >= linkDraft.Count) return;
        linkDraft.RemoveAt(index);
        CloseLinkForm();
        RenderLinkDraft();
        SetEditorStatus("連結已從本次編輯移除；按整頁儲存才會寫入。", false);
    }

    private static bool HasLinkValue(string value) => !string.IsNullOrWhiteSpace(value);

    private bool ApplyLinkForm()
    {
        string label = linkLabelInput.text.Trim();
        string url = linkUrlInput.text.Trim();
        if (!HasLinkValue(label))
        {
            SetEditorStatus("請先填寫連結名稱。", true);
            return false;
        }

        if (!HasLinkValue(url))
        {
            SetEditorStatus("請先填寫連結網址或路徑。", true);
            return false;
        }

        CareerProfileLink existing = editingLinkIndex >= 0 && editingLinkIndex < linkDraft.Count
            ? linkDraft[editingLinkIndex] : null;
        CareerProfileLink saved = new CareerProfileLink
        {
            Id = existing?.Id ?? CareerProfileIdGenerator.CreateLinkId(),
            Label = label,
            Url = url
        };
        if (existing == null) linkDraft.Add(saved);
        else linkDraft[editingLinkIndex] = saved;
        CloseLinkForm();
        RenderLinkDraft();
        SetEditorStatus("連結已暫存；按整頁儲存才會寫入。", false);
        return true;
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

    private static List<CareerExperience> CloneExperiences(IEnumerable<CareerExperience> source)
    {
        return (source ?? Enumerable.Empty<CareerExperience>())
            .Where(item => item != null)
            .Select(item => new CareerExperience
            {
                Id = item.Id,
                Organization = item.Organization,
                Role = item.Role,
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                IsCurrent = item.IsCurrent,
                Description = item.Description,
                SkillIds = item.SkillIds == null
                    ? new List<string>() : new List<string>(item.SkillIds)
            })
            .ToList();
    }

    private static string ExperienceSkillId(CareerSkill skill)
    {
        return RequirementCatalog.NormalizeSkill(
            string.IsNullOrWhiteSpace(skill?.SkillId) ? skill?.Name : skill.SkillId);
    }

    private void RenderExperienceDraft()
    {
        if (experienceListRoot == null) return;
        foreach (Transform child in experienceListRoot.Cast<Transform>().ToArray())
        {
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        TMP_FontAsset font = ResolveFontAsset();
        if (experienceDraft == null || experienceDraft.Count == 0)
        {
            TMP_Text empty = CreateText(experienceListRoot, "Empty", "尚未新增工作經歷",
                font, 20, new Vector2(0f, 1f), Vector2.zero,
                new Vector2(0f, 36f), TextAlignmentOptions.MidlineLeft);
            empty.color = TextSecondary;
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            return;
        }

        for (int index = 0; index < experienceDraft.Count; index++)
        {
            int experienceIndex = index;
            CareerExperience item = experienceDraft[index];
            GameObject row = CreateUiObject("Experience_" + index, experienceListRoot,
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
            row.AddComponent<LayoutElement>().preferredHeight = 78f;

            int months = RequirementMatchEngine.CalculateCoveredMonths(
                new[] { item }, DateTimeOffset.Now);
            string duration = months > 0 ? FormatExperienceMonths(months) : null;
            TMP_Text label = CreateText(row.transform, "Summary", Join("｜",
                Join(" / ", item.Organization, item.Role),
                FormatPeriod(item.StartDate, item.EndDate, item.IsCurrent), duration),
                font, 21, new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(0f, 52f), TextAlignmentOptions.MidlineLeft);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.minWidth = 120f;
            CreateLayoutButton(row.transform, "Button_Edit", "編輯", font, 100f,
                ButtonTone.Secondary).onClick.AddListener(
                    () => OpenExperienceForm(experienceIndex));
            CreateLayoutButton(row.transform, "Button_Remove", "移除", font, 100f,
                ButtonTone.Secondary).onClick.AddListener(
                    () => RemoveExperience(experienceIndex));
        }
    }

    private static string FormatExperienceMonths(int months)
    {
        int years = months / 12;
        int remainder = months % 12;
        return years == 0 ? remainder + " 個月"
            : remainder == 0 ? years + " 年"
            : years + " 年 " + remainder + " 個月";
    }

    private static bool TryParseExperienceMonth(string value, out DateTime month)
    {
        month = default(DateTime);
        if (string.IsNullOrWhiteSpace(value)) return false;
        string[] formats = { "yyyy/MM", "yyyy-MM", "yyyy.MM", "yyyy/MM/dd",
            "yyyy-MM-dd", "yyyy.MM.dd", "yyyy" };
        if (!DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime parsed)) return false;
        month = new DateTime(parsed.Year, parsed.Month, 1);
        return true;
    }

    private static void SetExperienceMonthFields(
        string value, TMP_InputField year, TMP_Dropdown month)
    {
        if (TryParseExperienceMonth(value, out DateTime parsed))
        {
            year.text = parsed.Year.ToString(CultureInfo.InvariantCulture);
            month.SetValueWithoutNotify(parsed.Month);
        }
        else
        {
            year.text = string.Empty;
            month.SetValueWithoutNotify(0);
        }
    }

    private void OpenExperienceForm(int index)
    {
        editingExperienceIndex = index;
        CareerExperience item = index >= 0 && experienceDraft != null
            && index < experienceDraft.Count ? experienceDraft[index] : null;
        experienceOrganizationInput.text = item?.Organization ?? string.Empty;
        experienceRoleInput.text = item?.Role ?? string.Empty;
        SetExperienceMonthFields(item?.StartDate, experienceStartYearInput,
            experienceStartMonthDropdown);
        SetExperienceMonthFields(item?.EndDate, experienceEndYearInput,
            experienceEndMonthDropdown);
        experienceCurrentToggle.isOn = item?.IsCurrent ?? false;
        experienceEndFields.SetActive(!experienceCurrentToggle.isOn);
        experienceDescriptionInput.text = item?.Description ?? string.Empty;
        selectedExperienceSkillIds.Clear();
        foreach (string id in item?.SkillIds ?? Enumerable.Empty<string>())
        {
            string normalized = RequirementCatalog.NormalizeSkill(id);
            if (normalized.Length > 0) selectedExperienceSkillIds.Add(normalized);
        }
        RenderExperienceSkillChoices();
        experienceSkillChoices.SetActive(false);
        experienceFormRoot.SetActive(true);
        SetEditorStatus("", false);
    }

    private void CloseExperienceForm()
    {
        editingExperienceIndex = -1;
        selectedExperienceSkillIds.Clear();
        if (experienceFormRoot != null) experienceFormRoot.SetActive(false);
        if (experienceSkillChoices != null) experienceSkillChoices.SetActive(false);
    }

    private void RemoveExperience(int index)
    {
        if (experienceDraft == null || index < 0 || index >= experienceDraft.Count) return;
        experienceDraft.RemoveAt(index);
        CloseExperienceForm();
        RenderExperienceDraft();
        SetEditorStatus("工作經歷已從本次編輯移除；按整頁儲存才會寫入。", false);
    }

    private static bool TryReadExperienceMonth(
        TMP_InputField yearInput, TMP_Dropdown monthDropdown, out DateTime month)
    {
        month = default(DateTime);
        return int.TryParse(yearInput.text.Trim(), NumberStyles.None,
                   CultureInfo.InvariantCulture, out int year)
            && year >= 1 && year <= 9999
            && monthDropdown.value >= 1 && monthDropdown.value <= 12
            && DateTime.TryParseExact(year.ToString("D4", CultureInfo.InvariantCulture)
                + "/" + monthDropdown.value.ToString("D2", CultureInfo.InvariantCulture),
                "yyyy/MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out month);
    }

    private bool ApplyExperienceForm()
    {
        string organization = experienceOrganizationInput.text.Trim();
        string role = experienceRoleInput.text.Trim();
        if (organization.Length == 0 || role.Length == 0)
        {
            SetEditorStatus("請先填寫公司／組織與職務。", true);
            return false;
        }
        if (!TryReadExperienceMonth(experienceStartYearInput,
            experienceStartMonthDropdown, out DateTime start))
        {
            SetEditorStatus("請填寫有效的開始年月。", true);
            return false;
        }
        DateTime now = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        if (start > now)
        {
            SetEditorStatus("開始年月不能晚於現在。", true);
            return false;
        }
        bool isCurrent = experienceCurrentToggle.isOn;
        DateTime end = default(DateTime);
        if (!isCurrent && !TryReadExperienceMonth(experienceEndYearInput,
            experienceEndMonthDropdown, out end))
        {
            SetEditorStatus("請填寫有效的結束年月，或勾選目前仍在職。", true);
            return false;
        }
        if (!isCurrent && (end < start || end > now))
        {
            SetEditorStatus("結束年月不得早於開始年月，也不能晚於現在。", true);
            return false;
        }
        CareerExperience existing = editingExperienceIndex >= 0
            && editingExperienceIndex < experienceDraft.Count
                ? experienceDraft[editingExperienceIndex] : null;
        CareerExperience saved = new CareerExperience
        {
            Id = existing?.Id ?? CareerProfileIdGenerator.CreateExperienceId(),
            Organization = organization,
            Role = role,
            StartDate = start.ToString("yyyy/MM", CultureInfo.InvariantCulture),
            EndDate = isCurrent ? null : end.ToString("yyyy/MM", CultureInfo.InvariantCulture),
            IsCurrent = isCurrent,
            Description = experienceDescriptionInput.text.Trim(),
            SkillIds = selectedExperienceSkillIds.OrderBy(id => id, StringComparer.Ordinal).ToList()
        };
        if (existing == null) experienceDraft.Add(saved);
        else experienceDraft[editingExperienceIndex] = saved;
        CloseExperienceForm();
        RenderExperienceDraft();
        SetEditorStatus("工作經歷已暫存；按整頁儲存才會寫入。", false);
        return true;
    }

    private void UpdateExperienceSkillSummary()
    {
        if (experienceSkillSummary == null) return;
        string[] labels = (skillDraft ?? new List<CareerSkill>())
            .Where(skill => selectedExperienceSkillIds.Contains(ExperienceSkillId(skill)))
            .Select(skill => skill.Name).Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct().ToArray();
        experienceSkillSummary.text = labels.Length == 0
            ? "選擇關聯技能（可不選）" : "已選：" + string.Join("、", labels);
    }

    private void RenderExperienceSkillChoices()
    {
        if (experienceSkillChoices == null) return;
        Transform root = experienceSkillChoices.transform;
        foreach (Transform child in root.Cast<Transform>().ToArray())
        {
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
        TMP_FontAsset font = ResolveFontAsset();
        var choices = (skillDraft ?? new List<CareerSkill>())
            .Where(skill => !string.IsNullOrWhiteSpace(skill?.Name)
                && ExperienceSkillId(skill).Length > 0)
            .GroupBy(ExperienceSkillId).Select(group => group.First()).ToList();
        if (choices.Count == 0)
        {
            TMP_Text empty = CreateText(root, "Empty", "尚無技能，可先到技能區新增。",
                font, 20, new Vector2(0f, 1f), Vector2.zero,
                new Vector2(0f, 42f), TextAlignmentOptions.MidlineLeft);
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
        }
        foreach (CareerSkill skill in choices)
        {
            string id = ExperienceSkillId(skill);
            CreateExperienceSkillOption(root, font, id, skill.Name);
        }
        foreach (string id in selectedExperienceSkillIds
            .Where(id => choices.All(skill => ExperienceSkillId(skill) != id)).ToArray())
        {
            CreateExperienceSkillOption(root, font, id, "原有技能：" + id);
        }
        UpdateExperienceSkillSummary();
    }

    private void CreateExperienceSkillOption(
        Transform root, TMP_FontAsset font, string id, string label)
    {
        GameObject row = CreateUiObject("SkillChoice_" + id, root,
            typeof(Image), typeof(Toggle));
        Image background = row.GetComponent<Image>();
        background.color = Surface;
        row.AddComponent<LayoutElement>().preferredHeight = 50f;
        GameObject checkbox = CreateUiObject("Checkbox", row.transform,
            typeof(Image), typeof(Outline));
        RectTransform checkboxRect = checkbox.GetComponent<RectTransform>();
        checkboxRect.anchorMin = new Vector2(0f, 0.5f);
        checkboxRect.anchorMax = new Vector2(0f, 0.5f);
        checkboxRect.sizeDelta = new Vector2(24f, 24f);
        checkboxRect.anchoredPosition = new Vector2(24f, 0f);
        checkbox.GetComponent<Image>().color = Surface;
        checkbox.GetComponent<Outline>().effectColor = Border;
        GameObject check = CreateUiObject("Selected", checkbox.transform, typeof(Image));
        Stretch(check.GetComponent<RectTransform>(), new Vector2(4f, 4f),
            new Vector2(-4f, -4f));
        Image checkImage = check.GetComponent<Image>();
        checkImage.color = Primary;
        TMP_Text text = CreateText(row.transform, "Label", label, font, 21,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            TextAlignmentOptions.MidlineLeft);
        Stretch(text.rectTransform, new Vector2(50f, 4f), new Vector2(-12f, -4f));
        Toggle toggle = row.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = checkImage;
        toggle.isOn = selectedExperienceSkillIds.Contains(id);
        toggle.onValueChanged.AddListener(on =>
        {
            if (on) selectedExperienceSkillIds.Add(id);
            else selectedExperienceSkillIds.Remove(id);
            UpdateExperienceSkillSummary();
        });
    }

    private static List<CareerProject> CloneProjects(IEnumerable<CareerProject> source)
    {
        return (source ?? Enumerable.Empty<CareerProject>())
            .Where(item => item != null)
            .Select(item => new CareerProject
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Technologies = item.Technologies == null
                    ? new List<string>() : new List<string>(item.Technologies),
                Url = item.Url
            })
            .ToList();
    }

    private static string ProjectTechnologyKey(string value) =>
        RequirementCatalog.NormalizeSkill(value);

    private void RenderProjectDraft()
    {
        if (projectListRoot == null) return;
        foreach (Transform child in projectListRoot.Cast<Transform>().ToArray())
        {
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        TMP_FontAsset font = ResolveFontAsset();
        if (projectDraft == null || projectDraft.Count == 0)
        {
            TMP_Text empty = CreateText(projectListRoot, "Empty", "尚未新增專案經歷",
                font, 20, new Vector2(0f, 1f), Vector2.zero,
                new Vector2(0f, 36f), TextAlignmentOptions.MidlineLeft);
            empty.color = TextSecondary;
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            return;
        }

        for (int index = 0; index < projectDraft.Count; index++)
        {
            int projectIndex = index;
            CareerProject item = projectDraft[index];
            GameObject row = CreateUiObject("Project_" + index, projectListRoot,
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
            row.AddComponent<LayoutElement>().preferredHeight = 78f;

            TMP_Text label = CreateText(row.transform, "Summary",
                Join("｜", item.Name, item.Description), font, 21,
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 52f),
                TextAlignmentOptions.MidlineLeft);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.minWidth = 120f;
            CreateLayoutButton(row.transform, "Button_Edit", "編輯", font, 100f,
                ButtonTone.Secondary).onClick.AddListener(() => OpenProjectForm(projectIndex));
            CreateLayoutButton(row.transform, "Button_Remove", "移除", font, 100f,
                ButtonTone.Secondary).onClick.AddListener(() => RemoveProject(projectIndex));
        }
    }

    private void OpenProjectForm(int index)
    {
        editingProjectIndex = index;
        CareerProject item = index >= 0 && projectDraft != null
            && index < projectDraft.Count ? projectDraft[index] : null;
        projectNameInput.text = item?.Name ?? string.Empty;
        projectDescriptionInput.text = item?.Description ?? string.Empty;
        projectUrlInput.text = item?.Url ?? string.Empty;
        selectedProjectTechnologies.Clear();
        foreach (string technology in item?.Technologies ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(technology))
                selectedProjectTechnologies.Add(technology.Trim());
        }
        RenderProjectSkillChoices();
        projectSkillChoices.SetActive(false);
        projectFormRoot.SetActive(true);
        SetEditorStatus("", false);
    }

    private void CloseProjectForm()
    {
        editingProjectIndex = -1;
        selectedProjectTechnologies.Clear();
        if (projectFormRoot != null) projectFormRoot.SetActive(false);
        if (projectSkillChoices != null) projectSkillChoices.SetActive(false);
    }

    private void RemoveProject(int index)
    {
        if (projectDraft == null || index < 0 || index >= projectDraft.Count) return;
        projectDraft.RemoveAt(index);
        CloseProjectForm();
        RenderProjectDraft();
        SetEditorStatus("專案經歷已從本次編輯移除；按整頁儲存才會寫入。", false);
    }

    private bool ApplyProjectForm()
    {
        string name = projectNameInput.text.Trim();
        if (name.Length == 0)
        {
            SetEditorStatus("請先填寫專案名稱。", true);
            return false;
        }

        CareerProject existing = editingProjectIndex >= 0
            && editingProjectIndex < projectDraft.Count
                ? projectDraft[editingProjectIndex] : null;
        CareerProject saved = new CareerProject
        {
            Id = existing?.Id ?? CareerProfileIdGenerator.CreateProjectId(),
            Name = name,
            Description = projectDescriptionInput.text.Trim(),
            Technologies = selectedProjectTechnologies
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            Url = projectUrlInput.text.Trim()
        };
        if (existing == null) projectDraft.Add(saved);
        else projectDraft[editingProjectIndex] = saved;
        CloseProjectForm();
        RenderProjectDraft();
        SetEditorStatus("專案經歷已暫存；按整頁儲存才會寫入。", false);
        return true;
    }

    private void UpdateProjectSkillSummary()
    {
        if (projectSkillSummary == null) return;
        projectSkillSummary.text = selectedProjectTechnologies.Count == 0
            ? "選擇使用技能（可不選）"
            : "已選：" + string.Join("、", selectedProjectTechnologies
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
    }

    private void RenderProjectSkillChoices()
    {
        if (projectSkillChoices == null) return;
        Transform root = projectSkillChoices.transform;
        foreach (Transform child in root.Cast<Transform>().ToArray())
        {
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
        TMP_FontAsset font = ResolveFontAsset();
        var choices = (skillDraft ?? new List<CareerSkill>())
            .Where(skill => !string.IsNullOrWhiteSpace(skill?.Name)
                && ExperienceSkillId(skill).Length > 0)
            .GroupBy(ExperienceSkillId).Select(group => group.First()).ToList();
        if (choices.Count == 0 && selectedProjectTechnologies.Count == 0)
        {
            TMP_Text empty = CreateText(root, "Empty", "尚無技能，可先到技能區新增。",
                font, 20, new Vector2(0f, 1f), Vector2.zero,
                new Vector2(0f, 42f), TextAlignmentOptions.MidlineLeft);
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
        }
        foreach (CareerSkill skill in choices)
        {
            CreateProjectSkillOption(root, font, skill.Name);
        }
        foreach (string technology in selectedProjectTechnologies
            .Where(value => choices.All(skill =>
                ExperienceSkillId(skill) != ProjectTechnologyKey(value))).ToArray())
        {
            CreateProjectSkillOption(root, font, technology);
        }
        UpdateProjectSkillSummary();
    }

    private void CreateProjectSkillOption(Transform root, TMP_FontAsset font, string label)
    {
        string key = ProjectTechnologyKey(label);
        GameObject row = CreateUiObject("Technology_" + key, root,
            typeof(Image), typeof(Toggle));
        Image background = row.GetComponent<Image>();
        background.color = Surface;
        row.AddComponent<LayoutElement>().preferredHeight = 50f;
        GameObject checkbox = CreateUiObject("Checkbox", row.transform,
            typeof(Image), typeof(Outline));
        RectTransform checkboxRect = checkbox.GetComponent<RectTransform>();
        checkboxRect.anchorMin = new Vector2(0f, 0.5f);
        checkboxRect.anchorMax = new Vector2(0f, 0.5f);
        checkboxRect.sizeDelta = new Vector2(24f, 24f);
        checkboxRect.anchoredPosition = new Vector2(24f, 0f);
        checkbox.GetComponent<Image>().color = Surface;
        checkbox.GetComponent<Outline>().effectColor = Border;
        GameObject check = CreateUiObject("Selected", checkbox.transform, typeof(Image));
        Stretch(check.GetComponent<RectTransform>(), new Vector2(4f, 4f),
            new Vector2(-4f, -4f));
        Image checkImage = check.GetComponent<Image>();
        checkImage.color = Primary;
        TMP_Text text = CreateText(row.transform, "Label", label, font, 21,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            TextAlignmentOptions.MidlineLeft);
        Stretch(text.rectTransform, new Vector2(50f, 4f), new Vector2(-12f, -4f));
        Toggle toggle = row.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = checkImage;
        toggle.isOn = selectedProjectTechnologies.Any(value =>
            ProjectTechnologyKey(value) == key);
        toggle.onValueChanged.AddListener(on =>
        {
            selectedProjectTechnologies.RemoveWhere(value =>
                ProjectTechnologyKey(value) == key);
            if (on) selectedProjectTechnologies.Add(label);
            UpdateProjectSkillSummary();
        });
    }

    private static List<CareerEducation> CloneEducations(IEnumerable<CareerEducation> source)
    {
        return (source ?? Enumerable.Empty<CareerEducation>())
            .Where(item => item != null)
            .Select(item => new CareerEducation
            {
                Id = item.Id,
                Institution = item.Institution,
                Program = item.Program,
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                Notes = item.Notes,
                DegreeLevel = item.DegreeLevel,
                CompletionStatus = item.CompletionStatus,
                FieldTags = item.FieldTags == null
                    ? new List<string>() : new List<string>(item.FieldTags)
            }).ToList();
    }

    private void RenderEducationDraft()
    {
        if (educationListRoot == null) return;
        foreach (Transform child in educationListRoot.Cast<Transform>().ToArray())
        {
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
        TMP_FontAsset font = ResolveFontAsset();
        if (educationDraft == null || educationDraft.Count == 0)
        {
            TMP_Text empty = CreateText(educationListRoot, "Empty", "尚未新增學歷", font, 20,
                new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 36f),
                TextAlignmentOptions.MidlineLeft);
            empty.color = TextSecondary;
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            return;
        }
        for (int index = 0; index < educationDraft.Count; index++)
        {
            int educationIndex = index;
            CareerEducation item = educationDraft[index];
            GameObject row = CreateUiObject("Education_" + index, educationListRoot,
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
            TMP_Text label = CreateText(row.transform, "Summary",
                Join("｜", item.Institution, item.Program, DegreeLabel(item.DegreeLevel),
                    CompletionLabel(item.CompletionStatus)), font, 21,
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 52f),
                TextAlignmentOptions.MidlineLeft);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.minWidth = 120f;
            CreateLayoutButton(row.transform, "Button_Edit", "編輯", font, 100f,
                ButtonTone.Secondary).onClick.AddListener(() => OpenEducationForm(educationIndex));
            CreateLayoutButton(row.transform, "Button_Remove", "移除", font, 100f,
                ButtonTone.Secondary).onClick.AddListener(() => RemoveEducation(educationIndex));
        }
    }

    private void OpenEducationForm(int index)
    {
        editingEducationIndex = index;
        CareerEducation item = index >= 0 && educationDraft != null
            && index < educationDraft.Count ? educationDraft[index] : null;
        educationInstitutionInput.text = item?.Institution ?? string.Empty;
        educationProgramInput.text = item?.Program ?? string.Empty;
        educationDegreeDropdown.SetValueWithoutNotify(item?.DegreeLevel.HasValue == true
            && item.DegreeLevel.Value >= DegreeLevel.HighSchool
            && item.DegreeLevel.Value <= DegreeLevel.Doctorate
                ? (int)item.DegreeLevel.Value : 0);
        educationStatusDropdown.SetValueWithoutNotify((int)(item?.CompletionStatus
            ?? EducationCompletionStatus.Unknown));
        SetExperienceMonthFields(item?.StartDate, educationStartYearInput,
            educationStartMonthDropdown);
        SetExperienceMonthFields(item?.EndDate, educationEndYearInput,
            educationEndMonthDropdown);
        educationFieldTagsInput.text = item?.FieldTags == null
            ? string.Empty : string.Join("、", item.FieldTags);
        educationNotesInput.text = item?.Notes ?? string.Empty;
        educationFormRoot.SetActive(true);
        SetEditorStatus("", false);
    }

    private void CloseEducationForm()
    {
        editingEducationIndex = -1;
        if (educationFormRoot != null) educationFormRoot.SetActive(false);
    }

    private void RemoveEducation(int index)
    {
        if (educationDraft == null || index < 0 || index >= educationDraft.Count) return;
        educationDraft.RemoveAt(index);
        CloseEducationForm();
        RenderEducationDraft();
        SetEditorStatus("學歷已從本次編輯移除；按整頁儲存才會寫入。", false);
    }

    private bool ApplyEducationForm()
    {
        string institution = educationInstitutionInput.text.Trim();
        if (institution.Length == 0)
        {
            SetEditorStatus("請先填寫學校／機構。", true);
            return false;
        }
        bool hasStart = educationStartYearInput.text.Trim().Length > 0
            || educationStartMonthDropdown.value > 0;
        bool hasEnd = educationEndYearInput.text.Trim().Length > 0
            || educationEndMonthDropdown.value > 0;
        DateTime start = default(DateTime);
        DateTime end = default(DateTime);
        if (hasStart && !TryReadExperienceMonth(educationStartYearInput,
            educationStartMonthDropdown, out start))
        {
            SetEditorStatus("請填寫有效的開始年月，或兩欄都留空。", true);
            return false;
        }
        if (hasEnd && !TryReadExperienceMonth(educationEndYearInput,
            educationEndMonthDropdown, out end))
        {
            SetEditorStatus("請填寫有效的結束年月，或兩欄都留空。", true);
            return false;
        }
        if (hasStart && hasEnd && end < start)
        {
            SetEditorStatus("結束年月不得早於開始年月。", true);
            return false;
        }
        CareerEducation existing = editingEducationIndex >= 0
            && editingEducationIndex < educationDraft.Count
                ? educationDraft[editingEducationIndex] : null;
        CareerEducation saved = new CareerEducation
        {
            Id = existing?.Id ?? CareerProfileIdGenerator.CreateEducationId(),
            Institution = institution,
            Program = educationProgramInput.text.Trim(),
            DegreeLevel = educationDegreeDropdown.value == 0
                ? (DegreeLevel?)null : (DegreeLevel)educationDegreeDropdown.value,
            CompletionStatus = (EducationCompletionStatus)educationStatusDropdown.value,
            StartDate = hasStart ? start.ToString("yyyy/MM", CultureInfo.InvariantCulture) : null,
            EndDate = hasEnd ? end.ToString("yyyy/MM", CultureInfo.InvariantCulture) : null,
            FieldTags = educationFieldTagsInput.text
                .Split(new[] { ',', '，', '、', ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim()).Where(tag => tag.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Notes = educationNotesInput.text.Trim()
        };
        if (existing == null) educationDraft.Add(saved);
        else educationDraft[editingEducationIndex] = saved;
        CloseEducationForm();
        RenderEducationDraft();
        SetEditorStatus("學歷已暫存；按整頁儲存才會寫入。", false);
        return true;
    }

    private static List<CareerLanguage> CloneLanguages(IEnumerable<CareerLanguage> source)
    {
        return (source ?? Enumerable.Empty<CareerLanguage>())
            .Where(item => item != null)
            .Select(item => new CareerLanguage
            {
                Id = item.Id,
                Name = item.Name,
                Level = item.Level,
                Notes = item.Notes,
                LanguageId = item.LanguageId,
                Proficiency = item.Proficiency,
                Certifications = item.Certifications == null
                    ? new List<string>() : new List<string>(item.Certifications)
            })
            .ToList();
    }

    private static string LanguageLevelLabel(LanguageProficiency? value, string legacyLevel)
    {
        if (!value.HasValue) return ValueOrEmpty(legacyLevel, "程度未填寫");
        if (value.Value == LanguageProficiency.None) return "不會";
        if (value.Value <= LanguageProficiency.Elementary) return "略懂";
        if (value.Value <= LanguageProficiency.UpperIntermediate) return "中等";
        return "精通";
    }

    private static int LanguageLevelOption(LanguageProficiency? value)
    {
        if (!value.HasValue) return 0;
        if (value.Value == LanguageProficiency.None) return 1;
        if (value.Value <= LanguageProficiency.Elementary) return 2;
        if (value.Value <= LanguageProficiency.UpperIntermediate) return 3;
        return 4;
    }

    private static int LanguageNameOption(CareerLanguage item)
    {
        string id = RequirementCatalog.NormalizeLanguage(
            string.IsNullOrWhiteSpace(item?.LanguageId) ? item?.Name : item.LanguageId);
        int index = Array.IndexOf(LanguageIds, id);
        return index < 0 ? 0 : index + 1;
    }

    private void RenderLanguageDraft()
    {
        if (languageListRoot == null) return;
        foreach (Transform child in languageListRoot.Cast<Transform>().ToArray())
        {
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        TMP_FontAsset font = ResolveFontAsset();
        if (languageDraft == null || languageDraft.Count == 0)
        {
            TMP_Text empty = CreateText(languageListRoot, "Empty", "尚未新增語言能力", font, 20,
                new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 36f),
                TextAlignmentOptions.MidlineLeft);
            empty.color = TextSecondary;
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            return;
        }

        for (int index = 0; index < languageDraft.Count; index++)
        {
            int languageIndex = index;
            CareerLanguage language = languageDraft[index];
            GameObject row = CreateUiObject("Language_" + index, languageListRoot,
                typeof(Image), typeof(HorizontalLayoutGroup));
            Image image = row.GetComponent<Image>();
            image.color = Surface;
            ApplySlicedSprite(image);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 12, 8, 8);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            row.AddComponent<LayoutElement>().preferredHeight = 68f;

            TMP_Text label = CreateText(row.transform, "Summary",
                Join("｜", language.Name,
                    LanguageLevelLabel(language.Proficiency, language.Level)), font, 21,
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 52f),
                TextAlignmentOptions.MidlineLeft);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.minWidth = 120f;
            CreateLayoutButton(row.transform, "Button_Edit", "編輯", font, 100f,
                ButtonTone.Secondary).onClick.AddListener(() => OpenLanguageForm(languageIndex));
            CreateLayoutButton(row.transform, "Button_Remove", "移除", font, 100f,
                ButtonTone.Secondary).onClick.AddListener(() => RemoveLanguage(languageIndex));
        }
    }

    private void OpenLanguageForm(int index)
    {
        editingLanguageIndex = index;
        CareerLanguage item = index >= 0 && languageDraft != null && index < languageDraft.Count
            ? languageDraft[index] : null;
        languageNameDropdown.SetValueWithoutNotify(LanguageNameOption(item));
        editingLanguageInitialLevelOption = LanguageLevelOption(item?.Proficiency);
        languageLevelDropdown.SetValueWithoutNotify(editingLanguageInitialLevelOption);
        languageFormRoot.SetActive(true);
        SetEditorStatus("", false);
    }

    private void CloseLanguageForm()
    {
        editingLanguageIndex = -1;
        if (languageFormRoot != null) languageFormRoot.SetActive(false);
    }

    private void RemoveLanguage(int index)
    {
        if (languageDraft == null || index < 0 || index >= languageDraft.Count) return;
        languageDraft.RemoveAt(index);
        CloseLanguageForm();
        RenderLanguageDraft();
        SetEditorStatus("語言能力已從本次編輯移除；按整頁儲存才會寫入。", false);
    }

    private bool ApplyLanguageForm()
    {
        int nameOption = languageNameDropdown.value;
        int levelOption = languageLevelDropdown.value;
        if (nameOption == 0 || levelOption == 0)
        {
            SetEditorStatus("請選擇語言和程度。", true);
            return false;
        }

        string languageId = LanguageIds[nameOption - 1];
        if (languageDraft.Where((item, index) => index != editingLanguageIndex)
            .Any(item => string.Equals(RequirementCatalog.NormalizeLanguage(
                string.IsNullOrWhiteSpace(item.LanguageId) ? item.Name : item.LanguageId),
                languageId, StringComparison.Ordinal)))
        {
            SetEditorStatus("這項語言已存在，請編輯原有項目。", true);
            return false;
        }

        CareerLanguage existing = editingLanguageIndex >= 0
            && editingLanguageIndex < languageDraft.Count
                ? languageDraft[editingLanguageIndex] : null;
        CareerLanguage saved = new CareerLanguage
        {
            Id = existing?.Id ?? CareerProfileIdGenerator.CreateLanguageId(),
            Name = LanguageNames[nameOption - 1],
            LanguageId = languageId,
            Proficiency = existing != null && levelOption == editingLanguageInitialLevelOption
                ? existing.Proficiency : LanguageLevels[levelOption - 1],
            Level = existing?.Level,
            Notes = existing?.Notes,
            Certifications = existing?.Certifications == null
                ? new List<string>() : new List<string>(existing.Certifications)
        };
        if (existing == null) languageDraft.Add(saved);
        else languageDraft[editingLanguageIndex] = saved;
        CloseLanguageForm();
        RenderLanguageDraft();
        SetEditorStatus("語言能力已暫存；按整頁儲存才會寫入。", false);
        return true;
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
        if (experienceFormRoot != null && experienceFormRoot.activeSelf)
            RenderExperienceSkillChoices();
        if (projectFormRoot != null && projectFormRoot.activeSelf)
            RenderProjectSkillChoices();
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
        if (experienceFormRoot != null && experienceFormRoot.activeSelf)
            RenderExperienceSkillChoices();
        if (projectFormRoot != null && projectFormRoot.activeSelf)
            RenderProjectSkillChoices();
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
            item => Join("｜", item.Name, LanguageLevelLabel(item.Proficiency, item.Level)),
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
            item => Join("｜", item.Name, LanguageLevelLabel(item.Proficiency, item.Level)),
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
        CreateLinkEditorUi(content, font);
        CreateExperienceEditorUi(content, font);
        CreateProjectEditorUi(content, font);
        CreateEducationEditorUi(content, font);
        CreateLanguageEditorUi(content, font);

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

    private void CreateExperienceEditorUi(Transform parent, TMP_FontAsset font)
    {
        GameObject card = CreateUiObject("Field_experiences", parent,
            typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(Outline));
        card.GetComponent<Image>().color = SurfaceMuted;
        ApplySlicedSprite(card.GetComponent<Image>());
        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(1f, -1f);
        VerticalLayoutGroup cardLayout = card.GetComponent<VerticalLayoutGroup>();
        int padding = Mathf.RoundToInt(theme != null ? theme.SpaceMd : 16f);
        cardLayout.padding = new RectOffset(padding, padding, padding, padding);
        cardLayout.spacing = theme != null ? theme.SpaceSm : 12f;
        cardLayout.childAlignment = TextAnchor.UpperLeft;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;
        card.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text title = CreateText(card.transform, "Label_experiences", "工作經歷",
            font, Mathf.RoundToInt(theme != null ? theme.SectionTitleSize : 28f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 38f),
            TextAlignmentOptions.MidlineLeft);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
        TMP_Text help = CreateText(card.transform, "Help_experiences",
            "逐筆填寫，年資由起迄年月計算；關聯技能可不選。", font,
            Mathf.RoundToInt(theme != null ? theme.SupportingTextSize : 20f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 32f),
            TextAlignmentOptions.MidlineLeft);
        help.color = TextSecondary;
        help.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;

        GameObject list = CreateUiObject("ExperienceList", card.transform,
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        experienceListRoot = list.transform;
        VerticalLayoutGroup listLayout = list.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 8f;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        list.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
        CreateLayoutButton(card.transform, "Button_AddExperience", "新增工作經歷",
            font, 210f, ButtonTone.Primary).onClick.AddListener(
                () => OpenExperienceForm(-1));

        experienceFormRoot = CreateUiObject("ExperienceForm", card.transform,
            typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        experienceFormRoot.GetComponent<Image>().color = Surface;
        VerticalLayoutGroup formLayout = experienceFormRoot.GetComponent<VerticalLayoutGroup>();
        formLayout.padding = new RectOffset(16, 16, 16, 16);
        formLayout.spacing = 8f;
        formLayout.childControlWidth = true;
        formLayout.childControlHeight = true;
        formLayout.childForceExpandWidth = true;
        formLayout.childForceExpandHeight = false;
        experienceFormRoot.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        experienceOrganizationInput = CreateLabeledSkillInput(experienceFormRoot.transform,
            "ExperienceOrganization", "公司／組織", "例如：某某科技", font, 56f, true);
        experienceRoleInput = CreateLabeledSkillInput(experienceFormRoot.transform,
            "ExperienceRole", "職務", "例如：軟體工程師", font, 56f, true);
        experienceStartYearInput = CreateLabeledSkillInput(experienceFormRoot.transform,
            "ExperienceStartYear", "開始年份", "例如：2022", font, 56f, true);
        experienceStartYearInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        experienceStartMonthDropdown = CreateLabeledSkillDropdown(
            experienceFormRoot.transform, "ExperienceStartMonth", "開始月份", font,
            new[] { "請選擇月份" }.Concat(Enumerable.Range(1, 12)
                .Select(month => month + " 月")).ToArray());

        GameObject currentRow = CreateUiObject("CurrentEmployment", experienceFormRoot.transform,
            typeof(Image), typeof(Toggle));
        currentRow.GetComponent<Image>().color = SurfaceMuted;
        currentRow.AddComponent<LayoutElement>().preferredHeight = 56f;
        GameObject checkbox = CreateUiObject("Checkbox", currentRow.transform,
            typeof(Image), typeof(Outline));
        RectTransform checkboxRect = checkbox.GetComponent<RectTransform>();
        checkboxRect.anchorMin = new Vector2(0f, 0.5f);
        checkboxRect.anchorMax = new Vector2(0f, 0.5f);
        checkboxRect.sizeDelta = new Vector2(28f, 28f);
        checkboxRect.anchoredPosition = new Vector2(30f, 0f);
        checkbox.GetComponent<Image>().color = Surface;
        checkbox.GetComponent<Outline>().effectColor = Border;
        GameObject check = CreateUiObject("Selected", checkbox.transform, typeof(Image));
        Stretch(check.GetComponent<RectTransform>(), new Vector2(4f, 4f),
            new Vector2(-4f, -4f));
        Image checkImage = check.GetComponent<Image>();
        checkImage.color = Primary;
        TMP_Text currentLabel = CreateText(currentRow.transform, "Label", "目前仍在職",
            font, 21, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            TextAlignmentOptions.MidlineLeft);
        Stretch(currentLabel.rectTransform, new Vector2(56f, 4f), new Vector2(-12f, -4f));
        experienceCurrentToggle = currentRow.GetComponent<Toggle>();
        experienceCurrentToggle.targetGraphic = currentRow.GetComponent<Image>();
        experienceCurrentToggle.graphic = checkImage;

        experienceEndFields = CreateUiObject("ExperienceEndFields", experienceFormRoot.transform,
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        VerticalLayoutGroup endLayout = experienceEndFields.GetComponent<VerticalLayoutGroup>();
        endLayout.spacing = 8f;
        endLayout.childControlWidth = true;
        endLayout.childControlHeight = true;
        endLayout.childForceExpandWidth = true;
        endLayout.childForceExpandHeight = false;
        experienceEndFields.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
        experienceEndYearInput = CreateLabeledSkillInput(experienceEndFields.transform,
            "ExperienceEndYear", "結束年份", "例如：2025", font, 56f, true);
        experienceEndYearInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        experienceEndMonthDropdown = CreateLabeledSkillDropdown(
            experienceEndFields.transform, "ExperienceEndMonth", "結束月份", font,
            new[] { "請選擇月份" }.Concat(Enumerable.Range(1, 12)
                .Select(month => month + " 月")).ToArray());
        experienceCurrentToggle.onValueChanged.AddListener(
            current => experienceEndFields.SetActive(!current));

        experienceDescriptionInput = CreateLabeledSkillInput(experienceFormRoot.transform,
            "ExperienceDescription", "工作內容（選填）", "說明實際負責的工作與成果",
            font, 144f, false);
        TMP_Text skillLabel = CreateText(experienceFormRoot.transform,
            "Label_ExperienceSkills", "使用技能（選填）", font, 20,
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 30f),
            TextAlignmentOptions.MidlineLeft);
        skillLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        Button skillButton = CreateLayoutButton(experienceFormRoot.transform,
            "Button_ExperienceSkills", "選擇關聯技能（可不選）", font,
            260f, ButtonTone.Secondary);
        experienceSkillSummary = skillButton.GetComponentInChildren<TMP_Text>();
        experienceSkillSummary.fontSize = 20;
        experienceSkillSummary.enableWordWrapping = false;
        experienceSkillSummary.overflowMode = TextOverflowModes.Ellipsis;
        skillButton.onClick.AddListener(() =>
            experienceSkillChoices.SetActive(!experienceSkillChoices.activeSelf));
        experienceSkillChoices = CreateUiObject("ExperienceSkillChoices",
            experienceFormRoot.transform, typeof(Image), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        experienceSkillChoices.GetComponent<Image>().color = SurfaceMuted;
        VerticalLayoutGroup choicesLayout =
            experienceSkillChoices.GetComponent<VerticalLayoutGroup>();
        choicesLayout.padding = new RectOffset(8, 8, 8, 8);
        choicesLayout.spacing = 4f;
        choicesLayout.childControlWidth = true;
        choicesLayout.childControlHeight = true;
        choicesLayout.childForceExpandWidth = true;
        choicesLayout.childForceExpandHeight = false;
        experienceSkillChoices.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
        experienceSkillChoices.SetActive(false);

        GameObject actions = CreateUiObject("ExperienceActions", experienceFormRoot.transform,
            typeof(HorizontalLayoutGroup));
        HorizontalLayoutGroup actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
        actionsLayout.spacing = 12f;
        actionsLayout.childAlignment = TextAnchor.MiddleRight;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = true;
        actionsLayout.childForceExpandWidth = false;
        actionsLayout.childForceExpandHeight = true;
        actions.AddComponent<LayoutElement>().preferredHeight = 56f;
        CreateLayoutButton(actions.transform, "Button_CancelExperience", "取消", font,
            120f, ButtonTone.Secondary).onClick.AddListener(CloseExperienceForm);
        CreateLayoutButton(actions.transform, "Button_SaveExperience", "儲存這筆", font,
            150f, ButtonTone.Primary).onClick.AddListener(() => ApplyExperienceForm());
        experienceFormRoot.SetActive(false);
    }

    private void CreateProjectEditorUi(Transform parent, TMP_FontAsset font)
    {
        GameObject card = CreateUiObject("Field_projects", parent,
            typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(Outline));
        card.GetComponent<Image>().color = SurfaceMuted;
        ApplySlicedSprite(card.GetComponent<Image>());
        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(1f, -1f);
        VerticalLayoutGroup cardLayout = card.GetComponent<VerticalLayoutGroup>();
        int padding = Mathf.RoundToInt(theme != null ? theme.SpaceMd : 16f);
        cardLayout.padding = new RectOffset(padding, padding, padding, padding);
        cardLayout.spacing = theme != null ? theme.SpaceSm : 12f;
        cardLayout.childAlignment = TextAnchor.UpperLeft;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;
        card.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text title = CreateText(card.transform, "Label_projects", "專案經歷",
            font, Mathf.RoundToInt(theme != null ? theme.SectionTitleSize : 28f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 38f),
            TextAlignmentOptions.MidlineLeft);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
        TMP_Text help = CreateText(card.transform, "Help_projects",
            "逐筆記錄做了什麼與成果；技能和網址可不填。", font,
            Mathf.RoundToInt(theme != null ? theme.SupportingTextSize : 20f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 32f),
            TextAlignmentOptions.MidlineLeft);
        help.color = TextSecondary;
        help.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;

        GameObject list = CreateUiObject("ProjectList", card.transform,
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        projectListRoot = list.transform;
        VerticalLayoutGroup listLayout = list.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 8f;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        list.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
        CreateLayoutButton(card.transform, "Button_AddProject", "新增專案經歷",
            font, 210f, ButtonTone.Primary).onClick.AddListener(
                () => OpenProjectForm(-1));

        projectFormRoot = CreateUiObject("ProjectForm", card.transform,
            typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        projectFormRoot.GetComponent<Image>().color = Surface;
        VerticalLayoutGroup formLayout = projectFormRoot.GetComponent<VerticalLayoutGroup>();
        formLayout.padding = new RectOffset(16, 16, 16, 16);
        formLayout.spacing = 8f;
        formLayout.childControlWidth = true;
        formLayout.childControlHeight = true;
        formLayout.childForceExpandWidth = true;
        formLayout.childForceExpandHeight = false;
        projectFormRoot.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        projectNameInput = CreateLabeledSkillInput(projectFormRoot.transform,
            "ProjectName", "專案名稱", "例如：JobCheck", font, 56f, true);
        projectDescriptionInput = CreateLabeledSkillInput(projectFormRoot.transform,
            "ProjectDescription", "簡述／成果（選填）",
            "說明做了什麼、負責哪些部分，以及完成的成果", font, 168f, false);
        TMP_Text skillLabel = CreateText(projectFormRoot.transform,
            "Label_ProjectSkills", "使用技能（選填）", font, 20,
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 30f),
            TextAlignmentOptions.MidlineLeft);
        skillLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        Button skillButton = CreateLayoutButton(projectFormRoot.transform,
            "Button_ProjectSkills", "選擇使用技能（可不選）", font,
            260f, ButtonTone.Secondary);
        projectSkillSummary = skillButton.GetComponentInChildren<TMP_Text>();
        projectSkillSummary.fontSize = 20;
        projectSkillSummary.enableWordWrapping = false;
        projectSkillSummary.overflowMode = TextOverflowModes.Ellipsis;
        skillButton.onClick.AddListener(() =>
            projectSkillChoices.SetActive(!projectSkillChoices.activeSelf));
        projectSkillChoices = CreateUiObject("ProjectSkillChoices",
            projectFormRoot.transform, typeof(Image), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        projectSkillChoices.GetComponent<Image>().color = SurfaceMuted;
        VerticalLayoutGroup choicesLayout =
            projectSkillChoices.GetComponent<VerticalLayoutGroup>();
        choicesLayout.padding = new RectOffset(8, 8, 8, 8);
        choicesLayout.spacing = 4f;
        choicesLayout.childControlWidth = true;
        choicesLayout.childControlHeight = true;
        choicesLayout.childForceExpandWidth = true;
        choicesLayout.childForceExpandHeight = false;
        projectSkillChoices.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
        projectSkillChoices.SetActive(false);

        projectUrlInput = CreateLabeledSkillInput(projectFormRoot.transform,
            "ProjectUrl", "專案網址（選填）", "例如：https://github.com/...",
            font, 56f, true);
        GameObject actions = CreateUiObject("ProjectActions", projectFormRoot.transform,
            typeof(HorizontalLayoutGroup));
        HorizontalLayoutGroup actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
        actionsLayout.spacing = 12f;
        actionsLayout.childAlignment = TextAnchor.MiddleRight;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = true;
        actionsLayout.childForceExpandWidth = false;
        actionsLayout.childForceExpandHeight = true;
        actions.AddComponent<LayoutElement>().preferredHeight = 56f;
        CreateLayoutButton(actions.transform, "Button_CancelProject", "取消", font,
            120f, ButtonTone.Secondary).onClick.AddListener(CloseProjectForm);
        CreateLayoutButton(actions.transform, "Button_SaveProject", "儲存這筆", font,
            150f, ButtonTone.Primary).onClick.AddListener(() => ApplyProjectForm());
        projectFormRoot.SetActive(false);
    }

    private void CreateEducationEditorUi(Transform parent, TMP_FontAsset font)
    {
        GameObject card = CreateUiObject("Field_educations", parent,
            typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(Outline));
        card.GetComponent<Image>().color = SurfaceMuted;
        ApplySlicedSprite(card.GetComponent<Image>());
        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(1f, -1f);
        VerticalLayoutGroup cardLayout = card.GetComponent<VerticalLayoutGroup>();
        int padding = Mathf.RoundToInt(theme != null ? theme.SpaceMd : 16f);
        cardLayout.padding = new RectOffset(padding, padding, padding, padding);
        cardLayout.spacing = theme != null ? theme.SpaceSm : 12f;
        cardLayout.childAlignment = TextAnchor.UpperLeft;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;
        card.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text title = CreateText(card.transform, "Label_educations", "學歷", font,
            Mathf.RoundToInt(theme != null ? theme.SectionTitleSize : 28f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 38f),
            TextAlignmentOptions.MidlineLeft);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
        TMP_Text help = CreateText(card.transform, "Help_educations",
            "逐筆填寫學校、學位與就學狀態；科系標籤可供職缺比對。", font,
            Mathf.RoundToInt(theme != null ? theme.SupportingTextSize : 20f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 32f),
            TextAlignmentOptions.MidlineLeft);
        help.color = TextSecondary;
        help.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;

        GameObject list = CreateUiObject("EducationList", card.transform,
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        educationListRoot = list.transform;
        VerticalLayoutGroup listLayout = list.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 8f;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        list.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
        CreateLayoutButton(card.transform, "Button_AddEducation", "新增學歷", font,
            170f, ButtonTone.Primary).onClick.AddListener(() => OpenEducationForm(-1));

        educationFormRoot = CreateUiObject("EducationForm", card.transform,
            typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        educationFormRoot.GetComponent<Image>().color = Surface;
        VerticalLayoutGroup formLayout = educationFormRoot.GetComponent<VerticalLayoutGroup>();
        formLayout.padding = new RectOffset(16, 16, 16, 16);
        formLayout.spacing = 8f;
        formLayout.childControlWidth = true;
        formLayout.childControlHeight = true;
        formLayout.childForceExpandWidth = true;
        formLayout.childForceExpandHeight = false;
        educationFormRoot.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        educationInstitutionInput = CreateLabeledSkillInput(educationFormRoot.transform,
            "EducationInstitution", "學校／機構", "例如：某某大學", font, 56f, true);
        educationProgramInput = CreateLabeledSkillInput(educationFormRoot.transform,
            "EducationProgram", "科系／學程（選填）", "例如：資訊工程學系", font, 56f, true);
        educationDegreeDropdown = CreateLabeledSkillDropdown(educationFormRoot.transform,
            "EducationDegree", "學位（選填）", font,
            new[] { "未填寫", "高中", "專科", "學士", "碩士", "博士" });
        educationStatusDropdown = CreateLabeledSkillDropdown(educationFormRoot.transform,
            "EducationStatus", "就學狀態（選填）", font,
            new[] { "未填寫", "在學", "已畢業", "未完成" });
        educationStartYearInput = CreateLabeledSkillInput(educationFormRoot.transform,
            "EducationStartYear", "開始年份（選填）", "例如：2020", font, 56f, true);
        educationStartYearInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        educationStartMonthDropdown = CreateLabeledSkillDropdown(educationFormRoot.transform,
            "EducationStartMonth", "開始月份（選填）", font,
            new[] { "未填寫" }.Concat(Enumerable.Range(1, 12)
                .Select(month => month + " 月")).ToArray());
        educationEndYearInput = CreateLabeledSkillInput(educationFormRoot.transform,
            "EducationEndYear", "結束年份（選填）", "例如：2024", font, 56f, true);
        educationEndYearInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        educationEndMonthDropdown = CreateLabeledSkillDropdown(educationFormRoot.transform,
            "EducationEndMonth", "結束月份（選填）", font,
            new[] { "未填寫" }.Concat(Enumerable.Range(1, 12)
                .Select(month => month + " 月")).ToArray());
        educationFieldTagsInput = CreateLabeledSkillInput(educationFormRoot.transform,
            "EducationFieldTags", "科系標籤（選填）",
            "例如：資訊工程、資訊管理", font, 56f, true);
        educationNotesInput = CreateLabeledSkillInput(educationFormRoot.transform,
            "EducationNotes", "備註（選填）", "其他需要補充的學歷資訊", font, 120f, false);

        GameObject actions = CreateUiObject("EducationActions", educationFormRoot.transform,
            typeof(HorizontalLayoutGroup));
        HorizontalLayoutGroup actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
        actionsLayout.spacing = 12f;
        actionsLayout.childAlignment = TextAnchor.MiddleRight;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = true;
        actionsLayout.childForceExpandWidth = false;
        actionsLayout.childForceExpandHeight = true;
        actions.AddComponent<LayoutElement>().preferredHeight = 56f;
        CreateLayoutButton(actions.transform, "Button_CancelEducation", "取消", font,
            120f, ButtonTone.Secondary).onClick.AddListener(CloseEducationForm);
        CreateLayoutButton(actions.transform, "Button_SaveEducation", "儲存這筆", font,
            150f, ButtonTone.Primary).onClick.AddListener(() => ApplyEducationForm());
        educationFormRoot.SetActive(false);
    }

    private void CreateLanguageEditorUi(Transform parent, TMP_FontAsset font)
    {
        GameObject cardObject = CreateUiObject("Field_languages", parent,
            typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(Outline));
        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = SurfaceMuted;
        ApplySlicedSprite(cardImage);
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

        TMP_Text title = CreateText(cardObject.transform, "Label_languages", "語言能力", font,
            Mathf.RoundToInt(theme != null ? theme.SectionTitleSize : 28f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 38f),
            TextAlignmentOptions.MidlineLeft);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
        TMP_Text help = CreateText(cardObject.transform, "Help_languages",
            "逐筆選擇語言與程度；未新增的語言不會自動判為不會。", font,
            Mathf.RoundToInt(theme != null ? theme.SupportingTextSize : 20f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 32f),
            TextAlignmentOptions.MidlineLeft);
        help.color = TextSecondary;
        help.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;

        GameObject list = CreateUiObject("LanguageList", cardObject.transform,
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        languageListRoot = list.transform;
        VerticalLayoutGroup listLayout = list.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 8f;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        list.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        CreateLayoutButton(cardObject.transform, "Button_AddLanguage", "新增語言", font,
            170f, ButtonTone.Primary).onClick.AddListener(() => OpenLanguageForm(-1));

        languageFormRoot = CreateUiObject("LanguageForm", cardObject.transform,
            typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        languageFormRoot.GetComponent<Image>().color = Surface;
        VerticalLayoutGroup formLayout = languageFormRoot.GetComponent<VerticalLayoutGroup>();
        formLayout.padding = new RectOffset(16, 16, 16, 16);
        formLayout.spacing = 8f;
        formLayout.childControlWidth = true;
        formLayout.childControlHeight = true;
        formLayout.childForceExpandWidth = true;
        formLayout.childForceExpandHeight = false;
        languageFormRoot.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        languageNameDropdown = CreateLabeledSkillDropdown(languageFormRoot.transform,
            "LanguageName", "語言", font, new[] { "請選擇語言", "中文", "英文", "日文" });
        languageLevelDropdown = CreateLabeledSkillDropdown(languageFormRoot.transform,
            "LanguageLevel", "程度", font,
            new[] { "請選擇程度", "不會", "略懂", "中等", "精通" });

        GameObject actions = CreateUiObject("LanguageActions", languageFormRoot.transform,
            typeof(HorizontalLayoutGroup));
        HorizontalLayoutGroup actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
        actionsLayout.spacing = 12f;
        actionsLayout.childAlignment = TextAnchor.MiddleRight;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = true;
        actionsLayout.childForceExpandWidth = false;
        actionsLayout.childForceExpandHeight = true;
        actions.AddComponent<LayoutElement>().preferredHeight = 56f;
        CreateLayoutButton(actions.transform, "Button_CancelLanguage", "取消", font,
            120f, ButtonTone.Secondary).onClick.AddListener(CloseLanguageForm);
        CreateLayoutButton(actions.transform, "Button_SaveLanguage", "儲存這筆", font,
            150f, ButtonTone.Primary).onClick.AddListener(() => ApplyLanguageForm());
        languageFormRoot.SetActive(false);
    }

    private void CreateLinkEditorUi(Transform parent, TMP_FontAsset font)
    {
        GameObject cardObject = CreateUiObject("Field_links", parent,
            typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(Outline));
        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = SurfaceMuted;
        ApplySlicedSprite(cardImage);
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

        TMP_Text title = CreateText(cardObject.transform, "Label_links", "連結", font,
            Mathf.RoundToInt(theme != null ? theme.SectionTitleSize : 28f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 38f),
            TextAlignmentOptions.MidlineLeft);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
        TMP_Text help = CreateText(cardObject.transform, "Help_links",
            "個人網站、作品集或其他參考網址。", font,
            Mathf.RoundToInt(theme != null ? theme.SupportingTextSize : 20f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 32f),
            TextAlignmentOptions.MidlineLeft);
        help.color = TextSecondary;
        help.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;

        GameObject list = CreateUiObject("LinkList", cardObject.transform,
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        linkListRoot = list.transform;
        VerticalLayoutGroup listLayout = list.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 8f;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        list.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        CreateLayoutButton(cardObject.transform, "Button_AddLink", "新增連結", font,
            170f, ButtonTone.Primary).onClick.AddListener(() => OpenLinkForm(-1));

        linkFormRoot = CreateUiObject("LinkForm", cardObject.transform,
            typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        linkFormRoot.GetComponent<Image>().color = Surface;
        VerticalLayoutGroup formLayout = linkFormRoot.GetComponent<VerticalLayoutGroup>();
        formLayout.padding = new RectOffset(16, 16, 16, 16);
        formLayout.spacing = 8f;
        formLayout.childControlWidth = true;
        formLayout.childControlHeight = true;
        formLayout.childForceExpandWidth = true;
        formLayout.childForceExpandHeight = false;
        linkFormRoot.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        linkLabelInput = CreateLabeledSkillInput(linkFormRoot.transform, "LinkLabel",
            "連結名稱（功用）", "例如：GitHub、作品集", font, 56f, true);
        linkUrlInput = CreateLabeledSkillInput(linkFormRoot.transform, "LinkUrl",
            "連結網址或路徑", "貼上網址或本機路徑", font, 56f, true);

        GameObject actions = CreateUiObject("LinkActions", linkFormRoot.transform,
            typeof(HorizontalLayoutGroup));
        HorizontalLayoutGroup actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
        actionsLayout.spacing = 12f;
        actionsLayout.childAlignment = TextAnchor.MiddleRight;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = true;
        actionsLayout.childForceExpandWidth = false;
        actionsLayout.childForceExpandHeight = true;
        actions.AddComponent<LayoutElement>().preferredHeight = 56f;
        CreateLayoutButton(actions.transform, "Button_CancelLink", "取消", font,
            120f, ButtonTone.Secondary).onClick.AddListener(CloseLinkForm);
        CreateLayoutButton(actions.transform, "Button_SaveLink", "儲存這筆", font,
            150f, ButtonTone.Primary).onClick.AddListener(() => ApplyLinkForm());
        linkFormRoot.SetActive(false);
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
            typeof(Image), typeof(JobCheckPopupDropdown));
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
        templateRect.pivot = new Vector2(0.5f, 0f);
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
        // TMP_Dropdown subtracts the item-to-content offset when calculating
        // popup height. A zero-height template content loses one entire row.
        contentRect.sizeDelta = new Vector2(0f, 48f);

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
        templateRect.SetParent(editorRoot.transform, false);
        templateRect.anchorMin = new Vector2(0.5f, 0.5f);
        templateRect.anchorMax = new Vector2(0.5f, 0.5f);
        templateRect.pivot = new Vector2(0.5f, 0f);
        templateRect.sizeDelta = new Vector2(0f, 260f);
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
