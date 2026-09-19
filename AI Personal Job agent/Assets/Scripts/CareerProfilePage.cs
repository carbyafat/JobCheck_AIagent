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

    private Button editButton;
    private GameObject editorRoot;
    private Text editorStatus;
    private readonly Dictionary<string, InputField> editorFields =
        new Dictionary<string, InputField>(StringComparer.Ordinal);

    private static readonly Color PanelColor = new Color(0.94f, 0.94f, 0.94f, 1f);
    private static readonly Color FieldColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color ButtonColor = new Color(0.55f, 0.32f, 0.34f, 1f);

    private void OnEnable()
    {
        EnsureEditorUi();
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
        editorFields["links"].text = Lines(CurrentProfile.Links,
            item => Join(" | ", item.Label, item.Url));
        editorFields["skills"].text = Lines(CurrentProfile.Skills,
            item => Join(" | ", item.Name, item.Notes));
        editorFields["experiences"].text = Lines(CurrentProfile.Experiences,
            item => Join(" | ", item.Organization, item.Role, item.StartDate,
                item.IsCurrent ? "至今" : item.EndDate, OneLine(item.Description)));
        editorFields["projects"].text = Lines(CurrentProfile.Projects,
            item => Join(" | ", item.Name,
                item.Technologies == null ? null : string.Join(", ", item.Technologies),
                item.Url, OneLine(item.Description)));
        editorFields["educations"].text = Lines(CurrentProfile.Educations,
            item => Join(" | ", item.Institution, item.Program, item.StartDate,
                item.EndDate, OneLine(item.Notes)));
        editorFields["languages"].text = Lines(CurrentProfile.Languages,
            item => Join(" | ", item.Name, item.Level, OneLine(item.Notes)));

        SetEditorStatus("", false);
        displayText.gameObject.SetActive(false);
        editButton.gameObject.SetActive(false);
        editorRoot.SetActive(true);
    }

    public void CancelEditor()
    {
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
    }

    public void SaveEditor()
    {
        if (CurrentProfile == null)
        {
            SetEditorStatus("沒有可儲存的履歷資料。", true);
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        var candidate = new CareerProfile
        {
            Id = CurrentProfile.Id,
            SchemaVersion = CareerProfile.CurrentSchemaVersion,
            Summary = editorFields["summary"].text,
            CreatedAt = CurrentProfile.CreatedAt ?? now,
            UpdatedAt = now,
            Links = ParseRows(editorFields["links"].text, 2, (values, index) =>
                new CareerProfileLink
                {
                    Id = ExistingId(CurrentProfile.Links, index, item => item.Id,
                        CareerProfileIdGenerator.CreateLinkId),
                    Label = values[0],
                    Url = values[1]
                }),
            Skills = ParseRows(editorFields["skills"].text, 2, (values, index) =>
                new CareerSkill
                {
                    Id = ExistingId(CurrentProfile.Skills, index, item => item.Id,
                        CareerProfileIdGenerator.CreateSkillId),
                    Name = values[0],
                    Notes = values[1]
                }),
            Experiences = ParseRows(editorFields["experiences"].text, 5, (values, index) =>
                new CareerExperience
                {
                    Id = ExistingId(CurrentProfile.Experiences, index, item => item.Id,
                        CareerProfileIdGenerator.CreateExperienceId),
                    Organization = values[0],
                    Role = values[1],
                    StartDate = values[2],
                    EndDate = IsCurrentValue(values[3]) ? null : values[3],
                    IsCurrent = IsCurrentValue(values[3]),
                    Description = values[4]
                }),
            Projects = ParseRows(editorFields["projects"].text, 4, (values, index) =>
                new CareerProject
                {
                    Id = ExistingId(CurrentProfile.Projects, index, item => item.Id,
                        CareerProfileIdGenerator.CreateProjectId),
                    Name = values[0],
                    Technologies = SplitCommaList(values[1]),
                    Url = values[2],
                    Description = values[3]
                }),
            Educations = ParseRows(editorFields["educations"].text, 5, (values, index) =>
                new CareerEducation
                {
                    Id = ExistingId(CurrentProfile.Educations, index, item => item.Id,
                        CareerProfileIdGenerator.CreateEducationId),
                    Institution = values[0],
                    Program = values[1],
                    StartDate = values[2],
                    EndDate = values[3],
                    Notes = values[4]
                }),
            Languages = ParseRows(editorFields["languages"].text, 3, (values, index) =>
                new CareerLanguage
                {
                    Id = ExistingId(CurrentProfile.Languages, index, item => item.Id,
                        CareerProfileIdGenerator.CreateLanguageId),
                    Name = values[0],
                    Level = values[1],
                    Notes = values[2]
                })
        };

        PersistenceStorageResult<CareerProfile> result = CareerProfileRepository.Save(
            ResolveProjectRelativePath(personalDataRootPath),
            candidate);
        if (!result.IsSuccess)
        {
            SetEditorStatus("儲存失敗：" + FormatIssues(result.Issues), true);
            return;
        }

        CurrentProfile = result.Value;
        SetDisplay(FormatProfile(CurrentProfile));
        CancelEditor();
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

    private static string Lines<T>(IEnumerable<T> items, Func<T, string> format)
        where T : class
    {
        return items == null
            ? string.Empty
            : string.Join("\n", items.Where(item => item != null).Select(format));
    }

    private static string OneLine(string value)
    {
        return string.IsNullOrEmpty(value)
            ? value
            : value.Replace("\r", " ").Replace("\n", " ");
    }

    private static List<T> ParseRows<T>(
        string source,
        int fieldCount,
        Func<string[], int, T> create)
    {
        var result = new List<T>();
        if (string.IsNullOrWhiteSpace(source))
        {
            return result;
        }

        string[] lines = source.Replace("\r\n", "\n").Split('\n');
        foreach (string rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            string[] rawValues = rawLine.Split('|');
            var values = new string[fieldCount];
            for (int index = 0; index < fieldCount; index++)
            {
                values[index] = index < rawValues.Length ? rawValues[index].Trim() : string.Empty;
            }

            result.Add(create(values, result.Count));
        }

        return result;
    }

    private static string ExistingId<T>(
        IList<T> items,
        int index,
        Func<T, string> getId,
        Func<string> createId)
        where T : class
    {
        return items != null && index < items.Count && items[index] != null
            ? getId(items[index])
            : createId();
    }

    private static bool IsCurrentValue(string value)
    {
        return string.Equals(value, "至今", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "現在", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "current", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> SplitCommaList(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToList();
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

    private void EnsureEditorUi()
    {
        if (editorRoot != null)
        {
            return;
        }

        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        editButton = CreateButton(transform, "Button_EditProfile", "編輯履歷", font,
            new Vector2(1f, 1f), new Vector2(-120f, -45f), new Vector2(180f, 56f));
        editButton.onClick.AddListener(OpenEditor);

        editorRoot = CreateUiObject("CareerProfileEditor", transform, typeof(Image));
        RectTransform panel = editorRoot.GetComponent<RectTransform>();
        Stretch(panel, new Vector2(34f, 30f), new Vector2(-34f, -30f));
        editorRoot.GetComponent<Image>().color = PanelColor;

        CreateText(editorRoot.transform, "Title", "編輯個人履歷", font, 34,
            new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(600f, 52f), TextAnchor.MiddleCenter);

        CreateEditorField("summary", "自我介紹（可多行）", "介紹背景、能力與職涯方向", font,
            panel, new Vector2(24f, -82f), new Vector2(750f, 180f));
        CreateEditorField("links", "連結（每行：名稱 | URL）", "GitHub | https://github.com/...", font,
            panel, new Vector2(24f, -300f), new Vector2(750f, 130f));
        CreateEditorField("skills", "技能（每行：技能 | 備註）", "Unity | 主要開發工具", font,
            panel, new Vector2(24f, -468f), new Vector2(750f, 130f));
        CreateEditorField("languages", "語言（每行：語言 | 程度 | 備註）", "中文 | 母語 |", font,
            panel, new Vector2(24f, -636f), new Vector2(750f, 130f));

        CreateEditorField("experiences", "工作經歷（公司 | 職務 | 開始 | 結束／至今 | 內容）",
            "公司 | 工程師 | 2024/01 | 至今 | 工作內容", font,
            panel, new Vector2(806f, -82f), new Vector2(750f, 180f));
        CreateEditorField("projects", "專案（名稱 | 技術逗號分隔 | URL | 說明）",
            "JobCheck | Unity, C# | https://... | 專案說明", font,
            panel, new Vector2(806f, -300f), new Vector2(750f, 180f));
        CreateEditorField("educations", "學歷（機構 | 項目 | 開始 | 結束 | 備註）",
            "學校 | 科系 | 2020 | 2024 |", font,
            panel, new Vector2(806f, -518f), new Vector2(750f, 180f));

        editorStatus = CreateText(editorRoot.transform, "Status", "", font, 22,
            new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(900f, 54f), TextAnchor.MiddleCenter);
        Button cancel = CreateButton(editorRoot.transform, "Button_Cancel", "取消", font,
            new Vector2(0.5f, 0f), new Vector2(-110f, 36f), new Vector2(180f, 56f));
        Button save = CreateButton(editorRoot.transform, "Button_Save", "儲存", font,
            new Vector2(0.5f, 0f), new Vector2(110f, 36f), new Vector2(180f, 56f));
        cancel.onClick.AddListener(CancelEditor);
        save.onClick.AddListener(SaveEditor);
        editorRoot.SetActive(false);
    }

    private void CreateEditorField(
        string key,
        string title,
        string placeholder,
        Font font,
        RectTransform panel,
        Vector2 topLeft,
        Vector2 size)
    {
        CreateText(panel, "Label_" + key, title, font, 22,
            new Vector2(0f, 1f), topLeft, new Vector2(size.x, 34f), TextAnchor.MiddleLeft);
        InputField input = CreateInput(panel, "Input_" + key, placeholder, font,
            new Vector2(topLeft.x, topLeft.y - 38f), size);
        editorFields.Add(key, input);
    }

    private static InputField CreateInput(
        Transform parent,
        string name,
        string placeholder,
        Font font,
        Vector2 topLeft,
        Vector2 size)
    {
        GameObject root = CreateUiObject(name, parent, typeof(Image), typeof(InputField));
        RectTransform rect = root.GetComponent<RectTransform>();
        TopLeft(rect, topLeft, size);
        root.GetComponent<Image>().color = FieldColor;

        Text placeholderText = CreateText(root.transform, "Placeholder", placeholder, font, 20,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
        Stretch(placeholderText.rectTransform, new Vector2(10f, 7f), new Vector2(-10f, -7f));
        placeholderText.color = new Color(0.55f, 0.55f, 0.55f, 1f);
        Text valueText = CreateText(root.transform, "Text", "", font, 22,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
        Stretch(valueText.rectTransform, new Vector2(10f, 7f), new Vector2(-10f, -7f));
        valueText.supportRichText = false;

        InputField input = root.GetComponent<InputField>();
        input.textComponent = valueText;
        input.placeholder = placeholderText;
        input.lineType = InputField.LineType.MultiLineNewline;
        input.targetGraphic = root.GetComponent<Image>();
        return input;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string label,
        Font font,
        Vector2 anchor,
        Vector2 position,
        Vector2 size)
    {
        GameObject root = CreateUiObject(name, parent, typeof(Image), typeof(Button));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = root.GetComponent<Image>();
        image.color = ButtonColor;
        Button button = root.GetComponent<Button>();
        button.targetGraphic = image;
        Text text = CreateText(root.transform, "Text", label, font, 24,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        text.color = Color.white;
        return button;
    }

    private static Text CreateText(
        Transform parent,
        string name,
        string value,
        Font font,
        int fontSize,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        TextAnchor alignment)
    {
        GameObject root = CreateUiObject(name, parent, typeof(Text));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor.y > 0.99f ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Text text = root.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        text.text = value;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
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
            ? new Color(0.75f, 0.12f, 0.12f, 1f)
            : new Color(0.18f, 0.18f, 0.18f, 1f);
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
