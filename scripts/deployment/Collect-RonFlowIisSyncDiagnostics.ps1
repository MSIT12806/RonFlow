[CmdletBinding()]
param(
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function ConvertTo-RedactedText {
    param([AllowNull()][string]$Text)

    if ($null -eq $Text) {
        return $null
    }

    $redacted = $Text
    $redacted = [regex]::Replace(
        $redacted,
        '(?is)(name\s*=\s*"[^"]*(?:token|password|secret|privatekey)[^"]*"\s+value\s*=\s*")[^"]*',
        '$1***')
    $redacted = [regex]::Replace(
        $redacted,
        '(?is)(value\s*=\s*")[^"]*("\s+name\s*=\s*"[^"]*(?:token|password|secret|privatekey)[^"]*")',
        '$1***$2')
    $redacted = [regex]::Replace(
        $redacted,
        '(?is)((?:token|password|secret|privatekey)\s*=\s*")[^"]*',
        '$1***')
    $redacted = [regex]::Replace($redacted, 'github_pat_[A-Za-z0-9_]+', 'github_pat_***', 'IgnoreCase')
    $redacted = [regex]::Replace($redacted, 'https://[^@\s]+@github\.com', 'https://***@github.com', 'IgnoreCase')
    return $redacted
}

function Write-Section {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    "`r`n===== $Name =====" | Add-Content -LiteralPath $reportPath -Encoding utf8
    try {
        $result = & $Action 2>&1 | Out-String
        (ConvertTo-RedactedText $result).TrimEnd() | Add-Content -LiteralPath $reportPath -Encoding utf8
    }
    catch {
        "ERROR: $($_.Exception.Message)" | Add-Content -LiteralPath $reportPath -Encoding utf8
    }
}

if (-not (Test-IsAdministrator)) {
    throw 'Please right-click Collect-RonFlowIisSyncDiagnostics.cmd and choose Run as administrator.'
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $env:LOCALAPPDATA "RonFlow\diagnostics\iis-sync-$timestamp"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$reportPath = Join-Path $OutputDirectory 'report.txt'
"RonFlow IIS SQLite-to-Git diagnostics collected at $(Get-Date -Format o)" | Set-Content -LiteralPath $reportPath -Encoding utf8
"This report redacts access tokens, passwords, secrets, and credential-bearing GitHub URLs." | Add-Content -LiteralPath $reportPath -Encoding utf8

$appCmd = Join-Path $env:WINDIR 'System32\inetsrv\appcmd.exe'
if (-not (Test-Path -LiteralPath $appCmd)) {
    throw "IIS appcmd was not found at $appCmd."
}

$apiRoot = 'C:\inetpub\ronflow-api'
$appData = Join-Path $apiRoot 'App_Data'
$repositoryPath = Join-Path $appData 'ronflow-db-repository'
$runtimeDatabase = Join-Path $appData 'ronflow.db'
$repositoryDatabase = Join-Path $repositoryPath 'ronflow.db'
$diagnosticLog = Join-Path $appData 'database-git-sync.log'

Write-Section 'Machine and execution identity' {
    [pscustomobject]@{
        ComputerName = $env:COMPUTERNAME
        UserName = [Security.Principal.WindowsIdentity]::GetCurrent().Name
        PowerShell = $PSVersionTable.PSVersion.ToString()
        CollectedAt = (Get-Date).ToString('o')
    } | Format-List
}

Write-Section 'IIS applications' {
    & $appCmd list app /config /xml
}

Write-Section 'IIS app pools' {
    & $appCmd list apppool /config /xml
}

Write-Section 'IIS worker processes and application mapping' {
    & $appCmd list wp
    & $appCmd list app /config /xml
}

Write-Section 'Effective ASP.NET Core configuration for RonFlow applications' {
    $applicationsXml = (& $appCmd list app /config /xml) -join [Environment]::NewLine
    $ronFlowApps = [regex]::Matches($applicationsXml, 'APP\.NAME="(?<name>[^"]*ronflow[^"]*)"', 'IgnoreCase') |
        ForEach-Object { $_.Groups['name'].Value } |
        Sort-Object -Unique
    if (-not $ronFlowApps) {
        'No IIS application whose name contains RonFlow was found.'
        return
    }

    foreach ($app in $ronFlowApps) {
        "--- $app ---"
        & $appCmd list config $app /section:system.webServer/aspNetCore /config /xml
    }
}

Write-Section 'RonFlow persistence files' {
    Get-Item -LiteralPath $runtimeDatabase, $repositoryDatabase, $diagnosticLog -ErrorAction SilentlyContinue |
        Select-Object FullName, Length, LastWriteTimeUtc |
        Format-Table -AutoSize
}

Write-Section 'Database sync diagnostic log tail' {
    if (Test-Path -LiteralPath $diagnosticLog) {
        Get-Content -LiteralPath $diagnosticLog -Tail 180
    }
    else {
        "Not found: $diagnosticLog"
    }
}

Write-Section 'ASP.NET Core stdout log tail' {
    $stdoutLogs = Get-ChildItem -LiteralPath (Join-Path $apiRoot 'logs') -Filter 'stdout*' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 3
    if (-not $stdoutLogs) {
        'No stdout log files found.'
        return
    }

    foreach ($log in $stdoutLogs) {
        "--- $($log.FullName) ---"
        Get-Content -LiteralPath $log.FullName -Tail 160
    }
}

Write-Section 'Local database sync repository state' {
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryPath '.git'))) {
        "Not a Git repository: $repositoryPath"
        return
    }

    git --git-dir=(Join-Path $repositoryPath '.git') rev-parse main
    git --git-dir=(Join-Path $repositoryPath '.git') rev-parse refs/remotes/origin/main
    git --git-dir=(Join-Path $repositoryPath '.git') log -5 --format='%h %ad %an %s' --date=iso main
    Get-Content -LiteralPath (Join-Path $repositoryPath '.git\config')
}

Write-Section 'Application event log entries from the last two hours' {
    $since = (Get-Date).AddHours(-2)
    Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = $since } -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ProviderName -match 'IIS|ASP.NET|\.NET Runtime' -or
            $_.Message -match 'RonFlow|database Git sync|DatabaseSync'
        } |
        Select-Object -First 120 TimeCreated, ProviderName, Id, LevelDisplayName, Message |
        Format-List
}

Write-Host "Diagnostics collected: $reportPath"
Write-Host 'Please share report.txt (it redacts credentials), or share only the Effective ASP.NET Core configuration, IIS worker processes, and log-tail sections.'
