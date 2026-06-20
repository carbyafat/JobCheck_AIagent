using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 總攬頁中的單筆職缺列。負責顯示摘要，並在點擊時通知 AllJobPage 開啟詳細頁。
/// </summary>
public class Panel_SingleJob : MonoBehaviour
{
    [Tooltip("公司名稱文字。")]
    [SerializeField] private TMP_Text textCompany;
    [Tooltip("職缺名稱文字。")]
    [SerializeField] private TMP_Text textJobName;
    [Tooltip("薪資文字。")]
    [SerializeField] private TMP_Text textSalary;
    [Tooltip("目前狀態文字。逾期時會變紅。")]
    [SerializeField] private TMP_Text textStatus;
    [Tooltip("點擊開啟詳細頁的按鈕。")]
    [SerializeField] private Button buttonOpenDetail;
    [Tooltip("備用詳細頁引用；通常由 AllJobPage 統一處理。")]
    [SerializeField] private Panel_JobDetail jobDetailPanel;
    [Tooltip("列表文字大小。小於等於 0 時不主動調整。")]
    [SerializeField] private float displayFontSize = 22f;
    [Tooltip("正常狀態文字顏色。")]
    [SerializeField] private Color normalStatusTextColor = Color.white;
    [Tooltip("逾期狀態文字顏色。")]
    [SerializeField] private Color expiredStatusTextColor = Color.red;

    private JobSummaryData currentData;
    private AllJobPage ownerPage;

    private void Awake()
    {
        AutoBindReferences();
        BindButton();
    }

    /// <summary>
    /// 填入職缺摘要資料並刷新顯示。
    /// </summary>
    /// <param name="data">職缺摘要資料。</param>
    public void SetData(JobSummaryData data)
    {
        currentData = data;

        SetText(textCompany, data != null ? data.company : string.Empty);
        SetText(textJobName, data != null ? data.title : string.Empty);
        SetText(textSalary, data != null ? data.salary : string.Empty);
        SetText(textStatus, data != null ? ConvertStatusToDisplayText(data.status, data.is_expired) : string.Empty);
        SetStatusColor(data != null && data.is_expired);
        ApplyDisplayFontSize();
    }

    /// <summary>
    /// 指定總攬頁控制器，點擊時會回呼它開啟詳細頁。
    /// </summary>
    /// <param name="owner">總攬頁控制器。</param>
    public void SetOwner(AllJobPage owner)
    {
        ownerPage = owner;
    }

    /// <summary>
    /// 清空目前列資料與文字。
    /// </summary>
    public void Clear()
    {
        currentData = null;
        SetText(textCompany, string.Empty);
        SetText(textJobName, string.Empty);
        SetText(textSalary, string.Empty);
        SetText(textStatus, string.Empty);
        SetStatusColor(false);
    }

    /// <summary>
    /// 點擊職缺列時呼叫。
    /// </summary>
    public void OnClickJob()
    {
        if (currentData == null)
        {
            return;
        }

        if (ownerPage != null)
        {
            ownerPage.OpenJobDetail(currentData);
            return;
        }

        LoadJobDetail(currentData);
    }

    /// <summary>
    /// 備用詳細頁載入流程；沒有 ownerPage 時使用。
    /// </summary>
    /// <param name="data">要載入的職缺摘要資料。</param>
    public void LoadJobDetail(JobSummaryData data)
    {
        // TODO: Detail UI is not implemented yet. This method is the entry point for refreshing it later.
        if (data == null || string.IsNullOrEmpty(data.detailFullPath))
        {
            return;
        }

        if (!File.Exists(data.detailFullPath))
        {
            Debug.LogWarning("Job detail json not found: " + data.detailFullPath);
            return;
        }

        string json = File.ReadAllText(data.detailFullPath);
        Debug.Log("Loaded job detail json: " + data.detailFullPath);

        if (jobDetailPanel == null)
        {
            jobDetailPanel = FindObjectOfType<Panel_JobDetail>(true);
        }

        if (jobDetailPanel != null)
        {
            jobDetailPanel.LoadFromJson(json);
        }
        else
        {
            Debug.LogWarning("Panel_JobDetail not found in scene.");
        }
    }

    /// <summary>
    /// 自動綁定子物件文字與按鈕。
    /// </summary>
    private void AutoBindReferences()
    {
        if (textCompany == null)
        {
            textCompany = FindChildText("TMP_Company");
        }

        if (textJobName == null)
        {
            textJobName = FindChildText("TMP_JobName");
        }

        if (textSalary == null)
        {
            textSalary = FindChildText("TMP_Paid");
        }

        if (textStatus == null)
        {
            textStatus = FindChildText("TMP_Status");
        }

        if (buttonOpenDetail == null)
        {
            buttonOpenDetail = GetComponent<Button>();
        }

        if (buttonOpenDetail == null)
        {
            buttonOpenDetail = gameObject.AddComponent<Button>();
        }

        if (jobDetailPanel == null)
        {
            jobDetailPanel = FindObjectOfType<Panel_JobDetail>(true);
        }
    }

    /// <summary>
    /// 綁定點擊事件。
    /// </summary>
    private void BindButton()
    {
        if (buttonOpenDetail == null)
        {
            return;
        }

        buttonOpenDetail.onClick.RemoveListener(OnClickJob);
        buttonOpenDetail.onClick.AddListener(OnClickJob);
    }

    /// <summary>
    /// 依名稱尋找子物件 TMP_Text。
    /// </summary>
    /// <param name="childName">子物件名稱。</param>
    /// <returns>找到的 TMP_Text；找不到回傳 null。</returns>
    private TMP_Text FindChildText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    /// <summary>
    /// 設定文字內容。
    /// </summary>
    /// <param name="target">目標文字元件。</param>
    /// <param name="value">文字內容。</param>
    private void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    /// <summary>
    /// 套用列表文字大小。
    /// </summary>
    private void ApplyDisplayFontSize()
    {
        SetFontSize(textCompany);
        SetFontSize(textJobName);
        SetFontSize(textSalary);
        SetFontSize(textStatus);
    }

    /// <summary>
    /// 設定單一文字大小。
    /// </summary>
    /// <param name="target">目標文字元件。</param>
    private void SetFontSize(TMP_Text target)
    {
        if (target != null && displayFontSize > 0f)
        {
            target.fontSize = displayFontSize;
        }
    }

    /// <summary>
    /// 設定狀態文字顏色。
    /// </summary>
    /// <param name="isExpired">是否逾期。</param>
    private void SetStatusColor(bool isExpired)
    {
        if (textStatus != null)
        {
            textStatus.color = isExpired ? expiredStatusTextColor : normalStatusTextColor;
        }
    }

    /// <summary>
    /// 將狀態代碼轉成中文顯示文字。
    /// </summary>
    /// <param name="status">狀態代碼。</param>
    /// <param name="isExpired">是否逾期。</param>
    /// <returns>中文狀態文字。</returns>
    private string ConvertStatusToDisplayText(string status, bool isExpired)
    {
        string text;
        switch (status)
        {
            case "not_viewed":
                text = "未檢視";
                break;
            case "not_applied":
                text = "未投遞";
                break;
            case "interested":
                text = "有興趣";
                break;
            case "not_applying":
                text = "確認不投";
                break;
            case "applied":
                text = "已投遞";
                break;
            case "interview_scheduled":
                text = "已預約面試";
                break;
            case "waiting_reply":
                text = "等回覆";
                break;
            case "interviewing":
                text = "已面試";
                break;
            case "offer":
                text = "錄取";
                break;
            case "rejected":
                text = "未錄取";
                break;
            case "closed":
                text = "職缺關閉";
                break;
            case "archived":
                text = "封存";
                break;
            case "archived_wait_other_job_result":
                text = "已封存，等待其他面試結果";
                break;
            default:
                text = string.IsNullOrEmpty(status) ? "未檢視" : status;
                break;
        }

        return isExpired ? text + "（已逾期）" : text;
    }
}
