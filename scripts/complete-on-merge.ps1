[CmdletBinding()]
param(
    [string]$Repo,

    [Parameter(Mandatory = $true)]
    [string]$Branch,

    [string]$PullRequestUrl = "",

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

$issueNumber = Get-IssueNumberFromBranch -Name $Branch
if ($null -eq $issueNumber) {
    Write-Host "Il branch '$Branch' non corrisponde al pattern issue/<numero>-slug. Transizione di completamento saltata."
    return
}

Set-IssueWorkflowState -Repository $Repo -Number $issueNumber -State "completed" -AllStateLabels $stateLabels

if ([string]::IsNullOrWhiteSpace($PullRequestUrl)) {
    Invoke-GhComment -Repository $Repo -Number $issueNumber -Body "PR mergiata. Issue spostata in stato completed."
}
else {
    Invoke-GhComment -Repository $Repo -Number $issueNumber -Body "PR mergiata ($PullRequestUrl). Issue spostata in stato completed."
}
