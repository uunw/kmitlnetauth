use clap::Parser;
use kmitlnetauth_core::{AuthClient, Config};
use std::path::PathBuf;
use tracing::{info, error, Level};
use directories::ProjectDirs;
use tracing_appender::rolling::{RollingFileAppender, Rotation};
use std::io::{Write, IsTerminal};
use std::str::FromStr;

mod tui;

struct CombinedWriter<W1: Write, W2: Write> {
    w1: W1,
    w2: W2,
}

impl<W1: Write, W2: Write> Write for CombinedWriter<W1, W2> {
    fn write(&mut self, buf: &[u8]) -> std::io::Result<usize> {
        let res = self.w1.write(buf)?;
        self.w2.write_all(buf)?;
        Ok(res)
    }

    fn flush(&mut self) -> std::io::Result<()> {
        self.w1.flush()?;
        self.w2.flush()?;
        Ok(())
    }
}

#[derive(Parser, Debug)]
#[command(author, version, about, long_about = None)]
struct Args {
    /// Path to config file
    #[arg(short, long)]
    config: Option<PathBuf>,

    /// Run as daemon (no TUI)
    #[arg(short, long)]
    daemon: bool,
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let args = Args::parse();

    // Determine config path
    let config_path = if let Some(path) = args.config {
        path
    } else {
        // Default paths
        if cfg!(target_os = "linux") {
            let global_path = PathBuf::from("/etc/kmitlnetauth/config.yaml");
            if global_path.exists() {
                global_path
            } else {
                 match ProjectDirs::from("com", "kmitl", "netauth") {
                    Some(proj_dirs) => proj_dirs.config_dir().join("config.yaml"),
                    None => PathBuf::from("config.yaml"),
                }
            }
        } else {
             match ProjectDirs::from("com", "kmitl", "netauth") {
                Some(proj_dirs) => proj_dirs.config_dir().join("config.yaml"),
                None => PathBuf::from("config.yaml"),
            }
        }
    };

    // Peek config for log level
    let log_level_str = Config::load(&config_path).map(|c| c.log_level).unwrap_or_else(|_| "info".to_string());
    let log_level = Level::from_str(&log_level_str).unwrap_or(Level::INFO);

    // Check mode
    let is_interactive = std::io::stdout().is_terminal();
    let run_as_daemon = args.daemon || !is_interactive;

    if run_as_daemon {
        // Setup Daemon Logging (File + Stdout)
        let log_dir = if let Some(proj_dirs) = ProjectDirs::from("com", "kmitl", "netauth") {
            proj_dirs.data_local_dir().join("logs")
        } else {
            PathBuf::from("logs")
        };

        let file_appender = RollingFileAppender::new(Rotation::DAILY, &log_dir, "service.log");
        let multi_writer = CombinedWriter {
            w1: std::io::stdout(),
            w2: file_appender,
        };
        let (non_blocking, _guard) = tracing_appender::non_blocking(multi_writer);

        tracing_subscriber::fmt()
            .with_writer(non_blocking)
            .with_ansi(false)
            .with_max_level(log_level)
            .init();

        info!("Starting KMITL NetAuth Service (Daemon)");
        info!("Using config file: {:?}", config_path);
    } else {
        // Setup TUI Logging
        tui_logger::init_logger(log::LevelFilter::from_str(&log_level_str).unwrap_or(log::LevelFilter::Info))?;
        tui_logger::set_default_level(log::LevelFilter::from_str(&log_level_str).unwrap_or(log::LevelFilter::Info));
    }

    let mut config = match Config::load(&config_path) {
        Ok(cfg) => cfg,
        Err(e) => {
            error!("Failed to load config: {}", e);
            Config::default()
        }
    };
    
    // Interactive Setup (Only if TUI/Interactive mode and missing creds)
    // Actually, TUI can handle login input. 
    // But if we want the CLI wizard, we should run it before TUI init?
    // User requested "login command in TUI". So we can skip CLI wizard if TUI is active.
    // BUT legacy wizard is useful.
    // Let's keep wizard ONLY if NOT daemon AND config missing AND user hasn't started TUI yet?
    // Actually, if we launch TUI, we can show a popup "Please Login".
    // So let's skip the CLI wizard if we are going into TUI mode, rely on TUI.
    
    if run_as_daemon && config.username.is_empty() {
         error!("Username not set in config. Please configure it.");
         return Ok(());
    }

    // Run
    let client = AuthClient::new(config.clone())?;

    if run_as_daemon {
        client.run_loop().await;
    } else {
        // TUI Mode
        // Spawn client in background
        tokio::spawn(async move {
            client.run_loop().await;
        });
        
        // Run TUI
        tui::run(config).await?;
    }

    Ok(())
}