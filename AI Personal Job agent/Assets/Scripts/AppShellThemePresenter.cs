using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 將 JobCheck 共用 Theme 套用到應用程式外框與主要導覽。
/// 保留明確的序列化參考，避免以場景名稱猜測重要 UI。
/// </summary>
[ExecuteAlways]
public sealed class AppShellThemePresenter : MonoBehaviour
{
    [SerializeField] private JobCheckUiTheme theme;
    [SerializeField] private Image appBackground;
    [SerializeField] private Image sidebarBackground;
    [SerializeField] private Text appNameText;
    [SerializeField] private Text versionText;
    [SerializeField] private Image activeNavigationIndicator;
    [SerializeField] private Button buttonHome;
    [SerializeField] private Button buttonJobs;
    [SerializeField] private Button buttonResume;
    [SerializeField] private Button buttonJobPreferences;

    public JobCheckUiTheme Theme => theme;

    private void OnEnable()
    {
        ApplyBaseStyle();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyBaseStyle();
    }
#endif

    public void ApplyNavigation(Button activeButton)
    {
        if (theme == null)
        {
            return;
        }

        StyleNavigationButton(buttonHome, buttonHome == activeButton, 0);
        StyleNavigationButton(buttonJobs, buttonJobs == activeButton, 1);
        StyleNavigationButton(buttonResume, buttonResume == activeButton, 2);
        StyleNavigationButton(buttonJobPreferences, buttonJobPreferences == activeButton, 3);
        PositionActiveIndicator(activeButton);
    }

    private void ApplyBaseStyle()
    {
        if (theme == null)
        {
            return;
        }

        if (appBackground != null)
        {
            appBackground.color = theme.AppBackground;
        }

        if (sidebarBackground != null)
        {
            sidebarBackground.color = theme.SidebarBackground;
        }

        if (appNameText != null)
        {
            appNameText.color = theme.TextOnPrimary;
            appNameText.fontSize = Mathf.RoundToInt(theme.BrandTitleSize);
        }

        if (versionText != null)
        {
            versionText.color = WithAlpha(theme.TextOnPrimary, 0.68f);
            versionText.fontSize = Mathf.RoundToInt(theme.SupportingTextSize);
        }

        ApplyNavigation(ResolveInitialButton());
    }

    private Button ResolveInitialButton()
    {
        AppPageNavigator navigator = GetComponent<AppPageNavigator>();
        if (navigator == null)
        {
            return buttonHome;
        }

        switch (navigator.CurrentPage)
        {
            case AppPageNavigator.AppPage.Jobs:
                return buttonJobs;
            case AppPageNavigator.AppPage.Resume:
                return buttonResume;
            case AppPageNavigator.AppPage.JobPreferences:
                return buttonJobPreferences;
            default:
                return buttonHome;
        }
    }

    private void StyleNavigationButton(Button button, bool active, int index)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rect = button.transform as RectTransform;
        if (rect != null)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, theme.ButtonHeight);
            Vector2 position = rect.anchoredPosition;
            position.y = -180f - index * (theme.ButtonHeight + theme.SpaceLg);
            rect.anchoredPosition = position;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = active ? theme.SurfaceMuted : theme.PrimaryPressed;
        colors.highlightedColor = active ? theme.Surface : theme.PrimaryHover;
        colors.pressedColor = active ? theme.Border : theme.Primary;
        colors.selectedColor = active ? theme.SurfaceMuted : theme.PrimaryHover;
        colors.disabledColor = WithAlpha(theme.TextSecondary, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        Image image = button.targetGraphic as Image;
        if (image != null)
        {
            if (theme.ButtonBackgroundSprite != null)
            {
                image.sprite = theme.ButtonBackgroundSprite;
            }

            image.type = Image.Type.Sliced;
            image.color = colors.normalColor;
        }

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = active ? theme.PrimaryPressed : theme.TextOnPrimary;
            label.fontSize = Mathf.RoundToInt(theme.ButtonTextSize);
        }
    }

    private void PositionActiveIndicator(Button activeButton)
    {
        if (activeNavigationIndicator == null)
        {
            return;
        }

        int activeIndex = activeButton == buttonJobs ? 1
            : activeButton == buttonResume ? 2
            : activeButton == buttonJobPreferences ? 3 : 0;
        RectTransform rect = activeNavigationIndicator.rectTransform;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, theme.ButtonHeight);
        Vector2 position = rect.anchoredPosition;
        position.y = -180f - activeIndex * (theme.ButtonHeight + theme.SpaceLg);
        rect.anchoredPosition = position;
        activeNavigationIndicator.color = theme.Primary;
        activeNavigationIndicator.gameObject.SetActive(activeButton != null);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
