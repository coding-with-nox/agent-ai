# NOcodeX Agent v2.0

NOcodeX is a command-driven .NET 10 CLI agent scaffold organized with Clean Architecture and CQRS.

## Documentation

- Detailed usage wiki (step-by-step): `https://github.com/coding-with-nox/agent-ai/wiki`
- Deployment notes: `DOCS/deploy.md`
- GitHub issue automation: `DOCS/github-agent.md`
- Project overview: `DOCS/description.md`
- Wiki source files (auto-sync): `DOCS/wiki`

## Wiki Sync Automation

The repository includes `.github/workflows/sync-wiki.yml` to publish `DOCS/wiki/*` to the GitHub Wiki.

Required setup:

1. Create a Personal Access Token with write access to this repository.
2. Save it in GitHub Actions secrets as `WIKI_PUSH_TOKEN`.
3. Push changes under `DOCS/wiki/` to trigger sync automatically.

## Current implementation scope

This repository includes:

- Solution + layered project structure (`NocodeX.Cli`, `NocodeX.Core`, `NocodeX.Application`, `NocodeX.Infrastructure`).
- Stack system with seven built-in presets.
- `/stack set`, `/stack show`, `/stack presets`, `/stack validate` commands.
- `StackGuardBehavior` to block stack-required commands when no stack is active.
- Initial `/gen:endpoint` command wired through MediatR.
- `nocodex.config.json` template.

## Example usage

```bash
nocodex stack presets
nocodex stack set dotnet-clean
nocodex stack show
nocodex gen:endpoint POST /api/orders
```

If no stack is configured, stack-dependent generation commands fail with a guided message.

## Development notes

- Target framework: .NET 10
- CLI: `System.CommandLine`
- Mediator: `MediatR`
- Validation: `FluentValidation`
- Logging package reference: `Serilog`


## Web configurator

È disponibile una UI web leggera per generare/modificare `nocodex.config.json` senza fermare l'agente.

```bash
docker compose up --build nocodex-config-web
```

Apri `http://localhost:8088`.

- Se il file config manca o è JSON malformato, l'agente logga l'errore e continua con valori di default (no crash).
- La UI salva il file in `/workspace/nocodex.config.json` (montato dal volume locale).

