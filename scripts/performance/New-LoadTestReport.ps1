[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$InputPath,
  [string]$OutputPath,
  [string]$Title = 'RonFlow Load Test Report'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Metric {
  param(
    [Parameter(Mandatory = $true)][pscustomobject]$Metrics,
    [Parameter(Mandatory = $true)][string]$Name
  )

  $property = $Metrics.PSObject.Properties[$Name]
  if ($null -eq $property) {
    return $null
  }

  return $property.Value
}

function Get-MetricNumber {
  param(
    [pscustomobject]$Metric,
    [Parameter(Mandatory = $true)][string]$PropertyName
  )

  if ($null -eq $Metric) {
    return $null
  }

  $property = $Metric.PSObject.Properties[$PropertyName]
  if ($null -eq $property) {
    return $null
  }

  return [double]$property.Value
}

function Resolve-MetricPropertyName {
  param(
    [pscustomobject]$Metric,
    [Parameter(Mandatory = $true)][string]$PropertyName
  )

  if ($PropertyName -eq 'rate') {
    $rateProperty = $Metric.PSObject.Properties['rate']
    if ($null -ne $rateProperty) {
      return 'rate'
    }

    $valueProperty = $Metric.PSObject.Properties['value']
    if ($null -ne $valueProperty) {
      return 'value'
    }
  }

  return $PropertyName
}

function Format-Number {
  param(
    [AllowNull()][object]$Value,
    [int]$Decimals = 2,
    [string]$Suffix = ''
  )

  if ($null -eq $Value) {
    return 'n/a'
  }

  return ('{0:N' + $Decimals + '}{1}') -f [double]$Value, $Suffix
}

function Get-ThresholdActualValue {
  param(
    [Parameter(Mandatory = $true)][pscustomobject]$Metric,
    [Parameter(Mandatory = $true)][string]$ThresholdExpression
  )

  if ($ThresholdExpression -match '^(rate|avg|min|max|med|p\(\d+\))') {
    $propertyName = Resolve-MetricPropertyName -Metric $Metric -PropertyName $Matches[1]
    return Get-MetricNumber -Metric $Metric -PropertyName $propertyName
  }

  return $null
}

function Test-ThresholdPass {
  param(
    [Parameter(Mandatory = $true)][pscustomobject]$Metric,
    [Parameter(Mandatory = $true)][string]$ThresholdExpression,
    [AllowNull()][object]$FallbackValue
  )

  if ($ThresholdExpression -match '^(rate|avg|min|max|med|p\(\d+\))(<=|>=|<|>)(-?\d+(?:\.\d+)?)$') {
    $propertyName = Resolve-MetricPropertyName -Metric $Metric -PropertyName $Matches[1]
    $operator = $Matches[2]
    $targetValue = [double]$Matches[3]
    $actualValue = Get-MetricNumber -Metric $Metric -PropertyName $propertyName

    if ($null -eq $actualValue) {
      return $null
    }

    switch ($operator) {
      '<' { return $actualValue -lt $targetValue }
      '<=' { return $actualValue -le $targetValue }
      '>' { return $actualValue -gt $targetValue }
      '>=' { return $actualValue -ge $targetValue }
    }
  }

  if ($null -ne $FallbackValue) {
    return [bool]$FallbackValue
  }

  return $null
}

function Get-ThresholdRows {
  param([pscustomobject]$Metrics)

  $rows = @()
  foreach ($metricProperty in $Metrics.PSObject.Properties) {
    $metric = $metricProperty.Value
    $thresholdsProperty = $metricProperty.Value.PSObject.Properties['thresholds']
    if ($null -eq $thresholdsProperty) {
      continue
    }

    foreach ($thresholdProperty in $thresholdsProperty.Value.PSObject.Properties) {
      $actualValue = Get-ThresholdActualValue -Metric $metric -ThresholdExpression $thresholdProperty.Name
      $rows += [pscustomobject]@{
        Metric = $metricProperty.Name
        Threshold = $thresholdProperty.Name
        ActualValue = $actualValue
        Passed = Test-ThresholdPass -Metric $metric -ThresholdExpression $thresholdProperty.Name -FallbackValue $thresholdProperty.Value
      }
    }
  }

  return $rows
}

function Get-CheckRows {
  param([pscustomobject]$Group)

  $rows = @()

  foreach ($checkProperty in $Group.checks.PSObject.Properties) {
    $check = $checkProperty.Value
    $total = [int]$check.passes + [int]$check.fails
    $passRate = if ($total -eq 0) { $null } else { [double]$check.passes / $total * 100 }
    $rows += [pscustomobject]@{
      Path = $check.path
      Passes = [int]$check.passes
      Fails = [int]$check.fails
      PassRate = $passRate
    }
  }

  foreach ($childGroupProperty in $Group.groups.PSObject.Properties) {
    $rows += Get-CheckRows -Group $childGroupProperty.Value
  }

  return $rows
}

function ConvertTo-BarRowsHtml {
  param([object[]]$Rows)

  if ($Rows.Count -eq 0) {
    return '<p class="empty">No chart data.</p>'
  }

  $values = foreach ($row in $Rows) {
    if ($row -is [hashtable]) {
      [double]$row['Value']
      continue
    }

    [double]$row.Value
  }

  $maxValue = ($values | Measure-Object -Maximum).Maximum
  if ($null -eq $maxValue -or $maxValue -le 0) {
    $maxValue = 1
  }

  $htmlRows = foreach ($row in $Rows) {
    if ($row -is [hashtable]) {
      $label = [string]$row['Label']
      $value = [double]$row['Value']
      $displayValue = [string]$row['DisplayValue']
    }
    else {
      $label = [string]$row.Label
      $value = [double]$row.Value
      $displayValue = [string]$row.DisplayValue
    }

    $widthPercent = [Math]::Max(2, [Math]::Round(($value / $maxValue) * 100, 1))
    @"
<div class="bar-row">
  <div class="bar-label">$([System.Net.WebUtility]::HtmlEncode($label))</div>
  <div class="bar-track"><div class="bar-fill" style="width: ${widthPercent}%"></div></div>
  <div class="bar-value">$([System.Net.WebUtility]::HtmlEncode($displayValue))</div>
</div>
"@
  }

  return ($htmlRows -join [Environment]::NewLine)
}

function ConvertTo-ThresholdTableHtml {
  param([object[]]$Rows)

  if ($Rows.Count -eq 0) {
    return '<p class="empty">No thresholds found.</p>'
  }

  $tableRows = foreach ($row in $Rows) {
    $statusClass = if ($row.Passed) { 'status-pass' } else { 'status-fail' }
    $statusLabel = if ($row.Passed) { 'PASS' } else { 'FAIL' }
    $actualValueLabel = if ($null -eq $row.ActualValue) { 'n/a' } else { Format-Number -Value $row.ActualValue }
    @"
<tr>
  <td>$([System.Net.WebUtility]::HtmlEncode($row.Metric))</td>
  <td>$([System.Net.WebUtility]::HtmlEncode($row.Threshold))</td>
  <td>$([System.Net.WebUtility]::HtmlEncode($actualValueLabel))</td>
  <td><span class="status-pill $statusClass">$statusLabel</span></td>
</tr>
"@
  }

  return @"
<table>
  <thead>
    <tr>
      <th>Metric</th>
      <th>Threshold</th>
      <th>Actual</th>
      <th>Status</th>
    </tr>
  </thead>
  <tbody>
$($tableRows -join [Environment]::NewLine)
  </tbody>
</table>
"@
}

function ConvertTo-ChecksTableHtml {
  param([object[]]$Rows)

  if ($Rows.Count -eq 0) {
    return '<p class="empty">No checks found.</p>'
  }

  $tableRows = foreach ($row in $Rows) {
    @"
<tr>
  <td>$([System.Net.WebUtility]::HtmlEncode($row.Path))</td>
  <td>$($row.Passes)</td>
  <td>$($row.Fails)</td>
  <td>$([System.Net.WebUtility]::HtmlEncode((Format-Number -Value $row.PassRate -Suffix '%')))</td>
</tr>
"@
  }

  return @"
<table>
  <thead>
    <tr>
      <th>Check</th>
      <th>Passes</th>
      <th>Fails</th>
      <th>Pass rate</th>
    </tr>
  </thead>
  <tbody>
$($tableRows -join [Environment]::NewLine)
  </tbody>
</table>
"@
}

$resolvedInputPath = (Resolve-Path -LiteralPath $InputPath).Path
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
  $OutputPath = [System.IO.Path]::ChangeExtension($resolvedInputPath, '.html')
}

$rawJson = Get-Content -LiteralPath $resolvedInputPath -Raw
$summary = $rawJson | ConvertFrom-Json
$metrics = $summary.metrics

$httpReqs = Get-Metric -Metrics $metrics -Name 'http_reqs'
$httpReqDuration = Get-Metric -Metrics $metrics -Name 'http_req_duration'
$httpReqFailed = Get-Metric -Metrics $metrics -Name 'http_req_failed'
$checksMetric = Get-Metric -Metrics $metrics -Name 'checks'
$boardDuration = Get-Metric -Metrics $metrics -Name 'ronflow_board_duration'
$boardControllerDuration = Get-Metric -Metrics $metrics -Name 'ronflow_board_controller_duration'
$boardApplicationDuration = Get-Metric -Metrics $metrics -Name 'ronflow_board_application_duration'
$boardStoreDuration = Get-Metric -Metrics $metrics -Name 'ronflow_board_store_duration'
$iterationDuration = Get-Metric -Metrics $metrics -Name 'iteration_duration'

$checkRows = @(Get-CheckRows -Group $summary.root_group)
$thresholdRows = @(Get-ThresholdRows -Metrics $metrics)
$projectCount = ($summary.setup_data.projectIds | Measure-Object).Count
$checksPassRate = if ($null -eq $checksMetric) { $null } else { [double]$checksMetric.value * 100 }
$httpFailureRate = if ($null -eq $httpReqFailed) { $null } else { [double]$httpReqFailed.value * 100 }

$summaryCards = @(
  @{ Label = 'Requests'; Value = (Format-Number -Value (Get-MetricNumber -Metric $httpReqs -PropertyName 'count') -Decimals 0) },
  @{ Label = 'Req/s'; Value = (Format-Number -Value (Get-MetricNumber -Metric $httpReqs -PropertyName 'rate') -Suffix ' req/s') },
  @{ Label = 'HTTP p95'; Value = (Format-Number -Value (Get-MetricNumber -Metric $httpReqDuration -PropertyName 'p(95)') -Suffix ' ms') },
  @{ Label = 'Board p95'; Value = (Format-Number -Value (Get-MetricNumber -Metric $boardDuration -PropertyName 'p(95)') -Suffix ' ms') },
  @{ Label = 'HTTP fail rate'; Value = (Format-Number -Value $httpFailureRate -Suffix '%') },
  @{ Label = 'Checks pass rate'; Value = (Format-Number -Value $checksPassRate -Suffix '%') },
  @{ Label = 'Projects'; Value = (Format-Number -Value $projectCount -Decimals 0) },
  @{ Label = 'Iteration p95'; Value = (Format-Number -Value (Get-MetricNumber -Metric $iterationDuration -PropertyName 'p(95)') -Suffix ' ms') }
)

$timingBars = @(
  @{ Label = 'HTTP request p95'; Value = (Get-MetricNumber -Metric $httpReqDuration -PropertyName 'p(95)'); DisplayValue = (Format-Number -Value (Get-MetricNumber -Metric $httpReqDuration -PropertyName 'p(95)') -Suffix ' ms') },
  @{ Label = 'Board request p95'; Value = (Get-MetricNumber -Metric $boardDuration -PropertyName 'p(95)'); DisplayValue = (Format-Number -Value (Get-MetricNumber -Metric $boardDuration -PropertyName 'p(95)') -Suffix ' ms') },
  @{ Label = 'Controller p95'; Value = (Get-MetricNumber -Metric $boardControllerDuration -PropertyName 'p(95)'); DisplayValue = (Format-Number -Value (Get-MetricNumber -Metric $boardControllerDuration -PropertyName 'p(95)') -Suffix ' ms') },
  @{ Label = 'Application p95'; Value = (Get-MetricNumber -Metric $boardApplicationDuration -PropertyName 'p(95)'); DisplayValue = (Format-Number -Value (Get-MetricNumber -Metric $boardApplicationDuration -PropertyName 'p(95)') -Suffix ' ms') },
  @{ Label = 'Store p95'; Value = (Get-MetricNumber -Metric $boardStoreDuration -PropertyName 'p(95)'); DisplayValue = (Format-Number -Value (Get-MetricNumber -Metric $boardStoreDuration -PropertyName 'p(95)') -Suffix ' ms') }
) | Where-Object { $null -ne $_.Value }

$cardsHtml = foreach ($card in $summaryCards) {
  @"
<section class="card">
  <div class="card-label">$([System.Net.WebUtility]::HtmlEncode($card.Label))</div>
  <div class="card-value">$([System.Net.WebUtility]::HtmlEncode($card.Value))</div>
</section>
"@
}

$html = @"
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>$([System.Net.WebUtility]::HtmlEncode($Title))</title>
  <style>
    :root {
      color-scheme: light;
      --bg: #f5f1e8;
      --surface: #fffdf9;
      --ink: #1f2933;
      --muted: #6b7280;
      --accent: #146356;
      --accent-soft: #d7efe9;
      --warn: #a61b1b;
      --warn-soft: #f9d7d7;
      --border: #e5ded3;
    }

    * { box-sizing: border-box; }
    body {
      margin: 0;
      font-family: "Segoe UI", sans-serif;
      color: var(--ink);
      background:
        radial-gradient(circle at top right, #f5dbc0 0, transparent 30%),
        linear-gradient(180deg, #f7f4ef 0%, var(--bg) 100%);
    }

    .page {
      max-width: 1180px;
      margin: 0 auto;
      padding: 32px 20px 48px;
    }

    .hero {
      padding: 28px;
      border: 1px solid var(--border);
      border-radius: 24px;
      background: rgba(255, 253, 249, 0.92);
      box-shadow: 0 18px 45px rgba(31, 41, 51, 0.08);
    }

    .eyebrow {
      font-size: 12px;
      letter-spacing: 0.12em;
      text-transform: uppercase;
      color: var(--muted);
      margin-bottom: 10px;
    }

    h1, h2 {
      margin: 0 0 12px;
      font-weight: 700;
    }

    h1 { font-size: 34px; }
    h2 { font-size: 22px; margin-top: 32px; }

    .hero-meta {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
      color: var(--muted);
      font-size: 14px;
    }

    .card-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 14px;
      margin-top: 24px;
    }

    .card, .panel {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 18px;
      padding: 18px;
      box-shadow: 0 10px 30px rgba(31, 41, 51, 0.05);
    }

    .card-label {
      color: var(--muted);
      font-size: 13px;
      margin-bottom: 10px;
    }

    .card-value {
      font-size: 28px;
      font-weight: 700;
    }

    .panel-grid {
      display: grid;
      grid-template-columns: 1.2fr 1fr;
      gap: 18px;
      margin-top: 24px;
    }

    .bar-row {
      display: grid;
      grid-template-columns: 180px minmax(0, 1fr) 110px;
      gap: 12px;
      align-items: center;
      margin-bottom: 12px;
    }

    .bar-label, .bar-value {
      font-size: 14px;
    }

    .bar-track {
      height: 14px;
      border-radius: 999px;
      background: #efe7dc;
      overflow: hidden;
    }

    .bar-fill {
      height: 100%;
      border-radius: 999px;
      background: linear-gradient(90deg, #146356, #2f7f70);
    }

    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 14px;
    }

    th, td {
      text-align: left;
      padding: 10px 12px;
      border-bottom: 1px solid var(--border);
      vertical-align: top;
    }

    th {
      color: var(--muted);
      font-weight: 600;
      font-size: 12px;
      letter-spacing: 0.03em;
      text-transform: uppercase;
    }

    .status-pill {
      display: inline-block;
      padding: 4px 10px;
      border-radius: 999px;
      font-size: 12px;
      font-weight: 700;
    }

    .status-pass {
      color: var(--accent);
      background: var(--accent-soft);
    }

    .status-fail {
      color: var(--warn);
      background: var(--warn-soft);
    }

    .empty {
      color: var(--muted);
      margin: 0;
    }

    @media (max-width: 900px) {
      .panel-grid {
        grid-template-columns: 1fr;
      }

      .bar-row {
        grid-template-columns: 1fr;
      }
    }
  </style>
</head>
<body>
  <main class="page">
    <section class="hero">
      <div class="eyebrow">RonFlow performance</div>
      <h1>$([System.Net.WebUtility]::HtmlEncode($Title))</h1>
      <div class="hero-meta">
        <div>Source JSON: $([System.Net.WebUtility]::HtmlEncode($resolvedInputPath))</div>
        <div>Generated: $([System.Net.WebUtility]::HtmlEncode((Get-Date).ToString('yyyy-MM-dd HH:mm:ss')))</div>
      </div>
      <div class="card-grid">
$($cardsHtml -join [Environment]::NewLine)
      </div>
    </section>

    <div class="panel-grid">
      <section class="panel">
        <h2>Latency view</h2>
        $(ConvertTo-BarRowsHtml -Rows $timingBars)
      </section>

      <section class="panel">
        <h2>Thresholds</h2>
        $(ConvertTo-ThresholdTableHtml -Rows $thresholdRows)
      </section>
    </div>

    <div class="panel-grid">
      <section class="panel">
        <h2>Checks</h2>
        $(ConvertTo-ChecksTableHtml -Rows $checkRows)
      </section>

      <section class="panel">
        <h2>Key metrics snapshot</h2>
        <table>
          <thead>
            <tr>
              <th>Metric</th>
              <th>Avg</th>
              <th>p95</th>
              <th>Max</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>http_req_duration</td>
              <td>$([System.Net.WebUtility]::HtmlEncode((Format-Number -Value (Get-MetricNumber -Metric $httpReqDuration -PropertyName 'avg') -Suffix ' ms')))</td>
              <td>$([System.Net.WebUtility]::HtmlEncode((Format-Number -Value (Get-MetricNumber -Metric $httpReqDuration -PropertyName 'p(95)') -Suffix ' ms')))</td>
              <td>$([System.Net.WebUtility]::HtmlEncode((Format-Number -Value (Get-MetricNumber -Metric $httpReqDuration -PropertyName 'max') -Suffix ' ms')))</td>
            </tr>
            <tr>
              <td>ronflow_board_duration</td>
              <td>$([System.Net.WebUtility]::HtmlEncode((Format-Number -Value (Get-MetricNumber -Metric $boardDuration -PropertyName 'avg') -Suffix ' ms')))</td>
              <td>$([System.Net.WebUtility]::HtmlEncode((Format-Number -Value (Get-MetricNumber -Metric $boardDuration -PropertyName 'p(95)') -Suffix ' ms')))</td>
              <td>$([System.Net.WebUtility]::HtmlEncode((Format-Number -Value (Get-MetricNumber -Metric $boardDuration -PropertyName 'max') -Suffix ' ms')))</td>
            </tr>
            <tr>
              <td>iteration_duration</td>
              <td>$([System.Net.WebUtility]::HtmlEncode((Format-Number -Value (Get-MetricNumber -Metric $iterationDuration -PropertyName 'avg') -Suffix ' ms')))</td>
              <td>$([System.Net.WebUtility]::HtmlEncode((Format-Number -Value (Get-MetricNumber -Metric $iterationDuration -PropertyName 'p(95)') -Suffix ' ms')))</td>
              <td>$([System.Net.WebUtility]::HtmlEncode((Format-Number -Value (Get-MetricNumber -Metric $iterationDuration -PropertyName 'max') -Suffix ' ms')))</td>
            </tr>
          </tbody>
        </table>
      </section>
    </div>
  </main>
</body>
</html>
"@

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
  New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

Set-Content -LiteralPath $OutputPath -Value $html -Encoding UTF8
Write-Host "Load test report generated: $OutputPath"