JobCheck
========

JobCheck 是以 Unity 製作的個人求職紀錄與應徵流程管理工具。V0.2.0 已將舊版直接讀寫 JSON 的做法，重構為 Domain、DTO、Mapper、Repository 與 ApplicationEvent 架構。


目前版本
--------

V0.2.0 已完成：

- Company、JobPosting、Application、ApplicationEvent 正式資料模型
- V0.2 JSON DTO、Mapper、Validator 與 Repository
- V0.1 demo migration、來源備份、manifest 與 report
- 職缺列表、篩選與詳細資料顯示
- 新增與編輯職缺
- 應徵狀態、收藏、備註與追蹤日期寫入
- Application 事件歷程顯示
- 原始職缺內容保留
- 受控 Tag 與 RiskFlag 新增、編輯及顯示
- TextMesh Pro 中文字體統一
- 265 個 Unity EditMode tests 全數通過

V0.2.0 不包含 AI 適配度分析、真實平台匯入、分析頁與新版匯入／匯出；這些屬於後續版本。


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
3. 在總攬頁按 Load 載入 `data/`。
4. 可瀏覽、篩選、新增或編輯職缺。
5. 點擊職缺可查看詳細內容、更新應徵狀態與查看事件歷程。


V0.2 資料結構
--------------

正式資料根目錄為儲存庫根目錄的 `data/`：

- `data/companies/`：公司主資料。
- `data/jobs/`：職缺主資料；以 `company_id` 關聯公司，不重複保存公司名稱。
- `data/applications/`：應徵資料及內嵌事件歷程。
- `data/migration/`：V0.1 demo migration 的備份與證據。

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

選擇 EditMode 後執行 Run All。V0.2.0 封版基準為 265 passed、0 failed。


後續方向
--------

- V0.2.1：真實資料導入流程
- V0.2.2：漏斗、平台成效與結案原因分析
- V0.2.3：新版匯入／匯出
- V0.2.4：AI Fit Score、摘要與面試準備
