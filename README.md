# RonFlow

RonFlow 是一個以 C# 與 ASP.NET Core 為核心的小型專案管理工具。

## 技術棧

### 前端

- Vue 3
- TypeScript
- Vite
- Tailwind CSS
- PrimeVue
- Playwright

### 後端

- ASP.NET Core Web API on .NET 10 LTS
- NUnit
- SQLite
- PostgreSQL

## 開發環境需求

建議先安裝以下工具：

- Node.js 22 LTS 或更新版本
- npm 10+ 或相容套件管理工具
- .NET 10 SDK

若要執行目前的前後端整合流程，不需要先安裝資料庫。

## 建置說明

### 前端建置

code-frontend 已初始化完成，可使用以下流程：

```powershell
Set-Location code-backend
dotnet run --project .\RonFlow.Api\RonFlow.Api.csproj --urls http://127.0.0.1:5078
```

另一個終端機中啟動前端：

```powershell
Set-Location code-frontend
npm install
npm run dev
```

Vite 開發伺服器會將 `/api` 代理到 `http://127.0.0.1:5078`。

正式建置：

```powershell
Set-Location code-frontend
npm install
npm run build
```

若已加入 Playwright 測試設定：

```powershell
Set-Location code-frontend
npx playwright install chromium
npm run test:e2e
```

Playwright 目前會自動啟動：

- 前端 Vite dev server
- 後端 RonFlow.Api

### 後端建置

code-backend 已建立下列專案：

- `RonFlow.Domain`
- `RonFlow.Application`
- `RonFlow.Infrastructure`
- `RonFlow.Api`
- `RonFlow.Api.Tests`

可使用以下流程：

```powershell
Set-Location code-backend
dotnet restore .\RonFlow.sln
dotnet build .\RonFlow.sln
dotnet run --project .\RonFlow.Api\RonFlow.Api.csproj
```

執行 NUnit 測試：

```powershell
Set-Location code-backend
dotnet test .\RonFlow.Api.Tests\RonFlow.Api.Tests.csproj
```