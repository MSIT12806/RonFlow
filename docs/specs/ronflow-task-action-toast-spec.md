# RonFlow 任務操作後端通知與 Toast 規格

## 範圍

任務成功變更 workflow 狀態，或成功移到垃圾桶時，系統應由後端狀態變更事件驅動前端顯示短暫的成功 Toast。

通知流程固定為：

1. 前端送出 task mutation request。
2. 後端完成授權、Domain mutation 與 persistence。
3. 後端 dispatch `TaskWorkflowStateChangedDomainEvent` 或 `TaskMovedToTrashDomainEvent`。
4. Domain Event handler 將通知寫入 task notification outbox。
5. Background Service 以 polling 消費 outbox，經 SignalR session group 發送 `taskNotification`。
6. 前端收到 `taskNotification` 後才顯示 Toast。

前端 mutation request 的成功回應本身不得直接觸發成功 Toast。

## 行為

- 後端發出 workflow state changed notification：顯示「任務流程已更新」，並指出目標欄位。
- 後端發出 moved to trash notification：顯示「任務已刪除」及「任務已移到垃圾桶」。
- API 操作失敗：不顯示成功 Toast，沿用既有的看板或任務錯誤訊息。

## 驗收

- workflow 拖曳操作完成後，收到後端 `taskNotification` 才顯示成功 Toast。
- 詳細資訊送入 Flow 完成後，收到後端 `taskNotification` 才顯示成功 Toast。
- 從詳細資訊移到垃圾桶完成後，收到後端 `taskNotification` 才顯示成功 Toast。
- 後端 outbox/background polling/SignalR 發送失敗時，不得將未送出的通知標記為 processed。
