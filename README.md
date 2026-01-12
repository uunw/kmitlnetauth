# KMITL NetAuth (Rust Re-implementation)

A secure, cross-platform Rust implementation of the Auto-Authen-KMITL tool for Windows, Linux, and Docker.

## Features

- **Cross-Platform**: Supports Windows (MSI/Service/Tray), Linux (systemd/CLI), and Docker (Alpine).
- **Secure Credentials**: Uses System Keyring (Windows Credential Manager / Linux Secret Service) to store passwords.
- **Interactive Setup**: Built-in CLI wizard for first-time configuration.
- **System Tray**: GUI settings and status monitoring via a system tray icon.
- **Log Rotation**: Automatic daily log rotation to prevent disk bloat.
- **Docker Ready**: Supports environment variables for easy container deployment.

## Components

- **Core (`kmitlnetauth-core`)**: Shared authentication logic and secure config management.
- **Service (`kmitlnetauth`)**: Background daemon for continuous connectivity maintenance.
- **Tray (`kmitlnetauth-tray`)**: System tray application for Windows/Linux GUI.

## Development

We use [bacon](https://github.com/Canop/bacon) for background checking.

```bash
cargo install bacon
bacon            # Continuous check
bacon run-tray   # Run tray app
bacon run-service # Run service
```

## Build & Installation

### Linux

1. **Build**:
   ```bash
   cargo build --release
   ```
2. **Service Setup**:
   - Copy `target/release/kmitlnetauth` to `/usr/bin/`.
   - Copy `crates/service/kmitlnetauth.service` to `/etc/systemd/system/`.
   - `sudo systemctl enable --now kmitlnetauth.service`

3. **Global Config**: `/etc/kmitlnetauth/config.yaml`
4. **User Config**: `~/.config/kmitlnetauth/config.yaml`

### Windows

1. **Manual Run**:
   - `kmitlnetauth.exe`: Background service.
   - `kmitlnetauth-tray.exe`: Tray icon with GUI settings.

2. **MSI Installer**:
   - Requires [WiX Toolset](https://wixtoolset.org/).
   - Install helper: `cargo install cargo-wix`
   - Build: `cargo wix -p kmitlnetauth-tray --nocapture`
   - Result: `target/wix/*.msi` (Includes shortcuts and uninstaller).

### Docker

Build and run using environment variables:

```bash
docker build -t kmitlnetauth .
docker run -d \
  -e KMITL_USERNAME="670xxxxx" \
  -e KMITL_PASSWORD="your_password" \
  -e KMITL_IP="10.x.x.x" \
  kmitlnetauth
```

## Configuration

Settings are stored in `config.yaml`. **Environment Variables** override file settings:

- `KMITL_USERNAME`: Your Student ID.
- `KMITL_PASSWORD`: Your Password (migrated to keyring on first run).
- `KMITL_IP`: Your Static IP address.
- `KMITL_INTERVAL`: Heartbeat interval in seconds (default: 300).
- `KMITL_AUTO_LOGIN`: Set to `true` or `false`.

## Security Note

This application does **not** store your password in plain text in the configuration file by default. It uses the operating system's secure credential store. If you provide a password via `config.yaml` or Environment Variable, it will be automatically migrated to the secure store and removed from the plain-text file.

## CI/CD

GitHub Actions are configured to build Linux binaries, Windows MSI installers, and Docker images automatically on push to `main`.