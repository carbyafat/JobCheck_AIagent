JobCheck
========

JobCheck 是以 Unity 製作的個人求職紀錄與應徵流程管理工具。V0.2.0 建立了 Domain、DTO、Mapper、Repository 與 ApplicationEvent 架構；V0.2.1 則讓這套架構能安全地承載個人真實求職資料。


目前版本
--------

V0.2.1 功能已完成：

- Demo 與個人資料使用不同資料目錄，可在 UI 中切換。
- `personal_data/` 完全排除於 Git，不會將真實求職資料混入正式 demo。
- 新增及編輯表單支援完整的結構化職缺欄位、原始職缺內容、Tag 與 RiskFlag。
- 薪資類型、薪資週期、工作模式與聘僱形式改為受控選項。
- 多值欄位使用勾選介面，工作內容與技能需求可使用多行輸入。
- 應徵事件可補登實際發生日期，並以事件歷程推導目前狀態。
- 日期與文字事件使用整合輸入視窗，標題會依操作內容切換。
- 個人職缺可移至 `personal_data/trash/`，Demo 資料禁止刪除。
- Domain 驗證錯誤集中翻譯為繁體中文，不直接向使用者顯示 enum 名稱。
- 已使用真實求職資料持續驗證新增、編輯、狀態、事件與刪除流程。

V0.2.1 的「真實資料導入」是人工整理與輸入流程，不包含平台爬取或自動匯入。分析頁、新版匯入／匯出及 AI 功能仍屬於後續版本。

完整紀錄請見 `docs/v0.2/v0.2.1_real_data_onboarding_report.md`。


開啟方式
--------

Unity 版本：2022.3.62f3 LTS

Unity 專案：

    AI Personal Job agent/

主要場景：

    Assets/Scenes/SampleScene.unity

基本操作：

1. 使用 Unity 2022.3.62f3 開啟專案。
2. 開啟 SampleScene 並進入 Play Mode。
3. 在總攬頁確認目前是 Demo 或個人資料，點擊資料標示可切換。
4. 按 Load 載入目前選取的資料區。
5. 可瀏覽、篩選、新增、編輯或刪除個人職缺。
6. 點擊職缺可查看詳細內容、補登應徵事件、設定追蹤日期與查看事件歷程。


V0.2 資料結構
--------------

儲存庫根目錄目前有兩套互相隔離的資料區：

- `data/`：納入版本控制的虛構 Demo 資料。
- `personal_data/`：本機個人資料，整個目錄由 `.gitignore` 排除。

兩套資料區的主要資料使用相同 V0.2 結構：

- `companies/`：公司主資料。
- `jobs/`：職缺主資料；以 `company_id` 關聯公司，不重複保存公司名稱。
- `applications/`：應徵資料及內嵌事件歷程。

`data/migration/` 另外保存 V0.1 demo migration 的備份與證據。個人資料被刪除時則會連同相關 Application 與事件移至 `personal_data/trash/`，目前尚未提供 UI 還原功能。

已提交的資料全部是虛構 demo。Unity UI 日後產生的新 UUID JSON 預設只留在本機，不會自動進入 Git；正式 demo 清單與加入方式請見 `data/README.md`。


主要程式區域
------------

- `Assets/JobCheck/Runtime/`：Domain 模型、enum、ID、驗證與事件狀態推導。
- `Assets/JobCheck/Persistence/`：DTO、Mapper、Repository、migration、query 與寫入服務。
- `Assets/JobCheck/Tests/`：Domain 與 Persistence EditMode tests。
- `Assets/Scripts/`：Unity UI 與 V0.2 顯示／操作接軌。
- `docs/v0.2/`：V0.2 規則、migration 與完成紀錄。


測試
----

在 Unity 開啟：

    Window > General > Test Runner

選擇 EditMode 後執行 Run All。V0.2.0 的完整封版基準為 265 passed、0 failed；V0.2.1 增加了個人資料、歷史事件、完整職缺輸入、可恢復刪除與錯誤翻譯測試。V0.2.1 已完成持續人工驗收與最新程式編譯，但封版時沒有另記一次完整 Run All 的新總數。


後續方向
--------

- V0.2.2：漏斗、平台成效與結案原因分析
- V0.2.3：新版匯入／匯出
- V0.2.4：AI Fit Score、摘要與面試準備
