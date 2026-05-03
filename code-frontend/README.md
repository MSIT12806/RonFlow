# RonFlow Frontend

這個目錄是 RonFlow 的前端專案，已使用下列技術完成初始化：

- Vue 3
- TypeScript
- Vite
- Tailwind CSS
- PrimeVue
- Playwright

## 常用指令

安裝相依套件：

```powershell
npm install
```

啟動開發伺服器：

```powershell
npm run dev
```

預設情況下，Vite 會把 `/api` 代理到 `http://127.0.0.1:5078`。
請先在另一個終端機啟動後端：

```powershell
Set-Location ..\code-backend
dotnet run --project .\RonFlow.Api\RonFlow.Api.csproj --urls http://127.0.0.1:5078
```

正式建置：

```powershell
npm run build
```

執行 E2E 測試：

```powershell
npx playwright install chromium
npm run test:e2e
```

Playwright 會自動啟動：

- 後端 API (`RonFlow.Api`)
- 前端 Vite dev server

開啟 Playwright UI：

```powershell
npm run test:e2e:ui
```

## 目前內容

- 專案列表、建立專案、建立任務、任務詳細資訊流程
- 前端透過真後端 API 載入 Project / Board / Task Detail
- PrimeVue 主題與元件整合
- Tailwind CSS 樣式管線
- Playwright 真後端 E2E 驗證

