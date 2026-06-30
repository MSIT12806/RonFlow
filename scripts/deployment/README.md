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

## 一鍵 UAC 提權部署

若 localhost 部署需要寫入 `C:\inetpub` 或修復 IIS applications，但你不想手動開啟「系統管理員」terminal，可直接雙擊：

```text
scripts\deployment\Start-LocalhostDeploy.cmd
```

`.cmd` 入口會保留視窗，方便你看到錯誤、exit code 與 log 路徑。

也可以從一般 terminal 執行：

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\deployment\Start-LocalhostDeploy.ps1
```

這個入口會跳出 Windows UAC。你同意後，它會在 elevated 子程序中執行：

```powershell
Deploy-LocalhostSites.ps1 -EnsureIisApplications -StopIisHosting -SkipFrontendInstall
```

執行紀錄會寫到：

```powershell
$env:LOCALAPPDATA\RonFlow\localhost-deploy\latest-self-elevated.log
```

最近一次執行狀態會寫到：

```powershell
$env:LOCALAPPDATA\RonFlow\localhost-deploy\self-elevated-last-run.json
```

## 非管理員執行

這支腳本的預設路徑以「非管理員可執行」為目標，但有兩個前提：

- `C:\inetpub\ronauth-api`、`C:\inetpub\ronflow-api`、`C:\inetpub\ronflow-web` 已存在且目前使用者有 Modify 權限。
- `/ronauth-api` 與 `/ronflow-api` IIS applications 已經建立好；初次建立或修復 IIS application 需要系統管理員權限。

在上述前提成立時，腳本會：

- 會先停止腳本自己管理的 API 程序，避免 publish 目標被舊程序鎖住。
- 在 `IisApplications` 模式下，預設不會先停 IIS。
- 對 IIS 站台中的 ASP.NET Core 應用，腳本會在 publish 前暫時放置 `app_offline.htm`，讓 IIS 釋放檔案鎖。

也就是說，若你只是要更新已授權的 localhost 站台內容，預設不需要用系統管理員權限執行。若腳本回報部署目標不可寫，代表目前 `C:\inetpub` ACL 不允許一般 terminal 寫入，請改用 elevated PowerShell、排程工作，或一次性調整目標資料夾 ACL。

## 何時才需要停 IIS

只有在你明確要停 IIS site / app pools，或要搭配 `-EnsureIisApplications` 建立/修復 IIS application 時，才使用 `-StopIisHosting`：

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\deployment\Deploy-LocalhostSites.ps1 -EnsureIisApplications -StopIisHosting
```

這條路徑需要系統管理員權限，因為它會直接操作 IIS site 與 app pool。

## 以排程工作執行完整 IIS 部署

若一般 VS Code terminal 沒有系統管理員權限，可以先安裝一個本機排程工作。安裝時會要求 UAC 同意；安裝完成後，之後可從一般 terminal 觸發完整 localhost 部署，並讓部署流程具備 IIS stop/start 權限。

安裝排程工作：

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\deployment\Install-LocalhostDeployScheduledTask.ps1
```

如果機器上同時有 Microsoft Store 版 PowerShell 與一般 MSI 安裝版 PowerShell，安裝腳本會優先使用 `C:\Program Files\PowerShell\7\pwsh.exe`。若你要指定固定 runtime，可傳入：

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\deployment\Install-LocalhostDeployScheduledTask.ps1 -PwshPath "C:\Program Files\PowerShell\7\pwsh.exe"
```

觸發完整部署：

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\deployment\Invoke-LocalhostDeployScheduledTask.ps1
```

查看最近一次執行結果：

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\deployment\Invoke-LocalhostDeployScheduledTask.ps1 -ShowLastRun
```

排程工作預設會執行：

```powershell
Deploy-LocalhostSites.ps1 -EnsureIisApplications -StopIisHosting -SkipFrontendInstall
```

執行紀錄會寫到：

```powershell
$env:LOCALAPPDATA\RonFlow\localhost-deploy\latest.log
```

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
