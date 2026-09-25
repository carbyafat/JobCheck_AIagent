# JobCheck 面試追問題庫

用途：面試前 5 分鐘快速複習。每題先講「短答」，被追問時再補「依據」。

## 30 秒專案定位

> JobCheck 是用 Unity 製作的本機優先求職流程管理工具。UI 只負責輸入與呈現；應徵狀態、驗證與 JSON 儲存分開處理。重點不是把 AI 接進來，而是讓資料安全、流程可追溯，且規則比對能重現與驗證。

## 最可能被問的題目

### 1. 為什麼用 Unity 做這類工具，不用 Web 或桌面框架？

**短答：** 因為 Unity 是我最熟悉的工具，所以我選它快速完成一個可操作的完整 Demo，並把時間放在資料模型、應徵流程與錯誤處理。它不是這類工具唯一或必然最好的技術選擇。

**被追問技術選擇時再補：** 即使 UI 用 Unity，我仍把畫面協調放在 `Assets/Scripts/`，Domain 與 Persistence 放在 `Assets/JobCheck/`，避免商業規則綁死在 MonoBehaviour。

**設備商延伸：** 我熟悉 Unity 的 UI 與互動開發；面對設備軟體時，我會延續同一個原則：畫面呈現、設備通訊與安全規則各自分開，不讓按鈕事件直接決定設備狀態。

### 2. UI 按鈕怎麼避免各自寫一套規則？

**短答：** UI 將使用者動作轉成 Command，Command 再呼叫 Domain 驗證與 Repository；UI 不直接修改 JSON，也不自行推導應徵狀態。

**依據：** `Panel_JobDetail.cs` → `AllJobPage.cs` → `ApplicationCommandService.cs` → `JobCheckDataRepository.cs`。

### 3. 為什麼 Application 要保留事件歷程，又存目前狀態？

**短答：** 事件歷程回答「發生過什麼、何時發生」，目前狀態則讓列表與查詢快速顯示。狀態可由事件統一推導與驗證，因此不把轉換規則散落在 UI。

**依據：** `ApplicationStateReducer.cs`，以及 `ApplicationCommandService.cs`。

**可補充：** 同一職缺已結案後才允許再次投遞；新一輪會建立新的 Application 並連回前一輪，不覆蓋歷史。

### 4. JSON 寫到一半失敗怎麼辦？

**短答：** 先完成資料驗證與 DTO 轉換，再寫入同目錄暫存檔，最後才原子替換正式檔。匯入取代履歷前另有備份與重新載入核對。

**依據：** `JobCheckDataRepository.cs`、`CareerProfileRepository.cs`、`CareerProfilePortableImportService.cs`。

### 5. Demo 資料和真實資料怎麼隔離？

**短答：** Demo 使用版控內的虛構 `data/`；個人資料放在被 Git 忽略的 `personal_data/`。Demo 可互動展示，但刪除被禁止，避免現場誤刪範例資料。

**依據：** `AllJobPage.cs` 的資料區切換與 `CanDeleteJobPostings`，以及 `data/README.md`。

### 6. 履歷與職缺的符合度是 AI 判斷嗎？可信嗎？

**短答：** 目前不是生成式 AI，而是本機規則式比對。技能、年資、學歷、語言都能回溯到具體欄位；「履歷沒填」與「明確不符合」會分開顯示，不會假裝模型知道答案。

**依據：** `Runtime/Matching/RequirementMatchEngine.cs`、`RequirementScore.cs`。

### 7. AI 生成程式碼後，你如何確認不是黑箱？

**短答：** 我先訂資料邊界與不可違反的規則，再用 AI 協助實作；每個功能會從 UI 入口追到 Command、Domain、Repository，並補測試與實機驗收。AI 產出是實作草稿，設計與驗收責任仍在我。

**具體例子：** 重複投遞的行為改成「進行中禁止、結案後才建立新一輪」；履歷 enum 壞資料不再靜默套預設值，而是顯示欄位錯誤且不改原始檔。

### 8. 目前有哪些技術債？為什麼暫時不修？

**短答：** `AllJobPage`、`Panel_JobDetail`、`CareerProfilePage` 目前偏大，是 MVP 先完成完整流程的取捨。下一步會把畫面協調拆成較小的 Presenter／Controller；不過資料規則已先集中在 Domain 與 Command，所以拆 UI 不會碰到核心資料模型。

**不要主動說成 bug：** 個人求職資料量通常不到數百筆，完整 JSON 載入目前是可接受的正確性優先取捨；若未來改成多人或雲端同步，再做快取、局部刷新或背景讀取。

### 9. 如果變成設備操作軟體，你會優先補什麼？

**短答：** 我會先把資料來源抽象成介面，將目前的 JSON Repository 換成設備通訊／服務層；UI 保持只呈現 ViewModel。接著補上連線狀態、逾時／重試、告警分級、權限、操作審計與安全互鎖。

**重點：** 不會把設備通訊邏輯寫進 Button listener，也不會讓 UI 畫面直接決定安全狀態。

### 10. 你怎麼測試？

**短答：** Domain 與 Persistence 用 Unity EditMode 測試驗證資料規則、轉換、匯入與原子寫入；UI 再用實際 Play Mode 驗收關鍵流程。遇到錯誤資料時，測試會用暫存目錄建立檔案，確認讀取失敗且原檔未改，不碰 Demo 資料。

**依據：** `Assets/JobCheck/Tests/`，尤其 `PersistenceEditMode/`。

## 面試時的回答節奏

1. 先用一到兩句回答設計目的。
2. 補一個具體取捨或失敗情境。
3. 指出實作位置或驗證方式；不需要背完整檔名。
4. 沒做的功能直接說明邊界與下一步，不把 roadmap 說成完成品。

## 避免的說法

- 不說「AI 幫我全部做完」；改說 AI 協助實作，你定義規則、審查與驗收。
- 不說「這是 event sourcing」當作口號；先說保留事件是為了可追溯，再提 Reducer 推導狀態。
- 不把規則式履歷比對說成 AI 推薦。
- 不承諾目前已具備多人同步、雲端、平台爬蟲或自動投遞。
