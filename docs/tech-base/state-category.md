# State Category

State Category 指的是系統用來理解 [Workflow State](./workflow-state.md) 大致語意的分類。它回答的不是「這個狀態叫什麼名字」，而是「這個狀態在流程語意上屬於哪一類」。

當使用者可以自訂 Workflow State 名稱時，單靠名稱往往不足以讓系統正確判斷狀態。例如不同團隊可能把狀態命名為「開發中」、「處理中」、「施工中」或「QA」，但系統仍然需要知道它們分別屬於進行中、檢查中或完成等類型。

因此，State Category 通常會和 Workflow State 分開建模。Workflow State 保留團隊自己的語言，State Category 則提供系統可穩定理解的語意，例如 NotStarted、Active、Review、Done 或 Canceled。