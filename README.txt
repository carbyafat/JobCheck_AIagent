AI Personal Job Agent
=====================

1. 專案作用
-----------

本專案是一個個人求職職缺管理工具的 MVP。

核心目標是將職缺資料整理成 JSON，並透過 Unity 製作一個簡單介面，用來瀏覽、追蹤、篩選與管理職缺狀態。

長期目標是讓 Unity 成為主要操作介面，並串接分析用 AI：

- AI 負責解析職缺、分析適配度、產生建議
- Unity 負責顯示資料、操作狀態、篩選與管理求職流程

目前階段尚未在 Unity 內直接串接 AI。職缺 JSON 目前是另外使用 AI 或人工流程產生後，再放入專案資料夾供 Unity 讀取。

資料分工如下：

- jobs/
  職缺原始資料。由 AI 或人工整理產生，原則上不由 Unity 程式修改。

- job_index/
  職缺列表摘要。Unity 開啟時優先讀取 jobs_index.json，用來快速顯示總攬頁。

- job_tracking/
  使用者操作資料。包含狀態、最後操作日期、人工到期日、我的最愛、適配度分數等。Unity 程式會修改這裡的資料。

- docs/
  專案規格文件，例如職缺 JSON 解析規則。

- AI Personal Job agent/
  Unity 專案本體。


2. 目前進度
-----------

目前已完成一個可運作的粗略版本：

- 職缺 JSON 資料格式規劃
- 職缺解析規格文件 docs/job_parse_spec.md
- 20 筆展示用假職缺資料
- jobs_index.json 總攬資料
- 每筆職缺對應的 tracking JSON
- Unity 總攬頁
- 職缺列表載入
- 分頁顯示
- 單筆職缺顯示公司、職稱、薪資、狀態
- 點擊職缺後進入詳細頁
- 詳細頁顯示職缺內容、技能、福利、招募流程等資料
- 狀態切換
- 狀態寫回 job_tracking/*.tracking.json
- 人工到期日設定
- 逾期判斷與紅色提示
- 逾期職缺優先顯示
- 篩選面板初版
  - 薪水下限
  - 狀態
  - 只看逾期
  - 適配度分數下限


3. 使用方式
-----------

開啟 Unity 專案：

AI Personal Job agent/

主要場景：

Assets/Scenes/SampleScene.unity

基本流程：

目前使用方式分成兩段：

第一段：產生職缺 JSON

1. 使用外部 AI 或人工整理職缺資料
2. 依照 docs/job_parse_spec.md 的格式產生 jobs/*.json
3. 更新 job_index/jobs_index.json
4. 為每筆職缺準備 job_tracking/*.tracking.json

第二段：Unity 讀取與管理

1. 開啟場景
2. 執行 Unity Play Mode
3. 在總攬頁按下 Load
4. 程式讀取 job_index/jobs_index.json
5. 程式讀取 job_tracking/*.tracking.json
6. 顯示職缺列表
7. 點擊某筆職缺進入詳細頁
8. 可在詳細頁切換狀態或設定到期日
9. 返回總攬頁後會看到更新後的狀態

資料來源：

- 職缺詳細資料放在 jobs/*.json
- 總攬列表放在 job_index/jobs_index.json
- 使用者狀態資料放在 job_tracking/*.tracking.json

注意事項：

- jobs/*.json 視為原始職缺資料，原則上不要由 Unity 修改。
- job_tracking/*.tracking.json 是 Unity 可以寫入的操作資料。
- 目前專案內的職缺資料皆為展示用假資料。


4. 後續規劃功能
---------------

短期規劃：

- 篩選面板 UI 細修
- 我的最愛切換與排序
- 適配度分數顯示
- 適配度分析結果頁
- 人工到期日清除功能
- 狀態過期規則設定化
- 詳細頁排版優化

中期規劃：

- AI 適配度分析流程
- candidate_profile.json 個人履歷與求職偏好資料
- job_analysis/*.fit.json 儲存 AI 分析結果
- 將 fit_score 寫回 job_tracking
- 支援從 1111、104 或其他平台匯入職缺
- 匯入時自動依 job_parse_spec.md 轉成標準 JSON

長期規劃：

- Unity 串接分析用 AI
- 在 Unity 內直接觸發職缺解析與適配度分析
- 履歷資料管理
- 依職缺產生面試問題
- 依職缺產生客製履歷重點
- 投遞紀錄與面試紀錄管理
- 更完整的搜尋與篩選
- 將 Unity UI 做成更完整的個人求職儀表板
