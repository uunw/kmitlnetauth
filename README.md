# KMITL NetAuth

A secure, cross-platform auto-authentication service for the KMITL network. Built with .NET 10.

## Features

- **Auto-Reconnect** - Monitors connection and re-authenticates automatically
- **Secure Credentials** - Passwords stored via Windows DPAPI or encrypted file (Linux), never in plain text
- **Cross-Platform** - Windows 10+ (MSI + GUI), Linux (DEB/RPM + systemd), Docker
- **Windows GUI** - Full WPF + wpfui app with Dashboard, Log viewer, Settings, Debug, and About pages
- **CLI** - Interactive setup wizard, status display, daemon mode (`-d`)
- **Auto-Update** - Checks GitHub releases, downloads and installs updates automatically (Windows)
- **TOML Config** - Grouped, commented config file with environment variable overrides
- **Log Rotation** - Daily rotating log files with configurable retention
- **46 Tests** - xunit test coverage across Core (38) and CLI (8)

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
├── KmitlNetAuth.Core        # Shared library: auth, config, platform abstractions
├── KmitlNetAuth.Cli         # CLI application with daemon mode
├── KmitlNetAuth.Tray        # Windows 10+ GUI app (WPF + wpfui)
├── KmitlNetAuth.Core.Tests  # 38 xunit tests
└── KmitlNetAuth.Cli.Tests   # 8 xunit tests
```

| Component | Description |
|---|---|
| **Core** | Auth client (configurable URLs), TOML config (Tomlyn) with env var overrides, credential storage (DPAPI/AES), DHCP detection, notifications, auto-start |
| **CLI** | `System.CommandLine` v2.0.6 with subcommands (run, setup, status, config), `Serilog` logging, `Spectre.Console` setup wizard, systemd/Windows Service integration |
| **Tray** | WPF + wpfui GUI with sidebar navigation: Dashboard, Log, Settings, Debug, About pages. Tray icon, auto-update with MSI download, DHCP detection |

## Quick Login (Headless Linux)

Need internet on a headless server (e.g., Proxmox) before you can install anything?

```bash
bash scripts/kmitl-login.sh
```

Or a raw curl one-liner:

```bash
curl -sk -X POST "https://portal.kmitl.ac.th:19008/portalauth/login" \
  -d "userName=YOUR_ID&userPass=YOUR_PASS&uaddress=&umac=$(ip link show | awk '/ether/{print $2;exit}' | tr -d ':')&agreed=1&acip=10.252.13.10&authType=1"
```

## Installation

See the **[Full Installation Guide](docs/INSTALL.md)** for all platforms with step-by-step instructions.

Quick links: [Debian/Ubuntu](docs/INSTALL.md#linux---install-from-github-releases) | [RHEL/CentOS](docs/INSTALL.md#linux---install-from-github-releases) | [Windows MSI](docs/INSTALL.md#windows---msi-installer) | [Docker](docs/INSTALL.md#docker) | [Build from source](docs/INSTALL.md#linux---build-from-source)

### Deployment Strategy

| Format | Type | Size | Runtime Required? |
|---|---|---|---|
| Linux standalone binary | Self-contained, single-file | ~14 MB | No |
| `.deb` / `.rpm` | Framework-dependent | ~1-2 MB | Yes (`dotnet-runtime-10.0`) |
| Windows `.msi` | Framework-dependent | ~8 MB | Yes (.NET 10 Runtime, auto-prompted) |
| Docker | Self-contained, multi-platform | ~14 MB | No |

### Docker

```bash
docker run -d \
  -e KMITL_USERNAME="670xxxxx" \
  -e KMITL_PASSWORD="your_password" \
  ghcr.io/uunw/kmitlnetauth:latest
```

## Configuration

TOML config with grouped sections. Default locations:
- **Linux:** `/etc/kmitlnetauth/config.toml` or `~/.config/kmitlnetauth/config.toml`
- **Windows:** `%APPDATA%\kmitlnetauth\config.toml`

```toml
[auth]
username = "670xxxxx"
ip_address = ""                        # Leave empty for auto-detect

[service]
interval = 300                         # Heartbeat interval (seconds)
max_attempt = 20                       # Max login retries before backoff
auto_login = true

[logging]
level = "Information"                  # Verbose / Debug / Information / Warning / Error

[notifications]
enabled = true

[update]
auto_check = true
check_interval_hours = 24
```

All settings can be overridden via environment variables (`KMITL_USERNAME`, `KMITL_PASSWORD`, `KMITL_IP`, etc.).

See [docs/INSTALL.md#configuration-reference](docs/INSTALL.md#configuration-reference) for the full list.

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build & Test

```bash
dotnet build
dotnet test    # 46 tests
```

### Run

```bash
# CLI
dotnet run --project src/KmitlNetAuth.Cli

# Tray (Windows 10+ only)
dotnet run --project src/KmitlNetAuth.Tray
```

## CI/CD

GitHub Actions auto-builds and releases on every push to main:
- **Linux:** Standalone binary (x64 + arm64) + `.deb` + `.rpm`
- **Windows:** `.msi` installer (x64) via WiX v7
- **Docker:** Multi-arch image (amd64 + arm64) pushed to `ghcr.io/uunw/kmitlnetauth`
- **Tests:** 46 tests run in CI (amd64)
- **Release:** Auto-tagged with date-based version (`YYYYMMDD.N`)

Dependabot keeps NuGet packages, GitHub Actions, and Docker base images up to date.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, conventions, and how to submit changes.

## License

[MIT](LICENSE)

## References

- Original: [Auto-Authen-KMITL](https://github.com/CE-HOUSE/Auto-Authen-KMITL)
