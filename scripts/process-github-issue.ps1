[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Repo,

    [Parameter(Mandatory = $true)]
    [int]$IssueNumber,

    [string]$ProjectRoot = ".",

    [switch]$PushMain,

    [switch]$CreatePullRequest,

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

function New-SafeBranchName {
    param([Parameter(Mandatory = $true)][string]$Title, [int]$Issue)

    $slug = $Title.ToLowerInvariant()
    $slug = [regex]::Replace($slug, '[^a-z0-9]+', '-')
    $slug = $slug.Trim('-')
    if ($slug.Length -gt 48) {
        $slug = $slug.Substring(0, 48).Trim('-')
    }

    if ([string]::IsNullOrWhiteSpace($slug)) {
        $slug = "task"
    }

    return "issue/$Issue-$slug"
}

function Assert-CleanWorktree {
    $status = git status --porcelain
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to inspect git status."
    }

    if (-not [string]::IsNullOrWhiteSpace(($status -join ""))) {
        throw "Working tree is not clean. Commit or stash changes before running the issue processor."
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

Require-Command "gh"
Require-Command "git"
Require-Command "dotnet"

Push-Location $ProjectRoot
try {
    $issueJson = gh issue view $IssueNumber --repo $Repo --json number,title,body,url,state
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to read GitHub issue #$IssueNumber from $Repo."
    }

    $issue = $issueJson | ConvertFrom-Json
    if ($issue.state -ne "OPEN") {
        Write-Host "Issue #$IssueNumber is not open. Nothing to do."
        return
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

        return
    }

    Assert-CleanWorktree

    git fetch origin main
    if ($LASTEXITCODE -ne 0) { throw "git fetch origin main failed." }

    git checkout main
    if ($LASTEXITCODE -ne 0) { throw "git checkout main failed." }

    git pull --ff-only origin main
    if ($LASTEXITCODE -ne 0) { throw "git pull --ff-only origin main failed." }

    $workingBranch = "main"
    if (-not $PushMain) {
        $workingBranch = New-SafeBranchName -Title $issue.title -Issue $IssueNumber
        git checkout -B $workingBranch
        if ($LASTEXITCODE -ne 0) { throw "git checkout -B $workingBranch failed." }
    }

    foreach ($commandLine in $commands) {
        $tokens = Split-CommandLine -CommandLine $commandLine
        if ($tokens.Count -eq 0) {
            continue
        }

        Write-Host "Running: nocodex $($tokens -join ' ')"
        & dotnet run --project src/NocodeX.Cli/NocodeX.Cli.csproj -- @tokens
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed: nocodex $commandLine"
        }
    }

    $changed = git status --porcelain
    if ($LASTEXITCODE -ne 0) { throw "Unable to inspect git status after execution." }

    if ([string]::IsNullOrWhiteSpace(($changed -join ""))) {
        $noChangeMessage = "Esecuzione completata su #$IssueNumber ma non sono state prodotte modifiche da committare."
        Write-Host $noChangeMessage
        if ($CommentOnIssue) {
            Invoke-GhComment -Repository $Repo -Number $IssueNumber -Body $noChangeMessage
        }
        return
    }

    git add -A
    if ($LASTEXITCODE -ne 0) { throw "git add failed." }

    $cleanTitle = [regex]::Replace($issue.title, '\s+', ' ').Trim()
    if ($cleanTitle.Length -gt 60) {
        $cleanTitle = $cleanTitle.Substring(0, 60).Trim()
    }

    $commitMessage = "feat: resolve #$IssueNumber $cleanTitle"
    git commit -m $commitMessage
    if ($LASTEXITCODE -ne 0) { throw "git commit failed." }

    if ($PushMain) {
        git push origin main
        if ($LASTEXITCODE -ne 0) { throw "git push origin main failed." }

        $doneMessage = "Completato #$IssueNumber: commit pushato direttamente su `main`."
        Write-Host $doneMessage
        if ($CommentOnIssue) {
            Invoke-GhComment -Repository $Repo -Number $IssueNumber -Body $doneMessage
        }
    }
    else {
        git push -u origin $workingBranch
        if ($LASTEXITCODE -ne 0) { throw "git push -u origin $workingBranch failed." }

        $message = "Completato #$IssueNumber: modifiche pushate su branch `$workingBranch`."

        if ($CreatePullRequest) {
            $prTitle = "Resolve #$IssueNumber - $cleanTitle"
            $prBody = "Automated changes for #$IssueNumber"
            gh pr create --repo $Repo --base main --head $workingBranch --title $prTitle --body $prBody | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to create pull request for branch $workingBranch."
            }

            $message += " PR creata verso `main`."
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
