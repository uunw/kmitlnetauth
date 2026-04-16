# KMITL NetAuth

A secure, cross-platform auto-authentication service for the KMITL network. Built with .NET 10.

## Features

- **Auto-Reconnect** - Monitors connection and re-authenticates automatically
- **Secure Credentials** - Passwords stored via Windows DPAPI or encrypted file (Linux), never in plain text
- **Cross-Platform** - Windows (MSI + system tray), Linux (DEB/RPM + systemd), Docker
- **System Tray** - Windows tray app with auto-login toggle, auto-start, and log level control
- **CLI** - Interactive setup wizard, status display, daemon mode (`-d`)
- **Notifications** - Desktop notifications on connection state changes
- **Log Rotation** - Daily rotating log files

## Quick Start

```bash
# Interactive setup
kmitlnetauth setup

# Run in foreground
kmitlnetauth

# Run as daemon
kmitlnetauth -d

# Check status
kmitlnetauth status
```

## Architecture

```
KmitlNetAuth.sln
├── KmitlNetAuth.Core     # Shared library: auth, config, platform abstractions
├── KmitlNetAuth.Cli      # CLI application with daemon mode
└── KmitlNetAuth.Tray     # Windows system tray app (WinForms)
```

| Component | Description |
|---|---|
| **Core** | Auth client (login, heartbeat, internet check), YAML config with env var overrides, credential storage, notifications, auto-start |
| **CLI** | `System.CommandLine` with subcommands (setup, status, config), `Serilog` logging, `Microsoft.Extensions.Hosting` for daemon/systemd/Windows Service |
| **Tray** | WinForms `NotifyIcon` with context menu, hosts auth worker in background |

## Installation

See [Installation Guide](docs/INSTALL-PACKAGES.md) for detailed instructions.

### Docker

```bash
docker run -d \
  -e KMITL_USERNAME="670xxxxx" \
  -e KMITL_PASSWORD="your_password" \
  ghcr.io/OWNER/kmitlnetauth:latest
```

## Configuration

Default config locations:
- **Linux:** `/etc/kmitlnetauth/config.yaml` or `~/.config/kmitlnetauth/config.yaml`
- **Windows:** `%APPDATA%\kmitlnetauth\config.yaml`

```yaml
username: "670xxxxx"
ip_address: "10.x.x.x"
interval: 300
max_attempt: 20
auto_login: true
log_level: Information
```

All settings can be overridden via environment variables (`KMITL_USERNAME`, `KMITL_PASSWORD`, `KMITL_IP`, etc.).

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build

```bash
dotnet build
```

### Run

```bash
# CLI
dotnet run --project src/KmitlNetAuth.Cli

# Tray (Windows only)
dotnet run --project src/KmitlNetAuth.Tray
```

### Publish

```bash
# Linux self-contained binary
dotnet publish src/KmitlNetAuth.Cli -c Release -r linux-x64 --self-contained /p:PublishSingleFile=true

# Windows self-contained binary
dotnet publish src/KmitlNetAuth.Cli -c Release -r win-x64 --self-contained /p:PublishSingleFile=true
```

## CI/CD

GitHub Actions builds on every push:
- **Linux:** Binary + `.deb` + `.rpm`
- **Windows:** Binary + `.msi`
- **Docker:** Image pushed to `ghcr.io`

Releases are created automatically on tag push (`v*`).

### Version Format

Date-based: `YYYYMMDD.N` (e.g., `20260416.0`)

## License

MIT

## References

- Original: [Auto-Authen-KMITL](https://github.com/CE-HOUSE/Auto-Authen-KMITL)
