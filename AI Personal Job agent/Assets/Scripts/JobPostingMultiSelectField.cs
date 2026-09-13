using System;
using System.Collections.Generic;
using JobCheck.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 由 Prefab 內既有 Toggle 組成的多選欄位。顯示中文名稱，對外只回傳穩定代碼。
/// </summary>
public sealed class JobPostingMultiSelectField : MonoBehaviour
{
    private readonly Dictionary<string, Toggle> togglesByValue =
        new Dictionary<string, Toggle>(StringComparer.OrdinalIgnoreCase);

    public void Configure(IReadOnlyList<JobPostingLabelOption> options)
    {
        togglesByValue.Clear();
        if (options == null)
        {
            return;
        }

        Toggle[] toggles = GetComponentsInChildren<Toggle>(true);
        foreach (Toggle toggle in toggles)
        {
            string value = GetValue(toggle);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            togglesByValue[value] = toggle;
            foreach (JobPostingLabelOption option in options)
            {
                if (!string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SetLabel(toggle, option.DisplayName);

                break;
            }
        }
    }

    public void SetSelectedValues(IEnumerable<string> values)
    {
        EnsureToggleCache();
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (values != null)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    selected.Add(value.Trim());
                }
            }
        }

        foreach (KeyValuePair<string, Toggle> item in togglesByValue)
        {
            item.Value.SetIsOnWithoutNotify(selected.Contains(item.Key));
        }
    }

    public List<string> GetSelectedValues()
    {
        EnsureToggleCache();
        var values = new List<string>();
        foreach (KeyValuePair<string, Toggle> item in togglesByValue)
        {
            if (item.Value != null && item.Value.isOn)
            {
                values.Add(item.Key);
            }
        }

        return values;
    }

    private void EnsureToggleCache()
    {
        if (togglesByValue.Count > 0)
        {
            return;
        }

        Toggle[] toggles = GetComponentsInChildren<Toggle>(true);
        foreach (Toggle toggle in toggles)
        {
            string value = GetValue(toggle);
            if (!string.IsNullOrWhiteSpace(value))
            {
                togglesByValue[value] = toggle;
            }
        }
    }

    private static string GetValue(Toggle toggle)
    {
        const string prefix = "Option_";
        return toggle != null && toggle.name.StartsWith(prefix, StringComparison.Ordinal)
            ? toggle.name.Substring(prefix.Length)
            : null;
    }

    private static void SetLabel(Toggle toggle, string displayName)
    {
        TMP_Text tmpLabel = toggle.GetComponentInChildren<TMP_Text>(true);
        if (tmpLabel != null)
        {
            tmpLabel.text = displayName;
        }

        // 篩選面板的舊 Toggle 範本仍使用 Unity UI Text；兩者都支援，
        // 避免複製範本後所有選項都保留成「只看逾期」。
        UnityEngine.UI.Text legacyLabel = toggle.GetComponentInChildren<UnityEngine.UI.Text>(true);
        if (legacyLabel != null)
        {
            legacyLabel.text = displayName;
        }
    }
}
