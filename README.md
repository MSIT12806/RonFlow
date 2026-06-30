# RonFlow

RonFlow 是一個以 C# 與 ASP.NET Core 為核心的小型專案管理工具。

## 第一次進專案先看哪裡

若是第一次進入這個專案，建議固定依照下面順序閱讀：

1. 先看這份 README，理解專案定位、開發流程與啟動方式。
2. 若你是要接入 RonFlow 的 AI agent，接著看 [docs/RonflowSkills.md](docs/RonflowSkills.md)，取得本地登入入口與 AI bootstrap 起點。
3. 再看 [docs/specs/ronflow-core-flow-spec.md](docs/specs/ronflow-core-flow-spec.md)，這是目前 RonFlow 行為的 living spec。
4. 接著看對應的 [code-frontend/tests/e2e](code-frontend/tests/e2e) 驗收測試，確認 spec 哪些已被 acceptance tests 承接、哪些還沒有。
5. 最後再進入對應的 production code，比對前端畫面、API、application service 與 domain model 是否已經完整落地。

建議工作順序也是同一條鏈：spec -> acceptance tests -> production code，而不是直接從程式碼反推需求。

## 文件定位

以下文件可視為目前較可信的入口與定位方式：

- Canonical product behavior： [docs/specs/ronflow-core-flow-spec.md](docs/specs/ronflow-core-flow-spec.md)
- AI agent bootstrap entry： [docs/RonflowSkills.md](docs/RonflowSkills.md)
- Canonical technical explanations： [docs/ronflow-tech/ronflow-technical-structure-highlights.md](docs/ronflow-tech/ronflow-technical-structure-highlights.md)、[docs/ronflow-tech](docs/ronflow-tech)

若文件之間出現衝突，優先以 active spec 與目前 acceptance tests 為準；devlog 與歷史文件主要用來理解設計思路，不應直接視為目前行為真相。

## 開發流程

RonFlow 採用 spec-first 的開發流程。

本專案的日常開發也依賴 RonFlow 本身的專案管理流程。不論是規格調整、功能增修或 Bug 修復，都應先在 RonFlow 中建立對應 task，再依照該 task 內定義的完成步驟往下執行。

1. 團隊決議要增加或修改系統功能時，先在 RonFlow 建立對應 task，並確認 task 內的完成步驟與追蹤資訊。
2. 接著修改 spec 文件，明確描述系統應該如何運作。
3. 再撰寫驗收測試，以驗收測試對齊 spec 文件。
4. 之後採用 ATDD 方式開發，先讓驗收測試逐步通過，再完成所有 production code。
5. 開發與重構期間，程式碼必須持續對齊 spec 與 task 定義的完成順序，而不是反過來把 spec 當成目前程式碼行為的翻譯。

這個專案以 spec 作為「系統應該要如何」的指引，也以 RonFlow task 作為工作編排與追蹤入口；預期工作方式是先開 task、再改 spec，最後讓測試與程式碼對齊 spec。

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

## Localhost 自動部署

若要把 RonFlow / RonAuth API 與 RonFlow frontend 一次部署到 `http://localhost`，請使用 [scripts/deployment/Deploy-LocalhostSites.ps1](scripts/deployment/Deploy-LocalhostSites.ps1)。

正式要求：

- 這支腳本請用 PowerShell 7 (`pwsh`) 執行。
- Windows PowerShell 5.1 不再視為支援的執行環境。

原因是 localhost 部署流程會啟動多個原生程序並執行 frontend production build；這條路徑已在 PowerShell 7 驗證可正常完成，但在 Windows PowerShell 5.1 下曾出現 host 與原生程序互動異常，導致腳本無法穩定跑完。

使用方式與參數說明請看 [scripts/deployment/README.md](scripts/deployment/README.md)。

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
