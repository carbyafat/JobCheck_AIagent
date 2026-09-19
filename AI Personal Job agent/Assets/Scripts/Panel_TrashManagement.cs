using System;
using System.Collections.Generic;
using System.Linq;
using JobCheck.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>個人資料垃圾桶：列出、還原及永久清除已刪除職缺。</summary>
public sealed class Panel_TrashManagement : MonoBehaviour
{
    [SerializeField] private TMP_Text textMessage;
    [SerializeField] private TMP_Dropdown dropdownEntries;
    [SerializeField] private Button buttonRestore;
    [SerializeField] private Button buttonDelete;
    [SerializeField] private Button buttonEmpty;
    [SerializeField] private Button buttonClose;

    private readonly List<JobPostingTrashEntry> entries = new List<JobPostingTrashEntry>();
    private string dataRoot;
    private Action onDataChanged;
    private ConfirmAction pendingConfirmation;

    private enum ConfirmAction { None, DeleteOne, EmptyAll }

    private void Awake()
    {
        Bind(buttonClose, Close);
        Bind(buttonRestore, RestoreSelected);
        Bind(buttonDelete, DeleteSelected);
        Bind(buttonEmpty, EmptyTrash);
        if (dropdownEntries != null)
        {
            dropdownEntries.onValueChanged.RemoveListener(ShowSelection);
            dropdownEntries.onValueChanged.AddListener(ShowSelection);
        }
    }

    public void Open(string personalDataRoot, bool personalProfileActive, Action changed)
    {
        dataRoot = personalDataRoot;
        onDataChanged = changed;
        gameObject.SetActive(true);
        if (!personalProfileActive)
        {
            entries.Clear();
            SetOptions(Array.Empty<string>());
            SetMessage("垃圾桶只屬於個人資料區；請先切換到「個人」。");
            SetActions(false);
            return;
        }
        Refresh();
    }

    public void Close()
    {
        ResetConfirmation();
        gameObject.SetActive(false);
    }

    private void Refresh()
    {
        ResetConfirmation();
        entries.Clear();
        PersistenceStorageResult<JobPostingTrashCatalog> result =
            JobPostingTrashService.List(dataRoot);
        if (!result.IsSuccess)
        {
            SetOptions(Array.Empty<string>());
            SetMessage("垃圾桶讀取失敗：\n" + FirstIssue(result.Issues));
            SetActions(false);
            return;
        }
        entries.AddRange(result.Value.Entries);
        SetOptions(entries.Select(EntryCaption));
        SetActions(entries.Count > 0);
        if (entries.Count == 0) SetMessage("垃圾桶目前是空的。");
        else ShowSelection(0);
    }

    private void ShowSelection(int index)
    {
        ResetConfirmation();
        JobPostingTrashEntry entry = Selected(index);
        if (entry == null) return;
        SetMessage("已刪除職缺：" + entry.Title + "\n"
            + "刪除時間：" + entry.DeletedAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm:ss") + "\n"
            + "相關應徵／事件會一起處理；應徵數：" + entry.ApplicationCount + "\n"
            + "還原遇到相同 ID 時會停止，不覆蓋現有資料。垃圾桶不隨匯出檔搬運。");
    }

    private void RestoreSelected()
    {
        ResetConfirmation();
        JobPostingTrashEntry entry = Selected();
        if (entry == null) return;
        PersistenceStorageResult<JobPostingTrashSummary> result =
            JobPostingTrashService.Restore(dataRoot, entry.DirectoryPath);
        if (!result.IsSuccess)
        {
            SetMessage("還原失敗，原有資料未覆蓋：\n" + FirstIssue(result.Issues));
            return;
        }
        onDataChanged?.Invoke();
        Refresh();
    }

    private void DeleteSelected()
    {
        JobPostingTrashEntry entry = Selected();
        if (entry == null) return;
        if (pendingConfirmation != ConfirmAction.DeleteOne)
        {
            pendingConfirmation = ConfirmAction.DeleteOne;
            SetButtonText(buttonDelete, "再次按下：永久刪除");
            SetButtonText(buttonEmpty, "清空垃圾桶");
            SetMessage("即將永久刪除「" + entry.Title + "」。此操作無法復原；請再次按下永久刪除。");
            return;
        }
        PersistenceStorageResult<JobPostingTrashDeleteSummary> result =
            JobPostingTrashService.DeletePermanently(dataRoot, entry.DirectoryPath);
        if (!result.IsSuccess)
        {
            SetMessage("永久刪除失敗：\n" + FirstIssue(result.Issues));
            ResetConfirmation();
            return;
        }
        Refresh();
    }

    private void EmptyTrash()
    {
        if (entries.Count == 0) return;
        if (pendingConfirmation != ConfirmAction.EmptyAll)
        {
            pendingConfirmation = ConfirmAction.EmptyAll;
            SetButtonText(buttonEmpty, "再次按下：全部永久刪除");
            SetButtonText(buttonDelete, "永久刪除所選");
            SetMessage("即將永久刪除垃圾桶中的 " + entries.Count
                + " 筆職缺。此操作無法復原；請再次按下清空垃圾桶。");
            return;
        }
        PersistenceStorageResult<JobPostingTrashDeleteSummary> result =
            JobPostingTrashService.Empty(dataRoot);
        if (!result.IsSuccess)
        {
            SetMessage("清空未完成：\n" + FirstIssue(result.Issues));
            ResetConfirmation();
            return;
        }
        Refresh();
    }

    private JobPostingTrashEntry Selected(int? index = null)
    {
        int value = index ?? (dropdownEntries != null ? dropdownEntries.value : -1);
        return value >= 0 && value < entries.Count ? entries[value] : null;
    }

    private static string EntryCaption(JobPostingTrashEntry entry) =>
        entry.DeletedAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm") + "　" + entry.Title;

    private void SetOptions(IEnumerable<string> captions)
    {
        if (dropdownEntries == null) return;
        dropdownEntries.ClearOptions();
        dropdownEntries.AddOptions(captions.ToList());
        dropdownEntries.value = 0;
        dropdownEntries.RefreshShownValue();
    }

    private void SetActions(bool enabled)
    {
        if (buttonRestore != null) buttonRestore.interactable = enabled;
        if (buttonDelete != null) buttonDelete.interactable = enabled;
        if (buttonEmpty != null) buttonEmpty.interactable = enabled;
    }

    private void ResetConfirmation()
    {
        pendingConfirmation = ConfirmAction.None;
        SetButtonText(buttonDelete, "永久刪除所選");
        SetButtonText(buttonEmpty, "清空垃圾桶");
    }

    private void SetMessage(string message)
    {
        if (textMessage != null) textMessage.text = message;
    }

    private static string FirstIssue(IReadOnlyList<PersistenceStorageIssue> issues) =>
        issues != null && issues.Count > 0 ? issues[0].Message : "未知錯誤";

    private static void SetButtonText(Button button, string text)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = text;
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
