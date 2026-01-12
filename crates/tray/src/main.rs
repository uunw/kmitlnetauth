use kmitlnetauth_core::Config;
use std::path::PathBuf;
use tray_icon::{
    menu::{Menu, MenuItem, MenuEvent, PredefinedMenuItem, CheckMenuItem},
    TrayIconBuilder, TrayIcon, TrayIconEvent,
};
use directories::ProjectDirs;
use tracing::error;
use tao::event_loop::{EventLoop, ControlFlow};
use auto_launch::AutoLaunchBuilder;
use std::env;

struct TrayApp {
    config: Config,
    config_path: PathBuf,
    // Keep reference to tray_icon so it isn't dropped
    #[allow(dead_code)]
    tray_icon: TrayIcon,
    // Menu Items
    item_quit: MenuItem,
    item_settings: MenuItem,
    item_auto_start: CheckMenuItem,
    item_auto_login: CheckMenuItem,
}

impl TrayApp {
    fn new() -> Self {
        // Setup Tray Menu
        let tray_menu = Menu::new();
        
        let item_settings = MenuItem::new("Settings (Config File)", true, None);
        let item_auto_login = CheckMenuItem::new("Auto Login", true, true, None);
        let item_auto_start = CheckMenuItem::new("Auto Start", true, false, None); 
        let item_quit = MenuItem::new("Quit", true, None);
        
        tray_menu.append(&item_auto_login).unwrap();
        tray_menu.append(&item_auto_start).unwrap();
        tray_menu.append(&PredefinedMenuItem::separator()).unwrap();
        tray_menu.append(&item_settings).unwrap();
        tray_menu.append(&PredefinedMenuItem::separator()).unwrap();
        tray_menu.append(&item_quit).unwrap();

        // Load icon
        // For production, we should load a real icon file or resource.
        // Creating a simple colored icon for now.
        let icon_rgba = vec![255, 0, 0, 255]; // Red pixel
        let icon = tray_icon::Icon::from_rgba(icon_rgba, 1, 1).unwrap();

        let tray_icon = TrayIconBuilder::new()
            .with_menu(Box::new(tray_menu))
            .with_tooltip("KMITL NetAuth")
            .with_icon(icon)
            .build()
            .unwrap();

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
            .set_use_launch_agent(true) 
            .build()
            .unwrap();
        
        if let Ok(enabled) = auto.is_enabled() {
            item_auto_start.set_checked(enabled);
        }

        Self {
            config,
            config_path,
            tray_icon,
            item_quit,
            item_settings,
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

    fn open_config(&mut self) {
         if !self.config_path.exists() {
             // Create empty if not exists so it can be opened
             if let Err(e) = self.config.save(&self.config_path) {
                 error!("Failed to create config file: {}", e);
             }
         }
         if let Err(e) = open::that(&self.config_path) {
             error!("Failed to open config file: {}", e);
         }
    }

    fn update_config(&mut self) {
        self.config.auto_login = self.item_auto_login.is_checked();
        let _ = self.config.save(&self.config_path);
    }
}

fn main() {
    // Log
    tracing_subscriber::fmt::init();

    let event_loop = EventLoop::new();
    let mut app = TrayApp::new();
    
    // Auto-open config if username is missing (First run)
    if app.config.username.is_empty() {
        app.open_config();
    }

    let menu_channel = MenuEvent::receiver();
    let tray_channel = TrayIconEvent::receiver();

    event_loop.run(move |_event, _, control_flow| {
        *control_flow = ControlFlow::Wait;

        if let Ok(event) = menu_channel.try_recv() {
            if event.id == app.item_quit.id() {
                *control_flow = ControlFlow::Exit;
            } else if event.id == app.item_settings.id() {
                app.open_config();
            } else if event.id == app.item_auto_login.id() {
                app.update_config();
            } else if event.id == app.item_auto_start.id() {
                app.toggle_auto_start(app.item_auto_start.is_checked());
            }
        }
        
        if let Ok(event) = tray_channel.try_recv() {
             // Handle tray click events if needed (e.g. left click to toggle something)
             println!("Tray event: {:?}", event);
        }
    });
}
