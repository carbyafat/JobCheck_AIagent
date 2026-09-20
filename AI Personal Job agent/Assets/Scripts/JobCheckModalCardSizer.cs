using UnityEngine;

/// <summary>依父層可用空間限制 JobCheck 彈窗卡片尺寸。</summary>
[ExecuteAlways]
public sealed class JobCheckModalCardSizer : MonoBehaviour
{
    [SerializeField, Min(1f)] private float preferredWidth = 1400f;
    [SerializeField, Min(1f)] private float preferredHeight = 740f;
    [SerializeField, Min(1f)] private float maxWidth = 1400f;
    [SerializeField, Min(1f)] private float maxHeight = 900f;
    [SerializeField, Range(0.5f, 1f)] private float viewportRatio = 0.85f;

    public void Configure(
        float width,
        float height,
        float maximumWidth,
        float maximumHeight,
        float ratio)
    {
        preferredWidth = Mathf.Max(1f, width);
        preferredHeight = Mathf.Max(1f, height);
        maxWidth = Mathf.Max(1f, maximumWidth);
        maxHeight = Mathf.Max(1f, maximumHeight);
        viewportRatio = Mathf.Clamp(ratio, 0.5f, 1f);
        Fit();
    }

    private void OnEnable()
    {
        Fit();
    }

    private void OnRectTransformDimensionsChange()
    {
        Fit();
    }

    public void Fit()
    {
        RectTransform rect = transform as RectTransform;
        RectTransform parent = rect != null ? rect.parent as RectTransform : null;
        if (rect == null || parent == null)
        {
            return;
        }

        Vector2 available = parent.rect.size;
        if (available.x <= 0f || available.y <= 0f)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(
            Mathf.Min(preferredWidth, maxWidth, available.x * viewportRatio),
            Mathf.Min(preferredHeight, maxHeight, available.y * viewportRatio));
    }
}
