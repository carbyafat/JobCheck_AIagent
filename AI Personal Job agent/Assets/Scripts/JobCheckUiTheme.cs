using TMPro;
using UnityEngine;

/// <summary>
/// JobCheck 共用視覺規格。頁面與 Prefab 應從同一份資產取得語意色彩、字體角色與尺寸，
/// 避免在各處重複決定相近但不同的數值。
/// </summary>
[CreateAssetMenu(fileName = "JobCheckUiTheme", menuName = "JobCheck/UI Theme")]
public sealed class JobCheckUiTheme : ScriptableObject
{
    [Header("Palette - Surfaces")]
    [SerializeField] private Color appBackground = new Color32(244, 241, 240, 255);
    [SerializeField] private Color surface = new Color32(255, 252, 251, 255);
    [SerializeField] private Color surfaceMuted = new Color32(238, 231, 229, 255);
    [SerializeField] private Color sidebarBackground = new Color32(53, 29, 35, 255);
    [SerializeField] private Color border = new Color32(216, 207, 208, 255);
    [SerializeField] private Color overlay = new Color(0f, 0f, 0f, 0.68f);

    [Header("Palette - Actions")]
    [SerializeField] private Color primary = new Color32(126, 70, 80, 255);
    [SerializeField] private Color primaryHover = new Color32(146, 88, 98, 255);
    [SerializeField] private Color primaryPressed = new Color32(103, 54, 65, 255);
    [SerializeField] private Color success = new Color32(62, 125, 91, 255);
    [SerializeField] private Color warning = new Color32(183, 121, 31, 255);
    [SerializeField] private Color danger = new Color32(169, 68, 66, 255);

    [Header("Palette - Text")]
    [SerializeField] private Color textPrimary = new Color32(46, 40, 41, 255);
    [SerializeField] private Color textSecondary = new Color32(113, 104, 106, 255);
    [SerializeField] private Color textOnPrimary = new Color32(255, 253, 252, 255);

    [Header("Typography")]
    [SerializeField] private TMP_FontAsset displayFont;
    [SerializeField] private TMP_FontAsset bodyFont;
    [SerializeField, Min(1f)] private float brandTitleSize = 38f;
    [SerializeField, Min(1f)] private float pageTitleSize = 48f;
    [SerializeField, Min(1f)] private float sectionTitleSize = 28f;
    [SerializeField, Min(1f)] private float bodySize = 22f;
    [SerializeField, Min(1f)] private float supportingTextSize = 18f;
    [SerializeField, Min(1f)] private float buttonTextSize = 22f;

    [Header("Spacing")]
    [SerializeField, Min(0f)] private float spaceXs = 8f;
    [SerializeField, Min(0f)] private float spaceSm = 12f;
    [SerializeField, Min(0f)] private float spaceMd = 16f;
    [SerializeField, Min(0f)] private float spaceLg = 24f;
    [SerializeField, Min(0f)] private float spaceXl = 32f;
    [SerializeField, Min(0f)] private float contentPadding = 32f;

    [Header("Components")]
    [SerializeField, Min(0f)] private float controlCornerRadius = 8f;
    [SerializeField, Min(0f)] private float cardCornerRadius = 12f;
    [SerializeField, Min(0f)] private float modalCornerRadius = 12f;
    [SerializeField, Min(1f)] private float compactButtonHeight = 48f;
    [SerializeField, Min(1f)] private float buttonHeight = 56f;
    [SerializeField, Min(1f)] private float inputHeight = 52f;
    [SerializeField, Min(1f)] private float sidebarWidth = 240f;
    [SerializeField, Min(1f)] private float modalMaxWidth = 1400f;

    public Color AppBackground => appBackground;
    public Color Surface => surface;
    public Color SurfaceMuted => surfaceMuted;
    public Color SidebarBackground => sidebarBackground;
    public Color Border => border;
    public Color Overlay => overlay;
    public Color Primary => primary;
    public Color PrimaryHover => primaryHover;
    public Color PrimaryPressed => primaryPressed;
    public Color Success => success;
    public Color Warning => warning;
    public Color Danger => danger;
    public Color TextPrimary => textPrimary;
    public Color TextSecondary => textSecondary;
    public Color TextOnPrimary => textOnPrimary;
    public TMP_FontAsset DisplayFont => displayFont;
    public TMP_FontAsset BodyFont => bodyFont;
    public float BrandTitleSize => brandTitleSize;
    public float PageTitleSize => pageTitleSize;
    public float SectionTitleSize => sectionTitleSize;
    public float BodySize => bodySize;
    public float SupportingTextSize => supportingTextSize;
    public float ButtonTextSize => buttonTextSize;
    public float SpaceXs => spaceXs;
    public float SpaceSm => spaceSm;
    public float SpaceMd => spaceMd;
    public float SpaceLg => spaceLg;
    public float SpaceXl => spaceXl;
    public float ContentPadding => contentPadding;
    public float ControlCornerRadius => controlCornerRadius;
    public float CardCornerRadius => cardCornerRadius;
    public float ModalCornerRadius => modalCornerRadius;
    public float CompactButtonHeight => compactButtonHeight;
    public float ButtonHeight => buttonHeight;
    public float InputHeight => inputHeight;
    public float SidebarWidth => sidebarWidth;
    public float ModalMaxWidth => modalMaxWidth;
}
