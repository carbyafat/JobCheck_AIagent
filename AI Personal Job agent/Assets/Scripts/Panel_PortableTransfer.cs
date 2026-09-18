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
    [SerializeField] private Button buttonExport;
    [SerializeField] private Button buttonClose;

    private string personalDataRoot;
    private bool personalProfileActive;

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
    }

    public void OpenExport(string dataRoot, bool isPersonalProfile)
    {
        personalDataRoot = dataRoot;
        personalProfileActive = isPersonalProfile;
        gameObject.SetActive(true);
        if (buttonExport != null) buttonExport.interactable = isPersonalProfile;
        if (textMessage != null)
        {
            textMessage.text = isPersonalProfile
                ? "將個人資料匯出為單一 .jobcheck.json 檔案。\n"
                    + "儲存位置：" + GetExportDirectory() + "\n\n"
                    + "檔案包含職缺、公司與應徵紀錄，尚未加密；請妥善保管。"
                : "目前是 Demo 資料區。請先切換至「個人」，才能匯出自己的資料。";
        }
    }

    public void Close() => gameObject.SetActive(false);

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
                textMessage.text = "匯出完成：" + summary.Path + "\n\n"
                    + "公司 " + summary.CompanyCount + "、職缺 " + summary.JobCount
                    + "、應徵 " + summary.ApplicationCount + "。\n"
                    + "請將這個檔案複製到其他裝置。檔案尚未加密。";
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
