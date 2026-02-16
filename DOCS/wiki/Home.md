# NOcodeX Wiki Operativa (Step-by-step)

Questa wiki descrive in modo puntuale come usare il progetto NOcodeX allo stato attuale del repository.

## 1. Cosa fa il progetto (oggi)

NOcodeX e una CLI .NET 8 orientata a:

- gestione stack applicativo (`stack`)
- generazione endpoint (`gen:endpoint`)
- gestione provider LLM (`llm`)
- integrazione MCP (`mcp`) e ACP (`acp`)
- pianificazione comandi (`plan`, `approve`)

Comandi root registrati in CLI:

- `stack`
- `gen:endpoint`
- `llm`
- `mcp`
- `acp`
- `plan`
- `approve`

## 2. Prerequisiti

Prerequisiti minimi:

- .NET 8 SDK installato
- repository clonato in una cartella di lavoro
- file `nocodex.config.json` nella root del workspace
- almeno un provider LLM configurato in `nocodex.config.json`

Prerequisiti opzionali:

- Node.js (se usi MCP via `npx`)
- Docker Desktop (se usi `Dockerfile` e `docker-compose.yml`)

## 3. Setup iniziale del repository

1. Apri terminale nella root progetto.
2. Ripristina dipendenze:

```bash
dotnet restore NocodeX.sln
```

3. Compila la soluzione:

```bash
dotnet build NocodeX.sln
```

4. (Opzionale) Esegui i test:

```bash
dotnet test NocodeX.sln
```

## 4. Configurazione `nocodex.config.json`

Il file viene letto dalla root corrente all'avvio CLI:

- path atteso: `./nocodex.config.json`
- se non trovato, vengono usati default interni (non consigliato in produzione)

Sezioni principali:

- `environment`
- `agent_version`
- `workspace_root`
- `llm`
- `mcp_servers`
- `acp_agents`
- `limits`

### 4.1 Provider LLM in VM nella stessa rete (LAN)

Caso tipico: provider su una VM `192.168.1.100`, porta `8000`.

Esempio provider vLLM via `host` + `port`:

```json
{
  "provider_id": "vllm-lan",
  "type": "vllm",
  "host": "192.168.1.100",
  "port": 8000,
  "base_path": "/v1",
  "model": "deepseek-ai/DeepSeek-Coder-V2-Instruct",
  "api_key_env": "VLLM_API_KEY",
  "timeout_seconds": 300
}
```

Esempio provider con URL assoluto (HTTPS/reverse proxy):

```json
{
  "provider_id": "vllm-lan-https",
  "type": "vllm",
  "base_url": "https://llm-vm.lan:9443/v1",
  "model": "deepseek-ai/DeepSeek-Coder-V2-Instruct",
  "api_key_env": "VLLM_API_KEY",
  "timeout_seconds": 300
}
```

Checklist rete LAN:

1. La VM risponde all'IP dalla tua macchina host.
2. Firewall VM consente la porta del provider.
3. Il servizio LLM ascolta su `0.0.0.0` o su interfaccia LAN (non solo `127.0.0.1`).
4. `api_key_env` punta a una variabile ambiente realmente valorizzata, se richiesta.

### 4.2 Variabili ambiente API key

Esempio PowerShell:

```powershell
$env:VLLM_API_KEY="your-secret-key"
```

Se usi Docker Compose, puoi valorizzare variabili in `.env` partendo da `.env.example`.

## 5. Avvio CLI

Modalita consigliata in sviluppo:

```bash
dotnet run --project src/NocodeX.Cli -- <comando>
```

Esempi:

```bash
dotnet run --project src/NocodeX.Cli --
dotnet run --project src/NocodeX.Cli -- stack presets
dotnet run --project src/NocodeX.Cli -- llm status
```

Note:

- Senza argomenti mostra il banner stato.
- Log file scritto in `./.nocodex/nocodex.log`.
- Audit scritto in `./.nocodex/audit.jsonl`.

## 6. Workflow consigliato (ordine operativo)

1. Verifica provider LLM:
   - `llm providers`
   - `llm health`
2. Scegli stack:
   - `stack presets`
   - `stack set <preset>`
   - `stack validate`
3. Lancia generazione:
   - `gen:endpoint <METHOD> <ROUTE>`
4. Se task complesso:
   - `plan "<catena comandi>"`
   - `approve`

## 7. Comandi stack

### 7.1 `stack presets`

Lista preset built-in disponibili.

Preset attuali:

- `dotnet-clean`
- `fastapi-hex`
- `go-micro`
- `laravel-modular`
- `nextjs-fullstack`
- `rust-axum`
- `spring-ddd`

### 7.2 `stack set <preset>`

Imposta lo stack attivo in memoria runtime CLI.

Esempio:

```bash
dotnet run --project src/NocodeX.Cli -- stack set dotnet-clean
```

### 7.3 `stack show`

Mostra JSON dello stack attivo.

### 7.4 `stack validate`

Valida lo stack corrente e stampa eventuali errori.

## 8. Comando di generazione

### 8.1 `gen:endpoint <method> <route>`

Genera scaffold endpoint tramite pipeline applicativa.

Esempi:

```bash
dotnet run --project src/NocodeX.Cli -- gen:endpoint POST /api/orders
dotnet run --project src/NocodeX.Cli -- gen:endpoint GET /api/orders/{id}
```

Se nessuno stack e impostato, la generazione viene bloccata da guard behavior.

## 9. Comandi LLM

### 9.1 `llm providers`

Lista provider registrati da config.

### 9.2 `llm status`

Stato completo provider (reachability, modello, metriche dove disponibili).

### 9.3 `llm health`

Health check sintetico su tutti i provider.

### 9.4 `llm models [provider]`

Mostra metadata modello per provider scelto (o primario).

### 9.5 `llm benchmark [provider]`

Esegue benchmark base e stampa throughput e timing.

### 9.6 `llm pull <model> [provider]`

Tenta pull/download modello (utile soprattutto per Ollama).

### 9.7 `llm set-primary <provider_id>`

Imposta provider primario in runtime.

## 10. Comandi MCP

### 10.1 `mcp servers`

Lista server MCP configurati.

### 10.2 `mcp status`

Health check server MCP.

### 10.3 `mcp tools <server_id>`

Lista tool esposti dal server MCP indicato.

## 11. Comandi ACP

### 11.1 `acp agents`

Lista agenti ACP configurati.

### 11.2 `acp status`

Health check agenti ACP.

## 12. Pianificazione (`plan` / `approve`)

### 12.1 Sintassi catena comandi

Parser supporta:

- `&&` per segmenti sequenziali
- `&` per segmenti paralleli nello stesso blocco

Esempio:

```bash
dotnet run --project src/NocodeX.Cli -- plan "gen:endpoint POST /api/orders && gen:endpoint GET /api/orders"
```

Poi:

```bash
dotnet run --project src/NocodeX.Cli -- approve
```

Importante:

- senza `plan` precedente, `approve` fallisce con messaggio guidato
- usa sempre virgolette sulla catena per evitare interpretazione shell di `&`

## 13. Uso con Docker

File presenti:

- `Dockerfile`
- `docker-compose.yml`
- `.env.example`

### 13.1 Build immagine

```bash
docker build -t agent-ai:local .
```

### 13.2 Esecuzione con Compose

1. Crea `.env` da `.env.example` se ti servono API key.
2. Esegui comando CLI nel container:

```bash
docker compose run --rm nocodex stack presets
docker compose run --rm nocodex llm status
docker compose run --rm nocodex gen:endpoint POST /api/orders
```

Note Compose:

- workspace locale montato in `/workspace`
- `working_dir` impostata su `/workspace`
- accesso a host via `host.docker.internal`

## 14. Troubleshooting rapido

Problema: `No LLM provider is healthy`.

Controlli:

1. `llm providers` mostra il provider atteso.
2. `llm health` fallisce o meno.
3. host/porta nel config sono raggiungibili.
4. API key presente se richiesta.
5. modello caricato lato server LLM.

Problema: generazione bloccata con stack non configurato.

Controlli:

1. `stack set <preset>`
2. `stack validate`
3. rilancia `gen:endpoint ...`

Problema: in Docker non vede config.

Controlli:

1. `nocodex.config.json` presente nella root montata.
2. comando lanciato con `docker compose run --rm nocodex ...`
3. volume bind attivo (workspace montato in `/workspace`).
