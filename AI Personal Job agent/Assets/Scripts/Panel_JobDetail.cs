using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JobCheck.Domain;
using JobCheck.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 職缺詳細頁控制器。負責顯示完整職缺內容、切換狀態、刷新狀態按鈕與返回總攬頁。
/// </summary>
public class Panel_JobDetail : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] private JobCheckUiTheme theme;

    [Header("Current UI")]
    [Tooltip("公司名稱 / 職缺名稱標題文字。")]
    [SerializeField] private TMP_Text textCompanyTitle;
    [Tooltip("目前狀態文字。逾期時會變紅。")]
    [SerializeField] private TMP_Text textStatus;
    [Tooltip("薪資文字。")]
    [SerializeField] private TMP_Text textSalary;
    [Tooltip("工作地點文字。")]
    [SerializeField] private TMP_Text textWorkPosition;
    [Tooltip("工作模式文字。")]
    [SerializeField] private TMP_Text textWorkMode;
    [Tooltip("經驗需求文字。")]
    [SerializeField] private TMP_Text textExperience;
    [Tooltip("學歷需求文字。")]
    [SerializeField] private TMP_Text textEducationNeed;
    [Tooltip("工作內容文字。")]
    [SerializeField] private TMP_Text textWorkContent;
    [Tooltip("技能需求標題文字。")]
    [SerializeField] private TMP_Text textSkillHead;
    [Tooltip("工具/技術需求文字。")]
    [SerializeField] private TMP_Text textSkillTool;
    [Tooltip("技能需求文字。")]
    [SerializeField] private TMP_Text textSkillTech;
    [Tooltip("福利制度文字。")]
    [SerializeField] private TMP_Text textWelfare;
    [Tooltip("招募流程文字。")]
    [SerializeField] private TMP_Text textRecruitmentProcess;
    [Tooltip("其他資訊文字。")]
    [SerializeField] private TMP_Text textOther;

    [Header("Layout")]
    [Tooltip("返回總攬頁按鈕。")]
    [SerializeField] private Button buttonBack;
    [Tooltip("需要強制重排的根節點。")]
    [SerializeField] private RectTransform layoutRoot;

    [Header("Event History")]
    [Tooltip("開啟／關閉 V0.2 應徵事件歷程。")]
    [SerializeField] private Button buttonShowEventHistory;
    [Tooltip("獨立顯示應徵事件歷程的面板。")]
    [SerializeField] private GameObject panelEventHistory;
    [Tooltip("事件歷程清單文字。")]
    [SerializeField] private TMP_Text textEventHistory;
    [Tooltip("關閉事件歷程面板。")]
    [SerializeField] private Button buttonCloseEventHistory;

    [Header("Resume Requirement Match")]
    [Tooltip("開啟履歷條件比對視窗。")]
    [SerializeField] private Button buttonShowRequirementMatch;
    [Tooltip("阻擋背景操作的履歷條件比對視窗。")]
    [SerializeField] private GameObject panelRequirementMatch;
    [Tooltip("履歷條件比對結果。")]
    [SerializeField] private TMP_Text textRequirementMatch;
    [Tooltip("關閉履歷條件比對視窗。")]
    [SerializeField] private Button buttonCloseRequirementMatch;

    [Header("Status Panel")]
    [Tooltip("開關狀態按鈕面板的按鈕。")]
    [SerializeField] private Button buttonShowStatusPanel;
    [Tooltip("狀態按鈕面板。")]
    [SerializeField] private GameObject panelStatusBtn;
    [Tooltip("目前狀態按鈕顏色。")]
    [SerializeField] private Color activeStatusButtonColor = new Color(1f, 0.25f, 0.25f, 1f);
    [Tooltip("非目前狀態按鈕顏色。")]
    [SerializeField] private Color inactiveStatusButtonColor = Color.white;
    [Tooltip("正常狀態文字顏色。")]
    [SerializeField] private Color normalStatusTextColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    [Tooltip("逾期狀態文字顏色。")]
    [SerializeField] private Color expiredStatusTextColor = Color.red;

    [Header("Follow-up Date")]
    [Tooltip("到期日顯示文字。")]
    [SerializeField] private TMP_Text textExpireDay;
    [Tooltip("開啟到期日輸入 UI 的按鈕。")]
    [SerializeField] private Button buttonManualSetExpireDay;

    [Header("Detail Input Dialog")]
    [Tooltip("整合日期與多行文字輸入的共用視窗。")]
    [SerializeField] private Panel_DetailInputDialog inputDialog;

    [Header("Status Buttons")]
    [Tooltip("切換為未檢視。")]
    [SerializeField] private Button buttonNotViewed;
    [Tooltip("切換為有興趣。")]
    [SerializeField] private Button buttonInterested;
    [Tooltip("切換為確認不投。")]
    [SerializeField] private Button buttonNotApplying;
    [Tooltip("切換為已投遞。")]
    [SerializeField] private Button buttonApplied;
    [Tooltip("切換為已預約面試。")]
    [SerializeField] private Button buttonWaitInterview;
    [Tooltip("切換為已面試。")]
    [SerializeField] private Button buttonWithInterview;
    [Tooltip("切換為等回覆。")]
    [SerializeField] private Button buttonWaitingReply;
    [Tooltip("記錄一次長期無回覆事件，不改變目前流程階段。")]
    [SerializeField] private Button buttonNoResponse;
    [Tooltip("切換為錄取。")]
    [SerializeField] private Button buttonOffer;
    [Tooltip("切換為未錄取。")]
    [SerializeField] private Button buttonRejected;
    [Tooltip("切換為封存。")]
    [SerializeField] private Button buttonArchived;
    [Tooltip("切換為已封存，等待其他面試結果。")]
    [SerializeField] private Button buttonArchivedWaitOtherJobResult;
    [Tooltip("V0.2 編輯 Application 備註。")]
    [SerializeField] private Button buttonEditNotes;
    [Tooltip("V0.2 編輯目前職缺的基本資料。")]
    [SerializeField] private Button buttonEditJobPosting;

    [Header("Delete Job Posting")]
    [Tooltip("開啟可復原刪除確認畫面；只有個人資料區可使用。")]
    [SerializeField] private Button buttonDeleteJobPosting;
    [Tooltip("刪除前的二次確認面板。")]
    [SerializeField] private GameObject panelDeleteConfirmation;
    [Tooltip("顯示即將刪除的公司、職稱與影響範圍。")]
    [SerializeField] private TMP_Text textDeleteConfirmation;
    [Tooltip("確認將職缺與相關應徵資料移入回收區。")]
    [SerializeField] private Button buttonConfirmDelete;
    [Tooltip("取消刪除並關閉確認面板。")]
    [SerializeField] private Button buttonCancelDelete;

    private JobDetailData currentData;
    private JobTrackingData currentTracking;
    private AllJobPage allJobPage;
    private bool isReadOnly;
    private DetailInputMode inputMode;
    private string pendingStatus;
    private CandidateCloseReason? pendingCloseReason;
    private string pendingCloseReasonNote;
    private DateTimeOffset? pendingScheduledFor;

    private enum DetailInputMode
    {
        FollowUp,
        InterviewSchedule,
        CandidateClose,
        EventOccurredAt,
        Notes
    }

    private void Awake()
    {
        AutoBindReferences();
        EnsureRequirementMatchUi();
        ApplyTheme();
        BindButtons();
        SetEventHistoryVisible(false);
        SetDeleteConfirmationVisible(false);
        SetRequirementMatchVisible(false);
    }

    /// <summary>
    /// 顯示詳細頁，並套用 tracking 資料。
    /// </summary>
    /// <param name="data">職缺詳細資料。</param>
    /// <param name="tracking">使用者操作追蹤資料。</param>
    public void Show(JobDetailData data, JobTrackingData tracking)
    {
        currentData = data;
        currentTracking = tracking;
        gameObject.SetActive(true);
        if (buttonShowEventHistory != null)
        {
            buttonShowEventHistory.gameObject.SetActive(true);
        }
        SetEventHistoryVisible(false);
        SetDeleteConfirmationVisible(false);
        SetRequirementMatchVisible(false);
        // 詳情物件若原本 inactive，Awake 會在上一行才完成自動綁定；此時再套一次唯讀狀態。
        SetReadOnly(isReadOnly);
        ApplyV02Labels();
        Refresh(data);
    }

    /// <summary>
    /// 根據目前職缺與 tracking 重新刷新詳細頁。
    /// </summary>
    /// <param name="data">職缺詳細資料。</param>
    public void Refresh(JobDetailData data)
    {
        if (data == null)
        {
            Clear();
            return;
        }

        string title = FormatTitle(data);
        bool isExpired = IsCurrentTrackingExpired();
        string status = FormatStatus(GetCurrentStatus(), isExpired);
        string benefits = BuildBenefitsText(data.benefits);

        SetText(textCompanyTitle, title);
        SetText(textStatus, "狀態: " + status);
        SetStatusTextColor(isExpired);
        SetText(textSalary, BuildLabelLine("薪資", JobCheckV02DisplayAdapter.FormatCompensation(data.compensation)));
        SetText(textWorkPosition, BuildLabelLine("工作地點", data.location != null ? data.location.raw_text : string.Empty));
        SetText(textWorkMode, BuildLabelLine("工作模式", data.location != null ? FormatWorkMode(data.location.work_mode) : string.Empty));
        SetText(textExperience, BuildLabelLine("經驗", data.requirements != null ? data.requirements.experience : string.Empty));
        SetText(textEducationNeed, BuildLabelLine("學歷需求", data.requirements != null ? data.requirements.education : string.Empty));
        SetText(textWorkContent, BuildWorkContent(data));
        SetText(textSkillHead, "技能需求");
        SetText(textSkillTool, BuildLabelLine("技能需求 工具/技術", data.requirements != null ? JoinList(data.requirements.tools) : string.Empty));
        SetText(textSkillTech, BuildLabelLine("技能需求 技能", data.requirements != null ? JoinList(data.requirements.skills) : string.Empty));
        SetText(textWelfare, benefits);
        SetText(textRecruitmentProcess, BuildListSection("招募流程", data.recruitment_process));
        SetText(textOther, BuildOtherText(data));
        ApplyTheme();
        SetStatusTextColor(isExpired);
        RefreshEventHistoryText();
        RefreshExpireDayText();

        RefreshStatusButtonColors();
        ForceBuildLayout();
    }

    /// <summary>
    /// 清空詳細頁內容。
    /// </summary>
    public void Clear()
    {
        currentData = null;
        currentTracking = null;
        SetText(textCompanyTitle, string.Empty);
        SetText(textStatus, string.Empty);
        SetText(textSalary, string.Empty);
        SetText(textWorkPosition, string.Empty);
        SetText(textWorkMode, string.Empty);
        SetText(textExperience, string.Empty);
        SetText(textEducationNeed, string.Empty);
        SetText(textWorkContent, string.Empty);
        SetText(textSkillHead, string.Empty);
        SetText(textSkillTool, string.Empty);
        SetText(textSkillTech, string.Empty);
        SetText(textWelfare, string.Empty);
        SetText(textRecruitmentProcess, string.Empty);
        SetText(textOther, string.Empty);
        SetText(textExpireDay, string.Empty);
        SetText(textEventHistory, string.Empty);
        SetEventHistoryVisible(false);
        SetDeleteConfirmationVisible(false);
        SetRequirementMatchVisible(false);
        HideInputDialog();
        ForceBuildLayout();
    }

    /// <summary>
    /// 關閉詳細頁並返回總攬頁。
    /// </summary>
    public void Hide()
    {
        SetRequirementMatchVisible(false);
        gameObject.SetActive(false);

        if (allJobPage == null)
        {
            allJobPage = FindObjectOfType<AllJobPage>(true);
        }

        if (allJobPage != null)
        {
            allJobPage.ShowAllJobPage();
        }
    }

    /// <summary>
    /// 將目前 V0.2 職缺交給總覽頁，以共用表單開啟編輯模式。
    /// </summary>
    public void EditJobPosting()
    {
        if (currentData == null)
        {
            return;
        }

        if (allJobPage == null)
        {
            allJobPage = FindObjectOfType<AllJobPage>(true);
        }

        if (allJobPage == null)
        {
            Debug.LogWarning("AllJobPage not found; cannot edit job posting.");
            return;
        }

        allJobPage.ShowEditJobPosting(currentData);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 顯示刪除影響範圍。實際資料在第二次確認前不會移動。
    /// </summary>
    public void ShowDeleteConfirmation()
    {
        if (currentData == null || allJobPage == null || !allJobPage.CanDeleteJobPostings)
        {
            return;
        }

        string company = currentData.company != null ? currentData.company.name : "未知公司";
        string title = currentData.job != null ? currentData.job.title : "未知職缺";
        if (panelDeleteConfirmation != null)
        {
            TMP_Text[] panelTexts = panelDeleteConfirmation.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text panelText in panelTexts)
            {
                if (panelText.name == "TMP_EventHistoryTitle_V02")
                {
                    panelText.text = "刪除職缺";
                    break;
                }
            }
        }

        SetText(
            textDeleteConfirmation,
            "確認刪除以下職缺？\n\n"
            + company + " / " + title
            + "\n\n相關投遞與事件會一併移至 personal_data/trash。"
            + "\n公司資料會保留，之後仍可手動復原。");
        SetDeleteConfirmationVisible(true);
        SetEventHistoryVisible(false);
        SetRequirementMatchVisible(false);
        if (panelStatusBtn != null)
        {
            panelStatusBtn.SetActive(false);
        }
    }

    /// <summary>
    /// 執行可復原刪除，成功後返回已重新載入的職缺總覽。
    /// </summary>
    public void ConfirmDeleteJobPosting()
    {
        if (currentData == null || allJobPage == null)
        {
            return;
        }

        PersistenceStorageResult<JobPostingTrashSummary> result =
            allJobPage.DeleteV02JobPosting(currentData.id);
        if (!result.IsSuccess)
        {
            SetText(
                textDeleteConfirmation,
                "刪除失敗，資料沒有完成移動。\n請查看 Console 的詳細錯誤。");
            return;
        }

        SetDeleteConfirmationVisible(false);
        gameObject.SetActive(false);
        allJobPage.ShowAllJobPage();
    }

    public void CancelDeleteJobPosting()
    {
        SetDeleteConfirmationVisible(false);
    }

    /// <summary>
    /// 指定總攬頁控制器，用於返回與更新 tracking。
    /// </summary>
    /// <param name="page">總攬頁控制器。</param>
    public void SetAllJobPage(AllJobPage page)
    {
        allJobPage = page;
    }

    /// <summary>
    /// 切換獨立的應徵事件歷程面板。歷程為不可變唯讀資訊。
    /// </summary>
    public void ToggleEventHistory()
    {
        if (panelEventHistory == null)
        {
            return;
        }

        RefreshEventHistoryText();
        bool shouldShow = !panelEventHistory.activeSelf;
        SetEventHistoryVisible(shouldShow);
        if (shouldShow)
        {
            SetRequirementMatchVisible(false);
            SetDeleteConfirmationVisible(false);
        }
        if (shouldShow && panelStatusBtn != null)
        {
            panelStatusBtn.SetActive(false);
        }
    }

    /// <summary>
    /// 關閉事件歷程面板，不修改任何事件資料。
    /// </summary>
    public void CloseEventHistory()
    {
        SetEventHistoryVisible(false);
    }

    /// <summary>開啟阻擋背景操作的履歷條件比對視窗。</summary>
    public void ShowRequirementMatch()
    {
        RefreshRequirementMatchText();
        SetEventHistoryVisible(false);
        SetDeleteConfirmationVisible(false);
        if (panelStatusBtn != null) panelStatusBtn.SetActive(false);
        HideInputDialog();
        SetRequirementMatchVisible(true);
    }

    /// <summary>關閉履歷條件比對視窗。</summary>
    public void CloseRequirementMatch()
    {
        SetRequirementMatchVisible(false);
    }

    private void ApplyV02Labels()
    {
        SetButtonLabel(buttonNotViewed, "公司已讀");
        SetButtonLabel(buttonNotApplying, "主動放棄");
        SetButtonLabel(buttonWaitInterview, "安排面試");
        SetButtonLabel(buttonWithInterview, "已完成面試");
        SetButtonLabel(buttonWaitingReply, "等待回覆");
        SetButtonLabel(buttonArchived, "收藏 / 取消收藏");
        SetButtonLabel(buttonArchivedWaitOtherJobResult, "公司已聯絡");
        SetButtonLabel(buttonManualSetExpireDay, "設定下次追蹤日");
        SetText(textExpireDay, "下次追蹤日: -");
        if (buttonEditNotes != null)
        {
            buttonEditNotes.gameObject.SetActive(true);
        }

        if (buttonDeleteJobPosting != null)
        {
            bool canDelete = allJobPage != null && allJobPage.CanDeleteJobPostings;
            SetButtonLabel(
                buttonDeleteJobPosting,
                canDelete ? "刪除職缺" : "Demo 不可刪除");
            SetButtonInteractable(buttonDeleteJobPosting, canDelete && !isReadOnly);
        }

        RefreshNoResponseButton();
    }

    /// <summary>
    /// 設定詳細頁是否只允許查看。
    /// </summary>
    /// <param name="value">true 表示唯讀。</param>
    public void SetReadOnly(bool value)
    {
        isReadOnly = value;
        SetButtonInteractable(buttonShowStatusPanel, !value);
        SetButtonInteractable(buttonManualSetExpireDay, !value);
        if (inputDialog != null) inputDialog.SetInteractable(!value);
        SetButtonInteractable(buttonNotViewed, !value);
        SetButtonInteractable(buttonInterested, !value);
        SetButtonInteractable(buttonNotApplying, !value);
        SetButtonInteractable(buttonApplied, !value);
        SetButtonInteractable(buttonWaitInterview, !value);
        SetButtonInteractable(buttonWithInterview, !value);
        SetButtonInteractable(buttonWaitingReply, !value);
        RefreshNoResponseButton();
        SetButtonInteractable(buttonOffer, !value);
        SetButtonInteractable(buttonRejected, !value);
        SetButtonInteractable(buttonArchived, !value);
        SetButtonInteractable(buttonArchivedWaitOtherJobResult, !value);
        SetButtonInteractable(buttonEditNotes, !value);
        SetButtonInteractable(buttonEditJobPosting, !value);
        SetButtonInteractable(
            buttonDeleteJobPosting,
            !value && allJobPage != null && allJobPage.CanDeleteJobPostings);

        if (value)
        {
            if (panelStatusBtn != null)
            {
                panelStatusBtn.SetActive(false);
            }

            HideInputDialog();
            SetDeleteConfirmationVisible(false);
        }
    }

    /// <summary>
    /// 切換為未檢視。
    /// </summary>
    public void SetStatusNotViewed()
    {
        ChangeStatus("viewed");
    }

    /// <summary>
    /// 切換為有興趣。
    /// </summary>
    public void SetStatusInterested()
    {
        ChangeStatus("interested");
    }

    /// <summary>
    /// 切換為確認不投。
    /// </summary>
    public void SetStatusNotApplying()
    {
        CloseStatusPanel();
        BeginPendingStatus("not_applying");
        inputMode = DetailInputMode.CandidateClose;
        ShowTextInput(
            "不再應徵原因",
            "可輸入：薪資、博弈、通勤、工時、週末、職務、技術、公司、更好機會、無回覆，或填寫其他說明。",
            string.Empty,
            "下一步");
    }

    /// <summary>
    /// 切換為已投遞。
    /// </summary>
    public void SetStatusApplied()
    {
        ChangeStatus("applied");
    }

    /// <summary>
    /// 切換為已預約面試。
    /// </summary>
    public void SetStatusWaitInterview()
    {
        CloseStatusPanel();
        BeginPendingStatus("interview_scheduled");
        inputMode = DetailInputMode.InterviewSchedule;
        ShowDateInput(
            "設定面試日期",
            "請輸入預定面試日期，格式為 yyyy.MM.dd。",
            string.Empty,
            "下一步");
    }

    /// <summary>
    /// 切換為已面試。
    /// </summary>
    public void SetStatusWithInterview()
    {
        ChangeStatus("interviewing");
    }

    /// <summary>
    /// 切換為等回覆。
    /// </summary>
    public void SetStatusWaitingReply()
    {
        ChangeStatus("waiting_reply");
    }

    /// <summary>
    /// 記錄本次追蹤為長期無回覆；事件會保留，但不會覆蓋目前應徵階段。
    /// </summary>
    public void MarkNoResponse()
    {
        if (!HasCurrentApplication())
        {
            Debug.LogWarning("尚未建立 Application；請先標記有興趣或已投遞，再記錄長期無回覆。", this);
            CloseStatusPanel();
            return;
        }

        ChangeStatus("no_response");
    }

    /// <summary>
    /// 切換為錄取。
    /// </summary>
    public void SetStatusOffer()
    {
        ChangeStatus("offer");
    }

    /// <summary>
    /// 切換為未錄取。
    /// </summary>
    public void SetStatusRejected()
    {
        ChangeStatus("rejected");
    }

    /// <summary>
    /// 切換為封存。
    /// </summary>
    public void SetStatusArchived()
    {
        if (currentData != null && allJobPage != null)
        {
            bool nextValue = currentTracking == null || !currentTracking.favorite;
            currentTracking = allJobPage.UpdateV02Favorite(currentData.id, nextValue);
            Refresh(currentData);
            CloseStatusPanel();
            return;
        }

        Debug.LogWarning("缺少 V0.2 職缺或總覽頁，無法切換收藏。", this);
    }

    /// <summary>
    /// 切換為已封存，等待其他面試結果。
    /// </summary>
    public void SetStatusArchivedWaitOtherJobResult()
    {
        ChangeStatus("contacted");
    }

    /// <summary>
    /// 開關狀態按鈕面板。
    /// </summary>
    public void ToggleStatusPanel()
    {
        if (isReadOnly)
        {
            Debug.LogWarning("V0.2 詳細頁目前是唯讀模式，狀態面板不提供修改。");
            return;
        }

        if (panelStatusBtn == null)
        {
            return;
        }

        panelStatusBtn.SetActive(!panelStatusBtn.activeSelf);
        RefreshStatusButtonColors();
    }

    /// <summary>
    /// 顯示到期日輸入框與確認按鈕。
    /// </summary>
    public void ShowExpireDayInput()
    {
        if (isReadOnly)
        {
            Debug.LogWarning("V0.2 詳細頁目前是唯讀模式，無法設定追蹤日期。");
            return;
        }

        inputMode = DetailInputMode.FollowUp;
        ShowDateInput(
            "設定下次追蹤日",
            "請輸入下一次要回頭確認此職缺的日期，格式為 yyyy.MM.dd。",
            GetCurrentManualExpireDayForInput(),
            "確認追蹤日");
    }

    /// <summary>
    /// 確認到期日輸入，格式必須為 yyyy.mm.dd。
    /// </summary>
    public void ConfirmExpireDayInput()
    {
        if (isReadOnly)
        {
            Debug.LogWarning("V0.2 詳細頁目前是唯讀模式，追蹤日期沒有寫入。");
            return;
        }

        if (currentData == null || allJobPage == null || inputDialog == null)
        {
            return;
        }

        string input = inputDialog.Value.Trim();
        if (inputMode == DetailInputMode.CandidateClose)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                inputDialog.SetError("放棄原因不可空白。");
                return;
            }

            ParseCandidateCloseReason(
                input,
                out CandidateCloseReason reason,
                out string reasonNote);
            pendingCloseReason = reason;
            pendingCloseReasonNote = reasonNote;
            ShowEventOccurredAtInput();
            return;
        }

        if (inputMode == DetailInputMode.Notes)
        {
            currentTracking = allJobPage.UpdateV02Notes(currentData.id, input);
            HideInputDialog();
            Refresh(currentData);
            return;
        }

        if (inputMode == DetailInputMode.EventOccurredAt)
        {
            DateTimeOffset? occurredAt = null;
            if (!string.IsNullOrWhiteSpace(input))
            {
                if (!DateTime.TryParseExact(
                    input,
                    "yyyy.MM.dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime occurredDate))
                {
                    inputDialog.SetError("日期格式錯誤，請輸入 yyyy.MM.dd；留空代表現在。");
                    return;
                }

                occurredAt = new DateTimeOffset(
                    occurredDate.Year,
                    occurredDate.Month,
                    occurredDate.Day,
                    12,
                    0,
                    0,
                    DateTimeOffset.Now.Offset);
            }

            currentTracking = allJobPage.UpdateV02ApplicationStatus(
                currentData.id,
                pendingStatus,
                pendingCloseReason,
                pendingCloseReasonNote,
                pendingScheduledFor,
                occurredAt);
            ResetPendingStatus();
            HideInputDialog();
            Refresh(currentData);
            return;
        }

        DateTime parsedDate;
        if (!DateTime.TryParseExact(input, "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
        {
            inputDialog.SetError("日期格式錯誤，請輸入 yyyy.MM.dd。");
            return;
        }

        DateTimeOffset expireAt = new DateTimeOffset(
            parsedDate.Year,
            parsedDate.Month,
            parsedDate.Day,
            23,
            59,
            59,
            DateTimeOffset.Now.Offset);

        if (inputMode == DetailInputMode.InterviewSchedule)
        {
            pendingScheduledFor = expireAt;
            ShowEventOccurredAtInput();
            return;
        }
        else
        {
            currentTracking = allJobPage.UpdateJobTrackingManualExpireAt(
                currentData.id,
                expireAt.ToString("yyyy-MM-ddTHH:mm:sszzz"));
        }
        HideInputDialog();
        Refresh(currentData);
    }

    /// <summary>
    /// 取消目前的日期、放棄原因或備註輸入，不進行任何資料寫入。
    /// </summary>
    public void CancelInput()
    {
        inputMode = DetailInputMode.FollowUp;
        ResetPendingStatus();
        HideInputDialog();
        RefreshExpireDayText();
    }

    /// <summary>
    /// 準備修改目前職缺狀態；先讓使用者選填事件實際發生日，再一次寫回 tracking。
    /// </summary>
    /// <param name="status">新的狀態代碼。</param>
    private void ChangeStatus(string status)
    {
        if (isReadOnly)
        {
            Debug.LogWarning("V0.2 詳細頁目前是唯讀模式，狀態沒有寫入。");
            return;
        }

        if (currentData == null || allJobPage == null)
        {
            return;
        }

        CloseStatusPanel();
        BeginPendingStatus(status);
        ShowEventOccurredAtInput();
    }

    private void BeginPendingStatus(string status)
    {
        pendingStatus = status;
        pendingCloseReason = null;
        pendingCloseReasonNote = null;
        pendingScheduledFor = null;
    }

    private void ShowEventOccurredAtInput()
    {
        inputMode = DetailInputMode.EventOccurredAt;
        string actionTitle = string.IsNullOrEmpty(pendingStatus)
            ? "設定事件日期"
            : FormatStatus(pendingStatus);
        ShowDateInput(
            actionTitle,
            "請輸入「" + actionTitle + "」的實際發生日，格式為 yyyy.MM.dd；留空代表現在。",
            string.Empty,
            "記錄事件");
    }

    private void ResetPendingStatus()
    {
        pendingStatus = null;
        pendingCloseReason = null;
        pendingCloseReasonNote = null;
        pendingScheduledFor = null;
    }

    /// <summary>
    /// 完成一次狀態操作後收起狀態選單，避免遮住詳細資料。
    /// 即使寫入因流程規則被拒絕，也會結束這次選擇操作；錯誤仍保留在 Console。
    /// </summary>
    private void CloseStatusPanel()
    {
        if (panelStatusBtn != null)
        {
            panelStatusBtn.SetActive(false);
        }
    }

    /// <summary>
    /// 依目前狀態刷新狀態按鈕顏色。
    /// </summary>
    private void RefreshStatusButtonColors()
    {
        string currentStatus = GetCurrentStatus();

        SetStatusButtonColor(buttonNotViewed, currentStatus == "viewed");
        SetStatusButtonColor(buttonInterested, currentStatus == "interested");
        SetStatusButtonColor(buttonNotApplying, currentStatus == "not_applying");
        SetStatusButtonColor(buttonApplied, currentStatus == "applied");
        SetStatusButtonColor(buttonWaitInterview, currentStatus == "interview_scheduled");
        SetStatusButtonColor(buttonWithInterview, currentStatus == "interviewing");
        SetStatusButtonColor(buttonWaitingReply, currentStatus == "waiting_reply");
        SetStatusButtonColor(buttonNoResponse, false);
        SetStatusButtonColor(buttonOffer, currentStatus == "offer");
        SetStatusButtonColor(buttonRejected, currentStatus == "rejected");
        SetStatusButtonColor(buttonArchived, currentTracking != null && currentTracking.favorite);
        SetStatusButtonColor(buttonArchivedWaitOtherJobResult, currentStatus == "contacted");
        RefreshNoResponseButton();
    }

    private bool HasCurrentApplication()
    {
        return currentTracking != null
            && !string.IsNullOrWhiteSpace(currentTracking.application_id);
    }

    private void RefreshNoResponseButton()
    {
        bool hasApplication = HasCurrentApplication();
        SetButtonInteractable(buttonNoResponse, !isReadOnly && hasApplication);
        SetButtonLabel(
            buttonNoResponse,
            hasApplication
                ? "標記長期無回覆"
                : "無回覆（先標記有興趣或已投遞）");
    }

    /// <summary>
    /// 設定單一狀態按鈕顏色。
    /// </summary>
    /// <param name="button">目標按鈕。</param>
    /// <param name="isActive">是否為目前狀態。</param>
    private void SetStatusButtonColor(Button button, bool isActive)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = isActive ? activeStatusButtonColor : inactiveStatusButtonColor;
        }
    }

    private void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    /// <summary>
    /// 強制刷新 LayoutGroup 與 Canvas 排版。
    /// </summary>
    public void ForceBuildLayout()
    {
        Canvas.ForceUpdateCanvases();

        RectTransform target = layoutRoot != null ? layoutRoot : transform as RectTransform;
        if (target == null)
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(target);

        LayoutGroup[] layoutGroups = target.GetComponentsInChildren<LayoutGroup>(true);
        foreach (LayoutGroup layoutGroup in layoutGroups)
        {
            RectTransform rect = layoutGroup.transform as RectTransform;
            if (rect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// 組合公司名稱與職缺名稱。
    /// </summary>
    /// <param name="data">職缺詳細資料。</param>
    /// <returns>標題文字。</returns>
    private string FormatTitle(JobDetailData data)
    {
        string company = data.company != null ? data.company.name : string.Empty;
        string title = data.job != null ? data.job.title : string.Empty;
        return CombineWithSlash(company, title);
    }

    /// <summary>
    /// 建立福利制度區塊文字。
    /// </summary>
    /// <param name="benefits">福利資料。</param>
    /// <returns>福利文字。</returns>
    private string BuildBenefitsText(BenefitsJsonData benefits)
    {
        if (benefits == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        AppendHeader(builder, "福利制度");
        AppendList(builder, "薪資獎金", benefits.salary_bonus);
        AppendList(builder, "保險健康", benefits.insurance_health);
        AppendList(builder, "彈性制度", benefits.flexibility);
        AppendList(builder, "教育訓練", benefits.training);
        AppendList(builder, "生活福利", benefits.life);
        AppendList(builder, "其他福利", benefits.other);
        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 建立其他資訊區塊文字。
    /// </summary>
    /// <param name="data">職缺詳細資料。</param>
    /// <returns>其他資訊文字。</returns>
    private string BuildOtherText(JobDetailData data)
    {
        StringBuilder builder = new StringBuilder();

        if (data == null)
        {
            return string.Empty;
        }

        AppendLine(builder, "來源", data.source != null ? data.source.platform : string.Empty);
        AppendLine(builder, "網址", data.source != null ? data.source.url : string.Empty);
        AppendLine(builder, "職缺標籤", FormatJobLabels(data.tags, false));
        AppendLine(builder, "風險提醒", FormatJobLabels(data.risk_flags, true));
        AppendLine(builder, "工作性質", data.work_conditions != null ? data.work_conditions.employment_type : string.Empty);
        AppendLine(builder, "上班時段", data.work_conditions != null ? data.work_conditions.working_hours : string.Empty);
        AppendLine(builder, "需求人數", data.job != null ? data.job.openings : string.Empty);
        AppendLine(builder, "目前狀態", FormatStatus(GetCurrentStatus()));
        AppendLine(builder, "最後操作日期", currentTracking != null ? currentTracking.last_action_at : string.Empty);
        AppendLine(builder, "下次追蹤日期", currentTracking != null ? currentTracking.manual_expire_at : string.Empty);
        AppendLine(builder, "是否逾期", IsCurrentTrackingExpired() ? "是" : "否");
        AppendLine(builder, "我的最愛", currentTracking != null && currentTracking.favorite ? "是" : "否");
        AppendLine(builder, "我的備註", currentTracking != null ? currentTracking.notes : string.Empty);
        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 將 Tag 或 RiskFlag 的穩定代碼轉為中文顯示文字。
    /// 未知的既有值仍原樣顯示，避免舊資料在畫面上消失。
    /// </summary>
    private string FormatJobLabels(IEnumerable<string> values, bool isRiskFlag)
    {
        if (values == null)
        {
            return string.Empty;
        }

        var displayValues = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(value.Trim()))
            {
                continue;
            }

            displayValues.Add(isRiskFlag
                ? JobPostingLabelCatalog.GetRiskFlagDisplayName(value)
                : JobPostingLabelCatalog.GetTagDisplayName(value));
        }

        return string.Join("、", displayValues);
    }

    private void RefreshEventHistoryText()
    {
        if (textEventHistory == null)
        {
            return;
        }

        if (currentTracking == null
            || currentTracking.event_history == null
            || currentTracking.event_history.Count == 0)
        {
            textEventHistory.text = "尚無應徵事件紀錄。";
            return;
        }

        var builder = new StringBuilder();
        foreach (string history in currentTracking.event_history)
        {
            if (string.IsNullOrWhiteSpace(history))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append("• ");
            builder.Append(history.Trim());
        }

        textEventHistory.text = builder.Length == 0
            ? "尚無應徵事件紀錄。"
            : builder.ToString();
    }

    private void SetEventHistoryVisible(bool value)
    {
        if (panelEventHistory != null)
        {
            panelEventHistory.SetActive(value);
        }
    }

    private void SetDeleteConfirmationVisible(bool value)
    {
        if (panelDeleteConfirmation != null)
        {
            panelDeleteConfirmation.SetActive(value);
        }
    }

    private void SetRequirementMatchVisible(bool value)
    {
        if (panelRequirementMatch == null)
        {
            return;
        }

        panelRequirementMatch.SetActive(value);
        if (value)
        {
            panelRequirementMatch.transform.SetAsLastSibling();
        }
    }

    private void RefreshRequirementMatchText()
    {
        SetText(textRequirementMatch, currentData == null
            ? "目前沒有可比對的職缺資料。"
            : JobRequirementMatchTextFormatter.Format(
                currentData.v027Match, currentData.v027Score));
    }

    /// <summary>
    /// 建立條列式區塊文字。
    /// </summary>
    /// <param name="title">區塊標題。</param>
    /// <param name="values">條列資料。</param>
    /// <returns>條列文字。</returns>
    private string BuildListSection(string title, List<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        AppendHeader(builder, title);
        foreach (string value in values)
        {
            if (!string.IsNullOrEmpty(value))
            {
                builder.Append("- ");
                builder.AppendLine(value);
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 優先顯示已結構化的工作內容；尚未解析時，改顯示建立職缺時保存的原始文字。
    /// </summary>
    /// <param name="data">職缺詳細資料。</param>
    /// <returns>可直接顯示於工作內容區塊的文字。</returns>
    private string BuildWorkContent(JobDetailData data)
    {
        string structuredContent = BuildListSection(
            "工作內容",
            data != null ? data.responsibilities : null);
        if (!string.IsNullOrEmpty(structuredContent))
        {
            return structuredContent;
        }

        string rawDescription = data != null && data.job != null
            ? data.job.raw_text
            : string.Empty;
        if (string.IsNullOrWhiteSpace(rawDescription))
        {
            return string.Empty;
        }

        return "原始職缺內容\n" + rawDescription.Trim();
    }

    /// <summary>
    /// 舊版詳情 Prefab 沒有比對視窗，執行時建立一致的阻擋式 Modal，
    /// 讓既有場景不需要重新手動綁定整組 UI。
    /// </summary>
    private void EnsureRequirementMatchUi()
    {
        if (buttonShowRequirementMatch != null && panelRequirementMatch != null
            && textRequirementMatch != null && buttonCloseRequirementMatch != null)
        {
            return;
        }

        TMP_FontAsset font = theme != null ? theme.BodyFont : textCompanyTitle?.font;
        Color primary = theme != null ? theme.Primary : new Color(0.49f, 0.27f, 0.31f, 1f);
        Color primaryHover = theme != null ? theme.PrimaryHover : primary;
        Color primaryPressed = theme != null ? theme.PrimaryPressed : primary;
        Color surface = theme != null ? theme.Surface : Color.white;
        Color surfaceMuted = theme != null
            ? theme.SurfaceMuted : new Color(0.94f, 0.92f, 0.92f, 1f);
        Color textPrimary = theme != null
            ? theme.TextPrimary : new Color(0.18f, 0.18f, 0.18f, 1f);
        Color textOnPrimary = theme != null ? theme.TextOnPrimary : Color.white;
        Color overlayColor = theme != null ? theme.Overlay : new Color(0f, 0f, 0f, 0.68f);

        if (buttonShowRequirementMatch == null)
        {
            GameObject buttonObject = CreateRuntimeUiObject(
                "Button_ShowRequirementMatch_V027", transform, typeof(Image), typeof(Button));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            AnchorAtTopRight(buttonRect, new Vector2(-745f, -55f), new Vector2(110f, 40f));
            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = surface;
            buttonShowRequirementMatch = buttonObject.GetComponent<Button>();
            buttonShowRequirementMatch.targetGraphic = buttonImage;
            SetButtonColors(buttonShowRequirementMatch, surface, surfaceMuted,
                theme != null ? theme.Border : surfaceMuted);
            TMP_Text label = CreateRuntimeText(buttonObject.transform, "Label", "履歷比對",
                font, theme != null ? theme.SupportingTextSize : 20f,
                textPrimary, TextAlignmentOptions.Center);
            StretchRuntime(label.rectTransform, new Vector2(6f, 4f), new Vector2(-6f, -4f));
        }

        if (panelRequirementMatch == null)
        {
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            Transform modalParent = rootCanvas != null ? rootCanvas.transform : transform;
            panelRequirementMatch = CreateRuntimeUiObject(
                "Panel_RequirementMatch_V027", modalParent, typeof(Image));
            RectTransform overlayRect = panelRequirementMatch.GetComponent<RectTransform>();
            StretchRuntime(overlayRect, Vector2.zero, Vector2.zero);
            Image overlay = panelRequirementMatch.GetComponent<Image>();
            overlay.color = overlayColor;
            overlay.raycastTarget = true;

            GameObject card = CreateRuntimeUiObject(
                "Card", panelRequirementMatch.transform, typeof(Image), typeof(Outline));
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(1300f, 760f);
            Image cardImage = card.GetComponent<Image>();
            cardImage.color = surface;
            cardImage.raycastTarget = true;
            Outline cardOutline = card.GetComponent<Outline>();
            cardOutline.effectColor = theme != null ? theme.Border : Color.gray;
            cardOutline.effectDistance = new Vector2(1f, -1f);

            TMP_Text title = CreateRuntimeText(card.transform, "Title", "履歷條件比對",
                font, theme != null ? theme.ModalTitleSize : 36f,
                textPrimary, TextAlignmentOptions.MidlineLeft);
            title.fontStyle = FontStyles.Bold;
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -18f);
            titleRect.sizeDelta = new Vector2(-190f, 64f);

            GameObject closeObject = CreateRuntimeUiObject(
                "Button_CloseRequirementMatch_V027", card.transform,
                typeof(Image), typeof(Button));
            RectTransform closeRect = closeObject.GetComponent<RectTransform>();
            AnchorAtTopRight(closeRect, new Vector2(-82f, -48f), new Vector2(120f, 52f));
            Image closeImage = closeObject.GetComponent<Image>();
            closeImage.color = primary;
            buttonCloseRequirementMatch = closeObject.GetComponent<Button>();
            buttonCloseRequirementMatch.targetGraphic = closeImage;
            SetButtonColors(buttonCloseRequirementMatch, primary, primaryHover, primaryPressed);
            TMP_Text closeLabel = CreateRuntimeText(closeObject.transform, "Label", "關閉",
                font, theme != null ? theme.ButtonTextSize : 22f,
                textOnPrimary, TextAlignmentOptions.Center);
            StretchRuntime(closeLabel.rectTransform, new Vector2(8f, 4f), new Vector2(-8f, -4f));

            GameObject viewportObject = CreateRuntimeUiObject(
                "ScrollView", card.transform, typeof(Image), typeof(RectMask2D),
                typeof(ScrollRect));
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            StretchRuntime(viewport, new Vector2(30f, 30f), new Vector2(-30f, -100f));
            viewportObject.GetComponent<Image>().color = surfaceMuted;

            GameObject contentObject = CreateRuntimeUiObject(
                "TMP_RequirementMatch_V027", viewportObject.transform,
                typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            textRequirementMatch = contentObject.GetComponent<TMP_Text>();
            textRequirementMatch.text = "履歷比對目前無法使用。";
            textRequirementMatch.font = font;
            textRequirementMatch.fontSize = theme != null ? theme.BodySize : 22f;
            textRequirementMatch.color = textPrimary;
            textRequirementMatch.alignment = TextAlignmentOptions.TopLeft;
            textRequirementMatch.enableWordWrapping = true;
            textRequirementMatch.margin = new Vector4(22f, 18f, 22f, 18f);
            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewportObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
        }

        panelRequirementMatch.transform.SetAsLastSibling();
    }

    private static GameObject CreateRuntimeUiObject(
        string name, Transform parent, params Type[] componentTypes)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = parent.gameObject.layer;
        gameObject.transform.SetParent(parent, false);
        foreach (Type type in componentTypes)
        {
            if (gameObject.GetComponent(type) == null) gameObject.AddComponent(type);
        }
        return gameObject;
    }

    private static TMP_Text CreateRuntimeText(
        Transform parent, string name, string value, TMP_FontAsset font,
        float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject gameObject = CreateRuntimeUiObject(name, parent, typeof(TextMeshProUGUI));
        TMP_Text text = gameObject.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void AnchorAtTopRight(
        RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void StretchRuntime(
        RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetButtonColors(
        Button button, Color normal, Color highlighted, Color pressed)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.selectedColor = highlighted;
        colors.pressedColor = pressed;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;
    }

    /// <summary>
    /// 自動綁定詳細頁 UI 參考。
    /// </summary>
    private void AutoBindReferences()
    {
        if (textCompanyTitle == null) textCompanyTitle = FindChildText("TMP_Company_Title");
        if (textStatus == null) textStatus = FindChildText("TMP_Status");
        if (textSalary == null) textSalary = FindChildText("TMP_Salay");
        if (textWorkPosition == null) textWorkPosition = FindChildText("TMP_WorkPosition");
        if (textWorkMode == null) textWorkMode = FindChildText("TMP_WorkMode");
        if (textExperience == null) textExperience = FindChildText("TMP_Experience");
        if (textEducationNeed == null) textEducationNeed = FindChildText("TMP_EducationNeed");
        if (textWorkContent == null) textWorkContent = FindChildText("TMP_WorkContent");
        if (textSkillHead == null) textSkillHead = FindChildText("TMP_Skill_Head");
        if (textSkillTool == null) textSkillTool = FindChildText("TMP_Skill_Tool");
        if (textSkillTech == null) textSkillTech = FindChildText("TMP_Skill_Tech");
        if (textWelfare == null) textWelfare = FindChildText("TMP_Welfare");
        if (textRecruitmentProcess == null) textRecruitmentProcess = FindChildText("TMP_RecruitmentProcess");
        if (textOther == null) textOther = FindChildText("TMP_Other");
        if (textExpireDay == null) textExpireDay = FindChildText("TMP_ExpireDay");

        if (buttonBack == null) buttonBack = FindChildButton("Button_Back");
        if (buttonManualSetExpireDay == null) buttonManualSetExpireDay = FindChildButton("Button_ManualSetExpireDay");
        if (inputDialog == null) inputDialog = GetComponentInChildren<Panel_DetailInputDialog>(true);
        if (inputDialog != null) inputDialog.Initialize();
        if (buttonShowStatusPanel == null) buttonShowStatusPanel = FindChildButton("Button_ShowStatusPanel");
        if (panelStatusBtn == null)
        {
            Transform foundStatusPanel = FindChildTransform("Panel_StatusBtn");
            if (foundStatusPanel != null)
            {
                panelStatusBtn = foundStatusPanel.gameObject;
            }
        }

        if (buttonNotViewed == null) buttonNotViewed = FindChildButton("Button_NotViewed");
        if (buttonInterested == null) buttonInterested = FindChildButton("Button_Interested");
        if (buttonNotApplying == null) buttonNotApplying = FindChildButton("Button_NotApplying");
        if (buttonNotApplying == null) buttonNotApplying = FindChildButton("Button_NotApplied");
        if (buttonApplied == null) buttonApplied = FindChildButton("Button_Applied");
        if (buttonWaitInterview == null) buttonWaitInterview = FindChildButton("Button_WaitInterview");
        if (buttonWithInterview == null) buttonWithInterview = FindChildButton("Button_WithInterview");
        if (buttonWaitingReply == null) buttonWaitingReply = FindChildButton("Button_WaitingReply");
        if (buttonWaitingReply == null) buttonWaitingReply = FindChildButton("Button_WaitInterviewResult");
        if (buttonNoResponse == null) buttonNoResponse = FindChildButton("Button_NoResponse");
        if (buttonOffer == null) buttonOffer = FindChildButton("Button_Offer");
        if (buttonRejected == null) buttonRejected = FindChildButton("Button_Rejected");
        if (buttonArchived == null) buttonArchived = FindChildButton("Button_Archived");
        if (buttonArchivedWaitOtherJobResult == null) buttonArchivedWaitOtherJobResult = FindChildButton("Button_Archived_WaitOtherJobResult");
        if (buttonEditNotes == null) buttonEditNotes = FindChildButton("Button_EditNotes_V02");
        if (buttonEditJobPosting == null) buttonEditJobPosting = FindChildButton("Button_EditJobPosting_V02");
        if (buttonDeleteJobPosting == null) buttonDeleteJobPosting = FindChildButton("Button_DeleteJobPosting_V02");
        if (buttonConfirmDelete == null) buttonConfirmDelete = FindChildButton("Button_ConfirmDeleteJobPosting_V02");
        if (buttonCancelDelete == null) buttonCancelDelete = FindChildButton("Button_CancelDeleteJobPosting_V02");
        if (textDeleteConfirmation == null) textDeleteConfirmation = FindChildText("TMP_DeleteJobPostingConfirmation_V02");
        if (panelDeleteConfirmation == null)
        {
            Transform foundDeletePanel = FindChildTransform("Panel_DeleteJobPostingConfirmation_V02");
            if (foundDeletePanel != null)
            {
                panelDeleteConfirmation = foundDeletePanel.gameObject;
            }
        }
        if (buttonShowEventHistory == null) buttonShowEventHistory = FindChildButton("Button_ShowEventHistory_V02");
        if (buttonCloseEventHistory == null) buttonCloseEventHistory = FindChildButton("Button_CloseEventHistory_V02");
        if (textEventHistory == null) textEventHistory = FindChildText("TMP_EventHistory_V02");
        if (panelEventHistory == null)
        {
            Transform foundEventPanel = FindChildTransform("Panel_EventHistory_V02");
            if (foundEventPanel != null)
            {
                panelEventHistory = foundEventPanel.gameObject;
            }
        }
        if (buttonShowRequirementMatch == null)
            buttonShowRequirementMatch = FindChildButton("Button_ShowRequirementMatch_V027");
        if (buttonCloseRequirementMatch == null)
            buttonCloseRequirementMatch = FindChildButton("Button_CloseRequirementMatch_V027");
        if (textRequirementMatch == null)
            textRequirementMatch = FindChildText("TMP_RequirementMatch_V027");
        if (panelRequirementMatch == null)
        {
            Transform foundMatchPanel = FindChildTransform("Panel_RequirementMatch_V027");
            if (foundMatchPanel != null) panelRequirementMatch = foundMatchPanel.gameObject;
        }
        if (layoutRoot == null) layoutRoot = transform as RectTransform;
        HideInputDialog();
    }

    /// <summary>
    /// 綁定詳細頁按鈕事件。
    /// </summary>
    private void BindButtons()
    {
        BindButton(buttonBack, Hide);
        BindButton(buttonManualSetExpireDay, ShowExpireDayInput);
        if (inputDialog != null)
        {
            BindButton(inputDialog.ConfirmButton, ConfirmExpireDayInput);
            BindButton(inputDialog.CancelButton, CancelInput);
        }
        BindButton(buttonShowStatusPanel, ToggleStatusPanel);
        BindButton(buttonNotViewed, SetStatusNotViewed);
        BindButton(buttonInterested, SetStatusInterested);
        BindButton(buttonNotApplying, SetStatusNotApplying);
        BindButton(buttonApplied, SetStatusApplied);
        BindButton(buttonWaitInterview, SetStatusWaitInterview);
        BindButton(buttonWithInterview, SetStatusWithInterview);
        BindButton(buttonWaitingReply, SetStatusWaitingReply);
        BindButton(buttonNoResponse, MarkNoResponse);
        BindButton(buttonOffer, SetStatusOffer);
        BindButton(buttonRejected, SetStatusRejected);
        BindButton(buttonArchived, SetStatusArchived);
        BindButton(buttonArchivedWaitOtherJobResult, SetStatusArchivedWaitOtherJobResult);
        BindButton(buttonEditJobPosting, EditJobPosting);
        BindButton(buttonDeleteJobPosting, ShowDeleteConfirmation);
        BindButton(buttonConfirmDelete, ConfirmDeleteJobPosting);
        BindButton(buttonCancelDelete, CancelDeleteJobPosting);
        BindButton(buttonShowEventHistory, ToggleEventHistory);
        BindButton(buttonCloseEventHistory, CloseEventHistory);
        BindButton(buttonShowRequirementMatch, ShowRequirementMatch);
        BindButton(buttonCloseRequirementMatch, CloseRequirementMatch);
    }

    /// <summary>
    /// 綁定單一按鈕事件。
    /// </summary>
    /// <param name="button">目標按鈕。</param>
    /// <param name="action">點擊事件。</param>
    private void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    /// <summary>
    /// 依名稱尋找子物件 TMP_Text。
    /// </summary>
    /// <param name="childName">子物件名稱。</param>
    /// <returns>找到的 TMP_Text；找不到回傳 null。</returns>
    private TMP_Text FindChildText(string childName)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text.name == childName)
            {
                return text;
            }
        }

        return null;
    }

    /// <summary>
    /// 依名稱尋找子物件 Button。
    /// </summary>
    /// <param name="childName">子物件名稱。</param>
    /// <returns>找到的 Button；找不到回傳 null。</returns>
    private Button FindChildButton(string childName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.name == childName)
            {
                return button;
            }
        }

        return null;
    }

    /// <summary>
    /// 依名稱尋找子物件 Transform。
    /// </summary>
    /// <param name="childName">子物件名稱。</param>
    /// <returns>找到的 Transform；找不到回傳 null。</returns>
    private Transform FindChildTransform(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
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

    private void SetButtonLabel(Button button, string value)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = value;
        }
    }

    private void ShowDateInput(
        string title,
        string description,
        string initialValue,
        string confirmLabel)
    {
        if (inputDialog == null)
        {
            Debug.LogError("Panel_JobDetail requires Panel_DetailInputDialog for date input.", this);
            return;
        }

        inputDialog.ShowDate(title, description, initialValue, confirmLabel);
    }

    /// <summary>
    /// 開啟多行文字輸入模式。
    /// </summary>
    private void ShowTextInput(
        string title,
        string description,
        string initialValue,
        string confirmLabel)
    {
        if (inputDialog == null)
        {
            Debug.LogError("Panel_JobDetail requires Panel_DetailInputDialog for text input.", this);
            return;
        }

        inputDialog.ShowText(title, description, initialValue, confirmLabel);
    }

    public void ShowNotesInput()
    {
        if (currentData == null)
        {
            return;
        }

        CloseStatusPanel();
        inputMode = DetailInputMode.Notes;
        ShowTextInput(
            "編輯應徵備註",
            "可輸入多行備註；將內容清空後儲存即可移除既有備註。",
            currentTracking != null ? currentTracking.notes : string.Empty,
            "儲存備註");
    }

    /// <summary>
    /// 將 UI 的短中文原因轉成穩定 enum；未命中固定選項時視為 Other 並保留原文。
    /// </summary>
    private void ParseCandidateCloseReason(
        string input,
        out CandidateCloseReason reason,
        out string note)
    {
        note = null;
        switch (input.Trim())
        {
            case "薪資": reason = CandidateCloseReason.SalaryTooLow; return;
            case "博弈": reason = CandidateCloseReason.GamblingIndustry; return;
            case "通勤": reason = CandidateCloseReason.Commute; return;
            case "工時": reason = CandidateCloseReason.WorkSchedule; return;
            case "週末": reason = CandidateCloseReason.WeekendDuty; return;
            case "職務": reason = CandidateCloseReason.RoleMismatch; return;
            case "技術": reason = CandidateCloseReason.TechMismatch; return;
            case "公司": reason = CandidateCloseReason.CompanyConcern; return;
            case "更好機會": reason = CandidateCloseReason.BetterOpportunity; return;
            case "無回覆": reason = CandidateCloseReason.NoResponse; return;
            default:
                reason = CandidateCloseReason.Other;
                note = input.Trim();
                return;
        }
    }

    /// <summary>
    /// 關閉共用輸入視窗。
    /// </summary>
    private void HideInputDialog()
    {
        if (inputDialog != null) inputDialog.Hide();
    }

    /// <summary>
    /// 依目前 tracking 刷新到期日顯示文字。
    /// </summary>
    private void RefreshExpireDayText()
    {
        if (textExpireDay == null)
        {
            return;
        }

        if (allJobPage == null || currentTracking == null)
        {
            textExpireDay.text = "下次追蹤日: -";
            return;
        }

        DateTimeOffset expireAt;
        if (allJobPage.TryGetTrackingExpireAt(currentTracking, out expireAt))
        {
            textExpireDay.text = "下次追蹤日: " + expireAt.ToString("yyyy/MM/dd");
            return;
        }

        textExpireDay.text = "下次追蹤日: -";
    }

    /// <summary>
    /// 取得目前人工到期日，供輸入框預填。
    /// </summary>
    /// <returns>yyyy.MM.dd 格式文字，沒有人工到期日時回傳空字串。</returns>
    private string GetCurrentManualExpireDayForInput()
    {
        if (currentTracking == null || string.IsNullOrEmpty(currentTracking.manual_expire_at))
        {
            return string.Empty;
        }

        DateTimeOffset expireAt;
        if (!DateTimeOffset.TryParse(currentTracking.manual_expire_at, out expireAt))
        {
            return string.Empty;
        }

        return expireAt.ToString("yyyy.MM.dd");
    }

    /// <summary>
    /// 加入「標籤: 值」格式文字。空值不加入。
    /// </summary>
    /// <param name="builder">文字組合器。</param>
    /// <param name="label">欄位標籤。</param>
    /// <param name="value">欄位值。</param>
    private void AppendLine(StringBuilder builder, string label, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        builder.Append(label);
        builder.Append(": ");
        builder.AppendLine(value);
    }

    /// <summary>
    /// 建立「標籤: 值」格式文字。空值會顯示破折號。
    /// </summary>
    /// <param name="label">欄位標籤。</param>
    /// <param name="value">欄位值。</param>
    /// <returns>格式化後文字。</returns>
    private string BuildLabelLine(string label, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return label + ": -";
        }

        return label + ": " + value;
    }

    /// <summary>
    /// 加入區塊標題。
    /// </summary>
    /// <param name="builder">文字組合器。</param>
    /// <param name="title">標題文字。</param>
    private void AppendHeader(StringBuilder builder, string title)
    {
        if (!string.IsNullOrEmpty(title))
        {
            builder.AppendLine(title);
        }
    }

    /// <summary>
    /// 加入條列式清單。
    /// </summary>
    /// <param name="builder">文字組合器。</param>
    /// <param name="title">清單標題。</param>
    /// <param name="values">清單內容。</param>
    private void AppendList(StringBuilder builder, string title, List<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return;
        }

        builder.AppendLine(title + ":");
        foreach (string value in values)
        {
            if (!string.IsNullOrEmpty(value))
            {
                builder.Append("- ");
                builder.AppendLine(value);
            }
        }
    }

    /// <summary>
    /// 將字串列表用頓號串接。
    /// </summary>
    /// <param name="values">字串列表。</param>
    /// <returns>串接後文字。</returns>
    private string JoinList(List<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("、", values);
    }

    /// <summary>
    /// 用斜線組合兩段文字。
    /// </summary>
    /// <param name="left">左側文字。</param>
    /// <param name="right">右側文字。</param>
    /// <returns>組合後文字。</returns>
    private string CombineWithSlash(string left, string right)
    {
        if (string.IsNullOrEmpty(left))
        {
            return right;
        }

        if (string.IsNullOrEmpty(right))
        {
            return left;
        }

        return left + " / " + right;
    }

    /// <summary>
    /// 將工作模式代碼轉成中文。
    /// </summary>
    /// <param name="workMode">工作模式代碼。</param>
    /// <returns>中文工作模式。</returns>
    private string FormatWorkMode(string workMode)
    {
        switch (workMode)
        {
            case "onsite":
                return "現場";
            case "remote":
                return "遠端";
            case "hybrid":
                return "混合";
            case "unknown":
                return "未知";
            default:
                return string.IsNullOrEmpty(workMode) ? string.Empty : workMode;
        }
    }

    private void SetStatusTextColor(bool isExpired)
    {
        if (textStatus != null)
        {
            textStatus.color = isExpired
                ? (theme != null ? theme.Danger : expiredStatusTextColor)
                : (theme != null ? theme.Primary : normalStatusTextColor);
        }
    }

    /// <summary>
    /// 將新版共用 Theme 套用到舊版職缺詳情文字，避免淺色內容面板仍使用白字。
    /// </summary>
    private void ApplyTheme()
    {
        if (theme == null)
        {
            return;
        }

        StyleDetailText(textCompanyTitle, theme.TextPrimary, FontStyles.Bold);
        StyleDetailText(textStatus, theme.Primary, FontStyles.Bold);
        StyleDetailText(textSalary, theme.TextPrimary, FontStyles.Normal);
        StyleDetailText(textWorkPosition, theme.TextPrimary, FontStyles.Normal);
        StyleDetailText(textWorkMode, theme.TextPrimary, FontStyles.Normal);
        StyleDetailText(textExperience, theme.TextPrimary, FontStyles.Normal);
        StyleDetailText(textEducationNeed, theme.TextPrimary, FontStyles.Normal);
        StyleDetailText(textWorkContent, theme.TextPrimary, FontStyles.Normal);
        StyleDetailText(textSkillHead, theme.TextPrimary, FontStyles.Bold);
        StyleDetailText(textSkillTool, theme.TextPrimary, FontStyles.Normal);
        StyleDetailText(textSkillTech, theme.TextPrimary, FontStyles.Normal);
        StyleDetailText(textWelfare, theme.TextPrimary, FontStyles.Normal);
        StyleDetailText(textRecruitmentProcess, theme.TextPrimary, FontStyles.Normal);
        StyleDetailText(textOther, theme.TextPrimary, FontStyles.Normal);
        StyleDetailText(textExpireDay, theme.TextSecondary, FontStyles.Normal);
        StyleDetailText(textEventHistory, theme.TextPrimary, FontStyles.Normal);
        StyleDetailText(textDeleteConfirmation, theme.TextPrimary, FontStyles.Normal);
        StyleDetailText(textRequirementMatch, theme.TextPrimary, FontStyles.Normal);
    }

    private void StyleDetailText(TMP_Text text, Color color, FontStyles style)
    {
        if (text == null)
        {
            return;
        }

        if (theme.BodyFont != null)
        {
            text.font = theme.BodyFont;
        }

        text.color = color;
        text.fontStyle = style;
    }

    /// <summary>
    /// 將狀態代碼轉成中文。
    /// </summary>
    /// <param name="status">狀態代碼。</param>
    /// <returns>中文狀態。</returns>
    private string FormatStatus(string status)
    {
        return FormatStatus(status, false);
    }

    /// <summary>
    /// 將狀態代碼轉成中文，並可附加逾期標記。
    /// </summary>
    /// <param name="status">狀態代碼。</param>
    /// <param name="isExpired">是否逾期。</param>
    /// <returns>中文狀態。</returns>
    private string FormatStatus(string status, bool isExpired)
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
            case "viewed":
                text = "公司已讀";
                break;
            case "contacted":
                text = "公司已聯絡";
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
            case "unknown":
                text = "狀態待確認";
                break;
            default:
                text = string.IsNullOrEmpty(status) ? "未檢視" : status;
                break;
        }

        return isExpired ? text + "（已逾期）" : text;
    }

    /// <summary>
    /// 取得目前 V0.2 Application 投影出的狀態。
    /// </summary>
    /// <returns>目前狀態代碼。</returns>
    private string GetCurrentStatus()
    {
        if (currentTracking != null && !string.IsNullOrEmpty(currentTracking.status))
        {
            return currentTracking.status;
        }

        return string.Empty;
    }

    /// <summary>
    /// 判斷目前 tracking 是否逾期。
    /// </summary>
    /// <returns>逾期時回傳 true。</returns>
    private bool IsCurrentTrackingExpired()
    {
        return allJobPage != null && currentTracking != null && allJobPage.IsTrackingExpired(currentTracking);
    }
}
