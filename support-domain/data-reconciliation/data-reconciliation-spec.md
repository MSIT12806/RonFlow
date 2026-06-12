# 資料收斂 Support Domain Spec

## 1. 規格目的

資料收斂是用來修復、合併、調整已經存在於多個資料庫中的 RonFlow 資料的 support domain。

這份規格存在的原因是：即使每個產品都依照 DDD 保護自己的 aggregate boundary，真實系統仍然會遇到營運層面的資料處理需求：

1. 使用者可能在資料庫同步機制成熟之前，已經在多個裝置上使用同一個產品。
2. 兩份資料庫可能包含同一個邏輯上的使用者、專案或任務，但技術 id 不同。
3. 產品可能需要匯入或重寫資料，同時仍然保留 aggregate invariants。
4. 一次性的 SQL script 可以修復單一事件，但很難保存可重複使用的業務規則、review flow 與操作脈絡。

這個 support domain 不應該被設計成泛用資料庫編輯器。每個產品 domain 都有自己的 aggregate root、reference rule 與歷史資料語意。因此 MVP 只聚焦在 RonFlow-aware reconciliation。

---

## 2. 範圍

### 2.1 MVP 範圍內

1. 比對兩個具有相同 RonFlow schema 的資料庫。
2. 在修改資料前產生 reconciliation plan。
3. 將指定 id remap 套用到 RonFlow 產品所擁有的所有 reference。
4. 重寫資料時尊重 aggregate root boundary。
5. 支援 dry-run console summary 與明確的 apply。
6. apply 前印出 destructive operations。
7. 第一階段只支援 RonFlow reconciliation。

### 2.2 MVP 範圍外

1. 任意 schema 的泛用跨資料庫 merge。
2. 沒有產品規則的自動語意合併。
3. reconciliation 執行期間仍允許目標資料庫有並行寫入。
4. 逐欄位互動式 visual diff。
5. RonAuth reconciliation。
6. 分散式同步或即時雙向 replication。

---

## 3. Ubiquitous Language

| 概念 | 說明 |
|---|---|
| 資料收斂 / Reconciliation | 讓兩份相關資料收斂到指定目標狀態的過程。 |
| Source Database | 提供資料、id 或 aggregate state 的來源資料庫。 |
| Target Database | apply operation 會修改的目標資料庫。 |
| Reconciliation Plan | 由 recipe 與輸入選項產生、可被 review 的預計變更集合。 |
| Dry Run | 只產生 plan 與 console summary，不修改 target database 的執行模式。 |
| Apply | 明確執行已確認 reconciliation plan 的操作。 |
| Recipe | RonFlow-specific reconciliation rules。 |
| Identity Match | 判斷兩筆資料是否代表同一個邏輯實體的規則。 |
| Id Remap | 將 reference 從一個技術 id 改寫成另一個技術 id。 |
| Aggregate Rewrite | 依照 product-aware rule 更新 aggregate root document/model 與它所擁有的 references。 |
| Source Retention Policy | 決定 source id/entity 要被 physical delete 或保留的策略。 |
| Conflict | 兩份資料都包含有意義但彼此不相容的值。 |
| Conflict Policy | 處理 conflict 的策略。 |
| Console Summary | 描述 plan 或 apply 結果的人類可讀 console output。 |

---

## 4. Domain Principles

### 4.1 尊重 Aggregate Root

Reconciliation 不應該只用 raw reference update 的方式修改資料，因為這可能破壞產品 aggregate rule。

RonFlow 的初始規則：

1. Project 是 aggregate root。
2. Task 是 aggregate root。
3. Project membership、project invitations、project subtask templates、task subtasks、task reminders、task activity timeline、code traceability 必須透過 product-aware aggregate data rules 重寫。
4. project id remap 必須更新每個指向該 project 的 task 與 projection reference。
5. user id remap 必須一致地更新 owner、member、actor reference。

### 4.2 Plan Before Apply

每一次 reconciliation operation 在修改 target database 前，都必須先產生 plan。

Plan 應包含：

1. 受影響的 product。
2. source database path。
3. target database path。
4. 使用的 recipe。
5. requested remaps。
6. 受影響的 aggregate roots。
7. 受影響的 references。
8. conflicts。
9. warnings。
10. 預估 row/document changes。

### 4.3 Local Mutation Safety

MVP 不建立 backup snapshot。

apply 前，console 必須清楚印出 target database path 與 planned destructive operations。operator 需要自行在工具外保留 backup，或透過 Git/database snapshot 管理復原點。

### 4.4 Human Review Is a Feature

資料收斂是低頻但重要的 operator workflow，應該被設計成需要人類 review。

預設流程：

```text
inspect -> generate plan -> review console output -> apply explicitly -> verify
```

---

## 5. Initial Product Recipe

### 5.1 RonFlow Recipe

RonFlow recipe 理解 RonFlow 的 SQLite persistence model 與產品 aggregate。

初始 aggregate roots：

1. Project
2. Task

初始 supporting records：

1. KnownUsers
2. PushSubscriptions
3. AiAuditOutbox
4. AiAuditReadModel
5. WorkflowThroughputOutbox
6. WorkflowThroughputBuckets

Recipe 必須優先採用 aggregate-aware JSON/model rewriting，而不是 blind SQL text replacement。

---

## 6. Use Cases

### 6.1 Remap RonFlow User Id

作為 operator，我想要將 RonFlow 中所有 user id A 的 reference 改寫成 user id B，讓不同裝置產生的資料可以指向同一個邏輯使用者。

輸入：

1. source user id A
2. target user id B
3. source retention policy
4. source database path
5. target database path

Source retention policies：

1. discard id A
2. preserve id A

預期行為：

1. 工具在 apply 前印出 dry-run console summary。
2. 所有由支援的 aggregate 擁有的 RonFlow user reference 都會從 id A 改寫成 id B。
3. KnownUsers 依照 source retention policy 被收斂。
4. Activity 與 audit actor references 會一起從 id A 改寫成 id B。
5. 如果 id A 被 discard，target database 中的 source KnownUsers record 會被 physical delete。

### 6.2 Remap RonFlow Project Id

作為 operator，我想要將 RonFlow 中所有 project id A 的 reference 改寫成 project id B，讓 tasks、reports 與 project-owned data 收斂到同一個 project。

輸入：

1. source project id A
2. target project id B
3. source retention policy
4. subtask template policy
5. source database path
6. target database path

Source retention policies：

1. discard project id A
2. preserve project id A

Subtask template policies：

1. use project A templates
2. use project B templates
3. merge templates
4. fail on template conflict

預期行為：

1. 工具在 apply 前印出 dry-run console summary。
2. 屬於 project A 的 tasks 可以被移動到 project B。
3. Project-owned references 與 projections 會從 id A 改寫成 id B。
4. Project subtask templates 依照選擇的 policy 處理。
5. Conflicting project metadata 會在 apply 前被印出。
6. 如果 project id A 被 discard，target database 中的 source project aggregate 會被 physical delete。

Open question：

1. 如果 project A 與 project B 都有標題與日期相似的 tasks，MVP 應該保留兩邊 tasks，還是提供 task-level duplicate detection？

### 6.3 Merge RonFlow Known Users

作為 operator，我想要合併代表同一個邏輯使用者的 duplicated KnownUsers entries，讓 project membership 與 task history 指向同一個 identity。

Identity match candidates：

1. email
2. user name
3. explicit id mapping file

預期行為：

1. Email match 必須 case-insensitive。
2. 如果 email 相同但 user name 不同，必須印出 conflict。
3. operator 可選擇 source wins、target wins 或 manual value selection。

### 6.4 Move RonFlow Tasks Between Projects

作為 operator，我想要將選定 tasks 從 project A 移動到 project B，同時保留 task content 與 activity history。

預期行為：

1. 每個被移動的 task 仍然是 Task aggregate root。
2. Task project id 會被改寫。
3. Reporting projections 會被 recalculated，或被明確標記為 stale。
4. 如果產品 model 支援 system activities，task activity timeline 應記錄 reconciliation operation。

### 6.5 Recalculate Derived Projections

作為 operator，我想要在 reconciliation 後重建 derived projections，避免 reports 保留 stale project 或 user references。

預期行為：

1. Plan 會識別 reconciliation 影響到的 projection tables。
2. operator 可以選擇 preserve、clear 或 rebuild projections。
3. 如果已有 rebuild support，MVP 可以選擇 clear-and-rebuild。

---

## 7. Interface Strategy

### 7.1 MVP Interface

MVP 應該是 console/CLI tool。

理由：

1. Reconciliation 是 operator workflow，不是日常 end-user workflow。
2. 操作應該可以 scriptable 與 reviewable。
3. Dry-run 與 apply output 可以在需要時複製到 RonFlow task。
4. CLI 可以避免在 domain 穩定前，先替 RonFlow 加上一個敏感的 mutation surface。

Example shape：

```text
ron-data-reconcile plan ronflow user-id \
  --source-db source.db \
  --target-db target.db \
  --from-user-id <idA> \
  --to-user-id <idB> \
  --source-retention discard

ron-data-reconcile apply \
  ronflow user-id \
  --target-db target.db \
  --from-user-id <idA> \
  --to-user-id <idB> \
  --source-retention discard \
  --yes
```

### 7.2 Future UI

等 CLI 與 domain model 穩定後，可以再考慮 UI。

適合 UI 化的能力：

1. plan diff viewer
2. conflict resolution wizard
3. operation history

UI 不應該繞過 CLI 使用的 plan/apply engine。

---

## 8. Non-Functional Requirements

### 8.1 Safety

1. Apply 必須要求明確 confirmation。
2. 工具必須在 apply 前印出 destructive operations。
3. 除非明確選擇 single-database repair mode，否則 source path 與 target path 相同時必須拒絕執行。
4. 工具必須驗證 database 看起來屬於選定的 product recipe。

### 8.2 Observability

1. 每次 plan 與 apply 都必須印出 console summary。
2. Console summary 必須包含 warnings、conflicts、changed aggregate ids 與 changed reference counts。
3. Failed apply 必須提供足夠資訊，讓 operator 能識別失敗步驟。

### 8.3 Idempotency

1. 重複 apply 已完成的 id remap 不應該製造 duplicate data。
2. 如果 target 看起來已經完成 reconciliation，plan 應該印出這個狀態。

### 8.4 Testability

1. Product recipe behavior 必須用 temporary SQLite database 的 infrastructure tests 覆蓋。
2. Use cases 必須包含 dry-run 與 apply tests。
3. Conflict cases 必須明確測試。

### 8.5 Portability

1. MVP 應能在 Windows 本機執行，因為 RonFlow 目前主要在 Windows/PowerShell 與 SQLite 上開發。
2. 核心 reconciliation rules 不應依賴 IIS 或 localhost hosting。

---

## 9. Open Questions

1. 這個 support domain 初期應該留在 RonFlow repository，還是第一個 slice 完成後移到 shared Ron tooling repository？
2. Conflict policy 應該用 command-line options 傳入，還是存在 recipe configuration file？
3. 如果 project A 與 project B 都有標題與日期相似的 tasks，MVP 應該保留兩邊 tasks，還是提供 task-level duplicate detection？

---

## 10. Acceptance Criteria

1. RonFlow user id remap 可以在不修改 target database 的情況下 plan。
2. RonFlow user id remap 可以在 explicit confirmation 後 apply。
3. RonFlow project id remap 可以在不修改 target database 的情況下 plan。
4. RonFlow project id remap 可以在 explicit confirmation 後 apply。
5. Project id remap plan 會顯示 subtask template policy。
6. 每個 plan 都會顯示 source retention policy。
7. Apply 在修改 target 前會印出 destructive operations。
8. Console output 會包含 changed aggregate roots、changed references、warnings 與 conflicts。
9. RonFlow recipe tests 會證明工具採用 aggregate-aware rewriting，而不是 blind text replacement。
10. RonAuth support 明確排除在 MVP 之外。
