[CmdletBinding()]
param(
  [string]$Configuration = 'Release',
  [string]$DeploymentRoot = 'C:\inetpub',
  [string]$RonAuthTargetPath,
  [string]$RonFlowApiTargetPath,
  [string]$RonFlowWebTargetPath,
  [ValidateSet('IisApplications', 'DirectPorts')]
  [string]$ApiAccessMode = 'IisApplications',
  [string]$IisSiteName = 'Default Web Site',
  [string]$IisAppPoolName = 'DefaultAppPool',
  [string]$RonAuthAppPoolName,
  [string]$RonFlowApiAppPoolName,
  [string]$RonAuthAppPath = '/ronauth-api',
  [string]$RonFlowApiAppPath = '/ronflow-api',
  [switch]$EnsureIisApplications,
  [string]$RonAuthOrigin = 'http://localhost:5136',
  [string]$RonFlowApiOrigin = 'http://localhost:5078',
  [string]$RonFlowWebOrigin = 'http://localhost',
  [string]$BuildVersion,
  [string]$BuildUpdatedAtUtc,
  [switch]$StopIisHosting,
  [switch]$SkipApiStart,
  [switch]$SkipFrontendInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$workspaceRoot = Split-Path -Parent $repoRoot
$ronAuthRepoRoot = Join-Path $workspaceRoot 'RonAuth'
$ronAuthProjectPath = Join-Path $ronAuthRepoRoot 'code-backend\RonAuth.Api\RonAuth.Api.csproj'
$ronFlowApiProjectPath = Join-Path $repoRoot 'code-backend\RonFlow.Api\RonFlow.Api.csproj'
$frontendRoot = Join-Path $repoRoot 'code-frontend'
$frontendDistPath = Join-Path $frontendRoot 'dist'
$resolvedBuildVersion = if ([string]::IsNullOrWhiteSpace($BuildVersion)) {
  (Get-Date).ToUniversalTime().ToString('yyyyMMdd.HHmmss')
}
else {
  $BuildVersion
}
$resolvedBuildUpdatedAtUtc = if ([string]::IsNullOrWhiteSpace($BuildUpdatedAtUtc)) {
  [DateTimeOffset]::UtcNow
}
else {
  [DateTimeOffset]::Parse($BuildUpdatedAtUtc).ToUniversalTime()
}

function Get-AbsolutePath {
  param([Parameter(Mandatory = $true)][string]$Path)

  if ([System.IO.Path]::IsPathRooted($Path)) {
    return [System.IO.Path]::GetFullPath($Path)
  }

  return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

if (-not $PSBoundParameters.ContainsKey('RonAuthTargetPath')) {
  $RonAuthTargetPath = Join-Path $DeploymentRoot 'ronauth-api'
}

if (-not $PSBoundParameters.ContainsKey('RonFlowApiTargetPath')) {
  $RonFlowApiTargetPath = Join-Path $DeploymentRoot 'ronflow-api'
}

if (-not $PSBoundParameters.ContainsKey('RonFlowWebTargetPath')) {
  $RonFlowWebTargetPath = Join-Path $DeploymentRoot 'ronflow-web'
}

$DeploymentRoot = Get-AbsolutePath -Path $DeploymentRoot
$RonAuthTargetPath = Get-AbsolutePath -Path $RonAuthTargetPath
$RonFlowApiTargetPath = Get-AbsolutePath -Path $RonFlowApiTargetPath
$RonFlowWebTargetPath = Get-AbsolutePath -Path $RonFlowWebTargetPath

if ([string]::IsNullOrWhiteSpace($RonAuthAppPoolName)) {
  $RonAuthAppPoolName = $IisAppPoolName
}

if ([string]::IsNullOrWhiteSpace($RonFlowApiAppPoolName)) {
  $RonFlowApiAppPoolName = $IisAppPoolName
}

$frontendApiBaseUrl = if ($ApiAccessMode -eq 'IisApplications') {
  "$RonFlowApiAppPath/api"
}
else {
  "$RonFlowApiOrigin/api"
}

$frontendRonAuthApiBaseUrl = if ($ApiAccessMode -eq 'IisApplications') {
  "$RonAuthAppPath/api/auth"
}
else {
  "$RonAuthOrigin/api/auth"
}

function Write-Step {
  param([Parameter(Mandatory = $true)][string]$Message)

  Write-Host ''
  Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-RequiredCommand {
  param([Parameter(Mandatory = $true)][string]$Name)

  $command = Get-Command $Name -ErrorAction SilentlyContinue
  if ($null -eq $command -and -not $Name.EndsWith('.cmd')) {
    $command = Get-Command ($Name + '.cmd') -ErrorAction SilentlyContinue
  }

  if ($null -eq $command) {
    throw "Required command not found: $Name"
  }

  return $command.Source
}

function Try-ResolveGitRevision {
  param([Parameter(Mandatory = $true)][string]$RepositoryPath)

  if ($null -eq $script:gitPath) {
    return $null
  }

  if (-not (Test-Path -LiteralPath $RepositoryPath)) {
    return $null
  }

  try {
    $revision = & $script:gitPath -C $RepositoryPath rev-parse HEAD 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($revision)) {
      return $null
    }

    return $revision.Trim()
  }
  catch {
    return $null
  }
}

function Test-IsAdministrator {
  $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = [Security.Principal.WindowsPrincipal]::new($currentIdentity)
  return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-Directory {
  param([Parameter(Mandatory = $true)][string]$Path)

  if (-not (Test-Path -LiteralPath $Path)) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
  }
}

function Get-ElevatedDeployCommand {
  return 'pwsh -NoLogo -NoProfile -File .\scripts\deployment\Deploy-LocalhostSites.ps1 -EnsureIisApplications -StopIisHosting -SkipFrontendInstall'
}

function Test-DirectoryWritable {
  param([Parameter(Mandatory = $true)][string]$Path)

  try {
    Ensure-Directory -Path $Path
    $probePath = Join-Path $Path ('.ronflow-deploy-write-test-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    Set-Content -LiteralPath $probePath -Value 'write-test' -Encoding ASCII
    Remove-Item -LiteralPath $probePath -Force
    return $true
  }
  catch {
    return $false
  }
}

function Assert-DeploymentTargetsWritable {
  $targetPaths = @($RonAuthTargetPath, $RonFlowApiTargetPath, $RonFlowWebTargetPath)
  $blockedPaths = @()

  foreach ($targetPath in $targetPaths) {
    if (-not (Test-DirectoryWritable -Path $targetPath)) {
      $blockedPaths += $targetPath
    }
  }

  if ($blockedPaths.Count -eq 0) {
    return
  }

  $blockedPathList = ($blockedPaths | ForEach-Object { "- $_" }) -join [Environment]::NewLine
  $repairCommand = Get-ElevatedDeployCommand
  throw @"
Deployment target is not writable for the current PowerShell session.

Blocked paths:
$blockedPathList

This is a deployment permission problem, not a build failure. The default localhost deployment writes under C:\inetpub, which is often writable only by Administrators.

Use one of these durable fixes:
1. Run the deployment from an elevated PowerShell 7 session:
$repairCommand

2. Install the elevated scheduled task once, then invoke it from a normal terminal:
pwsh -NoLogo -NoProfile -File .\scripts\deployment\Install-LocalhostDeployScheduledTask.ps1
pwsh -NoLogo -NoProfile -File .\scripts\deployment\Invoke-LocalhostDeployScheduledTask.ps1

3. If you intentionally want non-elevated deployments to C:\inetpub, grant the current user Modify permission on the three target folders.
"@
}

function New-AppOfflineMarker {
  param([Parameter(Mandatory = $true)][string]$TargetPath)

  Ensure-Directory -Path $TargetPath
  $markerPath = Join-Path $TargetPath 'app_offline.htm'
  Set-Content -LiteralPath $markerPath -Value '<html><body><h1>Deployment in progress</h1></body></html>' -Encoding UTF8
  return $markerPath
}

function Remove-AppOfflineMarker {
  param([Parameter(Mandatory = $true)][string]$MarkerPath)

  if (Test-Path -LiteralPath $MarkerPath) {
    Remove-Item -LiteralPath $MarkerPath -Force
  }
}

function Publish-DotnetSite {
  param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$TargetPath,
    [Parameter(Mandatory = $true)][string]$DisplayName
  )

  if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "$DisplayName project not found: $ProjectPath"
  }

  Write-Host "Placing app_offline.htm in $TargetPath to release IIS file locks during publish."
  $offlineMarkerPath = New-AppOfflineMarker -TargetPath $TargetPath

  try {
    & $script:dotnetPath publish $ProjectPath -c $Configuration -o $TargetPath
    if ($LASTEXITCODE -ne 0) {
      throw "$DisplayName publish failed with exit code $LASTEXITCODE"
    }
  }
  finally {
    Remove-AppOfflineMarker -MarkerPath $offlineMarkerPath
  }
}

function Clear-DirectoryContents {
  param([Parameter(Mandatory = $true)][string]$Path)

  Ensure-Directory -Path $Path
  Get-ChildItem -LiteralPath $Path -Force | Remove-Item -Recurse -Force
}

function Get-PortProcessIds {
  param([Parameter(Mandatory = $true)][int]$Port)

  $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
  if ($null -eq $connections) {
    return @()
  }

  return $connections | Select-Object -ExpandProperty OwningProcess -Unique
}

function Stop-ProcessesOnPort {
  param([Parameter(Mandatory = $true)][int]$Port)

  $processIds = Get-PortProcessIds -Port $Port
  foreach ($processId in $processIds) {
    try {
      $process = Get-Process -Id $processId -ErrorAction Stop
      if ($process.Id -eq $PID) {
        continue
      }

      Write-Host "Stopping process on port ${Port}: $($process.ProcessName) ($processId)"
      Stop-Process -Id $processId -Force
    }
    catch {
      Write-Warning "Failed to stop process $processId on port $Port. $($_.Exception.Message)"
    }
  }
}

function Test-PortOpen {
  param(
    [Parameter(Mandatory = $true)][string]$HostName,
    [Parameter(Mandatory = $true)][int]$Port,
    [int]$TimeoutMs = 1000
  )

  $client = New-Object System.Net.Sockets.TcpClient
  try {
    $asyncResult = $client.BeginConnect($HostName, $Port, $null, $null)
    if (-not $asyncResult.AsyncWaitHandle.WaitOne($TimeoutMs)) {
      return $false
    }

    $client.EndConnect($asyncResult)
    return $true
  }
  catch {
    return $false
  }
  finally {
    $client.Dispose()
  }
}

function Wait-ForPort {
  param(
    [Parameter(Mandatory = $true)][string]$HostName,
    [Parameter(Mandatory = $true)][int]$Port,
    [int]$TimeoutSeconds = 60,
    [string]$FailureHint
  )

  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  while ((Get-Date) -lt $deadline) {
    if (Test-PortOpen -HostName $HostName -Port $Port) {
      return
    }

    [System.Threading.Thread]::Sleep(200)
  }

  if ([string]::IsNullOrWhiteSpace($FailureHint)) {
    throw "Timed out waiting for ${HostName}:${Port} to accept TCP connections."
  }

  throw "Timed out waiting for ${HostName}:${Port} to accept TCP connections. $FailureHint"
}

function Write-BuildInfoFile {
  param(
    [Parameter(Mandatory = $true)][string]$TargetPath,
    [Parameter(Mandatory = $true)][string]$Application,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][DateTimeOffset]$UpdatedAtUtc,
    [string]$SourceRevision
  )

  Ensure-Directory -Path $TargetPath

  $informationalVersion = if ([string]::IsNullOrWhiteSpace($SourceRevision)) {
    $Version
  }
  else {
    "$Version+$SourceRevision"
  }

  $buildInfo = [ordered]@{
    application = $Application
    version = $Version
    informationalVersion = $informationalVersion
    updatedAtUtc = $UpdatedAtUtc.ToString('O')
    sourceRevision = if ([string]::IsNullOrWhiteSpace($SourceRevision)) { $null } else { $SourceRevision }
  }

  $buildInfoJson = $buildInfo | ConvertTo-Json
  $buildInfoPath = Join-Path $TargetPath 'build-info.json'
  Set-Content -LiteralPath $buildInfoPath -Value $buildInfoJson -Encoding UTF8
}

function Invoke-NpmInFrontend {
  param(
    [Parameter(Mandatory = $true)][string[]]$Arguments,
    [hashtable]$EnvironmentVariables
  )

  $npmExecutablePath = $script:npmPath
  if ($npmExecutablePath.EndsWith('.ps1', [System.StringComparison]::OrdinalIgnoreCase)) {
    $npmCmdPath = [System.IO.Path]::ChangeExtension($npmExecutablePath, '.cmd')
    if (Test-Path -LiteralPath $npmCmdPath) {
      $npmExecutablePath = $npmCmdPath
    }
  }

  $npmRoot = Split-Path -Parent $npmExecutablePath
  $npmCliPath = Join-Path $npmRoot 'node_modules\npm\bin\npm-cli.js'
  if (-not (Test-Path -LiteralPath $npmCliPath)) {
    throw "npm-cli.js not found: $npmCliPath"
  }

  Push-Location $frontendRoot
  try {
    $previousEnvironmentValues = @{}
    if ($null -ne $EnvironmentVariables) {
      foreach ($entry in $EnvironmentVariables.GetEnumerator()) {
        $previousEnvironmentValues[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key, [string]$entry.Value, 'Process')
      }
    }

    try {
      & $script:nodePath $npmCliPath @Arguments
      $processExitCode = $LASTEXITCODE
    }
    finally {
      if ($null -ne $EnvironmentVariables) {
        foreach ($entry in $EnvironmentVariables.GetEnumerator()) {
          [Environment]::SetEnvironmentVariable($entry.Key, $previousEnvironmentValues[$entry.Key], 'Process')
        }
      }
    }

    if ($processExitCode -ne 0) {
      throw "npm $($Arguments -join ' ') failed with exit code $processExitCode"
    }
  }
  finally {
    Pop-Location
  }
}

function New-FrontendWebConfigContent {
  return @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <defaultDocument enabled="true">
      <files>
        <clear />
        <add value="index.html" />
      </files>
    </defaultDocument>
    <staticContent>
      <mimeMap fileExtension=".webmanifest" mimeType="application/manifest+json" />
    </staticContent>
  </system.webServer>
</configuration>
"@
}

function Start-PublishedApi {
  param(
    [Parameter(Mandatory = $true)][string]$TargetPath,
    [Parameter(Mandatory = $true)][string]$ExecutableName,
    [Parameter(Mandatory = $true)][string]$Urls,
    [Parameter(Mandatory = $true)][string]$ServiceName,
    [Parameter(Mandatory = $true)][int]$Port
  )

  $logsPath = Join-Path $env:TEMP 'RonFlow-deploy-logs'
  Ensure-Directory -Path $logsPath

  $executablePath = Join-Path $TargetPath $ExecutableName
  if (-not (Test-Path -LiteralPath $executablePath)) {
    throw "$ServiceName executable not found: $executablePath"
  }

  $stdOutLogPath = Join-Path $logsPath ($ServiceName.ToLowerInvariant() + '.stdout.log')
  $stdErrLogPath = Join-Path $logsPath ($ServiceName.ToLowerInvariant() + '.stderr.log')
  $launcherPath = Join-Path $logsPath ($ServiceName.ToLowerInvariant() + '.launch.cmd')
  $cmdPath = Join-Path $env:SystemRoot 'System32\cmd.exe'

  if (-not (Test-Path -LiteralPath $cmdPath)) {
    throw "cmd.exe not found: $cmdPath"
  }

  $launcherLines = @(
    '@echo off',
    'setlocal',
    ('set "ASPNETCORE_URLS={0}"' -f $Urls),
    'set "ASPNETCORE_ENVIRONMENT=Production"',
    'set "DOTNET_ENVIRONMENT=Production"',
    ('call "{0}" --urls "{1}" 1>>"{2}" 2>>"{3}"' -f $executablePath, $Urls, $stdOutLogPath, $stdErrLogPath)
  )

  Set-Content -LiteralPath $launcherPath -Value $launcherLines -Encoding ASCII

  $process = Start-Process -FilePath $cmdPath -ArgumentList @('/d', '/c', ('call "{0}"' -f $launcherPath)) -WorkingDirectory $TargetPath -PassThru -WindowStyle Hidden
  if ($null -eq $process) {
    throw "Failed to start $ServiceName"
  }

  Write-Host "Started $ServiceName (PID $($process.Id))"

  Wait-ForPort -HostName '127.0.0.1' -Port $Port -FailureHint "Check logs: $stdOutLogPath / $stdErrLogPath"
}

function Get-AppCmdPath {
  return Join-Path $env:SystemRoot 'System32\inetsrv\appcmd.exe'
}

function Invoke-AppCmd {
  param(
    [Parameter(Mandatory = $true)][string[]]$Arguments,
    [switch]$IgnoreErrors
  )

  $appCmdPath = Get-AppCmdPath
  if (-not (Test-Path -LiteralPath $appCmdPath)) {
    throw "appcmd.exe not found: $appCmdPath"
  }

  $output = & $appCmdPath @Arguments 2>&1
  $exitCode = $LASTEXITCODE
  if ($exitCode -ne 0 -and -not $IgnoreErrors.IsPresent) {
    $commandText = "$appCmdPath $($Arguments -join ' ')"
    $outputText = ($output | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($outputText)) {
      $outputText = '<no output>'
    }

    throw @"
IIS appcmd command failed with exit code $exitCode.
Command: $commandText
Output:
$outputText
"@
  }

  return [pscustomobject]@{
    ExitCode = $exitCode
    Output = $output
  }
}

function Test-IisSiteExists {
  param([Parameter(Mandatory = $true)][string]$SiteName)

  $appCmdPath = Get-AppCmdPath
  if (-not (Test-Path -LiteralPath $appCmdPath)) {
    return $false
  }

  $result = Invoke-AppCmd -Arguments @('list', 'site', $SiteName) -IgnoreErrors
  return $result.ExitCode -eq 0
}

function Test-IisAppPoolExists {
  param([Parameter(Mandatory = $true)][string]$AppPoolName)

  $appCmdPath = Get-AppCmdPath
  if (-not (Test-Path -LiteralPath $appCmdPath)) {
    return $false
  }

  $result = Invoke-AppCmd -Arguments @('list', 'apppool', $AppPoolName) -IgnoreErrors
  return $result.ExitCode -eq 0
}

function Stop-IisHosting {
  param(
    [Parameter(Mandatory = $true)][string]$SiteName,
    [Parameter(Mandatory = $true)][string[]]$AppPoolNames
  )

  if (-not (Test-IsAdministrator)) {
    Write-Warning 'Skipping IIS stop/start flow because the current PowerShell session is not elevated.'
    return $false
  }

  $appCmdPath = Get-AppCmdPath
  if (-not (Test-Path -LiteralPath $appCmdPath)) {
    throw "appcmd.exe not found: $appCmdPath"
  }

  if (Test-IisSiteExists -SiteName $SiteName) {
    Invoke-AppCmd -Arguments @('stop', 'site', "/site.name:$SiteName") | Out-Null
  }

  foreach ($appPoolName in $AppPoolNames) {
    if (Test-IisAppPoolExists -AppPoolName $appPoolName) {
      Invoke-AppCmd -Arguments @('stop', 'apppool', "/apppool.name:$appPoolName") | Out-Null
    }
  }

  return $true
}

function Start-IisHosting {
  param(
    [Parameter(Mandatory = $true)][string]$SiteName,
    [Parameter(Mandatory = $true)][string[]]$AppPoolNames
  )

  if (-not (Test-IsAdministrator)) {
    return
  }

  $appCmdPath = Get-AppCmdPath
  if (-not (Test-Path -LiteralPath $appCmdPath)) {
    throw "appcmd.exe not found: $appCmdPath"
  }

  foreach ($appPoolName in $AppPoolNames) {
    if (Test-IisAppPoolExists -AppPoolName $appPoolName) {
      Invoke-AppCmd -Arguments @('start', 'apppool', "/apppool.name:$appPoolName") | Out-Null
    }
  }

  if (Test-IisSiteExists -SiteName $SiteName) {
    Invoke-AppCmd -Arguments @('start', 'site', "/site.name:$SiteName") | Out-Null
  }
}

function Test-HttpStatusCode {
  param(
    [Parameter(Mandatory = $true)][string]$Uri,
    [string]$Method = 'GET'
  )

  try {
    $response = Invoke-WebRequest -Uri $Uri -Method $Method -UseBasicParsing -TimeoutSec 20
    return [int]$response.StatusCode
  }
  catch {
    if ($null -ne $_.Exception.Response) {
      return [int]$_.Exception.Response.StatusCode
    }

    throw
  }
}

function Assert-IisApplicationHealth {
  param(
    [Parameter(Mandatory = $true)][string]$RonAuthHealthUri,
    [Parameter(Mandatory = $true)][string]$RonFlowHealthUri
  )

  $ronAuthStatusCode = Test-HttpStatusCode -Uri $RonAuthHealthUri -Method 'OPTIONS'
  $ronFlowStatusCode = Test-HttpStatusCode -Uri $RonFlowHealthUri

  $ronAuthHealthy = $ronAuthStatusCode -in @(200, 204, 400, 401, 405, 415)
  $ronFlowHealthy = $ronFlowStatusCode -in @(200, 401)

  if ($ronAuthHealthy -and $ronFlowHealthy) {
    return
  }

  $repairCommand = Get-ElevatedDeployCommand
  throw @"
Localhost IIS health checks failed after deployment.
RonAuth endpoint: $RonAuthHealthUri -> HTTP $ronAuthStatusCode
RonFlow endpoint: $RonFlowHealthUri -> HTTP $ronFlowStatusCode

If these endpoints are still returning 503, the IIS application bindings may need to be reapplied or the IIS app pool may need an elevated recycle.
Re-run the deployment from an elevated PowerShell 7 session with:
$repairCommand
"@
}

function Ensure-IisApplication {
  param(
    [Parameter(Mandatory = $true)][string]$SiteName,
    [Parameter(Mandatory = $true)][string]$ApplicationPath,
    [Parameter(Mandatory = $true)][string]$PhysicalPath,
    [Parameter(Mandatory = $true)][string]$AppPoolName
  )

  $appCmdPath = Get-AppCmdPath
  if (-not (Test-Path -LiteralPath $appCmdPath)) {
    throw "appcmd.exe not found: $appCmdPath"
  }

  if (-not (Test-IsAdministrator)) {
    throw "IIS application mode requires an elevated PowerShell session. Re-run the script as Administrator."
  }

  if (-not (Test-IisAppPoolExists -AppPoolName $AppPoolName)) {
    Invoke-AppCmd -Arguments @('add', 'apppool', "/name:$AppPoolName", '/managedRuntimeVersion:', '/managedPipelineMode:Integrated') | Out-Null
    Invoke-AppCmd -Arguments @('set', 'apppool', "/apppool.name:$AppPoolName", '/processModel.identityType:ApplicationPoolIdentity') | Out-Null
  }

  $appName = "$SiteName$ApplicationPath"
  $existingApp = Invoke-AppCmd -Arguments @('list', 'app', $appName) -IgnoreErrors
  if ($existingApp.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace(($existingApp.Output | Out-String))) {
    Invoke-AppCmd -Arguments @('set', 'app', $appName, "/physicalPath:$PhysicalPath", "/applicationPool:$AppPoolName") | Out-Null
  }
  else {
    Invoke-AppCmd -Arguments @('add', 'app', "/site.name:$SiteName", "/path:$ApplicationPath", "/physicalPath:$PhysicalPath", "/applicationPool:$AppPoolName") | Out-Null
  }

  $verifiedApp = Invoke-AppCmd -Arguments @('list', 'app', $appName) -IgnoreErrors
  if ($verifiedApp.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace(($verifiedApp.Output | Out-String))) {
    throw "Failed to verify IIS application after configuration: $appName"
  }
}

function Deploy-FrontendSite {
  if (-not (Test-Path -LiteralPath $frontendRoot)) {
    throw "Frontend project not found: $frontendRoot"
  }

  if (-not $SkipFrontendInstall.IsPresent) {
    Write-Step 'Installing frontend dependencies with npm ci'
    Invoke-NpmInFrontend -Arguments @('ci', '--no-audit', '--fund=false')
  }

  Write-Step 'Building RonFlow frontend'
  Invoke-NpmInFrontend -Arguments @('run', 'build') -EnvironmentVariables @{
    VITE_API_BASE_URL = $frontendApiBaseUrl
    VITE_RONAUTH_API_BASE_URL = $frontendRonAuthApiBaseUrl
  }

  if (-not (Test-Path -LiteralPath $frontendDistPath)) {
    throw "Frontend build output not found: $frontendDistPath"
  }

  Write-Step "Deploying RonFlow frontend to $RonFlowWebTargetPath"
  Clear-DirectoryContents -Path $RonFlowWebTargetPath
  Copy-Item -Path (Join-Path $frontendDistPath '*') -Destination $RonFlowWebTargetPath -Recurse -Force

  $webConfigPath = Join-Path $RonFlowWebTargetPath 'web.config'
  Set-Content -LiteralPath $webConfigPath -Value (New-FrontendWebConfigContent) -Encoding UTF8

  Write-BuildInfoFile -TargetPath $RonFlowWebTargetPath -Application 'RonFlow.Web' -Version $resolvedBuildVersion -UpdatedAtUtc $resolvedBuildUpdatedAtUtc -SourceRevision $script:ronFlowSourceRevision
}

$dotnetPath = Get-RequiredCommand -Name 'dotnet'
$nodePath = Get-RequiredCommand -Name 'node'
$npmPath = Get-RequiredCommand -Name 'npm'
$gitPath = Get-Command git -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue
$ronFlowSourceRevision = Try-ResolveGitRevision -RepositoryPath $repoRoot
$ronAuthSourceRevision = Try-ResolveGitRevision -RepositoryPath $ronAuthRepoRoot
$managedIisAppPools = @($RonAuthAppPoolName, $RonFlowApiAppPoolName) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique
$iisHostingStopped = $false

Assert-DeploymentTargetsWritable

if (-not $SkipApiStart.IsPresent) {
  Write-Step 'Stopping published API processes before publish'
  Stop-ProcessesOnPort -Port 5136
  Stop-ProcessesOnPort -Port 5078
}

if ($ApiAccessMode -eq 'IisApplications' -and $StopIisHosting.IsPresent) {
  Write-Step 'Stopping IIS site and app pools before publish'
  $iisHostingStopped = Stop-IisHosting -SiteName $IisSiteName -AppPoolNames $managedIisAppPools
}
elseif ($ApiAccessMode -eq 'IisApplications') {
  Write-Step 'Publishing IIS-hosted APIs without stopping IIS'
  Write-Host 'Using app_offline.htm to release ASP.NET Core file locks during publish.' -ForegroundColor Yellow
}

Write-Step "Publishing RonAuth API to $RonAuthTargetPath"
Publish-DotnetSite -ProjectPath $ronAuthProjectPath -TargetPath $RonAuthTargetPath -DisplayName 'RonAuth API'
Write-BuildInfoFile -TargetPath $RonAuthTargetPath -Application 'RonAuth.Api' -Version $resolvedBuildVersion -UpdatedAtUtc $resolvedBuildUpdatedAtUtc -SourceRevision $ronAuthSourceRevision

Write-Step "Publishing RonFlow API to $RonFlowApiTargetPath"
Publish-DotnetSite -ProjectPath $ronFlowApiProjectPath -TargetPath $RonFlowApiTargetPath -DisplayName 'RonFlow API'
Ensure-Directory -Path (Join-Path $RonFlowApiTargetPath 'logs')
Write-BuildInfoFile -TargetPath $RonFlowApiTargetPath -Application 'RonFlow.Api' -Version $resolvedBuildVersion -UpdatedAtUtc $resolvedBuildUpdatedAtUtc -SourceRevision $ronFlowSourceRevision

if ($ApiAccessMode -eq 'IisApplications' -and $EnsureIisApplications.IsPresent) {
  Write-Step 'Configuring IIS applications for path-based API access'
  Ensure-IisApplication -SiteName $IisSiteName -ApplicationPath $RonAuthAppPath -PhysicalPath $RonAuthTargetPath -AppPoolName $RonAuthAppPoolName
  Ensure-IisApplication -SiteName $IisSiteName -ApplicationPath $RonFlowApiAppPath -PhysicalPath $RonFlowApiTargetPath -AppPoolName $RonFlowApiAppPoolName
}
elseif (-not $SkipApiStart.IsPresent) {
  Write-Step 'Starting published API processes'
  Start-PublishedApi -TargetPath $RonAuthTargetPath -ExecutableName 'RonAuth.Api.exe' -Urls $RonAuthOrigin -ServiceName 'RonAuth.Api' -Port 5136
  Start-PublishedApi -TargetPath $RonFlowApiTargetPath -ExecutableName 'RonFlow.Api.exe' -Urls $RonFlowApiOrigin -ServiceName 'RonFlow.Api' -Port 5078
}

Deploy-FrontendSite

if ($ApiAccessMode -eq 'IisApplications' -and $iisHostingStopped) {
  Write-Step 'Starting IIS site and app pools after deploy'
  Start-IisHosting -SiteName $IisSiteName -AppPoolNames $managedIisAppPools
}

if ($ApiAccessMode -eq 'IisApplications') {
  Write-Step 'Verifying IIS-hosted localhost API health'
  Assert-IisApplicationHealth `
    -RonAuthHealthUri ($RonFlowWebOrigin.TrimEnd('/') + $RonAuthAppPath + '/api/auth/login') `
    -RonFlowHealthUri ($RonFlowWebOrigin.TrimEnd('/') + $RonFlowApiAppPath + '/api/ai/bootstrap')
}

Write-Host ''
Write-Host 'Deployment completed.' -ForegroundColor Green
Write-Host "RonAuth API:    $RonAuthTargetPath" -ForegroundColor Green
Write-Host "RonFlow API:    $RonFlowApiTargetPath" -ForegroundColor Green
Write-Host "RonFlow Frontend: $RonFlowWebTargetPath" -ForegroundColor Green
Write-Host "Build version:  $resolvedBuildVersion" -ForegroundColor Green
Write-Host "Updated at UTC: $($resolvedBuildUpdatedAtUtc.ToString('O'))" -ForegroundColor Green
Write-Host "Frontend origin: $RonFlowWebOrigin" -ForegroundColor Green
Write-Host ''
if ($ApiAccessMode -eq 'IisApplications') {
  Write-Host "Frontend now calls $RonFlowApiAppPath and $RonAuthAppPath on the same localhost site." -ForegroundColor Yellow
}
else {
  Write-Host 'Frontend now calls the API origins directly, so IIS URL Rewrite is no longer required for localhost deployment.' -ForegroundColor Yellow
}
