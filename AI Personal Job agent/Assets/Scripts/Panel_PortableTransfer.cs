using System;
using System.IO;
using System.Linq;
using JobCheck.Persistence;
using SimpleFileBrowser;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>個人資料搬運面板。選檔後預覽，僅新增無衝突的個人紀錄。</summary>
public sealed class Panel_PortableTransfer : MonoBehaviour
{
    [SerializeField] private TMP_Text textMessage;
    [SerializeField] private TMP_InputField inputPackagePath;
    [SerializeField] private Button buttonExport;
    [SerializeField] private Button buttonPreview;
    [SerializeField] private Button buttonImport;
    [SerializeField] private Button buttonClose;

    private string personalDataRoot;
    private bool personalProfileActive;
    private string previewedPath;
    private Action onImported;

    private void Awake()
    {
        if (buttonExport != null)
        {
            buttonExport.onClick.RemoveListener(Export);
            buttonExport.onClick.AddListener(Export);
        }
        if (buttonClose != null)
        {
            buttonClose.onClick.RemoveListener(Close);
            buttonClose.onClick.AddListener(Close);
        }
        if (buttonPreview != null)
        {
            buttonPreview.onClick.RemoveListener(ChooseAndPreviewImport);
            buttonPreview.onClick.AddListener(ChooseAndPreviewImport);
        }
        if (buttonImport != null)
        {
            buttonImport.onClick.RemoveListener(Import);
            buttonImport.onClick.AddListener(Import);
        }
        if (inputPackagePath != null)
        {
            inputPackagePath.readOnly = true;
            inputPackagePath.onValueChanged.AddListener(_ => InvalidatePreview());
        }
    }

    public void OpenExport(string dataRoot, bool isPersonalProfile, Action imported = null)
    {
        personalDataRoot = dataRoot;
        personalProfileActive = isPersonalProfile;
        onImported = imported;
        InvalidatePreview();
        gameObject.SetActive(true);
        if (buttonExport != null) buttonExport.interactable = isPersonalProfile;
        if (buttonPreview != null) buttonPreview.interactable = isPersonalProfile;
        if (textMessage != null)
        {
            textMessage.text = isPersonalProfile
                ? "將個人資料匯出為單一 .jobcheck.json 檔案。\n"
                    + "匯出位置：" + GetExportDirectory() + "\n"
                    + "跨裝置匯入時，將檔案複製到新裝置，再按「選擇檔案」預覽；"
                    + "同步只新增自己的新紀錄；同 ID 內容不同會停止，不覆蓋舊資料。\n"
                    + "檔案尚未加密；請妥善保管。"
                : "目前是 Demo 資料區。請先切換至「個人」，才能匯出或匯入。";
        }
    }

    public void Close() => gameObject.SetActive(false);

    public void ChooseAndPreviewImport()
    {
        if (!personalProfileActive || inputPackagePath == null || FileBrowser.IsOpen) return;
        FileBrowser.SetFilters(false,
            new FileBrowser.Filter("JobCheck backup", JobCheckPortablePackageDto.FileExtension));
        FileBrowser.ShowLoadDialog(
            paths =>
            {
                if (this == null || !gameObject.activeInHierarchy || inputPackagePath == null
                    || paths == null || paths.Length != 1) return;
                inputPackagePath.text = paths[0];
                PreviewImport();
            },
            () => { },
            FileBrowser.PickMode.Files,
            false,
            GetInitialBrowseDirectory(),
            null,
            "Select JobCheck backup",
            "Select");
    }

    public void PreviewImport()
    {
        InvalidatePreview();
        if (!personalProfileActive || inputPackagePath == null) return;
        PersistenceStorageResult<JobCheckPortableSyncPreview> result =
            JobCheckPortableImportService.PreviewSync(
                inputPackagePath.text.Trim().Trim('"'), personalDataRoot);
        if (textMessage == null) return;
        if (!result.IsSuccess)
        {
            textMessage.text = "無法同步這份搬運檔：\n"
                + string.Join("\n", result.Issues.Take(4).Select(issue => issue.Message));
            return;
        }
        bool hasNewData = result.Value.NewCompanyCount + result.Value.NewJobCount
            + result.Value.NewApplicationCount > 0;
        if (hasNewData)
        {
            previewedPath = result.Value.Source.Path;
            if (buttonImport != null) buttonImport.interactable = true;
        }
        textMessage.text = "預覽成功：" + result.Value.Source.Path + "\n"
            + "匯出時間：" + result.Value.Source.ExportedAt.ToLocalTime()
                .ToString("yyyy.MM.dd HH:mm") + "\n"
            + "新公司 " + result.Value.NewCompanyCount + "、新職缺 "
            + result.Value.NewJobCount + "、新應徵 " + result.Value.NewApplicationCount
            + "、新事件 " + result.Value.NewEventCount + "。\n"
            + (hasNewData
                ? "請確認檔案屬於自己的資料；匯入只加入新紀錄，不覆蓋原有紀錄。"
                : "沒有新紀錄，無須再次匯入。");
    }

    public void Import()
    {
        if (!personalProfileActive || string.IsNullOrEmpty(previewedPath)
            || inputPackagePath == null || !PathsMatch(previewedPath, inputPackagePath.text))
            return;
        // Import 會重新預覽檔案；不依賴先前預覽時的可變內容。
        PersistenceStorageResult<JobCheckPortableSyncPreview> result =
            JobCheckPortableImportService.Sync(previewedPath, personalDataRoot);
        InvalidatePreview();
        if (textMessage == null) return;
        if (!result.IsSuccess)
        {
            textMessage.text = "同步未完成，既有個人資料未覆蓋。\n"
                + (result.Issues.Count > 0 ? result.Issues[0].Message : "未知錯誤");
            return;
        }
        textMessage.text = "同步完成：新增公司 " + result.Value.NewCompanyCount
            + "、職缺 " + result.Value.NewJobCount + "、應徵 "
            + result.Value.NewApplicationCount + "、事件 " + result.Value.NewEventCount + "。"
            + (string.IsNullOrEmpty(result.Value.CleanupWarning)
                ? string.Empty : "\n" + result.Value.CleanupWarning);
        onImported?.Invoke();
    }

    private void InvalidatePreview()
    {
        previewedPath = null;
        if (buttonImport != null) buttonImport.interactable = false;
    }

    private static bool PathsMatch(string expected, string entered)
    {
        if (string.IsNullOrWhiteSpace(entered)) return false;
        try
        {
            return string.Equals(expected, Path.GetFullPath(entered.Trim().Trim('"')),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is ArgumentException || exception is NotSupportedException
            || exception is PathTooLongException)
        {
            return false;
        }
    }

    public void Export()
    {
        if (!personalProfileActive || string.IsNullOrWhiteSpace(personalDataRoot)) return;
        try
        {
            string directory = GetExportDirectory();
            Directory.CreateDirectory(directory);
            string destination = Path.Combine(directory,
                "JobCheck-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-"
                + Guid.NewGuid().ToString("N").Substring(0, 8)
                + JobCheckPortablePackageDto.FileExtension);
            PersistenceStorageResult<JobCheckPortableExportSummary> result =
                JobCheckPortableExportService.Export(personalDataRoot, destination);
            if (textMessage == null) return;
            if (result.IsSuccess)
            {
                JobCheckPortableExportSummary summary = result.Value;
                if (inputPackagePath != null) inputPackagePath.text = summary.Path;
                textMessage.text = "匯出完成：" + summary.Path + "\n\n"
                    + "公司 " + summary.CompanyCount + "、職缺 " + summary.JobCount
                    + "、應徵 " + summary.ApplicationCount + "。\n"
                    + "完整路徑已顯示於下方；按「選擇檔案」可預覽新增筆數。"
                    + "檔案尚未加密。";
            }
            else
            {
                textMessage.text = "匯出失敗，原始資料未變更。\n"
                    + (result.Issues.Count > 0 ? result.Issues[0].Message : "未知錯誤");
            }
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException || exception is NotSupportedException)
        {
            if (textMessage != null)
                textMessage.text = "無法建立匯出資料夾：" + exception.Message;
        }
    }

    private static string GetExportDirectory()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string root = string.IsNullOrWhiteSpace(documents)
            ? Application.persistentDataPath : documents;
        return Path.Combine(root, "JobCheck", "Exports");
    }

    private string GetInitialBrowseDirectory()
    {
        if (inputPackagePath != null && File.Exists(inputPackagePath.text))
            return Path.GetDirectoryName(inputPackagePath.text);
        string exports = GetExportDirectory();
        if (Directory.Exists(exports)) return exports;
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Directory.Exists(documents) ? documents : Application.persistentDataPath;
    }
}
