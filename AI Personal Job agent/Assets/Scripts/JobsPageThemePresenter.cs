using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 將共用 Theme 套用到職缺總覽頁的背景、標題與主要操作。
/// 單筆職缺卡片由 Panel_SingleJob 自己套用 Theme，確保重複使用時外觀一致。
/// </summary>
[ExecuteAlways]
public sealed class JobsPageThemePresenter : MonoBehaviour
{
    [SerializeField] private JobCheckUiTheme theme;

    public JobCheckUiTheme Theme => theme;

    private void OnEnable()
    {
        ApplyTheme();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyTheme();
    }
#endif

    public void ApplyTheme()
    {
        if (theme == null)
        {
            return;
        }

        Image background = GetComponent<Image>();
        if (background != null)
        {
            background.color = theme.AppBackground;
        }

        StyleText("Title_AllJob", theme.DisplayFont, theme.PageTitleSize, theme.TextPrimary);
        StyleText("TMP_Date", theme.BodyFont, theme.SupportingTextSize, theme.TextSecondary);

        StyleButton("Button_AddJobPosting", ButtonRole.Primary);

        StyleButton("Button_ShowFilter", ButtonRole.Secondary);
        StyleButton("Button_Load", ButtonRole.Secondary);
        StyleButton("Button_LastPage", ButtonRole.Secondary);
        StyleButton("Button_NextPage", ButtonRole.Secondary);

        StyleButton("Button_TrashManagement", ButtonRole.Tool);
        StyleButton("Button_PortableTransfer", ButtonRole.Tool);
        StyleButton("Button_ShowAnalytics", ButtonRole.Tool);
    }

    private void StyleText(string objectName, TMP_FontAsset font, float size, Color color)
    {
        Transform target = transform.Find(objectName);
        if (target == null)
        {
            return;
        }

        TMP_Text tmp = target.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            if (font != null)
            {
                tmp.font = font;
            }

            tmp.fontSize = size;
            tmp.color = color;
            return;
        }

        Text legacy = target.GetComponent<Text>();
        if (legacy != null)
        {
            legacy.fontSize = Mathf.RoundToInt(size);
            legacy.color = color;
        }
    }

    private void StyleButton(string objectName, ButtonRole role)
    {
        Transform target = transform.Find(objectName);
        Button button = target != null ? target.GetComponent<Button>() : null;
        if (button == null)
        {
            return;
        }

        float height = role == ButtonRole.Tool ? theme.CompactButtonHeight : theme.ButtonHeight;
        RectTransform rect = button.transform as RectTransform;
        if (rect != null)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        Color normal;
        Color highlighted;
        Color pressed;
        Color labelColor;

        switch (role)
        {
            case ButtonRole.Primary:
                normal = theme.Primary;
                highlighted = theme.PrimaryHover;
                pressed = theme.PrimaryPressed;
                labelColor = theme.TextOnPrimary;
                break;
            case ButtonRole.Tool:
                normal = theme.SurfaceMuted;
                highlighted = theme.Border;
                pressed = theme.Surface;
                labelColor = theme.TextSecondary;
                break;
            default:
                normal = theme.Surface;
                highlighted = theme.SurfaceMuted;
                pressed = theme.Border;
                labelColor = theme.TextPrimary;
                break;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.pressedColor = pressed;
        colors.selectedColor = highlighted;
        colors.disabledColor = WithAlpha(theme.TextSecondary, 0.35f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        Image image = button.targetGraphic as Image;
        if (image != null)
        {
            image.color = normal;
            if (image.sprite != null)
            {
                image.type = Image.Type.Sliced;
            }
        }

        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            if (theme.BodyFont != null)
            {
                label.font = theme.BodyFont;
            }

            label.fontSize = theme.ButtonTextSize;
            label.color = labelColor;
        }

        foreach (Text label in button.GetComponentsInChildren<Text>(true))
        {
            label.fontSize = Mathf.RoundToInt(theme.ButtonTextSize);
            label.color = labelColor;
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private enum ButtonRole
    {
        Primary,
        Secondary,
        Tool
    }
}
