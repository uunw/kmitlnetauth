# CLAUDE.md - Development Guide for AI Assistants

## Project Overview

KMITL NetAuth is a cross-platform auto-authentication service for the KMITL university network. It's a .NET 10 solution with 3 projects + 2 test projects.

## Quick Reference

```bash
# Build
dotnet build

# Run tests (46 total: 38 Core + 8 CLI)
dotnet test

# Run CLI
dotnet run --project src/KmitlNetAuth.Cli -- --help
dotnet run --project src/KmitlNetAuth.Cli -- status
dotnet run --project src/KmitlNetAuth.Cli -- setup
dotnet run --project src/KmitlNetAuth.Cli -- config
dotnet run --project src/KmitlNetAuth.Cli -- -d        # daemon mode

# Run Tray (Windows only, use EnableWindowsTargeting on other OS)
dotnet run --project src/KmitlNetAuth.Tray
```

## Solution Structure

| Project | Target | Purpose |
|---|---|---|
| `KmitlNetAuth.Core` | `net10.0` | Shared library: auth client, config, platform abstractions, DI |
| `KmitlNetAuth.Cli` | `net10.0` | CLI app with System.CommandLine, Serilog, daemon/systemd/Windows Service |
| `KmitlNetAuth.Tray` | `net10.0-windows10.0.17763.0` | WPF + wpfui GUI app (Windows 10+ only) |
| `KmitlNetAuth.Core.Tests` | xunit | 38 tests covering core functionality |
| `KmitlNetAuth.Cli.Tests` | xunit | 8 tests covering CLI commands |

## Key Files

| File | What it does |
|---|---|
| `src/KmitlNetAuth.Core/AuthClient.cs` | HTTP requests: login, heartbeat, internet check (configurable URLs) |
| `src/KmitlNetAuth.Core/Config.cs` | TOML config loading (Tomlyn), env var overrides, password migration |
| `src/KmitlNetAuth.Core/Services/AuthService.cs` | Main run loop (state machine) |
| `src/KmitlNetAuth.Core/DependencyInjection/CoreServiceCollectionExtensions.cs` | DI wiring, platform detection |
| `src/KmitlNetAuth.Core/Platform/DhcpDetector.cs` | DHCP detection and static IP warning |
| `src/KmitlNetAuth.Core/Platform/CredentialJsonContext.cs` | JSON source generators for trim safety |
| `src/KmitlNetAuth.Cli/Program.cs` | CLI entry point (System.CommandLine v2.0.6) |
| `src/KmitlNetAuth.Tray/App.xaml.cs` | WPF app entry, host setup, tray icon |
| `src/KmitlNetAuth.Tray/MainWindow.xaml(.cs)` | Main window with sidebar navigation |
| `src/KmitlNetAuth.Tray/Pages/` | Dashboard, Log, Settings, Debug, About pages |
| `src/KmitlNetAuth.Tray/LogBufferSink.cs` | In-app log buffer for live log viewer |
| `src/KmitlNetAuth.Tray/UpdateChecker.cs` | Auto-update: check GitHub releases, download MSI, install |

## Important Conventions

### System.CommandLine v2.0.6 (Stable)

This project uses the **stable v2.0.6 API**:
- `SetAction` (not `SetHandler`)
- `Subcommands.Add` (not `AddCommand`)
- `Options.Add` (not `AddOption`)
- Option constructor: `new Option<T>("--name", "-alias")` then set `Description` separately
- Action receives `(ParseResult, CancellationToken)`, not `InvocationContext`
- Invoke: `command.Parse(args).InvokeAsync()` not `command.InvokeAsync(args)`

### Config (TOML)

- **TOML** format via **Tomlyn v2.3.0** (not YAML)
- Config file: `config.toml` (not config.yaml)
- Grouped sections: `[auth]`, `[network]`, `[service]`, `[logging]`, `[notifications]`, `[update]`, `[tray]`
- Backward compatibility: auto-migrates legacy `config.yaml` to `config.toml`
- Env vars override file values (KMITL_USERNAME, KMITL_PASSWORD, etc.)
- Password auto-migrates to credential store on load
- `Config.Save()` strips password from TOML if credential store succeeds

### Platform Abstraction

- All platform-specific code is in `Core/Platform/Windows/` and `Core/Platform/Linux/`
- Branching is done at DI registration time with `OperatingSystem.IsWindows()`
- **Never** use `#if` compile directives in Core
- Use `[SupportedOSPlatform("windows")]` attribute on Windows-only classes
- Windows methods that load Windows types must be in separate methods (to avoid loading Windows assemblies on Linux)

### NuGet Package Management

- Central Package Management via `Directory.Packages.props`
- `.csproj` files reference packages **without** version
- All version changes go in `Directory.Packages.props`

### Auth Endpoints (configurable via TOML)

Default endpoints (configurable in `[network]` section of config.toml):

```
Login:     POST https://portal.kmitl.ac.th:19008/portalauth/login
Heartbeat: POST https://nani.csc.kmitl.ac.th/network-api/data/
Internet:  GET  http://detectportal.firefox.com/success.txt
```

Login form fields: `userName`, `userPass`, `uaddress`, `umac`, `agreed=1`, `acip=10.252.13.10`, `authType=1`
Heartbeat form fields: `username`, `os=Chrome v116.0.5845.141 on Windows 10 64-bit`, `speed=1.29`, `newauth=1`

### Cookie / SSL

- `HttpClient` must use a shared `CookieContainer` (cookies persist across login + heartbeat)
- SSL: `DangerousAcceptAnyServerCertificateValidator` (the KMITL portal uses self-signed certs)

### Credential Storage

- **Windows:** DPAPI (`ProtectedData`) with "kmitlnetauth" as entropy
- **Linux:** AES-CBC, key from PBKDF2(machine-id, "kmitlnetauth"), file at `~/.config/kmitlnetauth/.credentials` with chmod 600
- **Docker:** Env vars only, no credential store
- JSON source generators (`CredentialJsonContext`) used for trim safety

### Versioning

- Date-based: `YYYYMMDD.N` (e.g., `20260416.46`)
- CI passes `/p:Version=` to dotnet publish
- Local builds use `0.0.0.1` from `Directory.Build.props`

## Testing

```bash
# Run all tests
dotnet test

# Run specific project
dotnet test test/KmitlNetAuth.Core.Tests/
dotnet test test/KmitlNetAuth.Cli.Tests/
```

- **Core.Tests:** 38 tests (xunit + NSubstitute + coverlet)
- **Cli.Tests:** 8 tests (xunit)
- Tests run in CI (amd64 only)

## Build Requirements

- .NET 10 SDK (pinned in `global.json` to 10.0.201)
- `EnableWindowsTargeting=true` in `Directory.Build.props` for cross-compiling Tray on non-Windows
- `TreatWarningsAsErrors=true` - builds must have 0 warnings

## CI/CD

- `.github/workflows/build.yml` runs pipeline: version -> build-linux (x64+arm64) -> build-windows -> build-docker -> release
- Auto-tag + auto-release on every push to main
- Date-based version: `YYYYMMDD.N` (e.g., `20260416.46`)
- NuGet cache via setup-dotnet
- Tests run in CI (amd64 only)
- Dependabot: NuGet (weekly), GitHub Actions (weekly), Docker (weekly)

### Deployment Strategy (Mixed)

| Format | Type | Size | Notes |
|---|---|---|---|
| Linux standalone binary | Self-contained, trimmed, compressed, single-file | ~14 MB | No runtime needed |
| `.deb` / `.rpm` | Framework-dependent | ~1-2 MB | Requires `dotnet-runtime-10.0` |
| Windows `.msi` | Framework-dependent | ~8 MB | WiX v7 GA, OSMF EULA, requires .NET 10 Runtime |
| Docker | Self-contained, multi-platform (amd64+arm64) | ~14 MB | Alpine-based |

- MSI version: `YY.M.{run_number}` (e.g., `26.4.46`)
- MSI uses `MajorUpgrade` with `AllowSameVersionUpgrades`
- Installs to `C:\Program Files\KMITL NetAuth\`
- Auto-launches after install/upgrade
- WiX v7 requires `wix eula accept wix7` before build

### Linux Packages

- `.deb` / `.rpm` built with `dotnet-deb` and `dotnet-rpm` CLI tools
- Framework-dependent (requires `dotnet-runtime-10.0`)
- Installs to `/usr/lib/kmitlnetauth/` with wrapper script at `/usr/bin/kmitlnetauth`

## Windows Tray App

Full GUI application with sidebar navigation (not just a tray icon):

- **5 pages:** Dashboard, Log, Settings, Debug, About
- **Dashboard:** Status indicator, username, IP, uptime, Login Now/Pause buttons
- **Log:** In-app live log viewer with level filter (uses `LogBufferSink`)
- **Settings:** Full config editor grouped by TOML section, username validation, auto-start toggle
- **Debug:** Config viewer, credential status, network info, test login/heartbeat/check buttons
- **About:** Version, update check with download progress, GitHub link
- **Tray icon:** Double-click show/hide, close minimizes to tray
- **Tray context menu:** Show/Hide, Quit
- **Auto-update:** Checks GitHub releases, downloads MSI, installs via msiexec
- **DHCP detection:** Warns if using DHCP, offers to save static IP
- Custom app icon (blue "K" + wifi arcs)
- `FileDescription="KMITL NetAuth"` for Task Manager display
- Requires Windows 10 version 1809+

## Don't

- Don't store passwords in config.toml (use credential store or env vars)
- Don't change auth endpoint URLs or form field names without verifying against the actual KMITL portal
- Don't add `#if WINDOWS` / `#if LINUX` to Core - use DI
- Don't reference KmitlNetAuth.Cli from KmitlNetAuth.Tray (they are independent)
- Don't add NuGet package versions in `.csproj` files (use `Directory.Packages.props`)
- Don't use WinForms in Tray project (it's WPF + wpfui)
- Don't use NativeConsole for logging in Tray (use `LogBufferSink` with the in-app Log page)
- Tray app requires Windows 10 version 1809+ (net10.0-windows10.0.17763.0)
