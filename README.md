# Hakonexa - WSL Containers Manager

[日本語版 README](README.ja.md)

> Note: Linked repository Markdown documents are currently mostly written in Japanese.

Hakonexa - WSL Containers Manager is a WinUI / .NET desktop application for managing
containers on WSL (Windows Subsystem for Linux). It targets the new **WSL Containers**
platform (`wslc` CLI / WSL Container API, Public Preview) and aims to provide a native
Windows experience for day-to-day container, image, volume, network, log, shell, and WSL
resource management.

## Screenshot

![Hakonexa dashboard screenshot](docs/assets/screenshots/hakonexa-dashboard.png)

## Current status

The application is no longer just a scaffold. The solution contains the clean architecture
projects and implements the main WinUI shell plus management screens for:

- dashboard summary and running container resource usage
- container list, start, stop, restart, delete, details, logs, and interactive exec shell
- image list, pull, run, and remove
- volume list, create, and remove
- network list, create, and remove
- settings for WSL integration status and WSL resource limits

The managed platform, WSL Containers, is still in Public Preview, so runtime behavior and
APIs may change. See [`docs/reference/wsl-containers-platform.md`](docs/reference/wsl-containers-platform.md)
for the current platform notes and primary references.

## Architecture

The repository follows a clean architecture-oriented four-layer structure:

| Layer | Responsibility |
|---|---|
| Domain | Entities, value objects, and domain rules |
| Application | Use cases and interfaces for external dependencies |
| Infrastructure | `wslc` CLI integration and concrete system I/O |
| Presentation | WinUI views, view models, navigation, localization, and DI composition |

See [`docs/design/architecture-overview.md`](docs/design/architecture-overview.md) for the
current architecture snapshot and [ADR-0005](docs/adr/0005-adopt-clean-architecture-layering.md)
for the layering decision.

## Documentation

| Directory | Contents |
|---|---|
| [`docs/specs/`](docs/specs/README.md) | Feature specifications: what the app should do |
| [`docs/design/`](docs/design/README.md) | Current design snapshots |
| [`docs/adr/`](docs/adr/README.md) | Architecture Decision Records |
| [`docs/reference/`](docs/reference/README.md) | External platform references, including WSL Containers |

## Development with AI coding agents

This repository is designed to be developed with GitHub Copilot CLI and other AI coding
agents. Agent operating rules, including the required feature workflow, TDD policy, ADR
handling, and model routing, are documented in [`AGENTS.md`](AGENTS.md).

## Copilot CLI plugin setup

The repository workflow expects the following Copilot CLI plugins/marketplaces. These are
user-level Copilot CLI settings, so they are not restored automatically by cloning the repo.

```powershell
copilot plugin marketplace add dotnet/skills
copilot plugin install dotnet@dotnet-agent-skills
copilot plugin install dotnet-test@dotnet-agent-skills
copilot plugin install dotnet-msbuild@dotnet-agent-skills
copilot plugin install dotnet-nuget@dotnet-agent-skills
copilot plugin install microsoftdocs/mcp
```

Run the `copilot plugin install` commands one at a time. Running several installs in
parallel can corrupt the marketplace clone used by Copilot CLI.

The `winui` and `dotnet` plugins from the awesome-copilot marketplace are expected to be
installed separately in each development environment.
