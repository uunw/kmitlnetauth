use tui_input::Input;
use tui_input::backend::crossterm::EventHandler;
use kmitlnetauth_core::Config;
use crossterm::event::{KeyCode, KeyEvent};
use anyhow::Result;

#[derive(Debug, PartialEq)]
pub enum AppStatus {
    Online,
    Offline,
    Connecting,
    Paused,
}

pub struct App {
    pub input: Input,
    pub config: Config,
    pub status: AppStatus,
    pub ip_address: String,
    pub last_heartbeat: String,
    pub show_login_popup: bool,
    pub login_input_user: Input,
    pub login_input_pass: Input, // We need to handle password masking in UI
    pub focus_password: bool, // Toggle focus between user/pass in popup
}

impl App {
    pub fn new(config: Config) -> Self {
        Self {
            input: Input::default(),
            ip_address: config.ip_address.clone().unwrap_or_default(),
            config,
            status: AppStatus::Offline,
            last_heartbeat: "-".to_string(),
            show_login_popup: false,
            login_input_user: Input::default(),
            login_input_pass: Input::default(),
            focus_password: false,
        }
    }

    pub async fn handle_input(&mut self, key: KeyEvent) -> Result<bool> {
        // If Popup is open, handle popup input
        if self.show_login_popup {
            match key.code {
                KeyCode::Esc => {
                    self.show_login_popup = false;
                }
                KeyCode::Tab => {
                    self.focus_password = !self.focus_password;
                }
                KeyCode::Enter => {
                    // Submit Login
                    let new_user = self.login_input_user.value().to_string();
                    let new_pass = self.login_input_pass.value().to_string();
                    if !new_user.is_empty() && !new_pass.is_empty() {
                        self.config.username = new_user;
                        self.config.password = Some(new_pass);
                        // Save config? Or just use in memory?
                        // Better save.
                        // For now just close. Background task should notice config change or we send command.
                    }
                    self.show_login_popup = false;
                }
                _ => {
                    if self.focus_password {
                        self.login_input_pass.handle_event(&crossterm::event::Event::Key(key));
                    } else {
                        self.login_input_user.handle_event(&crossterm::event::Event::Key(key));
                    }
                }
            }
            return Ok(false);
        }

        // Main Command Input
        match key.code {
            KeyCode::Enter => {
                let cmd = self.input.value().to_string();
                self.input.reset();
                self.process_command(&cmd).await?;
            }
            KeyCode::Esc => {
                // Ignore or clear?
            }
            _ => {
                self.input.handle_event(&crossterm::event::Event::Key(key));
            }
        }
        Ok(false)
    }

    pub async fn process_command(&mut self, cmd: &str) -> Result<()> {
        let parts: Vec<&str> = cmd.split_whitespace().collect();
        if parts.is_empty() {
            return Ok(());
        }

        match parts[0] {
            "quit" | "exit" => {
                std::process::exit(0);
            }
            "login" => {
                self.show_login_popup = true;
                self.login_input_user = Input::new(self.config.username.clone());
                self.login_input_pass = Input::default(); // Don't pre-fill password for security/simplicity logic
                self.focus_password = false;
            }
            "connect" => {
                // Trigger connect
            }
            "stop" | "pause" => {
                self.status = AppStatus::Paused;
            }
            "start" | "resume" => {
                self.status = AppStatus::Offline; // Let it reconnect
            }
            _ => {}
        }
        Ok(())
    }

    pub fn on_tick(&mut self) {
        // Update timer or animations
    }

    pub async fn update(&mut self) -> Result<()> {
        // Poll background events
        Ok(())
    }
}
