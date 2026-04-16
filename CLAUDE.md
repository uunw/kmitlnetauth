# CLAUDE.md - Development Guide for AI Assistants

## Project Overview

KMITL NetAuth is a cross-platform auto-authentication service for the KMITL university network. It's a .NET 10 solution with 3 projects.

## Quick Reference

```bash
# Build
dotnet build

# Run CLI
dotnet run --project src/KmitlNetAuth.Cli -- --help
dotnet run --project src/KmitlNetAuth.Cli -- status
dotnet run --project src/KmitlNetAuth.Cli -- setup
dotnet run --project src/KmitlNetAuth.Cli -- -d        # daemon mode

# Run Tray (Windows only, use EnableWindowsTargeting on other OS)
dotnet run --project src/KmitlNetAuth.Tray
```

## Solution Structure

| Project | Target | Purpose |
|---|---|---|
| `KmitlNetAuth.Core` | `net10.0` | Shared library: auth client, config, platform abstractions, DI |
| `KmitlNetAuth.Cli` | `net10.0` | CLI app with System.CommandLine, Serilog, daemon/systemd/Windows Service |
| `KmitlNetAuth.Tray` | `net10.0-windows10.0.17763.0` | WPF + wpfui tray app (Windows 10+ only) |

## Key Files

| File | What it does |
|---|---|
| `src/KmitlNetAuth.Core/AuthClient.cs` | HTTP requests: login, heartbeat, internet check |
| `src/KmitlNetAuth.Core/Config.cs` | YAML config loading, env var overrides, password migration |
| `src/KmitlNetAuth.Core/Services/AuthService.cs` | Main run loop (state machine) |
| `src/KmitlNetAuth.Core/DependencyInjection/CoreServiceCollectionExtensions.cs` | DI wiring, platform detection |
| `src/KmitlNetAuth.Cli/Program.cs` | CLI entry point (System.CommandLine) |
| `src/KmitlNetAuth.Tray/App.xaml.cs` | WPF app entry, host setup, tray icon |
| `src/KmitlNetAuth.Tray/SettingsWindow.xaml` | Fluent UI settings form |
| `src/KmitlNetAuth.Tray/UpdateChecker.cs` | Auto-update: check GitHub releases, download MSI, install |

## Important Conventions

### System.CommandLine v2 beta5+

This project uses the **beta5 API** which has breaking changes from beta4:
- `SetHandler` -> `SetAction`
- `AddCommand` -> `Subcommands.Add`
- `AddOption` -> `Options.Add`
- Option constructor: `new Option<T>("--name", "-alias")` then set `Description` separately
- Action receives `(ParseResult, CancellationToken)`, not `InvocationContext`
- Invoke: `command.Parse(args).InvokeAsync()` not `command.InvokeAsync(args)`

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

### Auth Endpoints (hardcoded, must match exactly)

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

### Config

- YAML via YamlDotNet with `UnderscoredNamingConvention`
- Env vars override file values (KMITL_USERNAME, KMITL_PASSWORD, etc.)
- Password auto-migrates to credential store on load
- `Config.Save()` strips password from YAML if credential store succeeds

### Versioning

- Date-based: `YYYYMMDD.N` (e.g., `20260416.0`)
- CI passes `/p:Version=` to dotnet publish
- Local builds use `0.0.0.1` from `Directory.Build.props`

## Build Requirements

- .NET 10 SDK (pinned in `global.json` to 10.0.201)
- `EnableWindowsTargeting=true` in `Directory.Build.props` for cross-compiling Tray on non-Windows
- `TreatWarningsAsErrors=true` - builds must have 0 warnings

## CI/CD

- `.github/workflows/build.yml` runs 3 parallel jobs: linux (deb/rpm), windows (msi), docker (ghcr)
- Linux packages: `dotnet-deb` and `dotnet-rpm` CLI tools
- Windows MSI: WiX Toolset v5+ (`packaging/wix/Package.wxs`)
- Docker: Alpine multi-stage with `linux-musl-x64`, self-contained + trimmed

## Don't

- Don't store passwords in config.yaml (use credential store or env vars)
- Don't change auth endpoint URLs or form field names without verifying against the actual KMITL portal
- Don't add `#if WINDOWS` / `#if LINUX` to Core - use DI
- Don't reference KmitlNetAuth.Cli from KmitlNetAuth.Tray (they are independent)
- Don't add NuGet package versions in `.csproj` files (use `Directory.Packages.props`)
- Don't use WinForms in Tray project (it's WPF + wpfui now)
- Tray app requires Windows 10 version 1809+ (net10.0-windows10.0.17763.0)
