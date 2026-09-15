using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 職缺詳細頁共用輸入視窗。依操作需求顯示日期單行欄位或文字多行欄位，
/// 僅負責收集輸入與呈現錯誤，不直接修改或儲存職缺資料。
/// </summary>
public class Panel_DetailInputDialog : MonoBehaviour
{
    [Header("Dialog Text")]
    [Tooltip("視窗標題。")]
    [SerializeField] private TMP_Text textTitle;
    [Tooltip("本次輸入用途與格式說明。")]
    [SerializeField] private TMP_Text textDescription;
    [Tooltip("欄位驗證失敗時顯示的訊息。")]
    [SerializeField] private TMP_Text textError;

    [Header("Input Fields")]
    [Tooltip("日期專用單行輸入框，格式為 yyyy.MM.dd。")]
    [SerializeField] private TMP_InputField inputFieldDate;
    [Tooltip("備註與原因專用的多行輸入框。")]
    [SerializeField] private TMP_InputField inputFieldText;

    [Header("Actions")]
    [Tooltip("確認目前輸入。實際資料寫入由 Panel_JobDetail 處理。")]
    [SerializeField] private Button buttonConfirm;
    [Tooltip("取消輸入且不寫入資料。")]
    [SerializeField] private Button buttonCancel;

    private TMP_InputField activeInputField;

    public Button ConfirmButton
    {
        get
        {
            Initialize();
            return buttonConfirm;
        }
    }

    public Button CancelButton
    {
        get
        {
            Initialize();
            return buttonCancel;
        }
    }

    /// <summary>
    /// 目前啟用欄位的文字。
    /// </summary>
    public string Value => activeInputField != null ? activeInputField.text : string.Empty;

    private void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// 補齊 Prefab 引用並固定兩種欄位的輸入行為。
    /// </summary>
    public void Initialize()
    {
        AutoBindReferences();

        if (inputFieldDate != null)
        {
            inputFieldDate.lineType = TMP_InputField.LineType.SingleLine;
            inputFieldDate.characterLimit = 10;
        }

        if (inputFieldText != null)
        {
            inputFieldText.lineType = TMP_InputField.LineType.MultiLineNewline;
            inputFieldText.characterLimit = 0;
        }
    }

    /// <summary>
    /// 開啟日期輸入模式。
    /// </summary>
    public void ShowDate(string title, string description, string initialValue, string confirmLabel)
    {
        Show(title, description, initialValue, confirmLabel, inputFieldDate);
    }

    /// <summary>
    /// 開啟多行文字輸入模式。
    /// </summary>
    public void ShowText(string title, string description, string initialValue, string confirmLabel)
    {
        Show(title, description, initialValue, confirmLabel, inputFieldText);
    }

    /// <summary>
    /// 顯示欄位驗證訊息，視窗保持開啟以便修正。
    /// </summary>
    public void SetError(string message)
    {
        Initialize();
        if (textError != null)
        {
            textError.text = message ?? string.Empty;
            textError.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
    }

    /// <summary>
    /// 控制確認與取消按鈕是否可操作。
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        Initialize();
        if (buttonConfirm != null) buttonConfirm.interactable = interactable;
        if (buttonCancel != null) buttonCancel.interactable = interactable;
    }

    /// <summary>
    /// 關閉視窗並清除暫存輸入，不影響持久化資料。
    /// </summary>
    public void Hide()
    {
        if (inputFieldDate != null) inputFieldDate.text = string.Empty;
        if (inputFieldText != null) inputFieldText.text = string.Empty;
        activeInputField = null;
        SetError(string.Empty);
        gameObject.SetActive(false);
    }

    private void Show(
        string title,
        string description,
        string initialValue,
        string confirmLabel,
        TMP_InputField targetInputField)
    {
        Initialize();
        gameObject.SetActive(true);
        SetText(textTitle, title);
        SetText(textDescription, description);
        SetError(string.Empty);

        if (inputFieldDate != null)
        {
            inputFieldDate.gameObject.SetActive(targetInputField == inputFieldDate);
        }

        if (inputFieldText != null)
        {
            inputFieldText.gameObject.SetActive(targetInputField == inputFieldText);
        }

        SetButtonLabel(buttonConfirm, confirmLabel);
        activeInputField = targetInputField;
        if (activeInputField != null)
        {
            activeInputField.text = initialValue ?? string.Empty;
            activeInputField.ActivateInputField();
            activeInputField.Select();
        }
    }

    private void AutoBindReferences()
    {
        if (textTitle == null) textTitle = FindChildText("TMP_DialogTitle");
        if (textDescription == null) textDescription = FindChildText("TMP_DialogDescription");
        if (textError == null) textError = FindChildText("TMP_DialogError");
        if (inputFieldDate == null) inputFieldDate = FindChildInputField("InputField_Date");
        if (inputFieldText == null) inputFieldText = FindChildInputField("InputField_Text");
        if (buttonConfirm == null) buttonConfirm = FindChildButton("Button_DialogConfirm");
        if (buttonCancel == null) buttonCancel = FindChildButton("Button_DialogCancel");
    }

    private TMP_Text FindChildText(string childName)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text.name == childName) return text;
        }

        return null;
    }

    private TMP_InputField FindChildInputField(string childName)
    {
        TMP_InputField[] inputFields = GetComponentsInChildren<TMP_InputField>(true);
        foreach (TMP_InputField inputField in inputFields)
        {
            if (inputField.name == childName) return inputField;
        }

        return null;
    }

    private Button FindChildButton(string childName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.name == childName) return button;
        }

        return null;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value ?? string.Empty;
    }

    private static void SetButtonLabel(Button button, string value)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = value ?? string.Empty;
    }
}
