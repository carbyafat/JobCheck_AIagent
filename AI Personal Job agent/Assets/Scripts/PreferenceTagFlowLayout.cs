using UnityEngine;
using UnityEngine.UI;

/// <summary>依可用寬度自動換行的標籤布局，並將所需高度回報給外層 ScrollView。</summary>
public sealed class PreferenceTagFlowLayout : LayoutGroup
{
    public RectOffset Padding { get => padding; set => padding = value; }
    public float HorizontalSpacing { get; set; } = 8f;
    public float VerticalSpacing { get; set; } = 8f;
    public float RowHeight { get; set; } = 44f;

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        SetLayoutInputForAxis(padding.horizontal, padding.horizontal, -1f, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        SetLayoutInputForAxis(CalculateRequiredHeight(), CalculateRequiredHeight(), -1f, 1);
    }

    public override void SetLayoutHorizontal() => Arrange();
    public override void SetLayoutVertical() => Arrange();

    private float CalculateRequiredHeight()
    {
        if (rectChildren.Count == 0) return 4f;
        float available = Mathf.Max(1f, rectTransform.rect.width - padding.horizontal);
        float used = 0f;
        int rows = 1;
        foreach (RectTransform child in rectChildren)
        {
            float width = Mathf.Min(available, LayoutUtility.GetPreferredWidth(child));
            if (used > 0f && used + HorizontalSpacing + width > available)
            {
                rows++;
                used = 0f;
            }
            used += (used > 0f ? HorizontalSpacing : 0f) + width;
        }
        return padding.vertical + rows * RowHeight + (rows - 1) * VerticalSpacing;
    }

    private void Arrange()
    {
        float available = Mathf.Max(1f, rectTransform.rect.width - padding.horizontal);
        float x = padding.left;
        float y = padding.top;
        foreach (RectTransform child in rectChildren)
        {
            float width = Mathf.Min(available, LayoutUtility.GetPreferredWidth(child));
            if (x > padding.left && x + width > padding.left + available)
            {
                x = padding.left;
                y += RowHeight + VerticalSpacing;
            }
            SetChildAlongAxis(child, 0, x, width);
            SetChildAlongAxis(child, 1, y, RowHeight);
            x += width + HorizontalSpacing;
        }
    }
}
