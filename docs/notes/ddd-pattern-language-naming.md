# DDD 模式語言與命名原則

## 文件目的

本文記錄 RonFlow 後續開發時，關於 DDD / Clean Architecture 命名的一組工作原則。

這不是名詞百科，而是實作時的命名指標。

核心想法是：

> 型別名稱應該先表達它在系統中的角色，再表達它的技術實作。

如果一個名稱無法對應到清楚的模式語言，通常表示這個型別的責任還不夠清楚。

---

## 一、總原則

### 1. 先用業務語言命名，再補上模式語言

好的命名通常會長成：

```text
Project
Task
WorkflowState
TaskTitle
TaskCreated
CreateTaskApplicationService
InMemoryProjectRepository
TaskDetailView
```

也就是：

```text
業務概念 + 模式角色
```

而不是：

```text
TaskManager
ProjectHelper
BoardProcessor
TaskStore
DataHandler
```

這種名稱雖然看起來通用，但沒有說明它的責任與所屬層。

---

### 2. 名稱要能反映所在層

不同層應該使用不同的模式語言。

| 層 | 應優先使用的名稱 |
|---|---|
| Domain | Aggregate、Entity、Value Object、Domain Event、Policy、Specification、Factory、Repository（介面） |
| Application | Application Service、Command、Command Output、Query Service、View / Read Model |
| Infrastructure | InMemory / Sql / EfCore / Dapper + Repository / Gateway / Projector |
| API / Transport | Request、Response、Endpoint、Validation Error |

如果一個名稱同時混用多個層的語言，通常代表責任邊界不夠乾淨。

---

### 3. 優先避免模糊詞

以下名稱除非真的符合該語境，否則應避免：

- `Manager`
- `Helper`
- `Util`
- `Processor`
- `Engine`
- `Store`
- `Handler`

原因不是這些字不能用，而是它們太容易掩蓋責任。

例如：

- `RonFlowStore` 不如 `InMemoryProjectRepository`
- `CreateTaskHandler` 不如 `CreateTaskApplicationService`
- `BoardProcessor` 不如 `GetProjectBoardQueryService` 或 `BoardProjectionBuilder`

---

## 二、Domain 命名原則

### 2.1 Aggregate / Entity

Aggregate 與 Entity 應直接使用業務名詞。

例如：

```text
Project
Task
WorkflowState
```

不要把技術細節混進主要型別名稱。

例如避免：

```text
ProjectStateModel
TaskDataObject
WorkflowRecord
```

如果它是核心業務模型，就直接叫它業務名稱。

---

### 2.2 Value Object

Value Object 應用「概念名詞」命名，而不是驗證行為命名。

例如：

```text
ProjectName
TaskTitle
```

而不是：

```text
ValidatedProjectName
NormalizedTaskTitle
```

因為驗證與正規化是 Value Object 的責任，不需要寫進名字。

---

### 2.3 Domain Event

Domain Event 使用過去式，表示已經發生的事實。

例如：

```text
TaskCreated
TaskCompleted
TaskRejected
TaskReopened
```

避免用命令語氣或畫面語氣命名事件。

例如避免：

```text
CreateTaskEvent
DoneTask
TaskDoneButtonClicked
```

---

### 2.4 Policy / Specification / Factory

當邏輯不是單一 Entity 的自然責任時，應使用明確的模式名稱。

例如：

```text
DefaultWorkflow
TaskCompletionPolicy
WorkflowStateSpecification
ProjectFactory
```

不要用模糊名稱承載規則。

例如避免：

```text
WorkflowHelper
TaskRuleUtil
ProjectBuilderThing
```

---

### 2.5 Repository

Repository 是 Domain 對持久化邊界的抽象。

介面應以業務集合命名，例如：

```text
IProjectRepository
```

如果未來讀寫責任分離，可以進一步拆成：

```text
IProjectCommandRepository
IProjectQueryRepository
```

---

## 三、Application 命名原則

### 3.1 Application Service

當一個型別負責協調 use case、驗證輸入、操作 repository、呼叫 domain model，它應該叫 `ApplicationService`。

例如：

```text
CreateProjectApplicationService
CreateTaskApplicationService
```

在沒有 message bus、mediator pipeline 的情況下，不優先使用 `Handler`。

因為這裡處理的是「用例協調」，不是「訊息處理框架」。

---

### 3.2 Command 與 Command Output

命令相關資料應表達「執行一個動作」與「動作完成後回傳的結果」。

例如：

```text
CreateProjectCommand
CreateProjectOutput
CreateTaskOutput
```

若目前沒有獨立 command object，至少 output 名稱仍要清楚表達它是命令結果，而不是查詢 view。

因此像這樣會比較清楚：

```text
CreateTaskOutput
```

而不是：

```text
TaskDetailView
```

因為後者是查詢語言，不是命令語言。

---

### 3.3 Query Service 與 Read Model

查詢流程應直接表達它在回答什麼問題。

例如：

```text
GetProjectsQueryService
GetProjectBoardQueryService
GetTaskDetailQueryService
```

對應的輸出模型則用：

```text
ProjectListView
ProjectBoardView
TaskDetailView
```

如果用途是查詢畫面、列表、投影，就用 `View` 或 `ReadModel`。

如果用途是命令完成結果，就不要混用這組詞。

---

## 四、Infrastructure 命名原則

Infrastructure 名稱應同時說明「技術手段」與「扮演角色」。

例如：

```text
InMemoryProjectRepository
SqlProjectRepository
EfCoreProjectRepository
TaskBoardProjector
```

這比下列名稱清楚：

```text
ProjectStore
TaskPersistenceService
BoardDataManager
```

因為前者能直接告訴讀者它用什麼技術、遵守哪個模式。

---

## 五、API / Transport 命名原則

API 層應使用 transport 語言，而不是冒充 domain model。

例如：

```text
CreateProjectRequest
CreateTaskRequest
ProjectResponse
TaskDetailResponse
ValidationError
```

Request / Response 的責任是對外協定，不是承載核心業務邏輯。

因此不要讓 API DTO 長成 domain aggregate 的替身。

---

## 六、命名決策檢查表

每次新增型別時，至少檢查以下問題：

1. 這個名稱能否看出它所在的層？
2. 這個名稱能否對應到某個模式語言角色？
3. 這個名稱是在描述業務概念，還是在掩蓋責任？
4. 如果把類別內容刪掉，只看名稱，我能否推測它的責任？
5. 若我想用 `Manager`、`Helper`、`Store`、`Handler`，是否其實只是因為我還沒釐清責任？

如果答案不夠清楚，先不要寫 code，先把模型和邊界想清楚。

---

## 七、目前 RonFlow 採用的偏好

目前 RonFlow 在命名上採用以下偏好：

- Domain 使用業務名詞與模式語言，例如 `Project`、`TaskTitle`、`DefaultWorkflow`
- Application 使用 `ApplicationService`、`QueryService`、`Output`、`View`
- Infrastructure 使用 `InMemory...Repository` 這種具體命名
- API 使用 `Request` / `Response`
- 避免以 `Store`、`Manager`、`Helper` 作為預設命名

這組偏好不是教條，但在沒有更強理由前，應視為預設命名原則。