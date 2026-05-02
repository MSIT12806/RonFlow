# Q&A

## 如何確保驗收測試不會被迂迴通過？

### Q

我現在已經完成我專案的規格專寫與 前端 playwrite 的驗收測試案例。但我想問，如果我使用假的儲存方式，像是只用前端的 storage 去儲存使用者建立的 project 或者 task 這樣也很有可能可以通過 驗收測試，對吧？ 這樣要怎麼辦呢?

### A

你擔心的是對的：

如果 Playwright 只測畫面，假儲存也可能通過驗收。

解法不是放棄 Playwright，而是：

1. 正式 Playwright 必須跑真後端 API。
2. 加 API / Integration tests 驗證後端與資料庫。
3. 加 Application / Domain tests 驗證規則。
4. Definition of Done 明確禁止 fake storage 作為正式完成依據。
5. fake storage 只允許用於 UI prototype，不算完成。

一句話：

Playwright 負責證明使用者流程可用；Integration / Application / Domain tests 負責證明系統真的正確。