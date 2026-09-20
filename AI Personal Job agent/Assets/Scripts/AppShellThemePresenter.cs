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
    [SerializeField] private GameObject prototypeNote;
    [SerializeField] private Button buttonHome;
    [SerializeField] private Button buttonJobs;
    [SerializeField] private Button buttonResume;

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

        if (prototypeNote != null && prototypeNote.activeSelf)
        {
            prototypeNote.SetActive(false);
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
        colors.normalColor = active ? theme.Primary : theme.PrimaryPressed;
        colors.highlightedColor = theme.PrimaryHover;
        colors.pressedColor = theme.Primary;
        colors.selectedColor = active ? theme.Primary : theme.PrimaryHover;
        colors.disabledColor = WithAlpha(theme.TextSecondary, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        Image image = button.targetGraphic as Image;
        if (image != null)
        {
            image.type = Image.Type.Sliced;
            image.color = colors.normalColor;
        }

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = theme.TextOnPrimary;
            label.fontSize = Mathf.RoundToInt(theme.ButtonTextSize);
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
