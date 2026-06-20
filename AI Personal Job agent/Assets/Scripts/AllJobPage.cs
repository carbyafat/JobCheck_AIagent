using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class AllJobPage : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private string jobsIndexPath = "../job_index/jobs_index.json";
    [SerializeField] private string jobsFolderPath = "../jobs";

    [Header("UI")]
    [SerializeField] private Button buttonLoad;
    [SerializeField] private Button buttonNext;
    [SerializeField] private Button buttonLast;
    [SerializeField] private Button buttonShowFilter;
    [SerializeField] private Transform jobPanelRoot;
    [SerializeField] private List<Panel_SingleJob> jobPanels = new List<Panel_SingleJob>();

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

        RefreshPage();
    }

    public void Next()
    {
        if (!HasNextPage())
        {
            return;
        }

        currentPage++;
        RefreshPage();
    }

    public void Last()
    {
        if (!HasLastPage())
        {
            return;
        }

        currentPage--;
        RefreshPage();
    }

    public void ShowFilter()
    {
        // TODO: Filter UI is not implemented yet.
    }

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

    private bool HasNextPage()
    {
        return loadedJobs.Count > PageSize && currentPage < TotalPageCount - 1;
    }

    private bool HasLastPage()
    {
        return loadedJobs.Count > PageSize && currentPage > 0;
    }

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
            loadedJobs.Add(job);
        }
    }

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
            summary.status = detail.tracking != null ? detail.tracking.status : string.Empty;
            loadedJobs.Add(summary);
        }
    }

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
        }
    }

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

    private Button FindChildButton(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private string ResolveProjectRelativePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }

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
public class JobsIndexData
{
    public string schema_version;
    public string generated_at;
    public List<JobSummaryData> jobs;
}

[Serializable]
public class JobSummaryData
{
    public string id;
    public string file;
    public string company;
    public string title;
    public string salary;
    public string parse_status;
    public string status;

    [NonSerialized] public string detailFullPath;
}

[Serializable]
public class JobDetailData
{
    public string schema_version;
    public string id;
    public string parse_status;
    public CompanyJsonData company;
    public JobJsonData job;
    public CompensationJsonData compensation;
    public TrackingJsonData tracking;
}

[Serializable]
public class CompanyJsonData
{
    public string name;
}

[Serializable]
public class JobJsonData
{
    public string title;
}

[Serializable]
public class CompensationJsonData
{
    public string raw_text;
}

[Serializable]
public class TrackingJsonData
{
    public string status;
}
