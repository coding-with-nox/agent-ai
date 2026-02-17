[CmdletBinding()]
param(
    [string]$Repo,

    [Parameter(Mandatory = $true)]
    [string]$Branch,

    [string]$ProjectRoot,

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

$stateLabels = Get-StateLabels -Config $config
$labelCatalog = Get-LabelCatalog -Config $config
$defaultBranch = Get-DefaultBranch -Config $config
$testCommand = Get-TestCommand -Config $config

Require-Command "gh"

$issueNumber = Get-IssueNumberFromBranch -Name $Branch
if ($null -eq $issueNumber) {
    Write-Host "Il branch '$Branch' non corrisponde al pattern issue/<numero>-slug. Transizione di stato saltata."
    return
}

Push-Location $ProjectRoot
try {
    Ensure-WorkflowLabels -Repository $Repo -LabelCatalog $labelCatalog

    $logPath = Join-Path $ProjectRoot ".nocodex-test-output.log"
    if (Test-Path $logPath) {
        Remove-Item $logPath -Force
    }

    Write-Host "Esecuzione test: $testCommand"
    Invoke-Expression "$testCommand *> `"$logPath`""
    $testExitCode = $LASTEXITCODE

    if ($testExitCode -eq 0) {
        Set-IssueWorkflowState -Repository $Repo -Number $issueNumber -State "release" -AllStateLabels $stateLabels

        $prJson = gh pr list --repo $Repo --head $Branch --state open --json number,url
        if ($LASTEXITCODE -ne 0) {
            throw "Impossibile interrogare le pull request per il branch $Branch."
        }

        $prs = @($prJson | ConvertFrom-Json)
        if ($prs.Count -eq 0) {
            $prTitle = Get-PrTitle -Config $config -IssueNumber $issueNumber -Title "Release #$issueNumber"
            $prBody = Get-PrBody -Config $config -IssueNumber $issueNumber
            gh pr create --repo $Repo --base $defaultBranch --head $Branch --title $prTitle --body $prBody | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Impossibile creare la pull request per il branch $Branch."
            }
        }

        Invoke-GhComment -Repository $Repo -Number $issueNumber -Body "Test pipeline OK. Issue spostata in stato release e PR pronta per review."
        return
    }

    $tail = "Nessun output catturato."
    if (Test-Path $logPath) {
        $tail = (Get-Content $logPath -Tail 120) -join "`n"
    }

    Set-IssueWorkflowState -Repository $Repo -Number $issueNumber -State "todo" -AllStateLabels $stateLabels

    $comment = @(
        "Test pipeline FAILED su branch $Branch. Issue riportata in stato todo.",
        "",
        "Estratto errori:",
        "``````text",
        $tail,
        "``````"
    ) -join "`n"

    Invoke-GhComment -Repository $Repo -Number $issueNumber -Body $comment
    throw "Test falliti per l'issue #$issueNumber."
}
finally {
    Pop-Location
}
