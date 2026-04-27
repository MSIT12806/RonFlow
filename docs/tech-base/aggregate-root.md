# Aggregate Root

Aggregate Root 是 [Aggregate](./aggregate.md) 對外暴露的唯一入口。當系統想修改某個 Aggregate 內的狀態時，應該透過 Aggregate Root 來接收 [Command](./command.md)、檢查規則，並決定是否產生 [Domain Event](./domain-event.md)。

它的價值在於保護一致性邊界。外部物件不應直接繞過 Root 去修改內部 [Entity](./entity.md)，否則 Aggregate 想維持的業務規則就容易被破壞。

因此，Aggregate Root 不是單純的「最上層物件」而已，而是負責守住整個 Aggregate 規則的決策入口。