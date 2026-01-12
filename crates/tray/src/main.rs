use kmitlnetauth_core::Config;
use std::path::PathBuf;
use tray_icon::{
    menu::{Menu, MenuItem, MenuEvent, PredefinedMenuItem, CheckMenuItem, Submenu},
    TrayIconBuilder, TrayIcon, TrayIconEvent,
};
use directories::ProjectDirs;
use tracing::error;
use tao::event_loop::{EventLoop, ControlFlow};
use auto_launch::AutoLaunchBuilder;
use std::env;

#[cfg(target_os = "windows")]
mod win_console {
    use windows::Win32::System::Console::{AllocConsole, GetConsoleWindow};
    use windows::Win32::UI::WindowsAndMessaging::{ShowWindow, SW_HIDE, SW_SHOW};

    pub fn show() {
        unsafe {
            let hwnd = GetConsoleWindow();
            if hwnd.0 == std::ptr::null_mut() {
                let _ = AllocConsole();
            } else {
                let _ = ShowWindow(hwnd, SW_SHOW);
            }
        }
    }
    pub fn hide() {
        unsafe {
             let hwnd = GetConsoleWindow();
             if hwnd.0 != std::ptr::null_mut() {
                 let _ = ShowWindow(hwnd, SW_HIDE);
             }
        }
    }
}

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
    item_show_console: CheckMenuItem,
    // Log Level Items
    item_log_error: CheckMenuItem,
    item_log_warn: CheckMenuItem,
    item_log_info: CheckMenuItem,
    item_log_debug: CheckMenuItem,
    item_log_trace: CheckMenuItem,
}

impl TrayApp {
    fn new() -> Self {
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

        // Setup Tray Menu
        let tray_menu = Menu::new();
        
        let item_settings = MenuItem::new("Settings (Config File)", true, None);
        let item_auto_login = CheckMenuItem::new("Auto Login", true, config.auto_login, None);
        let item_auto_start = CheckMenuItem::new("Auto Start", true, false, None); 
        let item_show_console = CheckMenuItem::new("Show Terminal", true, false, None);

        // Log Levels
        let log_submenu = Submenu::new("Log Level", true);
        let item_log_error = CheckMenuItem::new("Error", true, config.log_level.eq_ignore_ascii_case("error"), None);
        let item_log_warn = CheckMenuItem::new("Warn", true, config.log_level.eq_ignore_ascii_case("warn"), None);
        let item_log_info = CheckMenuItem::new("Info", true, config.log_level.eq_ignore_ascii_case("info"), None);
        let item_log_debug = CheckMenuItem::new("Debug", true, config.log_level.eq_ignore_ascii_case("debug"), None);
        let item_log_trace = CheckMenuItem::new("Trace", true, config.log_level.eq_ignore_ascii_case("trace"), None);
        
        let _ = log_submenu.append(&item_log_error);
        let _ = log_submenu.append(&item_log_warn);
        let _ = log_submenu.append(&item_log_info);
        let _ = log_submenu.append(&item_log_debug);
        let _ = log_submenu.append(&item_log_trace);

        let item_quit = MenuItem::new("Quit", true, None);
        
        let _ = tray_menu.append(&item_auto_login);
        let _ = tray_menu.append(&item_auto_start);
        #[cfg(target_os = "windows")]
        let _ = tray_menu.append(&item_show_console);
        
        let _ = tray_menu.append(&PredefinedMenuItem::separator());
        let _ = tray_menu.append(&item_settings);
        let _ = tray_menu.append(&log_submenu);
        let _ = tray_menu.append(&PredefinedMenuItem::separator());
        let _ = tray_menu.append(&item_quit);

        // Load icon
        let icon_rgba = vec![255, 0, 0, 255]; // Red pixel
        let icon = tray_icon::Icon::from_rgba(icon_rgba, 1, 1).unwrap();

        let tray_icon = TrayIconBuilder::new()
            .with_menu(Box::new(tray_menu))
            .with_tooltip("KMITL NetAuth")
            .with_icon(icon)
            .build()
            .unwrap();
        
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
            item_show_console,
            item_log_error,
            item_log_warn,
            item_log_info,
            item_log_debug,
            item_log_trace,
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

    fn set_log_level(&mut self, level: &str) {
        self.config.log_level = level.to_string();
        let _ = self.config.save(&self.config_path);
        
        // Update UI checks
        self.item_log_error.set_checked(level.eq_ignore_ascii_case("error"));
        self.item_log_warn.set_checked(level.eq_ignore_ascii_case("warn"));
        self.item_log_info.set_checked(level.eq_ignore_ascii_case("info"));
        self.item_log_debug.set_checked(level.eq_ignore_ascii_case("debug"));
        self.item_log_trace.set_checked(level.eq_ignore_ascii_case("trace"));
    }
    
    fn toggle_console(&mut self, show: bool) {
        #[cfg(target_os = "windows")]
        if show {
            win_console::show();
        } else {
            win_console::hide();
        }
        #[cfg(not(target_os = "windows"))]
        {
            // On Linux/Mac, if started from terminal, it stays there. 
            // If started as background/daemon, usually can't show terminal easily without spawning one.
            // No-op for now.
            let _ = show;
        }
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
            } else if event.id == app.item_show_console.id() {
                app.toggle_console(app.item_show_console.is_checked());
            } 
            // Log Levels
            else if event.id == app.item_log_error.id() { app.set_log_level("error"); }
            else if event.id == app.item_log_warn.id() { app.set_log_level("warn"); }
            else if event.id == app.item_log_info.id() { app.set_log_level("info"); }
            else if event.id == app.item_log_debug.id() { app.set_log_level("debug"); }
            else if event.id == app.item_log_trace.id() { app.set_log_level("trace"); }
        }
        
        if let Ok(_) = tray_channel.try_recv() {
             // Removed logging
        }
    });
}