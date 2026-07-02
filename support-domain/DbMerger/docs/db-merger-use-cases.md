# DbMerger Use Cases

## 1. 文件定位

本文件整理 DbMerger 第一輪 TDD 的 use cases。每個 use case 都應對應到 acceptance test，並作為後續 domain / infrastructure implementation 的工作順序。

---

## 2. Use Case List

### UC-001 Merge Different Records

**Goal**

將 local-only records 與 remote-only records 合併到同一份 merged snapshot。

**Primary Actor**

呼叫端 sync workflow。

**Preconditions**

```text
1. Local snapshot schema valid。
2. Remote snapshot schema valid。
3. Local 與 remote 的 records identity 不重複。
```

**Main Flow**

```text
1. Caller 建立 merge request。
2. DbMerger 載入 local / remote snapshots。
3. DbMerger 依 Keyed Union Rule 合併 records。
4. DbMerger 輸出 merged snapshot。
5. DbMerger 回傳 succeeded merge result 與 report。
```

**Acceptance Test**

`DbMergerAcceptanceTests.Merge_KeyedUnionRecords_WritesLocalAndRemoteRows`

### UC-002 Resolve Same Record Conflict With Local-Win

**Goal**

當 local 與 remote 有相同 identity 但內容不同時，caller 可用 LocalWinPolicy 決定保留 local record。

**Acceptance Test**

`DbMergerAcceptanceTests.Merge_SameIdentityConflict_WithLocalWin_WritesLocalRow`

### UC-003 Resolve Same Record Conflict With Remote-Win

**Goal**

當 local 與 remote 有相同 identity 但內容不同時，caller 可用 RemoteWinPolicy 決定保留 remote record。

**Acceptance Test**

`DbMergerAcceptanceTests.Merge_SameIdentityConflict_WithRemoteWin_WritesRemoteRow`

### UC-004 Detect RonFlow Identity Drift

**Goal**

RonFlow recipe 應偵測 KnownUsers 中相同 email 但不同 UserId 的 identity drift，不應在 built-in local-win / remote-win 下自動改寫 foreign keys。

**Acceptance Test**

`DbMergerAcceptanceTests.Merge_RonFlowKnownUsersIdentityDrift_FailsWithUnresolvedConflict`

### UC-005 Preserve Input Snapshots

**Goal**

Merge session 不得修改 local snapshot 或 remote snapshot。

**Acceptance Test**

`DbMergerAcceptanceTests.Merge_DoesNotModifyInputSnapshots`

---

## 3. First Implementation Slice

第一個 implementation slice 應只完成 UC-001 到 UC-003 的 generic keyed records recipe。

第二個 slice 再加入 RonFlow KnownUsers identity drift。

第三個 slice 才進入 RonFlow Projects / Tasks aggregate JSON merge。
