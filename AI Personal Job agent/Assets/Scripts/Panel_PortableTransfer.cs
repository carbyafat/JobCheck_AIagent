using System;
using System.IO;
using JobCheck.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>個人資料搬運面板。匯出只讀取來源；匯入階段將沿用此面板。</summary>
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
            buttonPreview.onClick.RemoveListener(PreviewImport);
            buttonPreview.onClick.AddListener(PreviewImport);
        }
        if (buttonImport != null)
        {
            buttonImport.onClick.RemoveListener(Import);
            buttonImport.onClick.AddListener(Import);
        }
        if (inputPackagePath != null)
            inputPackagePath.onValueChanged.AddListener(_ => InvalidatePreview());
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
                    + "跨裝置匯入時，將檔案複製到新裝置，貼上完整路徑後先按「預覽」；"
                    + "僅空白個人資料區能啟用「匯入」。\n"
                    + "檔案尚未加密；請妥善保管。"
                : "目前是 Demo 資料區。請先切換至「個人」，才能匯出或匯入。";
        }
    }

    public void Close() => gameObject.SetActive(false);

    public void PreviewImport()
    {
        InvalidatePreview();
        if (!personalProfileActive || inputPackagePath == null) return;
        PersistenceStorageResult<JobCheckPortableImportPreview> result =
            JobCheckPortableImportService.Preview(inputPackagePath.text.Trim().Trim('"'));
        if (textMessage == null) return;
        if (!result.IsSuccess)
        {
            textMessage.text = "無法預覽搬運檔：\n"
                + (result.Issues.Count > 0 ? result.Issues[0].Message : "未知錯誤");
            return;
        }
        bool destinationIsEmpty = JobCheckPortableImportService.CanImportIntoEmptyRoot(
            personalDataRoot);
        if (destinationIsEmpty)
        {
            previewedPath = result.Value.Path;
            if (buttonImport != null) buttonImport.interactable = true;
        }
        textMessage.text = "預覽成功：" + result.Value.Path + "\n"
            + "匯出時間：" + result.Value.ExportedAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm") + "\n"
            + "公司 " + result.Value.CompanyCount + "、職缺 " + result.Value.JobCount
            + "、應徵 " + result.Value.ApplicationCount + "、事件 " + result.Value.EventCount + "。\n"
            + (destinationIsEmpty
                ? "目標個人資料區為空白，可以匯入。"
                : "目前個人資料區已有紀錄或其他檔案，不能在這裡匯入；不會覆蓋既有資料。");
    }

    public void Import()
    {
        if (!personalProfileActive || string.IsNullOrEmpty(previewedPath)
            || inputPackagePath == null || !PathsMatch(previewedPath, inputPackagePath.text))
            return;
        // Import 會重新預覽檔案；不依賴先前預覽時的可變內容。
        PersistenceStorageResult<JobCheckPortableImportSummary> result =
            JobCheckPortableImportService.Import(previewedPath, personalDataRoot);
        InvalidatePreview();
        if (textMessage == null) return;
        if (!result.IsSuccess)
        {
            textMessage.text = "匯入失敗，既有個人資料未覆蓋。\n"
                + (result.Issues.Count > 0 ? result.Issues[0].Message : "未知錯誤");
            return;
        }
        textMessage.text = "匯入完成：公司 " + result.Value.CompanyCount
            + "、職缺 " + result.Value.JobCount + "、應徵 " + result.Value.ApplicationCount
            + "、事件 " + result.Value.EventCount + "。"
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
                    + "完整路徑已填入下方；請先按「預覽」。本機個人資料非空時不可回灌。"
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
}
