using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 職缺詳細頁控制器。負責顯示完整職缺內容、切換狀態、刷新狀態按鈕與返回總攬頁。
/// </summary>
public class Panel_JobDetail : MonoBehaviour
{
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
    [SerializeField] private Color normalStatusTextColor = Color.white;
    [Tooltip("逾期狀態文字顏色。")]
    [SerializeField] private Color expiredStatusTextColor = Color.red;

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
    [Tooltip("切換為錄取。")]
    [SerializeField] private Button buttonOffer;
    [Tooltip("切換為未錄取。")]
    [SerializeField] private Button buttonRejected;
    [Tooltip("切換為封存。")]
    [SerializeField] private Button buttonArchived;
    [Tooltip("切換為已封存，等待其他面試結果。")]
    [SerializeField] private Button buttonArchivedWaitOtherJobResult;

    private JobDetailData currentData;
    private JobTrackingData currentTracking;
    private AllJobPage allJobPage;

    private void Awake()
    {
        AutoBindReferences();
        BindButtons();
    }

    /// <summary>
    /// 顯示詳細頁，只使用職缺原始資料。
    /// </summary>
    /// <param name="data">職缺詳細資料。</param>
    public void Show(JobDetailData data)
    {
        Show(data, null);
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
        Refresh(data);
    }

    /// <summary>
    /// 從 JSON 字串載入職缺詳細資料。
    /// </summary>
    /// <param name="json">職缺詳細 JSON 字串。</param>
    public void LoadFromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Clear();
            return;
        }

        Show(JsonUtility.FromJson<JobDetailData>(json));
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
        string status = FormatStatus(GetCurrentStatus(data), isExpired);
        string benefits = BuildBenefitsText(data.benefits);

        SetText(textCompanyTitle, title);
        SetText(textStatus, "狀態: " + status);
        SetStatusTextColor(isExpired);
        SetText(textSalary, BuildLabelLine("薪資", data.compensation != null ? data.compensation.raw_text : string.Empty));
        SetText(textWorkPosition, BuildLabelLine("工作地點", data.location != null ? data.location.raw_text : string.Empty));
        SetText(textWorkMode, BuildLabelLine("工作模式", data.location != null ? FormatWorkMode(data.location.work_mode) : string.Empty));
        SetText(textExperience, BuildLabelLine("經驗", data.requirements != null ? data.requirements.experience : string.Empty));
        SetText(textEducationNeed, BuildLabelLine("學歷需求", data.requirements != null ? data.requirements.education : string.Empty));
        SetText(textWorkContent, BuildListSection("工作內容", data.responsibilities));
        SetText(textSkillHead, "技能需求");
        SetText(textSkillTool, BuildLabelLine("技能需求 工具/技術", data.requirements != null ? JoinList(data.requirements.tools) : string.Empty));
        SetText(textSkillTech, BuildLabelLine("技能需求 技能", data.requirements != null ? JoinList(data.requirements.skills) : string.Empty));
        SetText(textWelfare, benefits);
        SetText(textRecruitmentProcess, BuildListSection("招募流程", data.recruitment_process));
        SetText(textOther, BuildOtherText(data));

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
        ForceBuildLayout();
    }

    /// <summary>
    /// 關閉詳細頁並返回總攬頁。
    /// </summary>
    public void Hide()
    {
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
    /// 指定總攬頁控制器，用於返回與更新 tracking。
    /// </summary>
    /// <param name="page">總攬頁控制器。</param>
    public void SetAllJobPage(AllJobPage page)
    {
        allJobPage = page;
    }

    /// <summary>
    /// 切換為未檢視。
    /// </summary>
    public void SetStatusNotViewed()
    {
        ChangeStatus("not_viewed");
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
        ChangeStatus("not_applying");
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
        ChangeStatus("interview_scheduled");
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
        ChangeStatus("archived");
    }

    /// <summary>
    /// 切換為已封存，等待其他面試結果。
    /// </summary>
    public void SetStatusArchivedWaitOtherJobResult()
    {
        ChangeStatus("archived_wait_other_job_result");
    }

    /// <summary>
    /// 開關狀態按鈕面板。
    /// </summary>
    public void ToggleStatusPanel()
    {
        if (panelStatusBtn == null)
        {
            return;
        }

        panelStatusBtn.SetActive(!panelStatusBtn.activeSelf);
        RefreshStatusButtonColors();
    }

    /// <summary>
    /// 修改目前職缺狀態並寫回 tracking。
    /// </summary>
    /// <param name="status">新的狀態代碼。</param>
    private void ChangeStatus(string status)
    {
        if (currentData == null || allJobPage == null)
        {
            return;
        }

        currentTracking = allJobPage.UpdateJobTrackingStatus(currentData.id, status);
        Refresh(currentData);
    }

    /// <summary>
    /// 依目前狀態刷新狀態按鈕顏色。
    /// </summary>
    private void RefreshStatusButtonColors()
    {
        string currentStatus = GetCurrentStatus(currentData);

        SetStatusButtonColor(buttonNotViewed, currentStatus == "not_viewed");
        SetStatusButtonColor(buttonInterested, currentStatus == "interested");
        SetStatusButtonColor(buttonNotApplying, currentStatus == "not_applying");
        SetStatusButtonColor(buttonApplied, currentStatus == "applied");
        SetStatusButtonColor(buttonWaitInterview, currentStatus == "interview_scheduled");
        SetStatusButtonColor(buttonWithInterview, currentStatus == "interviewing");
        SetStatusButtonColor(buttonWaitingReply, currentStatus == "waiting_reply");
        SetStatusButtonColor(buttonOffer, currentStatus == "offer");
        SetStatusButtonColor(buttonRejected, currentStatus == "rejected");
        SetStatusButtonColor(buttonArchived, currentStatus == "archived");
        SetStatusButtonColor(buttonArchivedWaitOtherJobResult, currentStatus == "archived_wait_other_job_result");
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
        AppendLine(builder, "工作性質", data.work_conditions != null ? data.work_conditions.employment_type : string.Empty);
        AppendLine(builder, "上班時段", data.work_conditions != null ? data.work_conditions.working_hours : string.Empty);
        AppendLine(builder, "需求人數", data.job != null ? data.job.openings : string.Empty);
        AppendLine(builder, "目前狀態", FormatStatus(GetCurrentStatus(data)));
        AppendLine(builder, "最後操作日期", currentTracking != null ? currentTracking.last_action_at : string.Empty);
        AppendLine(builder, "人工過期日期", currentTracking != null ? currentTracking.manual_expire_at : string.Empty);
        AppendLine(builder, "是否逾期", IsCurrentTrackingExpired() ? "是" : "否");
        AppendLine(builder, "我的最愛", currentTracking != null && currentTracking.favorite ? "是" : "否");
        AppendLine(builder, "適配度", currentTracking != null && currentTracking.fit_score >= 0 ? currentTracking.fit_score.ToString() : "尚未評分");

        return builder.ToString().TrimEnd();
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

        if (buttonBack == null) buttonBack = FindChildButton("Button_Back");
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
        if (buttonOffer == null) buttonOffer = FindChildButton("Button_Offer");
        if (buttonRejected == null) buttonRejected = FindChildButton("Button_Rejected");
        if (buttonArchived == null) buttonArchived = FindChildButton("Button_Archived");
        if (buttonArchivedWaitOtherJobResult == null) buttonArchivedWaitOtherJobResult = FindChildButton("Button_Archived_WaitOtherJobResult");
        if (layoutRoot == null) layoutRoot = transform as RectTransform;
    }

    /// <summary>
    /// 綁定詳細頁按鈕事件。
    /// </summary>
    private void BindButtons()
    {
        BindButton(buttonBack, Hide);
        BindButton(buttonShowStatusPanel, ToggleStatusPanel);
        BindButton(buttonNotViewed, SetStatusNotViewed);
        BindButton(buttonInterested, SetStatusInterested);
        BindButton(buttonNotApplying, SetStatusNotApplying);
        BindButton(buttonApplied, SetStatusApplied);
        BindButton(buttonWaitInterview, SetStatusWaitInterview);
        BindButton(buttonWithInterview, SetStatusWithInterview);
        BindButton(buttonWaitingReply, SetStatusWaitingReply);
        BindButton(buttonOffer, SetStatusOffer);
        BindButton(buttonRejected, SetStatusRejected);
        BindButton(buttonArchived, SetStatusArchived);
        BindButton(buttonArchivedWaitOtherJobResult, SetStatusArchivedWaitOtherJobResult);
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
            textStatus.color = isExpired ? expiredStatusTextColor : normalStatusTextColor;
        }
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

    /// <summary>
    /// 取得目前顯示用狀態，優先使用 tracking 狀態。
    /// </summary>
    /// <param name="data">職缺詳細資料。</param>
    /// <returns>目前狀態代碼。</returns>
    private string GetCurrentStatus(JobDetailData data)
    {
        if (currentTracking != null && !string.IsNullOrEmpty(currentTracking.status))
        {
            return currentTracking.status;
        }

        return data != null && data.tracking != null ? data.tracking.status : string.Empty;
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
