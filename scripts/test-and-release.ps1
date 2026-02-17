[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Repo,

    [Parameter(Mandatory = $true)]
    [string]$Branch,

    [string]$ProjectRoot = "."
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:StateLabels = @("todo", "in progress", "wf reply", "test", "release", "completed")
$script:LabelCatalog = @(
    @{ Name = "todo"; Color = "1D76DB"; Description = "Ready for agent pickup" },
    @{ Name = "in progress"; Color = "FBCA04"; Description = "Agent is implementing" },
    @{ Name = "wf reply"; Color = "D93F0B"; Description = "Waiting for user clarification" },
    @{ Name = "test"; Color = "5319E7"; Description = "Ready for CI tests" },
    @{ Name = "release"; Color = "0E8A16"; Description = "Tests passed, PR ready" },
    @{ Name = "completed"; Color = "0E8A16"; Description = "PR merged and task completed" }
)

function Require-Command {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Ensure-WorkflowLabels {
    param([Parameter(Mandatory = $true)][string]$Repository)

    foreach ($label in $script:LabelCatalog) {
        gh label create $label.Name --repo $Repository --color $label.Color --description $label.Description --force | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to ensure label '$($label.Name)'."
        }
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
Require-Command "dotnet"

$issueNumber = Get-IssueNumberFromBranch -Name $Branch
if ($null -eq $issueNumber) {
    Write-Host "Branch '$Branch' does not map to issue/<number>-slug. Skipping state transition."
    return
}

Push-Location $ProjectRoot
try {
    Ensure-WorkflowLabels -Repository $Repo

    $logPath = Join-Path $ProjectRoot ".nocodex-test-output.log"
    if (Test-Path $logPath) {
        Remove-Item $logPath -Force
    }

    & dotnet test NocodeX.sln -c Release *> $logPath
    $testExitCode = $LASTEXITCODE

    if ($testExitCode -eq 0) {
        Set-IssueWorkflowState -Repository $Repo -Number $issueNumber -State "release"

        $prJson = gh pr list --repo $Repo --head $Branch --state open --json number,url
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to query pull requests for branch $Branch."
        }

        $prs = @($prJson | ConvertFrom-Json)
        if ($prs.Count -eq 0) {
            $prTitle = "Release #$issueNumber"
            $prBody = "Automated release candidate for #$issueNumber.`n`nCloses #$issueNumber"
            gh pr create --repo $Repo --base main --head $Branch --title $prTitle --body $prBody | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to create pull request for branch $Branch."
            }
        }

        Invoke-GhComment -Repository $Repo -Number $issueNumber -Body "Test pipeline OK. Issue spostata in stato release e PR pronta per review."
        return
    }

    $tail = "No output captured."
    if (Test-Path $logPath) {
        $tail = (Get-Content $logPath -Tail 120) -join "`n"
    }

    Set-IssueWorkflowState -Repository $Repo -Number $issueNumber -State "todo"

    $comment = @(
        "Test pipeline FAILED su branch $Branch. Issue riportata in stato todo.",
        "",
        "Estratto errori:",
        "```text",
        $tail,
        "```"
    ) -join "`n"

    Invoke-GhComment -Repository $Repo -Number $issueNumber -Body $comment
    throw "dotnet test failed for issue #$issueNumber."
}
finally {
    Pop-Location
}
