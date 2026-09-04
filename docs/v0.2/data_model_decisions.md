# JobCheck Unity V0.2 共通資料規則

狀態：第一版定案  
適用範圍：JobCheck Unity V0.2 資料模型、儲存、migration、統計與後續 UI 介接  
不適用範圍：帳號、雲端同步、多人協作、自動投遞及未確認的 AI 自動決策

## 1. 核心原則

1. 職缺資料與個人求職行為分開保存。
2. ApplicationEvent 是已發生事件的歷史紀錄，不因目前狀態改變而刪除或覆寫。
3. Application.current_stage 是方便 UI 與查詢使用的快取值，必須能由事件重新計算或驗證。
4. 讀取資料不得產生檔案、補寫預設值或改變原始內容。所有寫入必須由明確的使用者操作、migration 或 import 動作觸發。
5. 外部報告與匯入資料必須標示來源，預設不得計入個人求職統計。
6. 不從舊資料推測不存在的事件。無法可靠還原的資訊保留 legacy_status 並標示 needs_review。
7. 持久化 DTO 與業務規則分離。JSON DTO 維持 Unity JsonUtility 可處理的簡單 public fields、List 與巢狀物件；enum 驗證、關聯驗證與狀態推導由 Domain／Service 層負責。

## 2. 格式與命名

- V0.2 實體檔案使用 `schema_version: "0.2"`。
- 完整備份或匯出包使用獨立的 `format_version: "0.2"`。
- JSON 欄位與 enum 值一律使用小寫 `snake_case`。
- C# 型別使用 PascalCase；public persistence fields 可與 JSON 欄位維持 snake_case，避免額外轉換層造成錯誤。
- 文字檔使用 UTF-8。
- 時間使用帶時區的 ISO 8601，例如 `2026-09-04T20:30:00+08:00`。
- C# 解析時間一律使用 `DateTimeOffset`，不得以本機時區無條件補值。
- 新版 reader 可以忽略未知的 optional 欄位，但不得忽略未知的 schema_version、required 欄位或 enum 值。

## 3. ID 規則

### 3.1 新資料

新建立的 ID 使用型別前綴加 UUID N 格式的小寫字串：

- Company：`cmp_<32 lowercase hex>`
- JobPosting：`job_<32 lowercase hex>`
- Application：`app_<32 lowercase hex>`
- ApplicationEvent：`evt_<32 lowercase hex>`

ID 建立後不可因公司名稱、職稱、平台或狀態改變而修改。

### 3.2 舊資料

- 現有 job ID 在 migration 後繼續作為 JobPosting.id 使用，避免不必要的 ID 變更。
- 若舊 ID 不符合新版格式仍可保留，但必須是非空白、在 JobPosting 集合內唯一，且不得包含路徑分隔符或 `..`。
- 新建 Company、Application 與 ApplicationEvent ID 由 migration 產生，並寫入 migration manifest 的 ID mapping。
- migration 重跑時必須重用 manifest 中的 mapping，不得產生第二組 ID。
- 不以公司名稱或職稱直接作為 ID。

### 3.3 公司合併

- 正規化後公司名稱完全相同時視為同一 Company，即使資料來自不同平台也一樣。
- 正規化僅包含前後空白移除、連續空白合併與 Unicode 正規化。
- 不進行模糊比對或別名自動合併；疑似重複公司列入 migration report，交由人工確認。

## 4. 時間與事件排序

每個 ApplicationEvent 至少包含：

- `occurred_at`：事件實際發生時間。
- `recorded_at`：事件被輸入系統的時間。

排序依序使用：

1. occurred_at
2. recorded_at
3. event ID 的 ordinal 字串順序

規則：

- recorded_at 不可早於資料首次建立時間。
- 使用者可以補登較早發生的事件，因此 occurred_at 可以早於 recorded_at。
- 如果事件順序造成不可能的狀態轉換，資料不得被靜默修正，應回報 validation error 或 needs_review。
- 只有日期、沒有時間的舊資料，migration 統一使用該日期當地時間 `12:00:00`，並標示 `time_precision: "date"`。
- 完全沒有可靠時間的舊狀態不得捏造事件時間，改以 system event 的 migration timestamp 保存快照。

## 5. 核心資料物件

### 5.1 Company

必要欄位：

- id
- schema_version
- name

可選欄位：

- industry
- notes
- risk_flags
- created_at
- updated_at

Company 只保存公司層級資訊。特定職缺才有的薪資、工時或職務疑慮不得放入 Company。

### 5.2 JobPosting

必要欄位：

- id
- schema_version
- company_id
- title
- source
- captured_at

可選欄位：

- department
- category
- compensation
- location
- work_conditions
- responsibilities
- requirements
- benefits
- recruitment_process
- tags
- risk_flags
- raw_description
- legacy_id

JobPosting 是職缺本身，不保存個人的投遞狀態、收藏、結案原因或 Fit 評分。

### 5.3 Application

必要欄位：

- id
- schema_version
- job_posting_id
- source_type
- current_stage
- created_at
- updated_at

可選欄位：

- candidate_close_reason
- candidate_close_reason_note
- notes
- is_favorite
- is_archived
- archive_reason
- manual_follow_up_at
- previous_application_id
- legacy_status
- needs_review
- fit_score

規則：

- 同一 JobPosting 預設只允許一筆 active 的 `my_application`。
- 若確實再次投遞同一職缺，建立新的 Application，不覆蓋舊 Application。
- 再次投遞時，新 Application.previous_application_id 連結同一 JobPosting 最近一筆既有 Application。
- previous_application_id 只連結前一筆，不直接保存完整歷史清單；沿連結即可回溯多次投遞，且不得形成循環。
- `candidate_close_reason` 只允許用在 `closed_by_candidate`。
- 被公司拒絕必須使用 `rejected_by_company`，不得填入 CandidateCloseReason。
- archived 是顯示與整理狀態，不等於拒絕、Offer 或自行放棄。

### 5.4 ApplicationEvent

必要欄位：

- id
- schema_version
- application_id
- event_type
- occurred_at
- recorded_at
- actor

可選欄位：

- notes
- source_reference
- legacy_status
- time_precision
- supersedes_event_id

ApplicationEvent 建立後不可直接改寫歷史內容。需要修正時新增 `data_corrected` system event，並以 `supersedes_event_id` 指向被修正事件。

## 6. Enum 定義

### 6.1 ApplicationEventType

使用者／求職流程事件：

- saved
- applied
- viewed
- contacted
- interview_scheduled
- interview_completed
- waiting_response_started
- rejected_by_company
- closed_by_candidate
- offer_received
- no_response_marked

系統事件：

- migration_snapshot
- data_corrected

系統事件不得直接計入漏斗數量。

### 6.2 ApplicationStage

- unknown
- saved
- applied
- viewed
- contacted
- interview_scheduled
- interview_completed
- waiting_response
- offer_received
- rejected_by_company
- closed_by_candidate

`unknown` 只允許出現在 migration 或損壞資料修復流程；新建 Application 不得使用 unknown。

### 6.3 EventActor

- candidate
- company
- platform
- system

### 6.4 SourceType

- my_application
- imported_application
- external_report

只有 `my_application` 預設計入個人統計。

### 6.5 CandidateCloseReason

- salary_too_low
- gambling_industry
- commute
- work_schedule
- weekend_duty
- role_mismatch
- tech_mismatch
- company_concern
- better_opportunity
- no_response
- other

使用 `other` 時必須填寫 candidate_close_reason_note。

### 6.6 ArchiveReason

- user_archived
- waiting_other_job_result
- duplicate
- imported_reference
- other

## 7. Tag、RiskFlag 與 CloseReason

- Tag 描述職缺是什麼，用於分類、搜尋與職類統計，例如 unity、csharp、web、remote、game。
- RiskFlag 描述評估時需要注意什麼，例如 weekend_duty、salary_opaque、long_commute、role_ambiguous。
- CandidateCloseReason 描述本人為何結束一筆 Application，只能在結案時使用。
- 同一字串不可同時當作 Tag 與 RiskFlag。
- V0.2 初期允許 Tag 使用受控字串清單；RiskFlag 與 CandidateCloseReason 使用固定 enum。
- 新增 enum 必須更新 schema、中文顯示 mapping、測試與文件。

## 8. Application 狀態推導

### 8.1 一般規則

- reducer 只處理同一 Application 的事件。
- reducer 先依共通排序規則排序事件，再逐筆套用。
- saved、applied、viewed、contacted、interview_scheduled、interview_completed、waiting_response_started、offer_received 依事件更新 current_stage。
- rejected_by_company 與 closed_by_candidate 是互斥的 terminal stage。
- no_response_marked 不覆蓋 current_stage；它只建立可查詢的 no-response 標記。
- migration_snapshot 可以設定 migration 當下的 current_stage，但不計入漏斗。
- data_corrected 必須指定 supersedes_event_id；統計和 reducer 使用修正後的有效事件集合。
- 求職流程不是強制線性的狀態機。contacted、interview_scheduled 等事件可以在沒有 applied 或 viewed 的情況下出現，以支援公司主動聯絡等真實情境。
- 同一類事件可以出現多次，例如多輪面試。current_stage 由最後一筆有效階段事件決定，但漏斗與統計必須以 Application 去重。

### 8.2 衝突規則

- 同一 Application 同時存在有效的 rejected_by_company 與 closed_by_candidate 時，標示 needs_review，統計暫不計入 terminal outcome。
- terminal stage 之後出現一般流程事件時，除非有 data_corrected 或明確建立新的 Application，否則標示 needs_review。
- occurred_at 倒序本身不一定錯誤，因為允許補登；但按 occurred_at 排序後仍形成不可能流程時必須回報。
- current_stage 與 reducer 結果不一致時，以 reducer 結果為準並回報 cache mismatch；讀取流程本身不得自動寫回檔案。

## 9. 現有狀態 migration 原則

舊 tracking 只有目前狀態，通常沒有完整事件時間。migration 不得把一個狀態展開成多個假事件。

| 舊 status | 新資料處理 |
|---|---|
| not_viewed | 若沒有 favorite、日期、備註或其他使用者行為，不建立 Application |
| not_applied | 表示尚未投遞；若沒有 favorite、日期、備註或其他使用者行為，不建立 Application，否則建立 migration_snapshot 並設為 saved |
| interested | 建立 migration_snapshot，current_stage 設 saved |
| not_applying | 表示本人主動放棄；建立 migration_snapshot，current_stage 設 closed_by_candidate，無其他原因時使用 other 並註記由舊狀態轉換 |
| applied | 建立 migration_snapshot，current_stage 設 applied |
| interview_scheduled | 建立 migration_snapshot，current_stage 設 interview_scheduled |
| interviewing | 依現有 UI 語意「已面試」映射為 interview_completed |
| waiting_reply | 建立 migration_snapshot，current_stage 設 waiting_response |
| offer | 建立 migration_snapshot，current_stage 設 offer_received |
| rejected | 建立 migration_snapshot，current_stage 設 rejected_by_company |
| closed | 目前只有 demo 假資料，確定視為本人主動停止應徵；設 closed_by_candidate，原因使用 other 並註記由舊狀態轉換 |
| archived | is_archived 設 true；current_stage 依可用資訊決定，否則 unknown |
| archived_wait_other_job_result | is_archived 設 true，archive_reason 設 waiting_other_job_result |

所有 migration_snapshot 都保存 legacy_status，且不計入漏斗事件。

## 10. Application 建立邊界

- 單純存在一筆 JobPosting 不代表存在 Application。
- 使用者執行收藏、表示有興趣、投遞或後續互動時才建立 my_application。
- 因舊版載入行為自動生成、但仍為 not_viewed 且沒有任何使用者內容的 tracking 檔，不得轉成 Application。
- external_report 不得建立 my_application；只有使用者明確選擇「加入我的求職流程」後才能另建 my_application。

## 11. Fit Score 共通契約

第一批只定義契約，不實作計算或 UI。

維度：

- technical
- salary
- work_style
- industry
- location
- interest

規則：

- 每個維度為 optional 的 0 到 100 整數。
- 缺少維度不自動當作 0 分。
- overall 使用有值維度的權重重新正規化後計算。
- overall 採四捨五入至整數，MidpointRounding.AwayFromZero。
- 所有可用維度的原始權重總和必須大於 0。
- 權重保存在 settings，不得硬編碼在 UI。
- FitScore 保存 `source`、`calculation_version` 與 `calculated_at`。
- source 至少允許 manual 與 ai_suggested；人工值優先，AI 不得靜默覆蓋人工值。

## 12. 資料目錄

V0.2 正式資料放在：

```text
data/
  companies/
  jobs/
  applications/
  imports/
  settings/
  migration/
```

- companies、jobs、applications 每個實體各一個 JSON 檔。
- ApplicationEvent 第一版內嵌於對應 Application，避免多檔案交易造成半套寫入；若日後事件量明顯增長再獨立儲存。
- imports 保存外部來源原始資料與解析結果。
- settings 保存 Fit 權重及不屬於個別實體的設定。
- migration 保存 manifest、ID mapping、validation report 與執行紀錄。
- 舊 `jobs/`、`job_tracking/`、`job_index/` 在 V0.2 migration 驗收完成前維持唯讀且不得刪除。

## 13. 讀寫安全

- read 方法不得呼叫 create 或 save。
- 找不到資料時回傳明確的 NotFound／空結果，不自動建立檔案。
- 寫入先輸出至同資料夾暫存檔，完成序列化與驗證後再原子替換正式檔案。
- 寫入前驗證 required 欄位、enum、時間與 ID 關聯。
- 單一檔案錯誤不得使整批既有資料被清空或覆蓋。
- parser／repository 回報的錯誤至少包含檔案、欄位路徑、錯誤代碼與人類可讀訊息。
- UI 不直接使用 File.ReadAllText、File.WriteAllText 或 JsonUtility；檔案 I/O 統一經 repository。

## 14. Migration 安全契約

正式 migration 必須依序執行：

1. 掃描並驗證 V0.1 輸入。
2. 建立來源檔案清單、大小與 SHA-256 基準。
3. 建立完整備份。
4. 在 staging 目錄產生 V0.2 資料。
5. 驗證筆數、ID 關聯、enum、日期與 JSON 可反序列化性。
6. 產生 migration report 與 ID mapping。
7. 全部通過後才將 staging 切換為正式 data 目錄。

規則：

- migration 預設不得修改或刪除 V0.1 檔案。
- 第一輪 migration 只處理 demo golden data，不處理真實求職資料。
- migration 必須可安全重跑，或在偵測到已成功執行時明確拒絕；不得產生重複資料。
- 任一 required 資料失敗時整批不切換，保留 report 供人工處理。
- optional 資料缺漏可以完成轉換，但必須列入 warning。
- migration 完成不代表刪除舊資料；刪除屬於另一個需人工確認的版本工作。

## 15. 第一批測試基線

第一批至少建立下列 EditMode tests：

1. 四個核心模型的最小有效資料可以建立。
2. 四個模型序列化再反序列化後資料一致。
3. 缺少 optional 欄位不崩潰。
4. 缺少 required ID 會回報錯誤。
5. 無效 enum 會回報錯誤。
6. 無效 ISO 8601 時間會回報錯誤。
7. JobPosting.company_id 必須找到 Company。
8. Application.job_posting_id 必須找到 JobPosting。
9. ApplicationEvent.application_id 必須找到 Application。
10. 重複 ID 會回報錯誤。
11. unknown stage 不可用於新建 Application。
12. external_report 不符合個人統計輸入條件。
13. no_response_marked 不改變 current_stage。
14. rejected_by_company 與 closed_by_candidate 衝突時標示 needs_review。
15. 讀取不存在的 tracking／application 不會建立檔案。
16. 正規化後同名公司即使來自不同平台也只建立一個 Company。
17. 再次投遞建立新 Application，previous_application_id 只連到最近一筆且不可形成循環。

## 16. 第一批完成定義

- 本文件中的規則已完成審閱，未決項目有明確標記。
- Unity 專案可正常編譯，Console 無新增 Error。
- Domain 與 EditMode Tests 使用獨立 asmdef，不移動現有 UI scripts。
- 第一批測試全部通過。
- 現有 jobs、job_tracking、job_index、scene 與 prefab 沒有內容變更。
- V0.1 golden input 已鎖定並可被測試讀取。
- 尚未執行正式 migration，也尚未讓 UI 改讀 V0.2。

## 17. 已確認事項

1. 正規化後同名公司一律視為同一 Company，不因平台不同而拆分。
2. 舊 `closed` 目前只存在於 demo 假資料，migration 視為本人主動停止應徵。
3. `not_applied` 代表尚未投遞；`not_applying` 代表本人主動放棄，兩者不可混用。
4. 同一職缺再次投遞時建立新的 Application，並以 previous_application_id 連結最近一次投遞。
5. 第一輪 migration 只處理 demo 資料；目前沒有需要轉換的真實資料。
