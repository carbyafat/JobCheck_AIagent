using System.Text;
using JobCheck.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 新增／編輯職缺的最小輸入表單。只收集文字與顯示結果，資料規則全部交給 JobPostingCommandService。
/// </summary>
public sealed class Panel_JobPostingCreate : MonoBehaviour
{
    [Header("Fields")]
    [SerializeField] private TMP_InputField inputCompanyName;
    [SerializeField] private TMP_InputField inputTitle;
    [SerializeField] private TMP_InputField inputSourcePlatform;
    [SerializeField] private TMP_InputField inputSourceUrl;
    [SerializeField] private TMP_InputField inputRawDescription;

    [Header("Actions")]
    [SerializeField] private TMP_Text textTitle;
    [SerializeField] private Button buttonSave;
    [SerializeField] private Button buttonCancel;
    [SerializeField] private TMP_Text textMessage;

    private AllJobPage owner;
    private string editingJobPostingId;

    private void Awake()
    {
        AutoBindReferences();
        BindButtons();
    }

    /// <summary>
    /// 清空上一次輸入並顯示表單。
    /// </summary>
    public void Show(AllJobPage page)
    {
        owner = page;
        editingJobPostingId = null;
        AutoBindReferences();
        BindButtons();
        ClearFields();
        SetPanelLabels("新增職缺", "儲存");
        SetMessage(string.Empty, false);
        gameObject.SetActive(true);

        if (inputCompanyName != null)
        {
            inputCompanyName.Select();
            inputCompanyName.ActivateInputField();
        }
    }

    /// <summary>
    /// 以既有資料開啟編輯模式。此表單只修改目前支援的五個欄位。
    /// </summary>
    public void ShowForEdit(AllJobPage page, JobDetailData data)
    {
        owner = page;
        editingJobPostingId = data != null ? data.id : null;
        AutoBindReferences();
        BindButtons();
        SetText(inputCompanyName, data != null && data.company != null ? data.company.name : null);
        SetText(inputTitle, data != null && data.job != null ? data.job.title : null);
        SetText(inputSourcePlatform, data != null && data.source != null ? data.source.platform : null);
        SetText(inputSourceUrl, data != null && data.source != null ? data.source.url : null);
        SetText(inputRawDescription, data != null && data.job != null ? data.job.raw_text : null);
        SetPanelLabels("編輯職缺", "儲存變更");
        SetMessage(string.Empty, false);
        gameObject.SetActive(true);

        if (inputCompanyName != null)
        {
            inputCompanyName.Select();
            inputCompanyName.ActivateInputField();
        }
    }

    public void Submit()
    {
        if (owner == null)
        {
            SetMessage("找不到職缺列表控制器。", true);
            return;
        }

        bool isEditing = !string.IsNullOrEmpty(editingJobPostingId);
        PersistenceStorageResult<JobPostingWriteSummary> result = isEditing
            ? owner.UpdateV02JobPosting(new JobPostingEditRequest
            {
                JobPostingId = editingJobPostingId,
                CompanyName = GetText(inputCompanyName),
                Title = GetText(inputTitle),
                SourcePlatform = GetText(inputSourcePlatform),
                SourceUrl = GetText(inputSourceUrl),
                RawDescription = GetText(inputRawDescription)
            })
            : owner.CreateV02JobPosting(new JobPostingCreateRequest
            {
                CompanyName = GetText(inputCompanyName),
                Title = GetText(inputTitle),
                SourcePlatform = GetText(inputSourcePlatform),
                SourceUrl = GetText(inputSourceUrl),
                RawDescription = GetText(inputRawDescription)
            });
        if (result.IsSuccess)
        {
            gameObject.SetActive(false);
            return;
        }

        var message = new StringBuilder(isEditing ? "無法儲存職缺：" : "無法新增職缺：");
        foreach (PersistenceStorageIssue issue in result.Issues)
        {
            message.Append('\n').Append("• ").Append(issue.Message);
        }

        SetMessage(message.ToString(), true);
    }

    public void Cancel()
    {
        ClearFields();
        SetMessage(string.Empty, false);
        gameObject.SetActive(false);
    }

    private void AutoBindReferences()
    {
        inputCompanyName = inputCompanyName ?? FindInput("Input_CompanyName");
        inputTitle = inputTitle ?? FindInput("Input_Title");
        inputSourcePlatform = inputSourcePlatform ?? FindInput("Input_SourcePlatform");
        inputSourceUrl = inputSourceUrl ?? FindInput("Input_SourceUrl");
        inputRawDescription = inputRawDescription ?? FindInput("Input_RawDescription");
        if (textTitle == null)
        {
            Transform title = transform.Find("Text_Title");
            textTitle = title == null ? null : title.GetComponent<TMP_Text>();
        }
        buttonSave = buttonSave ?? FindButton("Button_SaveJobPosting");
        buttonCancel = buttonCancel ?? FindButton("Button_CancelJobPosting");

        if (textMessage == null)
        {
            Transform child = transform.Find("Text_Message");
            textMessage = child == null ? null : child.GetComponent<TMP_Text>();
        }
    }

    private void BindButtons()
    {
        if (buttonSave != null)
        {
            buttonSave.onClick.RemoveListener(Submit);
            buttonSave.onClick.AddListener(Submit);
        }

        if (buttonCancel != null)
        {
            buttonCancel.onClick.RemoveListener(Cancel);
            buttonCancel.onClick.AddListener(Cancel);
        }
    }

    private TMP_InputField FindInput(string childName)
    {
        Transform child = transform.Find(childName);
        return child == null ? null : child.GetComponent<TMP_InputField>();
    }

    private Button FindButton(string childName)
    {
        Transform child = transform.Find(childName);
        return child == null ? null : child.GetComponent<Button>();
    }

    private void ClearFields()
    {
        SetText(inputCompanyName, string.Empty);
        SetText(inputTitle, string.Empty);
        SetText(inputSourcePlatform, string.Empty);
        SetText(inputSourceUrl, string.Empty);
        SetText(inputRawDescription, string.Empty);
    }

    private void SetPanelLabels(string title, string saveButtonLabel)
    {
        if (textTitle != null)
        {
            textTitle.text = title;
        }

        if (buttonSave != null)
        {
            TMP_Text label = buttonSave.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = saveButtonLabel;
            }
        }
    }

    private void SetMessage(string message, bool isError)
    {
        if (textMessage == null)
        {
            return;
        }

        textMessage.text = message;
        textMessage.color = isError
            ? new Color(0.75f, 0.12f, 0.12f, 1f)
            : new Color(0.15f, 0.45f, 0.2f, 1f);
    }

    private static string GetText(TMP_InputField input)
    {
        return input == null ? null : input.text;
    }

    private static void SetText(TMP_InputField input, string value)
    {
        if (input != null)
        {
            input.SetTextWithoutNotify(value);
        }
    }
}
