# NocodeXConfig.psm1
# Modulo condiviso per tutti gli script dell'agente NOcodeX.
# Centralizza la lettura della configurazione e le funzioni helper comuni.

Set-StrictMode -Version Latest

function Get-NocodeXConfig {
    param(
        [string]$ConfigPath
    )

    if (-not $ConfigPath) {
        $ConfigPath = Join-Path (Split-Path $PSScriptRoot -Parent) "nocodex.config.json"
        if (-not (Test-Path $ConfigPath)) {
            $ConfigPath = Join-Path $PSScriptRoot ".." "nocodex.config.json"
        }
    }

    if (-not (Test-Path $ConfigPath)) {
        throw "File di configurazione non trovato: $ConfigPath"
    }

    $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    return $config
}

function Get-TargetRepo {
    param([Parameter(Mandatory)]$Config)
    return $Config.github.target_repo
}

function Get-DefaultBranch {
    param([Parameter(Mandatory)]$Config)
    $branch = $Config.github.default_branch
    if ([string]::IsNullOrWhiteSpace($branch)) { return "main" }
    return $branch
}

function Get-StateLabels {
    param([Parameter(Mandatory)]$Config)
    return @($Config.labels.states | ForEach-Object { $_.name })
}

function Get-PriorityLabels {
    param([Parameter(Mandatory)]$Config)
    return @($Config.labels.priorities | ForEach-Object { $_.name })
}

function Get-LabelCatalog {
    param([Parameter(Mandatory)]$Config)
    $catalog = @()
    foreach ($s in $Config.labels.states) {
        $catalog += @{ Name = $s.name; Color = $s.color; Description = $s.description }
    }
    foreach ($p in $Config.labels.priorities) {
        $catalog += @{ Name = $p.name; Color = $p.color; Description = $p.description }
    }
    return $catalog
}

function Get-DefaultPriority {
    param([Parameter(Mandatory)]$Config)
    $prio = $Config.labels.default_priority
    if ([string]::IsNullOrWhiteSpace($prio)) { return "p3" }
    return $prio
}

function Get-TestCommand {
    param([Parameter(Mandatory)]$Config)
    $cmd = $Config.github.test_command
    if ([string]::IsNullOrWhiteSpace($cmd)) { return "dotnet test *.sln -c Release" }
    return $cmd
}

function Get-WorkspaceDirectory {
    param([Parameter(Mandatory)]$Config)
    $dir = $Config.github.workspace_directory
    if ([string]::IsNullOrWhiteSpace($dir)) { $dir = "./workspaces" }
    if (-not [System.IO.Path]::IsPathRooted($dir)) {
        $root = Split-Path $PSScriptRoot -Parent
        $dir = Join-Path $root $dir
    }
    return [System.IO.Path]::GetFullPath($dir)
}

function Get-TargetRepoLocalPath {
    param([Parameter(Mandatory)]$Config)
    $workspace = Get-WorkspaceDirectory -Config $Config
    $repoName = ($Config.github.target_repo -split "/")[-1]
    return Join-Path $workspace $repoName
}

function Get-CommitMessage {
    param(
        [Parameter(Mandatory)]$Config,
        [string]$Prefix = "feat",
        [int]$IssueNumber,
        [string]$Title
    )
    $template = $Config.github.commit_conventions.message_template
    if ([string]::IsNullOrWhiteSpace($template)) {
        $template = "{prefix}: resolve #{issue_number} {title}"
    }
    $maxLen = $Config.github.commit_conventions.max_subject_length
    if ($maxLen -le 0) { $maxLen = 72 }

    $cleanTitle = ($Title -replace '\s+', ' ').Trim()
    $overhead = $template.Length - "{prefix}".Length - "{issue_number}".Length - "{title}".Length + $Prefix.Length + "$IssueNumber".Length
    $titleMax = $maxLen - $overhead
    if ($titleMax -lt 10) { $titleMax = 10 }
    if ($cleanTitle.Length -gt $titleMax) {
        $cleanTitle = $cleanTitle.Substring(0, $titleMax).Trim()
    }

    $msg = $template -replace '\{prefix\}', $Prefix
    $msg = $msg -replace '\{issue_number\}', $IssueNumber
    $msg = $msg -replace '\{title\}', $cleanTitle
    return $msg
}

function Get-PrTitle {
    param(
        [Parameter(Mandatory)]$Config,
        [int]$IssueNumber,
        [string]$Title
    )
    $template = $Config.github.pr_template.title_template
    if ([string]::IsNullOrWhiteSpace($template)) {
        $template = "Resolve #{issue_number} - {title}"
    }
    $result = $template -replace '\{issue_number\}', $IssueNumber
    $result = $result -replace '\{title\}', $Title
    return $result
}

function Get-PrBody {
    param(
        [Parameter(Mandatory)]$Config,
        [int]$IssueNumber
    )
    $template = $Config.github.pr_template.body_template
    if ([string]::IsNullOrWhiteSpace($template)) {
        $template = "Modifiche automatiche per #{issue_number}`n`nCloses #{issue_number}"
    }
    $result = $template -replace '\{issue_number\}', $IssueNumber
    return $result
}

function Require-Command {
    param([Parameter(Mandatory)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Comando richiesto '$Name' non trovato nel PATH."
    }
}

function Ensure-WorkflowLabels {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)]$LabelCatalog
    )

    foreach ($label in $LabelCatalog) {
        gh label create $label.Name --repo $Repository --color $label.Color --description $label.Description --force | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Impossibile creare/aggiornare la label '$($label.Name)'."
        }
    }
}

function Set-IssueWorkflowState {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][int]$Number,
        [Parameter(Mandatory)][string]$State,
        [Parameter(Mandatory)][string[]]$AllStateLabels
    )

    $remove = $AllStateLabels -join ","
    gh issue edit $Number --repo $Repository --remove-label $remove --add-label $State | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Impossibile impostare lo stato '$State' sull'issue #$Number."
    }
}

function Invoke-GhComment {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][int]$Number,
        [Parameter(Mandatory)][string]$Body
    )

    gh issue comment $Number --repo $Repository --body $Body | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Impossibile commentare l'issue #$Number."
    }
}

function Get-IssueNumberFromBranch {
    param([Parameter(Mandatory)][string]$Name)

    if ($Name -match '^issue/(\d+)-') {
        return [int]$Matches[1]
    }

    return $null
}

function Get-IssuePriorityRank {
    param(
        [Parameter(Mandatory)]$Issue,
        [Parameter(Mandatory)][string[]]$PriorityLabels
    )

    $labelNames = @($Issue.labels | ForEach-Object { $_.name })
    for ($i = 0; $i -lt $PriorityLabels.Count; $i++) {
        if ($labelNames -contains $PriorityLabels[$i]) {
            return $i
        }
    }
    return $PriorityLabels.Count
}

function New-SafeBranchName {
    param([Parameter(Mandatory)][string]$Title, [int]$Issue)

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
        throw "Impossibile leggere lo stato git."
    }

    if (-not [string]::IsNullOrWhiteSpace(($status -join ""))) {
        throw "Working tree non pulito. Committare o stashare le modifiche prima di eseguire il processore."
    }
}

Export-ModuleMember -Function *
