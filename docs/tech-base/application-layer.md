# Application Layer

Application Layer 指的是系統中負責協調用例、安排流程與驅動工作完成的那一層。它通常不承載最核心的商業規則，而是負責把外部輸入轉成系統內部的操作步驟，並呼叫適當的領域物件或服務來完成任務。

這一層常見的責任包括接收命令或查詢、控制交易邊界、協調多個物件合作，以及把結果整理成外部需要的回應格式。它像是系統的指揮層，決定一次操作要怎麼走，但不應該把所有規則都塞在自己身上。

當系統採用 [Layered Architecture](./layered-architecture.md) 時，Application Layer 往往位在介面層與 [Domain Model](./domain-model.md) 之間，並透過 [Infrastructure](./infrastructure.md) 提供的技術能力完成資料存取或外部整合。
