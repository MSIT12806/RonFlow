# RonFlow v0.3：讓 AI 可以順暢使用系統

## 1. 文件定位

本文件描述 RonFlow v0.3 的交付範圍。

這份 spec 不直接改寫 `ronflow-core-flow-spec.md` 的原因是：

1. `ronflow-core-flow-spec.md` 應維持為 RonFlow 目前產品行為的 living spec。
2. 「讓 AI 可以順暢使用系統」屬於一個新的版本目標與能力切片，不只是零散功能補丁。
3. v0.3 需要先回答產品設計問題：AI 要如何發現 RonFlow、理解 RonFlow、在安全邊界內操作 RonFlow。

本文件聚焦於這個版本應補上的 AI-facing product surface，而不是先假設 AI 只能透過既有人類 UI 硬操作。

---

## 2. v0.3 產品定位

RonFlow v0.3 定位為：

> 人類與 AI 都可以順暢使用的專案管理工具。

v0.1 解的是「個人可用」。

v0.2 解的是「多人協作可用」。

v0.3 要解的是：

```text
一個通用型 AI agent，在拿到最小必要 bootstrap 後，能不能安全地讀取 RonFlow 的工作上下文、理解主要名詞、查詢目前狀態、提出下一步、並在批准後完成必要操作？
```

這代表 RonFlow 從「給人用的工具」往前走一步，變成「人與 AI 都能共享的工作系統」。

---

## 3. v0.3 核心判斷標準

RonFlow v0.3 是否完成，應以以下問題判斷：

```text
一個新的 AI agent，在沒有讀完整份核心 spec 的前提下，是否能憑藉 RonFlow 提供的 bootstrap、能力清單與摘要查詢，順利接手目前工作並避免高風險誤操作？
```

也就是說，v0.3 至少應讓 AI 可以完成以下事情：

```text
1. 知道 RonFlow 的存在、定位與基本使用原則
2. 知道目前有哪些可用操作，而不是靠猜 API 或猜 UI
3. 知道主要領域名詞與它們之間的關係
4. 在寫入前先讀取目前 project / task / session / scope 的摘要
5. 提出變更提案，並在需要時等待人類批准
6. 執行單筆或小範圍變更，且能回報結果與影響
7. 在失敗時看懂錯誤原因，知道該重試、修正參數，還是回頭問人
8. 讓人類可以追查「哪個 AI 在什麼上下文下做了什麼事」
```

---

## 4. 問題陳述

RonFlow 目前已具備人類使用者流程與多人協作基礎，但若要讓 AI 真正成為使用者，現況仍有幾個缺口：

```text
1. AI 不知道 RonFlow 存在，也不知道何時應該使用它
2. AI 不知道有哪些能力可用，容易退化成猜測 API、猜欄位或猜畫面流程
3. AI 不知道 RonFlow 的領域名詞、狀態軸與 session / scope 規則
4. AI 若直接寫入，很容易在缺少上下文時做出高風險操作
5. AI 即使失敗，也可能只拿到模糊錯誤，無法知道下一步怎麼修正
6. 人類目前難以把 AI 的操作意圖、批准點與變更紀錄串成完整審計鏈
```

因此，v0.3 的重點不是「讓 AI 會點 UI」，而是讓 RonFlow 提供一層適合 AI 使用的產品介面。

---

## 5. v0.3 Scope

### 5.1 Included

RonFlow v0.3 應包含以下能力：

```text
1. AI bootstrap
2. AI 專用的領域名詞表
3. 可發現的 capabilities manifest
4. Read-first 的摘要查詢
5. Proposal / preview / approval 工作流
6. 結構化且可恢復的錯誤語意
7. AI 操作審計與觀測紀錄
8. AI session / scope awareness
9. 第一批 AI-native workflow guidance
```

### 5.2 Excluded

RonFlow v0.3 暫不以以下事項為完成條件：

```text
1. 讓 AI 直接透過畫面視覺辨識完成所有操作
2. 讓 AI 自行學習所有 RonFlow 規則，而不需要任何 bootstrap
3. 全自動、無人批准的大範圍資料修改
4. 把所有核心 spec 全部改寫成專供 AI 閱讀的單一文件
5. 以 LLM prompt 取代正式產品 contract、權限模型與審計機制
```

---

## 6. v0.3 核心主軸

RonFlow v0.3 聚焦於五條主線：

```text
1. Discoverability
2. Read-first Context Loading
3. Safe Write Workflow
4. AI Bootstrap and Guidance
5. Auditability
```

---

## 7. AI Bootstrap Spec

### 7.1 是否需要起始 prompt

需要，但它的角色應是 bootstrap，而不是完整產品手冊。

起始 prompt 的目的應是：

```text
1. 告訴 AI「RonFlow 是一個可被使用的工作系統」
2. 告訴 AI「何時應優先使用 RonFlow，而不是只留在聊天或文件裡」
3. 告訴 AI「有哪些高階能力入口可以查更多資訊」
4. 告訴 AI「哪些情況可以直接做，哪些情況必須先提案或等待批准」
```

起始 prompt 不應承擔以下責任：

```text
1. 直接塞進所有 API、所有規則、所有狀態轉移細節
2. 取代版本化 spec、名詞表、capabilities manifest
3. 依賴 prompt 記憶去保證安全邊界
```

### 7.2 Bootstrap 應包含的最小內容

RonFlow 提供給 AI 的 bootstrap，至少應包含：

```text
1. RonFlow 是什麼
2. RonFlow 主要處理哪些工作物件（project / board / task / session / scope）
3. AI 的基本工作原則：先讀後寫、先摘要後深入、遇高風險操作先提案
4. 可用能力入口：去哪裡查 capabilities manifest、去哪裡查 glossary、去哪裡查 workflow guidance
5. 明確 escalation 規則：哪些情況必須先問人
```

### 7.3 Bootstrap 成功標準

```text
1. 新的 AI agent 不需要先讀完整個 core flow spec，仍能知道如何開始
2. AI 可以知道自己應先查 manifest / glossary / summary，而不是直接寫入
3. AI 可以知道 RonFlow 的存在與用途，而不會把工作管理散落在對話或暫存筆記中
```

---

## 8. AI-facing System Capabilities

### 8.1 Capability Discovery

系統應提供可發現的能力清單，至少說明：

```text
1. 能做什麼操作
2. 每個操作需要哪些前置條件
3. 每個操作需要哪些參數
4. 哪些操作屬於 read，哪些屬於 write，哪些屬於高風險 write
5. 成功結果與常見錯誤碼
```

AI 不應需要靠猜測 endpoint、UI 控件或資料欄位來理解 RonFlow。

### 8.2 Ubiquitous Language for AI

系統應提供給 AI 用的領域名詞表，至少涵蓋：

```text
1. Project
2. Board
3. Task
4. Workflow State
5. Lifecycle State
6. Session
7. Scope
8. Proposal
9. Approval
10. Audit Trail
```

名詞表應額外說明：

```text
1. 哪些是使用者可見語彙
2. 哪些是工程 contract 語彙
3. 哪些概念常被混淆
```

### 8.3 Read-first Summary Queries

RonFlow 應提供低成本摘要查詢，讓 AI 在寫入前先理解目前上下文。

至少應支援以下摘要：

```text
1. 我目前在哪個 project / scope
2. 目前有哪些 open tasks / in-progress tasks / blocked tasks
3. 某個 task 的摘要、目前狀態、最近活動、是否有未完成提案
4. 目前 session 是否有效、是否已 activate
5. 目前使用者或 agent 是否具有執行該操作的權限
```

這些查詢的目標是降低 token 成本與誤判，而不是把整個 board 原封不動塞給 AI。

### 8.4 Proposal / Preview / Approval

RonFlow 應讓 AI 先提案，再視情況套用。

至少應支援：

```text
1. proposal：AI 提出預計變更內容與理由
2. preview：系統顯示實際會改哪些欄位、哪些 task、哪些狀態
3. approval：人類明確批准後才套用高風險變更
4. apply：批准後正式寫入
5. reject / revise：人類可拒絕或要求 AI 修正提案
```

### 8.5 Structured Errors and Recovery

RonFlow 應回傳可恢復的錯誤，而不是只有模糊失敗。

至少應區分：

```text
1. Unauthorized
2. Forbidden
3. SessionNotActivated
4. ScopeRequired
5. ValidationFailed
6. InvalidStateTransition
7. ConcurrencyConflict
8. ApprovalRequired
9. ResourceNotFound
```

錯誤回應至少應包含：

```text
1. machine-readable code
2. human-readable message
3. 可採取的下一步
```

### 8.6 Audit Trail for AI Actions

RonFlow 應記錄 AI 的操作軌跡，至少包含：

```text
1. actor 類型（human / AI）
2. actor identity
3. 執行操作的時間
4. 操作前後差異
5. 依據的 proposal / approval / task / session
6. 操作結果
```

這層能力不是可有可無，因為它直接決定人類是否敢把真實工作交給 AI。

---

## 9. Safe Write Workflow

### 9.1 寫入前提

AI 在寫入前，至少應先完成：

```text
1. 識別目前 project / scope
2. 讀取目標 task 或目標物件摘要
3. 確認自己有對應能力與權限
4. 判斷本次操作是否需要 approval
```

### 9.2 高風險操作

以下操作預設應視為高風險：

```text
1. 批次修改多筆 task
2. 刪除、封存或移到垃圾桶
3. 大幅重排 priority / workflow
4. 改寫 spec baseline
5. 變更 project 成員或權限
```

高風險操作不應默默執行。

### 9.3 小範圍安全操作

若 AI 已具備完整上下文，且操作屬於低風險，可直接執行的小範圍操作包括：

```text
1. 讀取摘要
2. 查詢 task 狀態
3. 補 comment 或 note 類低風險附註
4. 建立 proposal
5. 更新明確指定的單一欄位，且不跨 project / scope
```

實際低風險清單仍需由權限與 policy 規則約束。

---

## 10. AI User Flow

### 10.1 初次接觸 RonFlow

```text
1. AI 收到與工作管理相關的任務
2. Bootstrap 告知 AI 應優先使用 RonFlow 管理工作
3. AI 讀取 capabilities manifest 與 glossary
4. AI 讀取目前 session / scope 摘要
5. AI 決定接下來應查詢 project、task 或 proposal
```

### 10.2 接手目前工作

```text
1. AI 讀取目前 active project / scope
2. AI 讀取目前 open work items 摘要
3. AI 找出目前焦點 task、blocked task 與最近活動
4. AI 產生下一步建議
5. 人類可接受、修改或拒絕建議
```

### 10.3 進行寫入操作

```text
1. AI 先讀取目標物件摘要
2. AI 產生 proposal 或 preview
3. 若需要 approval，等待人類批准
4. 批准後執行 apply
5. 系統回傳結果、差異與審計紀錄
```

### 10.4 發生錯誤時

```text
1. 系統回傳結構化錯誤
2. AI 依錯誤碼判斷是否補參數、重試、切 scope、改成提案或回頭詢問人類
3. 若為權限、批准或跨 scope 問題，AI 不應自行繞過
```

---

## 11. v0.3 交付物

RonFlow v0.3 至少應交付以下產物：

```text
1. 一份 AI bootstrap 文件或等價入口
2. 一份 capabilities manifest
3. 一份 AI 專用 glossary / ubiquitous language 文件
4. 一組 read-first summary 查詢能力
5. 一組 proposal / preview / approval contract
6. 一組 AI action audit trail 設計
7. 一份對應的 acceptance / evaluation checklist
```

---

## 12. 驗收問題清單

v0.3 驗收時，至少應能回答以下問題：

```text
1. 新的 AI agent 是否能在短 bootstrap 後找到 RonFlow 的正確入口？
2. AI 是否能在不讀完整份 core spec 的前提下，查到自己需要的能力與名詞定義？
3. AI 是否能先用摘要查詢理解上下文，而不是直接進行寫入？
4. AI 在高風險操作前，是否會被導向 proposal / approval 流程？
5. AI 操作失敗時，是否能依結構化錯誤找到下一步？
6. 人類是否能追查 AI 做了哪些操作、為什麼做、是否經過批准？
```

若這些問題仍無法回答「是」，就代表 RonFlow 還沒有真正到達「AI 可順暢使用」的程度。