# Value Object

Value Object 是用來表達概念與規則的模型，但它本身不依賴身分識別。系統關心的是它代表的值是否相同，而不是它是不是同一筆資料。

和 [Entity](./entity.md) 不同，Value Object 通常不需要獨立 ID。像是優先度、金額、座標、日期區間或某種狀態資訊，都很適合用 Value Object 來封裝，讓規則集中在同一個地方。

在設計上，Value Object 常用來減少散落的基本型別判斷，讓模型更能直接表達業務語意。