# KMITL NetAuth - Installation Guide

## Debian / Ubuntu

### Install from GitHub Releases (Recommended)

Download and install the latest `.deb` package:

```bash
# Download latest release
curl -LO https://github.com/uunw/kmitlnetauth/releases/latest/download/kmitlnetauth_amd64.deb

# Install (requires dotnet-runtime-10.0)
sudo dpkg -i kmitlnetauth_*.deb
```

The package will:
- Install to `/usr/lib/kmitlnetauth/` with a wrapper script at `/usr/bin/kmitlnetauth`
- Install the systemd service to `/etc/systemd/system/kmitlnetauth.service`
- Enable and start the service automatically

> **Note:** The `.deb` package is framework-dependent (~1-2 MB) and requires `dotnet-runtime-10.0`. Install it from [Microsoft's package repository](https://learn.microsoft.com/en-us/dotnet/core/install/linux) if not already present.

### First-time setup

After installation, run the interactive setup wizard:

```bash
sudo kmitlnetauth setup
```

Or edit the config file manually:

```bash
sudo nano /etc/kmitlnetauth/config.toml
```

Then restart the service:

```bash
sudo systemctl restart kmitlnetauth
```

### Uninstall

```bash
sudo apt remove kmitlnetauth
```

---

## Red Hat / CentOS / Fedora

### Install from GitHub Releases

```bash
# Download latest release (requires dotnet-runtime-10.0)
sudo rpm -i https://github.com/uunw/kmitlnetauth/releases/latest/download/kmitlnetauth.x86_64.rpm
```

### First-time setup

```bash
sudo kmitlnetauth setup
sudo systemctl restart kmitlnetauth
```

### Uninstall

```bash
sudo rpm -e kmitlnetauth
```

---

## Windows

### MSI Installer (Recommended)

> **Requires Windows 10 version 1809 or later.**

1. Download the latest `.msi` from [GitHub Releases](https://github.com/uunw/kmitlnetauth/releases/latest)
2. Run the installer (installs to `C:\Program Files\KMITL NetAuth\`)
3. The GUI app launches automatically after installation
4. The app includes a full GUI with sidebar navigation:
   - **Dashboard** - Status indicator, username, IP, uptime, Login Now/Pause buttons
   - **Log** - In-app live log viewer with level filter
   - **Settings** - Full config editor grouped by TOML section, auto-start toggle
   - **Debug** - Config viewer, credential status, network info, test buttons
   - **About** - Version, update check with download progress

> **Note:** The MSI is framework-dependent (~8 MB) and requires the [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). The installer will prompt to install it if missing.

### Manual Installation

Download `kmitlnetauth.exe` from GitHub Releases and run:

```powershell
# Interactive setup
.\kmitlnetauth.exe setup

# Run as foreground service
.\kmitlnetauth.exe

# Run as daemon
.\kmitlnetauth.exe -d
```

To install as a Windows Service:

```powershell
sc.exe create KmitlNetAuth binPath="C:\Program Files\KMITL NetAuth\kmitlnetauth.exe -d" start=auto
sc.exe start KmitlNetAuth
```

---

## Docker

### Quick Start

```bash
docker run -d \
  --name kmitlnetauth \
  --restart unless-stopped \
  -e KMITL_USERNAME="670xxxxx" \
  -e KMITL_PASSWORD="your_password" \
  ghcr.io/uunw/kmitlnetauth:latest
```

### With custom settings

```bash
docker run -d \
  --name kmitlnetauth \
  --restart unless-stopped \
  -e KMITL_USERNAME="670xxxxx" \
  -e KMITL_PASSWORD="your_password" \
  -e KMITL_IP="10.x.x.x" \
  -e KMITL_INTERVAL=300 \
  -e KMITL_MAX_ATTEMPT=20 \
  -e KMITL_AUTO_LOGIN=true \
  -e KMITL_LOG_LEVEL=Information \
  ghcr.io/uunw/kmitlnetauth:latest
```

### Docker Compose

```yaml
services:
  kmitlnetauth:
    image: ghcr.io/uunw/kmitlnetauth:latest
    restart: unless-stopped
    environment:
      KMITL_USERNAME: "670xxxxx"
      KMITL_PASSWORD: "your_password"
      KMITL_IP: ""
      KMITL_INTERVAL: 300
```

---

## Systemd Service Management

```bash
# Check status
sudo systemctl status kmitlnetauth

# View logs
sudo journalctl -u kmitlnetauth -f

# Restart
sudo systemctl restart kmitlnetauth

# Stop
sudo systemctl stop kmitlnetauth

# Disable auto-start
sudo systemctl disable kmitlnetauth
```

---

## Environment Variables

All configuration can be overridden via environment variables:

| Variable | Description | Default |
|---|---|---|
| `KMITL_USERNAME` | Student ID | *(required)* |
| `KMITL_PASSWORD` | Password | *(required)* |
| `KMITL_IP` | Static IP address | Auto-detect |
| `KMITL_INTERVAL` | Heartbeat interval (seconds) | `300` |
| `KMITL_MAX_ATTEMPT` | Max login retry attempts | `20` |
| `KMITL_AUTO_LOGIN` | Enable auto-login | `true` |
| `KMITL_LOG_LEVEL` | Log level (Verbose/Debug/Information/Warning/Error) | `Information` |

---

## Config File

Default locations:
- **Linux (global):** `/etc/kmitlnetauth/config.toml`
- **Linux (user):** `~/.config/kmitlnetauth/config.toml`
- **Windows:** `%APPDATA%\kmitlnetauth\config.toml`

> **Note:** Legacy `config.yaml` files are automatically migrated to `config.toml` on first load.

Example `config.toml`:

```toml
[auth]
username = "670xxxxx"
ip_address = "10.x.x.x"

[service]
interval = 300
max_attempt = 20
auto_login = true

[logging]
level = "Information"
```

> **Note:** Passwords are stored securely in the OS credential store (Windows DPAPI / Linux encrypted file). They are never written to the config file in plain text after first setup.
