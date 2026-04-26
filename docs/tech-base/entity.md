# Entity

Entity 指的是在系統中具有持續身分識別的模型。即使它的屬性隨時間變動，只要它仍然是「同一個東西」，就通常會被建模為 Entity。

在 [Domain Modeling](./domain-modeling.md) 或 DDD 脈絡裡，Entity 的重點不是資料長什麼樣，而是它是否有自己的生命週期、狀態變化與業務規則。例如 Task、Project、User 這類對象，往往都不只是資料結構，而是需要持續被辨識與操作的實體。

因此，Entity 關心的是身分與規則，而不只是欄位集合。它也常常是產生 [Domain Event](./domain-event.md) 或承擔狀態轉換的主要模型。