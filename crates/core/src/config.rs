use serde::{Deserialize, Serialize};
use std::path::PathBuf;
use crate::error::{Error, Result};
use std::fs;
use crate::credentials::CredentialManager;
use tracing::warn;
use std::env;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Config {
    pub username: String,
    // Password is now optional in config file. If present, it will be migrated to keyring on load (if possible)
    // or used as fallback.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub password: Option<String>, 
    #[serde(skip_serializing_if = "Option::is_none")]
    pub ip_address: Option<String>,
    pub interval: u64,
    pub max_attempt: u32,
    pub auto_login: bool,
}

impl Default for Config {
    fn default() -> Self {
        Self {
            username: "".to_string(),
            password: None,
            ip_address: None,
            interval: 300,
            max_attempt: 20,
            auto_login: true,
        }
    }
}

impl Config {
    pub fn load(path: &PathBuf) -> Result<Self> {
        let mut config = if path.exists() {
            let content = fs::read_to_string(path)?;
            serde_yaml::from_str(&content)
                .map_err(|e| Error::Config(format!("Failed to parse YAML config: {}", e)))?
        } else {
            Self::default()
        };

        // Override with Environment Variables
        if let Ok(val) = env::var("KMITL_USERNAME") {
            config.username = val;
        }
        if let Ok(val) = env::var("KMITL_PASSWORD") {
            config.password = Some(val);
        }
        if let Ok(val) = env::var("KMITL_IP") {
            config.ip_address = Some(val);
        }
        if let Ok(val) = env::var("KMITL_INTERVAL") {
            if let Ok(parsed) = val.parse() {
                config.interval = parsed;
            }
        }
        if let Ok(val) = env::var("KMITL_MAX_ATTEMPT") {
            if let Ok(parsed) = val.parse() {
                config.max_attempt = parsed;
            }
        }
        if let Ok(val) = env::var("KMITL_AUTO_LOGIN") {
            if let Ok(parsed) = val.parse() {
                config.auto_login = parsed;
            }
        }

        // Migration: If password exists in config (from file or env), try to move it to Keyring
        // Note: For Docker/Env usage, we might NOT want to use keyring if it's not available (headless).
        // But the logic below attempts it and warns on failure, which is fine.
        if let Some(pwd) = &config.password {
            if !pwd.is_empty() && !config.username.is_empty() {
                if let Err(e) = CredentialManager::set_password(&config.username, pwd) {
                    warn!("Failed to migrate password to keyring: {:?}", e);
                    // In docker environment without keyring service, this will fail and simply warn, 
                    // which is acceptable. The password remains in `config.password` struct in memory
                    // and will be used by `get_password` fallback.
                } else {
                    // If successful, we could clear it, but for Env var case, we don't clear the env var.
                    // We just leave it in the struct.
                }
            }
        }

        Ok(config)
    }

    pub fn save(&self, path: &PathBuf) -> Result<()> {
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent)?;
        }
        // When saving, we prefer NOT to save the password in plain text if possible.
        // Create a copy without password if we are using keyring.
        let mut config_to_save = self.clone();
        
        // If we successfully saved to keyring, we can remove it from file.
        // But we need to know if we are using keyring. 
        // Logic: Always try to save password to keyring. If successful, clear from struct.
        if let Some(pwd) = &self.password {
             if !pwd.is_empty() && !self.username.is_empty() {
                 match CredentialManager::set_password(&self.username, pwd) {
                     Ok(_) => {
                         config_to_save.password = None; // Don't write to file
                     },
                     Err(e) => {
                         warn!("Could not save password to keyring, falling back to file: {:?}", e);
                     }
                 }
             }
        }

        let content = serde_yaml::to_string(&config_to_save)
            .map_err(|e| Error::Config(format!("Failed to serialize config to YAML: {}", e)))?;
            
        fs::write(path, content)?;
        Ok(())
    }

    pub fn get_password(&self) -> String {
        // 1. Try config file (legacy/fallback)
        if let Some(pwd) = &self.password {
            if !pwd.is_empty() {
                return pwd.clone();
            }
        }
        // 2. Try keyring
        if !self.username.is_empty() {
            if let Ok(pwd) = CredentialManager::get_password(&self.username) {
                return pwd;
            }
        }
        "".to_string()
    }
}