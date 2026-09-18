using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>一次性把資料搬運入口放入 AllJobPage Prefab；已有入口時絕不覆寫。</summary>
public static class BuildPortableTransferPrefab
{
    private const string HostPath = "Assets/Prefab/AllJobPage.prefab";

    [MenuItem("JobCheck/Build Portable Transfer Prefab", true)]
    private static bool CanBuild()
    {
        GameObject host = AssetDatabase.LoadAssetAtPath<GameObject>(HostPath);
        return host != null && host.transform.Find("Button_PortableTransfer") == null
            && host.transform.Find("Panel_PortableTransfer") == null;
    }

    [MenuItem("JobCheck/Build Portable Transfer Prefab")]
    public static void Build()
    {
        if (!CanBuild())
        {
            Debug.LogWarning("資料搬運入口已存在；不覆寫 Prefab。");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("Yozai-Light SDF t:TMP_FontAsset");
        TMP_FontAsset font = guids.Length > 0
            ? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]))
            : TMP_Settings.defaultFontAsset;
        GameObject host = PrefabUtility.LoadPrefabContents(HostPath);
        try
        {
            Button entry = MakeButton("Button_PortableTransfer", host.transform, font, "資料搬運", 30);
            Place(entry.gameObject, -630, -472, 210, 70);

            GameObject overlay = MakeImage("Panel_PortableTransfer", host.transform,
                new Color(0f, 0f, 0f, .73f));
            Stretch(overlay.GetComponent<RectTransform>());
            Panel_PortableTransfer controller = overlay.AddComponent<Panel_PortableTransfer>();

            GameObject card = MakeImage("Card", overlay.transform, new Color(.98f, .98f, .98f, 1f));
            Place(card, 0, 0, 1420, 740);
            TMP_Text title = MakeLabel("Title", card.transform, font, "個人資料搬運 — 匯出", 43,
                TextAlignmentOptions.Center);
            Place(title.gameObject, 0, 290, 1250, 75);
            TMP_Text message = MakeLabel("Text_Message", card.transform, font, "", 30,
                TextAlignmentOptions.TopLeft);
            Place(message.gameObject, 0, 5, 1250, 430);
            message.enableWordWrapping = true;
            message.overflowMode = TextOverflowModes.Overflow;
            Button close = MakeButton("Button_Close", card.transform, font, "關閉", 30);
            Place(close.gameObject, -125, -290, 180, 70);
            Button export = MakeButton("Button_Export", card.transform, font, "匯出", 30);
            Place(export.gameObject, 125, -290, 180, 70);

            SerializedObject panelSerialized = new SerializedObject(controller);
            panelSerialized.FindProperty("textMessage").objectReferenceValue = message;
            panelSerialized.FindProperty("buttonClose").objectReferenceValue = close;
            panelSerialized.FindProperty("buttonExport").objectReferenceValue = export;
            panelSerialized.ApplyModifiedPropertiesWithoutUndo();
            overlay.SetActive(false);

            SerializedObject page = new SerializedObject(host.GetComponent<AllJobPage>());
            page.FindProperty("buttonPortableTransfer").objectReferenceValue = entry;
            page.FindProperty("portableTransferPanel").objectReferenceValue = controller;
            page.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(host, HostPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(host);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("JobCheck V0.2.3 portable transfer entry created.");
    }

    private static GameObject MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static TMP_Text MakeLabel(string name, Transform parent, TMP_FontAsset font,
        string caption, int fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.text = caption;
        label.color = new Color(.13f, .13f, .13f, 1f);
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static Button MakeButton(string name, Transform parent, TMP_FontAsset font,
        string caption, int fontSize)
    {
        GameObject go = MakeImage(name, parent, Color.white);
        Button button = go.AddComponent<Button>();
        TMP_Text label = MakeLabel("Text", go.transform, font, caption, fontSize,
            TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
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
