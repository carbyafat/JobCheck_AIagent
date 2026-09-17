using System;
using System.IO;
using JobCheck.Persistence;
using Object = UnityEngine.Object;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>一次性建置 V0.2.2 分析頁 Prefab 與 AllJobPage 入口。</summary>
public static class BuildAnalyticsPrefab
{
    private const string PanelPath = "Assets/Prefab/Panel_Analytics.prefab";
    private const string HostPath = "Assets/Prefab/AllJobPage.prefab";

    [MenuItem("JobCheck/Build Analytics Prefab")]
    public static void Build()
    {
        TMP_FontAsset font = FindFont();
        GameObject overlay = Ui("Panel_Analytics", null, new Color(0f, 0f, 0f, .73f));
        Stretch(overlay.GetComponent<RectTransform>());
        Panel_Analytics controller = overlay.AddComponent<Panel_Analytics>();

        GameObject card = Ui("Card", overlay.transform, new Color(.97f, .97f, .97f, 1f));
        Place(card, 0, 0, 1640, 960);
        TMP_Text title = Label("Title", card.transform, font, "應徵分析", 42, TextAlignmentOptions.Left);
        Place(title.gameObject, -630, 405, 260, 70);
        TMP_Text profile = Label("Text_Profile", card.transform, font, "資料區：Demo", 29, TextAlignmentOptions.Right);
        Place(profile.gameObject, 465, 405, 480, 60);
        Button close = CreateButton("Button_Close", card.transform, font, "關閉", 28);
        Place(close.gameObject, 735, 405, 125, 65);

        GameObject viewport = Ui("Viewport", card.transform, new Color(1f, 1f, 1f, 1f));
        Place(viewport, 0, -45, 1520, 770);
        viewport.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 55;
        TMP_Text report = Label("Text_Report", viewport.transform, font, "尚未載入分析資料", 30, TextAlignmentOptions.TopLeft);
        RectTransform reportRect = report.rectTransform;
        reportRect.anchorMin = new Vector2(0, 1);
        reportRect.anchorMax = new Vector2(1, 1);
        reportRect.pivot = new Vector2(.5f, 1);
        reportRect.offsetMin = new Vector2(24, -770);
        reportRect.offsetMax = new Vector2(-24, 0);
        report.enableWordWrapping = true;
        report.overflowMode = TextOverflowModes.Overflow;
        ContentSizeFitter fitter = report.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = reportRect;

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("textProfile").objectReferenceValue = profile;
        serialized.FindProperty("textReport").objectReferenceValue = report;
        serialized.FindProperty("buttonClose").objectReferenceValue = close;
        serialized.FindProperty("scrollRect").objectReferenceValue = scroll;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        GameObject panelPrefab = PrefabUtility.SaveAsPrefabAsset(overlay, PanelPath);
        Object.DestroyImmediate(overlay);

        GameObject host = PrefabUtility.LoadPrefabContents(HostPath);
        try
        {
            Transform oldPanel = host.transform.Find("Panel_Analytics");
            if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);
            Transform oldButton = host.transform.Find("Button_ShowAnalytics");
            if (oldButton != null) Object.DestroyImmediate(oldButton.gameObject);

            Button entry = CreateButton("Button_ShowAnalytics", host.transform, font, "分析", 32);
            Place(entry.gameObject, -410, -472, 200, 70);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, host.transform);
            instance.name = "Panel_Analytics";
            instance.SetActive(false);

            AllJobPage page = host.GetComponent<AllJobPage>();
            SerializedObject pageSerialized = new SerializedObject(page);
            pageSerialized.FindProperty("buttonShowAnalytics").objectReferenceValue = entry;
            pageSerialized.FindProperty("analyticsPanel").objectReferenceValue = instance.GetComponent<Panel_Analytics>();
            pageSerialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(host, HostPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(host);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("JobCheck V0.2.2 analytics prefab created.");
    }

    [MenuItem("JobCheck/Validate Analytics Prefab")]
    public static void Validate()
    {
        GameObject host = PrefabUtility.LoadPrefabContents(HostPath);
        try
        {
            Transform entry = host.transform.Find("Button_ShowAnalytics");
            Transform panel = host.transform.Find("Panel_Analytics");
            if (entry == null || entry.GetComponent<Button>() == null
                || panel == null || panel.gameObject.activeSelf
                || panel.GetComponent<Panel_Analytics>() == null
                || panel.GetSiblingIndex() != host.transform.childCount - 1)
                throw new InvalidOperationException("Analytics entry/panel Prefab binding is invalid.");
            SerializedObject page = new SerializedObject(host.GetComponent<AllJobPage>());
            if (page.FindProperty("buttonShowAnalytics").objectReferenceValue == null
                || page.FindProperty("analyticsPanel").objectReferenceValue == null)
                throw new InvalidOperationException("AllJobPage analytics references are missing.");
            SerializedObject controller = new SerializedObject(panel.GetComponent<Panel_Analytics>());
            foreach (string field in new[] { "textProfile", "textReport", "buttonClose", "scrollRect" })
                if (controller.FindProperty(field).objectReferenceValue == null)
                    throw new InvalidOperationException("Analytics panel reference missing: " + field);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(host);
        }

        GameObject runtime = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HostPath));
        try
        {
            runtime.GetComponent<AllJobPage>().ShowAnalytics();
            Panel_Analytics opened = runtime.GetComponentInChildren<Panel_Analytics>(true);
            Transform reportText = opened.transform.Find("Card/Viewport/Text_Report");
            if (!opened.gameObject.activeSelf || reportText == null
                || string.IsNullOrWhiteSpace(reportText.GetComponent<TMP_Text>().text))
                throw new InvalidOperationException("Analytics panel did not open with report text.");
            opened.Close();
            if (opened.gameObject.activeSelf)
                throw new InvalidOperationException("Analytics panel did not close.");
        }
        finally
        {
            Object.DestroyImmediate(runtime);
        }

        string empty = Panel_Analytics.FormatReport(new ApplicationAnalyticsReport(
            null, null, null, null, null, null));
        if (!empty.Contains("目前沒有職缺") || !empty.Contains("—"))
            throw new InvalidOperationException("Empty-state analytics report is invalid.");
        string longName = new string('長', 120) + "<測試>";
        string formatted = Panel_Analytics.FormatReport(new ApplicationAnalyticsReport(
            null,
            null,
            new[] { new PlatformPerformanceMetric(longName, 1, 0, 0, 0, 0, 0) },
            null, null, null));
        if (!formatted.Contains("&lt;測試&gt;") || !formatted.Contains("樣本少於 5"))
            throw new InvalidOperationException("Long-platform/small-sample rendering is invalid.");

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string demoRoot = Path.GetFullPath(Path.Combine(projectRoot, "../data"));
        string personalRoot = Path.GetFullPath(Path.Combine(projectRoot, "../personal_data"));
        CheckProfile("Demo", demoRoot);
        if (Directory.Exists(personalRoot)) CheckProfile("Personal", personalRoot);
        Debug.Log("JobCheck V0.2.2 analytics prefab validation passed.");
    }

    private static void CheckProfile(string name, string path)
    {
        PersistenceStorageResult<ApplicationAnalyticsReport> report = ApplicationAnalyticsQuery.Load(path);
        if (!report.IsSuccess || report.Value == null)
            throw new InvalidOperationException(name + " analytics query failed.");
        string text = Panel_Analytics.FormatReport(report.Value);
        if (string.IsNullOrEmpty(text) || !text.Contains("應徵漏斗"))
            throw new InvalidOperationException(name + " analytics formatting failed.");
        Debug.Log(name + " analytics query/formatting passed (read-only).");
    }

    private static TMP_FontAsset FindFont()
    {
        string[] guids = AssetDatabase.FindAssets("Yozai-Light SDF t:TMP_FontAsset");
        return guids.Length > 0
            ? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]))
            : TMP_Settings.defaultFontAsset;
    }

    private static GameObject Ui(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        if (parent != null) go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string value, int size, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = new Color(.1f, .1f, .1f, 1f);
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, TMP_FontAsset font, string caption, int size)
    {
        GameObject go = Ui(name, parent, Color.white);
        Button button = go.AddComponent<Button>();
        TMP_Text text = Label("Text", go.transform, font, caption, size, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    private static void Place(GameObject go, float x, float y, float width, float height)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
