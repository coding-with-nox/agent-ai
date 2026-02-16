# NOcodeX - Guida al Deploy

## Prerequisiti

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installato
- Almeno un provider LLM configurato (Ollama, vLLM, Llama.cpp o compatibile OpenAI)
- Node.js (opzionale, richiesto per i server MCP via `npx`)

## Build

```bash
# Ripristino dipendenze e build della soluzione
dotnet restore NocodeX.sln
dotnet build NocodeX.sln -c Release
```

## Esecuzione dei test

```bash
dotnet test NocodeX.sln -c Release
```

## Pubblicazione

### Pubblicazione come applicazione standalone

```bash
dotnet publish src/NocodeX.Cli/NocodeX.Cli.csproj -c Release -o ./publish
```

L'eseguibile si troverÃ  nella cartella `./publish`.

### Installazione come tool .NET globale

```bash
# Pacchettizza il tool
dotnet pack src/NocodeX.Cli/NocodeX.Cli.csproj -c Release -o ./nupkg

# Installa il tool globalmente dal pacchetto locale
dotnet tool install --global --add-source ./nupkg NocodeX.Cli
```

Dopo l'installazione, il comando `nocodex` sara' disponibile nel terminale.

## Configurazione

1. Copia il file `nocodex.config.json` nella root del workspace dove intendi usare NOcodeX.

2. Configura almeno un provider LLM nella sezione `llm.providers`. Esempio minimo con Ollama locale:

```json
{
  "environment": "production",
  "agent_version": "2.0.0",
  "workspace_root": ".",
  "llm": {
    "primary_provider": "ollama-local",
    "providers": [
      {
        "provider_id": "ollama-local",
        "type": "ollama",
        "host": "localhost",
        "port": 11434,
        "model": "qwen2.5-coder:32b-instruct-q4_K_M",
        "default_temperature": 0.2,
        "default_max_tokens": 8192,
        "timeout_seconds": 300,
        "auto_pull": true
      }
    ],
    "fallback_chain": ["ollama-local"],
    "routing_rules": []
  },
  "mcp_servers": [],
  "acp_agents": [],
  "limits": {
    "max_tokens_per_task": 100000,
    "max_api_calls_per_task": 50,
    "max_execution_time_minutes": 15,
    "max_file_size_lines": 300,
    "max_concurrent_llm_requests": 1,
    "max_self_correction_attempts": 3
  }
}
```

## Configurazione dei provider LLM

### Ollama (locale)

1. Installa Ollama: https://ollama.com
2. Scarica un modello: `ollama pull qwen2.5-coder:32b-instruct-q4_K_M`
3. Ollama avvia automaticamente il server sulla porta 11434

### vLLM (server GPU remoto)

1. Installa vLLM sul server GPU
2. Avvia il server: `python -m vllm.entrypoints.openai.api_server --model deepseek-ai/DeepSeek-Coder-V2-Instruct`
3. Imposta la variabile d'ambiente `VLLM_API_KEY` se richiesta
4. Configura host e porta in `nocodex.config.json`

Esempio per VM nella stessa rete LAN:

```json
{
  "provider_id": "vllm-lan",
  "type": "vllm",
  "host": "192.168.1.100",
  "port": 8000,
  "base_path": "/v1",
  "model": "deepseek-ai/DeepSeek-Coder-V2-Instruct",
  "api_key_env": "VLLM_API_KEY"
}
```

In alternativa puoi usare `base_url` (utile con HTTPS/reverse proxy), ad esempio:

```json
{
  "provider_id": "vllm-lan-https",
  "type": "vllm",
  "base_url": "https://llm-vm.lan:9443/v1",
  "model": "deepseek-ai/DeepSeek-Coder-V2-Instruct",
  "api_key_env": "VLLM_API_KEY"
}
```

### Llama.cpp (sidecar locale)

1. Compila llama.cpp con supporto GPU
2. Avvia il server: `./server -m model.gguf -c 8192 --port 8080`
3. Configura la porta in `nocodex.config.json`

### Provider compatibile OpenAI

Qualsiasi server che espone un'API compatibile con OpenAI (es. TGI, LiteLLM) puo' essere configurato con il tipo `openai-compatible`.

## Configurazione MCP (opzionale)

I server MCP (Model Context Protocol) permettono a NOcodeX di interagire con filesystem, git e terminale:

```json
"mcp_servers": [
  {
    "id": "mcp-filesystem",
    "transport": "stdio",
    "endpoint": "npx -y @anthropic/mcp-filesystem /workspace",
    "enabled": true
  },
  {
    "id": "mcp-git",
    "transport": "stdio",
    "endpoint": "npx -y @anthropic/mcp-git",
    "enabled": true
  }
]
```

## Verifica dell'installazione

```bash
# Mostra il banner di stato
nocodex

# Verifica lo stack disponibile
nocodex stack presets

# Imposta uno stack
nocodex stack set dotnet-clean

# Controlla lo stato del provider LLM
nocodex llm status
```

## Struttura delle cartelle a runtime

NOcodeX crea una cartella `.nocodex` nel workspace corrente contenente:
- `nocodex.log` - Log giornaliero delle operazioni
- `audit.jsonl` - Registro di audit delle azioni eseguite

## Risoluzione dei problemi

| Problema | Soluzione |
|----------|-----------|
| Provider LLM non raggiungibile | Verificare che il server LLM sia in esecuzione e che host/porta siano corretti |
| Modello non trovato | Usare `nocodex llm pull <model>` o scaricare manualmente il modello |
| Errore "No stack configured" | Eseguire `nocodex stack set <preset>` prima dei comandi di generazione |
| Timeout durante la generazione | Aumentare `timeout_seconds` nel provider o `max_execution_time_minutes` nei limiti |

