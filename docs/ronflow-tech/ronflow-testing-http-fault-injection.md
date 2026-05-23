# RonFlow 的 Testing-only HTTP fault injection 實踐

## 為什麼這篇文章值得寫
RonFlow 在 E2E 測試裡，有一種很容易讓人誤會的技巧：
- 前端測試看起來像是在測真實 API 失敗
- 但測試本身又會先打一個 testing 專用 endpoint

如果只看表面，會很像是「為了測錯誤處理，又多造了一條不屬於產品流程的 API」。

這篇文章要說清楚的是：
- 這個技巧到底解決什麼問題
- 它和前端 `page.route` mock 有什麼差別
- 真正被測的是哪一條 request
- middleware 在 pipeline 裡如何攔截並改寫 response

## 這個技術概念是什麼
RonFlow 現在的做法，不是讓前端直接 mock 某個 request，也不是把業務 controller 改成測試特化版本。

它的做法是：
- 在 Testing 環境下提供一條 testing 專用 API
- 先由測試把 fault 規則寫進後端記憶體 store
- 當真正的業務 request 進來時，由 middleware 根據目前使用者、HTTP method、path pattern 判斷是否命中 fault
- 若命中，就直接由 middleware 回傳指定的 status code、message 或 delay

所以它測到的是：
- 前端真的有送出原本的 API request
- auth / route / middleware / controller pipeline 真的有被走到
- 前端收到真實 HTTP failure 後，會怎麼更新 UI

## 它背後的設計精神
### 1. 測試前提與被測行為分開
`/api/testing/faults` 的角色不是被測行為，而是測試前提設定。

真正被測的，仍然是使用者操作 UI 後送出的 `/api/...` request。

### 2. 讓失敗發生在 backend pipeline 內，而不是瀏覽器外層
如果用 `page.route`，request 根本不會進到 RonFlow backend。

如果用 testing fault middleware，request 仍然會：
- 帶著真實 token 進來
- 經過 authentication
- 進入 RonFlow 的 ASP.NET Core pipeline

差別只在於它會在 controller 前面被受控地截斷。

### 3. 讓 E2E 能測 integration，但不把每個 failure case 都綁死在 domain 條件上
有些錯誤處理測試，想驗的是 UI 對 500、404、409 的反應，不一定想為了每個 case 都構造一個真實且複雜的 domain 前提。

這時候 testing-only fault injection 可以提供一個折衷：
- 比前端 mock 更接近真實 request 流程
- 比真的去製造每一種後端錯誤前提更便宜

## 這樣做的優點
### 1. 前端仍然在打真實 API
E2E 不是攔截瀏覽器 request 後自製 response，而是讓 request 真正進後端。

### 2. 可以測 loading、error、stale state 等 UI 行為
除了 status code，也可以用 `delayMs` 測按鈕鎖定、loading state。

### 3. fault 是 per-user 的，不容易互相污染
RonFlow 的 fault store 以目前登入 userId 當 key，不是全域切換開關。

## 代價與限制
### 1. 它不是在驗證 controller 本來就會回這個錯誤
如果 middleware 命中 fault，controller 根本不會執行。

所以這種 E2E 不能拿來證明某個 controller 在真實 domain 條件下會自然回 500 或某段 message。

### 2. 這不是 strict one-shot fault
目前實作是 `ReplaceFaults` 與 `ClearFaults` 模式。

也就是 fault 會持續存在，直到被新的規則覆蓋或被 clear，而不是只吃掉下一次 request。

### 3. generic UI error message 往往不是 backend 原文
很多前端顯示的錯誤訊息，其實是 UI 自己把 500/404/409 映射成穩定文案。

所以這個技巧比較像是在測：
- 真 request 失敗時 UI 是否進入正確狀態

而不是測：
- backend 原始 message 是否和畫面文案完全一樣

## RonFlow 裡是怎麼實作的
### 後端：TestingController 只負責寫入 fault 規則
`TestingController` 不處理產品功能，而是讓測試把 fault 規則寫入 store。

```csharp
[HttpPut]
public IResult ReplaceFaults([FromBody] ReplaceTestHttpFaultsRequest request, [FromServices] ITestHttpFaultStore faultStore)
{
    if (!TryGetCurrentUserId(out var currentUserId))
    {
        return Results.Unauthorized();
    }

    faultStore.Replace(currentUserId, request.Items.Select(item => item.ToFault()).ToArray());
    return Results.Ok();
}
```

這裡的意思是：
- 先從目前 JWT 取出 userId
- 用這個 userId 當 key
- 把這個使用者對應的 fault 規則整批替換掉

### 後端：fault 規則會記錄 method、path pattern、status 與 delay
RonFlow 的 fault model 長這樣：

```csharp
public sealed record TestHttpFault(string Method, string PathPattern, int? StatusCode, int DelayMs, string Message);
```

它代表一條規則至少定義：
- 哪個 HTTP method
- 哪個 path pattern
- 要不要延遲
- 要不要直接回某個 status code
- response body 裡的 message 是什麼

### 後端：middleware 在 controller 前面攔截 request
真正讓 fault 生效的，不是 `TestingController`，而是 `TestHttpFaultMiddleware`：

```csharp
var fault = faultStore.Match(userId, context.Request.Method, context.Request.Path.Value ?? string.Empty);
if (fault is not null)
{
    if (fault.DelayMs > 0)
    {
        await Task.Delay(fault.DelayMs, context.RequestAborted);
    }

    if (fault.StatusCode is not null)
    {
        context.Response.StatusCode = fault.StatusCode.Value;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message = fault.Message }, cancellationToken: context.RequestAborted);
        return;
    }
}

await next(context);
```

這段有兩種模式：
- `DelayMs > 0` 且 `StatusCode == null`：只延遲，最後仍繼續進 controller
- `StatusCode != null`：直接回應並 `return`，controller 不再執行

### 後端：middleware 被掛在 authentication 後、controller 前
RonFlow 的 pipeline 順序是：

```csharp
app.UseAuthentication();
app.UseMiddleware<CurrentUserDirectorySyncMiddleware>();
app.UseMiddleware<TestHttpFaultMiddleware>();
app.UseAuthorization();
app.MapControllers();
```

這代表：
- request 先完成 authentication
- middleware 才能知道目前是哪個 user
- fault middleware 在 controller 前面判斷是否要截斷 request

## 用 Mermaid 看清楚 middleware pipeline
下面這張圖把「設定 fault」和「真正被測的業務 request」拆成兩條線：

```mermaid
flowchart TD
    A[Playwright 測試先呼叫 PUT /api/testing/faults] --> B[TestingController 取得目前 userId]
    B --> C[ITestHttpFaultStore.Replace 寫入 fault 規則]
    C --> D[測試開始操作 UI]
    D --> E[前端照常送出原本的業務 request 例如 POST /api/invitations/{id}/accept]
    E --> F[UseAuthentication 驗證 JWT]
    F --> G[CurrentUserDirectorySyncMiddleware]
    G --> H[TestHttpFaultMiddleware 依 userId + method + path 查 fault]
    H --> I{有命中 fault?}
    I -- 否 --> J[UseAuthorization]
    J --> K[MapControllers]
    K --> L[真正的業務 controller 執行]
    I -- 是 --> M{StatusCode 是否有值?}
    M -- 否 --> N[先延遲 DelayMs]
    N --> J
    M -- 是 --> O[Middleware 直接回指定 status code 與 message]
    O --> P[前端收到真實 HTTP failure 並更新 UI]
    L --> Q[controller 回正常 response]
    Q --> R[前端更新 UI]
```

這張圖的關鍵不是 pipeline 很長，而是要看懂兩件事：

1. `PUT /api/testing/faults` 只是寫規則，不是被測業務流程。
2. 真正被測的 request 還是原本那條 `/api/...`，只是它在 controller 前面被 middleware 受控地攔截。

## 用一個實際測試案例看
例如 invitation inbox 的 E2E 會先設定：

```ts
await configureTestFaultsThroughApi(request, memberSession, [{
  method: 'POST',
  pathPattern: '/api/invitations/*/accept',
  statusCode: 500,
  message: 'accept failed',
}])
```

接著才真的去點畫面上的「接受邀請」：

```ts
await page.getByRole('button', { name: '接受邀請', exact: true }).click()
```

所以這個案例真正被測的不是 `/api/testing/faults`，而是：
- 使用者點按鈕後
- 前端真的送 `POST /api/invitations/{id}/accept`
- backend middleware 回 500
- 前端把這個 failure 轉成 UI 上的錯誤訊息

## 它和前端 `page.route` mock 有什麼差別
### `page.route` 的特性
- request 在瀏覽器層就被攔掉
- backend 根本不會收到這個 request
- 比較適合純前端狀態機測試

### testing-only fault middleware 的特性
- request 真的會進後端 pipeline
- auth、middleware、route 都是真的
- 比較適合驗證 integration 邊界仍然存在時，UI 對失敗的反應

## 這個技巧在 RonFlow 的定位
這個技巧最適合拿來做：
- loading / error / disabled 等 UI 異步狀態測試
- 真實 request path 與 auth 邊界仍要保留的 E2E
- 不值得為每個 failure case 建完整 domain 前提的情境

但它不應取代：
- backend API integration tests
- 驗證真實 domain 規則回應內容的測試

也就是說，它是一個介於前端 request mock 與完整後端業務失敗重建之間的測試技巧。

## 總結
RonFlow 的 Testing-only HTTP fault injection，不是在把產品流程偷偷換成另一條測試專用 API，而是在測試前先對 backend 寫入一組 fault 規則，讓真正的 `/api/...` request 進入 middleware 後被受控地延遲或失敗。

理解這件事的關鍵是分清楚兩個角色：
- `TestingController` 負責安排測試前提
- `TestHttpFaultMiddleware` 負責在真實 request pipeline 裡套用這個前提

這樣就能同時保留真實 request 的 integration 邊界，又避免為了每一種錯誤情境都去建非常重的後端前提。