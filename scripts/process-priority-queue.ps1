[CmdletBinding()]
param(
    [string]$Repo,

    [string]$ProjectRoot,

    [switch]$CommentOnIssue,

    [string]$ConfigPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "NocodeXConfig.psm1") -Force

$config = Get-NocodeXConfig -ConfigPath $ConfigPath

if ([string]::IsNullOrWhiteSpace($Repo)) {
    $Repo = Get-TargetRepo -Config $config
}

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Get-TargetRepoLocalPath -Config $config
}

$priorityLabels = Get-PriorityLabels -Config $config

function Get-ActiveIssueCount {
    param([Parameter(Mandatory = $true)][string]$Repository)

    $inProgressJson = gh issue list --repo $Repository --state open --label "in progress" --limit 100 --json number
    if ($LASTEXITCODE -ne 0) { throw "Impossibile leggere le issue in progress." }

    $testJson = gh issue list --repo $Repository --state open --label "test" --limit 100 --json number
    if ($LASTEXITCODE -ne 0) { throw "Impossibile leggere le issue in test." }

    $inProgress = $inProgressJson | ConvertFrom-Json
    $test = $testJson | ConvertFrom-Json

    return (@($inProgress).Count + @($test).Count)
}

Require-Command "gh"

Push-Location $ProjectRoot
try {
    $activeCount = Get-ActiveIssueCount -Repository $Repo
    if ($activeCount -gt 0) {
        Write-Host "Queue in pausa: ci sono gia $activeCount issue in stato in progress/test."
        return
    }

    $todoJson = gh issue list --repo $Repo --state open --label "todo" --limit 200 --json number,title,createdAt,labels
    if ($LASTEXITCODE -ne 0) {
        throw "Impossibile elencare le issue todo."
    }

    $todo = @($todoJson | ConvertFrom-Json)
    if ($todo.Count -eq 0) {
        Write-Host "Nessuna issue todo trovata."
        return
    }

    $nextIssue = $todo |
        Sort-Object @{ Expression = { Get-IssuePriorityRank -Issue $_ -PriorityLabels $priorityLabels } }, @{ Expression = { [DateTime]::Parse($_.createdAt) } } |
        Select-Object -First 1

    Write-Host "Selezionata issue #$($nextIssue.number) ($($nextIssue.title)) dalla coda todo."

    $scriptArgs = @{
        Repo = $Repo
        IssueNumber = $nextIssue.number
        ProjectRoot = $ProjectRoot
    }
    if ($ConfigPath) { $scriptArgs.ConfigPath = $ConfigPath }
    if ($CommentOnIssue) { $scriptArgs.CommentOnIssue = $true }

    & (Join-Path $PSScriptRoot "process-github-issue.ps1") @scriptArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Elaborazione fallita per l'issue #$($nextIssue.number)."
    }
}
finally {
    Pop-Location
}
