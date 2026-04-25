# Outside-In

Outside-In 是一種從外部行為往內部設計推進的開發思路。它通常會先從使用者真正看得見、碰得到的入口開始，例如畫面互動、API 呼叫或驗收情境，再逐步往內部的應用協調、領域規則與技術實作收斂。

這種做法的核心，不是把介面擺在最前面而已，而是讓開發從使用者價值出發。當團隊先釐清外部要交付的行為，就比較不容易一開始就陷入框架、資料表或內部結構的細節，卻忘了系統究竟要解決什麼問題。

Outside-In 常會和 [ATDD](./atdd.md)、[BDD](./bdd.md)、[Acceptance Criteria](./acceptance-criteria.md) 或 [Integration Test](./integration-test.md) 一起出現，因為這些方法都很重視從需求與可驗證結果往下推進設計。
