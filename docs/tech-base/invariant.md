# Invariant

Invariant 指的是在某個模型或一致性邊界內必須始終成立的條件。只要系統允許狀態改變，就不能讓這些條件被破壞。

它和一般的 Business Rule 很接近，但語氣通常更強。Business Rule 可以是某個操作成立前要檢查的規則；Invariant 則更像是模型無論經歷多少次變化，都應該被維持的基本事實。

在設計 [Aggregate](./aggregate.md) 或處理 [Command](./command.md) 時，Invariant 很重要，因為它能幫助團隊判斷哪些規則必須在同一個一致性範圍內被保護，而不能留到事後補救。
