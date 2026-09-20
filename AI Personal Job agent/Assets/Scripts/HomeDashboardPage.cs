using System;
using System.Collections.Generic;
using System.IO;
using JobCheck.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>JobCheck 首頁儀表板；只讀取既有摘要與履歷狀態，不寫回資料。</summary>
public sealed class HomeDashboardPage : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private string demoDataRootPath = "../data";
    [SerializeField] private string personalDataRootPath = "../personal_data";

    [Header("Theme")]
    [SerializeField] private JobCheckUiTheme theme;
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("Existing Placeholder")]
    [SerializeField] private GameObject placeholderRoot;

    public HomeDashboardSnapshot CurrentSnapshot { get; private set; }

    private AllJobPage allJobPage;
    private AppPageNavigator navigator;
    private GameObject dashboardRoot;
    private TMP_Text textDataProfile;
    private TMP_Text textRefreshedAt;
    private TMP_Text textJobCount;
    private TMP_Text textActiveCount;
    private TMP_Text textReviewCount;
    private TMP_Text textNoResponseCount;
    private TMP_Text textStatus;
    private TMP_Text textAttention;
    private TMP_Text textProfile;
    private Button buttonAddJob;
    private Button buttonViewJobs;
    private Button buttonAnalytics;
    private Button buttonEditResume;

    private TMP_FontAsset Font => theme != null && theme.BodyFont != null
        ? theme.BodyFont
        : fontAsset;
    private Color Background => theme != null
        ? theme.AppBackground
        : new Color32(244, 241, 240, 255);
    private Color Surface => theme != null ? theme.Surface : Color.white;
    private Color SurfaceMuted => theme != null
        ? theme.SurfaceMuted
        : new Color32(238, 231, 229, 255);
    private Color Border => theme != null
        ? theme.Border
        : new Color32(216, 207, 208, 255);
    private Color Primary => theme != null
        ? theme.Primary
        : new Color32(126, 70, 80, 255);
    private Color TextPrimary => theme != null
        ? theme.TextPrimary
        : new Color32(46, 40, 41, 255);
    private Color TextSecondary => theme != null
        ? theme.TextSecondary
        : new Color32(98, 88, 91, 255);
    private Color TextOnPrimary => theme != null ? theme.TextOnPrimary : Color.white;

    private void OnEnable()
    {
        EnsureUi();
        BindActions();
        Refresh();
    }

    public void OpenAddJob()
    {
        ShowJobsPage();
        ResolveAllJobPage();
        if (allJobPage != null)
        {
            allJobPage.ShowAddJobPosting();
        }
    }

    public void OpenJobs()
    {
        ShowJobsPage();
    }

    public void OpenAnalytics()
    {
        ShowJobsPage();
        ResolveAllJobPage();
        if (allJobPage != null)
        {
            allJobPage.ShowAnalytics();
        }
    }

    public void OpenResumeEditor()
    {
        ResolveNavigator();
        if (navigator != null)
        {
            navigator.ShowResume();
        }

        CareerProfilePage profilePage = FindObjectOfType<CareerProfilePage>(true);
        if (profilePage != null)
        {
            profilePage.OpenEditor();
        }
    }

    public void Refresh()
    {
        EnsureUi();
        if (dashboardRoot == null)
        {
            return;
        }

        ResolveAllJobPage();

        string dataRoot = allJobPage != null
            ? allJobPage.CurrentDataRoot
            : ResolveProjectRelativePath(SelectedProfile() == JobCheckDataProfile.Personal
                ? personalDataRootPath
                : demoDataRootPath);
        string personalRoot = ResolveProjectRelativePath(personalDataRootPath);
        PersistenceStorageResult<HomeDashboardSnapshot> result = HomeDashboardQuery.Load(
            dataRoot,
            personalRoot,
            DateTimeOffset.Now);
        if (!result.IsSuccess)
        {
            CurrentSnapshot = null;
            ShowError(result.Issues);
            return;
        }

        CurrentSnapshot = result.Value;
        Render(CurrentSnapshot);
    }

    private void Render(HomeDashboardSnapshot snapshot)
    {
        AnalyticsSummary summary = snapshot.Analytics;
        textDataProfile.text = "資料區：" + ProfileLabel();
        textRefreshedAt.text = "更新於 " + snapshot.RefreshedAt.ToLocalTime()
            .ToString("yyyy/MM/dd HH:mm");
        textJobCount.text = summary.JobPostingCount.ToString();
        textActiveCount.text = summary.ActiveApplicationCount.ToString();
        textReviewCount.text = summary.NeedsReviewCount.ToString();
        textNoResponseCount.text = summary.NoResponseMarkedCount.ToString();

        textStatus.text = !snapshot.HasJobData
            ? "目前還沒有職缺資料。先新增第一筆職缺，首頁才會開始整理求職進度。"
            : "已收錄 " + summary.JobPostingCount + " 筆職缺，"
                + summary.IncludedApplicationCount + " 輪本人應徵納入統計；"
                + "其中 " + summary.ActiveApplicationCount + " 輪仍在進行中。";

        if (snapshot.AttentionItemCount == 0)
        {
            textAttention.text = snapshot.HasJobData
                ? "目前沒有待檢視或長期未回覆的標記。"
                : "建立職缺並記錄投遞後，這裡會顯示需要留意的項目。";
        }
        else
        {
            var lines = new List<string>();
            if (summary.NeedsReviewCount > 0)
            {
                lines.Add("• 待檢視資料：" + summary.NeedsReviewCount + " 筆");
            }

            if (summary.NoResponseMarkedCount > 0)
            {
                lines.Add("• 已標記長期未回覆：" + summary.NoResponseMarkedCount + " 輪");
            }

            textAttention.text = string.Join("\n", lines);
        }

        if (!snapshot.HasSavedProfile)
        {
            textProfile.text = "尚未建立個人履歷。\n完成履歷後，首頁會顯示區塊完成度與更新時間。";
        }
        else
        {
            string updated = snapshot.ProfileUpdatedAt.HasValue
                ? snapshot.ProfileUpdatedAt.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
                : "—";
            textProfile.text = "已完成 " + snapshot.CompletedProfileSectionCount
                + " / " + HomeDashboardQuery.TotalProfileSectionCount + " 個履歷區塊"
                + "\n最後更新：" + updated;
        }
    }

    private void ShowError(IReadOnlyList<PersistenceStorageIssue> issues)
    {
        textDataProfile.text = "資料區：" + ProfileLabel();
        textRefreshedAt.text = "讀取失敗";
        textJobCount.text = "—";
        textActiveCount.text = "—";
        textReviewCount.text = "—";
        textNoResponseCount.text = "—";
        textStatus.text = "無法讀取首頁摘要。";
        textAttention.text = issues == null || issues.Count == 0
            ? "未知錯誤"
            : issues[0].Message;
        textProfile.text = "履歷狀態暫時無法顯示。";
    }

    private void EnsureUi()
    {
        if (dashboardRoot != null)
        {
            return;
        }

        TMP_FontAsset font = Font;
        if (font == null)
        {
            Debug.LogError("HomeDashboardPage 缺少 TMP 字型資產。");
            return;
        }

        if (placeholderRoot != null)
        {
            placeholderRoot.SetActive(false);
        }

        PrepareGlyphs(font);
        dashboardRoot = CreateUiObject("HomeDashboard", transform, typeof(Image));
        RectTransform root = dashboardRoot.GetComponent<RectTransform>();
        Stretch(root, Vector2.zero, Vector2.zero);
        dashboardRoot.GetComponent<Image>().color = Background;

        TMP_Text title = CreateText(root, "Title", "求職總覽",
            theme != null ? theme.PageTitleSize : 48f,
            TextAlignmentOptions.MidlineLeft);
        SetTopLeft(title.rectTransform, new Vector2(40f, -28f), new Vector2(520f, 62f));
        title.fontStyle = FontStyles.Bold;

        textDataProfile = CreateText(root, "DataProfile", "資料區：—",
            theme != null ? theme.BodySize : 22f,
            TextAlignmentOptions.MidlineRight);
        SetTopRight(textDataProfile.rectTransform,
            new Vector2(-40f, -26f), new Vector2(420f, 34f));
        textDataProfile.color = TextPrimary;
        textRefreshedAt = CreateText(root, "RefreshedAt", "更新於 —",
            theme != null ? theme.SupportingTextSize : 20f,
            TextAlignmentOptions.MidlineRight);
        SetTopRight(textRefreshedAt.rectTransform,
            new Vector2(-40f, -62f), new Vector2(420f, 30f));
        textRefreshedAt.color = TextSecondary;

        GameObject scrollObject = CreateUiObject(
            "DashboardScroll", root, typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        RectTransform viewport = scrollObject.GetComponent<RectTransform>();
        Stretch(viewport, new Vector2(40f, 32f), new Vector2(-40f, -112f));
        scrollObject.GetComponent<Image>().color = Background;

        GameObject contentObject = CreateUiObject(
            "Content", viewport, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup vertical = contentObject.GetComponent<VerticalLayoutGroup>();
        vertical.spacing = theme != null ? theme.SpaceLg : 24f;
        vertical.childAlignment = TextAnchor.UpperLeft;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        ContentSizeFitter contentFitter = contentObject.GetComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;

        CreateMetricRow(content);
        CreateDashboardColumns(content);
        BindActions();
    }

    private void BindActions()
    {
        BindButton(buttonAddJob, OpenAddJob);
        BindButton(buttonViewJobs, OpenJobs);
        BindButton(buttonAnalytics, OpenAnalytics);
        BindButton(buttonEditResume, OpenResumeEditor);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void ShowJobsPage()
    {
        ResolveNavigator();
        if (navigator != null)
        {
            navigator.ShowJobs();
        }
    }

    private void ResolveNavigator()
    {
        if (navigator == null)
        {
            navigator = FindObjectOfType<AppPageNavigator>(true);
        }
    }

    private void ResolveAllJobPage()
    {
        if (allJobPage == null)
        {
            allJobPage = FindObjectOfType<AllJobPage>(true);
        }
    }

    private void CreateMetricRow(RectTransform parent)
    {
        GameObject rowObject = CreateUiObject(
            "MetricRow", parent, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        HorizontalLayoutGroup row = rowObject.GetComponent<HorizontalLayoutGroup>();
        row.spacing = theme != null ? theme.SpaceMd : 16f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;
        LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
        rowLayout.minHeight = 150f;
        rowLayout.preferredHeight = 150f;

        textJobCount = CreateMetricCard(rowObject.transform, "Metric_Jobs", "職缺總數");
        textActiveCount = CreateMetricCard(rowObject.transform, "Metric_Active", "進行中應徵");
        textReviewCount = CreateMetricCard(rowObject.transform, "Metric_Review", "待檢視資料");
        textNoResponseCount = CreateMetricCard(
            rowObject.transform, "Metric_NoResponse", "長期未回覆");
    }

    private TMP_Text CreateMetricCard(Transform parent, string name, string label)
    {
        GameObject card = CreateCard(parent, name, 150f);
        TMP_Text value = CreateText(card.transform, "Value", "—",
            42f, TextAlignmentOptions.MidlineLeft);
        value.fontStyle = FontStyles.Bold;
        value.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
        TMP_Text caption = CreateText(card.transform, "Label", label,
            theme != null ? theme.SupportingTextSize : 20f,
            TextAlignmentOptions.MidlineLeft);
        caption.color = TextSecondary;
        caption.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
        return value;
    }

    private void CreateDashboardColumns(RectTransform parent)
    {
        GameObject columnsObject = CreateUiObject(
            "MainColumns", parent, typeof(CareerProfileColumnsLayout));
        columnsObject.GetComponent<CareerProfileColumnsLayout>().Configure(
            0.62f, theme != null ? theme.SpaceLg : 24f);
        RectTransform left = CreateColumn(columnsObject.transform, "Column_Status");
        RectTransform right = CreateColumn(columnsObject.transform, "Column_Actions");

        textStatus = CreateTextCard(left, "Card_Status", "目前求職狀態", 150f);
        textAttention = CreateTextCard(left, "Card_Attention", "需要留意", 150f);
        textProfile = CreateTextCard(right, "Card_Profile", "個人履歷", 150f);
        CreateQuickActionsCard(right);
    }

    private RectTransform CreateColumn(Transform parent, string name)
    {
        GameObject columnObject = CreateUiObject(
            name, parent, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        VerticalLayoutGroup column = columnObject.GetComponent<VerticalLayoutGroup>();
        column.spacing = theme != null ? theme.SpaceMd : 16f;
        column.childAlignment = TextAnchor.UpperLeft;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        ContentSizeFitter fitter = columnObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return columnObject.GetComponent<RectTransform>();
    }

    private TMP_Text CreateTextCard(
        Transform parent,
        string name,
        string heading,
        float minimumHeight)
    {
        GameObject card = CreateCard(parent, name, minimumHeight);
        TMP_Text title = CreateText(card.transform, "Title", heading,
            theme != null ? theme.SectionTitleSize : 28f,
            TextAlignmentOptions.MidlineLeft);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
        TMP_Text body = CreateText(card.transform, "Body", string.Empty,
            theme != null ? theme.BodySize : 22f,
            TextAlignmentOptions.TopLeft);
        body.color = TextSecondary;
        LayoutElement bodyLayout = body.gameObject.AddComponent<LayoutElement>();
        bodyLayout.minHeight = 82f;
        return body;
    }

    private void CreateQuickActionsCard(Transform parent)
    {
        GameObject card = CreateCard(parent, "Card_QuickActions", 260f);
        TMP_Text title = CreateText(card.transform, "Title", "快速操作",
            theme != null ? theme.SectionTitleSize : 28f,
            TextAlignmentOptions.MidlineLeft);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;

        buttonAddJob = CreateActionButton(card.transform, "Button_AddJob", "新增職缺", true);
        buttonViewJobs = CreateActionButton(card.transform, "Button_ViewJobs", "查看職缺", false);
        buttonAnalytics = CreateActionButton(card.transform, "Button_Analytics", "應徵分析", false);
        buttonEditResume = CreateActionButton(card.transform, "Button_EditResume", "編輯履歷", false);
    }

    private GameObject CreateCard(Transform parent, string name, float minimumHeight)
    {
        GameObject card = CreateUiObject(
            name, parent, typeof(Image), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter), typeof(Outline));
        Image image = card.GetComponent<Image>();
        image.color = Surface;
        ApplySprite(image);
        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(1f, -1f);
        VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
        int padding = Mathf.RoundToInt(theme != null ? theme.SpaceLg : 24f);
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = theme != null ? theme.SpaceSm : 12f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = card.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        LayoutElement cardLayout = card.AddComponent<LayoutElement>();
        cardLayout.minHeight = minimumHeight;
        return card;
    }

    private Button CreateActionButton(
        Transform parent,
        string name,
        string label,
        bool primary)
    {
        GameObject root = CreateUiObject(name, parent, typeof(Image), typeof(Button));
        Image image = root.GetComponent<Image>();
        image.color = primary ? Primary : SurfaceMuted;
        ApplySprite(image);
        Button button = root.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = primary ? Primary : SurfaceMuted;
        colors.highlightedColor = theme != null ? theme.PrimaryHover : Primary;
        colors.pressedColor = theme != null ? theme.PrimaryPressed : Primary;
        colors.selectedColor = colors.normalColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        TMP_Text text = CreateText(root.transform, "Text", label,
            theme != null ? theme.ButtonTextSize : 22f,
            TextAlignmentOptions.Center);
        Stretch(text.rectTransform, new Vector2(12f, 6f), new Vector2(-12f, -6f));
        text.color = primary ? TextOnPrimary : TextPrimary;
        LayoutElement buttonLayout = root.AddComponent<LayoutElement>();
        buttonLayout.minHeight = theme != null ? theme.ButtonHeight : 56f;
        buttonLayout.preferredHeight = theme != null ? theme.ButtonHeight : 56f;
        return button;
    }

    private TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        TextAlignmentOptions alignment)
    {
        GameObject root = CreateUiObject(name, parent, typeof(TextMeshProUGUI));
        TMP_Text text = root.GetComponent<TMP_Text>();
        text.font = Font;
        text.fontSize = size;
        text.color = TextPrimary;
        text.text = value;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private void PrepareGlyphs(TMP_FontAsset font)
    {
        const string characters =
            "求職總覽資料區個人更新於職缺總數進行中應徵待檢視長期未回覆目前狀態需要留意履歷快速操作新增查看分析編輯尚未建立完成區塊最後讀取失敗未知錯誤筆輪開始整理進度已收錄納入統計其中仍在沒有特別處理標記顯示項目已標記年月日時分";
        if (!font.TryAddCharacters(characters, out string missing)
            && !string.IsNullOrEmpty(missing))
        {
            Debug.LogWarning("首頁字型缺少下列字元：" + missing);
        }
    }

    private string ProfileLabel()
    {
        return allJobPage != null
            ? allJobPage.CurrentDataProfileLabel
            : SelectedProfile() == JobCheckDataProfile.Personal ? "個人" : "Demo";
    }

    private static JobCheckDataProfile SelectedProfile()
    {
        return PlayerPrefs.GetInt("JobCheck.DataProfile", (int)JobCheckDataProfile.Demo)
            == (int)JobCheckDataProfile.Personal
            ? JobCheckDataProfile.Personal
            : JobCheckDataProfile.Demo;
    }

    private static string ResolveProjectRelativePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }

    private void ApplySprite(Image image)
    {
        if (theme == null || theme.ButtonBackgroundSprite == null)
        {
            return;
        }

        image.sprite = theme.ButtonBackgroundSprite;
        image.type = Image.Type.Sliced;
    }

    private static GameObject CreateUiObject(
        string name,
        Transform parent,
        params Type[] components)
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

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetTopRight(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
