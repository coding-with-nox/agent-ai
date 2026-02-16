# NOcodeX Agent v2.0

NOcodeX is a command-driven .NET 8 CLI agent scaffold organized with Clean Architecture and CQRS.

## Documentation

- Detailed usage wiki (step-by-step): `docs/wiki.md`
- Deployment notes: `docs/deploy.md`
- Project overview: `docs/description.md`

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

- Target framework: .NET 8
- CLI: `System.CommandLine`
- Mediator: `MediatR`
- Validation: `FluentValidation`
- Logging package reference: `Serilog`
