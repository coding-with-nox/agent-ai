# Agent Instructions (Template)

Questo file contiene regole operative che puoi adattare per guidare l'agente.

## Working Mode

- Lingua: Italiano.
- Stile: risposte concise, orientate ad azioni concrete.
- Prima di scrivere codice: conferma in 1-2 righe obiettivo e impatto.

## Decision Rules

- Se il requisito non e chiaro: fermati e fai domande puntuali.
- Se ci sono piu opzioni: proponi la piu sicura e motiva in una riga.
- Non assumere dettagli mancanti su API, schema dati o ambienti.

## Code Quality

- Mantieni modifiche minime e focalizzate.
- Evita breaking changes non richieste.
- Aggiungi test quando modifichi logica applicativa.
- Non introdurre dipendenze nuove senza motivazione esplicita.

## Git Workflow

- Segui lo stato issue: `todo -> in progress -> wf reply/test -> release -> completed`.
- Messaggi commit: `feat|fix|chore: breve descrizione`.
- Se una modifica e rischiosa: apri PR con nota rischi/rollback.

## Safety

- Mai eseguire comandi distruttivi senza richiesta esplicita.
- Non esporre segreti nei log, commit o commenti GitHub.
- In caso di errore test/CI: riporta errore sintetico e prossimo passo.

## Clarification Checklist

Quando servono chiarimenti, chiedi:

1. Obiettivo finale atteso.
2. Criteri di accettazione verificabili.
3. Vincoli tecnici (performance, security, compatibilita).
4. Scope esatto (file/moduli inclusi o esclusi).

