[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Repo,

    [Parameter(Mandatory = $true)]
    [int]$IssueNumber
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:StateLabels = @("todo", "in progress", "wf reply", "test", "release", "completed")

function Require-Command {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Set-IssueWorkflowState {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][int]$Number,
        [Parameter(Mandatory = $true)][string]$State
    )

    $remove = $script:StateLabels -join ","
    gh issue edit $Number --repo $Repository --remove-label $remove --add-label $State | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set workflow state '$State' on issue #$Number."
    }
}

Require-Command "gh"

$issueJson = gh issue view $IssueNumber --repo $Repo --json labels,state
if ($LASTEXITCODE -ne 0) {
    throw "Failed to read issue #$IssueNumber."
}

$issue = $issueJson | ConvertFrom-Json
if ($issue.state -ne "OPEN") {
    Write-Host "Issue #$IssueNumber is not open."
    return
}

$labels = @($issue.labels | ForEach-Object { $_.name })
if ($labels -notcontains "wf reply") {
    Write-Host "Issue #$IssueNumber is not in wf reply. Nothing to do."
    return
}

Set-IssueWorkflowState -Repository $Repo -Number $IssueNumber -State "todo"
Write-Host "Issue #$IssueNumber moved from wf reply to todo."
