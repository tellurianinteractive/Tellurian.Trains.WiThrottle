# CLAUDE.md

## Project Overview

WiFred Server — a .NET 10 WiThrottle protocol server for wiFRED throttles to control model trains.
Combines a TCP server (WiThrottle protocol on port 12090) with a Blazor Server web dashboard.

## Build & Test

- Build: `dotnet build` from the repo root
- Test: `dotnet test` (88+ tests in Tellurian.Trains.WiFreds.Tests)
- Solution file: `Tellurian.Trains.WiFreds.slnx`

## Architecture

- **SDK**: `Microsoft.NET.Sdk.Web` (Blazor Server + background services in a single host)
- **Program.cs**: Top-level statements, `WebApplication.CreateBuilder`
- **Background services**: `WiFredTcpServer`, `MdnsAdvertiser`, `WiFredDiscoveryService`, `CommandStationInitializer`
- **Blazor components**: `Components/` folder (App.razor, Routes.razor, Layout/, Pages/)
- **Configuration records**: `Configuration/` folder, bound via `IOptions<T>` pattern
- **Protocol parsing**: `Protocol/` folder, discriminated union with `WiFredMessage` abstract record

## Key Conventions

- Settings classes are immutable records in `Configuration/`
- `WiFredDiscoveryService` is registered as both singleton and hosted service (so Blazor pages can inject it)
- Development mode uses `LoggingLocoController` instead of real command station adapters
- RESX-based localization in `Resources/` with two-letter language codes (en, sv, nb, da, de)
- Strongly-typed resource access via `Messages.Designer.cs` (auto-generated from `Messages.resx`)
- Dark theme UI consistent across all pages
- Release notes in `ReleaseNotes.md` at repo root

## Deployment

- Published as self-contained single-file executables for win-x64, linux-arm, linux-arm64
- Publish profiles in `Properties/PublishProfiles/`
- Releases via GitHub Releases
- Targets Raspberry Pi (headless or with screen) as primary deployment platform

## Related Projects

- YardController.Web (`Tellurian.Trains.YardController.App`) uses the same Blazor/localization patterns
