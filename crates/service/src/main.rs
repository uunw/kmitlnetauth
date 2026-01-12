use clap::Parser;
use kmitlnetauth_core::{AuthClient, Config};
use std::path::PathBuf;
use tracing::{info, error, warn};
use directories::ProjectDirs;
use tracing_appender::rolling::{RollingFileAppender, Rotation};
use std::io::{Write, IsTerminal};
use dialoguer::{Input, Password, Confirm};

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
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    // Setup logging with rotation
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
        .init();

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

    info!("Starting KMITL NetAuth Service");
    info!("Using config file: {:?}", config_path);

    let mut config = match Config::load(&config_path) {
        Ok(cfg) => cfg,
        Err(e) => {
            error!("Failed to load config: {}", e);
            Config::default()
        }
    };
    
    // Check if username is set
    if config.username.is_empty() {
        if std::io::stdin().is_terminal() {
            println!("Configuration missing or incomplete.");
            println!("Running interactive setup...");
            
            let username: String = Input::new()
                .with_prompt("KMITL Username (Student ID)")
                .interact_text()
                .unwrap();
                
            let password = Password::new()
                .with_prompt("Password")
                .interact()
                .unwrap();

            let ip_address: String = Input::new()
                .with_prompt("Your IP Address")
                .interact_text()
                .unwrap();
                
            let auto_login = Confirm::new()
                .with_prompt("Enable Auto Login?")
                .default(true)
                .interact()
                .unwrap();

            config.username = username;
            config.password = Some(password);
            config.ip_address = Some(ip_address);
            config.auto_login = auto_login;

            match config.save(&config_path) {
                Ok(_) => info!("Configuration saved to {:?}", config_path),
                Err(e) => error!("Failed to save configuration: {}", e),
            }
        } else {
            error!("Username not set in config and not running interactively. Please configure it via file or environment variables.");
            return Ok(());
        }
    }

    let client = AuthClient::new(config)?;
    client.run_loop().await;

    Ok(())
}