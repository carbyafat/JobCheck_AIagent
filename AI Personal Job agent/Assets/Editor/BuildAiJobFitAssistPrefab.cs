using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一次性把 V0.2.9 AI 輔助入口與完整靜態 Modal 寫入職缺詳情 Prefab。
/// 已存在時拒絕重建，避免覆蓋後續人工調整。
/// </summary>
public static class BuildAiJobFitAssistPrefab
{
    private const string HostPath = "Assets/Prefab/SinglePanel_Detail.prefab";
    private const string ThemePath = "Assets/JobCheckUiTheme.asset";

    [MenuItem("JobCheck/Build V0.2.9 AI Assist UI", true)]
    private static bool CanBuild()
    {
        GameObject host = AssetDatabase.LoadAssetAtPath<GameObject>(HostPath);
        return host != null
            && Find(host.transform, "Button_ShowAiAssist_V029") == null
            && Find(host.transform, "Panel_AiJobFitAssist_V029") == null;
    }

    [MenuItem("JobCheck/Build V0.2.9 AI Assist UI")]
    public static void Build()
    {
        if (!CanBuild())
        {
            Debug.LogWarning("V0.2.9 AI 輔助 UI 已存在；為避免覆寫人工調整，已取消建置。");
            return;
        }

        JobCheckUiTheme theme = AssetDatabase.LoadAssetAtPath<JobCheckUiTheme>(ThemePath);
        TMP_FontAsset font = theme != null && theme.BodyFont != null
            ? theme.BodyFont
            : FindFont();
        Color surface = theme != null ? theme.Surface : new Color32(255, 252, 251, 255);
        Color muted = theme != null ? theme.SurfaceMuted : new Color32(238, 231, 229, 255);
        Color primary = theme != null ? theme.Primary : new Color32(126, 70, 80, 255);
        Color text = theme != null ? theme.TextPrimary : new Color32(46, 40, 41, 255);
        Color secondary = theme != null ? theme.TextSecondary : new Color32(98, 88, 91, 255);
        Color onPrimary = theme != null ? theme.TextOnPrimary : Color.white;
        Color overlay = theme != null ? theme.Overlay : new Color(0f, 0f, 0f, .65f);

        GameObject host = PrefabUtility.LoadPrefabContents(HostPath);
        try
        {
            Panel_JobDetail detail = host.GetComponent<Panel_JobDetail>();
            if (detail == null)
                throw new InvalidOperationException("SinglePanel_Detail 缺少 Panel_JobDetail。");

            Button entry = Button(
                "Button_ShowAiAssist_V029",
                host.transform,
                font,
                "AI 輔助",
                20,
                primary,
                onPrimary);
            TopRight(entry.GetComponent<RectTransform>(), -925, -55, 144, 40);

            GameObject panelObject = Image(
                "Panel_AiJobFitAssist_V029",
                host.transform,
                overlay);
            Stretch(panelObject.GetComponent<RectTransform>());
            Panel_AiJobFitAssist panel = panelObject.AddComponent<Panel_AiJobFitAssist>();

            GameObject card = Image("Card", panelObject.transform, surface);
            Center(card.GetComponent<RectTransform>(), 0, 0, 1280, 820);

            TMP_Text title = Label(
                "Title",
                card.transform,
                font,
                "AI 求職輔助",
                34,
                TextAlignmentOptions.MidlineLeft,
                text);
            Center(title.rectTransform, -430, 360, 320, 58);
            title.fontStyle = FontStyles.Bold;

            TMP_Text subtitle = Label(
                "Subtitle",
                card.transform,
                font,
                "離線 JSON 交換｜匯出前預覽，匯入後驗證；不會自動上傳或改寫母資料",
                19,
                TextAlignmentOptions.MidlineLeft,
                secondary);
            Center(subtitle.rectTransform, 40, 322, 950, 42);

            Button close = Button(
                "Button_Close",
                card.transform,
                font,
                "關閉",
                21,
                muted,
                text);
            Center(close.GetComponent<RectTransform>(), 535, 360, 150, 50);

            GameObject scrollObject = Image("ScrollView", card.transform, muted);
            Center(scrollObject.GetComponent<RectTransform>(), 0, -18, 1180, 620);
            ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 48f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewport = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(RectMask2D));
            viewport.layer = 5;
            viewport.transform.SetParent(scrollObject.transform, false);
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = surface;
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect);
            viewportRect.offsetMin = new Vector2(16, 16);
            viewportRect.offsetMax = new Vector2(-42, -16);

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.layer = 5;
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 1520);

            Scrollbar scrollbar = CreateScrollbar(scrollObject.transform, muted, primary);
            RectTransform scrollbarRect = scrollbar.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1, 0);
            scrollbarRect.anchorMax = new Vector2(1, 1);
            scrollbarRect.pivot = new Vector2(1, .5f);
            scrollbarRect.offsetMin = new Vector2(-28, 16);
            scrollbarRect.offsetMax = new Vector2(-10, -16);
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.verticalScrollbarSpacing = 4;

            TMP_Text dataHeading = Label(
                "Heading_DataScope",
                content.transform,
                font,
                "1　確認送出資料",
                25,
                TextAlignmentOptions.MidlineLeft,
                text);
            Top(dataHeading.rectTransform, 0, -10, 1060, 48);
            dataHeading.fontStyle = FontStyles.Bold;

            TMP_Text dataPreview = Label(
                "Text_DataPreview",
                content.transform,
                font,
                "開啟面板後顯示本次送出範圍。",
                20,
                TextAlignmentOptions.TopLeft,
                text);
            Top(dataPreview.rectTransform, 0, -62, 1060, 250);
            ConfigureBody(dataPreview);

            Button export = Button(
                "Button_ExportRequest",
                content.transform,
                font,
                "匯出分析資料",
                21,
                primary,
                onPrimary);
            Top(export.GetComponent<RectTransform>(), -170, -326, 300, 54);
            Button copy = Button(
                "Button_CopyInstructions",
                content.transform,
                font,
                "複製使用說明",
                21,
                muted,
                text);
            Top(copy.GetComponent<RectTransform>(), 170, -326, 300, 54);

            TMP_Text status = Label(
                "Text_Status",
                content.transform,
                font,
                "JobCheck 不會自動連線或上傳資料。",
                19,
                TextAlignmentOptions.TopLeft,
                secondary);
            Top(status.rectTransform, 0, -392, 1060, 125);
            ConfigureBody(status);

            TMP_Text importHeading = Label(
                "Heading_Import",
                content.transform,
                font,
                "2　匯入 AI 回傳 JSON",
                25,
                TextAlignmentOptions.MidlineLeft,
                text);
            Top(importHeading.rectTransform, 0, -526, 1060, 48);
            importHeading.fontStyle = FontStyles.Bold;

            TMP_InputField input = Input(
                "Input_ResultJson",
                content.transform,
                font,
                "貼上 AI 回傳的 JSON；也可以按下方按鈕選擇 .jobcheck-ai-result.json",
                18,
                text,
                secondary,
                surface);
            Top(input.GetComponent<RectTransform>(), 0, -580, 1060, 280);

            Button choose = Button(
                "Button_ChooseResult",
                content.transform,
                font,
                "選擇結果檔",
                21,
                muted,
                text);
            Top(choose.GetComponent<RectTransform>(), -170, -874, 300, 54);
            Button validate = Button(
                "Button_ValidateResult",
                content.transform,
                font,
                "驗證並預覽",
                21,
                muted,
                text);
            Top(validate.GetComponent<RectTransform>(), 170, -874, 300, 54);

            TMP_Text resultHeading = Label(
                "Heading_Result",
                content.transform,
                font,
                "3　分析結果預覽",
                25,
                TextAlignmentOptions.MidlineLeft,
                text);
            Top(resultHeading.rectTransform, 0, -940, 1060, 48);
            resultHeading.fontStyle = FontStyles.Bold;

            TMP_Text resultPreview = Label(
                "Text_ResultPreview",
                content.transform,
                font,
                "尚未匯入 AI 分析結果。",
                20,
                TextAlignmentOptions.TopLeft,
                text);
            Top(resultPreview.rectTransform, 0, -994, 1060, 500);
            ConfigureBody(resultPreview);

            Button save = Button(
                "Button_SaveResult",
                card.transform,
                font,
                "儲存分析",
                22,
                primary,
                onPrimary);
            Center(save.GetComponent<RectTransform>(), 470, -365, 220, 56);
            save.interactable = false;

            SerializedObject panelSerialized = new SerializedObject(panel);
            Set(panelSerialized, "theme", theme);
            Set(panelSerialized, "textDataPreview", dataPreview);
            Set(panelSerialized, "textStatus", status);
            Set(panelSerialized, "inputResultJson", input);
            Set(panelSerialized, "textResultPreview", resultPreview);
            Set(panelSerialized, "scrollRect", scroll);
            Set(panelSerialized, "buttonExportRequest", export);
            Set(panelSerialized, "buttonCopyInstructions", copy);
            Set(panelSerialized, "buttonChooseResult", choose);
            Set(panelSerialized, "buttonValidateResult", validate);
            Set(panelSerialized, "buttonSaveResult", save);
            Set(panelSerialized, "buttonClose", close);
            panelSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject detailSerialized = new SerializedObject(detail);
            Set(detailSerialized, "buttonShowAiAssist", entry);
            Set(detailSerialized, "aiAssistPanel", panel);
            detailSerialized.ApplyModifiedPropertiesWithoutUndo();

            panelObject.SetActive(false);
            panelObject.transform.SetAsLastSibling();
            PrefabUtility.SaveAsPrefabAsset(host, HostPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(host);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("JobCheck V0.2.9 AI assist static UI created in SinglePanel_Detail.prefab.");
    }

    [MenuItem("JobCheck/Validate V0.2.9 AI Assist UI")]
    public static void Validate()
    {
        GameObject host = PrefabUtility.LoadPrefabContents(HostPath);
        try
        {
            Transform button = Find(host.transform, "Button_ShowAiAssist_V029");
            Transform panel = Find(host.transform, "Panel_AiJobFitAssist_V029");
            if (button == null || button.GetComponent<Button>() == null)
                throw new InvalidOperationException("AI 輔助入口按鈕不存在。");
            if (panel == null || panel.GetComponent<Panel_AiJobFitAssist>() == null)
                throw new InvalidOperationException("AI 輔助靜態 Modal 不存在。");
            if (panel.gameObject.activeSelf)
                throw new InvalidOperationException("AI 輔助 Modal 預設必須關閉。");
            if (panel.GetSiblingIndex() != host.transform.childCount - 1)
                throw new InvalidOperationException("AI 輔助 Modal 必須位於最上層 sibling。");

            SerializedObject detail = new SerializedObject(host.GetComponent<Panel_JobDetail>());
            if (detail.FindProperty("buttonShowAiAssist").objectReferenceValue == null
                || detail.FindProperty("aiAssistPanel").objectReferenceValue == null)
            {
                throw new InvalidOperationException("Panel_JobDetail 的 AI UI 引用不完整。");
            }

            SerializedObject controller = new SerializedObject(
                panel.GetComponent<Panel_AiJobFitAssist>());
            foreach (string field in new[]
            {
                "textDataPreview", "textStatus", "inputResultJson", "textResultPreview",
                "scrollRect", "buttonExportRequest", "buttonCopyInstructions",
                "buttonChooseResult", "buttonValidateResult", "buttonSaveResult", "buttonClose"
            })
            {
                if (controller.FindProperty(field).objectReferenceValue == null)
                    throw new InvalidOperationException("AI 輔助 UI 缺少引用：" + field);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(host);
        }

        Debug.Log("JobCheck V0.2.9 AI assist prefab validation passed.");
    }

    private static TMP_FontAsset FindFont()
    {
        string[] guids = AssetDatabase.FindAssets("Yozai-Light SDF t:TMP_FontAsset");
        return guids.Length > 0
            ? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]))
            : TMP_Settings.defaultFontAsset;
    }

    private static GameObject Image(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static TMP_Text Label(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string value,
        float size,
        TextAlignmentOptions alignment,
        Color color)
    {
        var go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = value;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static Button Button(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string caption,
        float size,
        Color background,
        Color foreground)
    {
        GameObject go = Image(name, parent, background);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        TMP_Text label = Label(
            "Text",
            go.transform,
            font,
            caption,
            size,
            TextAlignmentOptions.Center,
            foreground);
        Stretch(label.rectTransform);
        return button;
    }

    private static TMP_InputField Input(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string hint,
        float size,
        Color textColor,
        Color hintColor,
        Color background)
    {
        GameObject go = Image(name, parent, background);
        TMP_InputField input = go.AddComponent<TMP_InputField>();
        input.targetGraphic = go.GetComponent<Image>();
        GameObject viewport = new GameObject(
            "Text Area",
            typeof(RectTransform),
            typeof(RectMask2D));
        viewport.layer = 5;
        viewport.transform.SetParent(go.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewportRect.offsetMin = new Vector2(16, 12);
        viewportRect.offsetMax = new Vector2(-16, -12);
        TMP_Text placeholder = Label(
            "Placeholder",
            viewport.transform,
            font,
            hint,
            size,
            TextAlignmentOptions.TopLeft,
            hintColor);
        Stretch(placeholder.rectTransform);
        TMP_Text value = Label(
            "Text",
            viewport.transform,
            font,
            string.Empty,
            size,
            TextAlignmentOptions.TopLeft,
            textColor);
        Stretch(value.rectTransform);
        input.textViewport = viewportRect;
        input.textComponent = (TextMeshProUGUI)value;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.characterLimit = 1024 * 1024;
        input.richText = false;
        return input;
    }

    private static Scrollbar CreateScrollbar(Transform parent, Color background, Color handleColor)
    {
        GameObject root = Image("Scrollbar Vertical", parent, background);
        Scrollbar scrollbar = root.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        GameObject sliding = new GameObject("Sliding Area", typeof(RectTransform));
        sliding.layer = 5;
        sliding.transform.SetParent(root.transform, false);
        Stretch(sliding.GetComponent<RectTransform>());
        GameObject handle = Image("Handle", sliding.transform, handleColor);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        Stretch(handleRect);
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        return scrollbar;
    }

    private static void ConfigureBody(TMP_Text value)
    {
        value.enableWordWrapping = true;
        value.overflowMode = TextOverflowModes.Overflow;
        value.richText = true;
    }

    private static void Set(SerializedObject value, string field, UnityEngine.Object target)
    {
        SerializedProperty property = value.FindProperty(field);
        if (property == null)
            throw new InvalidOperationException("找不到序列化欄位：" + field);
        property.objectReferenceValue = target;
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (string.Equals(child.name, name, StringComparison.Ordinal)) return child;
        return null;
    }

    private static void TopRight(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Top(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
        rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Center(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
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
