# RonFlow 的 spec-first + acceptance-first Outside-In 開發流程

## 為什麼這篇文章值得寫
RonFlow 不是先寫一堆程式，再回頭整理需求，而是刻意讓規格、驗收測試、實作形成一條有順序的鏈。

這件事值得寫成文章，因為它不是單純的流程偏好，而是整個系統能否持續演進的基礎。當需求會一直變，bounded context 會逐漸長大，若沒有一個清楚的 Outside-In 開發方法，程式很快就會退化成只是在追 bug 與追畫面。

## 這個技術概念是什麼
這裡說的不是單一方法，而是一組互相支撐的方法：
- SDD：先把產品行為寫成規格
- ATDD：先寫驗收條件與驗收測試
- TDD：在局部實作中用更細粒度的測試收斂設計
- Outside-In：從使用者看得到的行為開始，一路往 application、domain、infrastructure 推進

對 RonFlow 而言，這不是理論口號，而是實際的工作順序。

## 它背後的設計精神
### 1. 規格不是文件附屬品，而是產品行為的來源
如果規格只是寫給人看的摘要，它很快就會失效。RonFlow 的做法比較接近把 spec 當作 intended behavior 的源頭，後面的 acceptance test 與 production code 應該往 spec 對齊。

### 2. 驗收測試先描述結果，不先描述內部實作
Outside-In 的好處在於，團隊會先問「使用者最後看到什麼」，而不是先問「我要不要先建 repository 或 DTO」。這能避免一開始就被技術切面綁住。

### 3. TDD 的價值是收斂內部設計，不是替代驗收測試
RonFlow 不是只靠單元測試描述整個系統，而是把 TDD 放在較小的應用服務、domain 邏輯、邊界條件上，用來補齊 acceptance test 無法提供的設計回饋。

## 這樣做的優點
### 1. 需求與程式的對齊成本比較低
因為規格先行，驗收測試又直接對應使用者行為，當需求改動時，比較容易知道哪些地方需要跟著動。

### 2. 不容易在一開始就做過度設計
Outside-In 迫使實作者先解決當前真正需要被看見的行為，因此可以避免一開始就鋪太多抽象層。

### 3. 測試的責任分工比較清楚
驗收測試驗的是使用者流程是否成立；較小粒度的測試驗的是局部邏輯是否穩定。這兩層搭起來，比只押一邊更穩。

## 代價與限制
### 1. 規格品質會直接影響實作品質
如果 spec 寫得模糊，後面的驗收測試與實作就容易漂移。

### 2. 一開始會覺得比較慢
先寫 spec、先寫 acceptance test，短期看起來比直接下 code 慢，但長期來看能減少大量回頭修正的成本。

### 3. 需要願意維持文件與測試的一致性
如果 spec 不更新、驗收測試也不更新，那流程就只剩表面形式。

## RonFlow 裡是怎麼實作的
### 先以規格描述產品邊界
RonFlow 把核心流程與後續延伸方向寫在 `docs/specs` 底下，讓產品意圖先被明文化。

### 再把規格落成 acceptance tests
前端 E2E 與 API integration tests 承接了大量驗收責任。像登入後才可進入 workspace、專案列表載入與錯誤狀態、owner scope 等，都不是事後補測，而是直接用來驅動修改方向（`project-list.screen.acceptance.spec.ts` 的 `未登入時首頁顯示登入與註冊入口`、`專案列表首屏顯示建立專案入口與空狀態`，以及 `ProjectApiIntegrationTests.GetProjects_WhenMultipleUsersHaveProjects_ReturnsOnlyCurrentUsersProjects`、`ProjectApiIntegrationTests.GetBoard_WhenProjectBelongsToAnotherUser_ReturnsAccessDenied`）。

### 再往內收斂到 application / domain
當 acceptance test 告訴我們行為應該如何時，實作才一路往內補 application service、query service、command service、aggregate 與 persistence（`CreateProjectCommandService.Create`、`GetProjectsQueryService.Get`、`Project.Create`、`SqliteProjectRepository.Add` / `Update`）。

這種順序在 RonAuth 整合、owner boundary、task lifecycle、push reminder 等功能上都看得到。

## 這件事對 RonFlow 代表什麼
RonFlow 的價值不只是功能堆疊，而是它正逐步變成一個可以展示開發方法論的作品。spec-first + acceptance-first Outside-In，使它不只是「有做出功能」，而是能說明「這些功能是如何被有秩序地做出來的」。

## 總結
RonFlow 的 SDD、ATDD、TDD 並不是三個彼此分離的標籤，而是一條從 spec 到驗收到實作的鏈。Outside-In 把這條鏈的方向固定下來，讓系統的演進更能對齊真實使用者流程，而不是只對齊內部技術方便性。
