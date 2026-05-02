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
- Docker
- NUnit
- SQLite
- PostgreSQL

## 開發環境需求

建議先安裝以下工具：

- Node.js 22 LTS 或更新版本
- npm 10+ 或相容套件管理工具
- .NET 10 SDK
- Docker Desktop
- PostgreSQL 16+（若不使用 Docker 啟動資料庫）

## 建置說明

### 前端建置

code-frontend 已初始化完成，可使用以下流程：

```powershell
Set-Location code-frontend
npm install
npm run dev
```

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

### 後端建置

code-backend 已建立 `RonFlow.Api` 與 `RonFlow.Api.Tests`，可使用以下流程：

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