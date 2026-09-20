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

    private void OnRectTransformDimensionsChange()
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
        StyleButton("Button_MoreActions", ButtonRole.Secondary);
        StyleButton("Button_LastPage", ButtonRole.Secondary);
        StyleButton("Button_NextPage", ButtonRole.Secondary);

        StyleButton("Button_TrashManagement", ButtonRole.Tool);
        StyleButton("Button_PortableTransfer", ButtonRole.Tool);
        StyleButton("Button_ShowAnalytics", ButtonRole.Tool);
        StyleMoreActionsPanel();
        StyleModalWindows();
    }

    private void StyleText(string objectName, TMP_FontAsset font, float size, Color color)
    {
        Transform target = FindDescendant(objectName);
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
        Transform target = FindDescendant(objectName);
        StyleButton(target, role);
    }

    private void StyleButton(Button button, ButtonRole role)
    {
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
                labelColor = theme.TextPrimary;
                break;
            case ButtonRole.Danger:
                normal = theme.Danger;
                highlighted = Color.Lerp(theme.Danger, Color.white, 0.12f);
                pressed = Color.Lerp(theme.Danger, Color.black, 0.16f);
                labelColor = theme.TextOnPrimary;
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
        colors.disabledColor = WithAlpha(theme.TextSecondary, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        Image image = button.targetGraphic as Image;
        if (image != null)
        {
            image.color = normal;
            if (theme.ButtonBackgroundSprite != null)
            {
                image.sprite = theme.ButtonBackgroundSprite;
            }

            image.type = Image.Type.Sliced;
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

    private void StyleMoreActionsPanel()
    {
        Transform target = FindDescendant("Panel_MoreActions");
        Image image = target != null ? target.GetComponent<Image>() : null;
        if (image == null)
        {
            return;
        }

        image.color = theme.Surface;
        if (theme.ButtonBackgroundSprite != null)
        {
            image.sprite = theme.ButtonBackgroundSprite;
        }

        image.type = Image.Type.Sliced;
    }

    private void StyleModalWindows()
    {
        StyleModal("FilterPanels", 1400f, 760f);
        StyleModal("Panel_TrashManagement", 1400f, 740f);
        StyleModal("Panel_PortableTransfer", 1400f, 740f);
        StyleModal("Panel_Analytics", 1400f, 900f);
        StyleModal("Panel_JobPostingCreate", 1400f, 900f);
    }

    private void StyleModal(string modalName, float preferredWidth, float preferredHeight)
    {
        Transform modal = FindDescendant(modalName);
        if (modal == null)
        {
            return;
        }

        RectTransform modalRect = modal as RectTransform;
        if (modalRect != null)
        {
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.anchoredPosition = Vector2.zero;
            modalRect.sizeDelta = Vector2.zero;
        }

        Image overlay = modal.GetComponent<Image>();
        if (overlay != null)
        {
            overlay.color = theme.Overlay;
            overlay.raycastTarget = true;
        }

        Transform card = FindDescendant(modal, "Card");
        RectTransform cardRect = card as RectTransform;
        if (cardRect != null)
        {
            Vector2 available = modalRect != null ? modalRect.rect.size : Vector2.zero;
            float viewportWidth = available.x > 0f ? available.x * theme.ModalViewportRatio : preferredWidth;
            float viewportHeight = available.y > 0f ? available.y * theme.ModalViewportRatio : preferredHeight;
            float width = Mathf.Min(preferredWidth, theme.ModalMaxWidth, viewportWidth);
            float height = Mathf.Min(preferredHeight, theme.ModalMaxHeight, viewportHeight);
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(width, height);

            Image cardImage = card.GetComponent<Image>();
            if (cardImage != null)
            {
                cardImage.color = theme.Surface;
                if (theme.ButtonBackgroundSprite != null)
                {
                    cardImage.sprite = theme.ButtonBackgroundSprite;
                }

                cardImage.type = Image.Type.Sliced;
            }
        }

        StyleModalText(modal, "Title");
        StyleModalText(modal, "Text_Title");
        foreach (Button button in modal.GetComponentsInChildren<Button>(true))
        {
            StyleButton(button, ResolveModalButtonRole(button.name));
        }
    }

    private void StyleModalText(Transform modal, string objectName)
    {
        Transform target = FindDescendant(modal, objectName);
        TMP_Text text = target != null ? target.GetComponent<TMP_Text>() : null;
        if (text == null)
        {
            return;
        }

        if (theme.BodyFont != null)
        {
            text.font = theme.BodyFont;
        }

        text.fontSize = theme.ModalTitleSize;
        text.fontStyle = FontStyles.Bold;
        text.color = theme.TextPrimary;
    }

    private static ButtonRole ResolveModalButtonRole(string objectName)
    {
        if (objectName == "Button_DeletePermanently" || objectName == "Button_EmptyTrash")
        {
            return ButtonRole.Danger;
        }

        if (objectName == "Button_AppliedFilter" ||
            objectName == "Button_SaveJobPosting" ||
            objectName == "Button_Restore" ||
            objectName == "Button_Import")
        {
            return ButtonRole.Primary;
        }

        return ButtonRole.Secondary;
    }

    private void StyleButton(Transform target, ButtonRole role)
    {
        Button button = target != null ? target.GetComponent<Button>() : null;
        if (button == null)
        {
            return;
        }

        StyleButton(button, role);
    }

    private Transform FindDescendant(string objectName)
    {
        foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private enum ButtonRole
    {
        Primary,
        Secondary,
        Tool,
        Danger
    }
}
