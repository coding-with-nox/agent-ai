# GitHub Issue Automation

Questa pagina descrive il flusso operativo automatico issue -> test -> release -> completed.

## 1. Label usate

Stati issue:

- `todo`
- `in progress`
- `wf reply`
- `test`
- `release`
- `completed`

Priorita:

- `p0` (massima)
- `p1`
- `p2`
- `p3`

## 2. Ordine di esecuzione

La queue prende una sola issue alla volta, con ordinamento:

1. priorita (`p0` -> `p3`)
2. data di creazione (piu vecchia prima)

Script queue:

- `scripts/process-priority-queue.ps1`

## 3. State machine

1. `todo -> in progress`
2. Se ambiguo/incompleto:
   - commento con domande
   - `in progress -> wf reply`
3. Se implementazione ok:
   - push branch `issue/<numero>-slug`
   - `in progress -> test`
4. Test pipeline:
   - pass: `test -> release` + creazione PR
   - fail: `test -> todo` + commento con errore
5. Merge PR:
   - `release -> completed`

## 4. Workflow GitHub Actions

- `.github/workflows/agent-priority-queue.yml`
  - scheduler + manuale
  - prende la prossima issue `todo`

- `.github/workflows/agent-test-and-release.yml`
  - trigger su push branch `issue/**`
  - esegue `dotnet test`
  - aggiorna stato issue e PR

- `.github/workflows/agent-complete-on-merge.yml`
  - trigger su PR merge
  - porta issue a `completed`

- `.github/workflows/agent-requeue-on-reply.yml`
  - se utente commenta su issue in `wf reply`
  - riporta issue in `todo`

- `.github/workflows/agent-issue-runner.yml`
  - runner manuale one-shot su issue specifica

## 5. Script coinvolti

- `scripts/process-github-issue.ps1`
- `scripts/process-priority-queue.ps1`
- `scripts/test-and-release.ps1`
- `scripts/complete-on-merge.ps1`
- `scripts/requeue-on-reply.ps1`

## 6. Template issue

Template task agent:

- `.github/issue-template/agent-task.yml`

Nota: GitHub riconosce in modo nativo `.github/ISSUE_TEMPLATE`.
Se mantieni `.github/issue-template`, verifica che nel tuo setup il template venga caricato come previsto.

## 7. Esecuzione manuale rapida

Processare una issue specifica:

```powershell
./scripts/process-github-issue.ps1 -Repo owner/repo -IssueNumber 123 -CommentOnIssue
```

Processare la queue prioritaria:

```powershell
./scripts/process-priority-queue.ps1 -Repo owner/repo -CommentOnIssue
```
