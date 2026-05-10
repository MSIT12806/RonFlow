# RonFlow Frontend 程式碼分層說明

## 1. 文件定位

這份文件說明 `code-frontend` 目前的前端分層方式，以及每個目錄應該承擔的責任。

它不是在列出「所有可能的 Vue 專案分法」，而是描述 RonFlow 這個專案現在採用的切法，讓後續新增功能時有一致的放置原則。

目前 RonFlow 前端的主幹依賴方向可以先讀成：

```text
main.ts
  -> App.vue
    -> components/
    -> composables/
      -> api/
```

這條線代表的意思是：

1. `main.ts` 負責啟動 app 與註冊 UI framework。
2. `App.vue` 負責組裝頁面骨架。
3. `components/` 負責畫面區塊、互動元件與事件發送。
4. `composables/` 負責畫面狀態、流程控制、資料載入與提交行為。
5. `api/` 負責和後端 API 溝通，並整理 request / response 與錯誤模型。

## 2. 從目前程式碼看實際分層

### 2.1 App 組裝層

`src/App.vue` 是目前畫面的 composition root。

它本身不直接打 API，而是做兩件事：

1. 匯入頁面需要的視覺元件，例如 `ProjectSidebar`、`ProjectBoard`、`CreateProjectModal`。
2. 呼叫 `useRonFlowBoard()` 取得頁面需要的 state 與 actions，再透過 props / events 串接到元件。

這代表 `App.vue` 的責任是「接線」與「組裝」，不是承接所有業務邏輯。

### 2.2 畫面流程層

`src/composables/useRonFlowBoard.ts` 是目前最核心的頁面流程層。

它集中管理：

1. 專案列表、目前選中的專案、看板欄位、任務詳細資訊。
2. modal 開關、loading 狀態、submit 狀態、表單輸入值、錯誤訊息。
3. `loadProjects()`、`loadBoard()`、`submitProject()`、`submitTask()`、`moveTaskToState()` 這種跨元件的互動流程。
4. 後端錯誤轉成前端可呈現的 page error 或 field error。

這表示 composable 在 RonFlow 裡不是「小工具集合」，而是偏向 page-level 或 feature-level 的應用流程協調者。

### 2.3 API 邊界層

`src/api/` 目前已經明確切成幾種角色：

| 檔案 | 角色 |
|---|---|
| `request.ts` | 最底層 HTTP request 包裝，統一處理 base url、header、錯誤轉換。 |
| `projects.ts` | 與 Project 相關的 API 呼叫。 |
| `tasks.ts` | 與 Task 相關的 API 呼叫。 |
| `types.ts` | 前端使用的 API response 型別。 |
| `errors.ts` | API error 模型，例如 validation error、request error。 |
| `ronflowApi.ts` | barrel file，統一 re-export 給上層使用。 |

這一層的重點是「把 HTTP 細節關在邊界內」，讓 `composables/` 看到的是 typed function，而不是零散的 `fetch()` 呼叫。

### 2.4 UI 元件層

`src/components/` 放的是可以被 `App.vue` 組裝起來的 UI 區塊：

| 元件 | 主要責任 |
|---|---|
| `ProjectSidebar.vue` | 顯示專案清單、專案選取入口。 |
| `ProjectBoard.vue` | 顯示 workflow columns、task card、drag and drop 互動。 |
| `CreateProjectModal.vue` | 建立專案表單與對話框 UI。 |
| `CreateTaskModal.vue` | 建立任務表單與對話框 UI。 |
| `TaskDetailModal.vue` | 任務詳細資訊 UI。 |

以 `ProjectBoard.vue` 為例，它可以持有與畫面互動強相關的暫時性狀態，例如 drag 狀態；但它不負責決定如何載入整個 board，也不直接決定 API 失敗時要如何重整資料。

## 3. 各目錄應該放什麼

### 3.1 `src/components/`

這裡放的是「畫面可見的區塊或元件」。

適合放進 `components/` 的內容：

1. template 結構與樣式 class。
2. props / emits 定義。
3. 和 UI 呈現高度耦合的互動細節，例如 focus、drag state、modal 開關事件、局部顯示切換。
4. 只服務於單一畫面區塊的格式化或 view helper。

不應優先放進 `components/` 的內容：

1. 直接呼叫後端 API 的程式。
2. 跨多個元件共享的 page state。
3. 載入流程、重試策略、送出後刷新資料這類 application flow。
4. 後端錯誤轉譯與表單 validation mapping。

簡單判斷法：

如果拿掉畫面後，這段程式仍然是在描述「這個頁面怎麼運作」，那它多半不該留在 component，而應移到 composable。

### 3.2 `src/composables/`

這裡放的是「畫面流程邏輯」與「可被 Vue 組合式 API 消費的狀態協調器」。

適合放進 `composables/` 的內容：

1. 多個 component 共用的 `ref` / `computed` 狀態。
2. 首次載入、重新整理、送出表單、切換選擇這類流程控制。
3. 呼叫 `api/` 後，把 response 整理成畫面需要的狀態。
4. 把 technical error 轉成 UI 可用的錯誤訊息。
5. 對外提供給 component 的 actions，例如 `submitTask()`、`selectProject()`。

不應優先放進 `composables/` 的內容：

1. 純 HTTP request 細節。
2. 與 Vue 無關、也沒有狀態的單純型別定義。
3. 只屬於單一 component 的 DOM 細節。

目前 RonFlow 的慣例更接近：

1. 一個 composable 代表一個 page slice 或一個完整 feature。
2. composable 可以知道使用者流程與頁面錯誤文案。
3. composable 可以依賴 `api/`，但不要反向讓 `api/` 知道 composable 或 Vue state。

### 3.3 `src/api/`

這裡放的是「與後端邊界有關的程式」。

適合放進 `api/` 的內容：

1. API endpoint 對應函式。
2. request helper。
3. response DTO / type。
4. API 專用錯誤物件。
5. 依照資源或 use case 分檔的 client module。

不應優先放進 `api/` 的內容：

1. Vue `ref`、`computed`、`onMounted`。
2. component props / emits。
3. 畫面文案排版或 DOM event。
4. 哪個 modal 要開、哪個 panel 要顯示等 UI 決策。

這層的目標是讓上層可以用像 `getProjects()`、`createTask()` 這種語意化函式，而不是在 UI 裡散落 endpoint 字串。

### 3.4 `src/assets/`

這裡放的是由前端程式碼直接 import 的靜態資產，例如圖片、插圖、icon source 或元件需要 bundling 的素材。

如果資產要透過 ESM import 進入 Vue / CSS，應優先放這裡。

### 3.5 `public/`

這裡放的是不需要經過模組匯入、希望以固定 public path 提供的靜態檔案。

如果資產需要被原樣輸出，或用固定 URL 直接引用，適合放在 `public/`。

### 3.6 `src/style.css`

這裡是全域樣式入口，適合放：

1. app shell 等全域 layout。
2. theme token 或跨元件共用的視覺基礎設定。
3. 不屬於單一 component 的整體視覺規則。

若樣式只服務於單一元件，應先考慮留在該 `.vue` 檔內，而不是全部堆到全域樣式。

### 3.7 `src/main.ts`

這裡是 bootstrap 層，只負責：

1. 建立 Vue app。
2. 註冊 PrimeVue 與 theme。
3. 掛載全域樣式。
4. mount 根元件。

不要把畫面流程或 feature 狀態塞進 `main.ts`。

### 3.8 `tests/e2e/`

這裡放的是從使用者視角出發的驗收流程。

目前 `ronflow-ui-ux-acceptance.spec.ts` 已經在驗證：

1. 建立專案。
2. 建立任務。
3. 任務卡拖拉換欄。
4. 錯誤狀態與空狀態。

這層不是前端 runtime 的一部分，但它反過來約束了 `App -> components -> composables -> api` 這條鏈應該表現出的使用者行為。

## 4. 實務放置原則

新增程式碼時，可以用下面幾個問題快速判斷要放哪裡：

1. 這段程式是在描述「畫面怎麼長、怎麼互動」嗎？如果是，先考慮 `components/`。
2. 這段程式是在描述「這個頁面怎麼載入、送出、切換與同步狀態」嗎？如果是，先考慮 `composables/`。
3. 這段程式是在描述「如何呼叫後端、如何解析 response、如何包裝 request error」嗎？如果是，先考慮 `api/`。
4. 這段程式是否會讓下層反過來依賴上層？如果會，通常代表分層放錯位置。

目前 RonFlow 比較適合維持以下依賴規則：

```text
components -> composables -> api
```

補充原則：

1. `components/` 可以 emit event 給上層，但不要自己決定整個頁面的資料同步策略。
2. `composables/` 可以整合多個 API 呼叫，但不要回頭操作特定 DOM 細節。
3. `api/` 只處理邊界，不承擔 Vue 畫面生命週期。

## 5. 針對目前 RonFlow 的一句話摘要

RonFlow 前端目前採用的是一種很務實的薄 UI 分層：`App.vue` 負責組裝、`components/` 負責呈現與局部互動、`composables/` 負責頁面流程與狀態協調、`api/` 負責後端邊界，`tests/e2e/` 則從使用者行為驗證整條流程沒有走歪。