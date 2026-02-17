[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Repo,

    [Parameter(Mandatory = $true)]
    [string]$Branch,

    [string]$PullRequestUrl = ""
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

function Invoke-GhComment {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][int]$Number,
        [Parameter(Mandatory = $true)][string]$Body
    )

    gh issue comment $Number --repo $Repository --body $Body | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to comment on issue #$Number."
    }
}

function Get-IssueNumberFromBranch {
    param([Parameter(Mandatory = $true)][string]$Name)

    if ($Name -match '^issue/(\d+)-') {
        return [int]$Matches[1]
    }

    return $null
}

Require-Command "gh"

$issueNumber = Get-IssueNumberFromBranch -Name $Branch
if ($null -eq $issueNumber) {
    Write-Host "Branch '$Branch' does not map to issue/<number>-slug. Skipping completion transition."
    return
}

Set-IssueWorkflowState -Repository $Repo -Number $issueNumber -State "completed"

if ([string]::IsNullOrWhiteSpace($PullRequestUrl)) {
    Invoke-GhComment -Repository $Repo -Number $issueNumber -Body "PR mergiata. Issue spostata in stato completed."
}
else {
    Invoke-GhComment -Repository $Repo -Number $issueNumber -Body "PR mergiata ($PullRequestUrl). Issue spostata in stato completed."
}
