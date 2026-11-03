//! 录制浮条：一块透明的置顶小窗口。
//!
//! 与旧实现（`mini.rs` 把主窗口缩成一条）的区别：那条是不透明矩形，贴在别人窗口旁边
//! 像贴了张纸；这里是一块真正透明的窗口，窗里自己画一个圆角药丸，桌面从圆角外透过去。
//!
//! 透明窗口成立需要三件事同时做到（缺一条就白底/黑底）：
//! 1. 建窗口时 `.transparent(true)` + `.decorations(false)` + `.shadow(false)`；
//! 2. 页面里 `html/body` 背景必须是 `transparent`（见 `src/pill.css`）；
//! 3. 阴影只能用 CSS 画——窗口自带的那圈系统阴影会把透明区涂成灰色。
//!
//! 还要让它"不碍事"：`overlay_style::make_overlay` 给它 `WS_EX_NOACTIVATE`
//! （点它不夺前台，录制时键盘仍打在目标窗口上）并重新插到置顶带最上。

use tauri::{AppHandle, Manager, LogicalPosition, LogicalSize, WebviewUrl, WebviewWindowBuilder};

/// 浮条窗口的尺寸（逻辑像素）。宽度够放"正在录制 + 已录 N 秒 + 提示 + 停止"。
pub const WIDTH: f64 = 372.0;
pub const HEIGHT: f64 = 60.0;

/// 离屏幕底部的距离（逻辑像素）：留出任务栏高度以上的呼吸空间。
const BOTTOM_MARGIN: f64 = 96.0;

/// 显示浮条；已经存在就只把它摆好再抬到最上面。
pub fn show(app: &AppHandle) -> Result<(), String> {
    crate::crashlog::write("pill show：进入");
    if let Some(window) = app.get_webview_window("pill") {
        place(&window);
        let _ = window.show();
        crate::overlay_style::make_overlay(&window);
        return Ok(());
    }

    let window = WebviewWindowBuilder::new(app, "pill", WebviewUrl::App("pill.html".into()))
        .title("KeyMouse 录制中")
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
        .map_err(|e| format!("浮条窗口建不出来：{e}"))?;

    // 直接建成可见的：先建隐藏再 show，在透明窗口上偶发"showed 但 DWM 没合成"，
    // 现象是枚举得到窗口、IsWindowVisible 却是 false。
    crate::crashlog::write("pill show：建好了");
    let _ = window.show();
    crate::crashlog::write("pill show：已 show");
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

/// 收起浮条。用 `close` 而不是 `hide`：透明缺边框的窗口 hide 之后，
/// 下次 show 出来的第一帧常常是未重绘的黑块。
pub fn hide(app: &AppHandle) {
    if let Some(window) = app.get_webview_window("pill") {
        let _ = window.close();
    }
}

/// 摆位置 + 上"不抢焦点/不进 Alt+Tab/插到置顶带最上"的样式位。
fn arrange(window: &tauri::WebviewWindow) {
    crate::overlay_style::make_overlay(window);
    place(window);
}

/// 摆在主屏水平居中、靠下的位置。
///
/// 用 `available position + 可用尺寸`，而不是"写死屏幕中心"：多显示器与任务栏位置
/// 一变，写死的坐标就会把浮条摆到屏幕外面。
fn place(window: &tauri::WebviewWindow) {
    // 刚建出来、还没显示时 current_monitor() 可能是 None，退回主屏。
    let monitor = window
        .current_monitor()
        .ok()
        .flatten()
        .or_else(|| window.primary_monitor().ok().flatten());
    let Some(monitor) = monitor else {
        crate::crashlog::write("pill place：拿不到显示器，摆位跳过");
        return;
    };
    let scale = monitor.scale_factor();
    let origin = monitor.position();
    let area = monitor.size();
    let logical_w = area.width as f64 / scale;
    let logical_h = area.height as f64 / scale;
    let x = origin.x as f64 / scale + (logical_w - WIDTH) / 2.0;
    let y = origin.y as f64 / scale + logical_h - HEIGHT - BOTTOM_MARGIN;
    let _ = window.set_position(LogicalPosition::new(x, y));
    let _ = window.set_size(LogicalSize::new(WIDTH, HEIGHT));
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn pill_leaves_room_for_a_stop_button() {
        assert!(WIDTH >= 320.0 && WIDTH <= 520.0, "太窄放不下提示语，太宽挡视线");
        assert!(HEIGHT >= 48.0 && HEIGHT <= 80.0, "一行字 + 28px 按钮的下限");
    }

    #[test]
    fn pill_does_not_sit_in_the_taskbar() {
        assert!(BOTTOM_MARGIN >= 64.0, "贴底会被任务栏盖住");
    }
}
