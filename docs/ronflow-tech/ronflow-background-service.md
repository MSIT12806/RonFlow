# RonFlow 的 Background Service 實踐

## 為什麼這篇文章值得寫
Background service 在很多系統裡都是真正讓產品從「有人打 API 才會動」進化成「系統自己會主動運作」的關鍵。RonFlow 已經有這個雛形，而且它和提醒通知功能直接結合，因此很值得被單獨寫成文章。

## 這個技術概念是什麼
Background service 指的是系統內持續在背景執行的工作，不需要使用者每次都發出請求才開始動作。

在 RonFlow 目前的案例裡，它負責的是：
- 定期掃描是否有到期 reminder
- 觸發 reminder notification delivery

也就是把「時間到了就要做事」這種責任，從使用者操作流程裡抽離出來。

## 它和 DDD 有什麼關係
Background service 本身不是 DDD 的必要條件。

DDD 真正關心的是：
- domain model 是否清楚
- business rule 是否放在正確邊界
- bounded context 是否切分合理
- 領域事件是否能表達重要狀態變化

也就是說，沒有 background service，仍然可以做 DDD；沒有 message queue，也仍然可以做 DDD。

但在實務上，DDD 常常會走到一個階段：
- 某些行為屬於 core transaction，需要強一致
- 某些後續反應不需要跟主交易同時完成
- 某些資訊需要被其他 bounded context 或外部系統知道

這時候就很容易出現 domain event、eventual consistency、message queue，以及負責消費事件或執行排程工作的 background service。

可以把它理解成：
- DDD 幫你整理「哪些事是重要的領域變化」
- domain event 幫你表達「剛剛發生了什麼事」
- message queue 幫你把這些變化送到其他流程
- background service 則常常負責在背景處理這些後續工作

所以兩者的關係不是「DDD 一定需要 background service」，而是「當 DDD 系統開始處理非同步協作、跨 context 通知、或較弱一致性的後續流程時，background service 很常成為落地手段之一」。

## 它背後的設計精神
### 1. 系統不該只在使用者點按鈕時才工作
如果 reminder 的送達完全依賴使用者剛好打開頁面，那提醒功能就不可靠。

### 2. 主動型流程應該獨立於互動型流程
建立提醒、查看任務、修改專案，是互動型 use case；到期檢查與通知交付，則是系統主動型流程。這兩者不應混在同一個請求生命週期裡。

### 3. 背景工作也需要明確邊界
Background service 不應該自己塞滿所有邏輯，而是應該調用 application service，把責任維持清楚。

## 這樣做的優點
### 1. 提醒功能更可靠
即使使用者沒有剛好刷新頁面，系統仍然能主動處理到期提醒。

### 2. 系統開始具備主動性
這是很多真正業務系統都需要的能力，也是和單純 CRUD 工具拉開差距的一步。

### 3. 為未來更多背景流程打基礎
之後若要做報表預算、同步作業、清理工作、排程處理，background service 會是自然延伸點。

## 代價與限制
### 1. 需要考慮輪詢、效能與穩定性
背景工作會持續執行，若設計不好，可能浪費資源或造成不必要負載。

### 2. 失敗處理不能只靠 UI
背景工作失敗時，使用者未必正在畫面上，因此 logging 與監控就會變重要。

### 3. 單機背景服務與分散式部署考量不同
現在的 RonFlow 可以用單機背景服務處理，但若未來橫向擴展，背景作業的協調方式會更複雜。

### 4. 若進一步走向 domain event 與 MQ，系統複雜度會上升
一旦背景工作不只是時間輪詢，而是開始承接事件消費、重試、冪等、死信處理、跨服務通知，整體系統會更強，但也會更複雜。

## RonFlow 裡是怎麼實作的
RonFlow 後端有一個 `ReminderNotificationBackgroundService`，它繼承 `BackgroundService`，並用固定 polling interval 持續運作（`ReminderNotificationBackgroundService.ExecuteAsync`）。

它的責任很單純：
- 以 `PeriodicTimer` 定期醒來
- 呼叫 `DeliverDueReminderNotificationsCommandService`
- 若失敗就記錄錯誤 log

這裡最好的地方是，background service 沒有自己直接碰大量業務邏輯。真正的處理仍然放在 application service，讓背景排程只做「定時觸發」這件事。

（`DeliverDueReminderNotificationsCommandService.DeliverDueReminders`）

如果從 DDD 角度看，RonFlow 目前這一版比較像是「時間驅動的背景流程」，還不是「事件驅動的背景流程」。

也就是說，目前的設計是：
- task 與 reminder 的業務資料仍然在 domain model 中維持（`Task.Reminders`、`Task.GetDueUndispatchedReminders`、`Task.MarkReminderNotificationDispatched`）
- background service 定期檢查哪些 reminder 已到期
- application service 再負責把提醒轉成通知交付

這代表 RonFlow 已經踏進「非同步後續處理」的世界，但還沒有正式引入 MQ 或 domain event bus。

如果未來 RonFlow 要再往前走，可能會演進成：
- Task Reminder Created
- Task Reminder Due
- Reminder Notification Requested
- Reminder Notification Delivered / Failed

這些事件可以由 queue 傳遞，再由背景 consumer 處理。到那時，background service 就不只是輪詢器，而會更接近事件處理器。

## 這件事對 RonFlow 代表什麼
有了 background service，RonFlow 的系統能力就不再只靠 request-response 驅動。這使得提醒通知不只是個欄位或畫面功能，而是真正具備時效性與主動交付能力的系統功能。

更重要的是，它也證明 RonFlow 已經開始處理「系統內部流程」而不是只處理「使用者畫面流程」。從 DDD 視角看，這也表示 RonFlow 已經開始面對 core transaction 之外的後續流程，只是目前先用單機背景服務處理，而不是直接跳到 MQ 與完整事件驅動架構。

## 總結
RonFlow 的 background service 實踐，展示的是系統如何從被動反應使用者操作，走向主動執行時間相關工作。從 DDD 的角度看，它也對應到一個常見現實：當系統開始需要處理較弱一致性的後續反應時，background service 往往就是第一個落地工具。

RonFlow 目前還沒有走到完整的 domain event + message queue 架構，但它已經有了一個很合理的前置階段：先把背景流程與主互動流程分開，等未來真的需要跨 context 非同步協作時，再把這條路往事件驅動延伸。
