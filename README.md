# NOXVIS Agent v2.0

NOXVIS is a command-driven .NET 8 CLI agent scaffold organized with Clean Architecture and CQRS.

## Current implementation scope

This repository includes:

- Solution + layered project structure (`Noxvis.Cli`, `Noxvis.Core`, `Noxvis.Application`, `Noxvis.Infrastructure`).
- Stack system with seven built-in presets.
- `/stack set`, `/stack show`, `/stack presets`, `/stack validate` commands.
- `StackGuardBehavior` to block stack-required commands when no stack is active.
- Initial `/gen:endpoint` command wired through MediatR.
- `noxvis.config.json` template.

## Example usage

```bash
noxvis stack presets
noxvis stack set dotnet-clean
noxvis stack show
noxvis gen:endpoint POST /api/orders
```

If no stack is configured, stack-dependent generation commands fail with a guided message.

## Development notes

- Target framework: .NET 8
- CLI: `System.CommandLine`
- Mediator: `MediatR`
- Validation: `FluentValidation`
- Logging package reference: `Serilog`
