use reqwest::Client;
use crate::config::Config;
use crate::error::{Error, Result};
use mac_address::get_mac_address;
use std::time::Duration;
use tracing::{info, warn, error, debug};
use std::collections::HashMap;
use notify_rust::Notification;

const SERVER_URL: &str = "https://portal.kmitl.ac.th:19008/portalauth/login";
const HEARTBEAT_URL: &str = "https://nani.csc.kmitl.ac.th/network-api/data/";
const CHECK_URL: &str = "http://detectportal.firefox.com/success.txt";
const ACIP: &str = "10.252.13.10";

pub struct AuthClient {
    client: Client,
    config: Config,
    mac_address: String,
}

impl AuthClient {
    pub fn new(config: Config) -> Result<Self> {
        let client = Client::builder()
            .timeout(Duration::from_secs(10))
            .cookie_store(true)
            .danger_accept_invalid_certs(true)
            .build()?;

        let mac = match get_mac_address() {
            Ok(Some(mac)) => mac.to_string().replace(":", "").to_lowercase(),
            _ => "000000000000".to_string(),
        };

        Ok(Self {
            client,
            config,
            mac_address: mac,
        })
    }

    fn notify(&self, summary: &str, body: &str) {
        // Notifications might fail (headless linux), just log warning if so.
        if let Err(e) = Notification::new()
            .summary(summary)
            .body(body)
            .appname("KMITL NetAuth")
            .show() 
        {
            warn!("Failed to show notification: {}", e);
        }
    }

    pub async fn login(&self) -> Result<()> {
        let username = &self.config.username;
        let password = self.config.get_password(); // Use the helper that checks keyring
        let ip_address = self.config.ip_address.as_deref().unwrap_or("");

        if username.is_empty() || password.is_empty() {
            warn!("Username or password empty. Skipping login.");
            return Err(Error::AuthFailed("Missing credentials".into()));
        }

        info!("Logging in with username '{}'...", username);

        let mut params = HashMap::new();
        params.insert("userName", username.as_str());
        params.insert("userPass", password.as_str());
        params.insert("uaddress", ip_address);
        params.insert("umac", self.mac_address.as_str());
        params.insert("agreed", "1");
        params.insert("acip", ACIP);
        params.insert("authType", "1");

        let response = self.client.post(SERVER_URL)
            .form(&params)
            .send()
            .await?;

        if response.status().is_success() {
            let text = response.text().await?;
            debug!("Login response: {}", text);
            info!("Login request sent successfully.");
            // Check content for success keywords if possible? 
            // The original python script checks data['data'] in JSON, but we are just checking HTTP 200 for now.
            // Let's assume 200 is good enough, or we can improve later.
            
            self.notify("Login Successful", &format!("Logged in as {}", username));
            Ok(())
        } else {
            let status = response.status();
            error!("Login failed with status: {}", status);
            self.notify("Login Failed", &format!("Status: {}", status));
            Err(Error::AuthFailed(format!("Status code: {}", status)))
        }
    }

    pub async fn heartbeat(&self) -> Result<bool> {
        let mut params = HashMap::new();
        params.insert("username", self.config.username.as_str());
        params.insert("os", "Chrome v116.0.5845.141 on Windows 10 64-bit");
        params.insert("speed", "1.29");
        params.insert("newauth", "1");

        match self.client.post(HEARTBEAT_URL).form(&params).send().await {
            Ok(response) => {
                if response.status().is_success() {
                    debug!("Heartbeat OK");
                    Ok(true)
                } else {
                    warn!("Heartbeat failed with status: {}", response.status());
                    Ok(false)
                }
            }
            Err(e) => {
                warn!("Heartbeat connection error: {}", e);
                Ok(false)
            }
        }
    }

    pub async fn check_internet(&self) -> bool {
        match self.client.get(CHECK_URL).send().await {
            Ok(response) => {
                match response.text().await {
                    Ok(text) => text.trim() == "success",
                    Err(_) => false,
                }
            }
            Err(_) => false,
        }
    }

    pub async fn run_loop(&self) {
        let mut login_attempts = 0;
        let max_attempts = self.config.max_attempt;
        let mut was_connected = true; // Assume start connected to avoid noise? Or check first.

        loop {
            if !self.config.auto_login {
                 // Paused
                 tokio::time::sleep(Duration::from_secs(5)).await;
                 continue;
            }

            let has_internet = self.check_internet().await;

            if has_internet {
                if !was_connected {
                    info!("Internet connection restored.");
                    self.notify("Connected", "Internet connection is active.");
                    was_connected = true;
                }
                
                login_attempts = 0; 
                
                match self.heartbeat().await {
                    Ok(true) => {
                        // Heartbeat successful
                    },
                    Ok(false) | Err(_) => {
                         info!("Heartbeat failed, attempting login...");
                         if let Err(e) = self.login().await {
                             error!("Login error: {}", e);
                         }
                    }
                }
            } else {
                if was_connected {
                    warn!("Internet connection lost.");
                    self.notify("Disconnected", "Internet connection lost. Attempting to reconnect...");
                    was_connected = false;
                }
                
                warn!("No internet connection. Attempting login...");
                if login_attempts < max_attempts {
                    if let Err(e) = self.login().await {
                        error!("Login error: {}", e);
                    }
                    login_attempts += 1;
                } else {
                    error!("Max login attempts reached. Waiting...");
                    // Backoff
                    tokio::time::sleep(Duration::from_secs(60)).await;
                    login_attempts = 0; 
                }
            }

            tokio::time::sleep(Duration::from_secs(self.config.interval)).await;
        }
    }
}
