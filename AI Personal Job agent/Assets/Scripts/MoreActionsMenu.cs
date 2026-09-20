using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制職缺頁的低頻功能選單。選單預設收合，並在執行功能、按下 Esc、
/// 點擊選單外部或離開頁面時關閉。
/// </summary>
public sealed class MoreActionsMenu : MonoBehaviour
{
    [SerializeField] private Button buttonMoreActions;
    [SerializeField] private GameObject panelMoreActions;
    [SerializeField] private Button[] menuActionButtons;

    private bool isOpen;

    private void Awake()
    {
        BindButtons();
        SetOpen(false);
    }

    private void OnEnable()
    {
        BindButtons();
        SetOpen(false);
    }

    private void OnDisable()
    {
        SetOpen(false);
    }

    private void Update()
    {
        if (!isOpen)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetOpen(false);
            return;
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (!ContainsPointer(panelMoreActions != null ? panelMoreActions.transform as RectTransform : null) &&
            !ContainsPointer(buttonMoreActions != null ? buttonMoreActions.transform as RectTransform : null))
        {
            SetOpen(false);
        }
    }

    public void Toggle()
    {
        SetOpen(!isOpen);
    }

    public void Close()
    {
        SetOpen(false);
    }

    private void BindButtons()
    {
        if (buttonMoreActions != null)
        {
            buttonMoreActions.onClick.RemoveListener(Toggle);
            buttonMoreActions.onClick.AddListener(Toggle);
        }

        if (menuActionButtons == null)
        {
            return;
        }

        foreach (Button button in menuActionButtons)
        {
            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveListener(Close);
            button.onClick.AddListener(Close);
        }
    }

    private void SetOpen(bool open)
    {
        isOpen = open;
        if (panelMoreActions != null && panelMoreActions.activeSelf != open)
        {
            panelMoreActions.SetActive(open);
        }
    }

    private static bool ContainsPointer(RectTransform rect)
    {
        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition);
    }
}
