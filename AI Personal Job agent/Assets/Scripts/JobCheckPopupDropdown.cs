using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Positions a TMP dropdown template above its field and outside scroll-view masks.
/// The template's parent is assigned by the page that creates the dropdown.
/// </summary>
public sealed class JobCheckPopupDropdown : TMP_Dropdown
{
    public override void OnPointerClick(PointerEventData eventData)
    {
        PositionTemplate();
        base.OnPointerClick(eventData);
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        PositionTemplate();
        base.OnSubmit(eventData);
    }

    public new void Show()
    {
        PositionTemplate();
        base.Show();
    }

    private void PositionTemplate()
    {
        if (template == null) return;

        RectTransform source = (RectTransform)transform;
        template.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, source.rect.width);
        // TMP's generated list is offset from the template pivot by its first
        // item. Leave one field-height of clearance so the final option does not
        // end up behind the source field or the fixed editor footer.
        template.position = source.TransformPoint(
            new Vector3(0f, source.rect.yMax + source.rect.height + 4f, 0f));
    }
}
