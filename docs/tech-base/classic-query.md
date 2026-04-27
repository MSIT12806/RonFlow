# Classic Query

Classic Query 指的是直接從目前系統使用的資料表或查詢模型讀取資料，而不是先把事件轉成額外的投影，再提供查詢。

這種做法常見於一般 CRUD 或分層架構系統。它的優點是簡單、直接、容易落地，對於看板、列表或明細頁等常見查詢需求，往往已經足夠。

Classic Query 不代表沒有 [Domain Event](./domain-event.md)。系統仍然可以保留事件來表達業務事實，只是日常查詢先不依賴 [Event Projection](./event-projection.md) 或 [Event Sourcing](./event-sourcing.md) 來組裝畫面資料。