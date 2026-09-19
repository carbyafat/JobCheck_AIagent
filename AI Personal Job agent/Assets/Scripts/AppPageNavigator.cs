using UnityEngine;

/// <summary>
/// 應用程式最外層的頁面導覽。只負責切換主要頁面，不介入各頁面內部流程。
/// </summary>
public sealed class AppPageNavigator : MonoBehaviour
{
    public enum AppPage
    {
        Home,
        Jobs,
        Resume
    }

    [Header("Pages")]
    [SerializeField] private GameObject pageHome;
    [SerializeField] private GameObject pageJobs;
    [SerializeField] private GameObject pageResume;

    [Header("Startup")]
    [SerializeField] private AppPage initialPage = AppPage.Jobs;

    public AppPage CurrentPage { get; private set; }

    private void Awake()
    {
        Show(initialPage);
    }

    public void ShowHome()
    {
        Show(AppPage.Home);
    }

    public void ShowJobs()
    {
        Show(AppPage.Jobs);
    }

    public void ShowResume()
    {
        Show(AppPage.Resume);
    }

    public void Show(AppPage page)
    {
        CurrentPage = page;
        SetPageActive(pageHome, page == AppPage.Home);
        SetPageActive(pageJobs, page == AppPage.Jobs);
        SetPageActive(pageResume, page == AppPage.Resume);
    }

    private static void SetPageActive(GameObject page, bool active)
    {
        if (page != null && page.activeSelf != active)
        {
            page.SetActive(active);
        }
    }
}
