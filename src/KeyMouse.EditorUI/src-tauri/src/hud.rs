//! 运行日志 HUD：一块透明的置顶小窗，显示最近几步在做什么。
//!
//! 它取代了旧的录制浮条。旧浮条上有暂停/停止按钮，但播放时脚本会接管鼠标，用户
//! 往往根本点不到那颗按钮；而且窗口藏在屏幕角落，也没人愿意盯着计时器。现在窗口
//! 只读、鼠标穿透、没有按钮，停止统一交给 `Ctrl+Alt+Q`（见 `hotkey.rs`）。
//!
//! 透明窗口成立需要三件事同时做到（缺一条就白底/黑底）：
//! 1. 建窗口时 `.transparent(true)` + `.decorations(false)` + `.shadow(false)`；
//! 2. 页面里 `html/body` 背景必须是 `transparent`（见 `src/hud.css`）；
//! 3. 阴影只能用 CSS 画——窗口自带的那圈系统阴影会把透明区涂成灰色。
//!
//! 位置固定在当前显示器的**左上角**：不挡任务栏，也尽量不压住目标窗口的操作区。
//! 鼠标穿透由 `WS_EX_TRANSPARENT` 与 `set_ignore_cursor_events(true)` 双保险；
//! 它不抢焦点、不进 Alt+Tab，用户甚至不需要知道它在那里。
//!
//! 另外用 `WDA_EXCLUDEFROMCAPTURE` 把它从屏幕抓取里排除：HUD 仍显示给用户，但 OCR /
//! 找字 / 截图不会把日志文字读成目标内容。这个标记要 Win10 2004+，旧系统会忽略。

use std::collections::VecDeque;
use std::sync::Mutex;
use tauri::{AppHandle, LogicalPosition, LogicalSize, Manager, WebviewUrl, WebviewWindowBuilder};

/// HUD 窗口尺寸（逻辑像素）：7 行 11px 日志 + 一行小标题。
pub const WIDTH: f64 = 468.0;
pub const HEIGHT: f64 = 210.0;

/// 离屏幕左上角的距离（逻辑像素）：给圆角/阴影留呼吸空间。
const LEFT_MARGIN: f64 = 16.0;
const TOP_MARGIN: f64 = 16.0;

/// 最近一次运行的原始行。
///
/// HUD 是第二个 WebView，页面加载完成的时间不由命令行决定；第一条“执行中”往往
/// 比它先到。光靠事件会丢掉开头，所以这里留一份环形缓冲，页面一挂载就先拉一次。
/// 直接跑 CLI 不经过这个状态，不占内存也不影响命令行。
#[derive(Default)]
pub struct HudLog(Mutex<VecDeque<String>>);

impl HudLog {
    const CAPACITY: usize = 240;

    pub fn clear(&self) {
        self.0.lock().unwrap().clear();
    }

    pub fn push(&self, line: &str) {
        let mut lines = self.0.lock().unwrap();
        lines.push_back(line.to_string());
        while lines.len() > Self::CAPACITY {
            lines.pop_front();
        }
    }

    pub fn snapshot(&self) -> Vec<String> {
        self.0.lock().unwrap().iter().cloned().collect()
    }
}

/// 显示运行日志。录制与播放共用一块；已经存在就只摆好再抬到最上面。
pub fn show(app: &AppHandle) -> Result<(), String> {
    if let Some(window) = app.get_webview_window("hud") {
        arrange(&window);
        let _ = window.show();
        return Ok(());
    }

    let window = WebviewWindowBuilder::new(app, "hud", WebviewUrl::App("hud.html".into()))
        .title("KeyMouse 运行日志")
        .inner_size(WIDTH, HEIGHT)
        .resizable(false)
        .maximizable(false)
        .minimizable(false)
        .decorations(false)
        .transparent(true)
        .shadow(false)
        .always_on_top(true)
        .skip_taskbar(true)
        .focused(false)
        .build()
        .map_err(|e| format!("日志窗口建不出来：{e}"))?;

    // 直接建成可见的：先建隐藏再 show，在透明窗口上偶发"showed 但 DWM 没合成"，
    // 现象是枚举得到窗口、IsWindowVisible 却是 false。
    let _ = window.show();
    arrange(&window);

    // 建窗口这一帧里 `hwnd()` 偶尔还拿不到真句柄、显示器也还没绑好，
    // 于是摆位与样式位全落空；过一会儿再补一次，代价可以忽略。
    let late = window.clone();
    std::thread::spawn(move || {
        std::thread::sleep(std::time::Duration::from_millis(260));
        arrange(&late);
    });
    Ok(())
}

/// 收起 HUD。用 `close` 而不是 `hide`：透明缺边框的窗口 hide 之后，
/// 下次 show 出来的第一帧常常是未重绘的黑块。
pub fn hide(app: &AppHandle) {
    if let Some(window) = app.get_webview_window("hud") {
        let _ = window.close();
    }
}

/// 摆位置、设尺寸、上"不抢焦点/鼠标穿透/不进 Alt+Tab"的样式位。
fn arrange(window: &tauri::WebviewWindow) {
    crate::overlay_style::make_overlay(window);
    let _ = window.set_ignore_cursor_events(true);
    place(window);
}

/// 计算 HUD 在当前显示器上的逻辑坐标：左上角留一点边距。
///
/// 用 `display origin + display size` 而不是主屏绝对坐标：多显示器与缩放一变，
/// 写死的点就会把窗口摆到屏幕外。`scale` 是物理像素到逻辑像素的倍率。
fn hud_position(
    origin_x: i32,
    origin_y: i32,
    monitor_width: u32,
    monitor_height: u32,
    scale: f64,
) -> (f64, f64) {
    let logical_width = monitor_width as f64 / scale;
    let logical_height = monitor_height as f64 / scale;
    let left = origin_x as f64 / scale;
    let top = origin_y as f64 / scale;
    // 显示器比 HUD 还小（极少见）时优先保住左上角，不让窗口跑到屏幕外。
    let x = (left + LEFT_MARGIN).min(left + logical_width - WIDTH - LEFT_MARGIN);
    let y = (top + TOP_MARGIN).min(top + logical_height - HEIGHT - TOP_MARGIN);
    (x.max(left), y.max(top))
}

/// 摆在当前显示器左上角。
fn place(window: &tauri::WebviewWindow) {
    // 刚建出来、还没显示时 current_monitor() 可能是 None，退回主屏。
    let monitor = window
        .current_monitor()
        .ok()
        .flatten()
        .or_else(|| window.primary_monitor().ok().flatten());
    let Some(monitor) = monitor else {
        crate::crashlog::write("hud place：拿不到显示器，摆位跳过");
        return;
    };
    let scale = monitor.scale_factor();
    let origin = monitor.position();
    let area = monitor.size();
    let (x, y) = hud_position(origin.x, origin.y, area.width, area.height, scale);
    let _ = window.set_size(LogicalSize::new(WIDTH, HEIGHT));
    let _ = window.set_position(LogicalPosition::new(x, y));
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn hud_is_small_enough_to_ignore() {
        // 7 行日志 + 标题，宽到能放下“调用另一个流程”这类长步骤名，又不该横跨半个屏幕。
        assert!((420.0..=560.0).contains(&WIDTH), "太窄会截断步骤名，太宽会挡视线");
        assert!((170.0..=260.0).contains(&HEIGHT), "7 行 11px 日志加标题，再高就是一块板");
    }

    #[test]
    fn hud_sits_at_the_top_left_of_the_monitor() {
        let (x, y) = hud_position(0, 0, 1920, 1080, 1.0);
        assert!((x - LEFT_MARGIN).abs() < 0.01, "x={x}");
        assert!((y - TOP_MARGIN).abs() < 0.01, "y={y}");
    }

    #[test]
    fn hud_log_keeps_only_the_latest_lines() {
        let log = HudLog::default();
        for index in 0..(HudLog::CAPACITY + 12) {
            log.push(&format!("line {index}"));
        }
        let snapshot = log.snapshot();
        assert_eq!(snapshot.len(), HudLog::CAPACITY);
        assert_eq!(snapshot.first().unwrap(), "line 12");
        assert_eq!(snapshot.last().unwrap(), &format!("line {}", HudLog::CAPACITY + 11));
        log.clear();
        assert!(log.snapshot().is_empty());
    }

    #[test]
    fn hud_position_respects_monitor_origin_and_scale() {
        // 第二块 4K 屏在主屏左侧：-3840,0，200% 缩放 → 逻辑 1920×1080。
        let (x, y) = hud_position(-3840, 0, 3840, 2160, 2.0);
        assert!((x - (-1920.0 + LEFT_MARGIN)).abs() < 0.01, "x={x}");
        assert!((y - TOP_MARGIN).abs() < 0.01, "y={y}");
    }
}
