# 職缺解析規格 v0.1

## 目的

本文件定義如何將職缺資料轉換成標準 job JSON。

職缺資料來源可能是：

- 招募網站頁面
- 使用者貼上的文字
- 使用者提供的截圖
- 公司官網職缺頁

產出的 JSON 主要用於：

- 儲存職缺資料
- 瀏覽與篩選職缺
- 交給 Unity 程式讀取
- 之後提供給其他 AI skill 做履歷適配度分析

## 責任範圍

本文件只負責「客觀解析職缺內容」。

應該做的事：

- 保留原始職缺資訊
- 將薪資、地點、工作模式、技能需求等重要資訊標準化
- 保留足夠的 `raw_text`，方便之後人工檢查
- 讓不同平台來源的職缺都能轉成穩定的 JSON 結構

不應該做的事：

- 不比較履歷
- 不判斷使用者適不適合這份工作
- 不產生適配度分數
- 不依照個人偏好排序職缺
- 不改寫履歷或自傳

履歷比對、適配度分析、投遞建議，應該由另一個獨立 skill 或流程處理。

## 建議檔案結構

每一份職缺建議存成一個獨立 JSON。

```text
jobs/
  {source}_{company}_{title}_{captured_date}.json

jobs_index.json
```

建議一個職缺一個 JSON，原因是：

- 單一職缺比較容易重新解析
- 單一職缺比較容易更新追蹤狀態
- 不會因為職缺變多，讓單一 JSON 檔案過大
- 之後可以另外用 `jobs_index.json` 給 Unity 快速讀取列表摘要

## Jobs Index：總攬索引檔

`jobs_index.json` 是程式啟動時優先讀取的總攬檔。

用途：

- 快速顯示所有職缺列表
- 避免一開始就讀取所有詳細 JSON
- 使用者點擊某一筆職缺後，再依照 `file` 讀取完整職缺 JSON

畫面上第一版只需要顯示：

- 公司
- 職缺名稱
- 薪資

但資料中仍應保留 `id` 與 `file`，因為程式需要知道點擊後要載入哪一份詳細 JSON。

建議格式：

```json
{
  "schema_version": "0.1",
  "generated_at": "2026-06-20T10:50:00+08:00",
  "jobs": [
    {
      "id": "104_yahsin_software_engineer_2026-06-20",
      "file": "jobs/104_yahsin_software_engineer_2026-06-20.example.json",
      "company": "亞新工程顧問股份有限公司",
      "title": "軟體工程師",
      "salary": "待遇面議（經常性薪資達 4 萬元或以上）",
      "parse_status": "usable"
    }
  ]
}
```

欄位規則：

- `id` 對應詳細職缺 JSON 的 `id`
- `file` 是詳細職缺 JSON 的相對路徑
- `company` 來自 `company.name`
- `title` 來自 `job.title`
- `salary` 優先使用 `compensation.raw_text`
- `parse_status` 讓 Unity 可以預設只顯示 `usable`，或將 `needs_review`、`unusable` 分區顯示

第一版 Unity 顯示列表時，只需要使用：

```text
company
title
salary
```

使用者點擊列表項目時，再用同一筆資料的 `file` 載入詳細 JSON。

## JSON 頂層結構

```json
{
  "schema_version": "0.1",
  "id": "",
  "parse_status": "usable",
  "source": {},
  "company": {},
  "job": {},
  "compensation": {},
  "location": {},
  "work_conditions": {},
  "responsibilities": [],
  "requirements": {},
  "benefits": {},
  "recruitment_process": [],
  "parse_metadata": {},
  "tracking": {}
}
```

## 必要 JSON 結構

以下欄位應該固定存在於 JSON 結構中，即使某些值未知也一樣。

- `schema_version`
- `id`
- `parse_status`
- `source.platform`
- `source.url`
- `source.captured_at`
- `company.name`
- `job.title`
- `compensation.raw_text`
- `compensation.type`
- `location.raw_text`
- `location.work_mode`
- `responsibilities`
- `requirements`
- `parse_metadata`
- `tracking.status`

未知的單一值使用 `null`。

未知或沒有內容的列表使用 `[]`。

## Must-To-Have 職缺資料

一份職缺只有在具備足夠資訊時，才算是可用職缺。

以下四項是 must-to-have：

- `responsibilities`：工作內容、工作職責
- `requirements`：技能需求、資格條件
- `compensation`：薪資、薪資範圍，或明確的待遇面議文字
- `location`：工作地點，或遠端、混合工作資訊

如果任何 must-to-have 資料缺失，不可以默默當成正常職缺處理。

使用 `parse_status` 表示解析後的職缺可用狀態：

- `usable`：四項 must-to-have 都存在，職缺可正常使用
- `needs_review`：資料存在但不清楚、不完整，或低信心推論，需要人工確認
- `unusable`：缺少 must-to-have，這份職缺不應進入正常追蹤或匹配流程

範例：

```json
{
  "parse_status": "unusable",
  "parse_metadata": {
    "missing_must_to_have": [
      "compensation"
    ],
    "notes": [
      "來源沒有提供薪資，也沒有明確待遇面議文字。"
    ]
  }
}
```

`unusable` 的職缺仍然可以存檔，作為除錯或人工檢查紀錄。

但 Unity 預設應該能夠隱藏或分區顯示 `unusable` 職缺。

## 未知資料處理規則

不要猜不存在的資料。

使用規則：

- 未知單一值使用 `null`
- 未知列表使用 `[]`
- enum 類型但無法判斷時使用 `"unknown"`
- 重要原文盡量保存在 `raw_text`

對 must-to-have 欄位來說，`unknown` 不足以讓職缺成為正常可用職缺。

例子：

- 完全沒有薪資：`compensation.type` 可以是 `undisclosed`，但 `parse_status` 通常應為 `unusable`
- 寫著 `待遇面議`：這算是有薪資資料，可維持 `usable`
- 沒有工作地點，也沒有遠端或混合工作資訊：`location.work_mode` 可以是 `unknown`，但 `parse_status` 通常應為 `unusable`
- 條件只寫 `具相關經驗佳`：如果無法拆出技能，可標記為 `needs_review`

如果某個欄位是根據上下文推論出來的，應該記錄到 `parse_metadata.inferred_fields`。

範例：

```json
{
  "field": "location.work_mode",
  "value": "onsite",
  "reason": "職缺提供明確辦公室地址，且未提到遠端或混合工作。",
  "confidence": "medium"
}
```

## Source：資料來源

`source` 紀錄職缺來自哪裡。

```json
{
  "platform": "104",
  "url": null,
  "captured_at": "2026-06-20T10:40:00+08:00",
  "raw_images": []
}
```

規則：

- `platform` 是來源平台，例如 `104`、`LinkedIn`、`Cake`、`company_site`
- `url` 是職缺頁面的完整網址，若沒有則為 `null`
- `captured_at` 使用 ISO 8601 格式，並包含時區
- `raw_images` 可記錄截圖來源路徑

## Company：公司資訊

```json
{
  "name": "",
  "industry": null,
  "raw_text": ""
}
```

規則：

- `name` 是公司名稱
- `industry` 是產業別，只有明確出現時才填
- `raw_text` 保留原始公司資訊文字

## Job：職缺基本資訊

```json
{
  "title": "",
  "department": null,
  "category": null,
  "updated_date": null,
  "openings": null,
  "raw_text": ""
}
```

規則：

- `title` 是職稱
- `department` 是部門或團隊名稱
- `category` 是平台上的職務類別，例如 `軟體工程師`
- `updated_date` 如果沒有年份，可以保留原文日期，例如 `06/12`
- `openings` 如果不是乾淨數字，保留原文，例如 `2~3人`

## Compensation：薪資資訊

薪資必須同時保留原文與標準化欄位。

```json
{
  "type": "range",
  "period": "monthly",
  "min": 40000,
  "max": 70000,
  "currency": "TWD",
  "raw_text": "月薪 40,000~70,000 元",
  "notes": null
}
```

允許的 `type`：

- `fixed`：固定薪資
- `range`：薪資範圍
- `minimum`：最低薪資
- `maximum`：最高薪資
- `negotiable`：待遇面議
- `undisclosed`：未提供薪資
- `unknown`：無法判斷

允許的 `period`：

- `monthly`：月薪
- `yearly`：年薪
- `hourly`：時薪
- `daily`：日薪
- `project`：論件或專案計酬
- `unknown`：無法判斷
- `null`：不適用或沒有資訊

規則：

- `待遇面議` 使用 `type: "negotiable"`
- `月薪 4 萬以上` 使用 `type: "minimum"`、`period: "monthly"`、`min: 40000`
- `年薪 100 萬以上` 使用 `type: "minimum"`、`period: "yearly"`、`min: 1000000`
- 如果完全沒有薪資，使用 `type: "undisclosed"`，並將 `parse_status` 標為 `unusable`
- 除非來源有其他可靠薪資欄位，否則不應自行推測薪資
- 原始薪資文字保存在 `raw_text`

## Location：工作地點

```json
{
  "work_mode": "onsite",
  "city": "",
  "district": null,
  "address": null,
  "remote_allowed": false,
  "raw_text": ""
}
```

允許的 `work_mode`：

- `onsite`：現場工作
- `remote`：遠端工作
- `hybrid`：混合工作
- `unknown`：無法判斷

規則：

- 明確寫遠端工作時，使用 `remote`
- 明確寫部分遠端、混合辦公、居家加辦公室時，使用 `hybrid`
- 如果提供辦公室地址且未提到遠端，可推論為 `onsite`
- 如果完全沒有地點，也沒有遠端或混合工作資訊，使用 `unknown`，並將 `parse_status` 標為 `unusable`
- 原始地點文字保存在 `raw_text`

## Work Conditions：工作條件

```json
{
  "employment_type": null,
  "working_hours": null,
  "business_trip": null,
  "management_responsibility": null,
  "leave_policy": null,
  "start_date": null
}
```

規則：

- 平台提供的工作性質、上班時段、出差、管理責任、休假制度等資訊放在這裡
- 無法標準化時，保留原始文字

## Responsibilities：工作內容

`responsibilities` 是工作內容列表。

規則：

- 每個條列盡量保留為一個 item
- 不要合併不相關的工作內容
- 不要新增職缺沒有寫的工作內容
- 如果完全找不到工作內容，使用 `[]`，並將 `parse_status` 標為 `unusable`

## Requirements：技能需求與條件要求

```json
{
  "experience": null,
  "education": null,
  "major": null,
  "languages": [],
  "tools": [],
  "skills": [],
  "other_conditions": []
}
```

規則：

- `tools` 放具名工具、程式語言、框架、平台、技術名詞
- `skills` 放比較廣義的能力，例如軟體工程系統開發、AI 應用設計
- `other_conditions` 放軟性條件、偏好條件、較長的要求句子
- 同一個技術如果在多處出現，`tools` 中只保留一次
- 如果完全找不到技能或資格條件，保留空陣列，並將 `parse_status` 標為 `unusable`

## Benefits：福利制度

```json
{
  "salary_bonus": [],
  "insurance_health": [],
  "flexibility": [],
  "training": [],
  "life": [],
  "other": []
}
```

規則：

- 依照福利性質分類
- 無法分類時放入 `other`
- 儘量保留原始文字

## Recruitment Process：招募流程

```json
[
  "投遞履歷",
  "HR初審履歷",
  "部門主管面試"
]
```

規則：

- 保留流程順序
- 每個流程步驟存成一個字串

## Parse Metadata：解析資訊

```json
{
  "parser": "manual_from_screenshot",
  "confidence": "medium",
  "missing_fields": [],
  "missing_must_to_have": [],
  "inferred_fields": [],
  "notes": []
}
```

規則：

- 用來記錄解析品質與解析備註
- 缺少的一般欄位記錄到 `missing_fields`
- 缺少的 must-to-have 欄位記錄到 `missing_must_to_have`
- 推論出來的欄位記錄到 `inferred_fields`

允許的 `confidence`：

- `high`：資料完整且明確
- `medium`：資料大致完整，但有少量推論或缺漏
- `low`：資料不完整，或多處需要人工確認

## Tracking：使用者追蹤狀態

```json
{
  "status": "not_applied",
  "priority": null,
  "favorite": false,
  "applied_at": null,
  "last_updated_at": null,
  "notes": null
}
```

允許的 `status`：

- `not_applied`：尚未投遞
- `interested`：有興趣
- `applied`：已投遞
- `interviewing`：面試中
- `offer`：錄取或收到 offer
- `rejected`：未錄取
- `closed`：職缺關閉
- `archived`：封存

規則：

- `tracking` 是使用者流程資料，不是職缺原始資料
- 預設狀態為 `not_applied`
