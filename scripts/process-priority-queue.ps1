[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Repo,

    [string]$ProjectRoot = ".",

    [switch]$CommentOnIssue
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Require-Command {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Get-IssuePriorityRank {
    param([Parameter(Mandatory = $true)]$Issue)

    $labelNames = @($Issue.labels | ForEach-Object { $_.name })
    if ($labelNames -contains "p0") { return 0 }
    if ($labelNames -contains "p1") { return 1 }
    if ($labelNames -contains "p2") { return 2 }
    return 3
}

function Get-ActiveIssueCount {
    param([Parameter(Mandatory = $true)][string]$Repository)

    $inProgressJson = gh issue list --repo $Repository --state open --label "in progress" --limit 100 --json number
    if ($LASTEXITCODE -ne 0) { throw "Failed to read in-progress issues." }

    $testJson = gh issue list --repo $Repository --state open --label "test" --limit 100 --json number
    if ($LASTEXITCODE -ne 0) { throw "Failed to read test issues." }

    $inProgress = $inProgressJson | ConvertFrom-Json
    $test = $testJson | ConvertFrom-Json

    return (@($inProgress).Count + @($test).Count)
}

Require-Command "gh"

Push-Location $ProjectRoot
try {
    $activeCount = Get-ActiveIssueCount -Repository $Repo
    if ($activeCount -gt 0) {
        Write-Host "Queue paused: there are already $activeCount issue(s) in progress/test."
        return
    }

    $todoJson = gh issue list --repo $Repo --state open --label "todo" --limit 200 --json number,title,createdAt,labels
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to list todo issues."
    }

    $todo = @($todoJson | ConvertFrom-Json)
    if ($todo.Count -eq 0) {
        Write-Host "No todo issues found."
        return
    }

    $nextIssue = $todo |
        Sort-Object @{ Expression = { Get-IssuePriorityRank -Issue $_ } }, @{ Expression = { [DateTime]::Parse($_.createdAt) } } |
        Select-Object -First 1

    Write-Host "Picked issue #$($nextIssue.number) ($($nextIssue.title)) from todo queue."
    if ($CommentOnIssue) {
        & ./scripts/process-github-issue.ps1 -Repo $Repo -IssueNumber $nextIssue.number -ProjectRoot $ProjectRoot -CommentOnIssue
    }
    else {
        & ./scripts/process-github-issue.ps1 -Repo $Repo -IssueNumber $nextIssue.number -ProjectRoot $ProjectRoot
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to process queued issue #$($nextIssue.number)."
    }
}
finally {
    Pop-Location
}
