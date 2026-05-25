[CmdletBinding()]
param(
  [ValidateSet('S', 'M', 'L', 'Custom')]
  [string]$Scale = 'S',
  [int]$ProjectCount,
  [int]$TasksPerProject,
  [int]$MembersPerProject = 0,
  [int]$PendingInvitationsPerProject = 0,
  [string]$RonAuthApiBaseUrl = 'http://127.0.0.1:5146/api/auth',
  [string]$RonFlowApiBaseUrl = 'http://127.0.0.1:5088/api',
  [string]$DefaultPassword = 'Admin123!',
  [switch]$DisableTaskStateDistribution
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-ScaleDefaults {
  param(
    [string]$Preset,
    [hashtable]$BoundParameters
  )

  $defaults = switch ($Preset) {
    'S' { @{ ProjectCount = 10; TasksPerProject = 50 } }
    'M' { @{ ProjectCount = 50; TasksPerProject = 100 } }
    'L' { @{ ProjectCount = 100; TasksPerProject = 200 } }
    'Custom' { @{ ProjectCount = 0; TasksPerProject = 0 } }
    default { throw "Unsupported scale '$Preset'." }
  }

  if (-not $BoundParameters.ContainsKey('ProjectCount')) {
    $script:ProjectCount = $defaults.ProjectCount
  }

  if (-not $BoundParameters.ContainsKey('TasksPerProject')) {
    $script:TasksPerProject = $defaults.TasksPerProject
  }
}

function ConvertTo-JsonBody {
  param([object]$Value)

  return $Value | ConvertTo-Json -Depth 10 -Compress
}

function Get-HttpStatusCode {
  param([System.Management.Automation.ErrorRecord]$ErrorRecord)

  if ($null -eq $ErrorRecord.Exception.Response) {
    return $null
  }

  try {
    return [int]$ErrorRecord.Exception.Response.StatusCode
  }
  catch {
    return $null
  }
}

function Invoke-JsonRequest {
  param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('GET', 'POST', 'PATCH')]
    [string]$Method,
    [Parameter(Mandatory = $true)]
    [string]$Uri,
    [hashtable]$Headers,
    [object]$Body
  )

  $request = @{
    Method = $Method
    Uri = $Uri
    ContentType = 'application/json'
  }

  if ($null -ne $Headers -and $Headers.Count -gt 0) {
    $request.Headers = $Headers
  }

  if ($null -ne $Body) {
    $request.Body = ConvertTo-JsonBody $Body
  }

  return Invoke-RestMethod @request
}

function New-AuthHeaders {
  param([Parameter(Mandatory = $true)][string]$AccessToken)

  return @{ Authorization = "Bearer $AccessToken" }
}

function Ensure-RonFlowUserSession {
  param(
    [Parameter(Mandatory = $true)][string]$UserName,
    [Parameter(Mandatory = $true)][string]$Email,
    [Parameter(Mandatory = $true)][string]$Password
  )

  $session = $null

  try {
    $payload = Invoke-JsonRequest -Method 'POST' -Uri "$RonAuthApiBaseUrl/register" -Body @{
      userName = $UserName
      email = $Email
      password = $Password
    }

    $session = @{ userName = $UserName; email = $Email; accessToken = $payload.accessToken }
  }
  catch {
    $statusCode = Get-HttpStatusCode $_
    if ($statusCode -notin @(400, 409)) {
      throw
    }

    $payload = Invoke-JsonRequest -Method 'POST' -Uri "$RonAuthApiBaseUrl/login" -Body @{
      userName = $UserName
      password = $Password
    }

    $session = @{ userName = $UserName; email = $Email; accessToken = $payload.accessToken }
  }

  [void](Invoke-JsonRequest -Method 'GET' -Uri "$RonFlowApiBaseUrl/projects" -Headers (New-AuthHeaders -AccessToken $session.accessToken))
  return $session
}

function New-PerfUser {
  param(
    [Parameter(Mandatory = $true)][string]$Label
  )

  return @{
    userName = $Label
    email = "$Label@example.test"
    password = $DefaultPassword
  }
}

function Create-Project {
  param(
    [Parameter(Mandatory = $true)][hashtable]$Session,
    [Parameter(Mandatory = $true)][string]$ProjectName
  )

  return Invoke-JsonRequest -Method 'POST' -Uri "$RonFlowApiBaseUrl/projects" -Headers (New-AuthHeaders -AccessToken $Session.accessToken) -Body @{ name = $ProjectName }
}

function Create-Task {
  param(
    [Parameter(Mandatory = $true)][hashtable]$Session,
    [Parameter(Mandatory = $true)][string]$ProjectId,
    [Parameter(Mandatory = $true)][string]$TaskTitle
  )

  return Invoke-JsonRequest -Method 'POST' -Uri "$RonFlowApiBaseUrl/projects/$ProjectId/tasks" -Headers (New-AuthHeaders -AccessToken $Session.accessToken) -Body @{ title = $TaskTitle }
}

function Move-TaskState {
  param(
    [Parameter(Mandatory = $true)][hashtable]$Session,
    [Parameter(Mandatory = $true)][string]$ProjectId,
    [Parameter(Mandatory = $true)][string]$TaskId,
    [Parameter(Mandatory = $true)][string]$StateKey
  )

  [void](Invoke-JsonRequest -Method 'PATCH' -Uri "$RonFlowApiBaseUrl/projects/$ProjectId/tasks/$TaskId/state" -Headers (New-AuthHeaders -AccessToken $Session.accessToken) -Body @{ stateKey = $StateKey })
}

function Invite-ProjectMember {
  param(
    [Parameter(Mandatory = $true)][hashtable]$Session,
    [Parameter(Mandatory = $true)][string]$ProjectId,
    [Parameter(Mandatory = $true)][string]$Invitee
  )

  return Invoke-JsonRequest -Method 'POST' -Uri "$RonFlowApiBaseUrl/projects/$ProjectId/invitations" -Headers (New-AuthHeaders -AccessToken $Session.accessToken) -Body @{ invitee = $Invitee }
}

function Get-InvitationInbox {
  param(
    [Parameter(Mandatory = $true)][hashtable]$Session
  )

  $payload = Invoke-JsonRequest -Method 'GET' -Uri "$RonFlowApiBaseUrl/invitations" -Headers (New-AuthHeaders -AccessToken $Session.accessToken)
  return $payload.items
}

function Accept-Invitation {
  param(
    [Parameter(Mandatory = $true)][hashtable]$Session,
    [Parameter(Mandatory = $true)][string]$InvitationId
  )

  [void](Invoke-JsonRequest -Method 'POST' -Uri "$RonFlowApiBaseUrl/invitations/$InvitationId/accept" -Headers (New-AuthHeaders -AccessToken $Session.accessToken))
}

Resolve-ScaleDefaults -Preset $Scale -BoundParameters $PSBoundParameters

if ($ProjectCount -le 0 -or $TasksPerProject -le 0) {
  throw 'ProjectCount and TasksPerProject must be greater than zero.'
}

$ownerUser = New-PerfUser -Label 'perf-owner'
$ownerSession = Ensure-RonFlowUserSession -UserName $ownerUser.userName -Email $ownerUser.email -Password $ownerUser.password

$taskStates = @('todo', 'active', 'review', 'done')
$summary = [ordered]@{
  scale = $Scale
  projectCount = $ProjectCount
  tasksPerProject = $TasksPerProject
  membersPerProject = $MembersPerProject
  pendingInvitationsPerProject = $PendingInvitationsPerProject
  projectsCreated = 0
  tasksCreated = 0
  acceptedMembersCreated = 0
  pendingInvitationsCreated = 0
}

for ($projectIndex = 1; $projectIndex -le $ProjectCount; $projectIndex += 1) {
  $projectName = 'perf-project-{0:D4}' -f $projectIndex
  $project = Create-Project -Session $ownerSession -ProjectName $projectName
  $summary.projectsCreated += 1

  for ($memberIndex = 1; $memberIndex -le $MembersPerProject; $memberIndex += 1) {
    $memberUser = New-PerfUser -Label ('perf-p{0:D4}-member-{1:D2}' -f $projectIndex, $memberIndex)
    $memberSession = Ensure-RonFlowUserSession -UserName $memberUser.userName -Email $memberUser.email -Password $memberUser.password

    [void](Invite-ProjectMember -Session $ownerSession -ProjectId $project.id -Invitee $memberUser.email)
    $invitation = Get-InvitationInbox -Session $memberSession | Where-Object { $_.projectId -eq $project.id } | Select-Object -First 1
    if ($null -eq $invitation) {
      throw "Failed to locate invitation for accepted member '$($memberUser.email)' in project '$projectName'."
    }

    Accept-Invitation -Session $memberSession -InvitationId $invitation.id
    $summary.acceptedMembersCreated += 1
  }

  for ($inviteIndex = 1; $inviteIndex -le $PendingInvitationsPerProject; $inviteIndex += 1) {
    $inviteeUser = New-PerfUser -Label ('perf-p{0:D4}-invitee-{1:D2}' -f $projectIndex, $inviteIndex)
    [void](Ensure-RonFlowUserSession -UserName $inviteeUser.userName -Email $inviteeUser.email -Password $inviteeUser.password)
    [void](Invite-ProjectMember -Session $ownerSession -ProjectId $project.id -Invitee $inviteeUser.email)
    $summary.pendingInvitationsCreated += 1
  }

  for ($taskIndex = 1; $taskIndex -le $TasksPerProject; $taskIndex += 1) {
    $taskTitle = 'perf-task-p{0:D4}-t{1:D4}' -f $projectIndex, $taskIndex
    $task = Create-Task -Session $ownerSession -ProjectId $project.id -TaskTitle $taskTitle
    $summary.tasksCreated += 1

    if ($DisableTaskStateDistribution.IsPresent) {
      continue
    }

    $targetState = $taskStates[($taskIndex - 1) % $taskStates.Count]
    if ($targetState -eq 'todo') {
      continue
    }

    Move-TaskState -Session $ownerSession -ProjectId $project.id -TaskId $task.id -StateKey $targetState
  }

  Write-Host ("Seeded {0}/{1} projects" -f $projectIndex, $ProjectCount)
}

Write-Host 'Performance seed complete.'
$summary.GetEnumerator() | ForEach-Object {
  Write-Host ("{0}: {1}" -f $_.Key, $_.Value)
}