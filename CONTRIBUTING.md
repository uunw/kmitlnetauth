# Contributing to KMITL NetAuth

Thanks for your interest in contributing! This document covers the development setup, project conventions, and how to submit changes.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.201+)
- Git
- An editor with C# support (VS Code + C# Dev Kit, JetBrains Rider, or Visual Studio)

## Getting Started

```bash
git clone git@github.com:uunw/kmitlnetauth.git
cd kmitlnetauth
dotnet restore
dotnet build
```

Verify everything works:

```bash
dotnet test
dotnet run --project src/KmitlNetAuth.Cli -- --help
dotnet run --project src/KmitlNetAuth.Cli -- status
```

## Project Structure

```
src/
  KmitlNetAuth.Core/       # Shared library (net10.0)
    AuthClient.cs           # HTTP login, heartbeat, internet check (configurable URLs)
    Config.cs               # TOML config (Tomlyn) + env var overrides
    ConfigPaths.cs          # Platform-aware path resolution
    Platform/               # Platform abstractions + implementations
      ICredentialStore.cs   # Interface
      DhcpDetector.cs       # DHCP detection and static IP warning
      CredentialJsonContext.cs # JSON source generators for trim safety
      Windows/              # DPAPI, Registry, balloon tips
      Linux/                # AES file, notify-send, XDG autostart
    Services/
      AuthService.cs        # Main run loop (state machine)
    DependencyInjection/
      CoreServiceCollectionExtensions.cs

  KmitlNetAuth.Cli/        # CLI + daemon (net10.0)
    Program.cs              # System.CommandLine v2.0.6 entry point
    Commands/               # setup, status, config subcommands
    SetupWizard.cs          # Spectre.Console interactive prompts
    AuthWorker.cs           # BackgroundService wrapper

  KmitlNetAuth.Tray/       # Windows 10+ GUI app (WPF + wpfui)
    App.xaml(.cs)           # WPF entry, host setup, tray icon
    MainWindow.xaml(.cs)    # Main window with sidebar navigation
    Pages/                  # Dashboard, Log, Settings, Debug, About pages
    LogBufferSink.cs        # In-app log buffer for live log viewer
    UpdateChecker.cs        # Auto-update: GitHub releases + MSI download

test/
  KmitlNetAuth.Core.Tests/ # 38 xunit tests (NSubstitute, coverlet)
  KmitlNetAuth.Cli.Tests/  # 8 xunit tests

packaging/                  # systemd, debian scripts, WiX v7 MSI
docs/                       # Installation and usage guides
scripts/                    # Helper scripts (kmitl-login.sh)
```

## Development Workflow

### Running the CLI

```bash
# Foreground mode
dotnet run --project src/KmitlNetAuth.Cli

# With arguments
dotnet run --project src/KmitlNetAuth.Cli -- -d          # daemon
dotnet run --project src/KmitlNetAuth.Cli -- setup       # wizard
dotnet run --project src/KmitlNetAuth.Cli -- status      # show config
dotnet run --project src/KmitlNetAuth.Cli -- config      # manage config
dotnet run --project src/KmitlNetAuth.Cli -- -c /tmp/test.toml  # custom config
```

### Running the Tray (Windows only)

```bash
dotnet run --project src/KmitlNetAuth.Tray
```

### Running Tests

```bash
# All tests (46 total)
dotnet test

# Specific project
dotnet test test/KmitlNetAuth.Core.Tests/
dotnet test test/KmitlNetAuth.Cli.Tests/
```

### Building a release binary

```bash
dotnet publish src/KmitlNetAuth.Cli/KmitlNetAuth.Cli.csproj \
  -c Release -r linux-x64 --self-contained /p:PublishSingleFile=true -o ./publish
```

## Conventions

### Code Style

- File-scoped namespaces (`namespace X;`)
- `var` everywhere when the type is obvious
- Nullable reference types enabled
- No warnings allowed (`TreatWarningsAsErrors`)
- Follow `.editorconfig` settings

### Naming

- Interfaces: `I` prefix (e.g., `ICredentialStore`)
- Platform implementations: `{Platform}{Interface}` (e.g., `DpapiCredentialStore`, `LinuxNotificationService`)
- Async methods: `Async` suffix (e.g., `LoginAsync`)

### Platform Code

- All platform-specific code lives in `Platform/Windows/` or `Platform/Linux/`
- Branching happens at DI registration time (`OperatingSystem.IsWindows()`)
- Use `[SupportedOSPlatform("windows")]` attribute on Windows-only classes
- Never use `#if` compile-time directives in Core

### Dependencies

- NuGet versions are managed centrally in `Directory.Packages.props`
- When adding a new package, add the version there, then reference without version in `.csproj`

### Auth Endpoints

Auth endpoints are configurable via the `[network]` section in `config.toml`. Defaults:

| Endpoint | URL |
|---|---|
| Login | `https://portal.kmitl.ac.th:19008/portalauth/login` |
| Heartbeat | `https://nani.csc.kmitl.ac.th/network-api/data/` |
| Internet Check | `http://detectportal.firefox.com/success.txt` |

## Submitting Changes

1. Fork the repository
2. Create a feature branch: `git checkout -b feat/my-feature`
3. Make your changes
4. Ensure `dotnet build` passes with 0 warnings
5. Ensure `dotnet test` passes (46 tests)
6. Commit with a descriptive message:
   - `feat:` new feature
   - `fix:` bug fix
   - `docs:` documentation
   - `refactor:` code restructuring
   - `ci:` CI/CD changes
7. Push and open a Pull Request

### PR Guidelines

- Keep PRs focused - one feature or fix per PR
- Update docs if you change user-facing behavior
- Add a description of what changed and why
- Add tests for new functionality
- Test on Linux if your change touches platform code (or note that it needs testing)

## Versioning

Date-based: `YYYYMMDD.N` (e.g., `20260416.46`)

- CI sets the version automatically from `date` + `run_number`
- Release tags: `v20260416.46`
- Local builds default to `0.0.0.1`

## Architecture Notes

### Auth Flow (run loop)

```
Start -> Check auto_login
  |
  false -> sleep 5s -> repeat
  true  -> Check internet (Firefox portal)
    |
    Online  -> Heartbeat -> OK? done : Login -> sleep interval
    Offline -> Login (up to max_attempt) -> backoff 60s -> reset -> sleep interval
```

### Credential Storage

- **Windows:** DPAPI (`ProtectedData`) - encrypted per Windows user, stored in `%APPDATA%`
- **Linux:** AES-CBC with PBKDF2 key derived from `/etc/machine-id`, stored with chmod 600
- **Docker:** Environment variables only (no credential store)

### Notification Flow

- **Windows Tray:** WPF wpfui GUI with 5 pages (Dashboard, Log, Settings, Debug, About) + tray icon (requires Windows 10+)
- **Windows CLI:** Logged only (no UI)
- **Linux:** `notify-send` via `Process.Start` (fails silently if not available)

## Questions?

Open an issue at https://github.com/uunw/kmitlnetauth/issues
