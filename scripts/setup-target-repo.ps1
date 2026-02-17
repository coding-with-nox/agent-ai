[CmdletBinding()]
param(
    [string]$ConfigPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "NocodeXConfig.psm1") -Force

$config = Get-NocodeXConfig -ConfigPath $ConfigPath
$targetRepo = Get-TargetRepo -Config $config
$defaultBranch = Get-DefaultBranch -Config $config
$autoCreate = $config.github.auto_create
$visibility = $config.github.visibility
$workspace = Get-WorkspaceDirectory -Config $config
$localPath = Get-TargetRepoLocalPath -Config $config
$labelCatalog = Get-LabelCatalog -Config $config

Require-Command "gh"
Require-Command "git"

if ([string]::IsNullOrWhiteSpace($targetRepo)) {
    throw "Configurazione mancante: github.target_repo non specificato in nocodex.config.json."
}

Write-Host "Target repository: $targetRepo"
Write-Host "Default branch: $defaultBranch"
Write-Host "Workspace: $workspace"

# --- Step 1: Verifica se la repo esiste ---
$repoExists = $true
$null = gh repo view $targetRepo --json name 2>&1
if ($LASTEXITCODE -ne 0) {
    $repoExists = $false
}

# --- Step 2: Crea la repo se non esiste ---
if (-not $repoExists) {
    if (-not $autoCreate) {
        throw "Il repository '$targetRepo' non esiste e auto_create e disabilitato nel config."
    }

    Write-Host "Creazione repository '$targetRepo' ($visibility)..."
    gh repo create $targetRepo --$visibility --confirm
    if ($LASTEXITCODE -ne 0) {
        throw "Impossibile creare il repository '$targetRepo'."
    }

    Write-Host "Repository '$targetRepo' creato."
}

# --- Step 3: Sincronizza label ---
Write-Host "Sincronizzazione label su '$targetRepo'..."
Ensure-WorkflowLabels -Repository $targetRepo -LabelCatalog $labelCatalog
Write-Host "Label sincronizzate: $($labelCatalog.Count)"

# --- Step 4: Prepara la directory workspace ---
if (-not (Test-Path $workspace)) {
    New-Item -ItemType Directory -Path $workspace -Force | Out-Null
    Write-Host "Directory workspace creata: $workspace"
}

# --- Step 5: Clona o aggiorna la repo ---
if (Test-Path $localPath) {
    Write-Host "Repository gia clonato in '$localPath'. Aggiornamento..."
    Push-Location $localPath
    try {
        git fetch origin
        if ($LASTEXITCODE -ne 0) { throw "git fetch origin fallito." }

        git checkout $defaultBranch
        if ($LASTEXITCODE -ne 0) { throw "git checkout $defaultBranch fallito." }

        git pull --ff-only origin $defaultBranch
        if ($LASTEXITCODE -ne 0) { throw "git pull --ff-only origin $defaultBranch fallito." }
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Host "Clonazione '$targetRepo' in '$localPath'..."
    gh repo clone $targetRepo $localPath
    if ($LASTEXITCODE -ne 0) {
        throw "Impossibile clonare il repository '$targetRepo'."
    }
}

# --- Step 6: Se repo appena creata, copia workflow, script e template ---
if (-not $repoExists) {
    Write-Host "Setup iniziale: copia workflow, script e template nella repo target..."
    $agentRoot = Split-Path $PSScriptRoot -Parent

    # Copia workflow
    $targetWorkflowDir = Join-Path $localPath ".github" "workflows"
    New-Item -ItemType Directory -Path $targetWorkflowDir -Force | Out-Null

    $workflowFiles = @(
        "agent-issue-runner.yml",
        "agent-priority-queue.yml",
        "agent-test-and-release.yml",
        "agent-complete-on-merge.yml",
        "agent-requeue-on-reply.yml"
    )
    foreach ($wf in $workflowFiles) {
        $src = Join-Path $agentRoot ".github" "workflows" $wf
        if (Test-Path $src) {
            Copy-Item $src (Join-Path $targetWorkflowDir $wf) -Force
            Write-Host "  Copiato workflow: $wf"
        }
    }

    # Copia script
    $targetScriptsDir = Join-Path $localPath "scripts"
    New-Item -ItemType Directory -Path $targetScriptsDir -Force | Out-Null

    $scriptFiles = Get-ChildItem -Path $PSScriptRoot -Filter "*.ps1"
    foreach ($sf in $scriptFiles) {
        Copy-Item $sf.FullName (Join-Path $targetScriptsDir $sf.Name) -Force
        Write-Host "  Copiato script: $($sf.Name)"
    }
    $moduleFiles = Get-ChildItem -Path $PSScriptRoot -Filter "*.psm1"
    foreach ($mf in $moduleFiles) {
        Copy-Item $mf.FullName (Join-Path $targetScriptsDir $mf.Name) -Force
        Write-Host "  Copiato modulo: $($mf.Name)"
    }

    # Copia issue template
    $srcTemplateDir = Join-Path $agentRoot ".github" "issue-template"
    if (Test-Path $srcTemplateDir) {
        $targetTemplateDir = Join-Path $localPath ".github" "issue-template"
        New-Item -ItemType Directory -Path $targetTemplateDir -Force | Out-Null
        $templates = Get-ChildItem -Path $srcTemplateDir -File
        foreach ($t in $templates) {
            Copy-Item $t.FullName (Join-Path $targetTemplateDir $t.Name) -Force
            Write-Host "  Copiato template: $($t.Name)"
        }
    }

    # Copia config
    $configSrc = Join-Path $agentRoot "nocodex.config.json"
    if (Test-Path $configSrc) {
        Copy-Item $configSrc $localPath -Force
        Write-Host "  Copiato: nocodex.config.json"
    }

    # Copia agent-instructions
    $instrSrc = Join-Path $agentRoot "agent-instructions.md"
    if (Test-Path $instrSrc) {
        Copy-Item $instrSrc $localPath -Force
        Write-Host "  Copiato: agent-instructions.md"
    }

    # Commit iniziale
    Push-Location $localPath
    try {
        git config user.name "nocodex-agent[bot]"
        git config user.email "nocodex-agent[bot]@users.noreply.github.com"

        git add -A
        if ($LASTEXITCODE -ne 0) { throw "git add fallito." }

        git commit -m "chore: setup iniziale NOcodeX agent workflow"
        if ($LASTEXITCODE -ne 0) { throw "git commit fallito." }

        git push origin $defaultBranch
        if ($LASTEXITCODE -ne 0) { throw "git push fallito." }

        Write-Host "Commit iniziale pushato su $defaultBranch."
    }
    finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "Setup completato per '$targetRepo'."
Write-Host "  Percorso locale: $localPath"
Write-Host "  Label sincronizzate: $($labelCatalog.Count)"
Write-Host "  Stato: $(if ($repoExists) { 'aggiornato' } else { 'creato e configurato' })"
