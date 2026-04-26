# Business Rule

Business Rule 指的是某個領域或流程裡必須被遵守的業務規則。它描述的是「在業務上什麼可以成立、什麼不可以成立」，而不是單純的畫面提示或技術實作細節。

在需求分析、[Event Storming](./event-storming.md) 或 Behavior Mapping 裡，Business Rule 常會和 [Command](./command.md) 一起討論。當某個 Command 被提出時，系統需要先確認相關規則是否滿足，只有成立時才會產生對應的 [Domain Event](./domain-event.md)。

因此，Business Rule 比較接近業務語意層級的限制。它有時候會表現成驗證條件，但重點不在輸入格式，而在這個操作在業務上是否合理、是否允許。
