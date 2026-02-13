# CODEX Agent v2.0

CODEX is a command-driven .NET 8 CLI agent scaffold organized with Clean Architecture and CQRS.

## Current implementation scope

This repository includes:

- Solution + layered project structure (`Codex.Cli`, `Codex.Core`, `Codex.Application`, `Codex.Infrastructure`).
- Stack system with seven built-in presets.
- `/stack set`, `/stack show`, `/stack presets`, `/stack validate` commands.
- `StackGuardBehavior` to block stack-required commands when no stack is active.
- Initial `/gen:endpoint` command wired through MediatR.
- `codex.config.json` template.

## Example usage

```bash
codex stack presets
codex stack set dotnet-clean
codex stack show
codex gen:endpoint POST /api/orders
```

If no stack is configured, stack-dependent generation commands fail with a guided message.

## Development notes

- Target framework: .NET 8
- CLI: `System.CommandLine`
- Mediator: `MediatR`
- Validation: `FluentValidation`
- Logging package reference: `Serilog`
