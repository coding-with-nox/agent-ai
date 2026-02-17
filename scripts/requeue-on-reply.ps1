[CmdletBinding()]
param(
    [string]$Repo,

    [Parameter(Mandatory = $true)]
    [int]$IssueNumber,

    [string]$ConfigPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "NocodeXConfig.psm1") -Force

$config = Get-NocodeXConfig -ConfigPath $ConfigPath

if ([string]::IsNullOrWhiteSpace($Repo)) {
    $Repo = Get-TargetRepo -Config $config
}

$stateLabels = Get-StateLabels -Config $config

Require-Command "gh"

$issueJson = gh issue view $IssueNumber --repo $Repo --json labels,state
if ($LASTEXITCODE -ne 0) {
    throw "Impossibile leggere l'issue #$IssueNumber."
}

$issue = $issueJson | ConvertFrom-Json
if ($issue.state -ne "OPEN") {
    Write-Host "Issue #$IssueNumber non e aperta."
    return
}

$labels = @($issue.labels | ForEach-Object { $_.name })
if ($labels -notcontains "wf reply") {
    Write-Host "Issue #$IssueNumber non e in stato wf reply. Nessuna azione."
    return
}

Set-IssueWorkflowState -Repository $Repo -Number $IssueNumber -State "todo" -AllStateLabels $stateLabels
Write-Host "Issue #$IssueNumber spostata da wf reply a todo."
