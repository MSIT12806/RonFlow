# RonFlow Tech

這個目錄用來整理 RonFlow 的技術實踐文章。

它和 [RonFlow 技術結構亮點](../specs/RonFlow-%E6%8A%80%E8%A1%93%E7%B5%90%E6%A7%8B%E4%BA%AE%E9%BB%9E.md) 是互相對應的：
- 「技術結構亮點」負責回答 RonFlow 有哪些值得展現的工程結構
- `ronflow-tech` 負責把其中重要項目的實踐細節、設計取捨與落地方式寫清楚

如果新增了一個值得展示的技術結構，應同步考慮兩件事：
- 是否要在「技術結構亮點」補上一條高層摘要
- 是否要在 `ronflow-tech` 補上一篇或擴寫既有文章，說明它在程式碼中的實踐細節

目標不是只記錄「用了什麼技術」，而是把以下幾件事說清楚：
- 該技術或方法的概念是什麼
- 它背後的設計精神是什麼
- 它帶來哪些好處與代價
- RonFlow 實際是怎麼落地的

## 文章索引
- RonFlow 的 spec-first + acceptance-first Outside-In 開發流程：`ronflow-spec-first-outside-in.md`
- RonFlow 的 Clean Architecture 實踐：`ronflow-clean-architecture.md`
- RonFlow 的 DDD 實踐：`ronflow-ddd.md`
- RonFlow 的 workflow state / lifecycle state 雙狀態軸設計：`ronflow-task-state-axes.md`
- RonFlow 的 CQRS 實踐：`ronflow-cqrs.md`
- RonFlow 的 reporting / projection 實踐：`ronflow-reporting-projection.md`
- RonFlow 的多人協作 / 即時同步設計：`ronflow-real-time-collaboration.md`
- RonFlow 的 cross-cutting pattern language：`ronflow-cross-cutting-pattern-language.md`
- RonFlow 的壓力測試與 observability 實踐：`ronflow-load-testing-observability.md`
- RonFlow 的活動紀錄 / audit trail 雛形：`ronflow-activity-audit-trail.md`
- RonFlow 的 AI discovery surface：`ronflow-ai-discovery-surface.md`
- RonFlow 的 AI interaction surface：`ronflow-ai-interaction-surface.md`
- RonFlow 的 Testing-only HTTP fault injection 實踐：`ronflow-testing-http-fault-injection.md`
- RonFlow 的 office system level 通知實踐：`ronflow-office-level-notifications.md`
- RonFlow 的 Background Service 實踐：`ronflow-background-service.md`
- RonFlow x RonAuth supporting domain 結合互動：`ronflow-ronauth-domain-integration.md`
