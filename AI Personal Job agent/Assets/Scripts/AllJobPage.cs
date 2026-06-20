using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 職缺總攬頁控制器：載入索引、套用 tracking、處理分頁、排序與開啟詳細頁。
/// </summary>
public class AllJobPage : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("職缺總攬索引檔路徑，相對於 Unity 專案根目錄。")]
    [SerializeField] private string jobsIndexPath = "../job_index/jobs_index.json";
    [Tooltip("索引檔不存在時，用來掃描詳細職缺 JSON 的資料夾。")]
    [SerializeField] private string jobsFolderPath = "../jobs";
    [Tooltip("使用者操作資料 tracking JSON 的資料夾。")]
    [SerializeField] private string jobTrackingFolderPath = "../job_tracking";

    [Header("UI")]
    [Tooltip("載入全部職缺資料的按鈕。")]
    [SerializeField] private Button buttonLoad;
    [Tooltip("下一頁按鈕，沒有下一頁時會隱藏。")]
    [SerializeField] private Button buttonNext;
    [Tooltip("上一頁按鈕，沒有上一頁時會隱藏。")]
    [SerializeField] private Button buttonLast;
    [Tooltip("篩選按鈕，目前只保留入口。")]
    [SerializeField] private Button buttonShowFilter;
    [Tooltip("職缺列表列物件的父節點。")]
    [SerializeField] private Transform jobPanelRoot;
    [Tooltip("可重複使用的職缺列 UI。")]
    [SerializeField] private List<Panel_SingleJob> jobPanels = new List<Panel_SingleJob>();
    [Tooltip("職缺詳細頁。")]
    [SerializeField] private Panel_JobDetail jobDetailPanel;

    private readonly List<JobSummaryData> loadedJobs = new List<JobSummaryData>();
    private int currentPage;

    private int PageSize
    {
        get { return Mathf.Max(1, jobPanels.Count); }
    }

    private int TotalPageCount
    {
        get
        {
            if (loadedJobs.Count == 0)
            {
                return 1;
            }

            return Mathf.CeilToInt((float)loadedJobs.Count / PageSize);
        }
    }

    private void Awake()
    {
        AutoBindReferences();
        BindButtons();
        RefreshPage();
    }

    /// <summary>
    /// 載入職缺索引或職缺資料夾，並依逾期/最愛規則排序後刷新畫面。
    /// </summary>
    public void Load()
    {
        loadedJobs.Clear();
        currentPage = 0;

        string indexFullPath = ResolveProjectRelativePath(jobsIndexPath);
        if (File.Exists(indexFullPath))
        {
            LoadFromIndex(indexFullPath);
        }
        else
        {
            LoadFromJobFolder(ResolveProjectRelativePath(jobsFolderPath));
        }

        SortJobsForDisplay();
        RefreshPage();
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
    /// 篩選入口，目前尚未實作。
    /// </summary>
    public void ShowFilter()
    {
        // TODO: Filter UI is not implemented yet.
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

        if (string.IsNullOrEmpty(job.detailFullPath))
        {
            Debug.LogWarning("Job detail path is empty: " + job.id);
            return;
        }

        if (!File.Exists(job.detailFullPath))
        {
            Debug.LogWarning("Job detail json not found: " + job.detailFullPath);
            return;
        }

        string json = File.ReadAllText(job.detailFullPath);
        JobDetailData detail = JsonUtility.FromJson<JobDetailData>(json);
        JobTrackingData tracking = LoadOrCreateTracking(job.id);

        jobDetailPanel.SetAllJobPage(this);
        jobDetailPanel.Show(detail, tracking);
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
    /// 更新職缺狀態，寫回 tracking JSON，並刷新列表。
    /// </summary>
    /// <param name="jobId">職缺 ID。</param>
    /// <param name="status">新的狀態代碼。</param>
    /// <returns>更新後的 tracking 資料。</returns>
    public JobTrackingData UpdateJobTrackingStatus(string jobId, string status)
    {
        JobTrackingData tracking = LoadOrCreateTracking(jobId);
        tracking.status = status;
        tracking.last_action_at = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
        tracking.manual_expire_at = string.Empty;
        SaveTracking(tracking);

        foreach (JobSummaryData job in loadedJobs)
        {
            if (job.id == jobId)
            {
                job.status = tracking.status;
                job.last_action_at = tracking.last_action_at;
                job.manual_expire_at = tracking.manual_expire_at;
                job.favorite = tracking.favorite;
                job.fit_score = tracking.fit_score;
                job.is_expired = IsTrackingExpired(tracking);
                break;
            }
        }

        SortJobsForDisplay();
        RefreshPage();
        return tracking;
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

            if (jobIndex < loadedJobs.Count)
            {
                panel.gameObject.SetActive(true);
                panel.SetOwner(this);
                panel.SetData(loadedJobs[jobIndex]);
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
        return loadedJobs.Count > PageSize && currentPage < TotalPageCount - 1;
    }

    /// <summary>
    /// 是否存在上一頁。
    /// </summary>
    private bool HasLastPage()
    {
        return loadedJobs.Count > PageSize && currentPage > 0;
    }

    /// <summary>
    /// 從 jobs_index.json 載入列表摘要。
    /// </summary>
    /// <param name="indexFullPath">索引檔完整路徑。</param>
    private void LoadFromIndex(string indexFullPath)
    {
        string json = File.ReadAllText(indexFullPath);
        JobsIndexData indexData = JsonUtility.FromJson<JobsIndexData>(json);
        string indexDirectory = Path.GetDirectoryName(indexFullPath);

        if (indexData == null || indexData.jobs == null)
        {
            Debug.LogWarning("jobs_index.json parsed empty.");
            return;
        }

        foreach (JobSummaryData job in indexData.jobs)
        {
            if (job == null)
            {
                continue;
            }

            job.detailFullPath = ResolvePathFromBase(indexDirectory, job.file);
            ApplyTrackingToSummary(job);
            loadedJobs.Add(job);
        }
    }

    /// <summary>
    /// 直接掃描 jobs 資料夾建立列表摘要。
    /// </summary>
    /// <param name="folderFullPath">jobs 資料夾完整路徑。</param>
    private void LoadFromJobFolder(string folderFullPath)
    {
        if (!Directory.Exists(folderFullPath))
        {
            Debug.LogWarning("Job folder not found: " + folderFullPath);
            return;
        }

        string[] files = Directory.GetFiles(folderFullPath, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        foreach (string file in files)
        {
            string json = File.ReadAllText(file);
            JobDetailData detail = JsonUtility.FromJson<JobDetailData>(json);

            if (detail == null)
            {
                continue;
            }

            JobSummaryData summary = new JobSummaryData();
            summary.id = detail.id;
            summary.file = file;
            summary.detailFullPath = file;
            summary.company = detail.company != null ? detail.company.name : string.Empty;
            summary.title = detail.job != null ? detail.job.title : string.Empty;
            summary.salary = detail.compensation != null ? detail.compensation.raw_text : string.Empty;
            summary.parse_status = detail.parse_status;
            ApplyTrackingToSummary(summary);
            loadedJobs.Add(summary);
        }
    }

    /// <summary>
    /// 將 tracking 狀態、最愛、適配度、逾期結果套用到列表資料。
    /// </summary>
    /// <param name="job">要套用 tracking 的職缺摘要。</param>
    private void ApplyTrackingToSummary(JobSummaryData job)
    {
        if (job == null)
        {
            return;
        }

        JobTrackingData tracking = LoadOrCreateTracking(job.id);
        job.status = tracking.status;
        job.last_action_at = tracking.last_action_at;
        job.manual_expire_at = tracking.manual_expire_at;
        job.favorite = tracking.favorite;
        job.fit_score = tracking.fit_score;
        job.is_expired = IsTrackingExpired(tracking);
    }

    /// <summary>
    /// 讀取 tracking；如果不存在則建立預設 tracking 檔。
    /// </summary>
    /// <param name="jobId">職缺 ID。</param>
    /// <returns>職缺 tracking 資料。</returns>
    private JobTrackingData LoadOrCreateTracking(string jobId)
    {
        string trackingPath = GetTrackingPath(jobId);

        if (File.Exists(trackingPath))
        {
            string json = File.ReadAllText(trackingPath);
            JobTrackingData loaded = JsonUtility.FromJson<JobTrackingData>(json);
            if (loaded != null && !string.IsNullOrEmpty(loaded.job_id))
            {
                return loaded;
            }
        }

        JobTrackingData tracking = new JobTrackingData();
        tracking.job_id = jobId;
        tracking.status = "not_viewed";
        tracking.last_action_at = string.Empty;
        tracking.manual_expire_at = string.Empty;
        tracking.favorite = false;
        tracking.fit_score = -1;
        SaveTracking(tracking);
        return tracking;
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
        loadedJobs.Sort(CompareJobSummaryForDisplay);
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
    /// 將 tracking 資料寫回檔案。
    /// </summary>
    /// <param name="tracking">要儲存的 tracking 資料。</param>
    private void SaveTracking(JobTrackingData tracking)
    {
        string folderPath = ResolveProjectRelativePath(jobTrackingFolderPath);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string json = JsonUtility.ToJson(tracking, true);
        File.WriteAllText(GetTrackingPath(tracking.job_id), json);
    }

    /// <summary>
    /// 取得 tracking 檔完整路徑。
    /// </summary>
    /// <param name="jobId">職缺 ID。</param>
    /// <returns>tracking JSON 完整路徑。</returns>
    private string GetTrackingPath(string jobId)
    {
        string folderPath = ResolveProjectRelativePath(jobTrackingFolderPath);
        return Path.Combine(folderPath, jobId + ".tracking.json");
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

        if (jobDetailPanel == null)
        {
            jobDetailPanel = FindObjectOfType<Panel_JobDetail>(true);
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
                Transform[] children = jobPanelRoot.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in children)
                {
                    if (child == jobPanelRoot)
                    {
                        continue;
                    }

                    if (!child.name.StartsWith("Panel_SingleJob", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Panel_SingleJob panel = child.GetComponent<Panel_SingleJob>();
                    if (panel == null)
                    {
                        panel = child.gameObject.AddComponent<Panel_SingleJob>();
                    }

                    jobPanels.Add(panel);
                }
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

    /// <summary>
    /// 將基準資料夾下的相對路徑轉成完整路徑。
    /// </summary>
    /// <param name="baseDirectory">基準資料夾。</param>
    /// <param name="path">絕對路徑或相對路徑。</param>
    /// <returns>完整路徑。</returns>
    private string ResolvePathFromBase(string baseDirectory, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, path));
    }
}

[Serializable]
/// <summary>
/// jobs_index.json 的根資料。
/// </summary>
public class JobsIndexData
{
    public string schema_version;
    public string generated_at;
    public List<JobSummaryData> jobs;
}

[Serializable]
/// <summary>
/// 總攬列表使用的職缺摘要資料。
/// </summary>
public class JobSummaryData
{
    public string id;
    public string file;
    public string company;
    public string title;
    public string salary;
    public string parse_status;
    public string status;
    public string last_action_at;
    public string manual_expire_at;
    public bool favorite;
    public int fit_score;
    public bool is_expired;

    [NonSerialized] public string detailFullPath;
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
    public TrackingJsonData tracking;
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
/// 舊版職缺 JSON 內的 tracking 資料。主要保留相容用途。
/// </summary>
public class TrackingJsonData
{
    public string status;
    public string priority;
    public bool favorite;
    public string applied_at;
    public string last_updated_at;
    public string notes;
}

[Serializable]
/// <summary>
/// job_tracking/*.tracking.json 的使用者操作資料。
/// </summary>
public class JobTrackingData
{
    public string job_id;
    public string status;
    public string last_action_at;
    public string manual_expire_at;
    public bool favorite;
    public int fit_score;
}
