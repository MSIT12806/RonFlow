# Actor

Actor 指的是在某個流程中發出意圖、觸發互動或導致系統行為開始的角色。它可以是人，也可以是其他系統。重點不是它「做了什麼技術操作」，而是它在業務流程裡扮演了誰、提出了什麼需求或動作。

在 [Event Storming](./event-storming.md) 或需求分析裡，Actor 常會和 [Command](./command.md) 一起看。Actor 表示「誰提出了意圖」，Command 表示「提出了什麼意圖」，接著系統才可能依規則產生 [Domain Event](./domain-event.md)。

因此，Actor 關心的是互動發起者，而不是資料表中的欄位，也不一定等於任務的長期負責人或 [Assignee](./assignee.md)。