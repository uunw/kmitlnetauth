use ratatui::{
    layout::{Constraint, Direction, Layout, Rect},
    style::{Color, Modifier, Style},
    text::{Line, Span},
    widgets::{Block, Borders, Paragraph, Clear},
    Frame,
};
use tui_logger::TuiLoggerWidget;
use crate::tui::app::{App, AppStatus};

pub fn draw(f: &mut Frame, app: &mut App) {
    let chunks = Layout::default()
        .direction(Direction::Vertical)
        .constraints([
            Constraint::Length(3), // Header
            Constraint::Min(1),    // Main Body
            Constraint::Length(3), // Command Bar
        ])
        .split(f.area());

    draw_header(f, app, chunks[0]);
    draw_body(f, app, chunks[1]);
    draw_command_bar(f, app, chunks[2]);

    if app.show_login_popup {
        draw_login_popup(f, app);
    }
}

fn draw_header(f: &mut Frame, _app: &mut App, area: Rect) {
    let text = vec![
        Line::from(vec![
            Span::styled("KMITL NetAuth TUI", Style::default().add_modifier(Modifier::BOLD)),
            Span::raw(" | "),
            Span::raw("v1.0.0"),
        ]),
    ];
    let block = Paragraph::new(text)
        .block(Block::default().borders(Borders::ALL).title("Status"));
    f.render_widget(block, area);
}

fn draw_body(f: &mut Frame, app: &mut App, area: Rect) {
    let chunks = Layout::default()
        .direction(Direction::Horizontal)
        .constraints([
            Constraint::Percentage(30), // Status
            Constraint::Percentage(70), // Logs
        ])
        .split(area);

    // Status Panel
    let status_color = match app.status {
        AppStatus::Online => Color::Green,
        AppStatus::Offline => Color::Red,
        AppStatus::Connecting => Color::Yellow,
        AppStatus::Paused => Color::Gray,
    };

    let status_text = vec![
        Line::from(vec![Span::raw("Status: "), Span::styled(format!("{:?}", app.status), Style::default().fg(status_color))]),
        Line::from(format!("Username: {}", app.config.username)),
        Line::from(format!("IP: {}", app.ip_address)),
        Line::from(format!("Interval: {}s", app.config.interval)),
        Line::from(format!("Last Heartbeat: {}", app.last_heartbeat)),
    ];

    let status_block = Paragraph::new(status_text)
        .block(Block::default().borders(Borders::ALL).title("Info"));
    f.render_widget(status_block, chunks[0]);

    // Log Panel (using tui-logger)
    let logger = TuiLoggerWidget::default()
        .block(Block::default().title("Logs").borders(Borders::ALL))
        .style_error(Style::default().fg(Color::Red))
        .style_warn(Style::default().fg(Color::Yellow))
        .style_info(Style::default().fg(Color::Green))
        .style_debug(Style::default().fg(Color::Cyan))
        .style_trace(Style::default().fg(Color::Magenta));
    f.render_widget(logger, chunks[1]);
}

fn draw_command_bar(f: &mut Frame, app: &mut App, area: Rect) {
    let input_text = app.input.value();
    let command_bar = Paragraph::new(format!("> {}", input_text))
        .block(Block::default().borders(Borders::ALL).title("Command"));
    f.render_widget(command_bar, area);
    
    // Set cursor
    f.set_cursor_position(
        (area.x + 2 + app.input.cursor() as u16,
        area.y + 1)
    );
}

fn draw_login_popup(f: &mut Frame, app: &mut App) {
    let block = Block::default().title("Login").borders(Borders::ALL);
    let area = centered_rect(60, 20, f.area());
    f.render_widget(Clear, area); // Clear background
    f.render_widget(block, area);

    let chunks = Layout::default()
        .direction(Direction::Vertical)
        .margin(1)
        .constraints([
            Constraint::Length(3), // Username
            Constraint::Length(3), // Password
            Constraint::Length(1), // Help
        ])
        .split(area);

    let user_style = if !app.focus_password { Style::default().fg(Color::Yellow) } else { Style::default() };
    let pass_style = if app.focus_password { Style::default().fg(Color::Yellow) } else { Style::default() };

    let user_input = Paragraph::new(app.login_input_user.value())
        .block(Block::default().borders(Borders::ALL).title("Username").style(user_style));
    f.render_widget(user_input, chunks[0]);

    let pass_stars: String = "*".repeat(app.login_input_pass.value().len());
    let pass_input = Paragraph::new(pass_stars)
        .block(Block::default().borders(Borders::ALL).title("Password").style(pass_style));
    f.render_widget(pass_input, chunks[1]);
    
    let help = Paragraph::new("Tab: Switch | Enter: Login | Esc: Cancel");
    f.render_widget(help, chunks[2]);
}

fn centered_rect(percent_x: u16, percent_y: u16, r: Rect) -> Rect {
    let popup_layout = Layout::default()
        .direction(Direction::Vertical)
        .constraints([
            Constraint::Percentage((100 - percent_y) / 2),
            Constraint::Percentage(percent_y),
            Constraint::Percentage((100 - percent_y) / 2),
        ])
        .split(r);

    Layout::default()
        .direction(Direction::Horizontal)
        .constraints([
            Constraint::Percentage((100 - percent_x) / 2),
            Constraint::Percentage(percent_x),
            Constraint::Percentage((100 - percent_x) / 2),
        ])
        .split(popup_layout[1])[1]
}