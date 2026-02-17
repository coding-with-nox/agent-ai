# Logging e Monitoring

Questa pagina descrive il sistema di logging strutturato e lo stack di osservabilita (Grafana + Loki + Promtail) integrato nel progetto.

## 1. Logging strutturato

La CLI produce due file di log in parallelo nella directory `.nocodex/`:

| File | Formato | Scopo |
|---|---|---|
| `nocodex.log` | Testo (human-readable) | Debug manuale da terminale |
| `nocodex.json.log` | JSON compatto (Serilog Compact) | Ingestione automatica (Loki/Promtail) |

Ogni riga JSON contiene:

```json
{
  "@t": "2026-02-17T10:30:00.000Z",
  "@m": "Provider vllm-lan is healthy",
  "@l": "Information",
  "SourceContext": "NocodeX.Infrastructure.Llm.LlmClientManager",
  "Application": "NocodeX.Cli"
}
```

Campi standard Serilog Compact:

- `@t` — timestamp ISO 8601
- `@m` — messaggio renderizzato
- `@mt` — message template
- `@l` — livello (Information, Warning, Error, Fatal)
- `@x` — eccezione (se presente)

Proprietà di contesto aggiunte automaticamente:

- `Application` — sempre `NocodeX.Cli`
- `SourceContext` — classe che ha emesso il log

### Rotazione

Entrambi i file ruotano giornalmente (rolling daily). I vecchi file vengono mantenuti localmente — la retention e gestita da Loki per i log ingeriti.

## 2. Stack di monitoring

Lo stack di osservabilita e definito in `docker-compose.monitoring.yml` e comprende tre servizi:

| Servizio | Porta | Ruolo |
|---|---|---|
| **Loki** | 3100 | Aggregazione e query log |
| **Promtail** | 9080 | Agent di scraping — legge i file `.nocodex/*.json.log` |
| **Grafana** | 3000 | Dashboard, esplorazione log, alerting |

### 2.1 Avvio rapido

```bash
# Avvia lo stack di monitoring
docker compose -f docker-compose.monitoring.yml up -d

# Verifica che i servizi siano healthy
docker compose -f docker-compose.monitoring.yml ps
```

Grafana e accessibile su `http://localhost:3000` con credenziali di default:

- **User**: `admin`
- **Password**: `admin` (oppure il valore di `GF_ADMIN_PASSWORD` nel `.env`)

### 2.2 Avvio combinato (CLI + monitoring)

```bash
docker compose -f docker-compose.yml -f docker-compose.monitoring.yml up -d
```

### 2.3 Stop

```bash
docker compose -f docker-compose.monitoring.yml down
```

Per rimuovere anche i volumi dati (reset completo):

```bash
docker compose -f docker-compose.monitoring.yml down -v
```

## 3. Dashboard pre-configurata

Al primo avvio Grafana carica automaticamente la dashboard **"NocodeX — Logs & Errors"** nella cartella `NocodeX`.

Pannelli disponibili:

| Pannello | Tipo | Descrizione |
|---|---|---|
| **Log Volume by Level** | Time series (barre) | Volume log suddiviso per livello |
| **Error & Fatal Logs** | Logs | Stream filtrato su Error e Fatal |
| **All Logs (filtered)** | Logs | Stream completo con filtro variabile |
| **Errors by SourceContext** | Pie chart | Distribuzione errori per classe sorgente |
| **Error Rate** | Stat | Errori/minuto con soglie colorate |

### Variabili di filtro

- **Log Level** — dropdown multi-selezione (All, Error, Fatal, Warning, Information, Debug)
- **Search** — ricerca full-text libera

## 4. Alerting

Due regole di alert sono pre-configurate:

### 4.1 Critical Error Rate

- **Condizione**: piu di 5 log Error/Fatal in 5 minuti
- **Pending**: 2 minuti (evita falsi positivi per spike transitori)
- **Severita**: `critical`

### 4.2 Fatal Log Detected

- **Condizione**: almeno 1 log Fatal in 1 minuto
- **Pending**: immediato (0s)
- **Severita**: `critical`

### Configurazione notifiche

Il contact point di default e `default-email` con destinatario `ops@nocodex.local`.
Per modificarlo:

1. Accedi a Grafana > Alerting > Contact points
2. Modifica `default-email` con l'indirizzo reale
3. Oppure aggiungi un nuovo contact point (Slack, PagerDuty, Webhook, etc.)

## 5. Struttura file

```
monitoring/
  loki/
    loki-config.yaml         # Configurazione server Loki
  promtail/
    promtail-config.yaml     # Configurazione agent Promtail
  grafana/
    provisioning/
      datasources/
        loki.yaml            # Datasource Loki auto-provisioned
      dashboards/
        dashboards.yaml      # Provider dashboard da filesystem
      alerting/
        alerts.yaml          # Regole alert + contact points
    dashboards/
      nocodex-logs.json      # Dashboard JSON
docker-compose.monitoring.yml # Compose per lo stack
```

## 6. Troubleshooting

**Promtail non ingerisce log**

1. Verifica che `.nocodex/` contenga file `*.json.log`
2. Controlla i log Promtail: `docker compose -f docker-compose.monitoring.yml logs promtail`
3. Verifica che il volume mount sia corretto

**Grafana non mostra dati**

1. In Explore, seleziona datasource Loki
2. Query: `{job="nocodex"}`
3. Se vuoto, verificare Promtail (punto precedente)

**Alert non funzionano**

1. Grafana > Alerting > Alert rules — verifica stato regole
2. Grafana > Alerting > Contact points — verifica configurazione destinatario
3. Controlla che `GF_UNIFIED_ALERTING_ENABLED=true` sia attivo (default nel compose)
