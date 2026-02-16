# NOcodeX - Descrizione del Programma

## Cos'e' NOcodeX

NOcodeX e' un agente CLI autonomo per la generazione di codice production-ready, progettato per funzionare con Large Language Model (LLM) self-hosted. E' costruito con .NET 10 e segue i principi della Clean Architecture e il pattern CQRS (Command Query Responsibility Segregation).

L'obiettivo di NOcodeX e' permettere la generazione automatica di codice su diversi stack tecnologici, senza dipendere da servizi cloud esterni per l'inferenza AI.

## Architettura

Il progetto segue una struttura a livelli (Clean Architecture):

```
NocodeX.Cli             â†’ Livello presentazione (CLI con System.CommandLine)
NocodeX.Application     â†’ Livello applicazione (use case, CQRS con MediatR)
NocodeX.Core            â†’ Livello dominio (interfacce, modelli, enum, eccezioni)
NocodeX.Infrastructure  â†’ Livello infrastruttura (implementazioni concrete)
```

### NocodeX.Core (Dominio)

Contiene le astrazioni fondamentali del sistema:
- **Interfacce**: `ILlmProvider`, `IStackRegistry`, `IMcpClient`, `IAcpClient`, `IPlanEngine`, etc.
- **Modelli**: configurazioni LLM, piani di esecuzione, risultati, prompt
- **Enum**: tipi di provider, livelli di trust, categorie di errore
- **Eccezioni**: errori specifici del dominio (LLM non disponibile, context window superata, etc.)

### NocodeX.Application (Casi d'uso)

Implementa i comandi e le query del sistema tramite il pattern Mediator:
- **Generazione**: `GenEndpointCommand` per generare endpoint
- **Stack**: comandi per impostare e visualizzare lo stack tecnologico attivo
- **LLM**: query di stato, benchmark, pull dei modelli
- **Behavior**: guardie automatiche (verifica stack configurato, salute LLM)
- **Validatori**: validazione delle configurazioni con FluentValidation

### NocodeX.Infrastructure (Implementazioni)

Contiene le implementazioni concrete di tutti i servizi:
- **Provider LLM**: Ollama, vLLM, Llama.cpp, OpenAI-compatible
- **Generazione codice**: parsing XML dei blocchi di codice, mapping su file
- **Prompt**: sistema di template con placeholder per stack e contesto
- **Planning**: motore DAG per l'esecuzione di piani multi-step
- **Self-correction**: recupero automatico dagli errori di compilazione
- **MCP/ACP**: integrazione con protocolli per filesystem, git, terminale e multi-agente
- **Monitoraggio**: health check LLM, metriche GPU, audit log

### NocodeX.Cli (Presentazione)

Il punto di ingresso dell'applicazione:
- **Comandi**: `stack`, `gen`, `llm`, `mcp`, `acp`, `plan`
- **Rendering**: banner di stato, rendering streaming del codice, visualizzazione piani

## Funzionalita' principali

### 1. Generazione codice multi-stack

NOcodeX supporta 7 preset tecnologici integrati:

| Preset | Linguaggio | Framework |
|--------|-----------|-----------|
| `dotnet-clean` | C# | .NET 10 con Clean Architecture |
| `nextjs-fullstack` | TypeScript | Next.js |
| `fastapi-hex` | Python | FastAPI con architettura esagonale |
| `go-micro` | Go | Microservizi Go |
| `laravel-modular` | PHP | Laravel modulare |
| `spring-ddd` | Java | Spring Boot con DDD |
| `rust-axum` | Rust | Axum |

Ogni preset definisce convenzioni, regole e template di generazione specifici per lo stack.

### 2. Integrazione LLM self-hosted

NOcodeX si collega a modelli LLM eseguiti localmente o su server dedicati:
- **Ollama**: per l'esecuzione locale di modelli quantizzati
- **vLLM**: per server GPU ad alte prestazioni
- **Llama.cpp**: per inferenza ottimizzata su CPU/GPU
- **OpenAI-compatible**: qualsiasi server con API compatibile

Il sistema supporta:
- Routing intelligente basato sulla complessita' del task
- Catena di fallback tra provider
- Monitoraggio della salute dei provider e metriche GPU
- Gestione automatica della finestra di contesto

### 3. Motore di pianificazione

Per task complessi, NOcodeX utilizza un motore di pianificazione basato su DAG (Directed Acyclic Graph):
- Scomposizione automatica dei task in step
- Esecuzione parallela dove possibile
- Approvazione del piano prima dell'esecuzione

### 4. Self-correction

Il motore di auto-correzione rileva errori nel codice generato e tenta automaticamente di correggerli, fino a un numero massimo di tentativi configurabile.

### 5. Integrazione MCP e ACP

- **MCP (Model Context Protocol)**: permette l'accesso a filesystem, git e terminale tramite server esterni
- **ACP (Agent Control Protocol)**: abilita l'orchestrazione multi-agente per review e testing automatici

### 6. Audit e logging

Tutte le operazioni vengono registrate:
- Log giornaliero via Serilog
- Registro di audit in formato JSONL per tracciabilita'

## Comandi principali

```bash
# Gestione stack
nocodex stack presets          # Lista i preset disponibili
nocodex stack set <preset>     # Imposta lo stack attivo
nocodex stack show             # Mostra lo stack corrente
nocodex stack validate         # Valida la configurazione

# Generazione codice
nocodex gen:endpoint <method> <path>   # Genera un endpoint

# Gestione LLM
nocodex llm status             # Stato dei provider LLM
nocodex llm benchmark          # Benchmark del provider
nocodex llm pull <model>       # Scarica un modello

# MCP e ACP
nocodex mcp list               # Lista i server MCP
nocodex acp list               # Lista gli agenti ACP

# Pipeline di esecuzione
nocodex plan <descrizione>     # Crea un piano di esecuzione
nocodex approve <plan-id>      # Approva ed esegue un piano
```

## Tecnologie utilizzate

| Componente | Tecnologia |
|-----------|-----------|
| Runtime | .NET 10 |
| CLI framework | System.CommandLine |
| Mediator/CQRS | MediatR |
| Validazione | FluentValidation |
| Logging | Serilog |
| DI container | Microsoft.Extensions.DependencyInjection |
| UI console | Spectre.Console |

