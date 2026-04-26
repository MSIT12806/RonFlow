# Command

Command 指的是某個角色對系統提出的明確意圖，例如建立任務、變更狀態、指派負責人或標記緊急。它描述的是「希望系統做什麼」，而不是「系統已經發生了什麼」。

在 [Event Storming](./event-storming.md) 與應用層設計中，Command 常常會觸發驗證規則、狀態轉換與模型行為；當規則成立後，系統才可能產生對應的 [Domain Event](./domain-event.md)。

因此，Command 和 Domain Event 不應混為一談。Command 是意圖，Domain Event 是已經成立的業務事實。