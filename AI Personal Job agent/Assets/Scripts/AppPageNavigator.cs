using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 應用程式最外層的頁面導覽。只負責切換主要頁面，不介入各頁面內部流程。
/// </summary>
public sealed class AppPageNavigator : MonoBehaviour
{
    public enum AppPage
    {
        Home,
        Jobs,
        Resume,
        JobPreferences
    }

    [Header("Pages")]
    [SerializeField] private GameObject pageHome;
    [SerializeField] private GameObject pageJobs;
    [SerializeField] private GameObject pageResume;
    [SerializeField] private GameObject pageJobPreferences;

    [Header("Navigation")]
    [SerializeField] private Button buttonHome;
    [SerializeField] private Button buttonJobs;
    [SerializeField] private Button buttonResume;
    [SerializeField] private Button buttonJobPreferences;

    [Header("Startup")]
    [SerializeField] private AppPage initialPage = AppPage.Jobs;

    public AppPage CurrentPage { get; private set; }

    private AppShellThemePresenter themePresenter;

    private void Awake()
    {
        themePresenter = GetComponent<AppShellThemePresenter>();
        BindButton(buttonHome, ShowHome);
        BindButton(buttonJobs, ShowJobs);
        BindButton(buttonResume, ShowResume);
        BindButton(buttonJobPreferences, ShowJobPreferences);
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

    public void ShowJobPreferences()
    {
        Show(AppPage.JobPreferences);
    }

    public void Show(AppPage page)
    {
        CurrentPage = page;
        SetPageActive(pageHome, page == AppPage.Home);
        SetPageActive(pageJobs, page == AppPage.Jobs);
        SetPageActive(pageResume, page == AppPage.Resume);
        SetPageActive(pageJobPreferences, page == AppPage.JobPreferences);

        if (themePresenter == null)
        {
            themePresenter = GetComponent<AppShellThemePresenter>();
        }

        if (themePresenter != null)
        {
            themePresenter.ApplyNavigation(GetButton(page));
        }
    }

    private static void SetPageActive(GameObject page, bool active)
    {
        if (page != null && page.activeSelf != active)
        {
            page.SetActive(active);
        }
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private Button GetButton(AppPage page)
    {
        switch (page)
        {
            case AppPage.Home:
                return buttonHome;
            case AppPage.Resume:
                return buttonResume;
            case AppPage.JobPreferences:
                return buttonJobPreferences;
            default:
                return buttonJobs;
        }
    }

}
