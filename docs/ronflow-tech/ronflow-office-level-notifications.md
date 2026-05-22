# RonFlow 的 office system level 通知實踐

## 為什麼這篇文章值得寫
很多待辦或流程工具都只做到畫面上的提醒提示，但 RonFlow 已經往 office system level notification 走了一步，也就是把提醒送到瀏覽器作業系統層級，而不是只停留在頁面內部。

這件事很值得記錄，因為它不是單一 API 的使用，而是前端、Service Worker、後端推播、提醒排程共同協作的結果。

## 這個技術概念是什麼
所謂 office system level notification，可以理解成：
- 即使使用者沒有停留在當前頁面內容，也能透過瀏覽器通知收到提醒
- 通知可以被點擊，並把使用者帶回相關工作項目
- 提醒交付不只是畫面 UI 效果，而是跨越到裝置層級的使用者體驗

RonFlow 目前選擇的實作方式是 Web Push + Service Worker。

## 它背後的設計精神
### 1. 提醒不應只是資料欄位
如果 reminder 只是 task 底下一筆資料，而沒有交付機制，那它的價值是有限的。真正有用的是提醒能到達使用者。

### 2. 通知交付是跨邊界能力
這不是單靠前端或單靠後端能完成的事情。需要前端裝置註冊、service worker 接收通知、後端保存 subscription 並在適當時間發送。

### 3. 使用者體驗與系統設計要一起考慮
通知權限、裝置支援、訂閱是否存在、點擊通知後導向哪裡，這些都會影響實際體驗。

## 這樣做的優點
### 1. 提醒更接近真實工作場景
使用者不必一直停在頁面裡才能知道提醒到了。

### 2. 前端與後端合作更有展示價值
這個功能能完整展示瀏覽器能力、API、資料儲存、背景發送與 UX 是如何串起來的。

### 3. 為更完整通知系統打底
之後若要加通知中心、通知偏好、不同通知管道，現在這條交付鏈會是基礎。

## 代價與限制
### 1. 裝置與瀏覽器支援度有限
並不是所有裝置與環境都支援 Push API、Notification API、Service Worker。

### 2. 需要權限與訂閱管理
使用者拒絕權限、訂閱失效、裝置切換，都會影響通知可用性。

### 3. Web Push 本身就有技術整合成本
VAPID key、訂閱序列化、推播送達失敗處理，都不是零成本。

## RonFlow 裡是怎麼實作的
### 前端：用 composable 管理提醒通知啟用流程
RonFlow 前端有專門的 `usePushNotifications` composable，負責：
- 檢查目前環境是否支援通知與推播
- 向使用者請求通知權限
- 註冊 Service Worker
- 建立或取得 Push Subscription
- 把 subscription 回傳到後端註冊

（`usePushNotifications.initializePushNotifications`、`usePushNotifications.enableReminderDelivery`、`ensureSubscriptionRegistered`、`PushNotificationCommandService.getPublicKey`、`PushNotificationCommandService.registerSubscription`）

### 前端：Service Worker 接收推播並顯示通知
Service Worker 會接住 `push` 事件，把 payload 轉成系統通知；通知被點擊時，再把使用者帶回 RonFlow 相關頁面。

（`service-worker.js` 的 `self.addEventListener('push', ...)` 與 `self.addEventListener('notificationclick', ...)`）

### 後端：保存訂閱資訊並發送通知
後端保存 push subscription，並在提醒到期時透過 push sender 把通知送出。這代表提醒不是只是前端定時輪詢，而是真正由伺服器發動交付。

（`RegisterPushSubscriptionCommandService.Register`、`DeliverDueReminderNotificationsCommandService.DeliverDueReminders`）

### UI：讓提醒與通知能力有清楚狀態
RonFlow 也不是只做出一個按鈕，而是把通知能力是否可用、是否已授權、是否成功啟用，整理成可以回饋給使用者的狀態訊息。

## 這件事對 RonFlow 代表什麼
這個功能讓 RonFlow 從單純的流程管理畫面，往更接近辦公系統級應用的方向前進。它證明 RonFlow 不只是 CRUD 工具，而是已經開始處理跨裝置體驗、提醒交付與背景能力。

## 總結
RonFlow 的 office system level 通知實踐，價值不在於「有用 Web Push」，而在於把提醒真正做成一條從前端裝置、到後端交付、再回到使用者體驗的完整鏈。這是很有代表性的全端整合題目，也是 RonFlow 目前相當有辨識度的技術實踐之一。
