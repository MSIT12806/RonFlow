# Event Storming

Event Storming 是一種以事件為中心的需求探索與領域建模方法。它通常會從「已經發生了什麼業務事實」開始，把流程中的重要事件攤開，再一步一步回推是誰觸發、為什麼會發生、前面需要哪些條件，以及哪些模型要承接這些規則。

它的價值在於，團隊不是一開始就急著畫資料表、切 API 或決定資料夾結構，而是先把業務語意談清楚。當事件、命令、角色與規則之間的關係被看見後，後面的 [Domain Modeling](./domain-modeling.md)、邊界切分與系統設計通常會更穩定。

在實務上，Event Storming 常會產出一串事件流、待釐清問題與幾個候選模型。這些結果不一定直接等於最終實作，但能有效幫助團隊辨識哪些是真正重要的 [Domain Event](./domain-event.md)，哪些只是畫面操作、資料異動或技術層細節。

一個事件風暴通常會有以下流程：

1. 事件發散
2. 最小流程排序
3. 事件語意釐清與分類
4. 例外流程與狀態語意收斂
5. Command → Event Mapping
6. Actor / Role Mapping
7. Business Rules / Invariants
8. Aggregate / Entity 候選
9. Read Model / Backlog / ADR 整理

或是也可以

1. Event Discovery：事件發散與最小流程
2. Event Refinement：事件語意分類與例外流程
3. Behavior Mapping：Command、Actor、Business Rules
4. Modeling & Delivery：Aggregate、Read Model、Backlog、ADR