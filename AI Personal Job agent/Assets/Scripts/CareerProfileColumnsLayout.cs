using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 履歷顯示頁使用的雙欄版面。寬度依比例分配，高度取兩欄內容的較大值，
/// 讓外層 ScrollRect 能正確容納長履歷內容。
/// </summary>
public sealed class CareerProfileColumnsLayout : LayoutGroup
{
    [SerializeField, Range(0.3f, 0.7f)] private float leftRatio = 0.4f;
    [SerializeField, Min(0f)] private float spacing = 16f;

    public void Configure(float ratio, float columnSpacing)
    {
        leftRatio = Mathf.Clamp(ratio, 0.3f, 0.7f);
        spacing = Mathf.Max(0f, columnSpacing);
        SetDirty();
    }

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        float minimum = padding.horizontal + spacing;
        for (int i = 0; i < rectChildren.Count; i++)
        {
            minimum += LayoutUtility.GetMinWidth(rectChildren[i]);
        }

        SetLayoutInputForAxis(minimum, minimum, 1f, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        float height = 0f;
        for (int i = 0; i < rectChildren.Count; i++)
        {
            height = Mathf.Max(height, LayoutUtility.GetPreferredHeight(rectChildren[i]));
        }

        height += padding.vertical;
        SetLayoutInputForAxis(height, height, 0f, 1);
    }

    public override void SetLayoutHorizontal()
    {
        if (rectChildren.Count == 0)
        {
            return;
        }

        float available = Mathf.Max(0f, rectTransform.rect.width - padding.horizontal
            - spacing * Mathf.Max(0, rectChildren.Count - 1));
        float leftWidth = rectChildren.Count == 1 ? available : available * leftRatio;
        float rightWidth = rectChildren.Count == 1 ? 0f : available - leftWidth;

        SetChildAlongAxis(rectChildren[0], 0, padding.left, leftWidth);
        if (rectChildren.Count > 1)
        {
            SetChildAlongAxis(rectChildren[1], 0, padding.left + leftWidth + spacing, rightWidth);
        }
    }

    public override void SetLayoutVertical()
    {
        for (int i = 0; i < rectChildren.Count; i++)
        {
            float height = LayoutUtility.GetPreferredHeight(rectChildren[i]);
            SetChildAlongAxis(rectChildren[i], 1, padding.top, height);
        }
    }
}
