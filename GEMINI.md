# Project Context: KMITL NetAuth (Rust)

A secure, cross-platform re-implementation of the Auto-Authen-KMITL tool using Rust. This project aims to provide a robust background service and system tray application for authenticating with the KMITL network.

## Architecture

The project is organized as a Cargo Workspace with the following crates:

- **`crates/core`**: Shared logic library.
    - **Authentication**: Handles login logic (`portal.kmitl.ac.th`), heartbeat (`nani.csc.kmitl.ac.th`), and internet connectivity checks.
    - **Configuration**: Manages `config.yaml` loading/saving and environment variables.
    - **Security**: Integrates with System Keyring (via `keyring` crate) to securely store passwords.
- **`crates/service`**: Background service daemon (`kmitlnetauth`).
    - **Interactive Setup**: CLI wizard (`dialoguer`) for first-time configuration.
    - **Logging**: Rotating file logs (`tracing-appender`) + Stdout.
    - **Daemon**: Designed to run via `systemd` (Linux) or as a background process (Windows/Docker).
- **`crates/tray`**: System Tray Application (`kmitlnetauth-tray`).
    - **GUI**: Settings window using `egui` + `eframe`.
    - **Controls**: Auto-login toggle, Auto-start toggle (Registry/XDG Autostart), Open config file.

## Platform Support & Distribution

1.  **Windows**:
    - **MSI Installer**: Generated via `cargo-wix`. Installs service and tray app to `Program Files`.
    - **Tray App**: Provides GUI for settings.
2.  **Linux**:
    - **Service**: Runs via `systemd`. Unit file provided in `crates/service/kmitlnetauth.service`.
    - **Config**: Default path `/etc/kmitlnetauth/config.yaml` or `~/.config/kmitlnetauth/config.yaml`.
3.  **Docker**:
    - **Image**: Alpine-based (~10MB compressed).
    - **Config**: Fully configurable via Environment Variables (`KMITL_USERNAME`, `KMITL_PASSWORD`, etc.).

## Key Features

- **Secure Credentials**: Passwords are migrated to the OS Keyring and removed from `config.yaml` on first run.
- **Auto-Reconnect**: Monitors connection and re-authenticates automatically.
- **Notifications**: System desktop notifications on status changes (Login success/fail, Network lost).
- **Log Rotation**: Daily logs to prevent disk usage issues.

## Development

- **Tooling**: Uses `bacon` for background checking/testing.
- **CI/CD**: GitHub Actions automates builds for:
    - Docker Image (ghcr.io)
    - Windows MSI Installer
    - Linux Binary

## References
- Original Repo: https://github.com/CE-HOUSE/Auto-Authen-KMITL