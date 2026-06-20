using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Panel_SingleJob : MonoBehaviour
{
    [SerializeField] private TMP_Text textCompany;
    [SerializeField] private TMP_Text textJobName;
    [SerializeField] private TMP_Text textSalary;
    [SerializeField] private TMP_Text textStatus;
    [SerializeField] private Button buttonOpenDetail;
    [SerializeField] private float displayFontSize = 22f;

    private JobSummaryData currentData;

    private void Awake()
    {
        AutoBindReferences();
        BindButton();
    }

    public void SetData(JobSummaryData data)
    {
        currentData = data;

        SetText(textCompany, data != null ? data.company : string.Empty);
        SetText(textJobName, data != null ? data.title : string.Empty);
        SetText(textSalary, data != null ? data.salary : string.Empty);
        SetText(textStatus, data != null ? ConvertStatusToDisplayText(data.status) : string.Empty);
        ApplyDisplayFontSize();
    }

    public void Clear()
    {
        currentData = null;
        SetText(textCompany, string.Empty);
        SetText(textJobName, string.Empty);
        SetText(textSalary, string.Empty);
        SetText(textStatus, string.Empty);
    }

    public void OnClickJob()
    {
        if (currentData == null)
        {
            return;
        }

        LoadJobDetail(currentData);
    }

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

        // Keep the loaded json available here when the detail UI is ready.
        _ = json;
    }

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
    }

    private void BindButton()
    {
        if (buttonOpenDetail == null)
        {
            return;
        }

        buttonOpenDetail.onClick.RemoveListener(OnClickJob);
        buttonOpenDetail.onClick.AddListener(OnClickJob);
    }

    private TMP_Text FindChildText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private void ApplyDisplayFontSize()
    {
        SetFontSize(textCompany);
        SetFontSize(textJobName);
        SetFontSize(textSalary);
        SetFontSize(textStatus);
    }

    private void SetFontSize(TMP_Text target)
    {
        if (target != null && displayFontSize > 0f)
        {
            target.fontSize = displayFontSize;
        }
    }

    private string ConvertStatusToDisplayText(string status)
    {
        switch (status)
        {
            case "not_viewed":
                return "未檢視";
            case "not_applied":
                return "未投遞";
            case "interested":
                return "有興趣";
            case "applied":
                return "已投遞";
            case "interviewing":
                return "已面試";
            case "offer":
                return "錄取";
            case "rejected":
                return "未錄取";
            case "closed":
                return "職缺關閉";
            case "archived":
                return "封存";
            default:
                return string.IsNullOrEmpty(status) ? "未檢視" : status;
        }
    }
}
