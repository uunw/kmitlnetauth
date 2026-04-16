# KMITL NetAuth

A secure, cross-platform auto-authentication service for the KMITL network. Built with .NET 10.

## Features

- **Auto-Reconnect** - Monitors connection and re-authenticates automatically
- **Secure Credentials** - Passwords stored via Windows DPAPI or encrypted file (Linux), never in plain text
- **Cross-Platform** - Windows 10+ (MSI + system tray), Linux (DEB/RPM + systemd), Docker
- **System Tray** - Windows tray app with Fluent UI (WPF), auto-login, auto-start, settings, auto-update
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
└── KmitlNetAuth.Tray     # Windows 10+ system tray app (WPF + wpfui)
```

| Component | Description |
|---|---|
| **Core** | Auth client (login, heartbeat, internet check), YAML config with env var overrides, credential storage, notifications, auto-start |
| **CLI** | `System.CommandLine` with subcommands (setup, status, config), `Serilog` logging, `Microsoft.Extensions.Hosting` for daemon/systemd/Windows Service |
| **Tray** | WPF with Fluent UI (wpfui), system tray, settings window, auto-update with download, console log viewer |

## Quick Login (Headless Linux)

Need internet on a headless server before you can install anything?

```bash
bash scripts/kmitl-login.sh
```

Or a raw curl one-liner:

```bash
curl -sk -X POST "https://portal.kmitl.ac.th:19008/portalauth/login" \
  -d "userName=YOUR_ID&userPass=YOUR_PASS&uaddress=&umac=$(ip link show | awk '/ether/{print $2;exit}' | tr -d ':')&agreed=1&acip=10.252.13.10&authType=1"
```

## Installation

See the **[Full Installation Guide](docs/INSTALL.md)** for all platforms (Linux, Windows, Docker) with step-by-step instructions including build-from-source.

Quick links: [Debian/Ubuntu](docs/INSTALL.md#linux---install-from-github-releases) | [RHEL/CentOS](docs/INSTALL.md#linux---install-from-github-releases) | [Windows MSI](docs/INSTALL.md#windows---msi-installer) | [Docker](docs/INSTALL.md#docker) | [Build from source](docs/INSTALL.md#linux---build-from-source)

### Docker

```bash
docker run -d \
  -e KMITL_USERNAME="670xxxxx" \
  -e KMITL_PASSWORD="your_password" \
  ghcr.io/uunw/kmitlnetauth:latest
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

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, conventions, and how to submit changes.

## License

[MIT](LICENSE)

## References

- Original: [Auto-Authen-KMITL](https://github.com/CE-HOUSE/Auto-Authen-KMITL)
