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

## Flusso stato issue

Stati supportati (label):

- `todo`
- `in progress`
- `wf reply`
- `test`
- `release`
- `completed`

Priorita' supportate:

- `p0` (massima)
- `p1`
- `p2`
- `p3`

## Flusso operativo

1. L'agente prende un'issue in `todo` in base alla priorita' (`p0` > `p1` > `p2` > `p3`).
2. Sposta l'issue in `in progress`.
3. Legge title/body con `gh issue view`.
4. Cerca un blocco comandi nel body, ad esempio:

````text
```nocodex
stack set dotnet-clean
llm status
```
````

5. Se mancano informazioni (Goal, Acceptance Criteria, blocco comandi), commenta l'issue e la sposta in `wf reply`.
6. Se il task e' chiaro:
   - crea branch `issue/<id>-slug`
   - esegue i comandi `nocodex` richiesti
   - commit + push
   - sposta l'issue in `test`
7. La pipeline test esegue `dotnet test`:
   - success: issue -> `release` + creazione PR verso `main`
   - failure: issue -> `todo` + commento con estratto errore
8. Quando la PR viene mergiata, issue -> `completed`.

## Automazione GitHub Actions

Workflow principali:

- `.github/workflows/agent-priority-queue.yml`
  - scheduler + manuale
  - prende la prossima issue `todo` con priorita' massima
- `.github/workflows/agent-test-and-release.yml`
  - trigger su push branch `issue/**`
  - esegue test, porta in `release` o ritorna in `todo`
- `.github/workflows/agent-complete-on-merge.yml`
  - trigger su merge PR
  - porta issue in `completed`
- `.github/workflows/agent-requeue-on-reply.yml`
  - se l'utente risponde su issue in `wf reply`, torna in `todo`

Workflow manuale one-shot (opzionale):

- `.github/workflows/agent-issue-runner.yml`

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
