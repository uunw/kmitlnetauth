# Installation Guide

## Table of Contents

- [Quick Login (Headless Linux)](#quick-login-headless-linux)
- [Linux](#linux)
  - [Install from GitHub Releases](#linux---install-from-github-releases)
  - [Build from Source](#linux---build-from-source)
  - [Systemd Service](#systemd-service-management)
- [Windows](#windows)
  - [MSI Installer](#windows---msi-installer)
  - [Manual / Portable](#windows---manual--portable)
  - [Build from Source](#windows---build-from-source)
  - [Windows Service](#windows-service)
- [Docker](#docker)
  - [Docker Run](#docker-run)
  - [Docker Compose](#docker-compose)
  - [Build Image Locally](#build-docker-image-locally)
- [Configuration Reference](#configuration-reference)
- [Troubleshooting](#troubleshooting)

---

## Quick Login (Headless Linux)

If you're on a headless server (e.g., Proxmox, bare-metal) and need internet access **before** you can download anything, use the one-shot login script:

```bash
# Option 1: If you already have the script on a USB/local network
bash kmitl-login.sh

# Option 2: If you have curl but no internet (won't work - use Option 1)
# Copy the script from another machine first, then run it.
```

The script prompts for your Student ID and password, sends a login request to the KMITL portal, and verifies connectivity.

```bash
# With arguments (non-interactive)
bash kmitl-login.sh -u 670xxxxx -p yourpassword
```

After you have internet, proceed with the full installation below.

> **Tip:** You can also do a manual login with a single curl command:
>
> ```bash
> curl -sk -X POST "https://portal.kmitl.ac.th:19008/portalauth/login" \
>   -d "userName=YOUR_STUDENT_ID" \
>   -d "userPass=YOUR_PASSWORD" \
>   -d "uaddress=" \
>   -d "umac=$(ip link show | awk '/ether/ {print $2; exit}' | tr -d ':')" \
>   -d "agreed=1" \
>   -d "acip=10.252.13.10" \
>   -d "authType=1"
> ```

---

## Linux

### Linux - Install from GitHub Releases

#### Debian / Ubuntu (.deb)

```bash
# Download the latest .deb package
curl -LO https://github.com/uunw/kmitlnetauth/releases/latest/download/kmitlnetauth_amd64.deb

# Install
sudo dpkg -i kmitlnetauth_*.deb

# The service is automatically enabled and started after installation.
```

#### Red Hat / CentOS / Fedora (.rpm)

```bash
# Download and install
sudo rpm -i https://github.com/uunw/kmitlnetauth/releases/latest/download/kmitlnetauth.x86_64.rpm
```

#### Standalone Binary

```bash
# Download the binary
curl -LO https://github.com/uunw/kmitlnetauth/releases/latest/download/kmitlnetauth
chmod +x kmitlnetauth

# Move to system path
sudo mv kmitlnetauth /usr/bin/

# Copy systemd service file
sudo curl -o /etc/systemd/system/kmitlnetauth.service \
  https://raw.githubusercontent.com/uunw/kmitlnetauth/main/packaging/systemd/kmitlnetauth.service

sudo systemctl daemon-reload
sudo systemctl enable --now kmitlnetauth
```

#### First-Time Setup

After installation, run the interactive setup wizard:

```bash
# If installed system-wide
sudo kmitlnetauth setup

# Or as current user
kmitlnetauth setup
```

This will prompt you for:
- **Student ID** - Your KMITL student number
- **Password** - Stored securely in an encrypted file (not in config.yaml)
- **IP Address** - Optional, auto-detected if empty
- **Heartbeat Interval** - Default 300 seconds
- **Auto-login** - Enable/disable

Alternatively, edit the config file manually:

```bash
# System-wide config
sudo nano /etc/kmitlnetauth/config.yaml

# Or user config
nano ~/.config/kmitlnetauth/config.yaml
```

Example `config.yaml`:

```yaml
username: "670xxxxx"
ip_address: "10.x.x.x"
interval: 300
max_attempt: 20
auto_login: true
log_level: Information
```

Then restart the service:

```bash
sudo systemctl restart kmitlnetauth
```

### Linux - Build from Source

#### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

```bash
# Install .NET 10 SDK (example for Ubuntu/Debian)
# See https://learn.microsoft.com/en-us/dotnet/core/install/linux for your distro
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0

# Verify
dotnet --version
```

#### Build

```bash
# Clone the repository
git clone https://github.com/uunw/kmitlnetauth.git
cd kmitlnetauth

# Build
dotnet build

# Run directly
dotnet run --project src/KmitlNetAuth.Cli

# Or publish a self-contained single binary
dotnet publish src/KmitlNetAuth.Cli/KmitlNetAuth.Cli.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  /p:PublishSingleFile=true \
  -o ./publish

# The binary is at ./publish/kmitlnetauth
./publish/kmitlnetauth --help
```

#### Install the Built Binary

```bash
sudo cp ./publish/kmitlnetauth /usr/bin/
sudo cp packaging/systemd/kmitlnetauth.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now kmitlnetauth
```

### Systemd Service Management

```bash
# Check status
sudo systemctl status kmitlnetauth

# View logs (live)
sudo journalctl -u kmitlnetauth -f

# View recent logs
sudo journalctl -u kmitlnetauth --since "1 hour ago"

# Restart
sudo systemctl restart kmitlnetauth

# Stop
sudo systemctl stop kmitlnetauth

# Disable auto-start on boot
sudo systemctl disable kmitlnetauth

# Re-enable
sudo systemctl enable kmitlnetauth
```

Log files are also written to `~/.local/share/kmitlnetauth/logs/` with daily rotation.

---

## Windows

### Windows - MSI Installer

1. Download the latest `.msi` from [GitHub Releases](https://github.com/uunw/kmitlnetauth/releases/latest)
2. Run the installer - it will install both the CLI and the system tray app to `C:\Program Files\KMITL NetAuth\`
3. The tray app launches automatically after installation
4. Right-click the tray icon to:
   - Toggle **Auto Login**
   - Toggle **Auto Start** (runs on Windows startup)
   - Change **Log Level**
   - Open **Settings** (config file)
   - **Quit**

If no username is configured, the config file opens automatically in your default text editor on first launch.

### Windows - Manual / Portable

Download the standalone executables from [GitHub Releases](https://github.com/uunw/kmitlnetauth/releases/latest):

- `kmitlnetauth.exe` - CLI application
- `kmitlnetauth-tray.exe` - System tray application

```powershell
# Interactive setup
.\kmitlnetauth.exe setup

# Check status
.\kmitlnetauth.exe status

# Run in foreground
.\kmitlnetauth.exe

# Run as daemon (background)
.\kmitlnetauth.exe -d

# Or just run the tray app (includes the auth service)
.\kmitlnetauth-tray.exe
```

Config file location: `%APPDATA%\kmitlnetauth\config.yaml`

### Windows - Build from Source

#### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

```powershell
# Clone
git clone https://github.com/uunw/kmitlnetauth.git
cd kmitlnetauth

# Build all projects
dotnet build

# Run CLI
dotnet run --project src/KmitlNetAuth.Cli

# Run Tray
dotnet run --project src/KmitlNetAuth.Tray

# Publish self-contained
dotnet publish src/KmitlNetAuth.Cli/KmitlNetAuth.Cli.csproj `
  -c Release -r win-x64 --self-contained true `
  /p:PublishSingleFile=true -o ./publish/cli

dotnet publish src/KmitlNetAuth.Tray/KmitlNetAuth.Tray.csproj `
  -c Release -r win-x64 --self-contained true `
  /p:PublishSingleFile=true -o ./publish/tray
```

### Windows Service

To install the CLI as a Windows Service:

```powershell
# Install as service (run PowerShell as Administrator)
sc.exe create KmitlNetAuth `
  binPath="C:\Program Files\KMITL NetAuth\kmitlnetauth.exe -d" `
  start=auto `
  DisplayName="KMITL NetAuth"

# Start the service
sc.exe start KmitlNetAuth

# Check status
sc.exe query KmitlNetAuth

# Stop
sc.exe stop KmitlNetAuth

# Remove
sc.exe delete KmitlNetAuth
```

---

## Docker

### Docker Run

```bash
docker run -d \
  --name kmitlnetauth \
  --restart unless-stopped \
  -e KMITL_USERNAME="670xxxxx" \
  -e KMITL_PASSWORD="your_password" \
  ghcr.io/uunw/kmitlnetauth:latest
```

### With All Options

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
      KMITL_MAX_ATTEMPT: 20
      KMITL_AUTO_LOGIN: true
      KMITL_LOG_LEVEL: Information
```

```bash
docker compose up -d
```

### Manage

```bash
# View logs
docker logs -f kmitlnetauth

# Stop
docker stop kmitlnetauth

# Remove
docker rm kmitlnetauth

# Update to latest
docker pull ghcr.io/uunw/kmitlnetauth:latest
docker stop kmitlnetauth && docker rm kmitlnetauth
# Then re-run the docker run command above
```

### Build Docker Image Locally

```bash
git clone https://github.com/uunw/kmitlnetauth.git
cd kmitlnetauth

docker build -t kmitlnetauth .

docker run -d \
  --name kmitlnetauth \
  -e KMITL_USERNAME="670xxxxx" \
  -e KMITL_PASSWORD="your_password" \
  kmitlnetauth
```

---

## Configuration Reference

### Config File Locations

| Platform | Path |
|---|---|
| Linux (global) | `/etc/kmitlnetauth/config.yaml` |
| Linux (user) | `~/.config/kmitlnetauth/config.yaml` |
| Windows | `%APPDATA%\kmitlnetauth\config.yaml` |

Priority: CLI `--config` flag > global path (if exists) > user path.

### Config Fields

```yaml
username: "670xxxxx"        # Student ID (required)
ip_address: "10.x.x.x"     # Static IP (optional, auto-detect if empty)
interval: 300               # Heartbeat interval in seconds (default: 300)
max_attempt: 20             # Max login retries before backoff (default: 20)
auto_login: true            # Enable auto-login (default: true)
log_level: Information      # Verbose / Debug / Information / Warning / Error
```

> **Note:** Passwords are **never** stored in the config file. They are kept in the OS credential store:
> - **Windows:** DPAPI (encrypted per-user)
> - **Linux:** AES-encrypted file at `~/.config/kmitlnetauth/.credentials` (chmod 600)
> - **Docker:** Use the `KMITL_PASSWORD` environment variable

### Environment Variable Overrides

All config fields can be overridden via environment variables. Useful for Docker and CI.

| Variable | Config Field | Example |
|---|---|---|
| `KMITL_USERNAME` | `username` | `670xxxxx` |
| `KMITL_PASSWORD` | `password` | *(your password)* |
| `KMITL_IP` | `ip_address` | `10.0.0.50` |
| `KMITL_INTERVAL` | `interval` | `300` |
| `KMITL_MAX_ATTEMPT` | `max_attempt` | `20` |
| `KMITL_AUTO_LOGIN` | `auto_login` | `true` |
| `KMITL_LOG_LEVEL` | `log_level` | `Information` |

### Log File Locations

| Platform | Path |
|---|---|
| Linux | `~/.local/share/kmitlnetauth/logs/kmitlnetauth-YYYYMMDD.log` |
| Windows | `%LOCALAPPDATA%\kmitlnetauth\logs\kmitlnetauth-YYYYMMDD.log` |

Logs rotate daily and retain the last 30 days.

---

## Troubleshooting

### "Username not set in config"

Run the setup wizard first:

```bash
kmitlnetauth setup
```

### Service won't start

Check logs:

```bash
# Systemd
sudo journalctl -u kmitlnetauth -n 50

# Or check the log file directly
cat ~/.local/share/kmitlnetauth/logs/kmitlnetauth-*.log
```

### Login keeps failing

1. Verify your credentials work on the [KMITL portal](https://portal.kmitl.ac.th) directly
2. Check if your IP/MAC is correct: `kmitlnetauth status`
3. Try the one-shot script to test: `bash scripts/kmitl-login.sh`
4. Check if the portal is reachable: `curl -sk https://portal.kmitl.ac.th:19008/`

### Docker container exits immediately

Check logs:

```bash
docker logs kmitlnetauth
```

Make sure environment variables are set correctly (`KMITL_USERNAME` and `KMITL_PASSWORD` are required).

### Permission denied on Linux credential file

The credential file must be owned by the user running the service:

```bash
ls -la ~/.config/kmitlnetauth/.credentials
# Should be -rw------- (600)

# Fix ownership if needed
sudo chown $(whoami) ~/.config/kmitlnetauth/.credentials
chmod 600 ~/.config/kmitlnetauth/.credentials
```
