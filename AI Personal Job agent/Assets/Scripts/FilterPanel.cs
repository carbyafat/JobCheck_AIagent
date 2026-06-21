using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 職缺篩選面板。負責讀取 UI 條件並呼叫 AllJobPage 套用篩選。
/// </summary>
public class FilterPanel : MonoBehaviour
{
    [Header("Money")]
    [Tooltip("薪水下限輸入框。")]
    [SerializeField] private TMP_InputField inputFieldSalary;
    [Tooltip("清除薪水下限按鈕。")]
    [SerializeField] private Button buttonClearMoney;

    [Header("Status")]
    [Tooltip("狀態單選下拉選單。")]
    [SerializeField] private TMP_Dropdown dropdownStatus;

    [Header("Expired")]
    [Tooltip("只看逾期 Toggle。")]
    [SerializeField] private Toggle toggleExpiredDay;

    [Header("Score")]
    [Tooltip("適配度分數下限輸入框。")]
    [SerializeField] private TMP_InputField inputFieldScore;
    [Tooltip("清除適配度分數按鈕。")]
    [SerializeField] private Button buttonClearScore;

    [Header("Actions")]
    [Tooltip("套用篩選按鈕。")]
    [SerializeField] private Button buttonApplyFilter;
    [Tooltip("清除所有篩選條件按鈕。")]
    [SerializeField] private Button buttonClearAll;

    private readonly List<string> statusValues = new List<string>();
    private AllJobPage ownerPage;

    private void Awake()
    {
        AutoBindReferences();
        SetupStatusDropdown();
        BindButtons();
    }

    /// <summary>
    /// 指定總攬頁控制器。
    /// </summary>
    /// <param name="owner">總攬頁控制器。</param>
    public void SetOwner(AllJobPage owner)
    {
        ownerPage = owner;
    }

    /// <summary>
    /// 根據目前條件刷新 UI。
    /// </summary>
    /// <param name="condition">目前篩選條件。</param>
    public void RefreshUI(JobFilterCondition condition)
    {
        if (condition == null)
        {
            condition = new JobFilterCondition();
        }

        if (inputFieldSalary != null)
        {
            inputFieldSalary.text = condition.salaryMin >= 0 ? condition.salaryMin.ToString() : string.Empty;
        }

        if (dropdownStatus != null)
        {
            int index = statusValues.IndexOf(condition.status);
            dropdownStatus.value = index >= 0 ? index : 0;
            dropdownStatus.RefreshShownValue();
        }

        if (toggleExpiredDay != null)
        {
            toggleExpiredDay.isOn = condition.expiredOnly;
        }

        if (inputFieldScore != null)
        {
            inputFieldScore.text = condition.fitScoreMin >= 0 ? condition.fitScoreMin.ToString() : string.Empty;
        }
    }

    /// <summary>
    /// 套用篩選條件並關閉面板。
    /// </summary>
    public void ApplyFilter()
    {
        if (ownerPage == null)
        {
            ownerPage = FindObjectOfType<AllJobPage>(true);
        }

        if (ownerPage == null)
        {
            Debug.LogWarning("AllJobPage not found for filter.");
            return;
        }

        ownerPage.ApplyFilter(BuildConditionFromUI());
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 清除所有篩選條件並顯示全部職缺。
    /// </summary>
    public void ClearAll()
    {
        RefreshUI(new JobFilterCondition());

        if (ownerPage == null)
        {
            ownerPage = FindObjectOfType<AllJobPage>(true);
        }

        if (ownerPage != null)
        {
            ownerPage.ClearFilter();
        }
    }

    /// <summary>
    /// 清除薪水下限輸入。
    /// </summary>
    public void ClearMoney()
    {
        if (inputFieldSalary != null)
        {
            inputFieldSalary.text = string.Empty;
        }
    }

    /// <summary>
    /// 清除適配度分數下限輸入。
    /// </summary>
    public void ClearScore()
    {
        if (inputFieldScore != null)
        {
            inputFieldScore.text = string.Empty;
        }
    }

    /// <summary>
    /// 從目前 UI 欄位建立篩選條件。
    /// </summary>
    /// <returns>篩選條件。</returns>
    private JobFilterCondition BuildConditionFromUI()
    {
        JobFilterCondition condition = new JobFilterCondition();
        condition.salaryMin = ParseOptionalInt(inputFieldSalary != null ? inputFieldSalary.text : string.Empty);
        condition.fitScoreMin = ParseOptionalInt(inputFieldScore != null ? inputFieldScore.text : string.Empty);
        condition.expiredOnly = toggleExpiredDay != null && toggleExpiredDay.isOn;

        if (dropdownStatus != null && dropdownStatus.value >= 0 && dropdownStatus.value < statusValues.Count)
        {
            condition.status = statusValues[dropdownStatus.value];
        }

        return condition;
    }

    /// <summary>
    /// 將可空白的整數欄位轉成 int。空白或無效時回傳 -1。
    /// </summary>
    /// <param name="value">輸入文字。</param>
    /// <returns>整數值或 -1。</returns>
    private int ParseOptionalInt(string value)
    {
        int result;
        return int.TryParse(value, out result) ? result : -1;
    }

    /// <summary>
    /// 自動綁定 UI 參考。
    /// </summary>
    private void AutoBindReferences()
    {
        if (inputFieldSalary == null) inputFieldSalary = FindChildInputField("InputField_Salary");
        if (buttonClearMoney == null) buttonClearMoney = FindChildButton("Button_ClearMoney");
        if (dropdownStatus == null) dropdownStatus = FindChildDropdown("Dropdown_Status");
        if (toggleExpiredDay == null) toggleExpiredDay = FindChildToggle("Toggle_ExpiredDay");
        if (inputFieldScore == null) inputFieldScore = FindChildInputField("InputField_Score");
        if (buttonClearScore == null) buttonClearScore = FindChildButton("Button_ClearScore");
        if (buttonApplyFilter == null) buttonApplyFilter = FindChildButton("Button_AppliedFilter");
        if (buttonClearAll == null) buttonClearAll = FindChildButton("Button_ClearAll");
    }

    /// <summary>
    /// 設定狀態下拉選單選項。
    /// </summary>
    private void SetupStatusDropdown()
    {
        statusValues.Clear();
        statusValues.Add("all");
        statusValues.Add("not_viewed");
        statusValues.Add("interested");
        statusValues.Add("not_applying");
        statusValues.Add("applied");
        statusValues.Add("interview_scheduled");
        statusValues.Add("interviewing");
        statusValues.Add("waiting_reply");
        statusValues.Add("offer");
        statusValues.Add("rejected");
        statusValues.Add("archived");
        statusValues.Add("archived_wait_other_job_result");

        if (dropdownStatus == null)
        {
            return;
        }

        dropdownStatus.ClearOptions();
        dropdownStatus.AddOptions(new List<string>
        {
            "全部",
            "未檢視",
            "有興趣",
            "確認不投",
            "已投遞",
            "已預約面試",
            "已面試",
            "等回覆",
            "錄取",
            "未錄取",
            "封存",
            "已封存，等待其他面試結果"
        });
    }

    /// <summary>
    /// 綁定篩選面板按鈕事件。
    /// </summary>
    private void BindButtons()
    {
        BindButton(buttonApplyFilter, ApplyFilter);
        BindButton(buttonClearAll, ClearAll);
        BindButton(buttonClearMoney, ClearMoney);
        BindButton(buttonClearScore, ClearScore);
    }

    /// <summary>
    /// 綁定單一按鈕事件。
    /// </summary>
    /// <param name="button">目標按鈕。</param>
    /// <param name="action">點擊事件。</param>
    private void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private TMP_InputField FindChildInputField(string childName)
    {
        TMP_InputField[] fields = GetComponentsInChildren<TMP_InputField>(true);
        foreach (TMP_InputField field in fields)
        {
            if (field.name == childName)
            {
                return field;
            }
        }

        return null;
    }

    private TMP_Dropdown FindChildDropdown(string childName)
    {
        TMP_Dropdown[] dropdowns = GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            if (dropdown.name == childName)
            {
                return dropdown;
            }
        }

        return null;
    }

    private Toggle FindChildToggle(string childName)
    {
        Toggle[] toggles = GetComponentsInChildren<Toggle>(true);
        foreach (Toggle toggle in toggles)
        {
            if (toggle.name == childName)
            {
                return toggle;
            }
        }

        return null;
    }

    private Button FindChildButton(string childName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.name == childName)
            {
                return button;
            }
        }

        return null;
    }
}

/// <summary>
/// 職缺列表篩選條件。
/// </summary>
[Serializable]
public class JobFilterCondition
{
    public int salaryMin = -1;
    public string status = "all";
    public bool expiredOnly = false;
    public int fitScoreMin = -1;

    /// <summary>
    /// 複製一份篩選條件。
    /// </summary>
    /// <returns>新的篩選條件。</returns>
    public JobFilterCondition Clone()
    {
        return new JobFilterCondition
        {
            salaryMin = salaryMin,
            status = status,
            expiredOnly = expiredOnly,
            fitScoreMin = fitScoreMin
        };
    }
}
