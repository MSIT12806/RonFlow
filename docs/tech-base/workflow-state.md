# Workflow State

Workflow State 指的是一筆工作項目在流程中目前所處的位置，例如 Backlog、Ready、Active、Review、Done 或 Canceled。它的核心目的是讓團隊知道這件事在整體流程裡走到哪裡，而不是只記錄某個欄位值被改掉。

當系統允許使用者自訂流程時，Workflow State 往往不應該被寫死成固定名稱。不同團隊可能把相近狀態命名成「開發中」、「處理中」或「施工中」，但系統仍然可能需要透過某種狀態分類去理解它們大致屬於 [Backlog](./backlog.md)、進行中、檢查中或完成等語意。

因此，Workflow State 常會和 [Workflow](./workflow.md) 的設計綁在一起。前者是某個項目當下的流程位置，後者則是整體流程如何流動、有哪些狀態與轉換規則。
