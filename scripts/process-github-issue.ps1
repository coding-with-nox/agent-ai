[CmdletBinding()]
param(
    [string]$Repo,

    [Parameter(Mandatory = $true)]
    [int]$IssueNumber,

    [string]$ProjectRoot,

    [switch]$PushMain,

    [switch]$CreatePullRequest,

    [switch]$CommentOnIssue,

    [switch]$SkipStateManagement,

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
$priorityLabels = Get-PriorityLabels -Config $config
$labelCatalog = Get-LabelCatalog -Config $config
$defaultBranch = Get-DefaultBranch -Config $config

function Get-AgentCommandsFromBody {
    param([Parameter(Mandatory = $true)][string]$Body)

    $match = [regex]::Match(
        $Body,
        '(?ms)```(?:nocodex|agent)\s*(?<content>.*?)```',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    if (-not $match.Success) {
        return @()
    }

    $commands = @()
    $lines = $match.Groups["content"].Value -split "(`r`n|`n|`r)"
    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        if ($trimmed.StartsWith("#", [System.StringComparison]::Ordinal)) {
            continue
        }

        $commands += $trimmed
    }

    return $commands
}

function Split-CommandLine {
    param([Parameter(Mandatory = $true)][string]$CommandLine)

    $tokens = @()
    $matches = [regex]::Matches(
        $CommandLine,
        '"([^"]*)"|''([^'']*)''|[^\s]+')

    foreach ($match in $matches) {
        if ($match.Groups[1].Success) {
            $tokens += $match.Groups[1].Value
            continue
        }

        if ($match.Groups[2].Success) {
            $tokens += $match.Groups[2].Value
            continue
        }

        $tokens += $match.Value
    }

    return $tokens
}

function Get-ClarificationQuestions {
    param(
        [Parameter(Mandatory = $true)][string]$Body,
        [Parameter(Mandatory = $true)][string[]]$Commands
    )

    $questions = @()

    if ($Body -notmatch '(?im)^##\s*(Goal|Obiettivo)\b') {
        $questions += "Puoi aggiungere una sezione '## Goal' (o '## Obiettivo') con il risultato atteso?"
    }

    if ($Body -notmatch '(?im)^##\s*(Acceptance Criteria|Criteri di Accettazione)\b') {
        $questions += "Quali sono i criteri di accettazione verificabili?"
    }

    if ($Commands.Count -eq 0) {
        $questions += "Puoi aggiungere un blocco nocodex con i comandi da eseguire in autonomia?"
    }

    if ($Body -match '(?im)\b(TBD|TO BE DEFINED|TODO|DA DEFINIRE)\b') {
        $questions += "Nel testo ci sono punti ancora da definire (TBD/TODO). Puoi completarli prima dell'esecuzione?"
    }

    return $questions
}

function Ensure-IssueHasPriority {
    param(
        [Parameter(Mandatory = $true)]$Issue,
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][int]$Number
    )

    $labelNames = @($Issue.labels | ForEach-Object { $_.name })
    $priority = $priorityLabels | Where-Object { $labelNames -contains $_ } | Select-Object -First 1

    if ($null -eq $priority) {
        $defaultPrio = Get-DefaultPriority -Config $config
        gh issue edit $Number --repo $Repository --add-label $defaultPrio | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Impossibile aggiungere la priorita predefinita '$defaultPrio' all'issue #$Number."
        }
    }
}

Require-Command "gh"
Require-Command "git"
Require-Command "dotnet"

Push-Location $ProjectRoot
try {
    Ensure-WorkflowLabels -Repository $Repo -LabelCatalog $labelCatalog

    $issueJson = gh issue view $IssueNumber --repo $Repo --json number,title,body,url,state,labels
    if ($LASTEXITCODE -ne 0) {
        throw "Impossibile leggere l'issue #$IssueNumber da $Repo."
    }

    $issue = $issueJson | ConvertFrom-Json
    if ($issue.state -ne "OPEN") {
        Write-Host "Issue #$IssueNumber non e aperta. Nessuna azione."
        return
    }

    Ensure-IssueHasPriority -Issue $issue -Repository $Repo -Number $IssueNumber

    if (-not $SkipStateManagement) {
        Set-IssueWorkflowState -Repository $Repo -Number $IssueNumber -State "in progress" -AllStateLabels $stateLabels
    }

    $commands = Get-AgentCommandsFromBody -Body $issue.body
    $questions = Get-ClarificationQuestions -Body $issue.body -Commands $commands

    if ($questions.Count -gt 0) {
        $questionList = $questions | ForEach-Object { "- $_" }
        $comment = @(
            "Serve chiarimento prima di eseguire in autonomia l'issue #$IssueNumber.",
            "",
            "Domande:",
            $questionList
        ) -join "`n"

        Write-Host $comment
        if ($CommentOnIssue) {
            Invoke-GhComment -Repository $Repo -Number $IssueNumber -Body $comment
        }

        if (-not $SkipStateManagement) {
            Set-IssueWorkflowState -Repository $Repo -Number $IssueNumber -State "wf reply" -AllStateLabels $stateLabels
        }

        return
    }

    Assert-CleanWorktree

    git fetch origin $defaultBranch
    if ($LASTEXITCODE -ne 0) { throw "git fetch origin $defaultBranch fallito." }

    git checkout $defaultBranch
    if ($LASTEXITCODE -ne 0) { throw "git checkout $defaultBranch fallito." }

    git pull --ff-only origin $defaultBranch
    if ($LASTEXITCODE -ne 0) { throw "git pull --ff-only origin $defaultBranch fallito." }

    $workingBranch = $defaultBranch
    if (-not $PushMain) {
        $workingBranch = New-SafeBranchName -Title $issue.title -Issue $IssueNumber
        git checkout -B $workingBranch
        if ($LASTEXITCODE -ne 0) { throw "git checkout -B $workingBranch fallito." }
    }

    foreach ($commandLine in $commands) {
        $tokens = Split-CommandLine -CommandLine $commandLine
        if ($tokens.Count -eq 0) {
            continue
        }

        Write-Host "Running: nocodex $($tokens -join ' ')"
        & dotnet run --project src/NocodeX.Cli/NocodeX.Cli.csproj -- @tokens
        if ($LASTEXITCODE -ne 0) {
            throw "Comando fallito: nocodex $commandLine"
        }
    }

    $changed = git status --porcelain
    if ($LASTEXITCODE -ne 0) { throw "Impossibile leggere lo stato git dopo l'esecuzione." }

    if ([string]::IsNullOrWhiteSpace(($changed -join ""))) {
        $noChangeMessage = "Esecuzione completata su #$IssueNumber ma non sono state prodotte modifiche da committare."
        Write-Host $noChangeMessage
        if ($CommentOnIssue) {
            Invoke-GhComment -Repository $Repo -Number $IssueNumber -Body $noChangeMessage
        }

        if (-not $SkipStateManagement) {
            Set-IssueWorkflowState -Repository $Repo -Number $IssueNumber -State "wf reply" -AllStateLabels $stateLabels
        }

        return
    }

    git add -A
    if ($LASTEXITCODE -ne 0) { throw "git add fallito." }

    $cleanTitle = [regex]::Replace($issue.title, '\s+', ' ').Trim()
    $commitMessage = Get-CommitMessage -Config $config -Prefix "feat" -IssueNumber $IssueNumber -Title $cleanTitle
    git commit -m $commitMessage
    if ($LASTEXITCODE -ne 0) { throw "git commit fallito." }

    if ($PushMain) {
        git push origin $defaultBranch
        if ($LASTEXITCODE -ne 0) { throw "git push origin $defaultBranch fallito." }

        if (-not $SkipStateManagement) {
            Set-IssueWorkflowState -Repository $Repo -Number $IssueNumber -State "completed" -AllStateLabels $stateLabels
        }

        $doneMessage = "Completato #$IssueNumber: commit pushato direttamente su $defaultBranch."
        Write-Host $doneMessage
        if ($CommentOnIssue) {
            Invoke-GhComment -Repository $Repo -Number $IssueNumber -Body $doneMessage
        }
    }
    else {
        git push -u origin $workingBranch
        if ($LASTEXITCODE -ne 0) { throw "git push -u origin $workingBranch fallito." }

        if (-not $SkipStateManagement) {
            Set-IssueWorkflowState -Repository $Repo -Number $IssueNumber -State "test" -AllStateLabels $stateLabels
        }

        $message = "Completato #$IssueNumber: modifiche pushate su branch $workingBranch e ticket in stato test."

        if ($CreatePullRequest) {
            $prTitle = Get-PrTitle -Config $config -IssueNumber $IssueNumber -Title $cleanTitle
            $prBody = Get-PrBody -Config $config -IssueNumber $IssueNumber
            gh pr create --repo $Repo --base $defaultBranch --head $workingBranch --title $prTitle --body $prBody | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Impossibile creare la pull request per il branch $workingBranch."
            }

            $message += " PR creata verso $defaultBranch."
        }

        Write-Host $message
        if ($CommentOnIssue) {
            Invoke-GhComment -Repository $Repo -Number $IssueNumber -Body $message
        }
    }
}
finally {
    Pop-Location
}
