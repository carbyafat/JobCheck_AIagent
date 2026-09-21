using System;
using System.Collections.Generic;
using System.IO;
using JobCheck.Persistence;
using ApplicationEventType = JobCheck.Domain.ApplicationEventType;
using CandidateCloseReason = JobCheck.Domain.CandidateCloseReason;
using EventActor = JobCheck.Domain.EventActor;
using JobCheckDataSet = JobCheck.Domain.JobCheckDataSet;
using Company = JobCheck.Domain.Company;
using JobPosting = JobCheck.Domain.JobPosting;
using DomainApplication = JobCheck.Domain.Application;
using DomainApplicationEvent = JobCheck.Domain.ApplicationEvent;
using CareerProfile = JobCheck.Domain.CareerProfile;
using RequirementMatchEngine = JobCheck.Domain.RequirementMatchEngine;
using RequirementMatchResult = JobCheck.Domain.RequirementMatchResult;
using RequirementScore = JobCheck.Domain.RequirementScore;
using RequirementScoreCalculator = JobCheck.Domain.RequirementScoreCalculator;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 職缺總攬頁控制器：載入 V0.2 資料、處理分頁、排序與開啟詳細頁。
/// </summary>
public class AllJobPage : MonoBehaviour
{
    private const string DataProfilePreferenceKey = "JobCheck.DataProfile";

    [Header("Data")]
    [Tooltip("版本控制內的正式 Demo 資料根目錄；不可寫入個人真實資料。")]
    [SerializeField] private string demoDataRootPath = "../data";
    [Tooltip("只保存在本機、不納入 Git 的個人資料根目錄。")]
    [SerializeField] private string personalDataRootPath = "../personal_data";

    [Header("UI")]
    [Tooltip("載入全部職缺資料的按鈕。")]
    [SerializeField] private Button buttonLoad;
    [Tooltip("下一頁按鈕，沒有下一頁時會隱藏。")]
    [SerializeField] private Button buttonNext;
    [Tooltip("上一頁按鈕，沒有上一頁時會隱藏。")]
    [SerializeField] private Button buttonLast;
    [Tooltip("篩選按鈕，目前只保留入口。")]
    [SerializeField] private Button buttonShowFilter;
    [Tooltip("開啟 V0.2 新增職缺表單的按鈕。")]
    [SerializeField] private Button buttonAddJobPosting;
    [Tooltip("篩選面板。")]
    [SerializeField] private FilterPanel filterPanel;
    [Tooltip("職缺列表列物件的父節點。")]
    [SerializeField] private Transform jobPanelRoot;
    [Tooltip("可重複使用的職缺列 UI。")]
    [SerializeField] private List<Panel_SingleJob> jobPanels = new List<Panel_SingleJob>();
    [Tooltip("職缺詳細頁。")]
    [SerializeField] private Panel_JobDetail jobDetailPanel;
    [Tooltip("V0.2 新增職缺表單；表單物件實際保存在 AllJobPage Prefab。")]
    [SerializeField] private Panel_JobPostingCreate jobPostingCreatePanel;
    [Tooltip("切換 Demo／個人資料區的按鈕。")]
    [SerializeField] private Button buttonSwitchDataProfile;
    [Tooltip("顯示目前正在讀寫哪一個資料區。")]
    [SerializeField] private TMP_Text textDataProfile;
    [Tooltip("開啟 V0.2.2 應徵分析頁。")]
    [SerializeField] private Button buttonShowAnalytics;
    [Tooltip("唯讀的應徵分析頁 Prefab 實例。")]
    [SerializeField] private Panel_Analytics analyticsPanel;
    [Tooltip("開啟個人資料搬運面板。")]
    [SerializeField] private Button buttonPortableTransfer;
    [SerializeField] private Panel_PortableTransfer portableTransferPanel;
    [Tooltip("開啟個人資料垃圾桶管理。")]
    [SerializeField] private Button buttonTrashManagement;
    [SerializeField] private Panel_TrashManagement trashManagementPanel;

    private readonly List<JobSummaryData> loadedJobs = new List<JobSummaryData>();
    private readonly List<JobSummaryData> displayJobs = new List<JobSummaryData>();
    private JobFilterCondition currentFilter = new JobFilterCondition();
    private int currentPage;
    private JobCheckDataProfile currentDataProfile;

    public JobCheckDataProfile CurrentDataProfile => currentDataProfile;
    public string CurrentDataProfileLabel =>
        currentDataProfile == JobCheckDataProfile.Personal ? "個人" : "Demo";
    public string CurrentDataRoot => ResolveProjectRelativePath(ActiveDataRootPath);
    public string PersonalDataRoot => ResolveProjectRelativePath(personalDataRootPath);

    private string ActiveDataRootPath
    {
        get
        {
            return currentDataProfile == JobCheckDataProfile.Personal
                ? personalDataRootPath
                : demoDataRootPath;
        }
    }

    private int PageSize
    {
        get { return Mathf.Max(1, jobPanels.Count); }
    }

    private int TotalPageCount
    {
        get
        {
            if (displayJobs.Count == 0)
            {
                return 1;
            }

            return Mathf.CeilToInt((float)displayJobs.Count / PageSize);
        }
    }

    private void Awake()
    {
        LoadSelectedDataProfile();
        AutoBindReferences();
        BindButtons();
        RefreshDataProfileUi();
        RefreshPage();
    }

    /// <summary>
    /// 載入 V0.2 data，並依逾期/最愛規則排序後刷新畫面。
    /// </summary>
    public void Load()
    {
        loadedJobs.Clear();
        currentPage = 0;

        LoadFromV02Data(ResolveProjectRelativePath(ActiveDataRootPath));

        ApplyFilter(currentFilter);
    }

    /// <summary>
    /// 切換到下一頁。
    /// </summary>
    public void Next()
    {
        if (!HasNextPage())
        {
            return;
        }

        currentPage++;
        RefreshPage();
    }

    /// <summary>
    /// 切換到上一頁。
    /// </summary>
    public void Last()
    {
        if (!HasLastPage())
        {
            return;
        }

        currentPage--;
        RefreshPage();
    }

    /// <summary>
    /// 開啟既有的篩選面板；條件的讀取與套用由 FilterPanel 負責。
    /// </summary>
    public void ShowFilter()
    {
        if (filterPanel == null)
        {
            filterPanel = FindObjectOfType<FilterPanel>(true);
        }

        if (filterPanel == null)
        {
            Debug.LogWarning("FilterPanel not found in scene.");
            return;
        }

        filterPanel.SetOwner(this);
        filterPanel.RefreshUI(currentFilter);
        filterPanel.gameObject.SetActive(true);
    }

    /// <summary>
    /// 開啟 V0.2 新增職缺表單。
    /// </summary>
    public void ShowAddJobPosting()
    {
        if (jobPostingCreatePanel == null)
        {
            jobPostingCreatePanel = GetComponentInChildren<Panel_JobPostingCreate>(true);
        }

        if (jobPostingCreatePanel == null)
        {
            Debug.LogError("Panel_JobPostingCreate not found in AllJobPage prefab.");
            return;
        }

        jobPostingCreatePanel.Show(this);
    }

    /// <summary>
    /// 接收新增職缺表單並交給 V0.2 寫入服務；成功後重新載入列表。
    /// </summary>
    public PersistenceStorageResult<JobPostingWriteSummary> CreateV02JobPosting(
        JobPostingCreateRequest request)
    {
        PersistenceStorageResult<JobPostingWriteSummary> result =
            JobPostingCommandService.Create(
                ResolveProjectRelativePath(ActiveDataRootPath),
                request);
        if (result.IsSuccess)
        {
            Load();
        }
        else
        {
            foreach (PersistenceStorageIssue issue in result.Issues)
            {
                Debug.LogError(
                    "V0.2 job create failed [" + issue.Error + "] "
                    + issue.FieldPath + " " + issue.Message);
            }
        }

        return result;
    }

    /// <summary>
    /// 從詳細頁開啟既有職缺編輯表單。編輯完成或取消後回到重新載入的總覽頁。
    /// </summary>
    public void ShowEditJobPosting(JobDetailData data)
    {
        if (data == null)
        {
            Debug.LogWarning("缺少有效的 V0.2 職缺資料。");
            return;
        }

        if (jobPostingCreatePanel == null)
        {
            jobPostingCreatePanel = GetComponentInChildren<Panel_JobPostingCreate>(true);
        }

        if (jobPostingCreatePanel == null)
        {
            Debug.LogError("Panel_JobPostingCreate not found in AllJobPage prefab.");
            return;
        }

        gameObject.SetActive(true);
        jobPostingCreatePanel.ShowForEdit(this, data);
    }

    /// <summary>
    /// 接收編輯表單並更新既有 V0.2 職缺；成功後重新載入列表。
    /// </summary>
    public PersistenceStorageResult<JobPostingWriteSummary> UpdateV02JobPosting(
        JobPostingEditRequest request)
    {
        PersistenceStorageResult<JobPostingWriteSummary> result =
            JobPostingCommandService.Update(
                ResolveProjectRelativePath(ActiveDataRootPath),
                request);
        if (result.IsSuccess)
        {
            Load();
        }
        else
        {
            foreach (PersistenceStorageIssue issue in result.Issues)
            {
                Debug.LogWarning(
                    "V0.2 job update rejected [" + issue.Error + "] "
                    + issue.FieldPath + " " + issue.Message);
            }
        }

        return result;
    }

    /// <summary>
    /// 個人資料區才允許刪除；Demo 是版本控制內的驗收資料，不可由執行中的 UI 改動。
    /// </summary>
    public bool CanDeleteJobPostings => currentDataProfile == JobCheckDataProfile.Personal;

    /// <summary>
    /// 將職缺及所有相關 Application（包含內嵌事件）移入個人資料回收區。
    /// </summary>
    public PersistenceStorageResult<JobPostingTrashSummary> DeleteV02JobPosting(string jobPostingId)
    {
        if (!CanDeleteJobPostings)
        {
            var blocked = new PersistenceStorageResult<JobPostingTrashSummary>(
                null,
                new[]
                {
                    new PersistenceStorageIssue(
                        PersistenceStorageError.EntityValidationFailed,
                        jobPostingId,
                        "data_profile",
                        "Demo 資料不可刪除；請切換至個人資料區。")
                });
            LogDeleteIssues(blocked.Issues);
            return blocked;
        }

        PersistenceStorageResult<JobPostingTrashSummary> result =
            JobPostingTrashService.MoveToTrash(
                ResolveProjectRelativePath(ActiveDataRootPath),
                jobPostingId);
        if (result.IsSuccess)
        {
            Load();
        }
        else
        {
            LogDeleteIssues(result.Issues);
        }

        return result;
    }

    private static void LogDeleteIssues(IEnumerable<PersistenceStorageIssue> issues)
    {
        foreach (PersistenceStorageIssue issue in issues)
        {
            Debug.LogError(
                "V0.2 job delete failed [" + issue.Error + "] "
                + issue.FieldPath + " " + issue.Message);
        }
    }

    /// <summary>
    /// 套用篩選條件並刷新列表。
    /// </summary>
    /// <param name="condition">篩選條件。</param>
    public void ApplyFilter(JobFilterCondition condition)
    {
        currentFilter = condition != null ? condition.Clone() : new JobFilterCondition();
        displayJobs.Clear();

        foreach (JobSummaryData job in loadedJobs)
        {
            if (PassesFilter(job, currentFilter))
            {
                displayJobs.Add(job);
            }
        }

        SortJobsForDisplay();
        currentPage = 0;
        RefreshPage();
    }

    /// <summary>
    /// 清除篩選條件並顯示全部職缺。
    /// </summary>
    public void ClearFilter()
    {
        ApplyFilter(new JobFilterCondition());
    }

    /// <summary>
    /// 開啟某筆職缺的詳細頁。
    /// </summary>
    /// <param name="job">列表上選到的職缺摘要。</param>
    public void OpenJobDetail(JobSummaryData job)
    {
        if (job == null)
        {
            return;
        }

        if (jobDetailPanel == null)
        {
            jobDetailPanel = FindObjectOfType<Panel_JobDetail>(true);
        }

        if (jobDetailPanel == null)
        {
            Debug.LogWarning("Panel_JobDetail not found in scene.");
            return;
        }

        if (job.v02Detail == null)
        {
            Debug.LogWarning("V0.2 job detail is unavailable: " + job.id);
            return;
        }

        jobDetailPanel.SetAllJobPage(this);
        jobDetailPanel.SetReadOnly(false);
        jobDetailPanel.Show(job.v02Detail, job.v02Tracking);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 顯示總攬頁並刷新目前頁面。
    /// </summary>
    public void ShowAllJobPage()
    {
        gameObject.SetActive(true);
        RefreshPage();
    }

    /// <summary>
    /// 以 V0.2 Application Event 更新人工指定的下次追蹤時間。
    /// </summary>
    /// <param name="jobId">職缺 ID。</param>
    /// <param name="manualExpireAt">人工指定到期日，空字串代表清除。</param>
    /// <returns>更新後的 tracking 資料。</returns>
    public JobTrackingData UpdateJobTrackingManualExpireAt(string jobId, string manualExpireAt)
    {
        DateTimeOffset? followUpAt = null;
        DateTimeOffset parsed = default;
        if (!string.IsNullOrWhiteSpace(manualExpireAt)
            && !DateTimeOffset.TryParse(manualExpireAt, out parsed))
        {
            Debug.LogError("V0.2 下次追蹤時間格式錯誤：" + manualExpireAt);
            return FindLoadedV02Tracking(jobId);
        }

        if (!string.IsNullOrWhiteSpace(manualExpireAt))
        {
            followUpAt = parsed;
        }

        PersistenceStorageResult<ApplicationWriteSummary> result =
            ApplicationCommandService.SetManualFollowUp(
                ResolveProjectRelativePath(ActiveDataRootPath),
                jobId,
                followUpAt);
        return FinishV02Write(jobId, result);
    }

    /// <summary>
    /// 判斷 tracking 是否逾期。逾期只影響排序與顯示，不會改變狀態。
    /// </summary>
    /// <param name="tracking">使用者操作追蹤資料。</param>
    /// <returns>已逾期時回傳 true。</returns>
    public bool IsTrackingExpired(JobTrackingData tracking)
    {
        if (tracking == null)
        {
            return false;
        }

        DateTimeOffset expireAt;
        if (!string.IsNullOrEmpty(tracking.manual_expire_at))
        {
            return DateTimeOffset.TryParse(tracking.manual_expire_at, out expireAt) && DateTimeOffset.Now > expireAt;
        }

        int days = GetDefaultExpireDays(tracking.status);
        if (days <= 0 || string.IsNullOrEmpty(tracking.last_action_at))
        {
            return false;
        }

        DateTimeOffset lastActionAt;
        if (!DateTimeOffset.TryParse(tracking.last_action_at, out lastActionAt))
        {
            return false;
        }

        return DateTimeOffset.Now > lastActionAt.AddDays(days);
    }

    /// <summary>
    /// 嘗試取得 tracking 的實際到期日。人工到期日優先，否則使用預設天數推算。
    /// </summary>
    /// <param name="tracking">使用者操作追蹤資料。</param>
    /// <param name="expireAt">計算出的到期日。</param>
    /// <returns>能取得到期日時回傳 true。</returns>
    public bool TryGetTrackingExpireAt(JobTrackingData tracking, out DateTimeOffset expireAt)
    {
        expireAt = default;

        if (tracking == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(tracking.manual_expire_at))
        {
            return DateTimeOffset.TryParse(tracking.manual_expire_at, out expireAt);
        }

        int days = GetDefaultExpireDays(tracking.status);
        if (days <= 0 || string.IsNullOrEmpty(tracking.last_action_at))
        {
            return false;
        }

        DateTimeOffset lastActionAt;
        if (!DateTimeOffset.TryParse(tracking.last_action_at, out lastActionAt))
        {
            return false;
        }

        expireAt = lastActionAt.AddDays(days);
        return true;
    }

    /// <summary>
    /// 依目前頁碼把資料填入可見的 Panel_SingleJob。
    /// </summary>
    private void RefreshPage()
    {
        int startIndex = currentPage * PageSize;

        for (int i = 0; i < jobPanels.Count; i++)
        {
            int jobIndex = startIndex + i;
            Panel_SingleJob panel = jobPanels[i];

            if (panel == null)
            {
                continue;
            }

            if (jobIndex < displayJobs.Count)
            {
                panel.gameObject.SetActive(true);
                panel.SetOwner(this);
                panel.SetData(displayJobs[jobIndex]);
            }
            else
            {
                panel.Clear();
                panel.gameObject.SetActive(false);
            }
        }

        RefreshPageButtons();
    }

    /// <summary>
    /// 根據頁碼更新上一頁/下一頁按鈕顯示。
    /// </summary>
    private void RefreshPageButtons()
    {
        if (buttonNext != null)
        {
            buttonNext.gameObject.SetActive(HasNextPage());
        }

        if (buttonLast != null)
        {
            buttonLast.gameObject.SetActive(HasLastPage());
        }
    }

    /// <summary>
    /// 是否存在下一頁。
    /// </summary>
    private bool HasNextPage()
    {
        return displayJobs.Count > PageSize && currentPage < TotalPageCount - 1;
    }

    /// <summary>
    /// 是否存在上一頁。
    /// </summary>
    private bool HasLastPage()
    {
        return displayJobs.Count > PageSize && currentPage > 0;
    }

    /// <summary>
    /// 透過 V0.2 Repository 與 Query 載入職缺，並轉成既有 UI 暫時可顯示的資料形狀。
    /// 此流程不會讀取或建立 V0.1 tracking 檔。
    /// </summary>
    /// <param name="dataRoot">V0.2 data 根目錄完整路徑。</param>
    private void LoadFromV02Data(string dataRoot)
    {
        PersistenceStorageResult<JobPostingReadOnlyList> result =
            JobPostingReadOnlyQuery.Load(dataRoot);
        if (!result.IsSuccess)
        {
            foreach (PersistenceStorageIssue issue in result.Issues)
            {
                Debug.LogError(
                    "V0.2 data load failed [" + issue.Error + "] "
                    + issue.FilePath + " " + issue.FieldPath + " " + issue.Message);
            }

            return;
        }

        CareerProfile profile = null;
        PersistenceStorageResult<CareerProfile> profileResult =
            CareerProfileRepository.Load(PersonalDataRoot);
        if (profileResult.IsSuccess)
        {
            profile = profileResult.Value;
        }
        else
        {
            Debug.LogWarning("履歷載入失敗；本次職缺列表不計算規則符合度。");
        }

        foreach (JobPostingReadOnlyItem item in result.Value.Items)
        {
            JobSummaryData summary = JobCheckV02DisplayAdapter.CreateSummary(item);
            if (profile != null)
            {
                RequirementMatchResult match = RequirementMatchEngine.Compare(
                    profile,
                    item.JobPosting.Requirements,
                    DateTimeOffset.Now);
                RequirementScore score = RequirementScoreCalculator.Calculate(match);
                summary.fit_score = score.Score ?? -1;
                summary.v02Detail.v027Match = match;
                summary.v02Detail.v027Score = score;
                if (summary.v02Tracking != null)
                {
                    summary.v02Tracking.fit_score = summary.fit_score;
                }
            }
            summary.is_expired = IsTrackingExpired(summary.v02Tracking);
            loadedJobs.Add(summary);
        }

        Debug.Log("Loaded V0.2 jobs: " + loadedJobs.Count + " from " + dataRoot);
    }

    private JobTrackingData FindLoadedV02Tracking(string jobId)
    {
        foreach (JobSummaryData job in loadedJobs)
        {
            if (job != null && string.Equals(job.id, jobId, StringComparison.Ordinal))
            {
                return job.v02Tracking;
            }
        }

        return null;
    }

    /// <summary>
    /// 將既有 UI 狀態代碼翻譯成 V0.2 明確事件並安全寫入。
    /// 本人結案必須由呼叫端提供原因；面試預定時間可選填。
    /// </summary>
    public JobTrackingData UpdateV02ApplicationStatus(
        string jobId,
        string status,
        CandidateCloseReason? closeReason,
        string closeReasonNote,
        DateTimeOffset? scheduledFor,
        DateTimeOffset? occurredAt = null)
    {
        if (!TryMapV02Event(status, out ApplicationEventType eventType, out EventActor actor))
        {
            Debug.LogError("V0.2 不支援的狀態操作：" + status);
            return FindLoadedV02Tracking(jobId);
        }

        PersistenceStorageResult<ApplicationWriteSummary> result =
            ApplicationCommandService.RecordEvent(
                ResolveProjectRelativePath(ActiveDataRootPath),
                jobId,
                eventType,
                actor,
                occurredAt: occurredAt,
                scheduledFor: scheduledFor,
                closeReason: closeReason,
                closeReasonNote: closeReasonNote);
        return FinishV02Write(jobId, result);
    }

    /// <summary>
    /// 切換 V0.2 Application 收藏狀態，不建立流程事件。
    /// </summary>
    public JobTrackingData UpdateV02Favorite(string jobId, bool favorite)
    {
        PersistenceStorageResult<ApplicationWriteSummary> result =
            ApplicationCommandService.SetFavorite(
                ResolveProjectRelativePath(ActiveDataRootPath),
                jobId,
                favorite);
        return FinishV02Write(jobId, result);
    }

    /// <summary>
    /// 保存 V0.2 Application 自由備註，不建立流程事件。
    /// </summary>
    public JobTrackingData UpdateV02Notes(string jobId, string notes)
    {
        PersistenceStorageResult<ApplicationWriteSummary> result =
            ApplicationCommandService.SetNotes(
                ResolveProjectRelativePath(ActiveDataRootPath),
                jobId,
                notes);
        return FinishV02Write(jobId, result);
    }

    private JobTrackingData FinishV02Write(
        string jobId,
        PersistenceStorageResult<ApplicationWriteSummary> result)
    {
        if (!result.IsSuccess)
        {
            foreach (PersistenceStorageIssue issue in result.Issues)
            {
                Debug.LogError(
                    "V0.2 write failed [" + issue.Error + "] "
                    + issue.FieldPath + " " + issue.Message);
            }

            return FindLoadedV02Tracking(jobId);
        }

        Load();
        return FindLoadedV02Tracking(jobId);
    }

    private static bool TryMapV02Event(
        string status,
        out ApplicationEventType eventType,
        out EventActor actor)
    {
        actor = EventActor.Candidate;
        switch (status)
        {
            case "interested": eventType = ApplicationEventType.Saved; return true;
            case "applied": eventType = ApplicationEventType.Applied; return true;
            case "viewed": eventType = ApplicationEventType.Viewed; actor = EventActor.Platform; return true;
            case "contacted": eventType = ApplicationEventType.Contacted; actor = EventActor.Company; return true;
            case "interview_scheduled": eventType = ApplicationEventType.InterviewScheduled; actor = EventActor.Company; return true;
            case "interviewing": eventType = ApplicationEventType.InterviewCompleted; return true;
            case "waiting_reply": eventType = ApplicationEventType.WaitingResponseStarted; return true;
            case "no_response": eventType = ApplicationEventType.NoResponseMarked; return true;
            case "offer": eventType = ApplicationEventType.OfferReceived; actor = EventActor.Company; return true;
            case "rejected": eventType = ApplicationEventType.RejectedByCompany; actor = EventActor.Company; return true;
            case "not_applying": eventType = ApplicationEventType.ClosedByCandidate; return true;
            default: eventType = default; return false;
        }
    }

    /// <summary>
    /// 判斷職缺是否符合目前篩選條件。
    /// </summary>
    /// <param name="job">職缺摘要。</param>
    /// <param name="condition">篩選條件。</param>
    /// <returns>符合條件時回傳 true。</returns>
    private bool PassesFilter(JobSummaryData job, JobFilterCondition condition)
    {
        if (job == null || condition == null)
        {
            return false;
        }

        if (condition.salaryMin >= 0 && job.salary_min < condition.salaryMin)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(condition.status) && condition.status != "all" && job.status != condition.status)
        {
            return false;
        }

        if (condition.expiredOnly && !job.is_expired)
        {
            return false;
        }

        if (condition.fitScoreMin >= 0 && job.fit_score < condition.fitScoreMin)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 取得某狀態的預設逾期天數。
    /// </summary>
    /// <param name="status">狀態代碼。</param>
    /// <returns>天數；0 表示不自動逾期。</returns>
    private int GetDefaultExpireDays(string status)
    {
        switch (status)
        {
            case "interested":
                return 14;
            case "applied":
                return 7;
            case "interview_scheduled":
                return 3;
            case "interviewing":
                return 7;
            case "waiting_reply":
                return 7;
            case "archived_wait_other_job_result":
                return 14;
            default:
                return 0;
        }
    }

    /// <summary>
    /// 依逾期、最愛、ID 排序列表資料。
    /// </summary>
    private void SortJobsForDisplay()
    {
        displayJobs.Sort(CompareJobSummaryForDisplay);
    }

    /// <summary>
    /// 職缺列表排序比較方法。
    /// </summary>
    /// <param name="left">左側項目。</param>
    /// <param name="right">右側項目。</param>
    /// <returns>排序比較結果。</returns>
    private int CompareJobSummaryForDisplay(JobSummaryData left, JobSummaryData right)
    {
        if (left == null && right == null)
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        int expiredCompare = right.is_expired.CompareTo(left.is_expired);
        if (expiredCompare != 0)
        {
            return expiredCompare;
        }

        int favoriteCompare = right.favorite.CompareTo(left.favorite);
        if (favoriteCompare != 0)
        {
            return favoriteCompare;
        }

        return string.Compare(left.id, right.id, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 在 Demo 與本機個人資料之間切換。首次切到個人資料時才建立空白資料目錄，
    /// 避免單純讀取畫面就產生檔案。
    /// </summary>
    public void SwitchDataProfile()
    {
        JobCheckDataProfile next = currentDataProfile == JobCheckDataProfile.Demo
            ? JobCheckDataProfile.Personal
            : JobCheckDataProfile.Demo;

        if (next == JobCheckDataProfile.Personal && !EnsurePersonalDataRoot())
        {
            return;
        }

        currentDataProfile = next;
        PlayerPrefs.SetInt(DataProfilePreferenceKey, (int)currentDataProfile);
        PlayerPrefs.Save();
        RefreshDataProfileUi();
        Load();
    }

    public void ShowAnalytics()
    {
        if (analyticsPanel == null) return;
        analyticsPanel.Open(
            ResolveProjectRelativePath(ActiveDataRootPath),
            currentDataProfile == JobCheckDataProfile.Personal ? "個人" : "Demo");
    }

    public void ShowPortableTransfer()
    {
        if (portableTransferPanel == null) return;
        portableTransferPanel.OpenExport(
            ResolveProjectRelativePath(personalDataRootPath),
            currentDataProfile == JobCheckDataProfile.Personal,
            Load);
    }

    public void ShowTrashManagement()
    {
        if (trashManagementPanel == null) return;
        trashManagementPanel.Open(
            ResolveProjectRelativePath(personalDataRootPath),
            currentDataProfile == JobCheckDataProfile.Personal,
            Load);
    }

    private void LoadSelectedDataProfile()
    {
        int saved = PlayerPrefs.GetInt(DataProfilePreferenceKey, (int)JobCheckDataProfile.Demo);
        currentDataProfile = saved == (int)JobCheckDataProfile.Personal
            ? JobCheckDataProfile.Personal
            : JobCheckDataProfile.Demo;
    }

    /// <summary>
    /// 明確初始化個人資料根目錄。Repository 的一般 Load 仍維持唯讀、不自動建檔。
    /// </summary>
    private bool EnsurePersonalDataRoot()
    {
        string root = ResolveProjectRelativePath(personalDataRootPath);
        if (Directory.Exists(root))
        {
            return true;
        }

        var emptyDataSet = new JobCheckDataSet(
            new List<Company>(),
            new List<JobPosting>(),
            new List<DomainApplication>(),
            new List<DomainApplicationEvent>());
        PersistenceStorageResult<PersistenceWriteSummary> result =
            JobCheckDataRepository.WriteSnapshot(root, emptyDataSet);
        if (result.IsSuccess)
        {
            return true;
        }

        foreach (PersistenceStorageIssue issue in result.Issues)
        {
            Debug.LogError(
                "個人資料區建立失敗 [" + issue.Error + "] "
                + issue.FilePath + " " + issue.Message);
        }

        return false;
    }

    private void RefreshDataProfileUi()
    {
        if (textDataProfile != null)
        {
            textDataProfile.text = currentDataProfile == JobCheckDataProfile.Personal
                ? "資料：個人（點擊切換）"
                : "資料：Demo（點擊切換）";
        }
    }

    /// <summary>
    /// 自動尋找並綁定 UI 參考。
    /// </summary>
    private void AutoBindReferences()
    {
        if (buttonLoad == null)
        {
            buttonLoad = FindChildButton("Button_Load");
        }

        if (buttonNext == null)
        {
            buttonNext = FindChildButton("Button_NextPage");
        }

        if (buttonLast == null)
        {
            buttonLast = FindChildButton("Button_LastPage");
        }

        if (buttonShowFilter == null)
        {
            buttonShowFilter = FindChildButton("Button_ShowFilter");
        }

        if (buttonAddJobPosting == null)
        {
            buttonAddJobPosting = FindChildButton("Button_AddJobPosting");
        }

        if (buttonSwitchDataProfile == null)
        {
            buttonSwitchDataProfile = FindChildButton("TMP_Date");
        }

        if (textDataProfile == null)
        {
            Transform profile = transform.Find("TMP_Date");
            textDataProfile = profile != null ? profile.GetComponent<TMP_Text>() : null;
        }

        if (buttonShowAnalytics == null)
            buttonShowAnalytics = FindChildButton("Button_ShowAnalytics");
        if (analyticsPanel == null)
            analyticsPanel = GetComponentInChildren<Panel_Analytics>(true);
        if (buttonPortableTransfer == null)
            buttonPortableTransfer = FindChildButton("Button_PortableTransfer");
        if (portableTransferPanel == null)
            portableTransferPanel = GetComponentInChildren<Panel_PortableTransfer>(true);
        if (buttonTrashManagement == null)
            buttonTrashManagement = FindChildButton("Button_TrashManagement");
        if (trashManagementPanel == null)
            trashManagementPanel = GetComponentInChildren<Panel_TrashManagement>(true);

        if (filterPanel == null)
        {
            filterPanel = FindObjectOfType<FilterPanel>(true);
        }

        if (jobDetailPanel == null)
        {
            jobDetailPanel = FindObjectOfType<Panel_JobDetail>(true);
        }

        if (jobPostingCreatePanel == null)
        {
            jobPostingCreatePanel = GetComponentInChildren<Panel_JobPostingCreate>(true);
        }

        if (jobPanelRoot == null)
        {
            Transform foundRoot = transform.Find("JobPanels");
            jobPanelRoot = foundRoot != null ? foundRoot : transform;
        }

        if (jobPanels.Count == 0 && jobPanelRoot != null)
        {
            jobPanels.AddRange(jobPanelRoot.GetComponentsInChildren<Panel_SingleJob>(true));

            if (jobPanels.Count == 0)
            {
                Debug.LogError(
                    "JobPanels must contain preconfigured Panel_SingleJob components.",
                    this);
            }

            foreach (Panel_SingleJob panel in jobPanels)
            {
                if (panel != null)
                {
                    panel.SetOwner(this);
                }
            }
        }
    }

    /// <summary>
    /// 綁定總攬頁按鈕事件。
    /// </summary>
    private void BindButtons()
    {
        if (buttonLoad != null)
        {
            buttonLoad.onClick.RemoveListener(Load);
            buttonLoad.onClick.AddListener(Load);
        }

        if (buttonNext != null)
        {
            buttonNext.onClick.RemoveListener(Next);
            buttonNext.onClick.AddListener(Next);
        }

        if (buttonLast != null)
        {
            buttonLast.onClick.RemoveListener(Last);
            buttonLast.onClick.AddListener(Last);
        }

        if (buttonShowFilter != null)
        {
            buttonShowFilter.onClick.RemoveListener(ShowFilter);
            buttonShowFilter.onClick.AddListener(ShowFilter);
        }

        if (buttonAddJobPosting != null)
        {
            buttonAddJobPosting.onClick.RemoveListener(ShowAddJobPosting);
            buttonAddJobPosting.onClick.AddListener(ShowAddJobPosting);
        }

        if (buttonSwitchDataProfile != null)
        {
            buttonSwitchDataProfile.onClick.RemoveListener(SwitchDataProfile);
            buttonSwitchDataProfile.onClick.AddListener(SwitchDataProfile);
        }
        if (buttonShowAnalytics != null)
        {
            buttonShowAnalytics.onClick.RemoveListener(ShowAnalytics);
            buttonShowAnalytics.onClick.AddListener(ShowAnalytics);
        }
        if (buttonPortableTransfer != null)
        {
            buttonPortableTransfer.onClick.RemoveListener(ShowPortableTransfer);
            buttonPortableTransfer.onClick.AddListener(ShowPortableTransfer);
        }
        if (buttonTrashManagement != null)
        {
            buttonTrashManagement.onClick.RemoveListener(ShowTrashManagement);
            buttonTrashManagement.onClick.AddListener(ShowTrashManagement);
        }
    }

    /// <summary>
    /// 依子物件名稱尋找 Button。
    /// </summary>
    /// <param name="childName">子物件名稱。</param>
    /// <returns>找到的 Button；找不到回傳 null。</returns>
    private Button FindChildButton(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    /// <summary>
    /// 將專案相對路徑轉成完整路徑。
    /// </summary>
    /// <param name="path">絕對路徑或相對於 Unity 專案根目錄的路徑。</param>
    /// <returns>完整路徑。</returns>
    private string ResolveProjectRelativePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }

}

/// <summary>
/// JobCheck 的資料使用情境。Demo 可進 Git；Personal 永遠只留在使用者電腦。
/// </summary>
public enum JobCheckDataProfile
{
    Demo = 0,
    Personal = 1
}

[Serializable]
/// <summary>
/// 總攬列表使用的職缺摘要資料。
/// </summary>
public class JobSummaryData
{
    public string id;
    public string company;
    public string title;
    public string salary;
    public int salary_min;
    public string parse_status;
    public string status;
    public string last_action_at;
    public string manual_expire_at;
    public bool favorite;
    public int fit_score;
    public bool is_expired;

    [NonSerialized] public JobDetailData v02Detail;
    [NonSerialized] public JobTrackingData v02Tracking;
}

[Serializable]
/// <summary>
/// 單一職缺詳細 JSON 的根資料。
/// </summary>
public class JobDetailData
{
    public string schema_version;
    public string id;
    public string parse_status;
    public SourceJsonData source;
    public CompanyJsonData company;
    public JobJsonData job;
    public CompensationJsonData compensation;
    public LocationJsonData location;
    public WorkConditionsJsonData work_conditions;
    public List<string> responsibilities;
    public RequirementsJsonData requirements;
    public BenefitsJsonData benefits;
    public List<string> recruitment_process;
    public List<string> tags;
    public List<string> risk_flags;

    [NonSerialized] public RequirementMatchResult v027Match;
    [NonSerialized] public RequirementScore v027Score;
}

[Serializable]
/// <summary>
/// 職缺來源資料。
/// </summary>
public class SourceJsonData
{
    public string platform;
    public string url;
    public string captured_at;
    public List<string> raw_images;
}

[Serializable]
/// <summary>
/// 公司資料。
/// </summary>
public class CompanyJsonData
{
    public string name;
    public string industry;
    public string raw_text;
}

[Serializable]
/// <summary>
/// 職缺基本資料。
/// </summary>
public class JobJsonData
{
    public string title;
    public string department;
    public string category;
    public string updated_date;
    public string openings;
    public string raw_text;
}

[Serializable]
/// <summary>
/// 薪資資料。
/// </summary>
public class CompensationJsonData
{
    public string type;
    public string period;
    public bool has_min;
    public bool has_max;
    public int min;
    public int max;
    public string currency;
    public string raw_text;
    public string notes;
}

[Serializable]
/// <summary>
/// 工作地點與工作模式資料。
/// </summary>
public class LocationJsonData
{
    public string work_mode;
    public string city;
    public string district;
    public string address;
    public bool remote_allowed;
    public string raw_text;
}

[Serializable]
/// <summary>
/// 工作條件資料。
/// </summary>
public class WorkConditionsJsonData
{
    public string employment_type;
    public string working_hours;
    public string business_trip;
    public string management_responsibility;
    public string leave_policy;
    public string start_date;
}

[Serializable]
/// <summary>
/// 技能需求與資格條件資料。
/// </summary>
public class RequirementsJsonData
{
    public string experience;
    public string education;
    public string major;
    public List<LanguageJsonData> languages;
    public List<string> tools;
    public List<string> skills;
    public List<string> other_conditions;
}

[Serializable]
/// <summary>
/// 語文條件資料。
/// </summary>
public class LanguageJsonData
{
    public string name;
    public string listening;
    public string speaking;
    public string reading;
    public string writing;
    public string raw_text;
}

[Serializable]
/// <summary>
/// 福利制度資料。
/// </summary>
public class BenefitsJsonData
{
    public List<string> salary_bonus;
    public List<string> insurance_health;
    public List<string> flexibility;
    public List<string> training;
    public List<string> life;
    public List<string> other;
}

[Serializable]
/// <summary>
/// V0.2 Application 與事件投影成 UI 使用的追蹤資料。
/// </summary>
public class JobTrackingData
{
    public string job_id;
    public string application_id;
    public string status;
    public string last_action_at;
    public string manual_expire_at;
    public bool favorite;
    public int fit_score;
    public string notes;
    public bool is_archived;
    public List<string> event_history = new List<string>();
}
