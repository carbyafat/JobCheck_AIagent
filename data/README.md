# JobCheck V0.2 Demo Data

`data/` 是 V0.2 Repository 的正式 demo dataset，全部資料皆為虛構內容，不是真實求職紀錄。

## 資料組成

- `companies/`：Company 主資料。
- `jobs/`：JobPosting 主資料，以 `company_id` 關聯公司。
- `applications/`：Application 與內嵌 ApplicationEvent。
- `migration/`：V0.1 demo 的備份、manifest 與 migration report。

## Demo 來源

### V0.1 migration 基準

- 20 筆 `demo_job_001`～`demo_job_020`。
- 20 筆正規化後的 Company。
- 1 筆由舊 tracking 轉換的 Application。

這批資料用來驗證 V0.1 → V0.2 migration、來源備份及重跑安全性。

### V0.2 UI 驗收資料

以下資料由 Unity V0.2 UI 實際新增，用來保留人工驗收情境：

| 公司 | 職缺 | 驗證重點 |
|---|---|---|
| 人生測試 | Unity程式主管、Unity基層 | 同名公司重用 Company；Unity基層另有 Saved Application |
| 絕對測試 | 絕對工程師 | 基本新增與顯示 |
| 測試公司 | Unity工程師 | 基本新增與編輯 |
| 蒼海一聲笑 | 海洋工程 | Tag 與 RiskFlag 保存及顯示 |

UUID 檔名是 Domain ID 契約的一部分，因此正式 demo 保留 UI 產生的 ID，不改成人工流水號。

## Git 規則

根目錄 `.gitignore` 會忽略日後由 UI 新增的 `cmp_*.json`、`job_*.json` 與 `app_*.json`，避免個人操作資料污染版本控制；本文件列出的正式 demo 使用明確 allowlist 持續追蹤。

若要新增正式 demo，必須同時：

1. 確認 Company、JobPosting、Application 外鍵完整。
2. 通過 Repository 與 Unity EditMode tests。
3. 將檔案加入根目錄 `.gitignore` 的正式 demo allowlist。
4. 更新本文件的驗收情境。

