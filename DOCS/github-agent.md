# NOcodeX GitHub Issue Runner

Questa guida configura NOcodeX per lavorare su issue GitHub in modo autonomo, con domande di chiarimento quando i requisiti non sono completi.

## Prerequisiti

- `gh` CLI autenticato (`GH_TOKEN` o `gh auth login`)
- `git`
- `.NET 10 SDK`
- Configurazione `nocodex.config.json` valida

## Esecuzione manuale (locale/VM)

```powershell
./scripts/process-github-issue.ps1 `
  -Repo owner/repo `
  -IssueNumber 123 `
  -CreatePullRequest `
  -CommentOnIssue
```

Per push diretto su `main`:

```powershell
./scripts/process-github-issue.ps1 `
  -Repo owner/repo `
  -IssueNumber 123 `
  -PushMain `
  -CommentOnIssue
```

## Flusso del runner

1. Legge title/body dell'issue con `gh issue view`.
2. Cerca un blocco comandi nel body, ad esempio:

````text
```nocodex
stack set dotnet-clean
llm status
```
````

3. Se mancano informazioni (Goal, Acceptance Criteria, blocco comandi), commenta l'issue con domande e si ferma.
4. Se il task e' chiaro:
   - sincronizza `main`
   - crea branch (oppure resta su `main` con `-PushMain`)
   - esegue i comandi `nocodex` richiesti
   - commit + push
   - commenta l'issue con esito

## Automazione GitHub Actions

Workflow: `.github/workflows/agent-issue-runner.yml`

- Trigger automatico con label issue:
  - `agent:run` -> branch + PR
  - `agent:push-main` -> push diretto su `main`
- Trigger manuale: `workflow_dispatch` con `issue_number` e `push_main`.

## Context7 MCP

Configurazione inclusa in `nocodex.config.json`:

```json
{
  "id": "mcp-context7",
  "transport": "stdio",
  "endpoint": "npx -y @upstash/context7-mcp",
  "enabled": true
}
```

Variabile opzionale:

```bash
CONTEXT7_API_KEY=...
```
