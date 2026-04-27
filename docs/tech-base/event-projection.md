# Event Projection

Event Projection 是把一連串業務事件轉換成可查詢資料的過程。它通常會消化 [Domain Event](./domain-event.md)，再把結果整理成某種 [Read Model](./read-model.md)、報表資料或搜尋索引。

Projection 的重點是「把事件整理成適合讀取的形狀」。因此它和事件本身不是同一件事，也不一定意味著系統全面採用 [Event Sourcing](./event-sourcing.md)。

很多系統會先用 [Classic Query](./classic-query.md) 解決大部分查詢，只有在統計、聚合或效能需求變複雜時，才針對特定場景加入 Event Projection。