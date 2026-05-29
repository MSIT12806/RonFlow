# Localhost Deployment

`Deploy-LocalhostSites.ps1` 會把下列三個輸出一起部署到本機 localhost 環境：

- `RonAuth.Api`
- `RonFlow.Api`
- `RonFlow` frontend

## 執行需求

- 必須使用 PowerShell 7 (`pwsh`) 執行。
- 不支援 Windows PowerShell 5.1。
- 需要已安裝 `.NET 10 SDK`、`Node.js` 與 `npm`。

PowerShell 7 是正式要求，不只是建議值。這支腳本會跨越 `dotnet publish`、API 啟動與 frontend production build；這條整合路徑已在 PowerShell 7 完整驗證，但在 Windows PowerShell 5.1 下曾出現 native process / host 相容性問題，導致腳本無法穩定完成部署。

## 基本用法

從 repository root 執行：

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\deployment\Deploy-LocalhostSites.ps1
```

若已經安裝過 frontend dependencies，可略過 `npm ci`：

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\deployment\Deploy-LocalhostSites.ps1 -SkipFrontendInstall
```

## 非管理員執行

這支腳本的預設路徑以「非管理員可執行」為目標：

- 會先停止腳本自己管理的 API 程序，避免 publish 目標被舊程序鎖住。
- 在 `IisApplications` 模式下，預設不會先停 IIS。
- 對 IIS 站台中的 ASP.NET Core 應用，腳本會在 publish 前暫時放置 `app_offline.htm`，讓 IIS 釋放檔案鎖。

也就是說，若你只是要更新 localhost 站台內容，預設不需要用系統管理員權限執行。

## 何時才需要停 IIS

只有在你明確要停 IIS site / app pools 時，才使用 `-StopIisHosting`：

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\deployment\Deploy-LocalhostSites.ps1 -StopIisHosting
```

這條路徑需要系統管理員權限，因為它會直接操作 IIS site 與 app pool。

## 成功訊號

腳本成功完成時，會輸出：

- `Deployment completed.`
- RonAuth API / RonFlow API / RonFlow Frontend 的部署目錄
- build version 與 updated timestamp

Frontend 部署成功後，可再檢查：

```powershell
Get-Content C:\inetpub\ronflow-web\build-info.json
```

以及：

```powershell
Invoke-WebRequest http://localhost/
```