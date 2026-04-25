# Infrastructure

Infrastructure 指的是系統中承接外部技術細節的部分，例如資料庫、檔案系統、訊息佇列、HTTP 呼叫、身分驗證、框架整合或雲端資源存取。這些能力本身很重要，但通常不應該直接主導核心業務規則。

在分層設計裡，Infrastructure 往往扮演支援角色，負責把系統需要的技術能力接上來，例如把資料持久化、把事件送出去，或把外部服務的回應轉成內部可以使用的形式。

把 Infrastructure 與 [Application Layer](./application-layer.md) 或 [Domain Model](./domain-model.md) 適度分開，主要是為了降低耦合。這樣在技術選型變動時，比較不會連核心規則都被迫一起扭曲。
