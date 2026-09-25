# JobCheck 面試 Demo 功能與腳本速查

> 用途：面試前快速回想「畫面上的功能由哪裡實作、資料怎麼流、為什麼這樣設計」。
> 專案：Unity 2022.3.62f3 LTS，主要場景為 `Assets/Scenes/SampleScene.unity`。
> 注意：本文依目前工作分支整理；「求職條件」仍是開發中功能。

## 先記住這張架構圖

```text
Unity 畫面（Assets/Scripts）
    ↓ 收集輸入、呈現結果
Command / Query（Assets/JobCheck/Persistence）
    ↓ 寫入命令或唯讀查詢
Domain + Validator + Reducer（Assets/JobCheck/Runtime）
    ↓ 商業規則與狀態推導
Mapper + DTO + Repository（Assets/JobCheck/Persistence）
    ↓ JSON 轉換、驗證、原子寫入
data/ 或 personal_data/
```

面試時可用一句話說明：**UI 不直接決定資料規則；Command／Query 協調流程，Domain 負責規則，Repository 負責檔案邊界。**

## 功能 → 腳本對照

### 1. 應用程式導覽與視覺

| 功能 | 入口／主要腳本 | 相關腳本 | 要記得的設計 |
|---|---|---|---|
| 首頁、職缺、履歷、求職條件切頁 | `Assets/Scripts/AppPageNavigator.cs` | `AppShellThemePresenter.cs` | Navigator 只切主頁，不介入頁內流程。 |
| 共用色彩、字體、元件尺寸 | `Assets/Scripts/JobCheckUiTheme.cs` | `Assets/JobCheckUiTheme.asset`、`JobsPageThemePresenter.cs` | 視覺設定集中在 ScriptableObject，避免每個畫面各自硬編碼。 |
| Popup／Modal 尺寸與下拉選單 | `JobCheckPopupDropdown.cs` | `JobCheckModalCardSizer.cs` | UI 輔助元件，不承擔 Domain 規則。 |

### 2. 首頁摘要

| 功能 | 入口／主要腳本 | 相關腳本 | 要記得的設計 |
|---|---|---|---|
| 職缺數、進行中、待檢視、未回覆、履歷完成度 | `Assets/Scripts/HomeDashboardPage.cs` | `Persistence/Queries/HomeDashboardSnapshot.cs`、`ApplicationAnalyticsQuery.cs`、`CareerProfileRepository.cs` | 首頁是唯讀；求職數字重用 Analytics Query，避免兩套統計口徑。 |
| 快速新增職缺、開啟列表／分析／履歷 | `HomeDashboardPage.cs` | `AppPageNavigator.cs`、`AllJobPage.cs` | 首頁只導向既有流程，不自行複製功能。 |

### 3. 職缺列表、篩選與資料區切換

| 功能 | 入口／主要腳本 | 相關腳本 | 要記得的設計 |
|---|---|---|---|
| 載入、分頁、排序、列表顯示 | `Assets/Scripts/AllJobPage.cs` | `Panel_SingleJob.cs`、`JobCheckV02DisplayAdapter.cs`、`Persistence/Queries/JobPostingReadOnlyQuery.cs` | `AllJobPage` 是目前 UI 協調中心；Adapter 把 Domain 查詢結果轉成舊 UI 顯示模型。 |
| 條件篩選 | `Assets/Scripts/FilterPanel.cs` | `AllJobPage.cs` | FilterPanel 只收集條件，實際套用由 AllJobPage 執行。 |
| Demo／個人資料切換 | `AllJobPage.cs` | `JobCheckDataRepository.cs` | `data/` 是版控內虛構資料；`personal_data/` 被 `.gitignore` 排除。個人資料區不存在時才建立空結構。 |

### 4. 新增與編輯職缺

| 功能 | 入口／主要腳本 | 相關腳本 | 要記得的設計 |
|---|---|---|---|
| 輸入表單與欄位整理 | `Assets/Scripts/Panel_JobPostingCreate.cs` | `JobPostingMultiSelectField.cs`、`JobPostingLabelCatalog.cs` | 表單只收集資料、顯示錯誤，不自行實作驗證規則。 |
| 建立／更新流程 | `Persistence/Commands/JobPostingCommandService.cs` | `JobPostingCreateRequest.cs`、`JobPostingValidator.cs`、`CompanyNameNormalizer.cs` | 同名公司採明確正規化；更新保留 Job ID 與既有 Application 關聯。 |
| 寫入 JSON | `Persistence/Storage/JobCheckDataRepository.cs` | `JobPostingDtoMapper.cs`、`CompanyDtoMapper.cs`、`PersistenceJsonSerializer.cs` | 正式檔案寫入前先完成驗證與序列化；使用同目錄暫存檔做原子替換。 |
| 中文錯誤 | `Persistence/Commands/ValidationErrorLocalizer.cs` | 各 Validator 的 error enum | UI 不直接顯示內部 enum 名稱。 |

### 5. 職缺詳情與應徵追蹤

| 功能 | 入口／主要腳本 | 相關腳本 | 要記得的設計 |
|---|---|---|---|
| 詳情、原始內容、狀態按鈕、事件歷程 | `Assets/Scripts/Panel_JobDetail.cs` | `AllJobPage.cs`、`Panel_DetailInputDialog.cs` | Detail 負責互動與呈現，寫入仍回到 AllJobPage → Command。 |
| 應徵狀態、收藏、備註、追蹤日 | `Persistence/Commands/ApplicationCommandService.cs` | `ApplicationValidator.cs`、`ApplicationEventValidator.cs` | 修改會記成事件；Application 保存目前快照及完整事件歷史。 |
| 由事件推導目前狀態 | `Runtime/ApplicationEvents/ApplicationStateReducer.cs` | `ApplicationStateReducerInput.cs`、`ApplicationEventType.cs`、`ApplicationStage.cs` | 狀態不是散落在 UI 的判斷；Reducer 統一處理事件順序與衝突。 |
| Application 寫入 | `Persistence/Storage/JobCheckDataRepository.cs` | `ApplicationDtoMapper.cs`、`ApplicationEventDtoMapper.cs` | 單一 Application 與其事件一起驗證並原子寫入。 |

### 6. 履歷與職缺規則式比對

| 功能 | 入口／主要腳本 | 相關腳本 | 要記得的設計 |
|---|---|---|---|
| 開啟單筆職缺的符合度 | `AllJobPage.cs`、`Panel_JobDetail.cs` | `JobRequirementMatchTextFormatter.cs` | 載入時即時計算，只顯示結果，不寫回職缺 JSON。 |
| 技能、年資、學歷、語言比對 | `Runtime/Matching/RequirementMatchEngine.cs` | `RequirementCatalog.cs`、`RequirementInputParser.cs`、`RequirementModels.cs` | 這不是生成式 AI；是可重現、可測試的本機規則。 |
| 分數計算 | `Runtime/Matching/RequirementScore.cs` | `RequirementMatchResult.cs` | 結果區分「符合、明確不符、履歷未證明、職缺條件不明」，避免把缺資料誤判為不合格。 |

### 7. 應徵分析

| 功能 | 入口／主要腳本 | 相關腳本 | 要記得的設計 |
|---|---|---|---|
| 漏斗、平台成效、結果、結束原因 | `Assets/Scripts/Panel_Analytics.cs` | `Persistence/Queries/ApplicationAnalyticsQuery.cs`、`ApplicationAnalyticsReport.cs` | 完全唯讀；依已發生事件算里程碑，並排除重複、待檢視或無效資料。 |

### 8. 個人履歷母資料

| 功能 | 入口／主要腳本 | 相關腳本 | 要記得的設計 |
|---|---|---|---|
| 顯示／編輯自介、連結、技能、經歷、專案、學歷、語言 | `Assets/Scripts/CareerProfilePage.cs` | `CareerProfileColumnsLayout.cs` | 這是個人職涯「母資料」，不等同於某次投遞用的履歷文件。 |
| 履歷模型與驗證 | `Runtime/CareerProfiles/CareerProfile.cs` | `CareerProfileValidator.cs`、`CareerProfileIdGenerator.cs` | 各區塊可暫時留白，但已有內容必須符合格式與關聯規則。 |
| 履歷儲存 | `Persistence/Storage/CareerProfileRepository.cs` | `CareerProfileDto.cs`、`CareerProfileDtoMapper.cs` | 與職缺／應徵資料分開存於 `personal_data/profile/profile.json`。 |

### 9. 求職條件（目前分支開發中）

| 功能 | 入口／主要腳本 | 相關腳本 | 要記得的設計 |
|---|---|---|---|
| 目標職務、產業、薪資、地點、工作型態、工時等 | `Assets/Scripts/JobPreferencesPage.cs` | `AppPageNavigator.cs`、`CareerProfile.cs` 內的 `JobSearchPreferences` | 求職偏好和「我具備什麼能力」分開；目前是 V0.2.8 的輸入與保存基礎。 |
| 條件 DTO 與存取 | `Persistence/Dtos/CareerProfileDto.cs` | `CareerProfileDtoMapper.cs`、`CareerProfileRepository.cs` | 暫時隨個人履歷檔保存；雙向比對規則尚未完成前，不要在 demo 宣稱已有推薦決策。 |

### 10. 匯入、匯出與垃圾桶

| 功能 | 入口／主要腳本 | 相關腳本 | 要記得的設計 |
|---|---|---|---|
| 公司／職缺／應徵資料搬運 | `Assets/Scripts/Panel_PortableTransfer.cs` | `JobCheckPortableExportService.cs`、`JobCheckPortableImportService.cs`、`JobCheckPortablePackageDto.cs` | 匯入先預覽與驗證；同步有明確邊界，不靜默覆蓋。 |
| 履歷獨立搬運 | `CareerProfilePage.cs` | `CareerProfilePortableExportService.cs`、`CareerProfilePortableImportService.cs`、`CareerProfilePortablePackageDto.cs` | 履歷包與職缺資料包刻意分開；取代前會備份原履歷。 |
| 垃圾桶、還原、永久刪除 | `Assets/Scripts/Panel_TrashManagement.cs` | `JobPostingTrashService.cs` | 刪除個人職缺時，相關 Application／事件一起移動；永久刪除是另一個明確操作。 |
| 檔案選擇器 | `Assets/Plugins/SimpleFileBrowser/` | 上述 Transfer UI | 第三方 UI 套件，不是本專案核心商業邏輯。 |

### 11. 舊資料 Migration

| 功能 | 主要腳本 | 相關資料 | 要記得的設計 |
|---|---|---|---|
| V0.1 → V0.2 | `Persistence/Migration/JobCheckV01Migration.cs` | `LegacyV01Dtos.cs`、`MigrationDtos.cs`、`data/migration/` | 保留來源備份、SHA-256、staging、manifest 與 report；不是在正式資料上直接改寫。 |

## Domain 與 Persistence 檔案怎麼看

不需要記住每個 DTO 欄位。遇到功能時依這個順序追：

1. 從 `Assets/Scripts/` 找按鈕或畫面入口。
2. 看它呼叫哪個 `Commands/` 或 `Queries/`。
3. 規則問題看 `Runtime/` 的 Model／Validator／Reducer／Matching。
4. JSON 格式看 `Dtos/`；Domain 與 DTO 的轉換看 `Mapping/`。
5. 實際檔案讀寫、安全替換與資料根目錄看 `Storage/`。
6. 預期行為不確定時，直接看 `Assets/JobCheck/Tests/` 的同名測試。

## 程式碼初步審查（面試前先處理）

以下不是只根據完成報告整理，而是實際從 UI 入口追到 Query／Command／Domain／Repository，並對照 Unity Editor log 後列出的風險。優先度以「會不會在 Demo 現場直接被看到」為主。

### P1：Demo 前應優先修正

1. **狀態篩選選項和實際狀態碼不同步。** `AllJobPage.PassesFilter()` 採字串完全相等比較，因此下列差異會直接影響結果：

   | `FilterPanel` 選項 | Query 是否可能輸出 | 結果 |
   |---|---:|---|
   | `archived`（封存） | 否 | 永遠篩不到資料；目前「封存」按鈕實際是 `favorite`。 |
   | `archived_wait_other_job_result`（已封存，等待其他面試結果） | 否 | 永遠篩不到資料；目前相對應的舊按鈕實際寫入 `contacted`。 |
   | `viewed`（公司已讀） | 是 | 已補進 FilterPanel。 |
   | `contacted`（公司已聯絡） | 是 | 已補進 FilterPanel。 |
   | `unknown`（狀態待確認） | 僅異常／未知 stage | Query 有 fallback，但 FilterPanel 沒有選項；可選擇刻意不提供。 |

   其餘 `not_viewed`、`interested`、`not_applying`、`applied`、`interview_scheduled`、`interviewing`、`waiting_reply`、`offer`、`rejected` 兩邊一致。本次已補上 `viewed`、`contacted` 及對應測試；兩個舊封存選項暫時保留，後續再確認是否移除或重新定義。

### P2：資料正確性與例外風險

2. **面試日期的模型與顯示精度不一致。** 目前沒有排序、提醒或其他商業邏輯需要精確到小時、分鐘；唯一使用時分的地方是 `JobCheckV02DisplayAdapter.BuildEventHistory()`，它將 `ScheduledFor` 顯示成 `yyyy/MM/dd HH:mm`。UI 實際只收 `yyyy.MM.dd`，並用 23:59:59 填滿時間，所以畫面會呈現使用者未輸入的 23:59。既然產品只需要日期，建議將事件歷程改為只顯示日期，並把欄位註解／命名調整成日期語意；不需要新增時間輸入。
3. **重複按「已投遞」會建立第二筆 Application。** `ApplicationCommandService.RecordEvent()` 把已存在且不在 `Saved` 階段的 `Applied` 事件解讀為再次投遞，但 UI 的按鈕沒有「再次投遞」確認或防重複。雙擊或誤按可能產生一筆新的應徵流程。應把首次投遞與重新投遞拆成不同操作。
4. **求職條件頁的序列化欄位檢查不完整。** `JobPreferencesPage.HasSerializedUi()` 只檢查少數欄位；通過後的 `LoadPreferences()` 會直接使用多個 Dropdown、InputField 與 TagEditor。日後 Prefab／Scene 漏綁其中任何一個欄位時，仍會進入載入流程並拋出 `NullReferenceException`。應完整驗證依賴，並在訊息中指出缺少的欄位。
5. **部分履歷資料錯誤會被靜默改成預設值。** `CareerProfileDtoMapper` 對無效的薪資週期、重要度與部分時間值採 fallback，而不是回報 conversion issue。匯入或手動修改 JSON 後，錯誤資料可能看似成功載入但內容已被改寫。應讓 Mapper 回報可定位的欄位錯誤，再由匯入預覽阻擋。
6. **一次狀態變更會重複完整載入資料。** Command 執行前載入、Repository 儲存前再載入，UI 完成後又重新載入列表；這些同步檔案 I/O 與反序列化都在 Unity 主執行緒。現有少量 Demo 資料影響不大，但資料增加後可能造成畫面停頓。可先量測，再考慮讓一次操作共用同一份快照或做局部刷新。

### 已確認的設計取捨／目前不處理

- **Demo 資料區允許新增、編輯與改狀態是刻意設計。** 只有刪除受到限制，因此不列為缺陷；面試時可說明 Demo 資料允許互動操作，刪除則額外保護。
- **TMP 缺字 log 目前忽略。** 警告來自未事先把完整字集加入 TMP，但現有實際使用畫面未觀察到缺字，因此不列入 Demo 修正項目；若日後新增文案再按實際畫面處理。

### 已完成的一次性 UI 程式清理

- 已從 `JobPreferencesPage.cs` 移除不再使用的整頁 Editor UI 產生器、選項常數及專用定位 helper，共減少約 411 行。
- 正式流程仍由 `SampleScene` 中已序列化的求職條件 UI 提供，因此沒有修改 Scene 結構或欄位引用。
- 執行期 Tag chip 仍需動態新增與刪除，所以保留 `RenderTags()`、`CreateButton()`、`CreateText()`、`CreateUiObject()`、`Stretch()` 與 `ApplySliced()`。
- `AllJobPage.GetDefaultExpireDays()` 仍判斷已不在目前查詢輸出中的 `archived_wait_other_job_result`，屬於狀態模型變更後留下的重複／過期邏輯。

### Missing Script 檢查結論

- 已掃描 `Assets` 下所有 Scene／Prefab，共 1,158 個 `MonoBehaviour.m_Script` 引用；沒有 `fileID: 0`，也沒有找不到 `.meta` 的 GUID。
- 已將當時載入的 binary recovery backup `Temp/__Backupscenes/0.backup` 放進一次性專案，透過 `GameObjectUtility.GetMonoBehavioursWithMissingScriptCount()` 逐一掃描完整 Hierarchy，結果為 `total=0`。
- 原 Editor log 的四行警告出現在 `AwakeInstancesAfterBackupRestoration` 的 domain reload／備份還原階段；重新匯入同一份 backup 後無法重現。依目前證據判斷是還原當下的暫態警告，不對應任何持續缺少 Script 的 GameObject，因此沒有需要修復或刪除的場景物件。

### 驗證現況

- 已新增「公司已讀／公司已聯絡」篩選選項與狀態碼索引一致的 UI 測試，以及求職條件頁完整 Scene 序列化引用測試；完整狀態集合一致性及「重複投遞需確認」仍缺少整合測試。
- 本次無法從命令列重跑 Unity 測試，原因是相同專案已在另一個 Unity Editor 執行個體開啟；這不是測試失敗。
- 產生的 `.sln/.csproj` 直接用 `dotnet build` 也無法當成有效驗證：目前 NuGet restore 輸出位置與 Unity 產生專案期待的 `Temp/obj/Debug/.../project.assets.json` 不一致。最後仍應由已開啟的 Unity Editor 完成 compile 與 EditMode test。

## 面試 Demo 建議流程（5～7 分鐘）

1. **首頁**：說明這是本機優先的個人求職管理工具，摘要重用同一套 Analytics Query。
2. **職缺列表**：切換 Demo／個人資料，展示篩選與資料隔離。
3. **新增或編輯職缺**：帶到 UI → Command → Domain 驗證 → Repository 的分層。
4. **職缺詳情**：新增一個應徵事件，展示事件歷程與 Reducer 推導狀態。
5. **規則式比對**：強調可解釋、可重現，缺資料不等於不符合。
6. **履歷**：說明它是可重用的母資料，與單次投遞文件分開。
7. **資料搬運／垃圾桶**：用預覽、雜湊、備份與原子寫入收尾，突顯資料安全意識。

若「求職條件」尚未完成雙向比對，只展示資料輸入與下一步設計，不把 roadmap 說成現成功能。

## 面試時值得主動說的設計取捨

- **大量使用 AI 協作，但由人定義邊界與驗收。** 可說明你負責資料模型、不可覆寫原則、測試情境與 demo 驗收；AI 用於加速實作，不代表跳過設計責任。
- **目前真正的核心不是 LLM。** 規則式比對和分析可離線、可測試、結果可追溯；未來 AI 只做摘要與輔助，不直接改母資料。
- **資料安全優先。** Demo 與真實資料分離、匯入先預覽、取代先備份、檔案採驗證及原子替換。
- **事件歷程保留原因。** 只存最後狀態無法回答「何時投遞、何時面試、怎麼走到現在」；事件可支援回顧與分析。
- **承認目前技術債。** `AllJobPage.cs`、`Panel_JobDetail.cs`、`CareerProfilePage.cs` 偏大，現階段以可用 MVP 為主；後續可拆成 Presenter／Controller 與較小元件。

## 你最少要能回答的五題

1. 為什麼不用 UI 直接讀寫 JSON？
2. 為什麼 Application 要保存事件，而不是只存目前狀態？
3. Demo 與個人真實資料怎麼隔離？
4. 規則式比對怎麼避免把「沒資料」誤判成「不符合」？
5. AI 生成很多程式時，你如何驗證正確性與維持控制權？

建議回答第 5 題時，用具體證據：**版本範圍、Domain 規則、測試、人工驗收、資料備份與安全邊界**，不要只回答「我有 review 程式碼」。

## 相關深入文件

- `README.txt`：目前功能、開啟方式與 roadmap。
- `docs/v0.2/data_model_decisions.md`：資料模型、事件、狀態與 migration 決策。
- `docs/v0.2/`：各版本完成報告與人工驗收紀錄。
- `docs/job_parse_spec.md`：早期職缺 JSON 與解析規格背景。
