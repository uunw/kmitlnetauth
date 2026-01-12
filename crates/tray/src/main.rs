use eframe::egui;
use kmitlnetauth_core::{Config, CredentialManager};
use std::path::PathBuf;
use tray_icon::{
    menu::{Menu, MenuItem, MenuEvent, PredefinedMenuItem, CheckMenuItem},
    TrayIconBuilder, TrayIcon,
};
use directories::ProjectDirs;
use tracing::{info, error};
use crossbeam_channel::Receiver;
use auto_launch::AutoLaunchBuilder;
use std::env;

struct TrayApp {
    config: Config,
    config_path: PathBuf,
    // Keep reference to tray_icon so it isn't dropped
    #[allow(dead_code)]
    tray_icon: Option<TrayIcon>,
    show_window: bool,
    menu_channel: Receiver<MenuEvent>,
    // Menu Items
    item_quit: MenuItem,
    item_settings: MenuItem,
    item_open_config: MenuItem,
    item_auto_start: CheckMenuItem,
    item_auto_login: CheckMenuItem,
}

impl TrayApp {
    fn new(_cc: &eframe::CreationContext) -> Self {
        // Setup Tray Menu
        let tray_menu = Menu::new();
        
        let item_settings = MenuItem::new("Settings (UI)", true, None);
        let item_open_config = MenuItem::new("Open Config File", true, None);
        let item_auto_login = CheckMenuItem::new("Auto Login", true, true, None);
        let item_auto_start = CheckMenuItem::new("Auto Start", true, false, None); // Default false, will check later
        let item_quit = MenuItem::new("Quit", true, None);
        
        tray_menu.append(&item_auto_login).unwrap();
        tray_menu.append(&item_auto_start).unwrap();
        tray_menu.append(&PredefinedMenuItem::separator()).unwrap();
        tray_menu.append(&item_settings).unwrap();
        tray_menu.append(&item_open_config).unwrap();
        tray_menu.append(&PredefinedMenuItem::separator()).unwrap();
        tray_menu.append(&item_quit).unwrap();

        // Load icon (placeholder)
        let icon_rgba = vec![255, 0, 0, 255]; // Red pixel
        let icon = tray_icon::Icon::from_rgba(icon_rgba, 1, 1).unwrap();

        let tray_icon = TrayIconBuilder::new()
            .with_menu(Box::new(tray_menu))
            .with_tooltip("KMITL NetAuth")
            .with_icon(icon)
            .build()
            .unwrap();

        let menu_channel = MenuEvent::receiver();

        // Config path
        let config_path = if cfg!(target_os = "linux") {
             match ProjectDirs::from("com", "kmitl", "netauth") {
                Some(proj_dirs) => proj_dirs.config_dir().join("config.yaml"),
                None => PathBuf::from("config.yaml"),
            }
        } else {
             match ProjectDirs::from("com", "kmitl", "netauth") {
                Some(proj_dirs) => proj_dirs.config_dir().join("config.yaml"),
                None => PathBuf::from("config.yaml"),
            }
        };

        let config = Config::load(&config_path).unwrap_or_default();
        
        // Sync menu state
        item_auto_login.set_checked(config.auto_login);

        // Check Auto Start state
        let auto = AutoLaunchBuilder::new()
            .set_app_name("KMITL NetAuth")
            .set_app_path(env::current_exe().unwrap_or_default().to_str().unwrap())
            .set_use_launch_agent(true) // For mac, though we skip it, standard practice
            .build()
            .unwrap();
        
        if let Ok(enabled) = auto.is_enabled() {
            item_auto_start.set_checked(enabled);
        }

        Self {
            config,
            config_path,
            tray_icon: Some(tray_icon),
            show_window: false, // Start hidden
            menu_channel: menu_channel.clone(),
            item_quit,
            item_settings,
            item_open_config,
            item_auto_start,
            item_auto_login,
        }
    }
    
    fn toggle_auto_start(&self, enable: bool) {
        let current_exe = env::current_exe().unwrap_or_default();
        let auto = AutoLaunchBuilder::new()
            .set_app_name("KMITL NetAuth")
            .set_app_path(current_exe.to_str().unwrap())
            .set_use_launch_agent(true)
            .build()
            .unwrap();
            
        if enable {
            if let Err(e) = auto.enable() {
                error!("Failed to enable auto start: {}", e);
            }
        } else {
            if let Err(e) = auto.disable() {
                error!("Failed to disable auto start: {}", e);
            }
        }
    }
}

impl eframe::App for TrayApp {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        // Handle Tray Events
        while let Ok(event) = self.menu_channel.try_recv() {
             // Check against our items
             if event.id == self.item_quit.id() {
                 std::process::exit(0);
             } else if event.id == self.item_settings.id() {
                 self.show_window = true;
             } else if event.id == self.item_open_config.id() {
                 if !self.config_path.exists() {
                     // Create empty if not exists so it can be opened
                     if let Err(e) = self.config.save(&self.config_path) {
                         error!("Failed to create config file: {}", e);
                     }
                 }
                 if let Err(e) = open::that(&self.config_path) {
                     error!("Failed to open config file: {}", e);
                 }
             } else if event.id == self.item_auto_login.id() {
                 self.config.auto_login = self.item_auto_login.is_checked();
                 let _ = self.config.save(&self.config_path);
             } else if event.id == self.item_auto_start.id() {
                 self.toggle_auto_start(self.item_auto_start.is_checked());
             }
        }

        if self.show_window {
            egui::Window::new("KMITL NetAuth Settings")
                .open(&mut self.show_window)
                .show(ctx, |ui| {
                    ui.heading("Configuration");
                    
                    ui.horizontal(|ui| {
                        ui.label("Username:");
                        ui.text_edit_singleline(&mut self.config.username);
                    });

                    ui.horizontal(|ui| {
                        ui.label("Password:");
                        // Helper to handle password.
                        // We show placeholders if stored in keyring?
                        // Or just empty and let user type to overwrite.
                        let mut password = self.config.password.clone().unwrap_or_default();
                        if password.is_empty() && !self.config.username.is_empty() {
                            // Try fetch from keyring to display?
                            // Security risk? Maybe just "***" if exists.
                            if let Ok(_) = CredentialManager::get_password(&self.config.username) {
                                // Indicate password exists but don't show it?
                                // ui.label("(Stored in Keyring)");
                                // But user might want to change it.
                            }
                        }
                        
                        if ui.add(egui::TextEdit::singleline(&mut password).password(true)).changed() {
                            self.config.password = Some(password);
                        }
                    });

                    ui.horizontal(|ui| {
                        ui.label("Interval (sec):");
                        ui.add(egui::DragValue::new(&mut self.config.interval));
                    });
                    
                    ui.checkbox(&mut self.config.auto_login, "Auto Login");

                    if ui.button("Save").clicked() {
                        if let Err(e) = self.config.save(&self.config_path) {
                            error!("Failed to save config: {}", e);
                        } else {
                            info!("Config saved to {:?}", self.config_path);
                            // Update tray check state
                            self.item_auto_login.set_checked(self.config.auto_login);
                        }
                    }
                });
        }
    }
}

fn main() -> eframe::Result<()> {
    // Log
    tracing_subscriber::fmt::init();

    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default().with_inner_size([320.0, 240.0]),
        ..Default::default()
    };
    
    eframe::run_native(
        "KMITL NetAuth",
        options,
        Box::new(|cc| Ok(Box::new(TrayApp::new(cc)))),
    )
}