# Behavior Mapping

Behavior Mapping 指的是把使用者或系統的意圖、操作成立前的規則，以及最後發生的業務事實串成一條可討論的行為路徑。常見的整理方式會把 [Command](./command.md)、Business Rule 與 [Domain Event](./domain-event.md) 放在一起看，確認系統真正要支撐的是什麼行為。

在事件風暴之後，Behavior Mapping 很適合拿來把發散出的事件往可實作的方向收斂。它可以幫助團隊判斷：誰會發出命令、命令要滿足哪些限制、成功後會留下哪些事件，以及哪些地方還有規則需要澄清。

因此，Behavior Mapping 比較像是從事件探索走向設計與驗收之間的橋梁，而不是單純再把事件重新列一次。
