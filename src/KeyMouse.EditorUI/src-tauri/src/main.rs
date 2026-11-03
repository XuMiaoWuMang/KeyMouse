#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

//! 新编辑器外壳（Tauri）的入口。
//!
//! 它只做界面做不到的事：开窗口、文件对话框、读写文件、起 `KeyMouse.exe`。
//! 业务判断一律问命令行，不在这里重写一遍——那份逻辑在
//! `src\KeyMouse.Core`，两边各写一份迟早会分叉。
//!
//! 契约见 `docs/reference/editor-ipc.md`。

mod crashlog;
mod diagnostics;
mod files;
mod mini;
mod layout;
mod overlay_style;
mod pill;
mod pick;
mod process;
mod record;
mod run;
mod schema;
mod store;

use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;

use tauri::{AppHandle, Emitter, Manager, WindowEvent};

/// 界面里有没有未保存的改动。
///
/// 外壳不解析流程内容，所以它自己判断不了"脏了没有"——界面每次改动后推过来一个布尔值，
/// 外壳只在用户点 × 时用它决定"直接关"还是"先问一句"。
struct ShellState {
    dirty: Arc<AtomicBool>,
}

/// 界面推来的脏标记。
#[tauri::command]
fn set_dirty(state: tauri::State<'_, ShellState>, dirty: bool) {
    state.dirty.store(dirty, Ordering::Relaxed);
}

/// 界面确认"可以关"之后，让外壳真的关窗。
///
/// 为什么不在界面里直接 `getCurrentWindow().close()`：那条路会再走一遍
/// `CloseRequested`，脏标记还在的话又会被拦回来，于是永远关不掉。
#[tauri::command]
fn close_window(window: tauri::Window) -> Result<(), String> {
    store::remember_size(&window);
    window.destroy().map_err(|e| e.to_string())
}

/// 界面里没接住的错误推到这里，和 Rust 侧的 panic 落在同一个文件。
#[tauri::command]
fn log_error(message: String) {
    crashlog::write(&format!("ui {message}"));
}

/// 浮条上的「停止」：和主窗口那个按钮走同一条命令。
#[tauri::command]
fn pill_stop(app: AppHandle) -> Result<&'static str, String> {
    let outcome = record::record_stop(app.clone())?;
    pill::hide(&app);
    Ok(outcome)
}

/// 探针：环境变量 `KEYMOUSE_PILL_SPIKE=1` 时自动亮一次浮条。
///
/// 浮条平时只在录制期间出现，可"它长得对不对"不该逼着人先真录一轮。给自动化验证
/// （改完就跑、不用人手按键）留一个不靠键盘的入口；平时没人设这个变量，等于不存在。
pub fn spawn_pill_spike(app: &tauri::AppHandle) {
    if std::env::var_os("KEYMOUSE_PILL_SPIKE").is_none() {
        return;
    }
    let app = app.clone();
    std::thread::spawn(move || {
        std::thread::sleep(std::time::Duration::from_millis(900));
        if let Err(error) = pill::show(&app) {
            crate::crashlog::write(&format!("pill spike 失败：{error}"));
        }
    });
}

/// 把窗口夹进当前显示器：大小不超过屏，位置不越界。
///
/// 为什么还要"过一会儿再来一次"：`setup()` 里窗口还没显示，`current_monitor()` 常常给
/// `None`，这时"按屏幕夹一下"会整段跳过。带进来的旧尺寸一旦比当前屏幕还大（换显示器、改缩放、
/// 笔记本拔掉外接屏），底部状态行就永远点不到。夹一次只花几毫秒，值得重复一次。
fn clamp_to_monitor(window: &tauri::WebviewWindow) {
    let monitor = window
        .current_monitor()
        .ok()
        .flatten()
        .or_else(|| window.primary_monitor().ok().flatten());
    let Some(monitor) = monitor else { return };

    let scale = monitor.scale_factor();
    let origin = *monitor.position();
    let area = *monitor.size();
    let margin_x = (40.0 * scale).round() as i32;
    let margin_y = (80.0 * scale).round() as i32;

    // 大小：留出边距（右边距给滚动条，下边距给任务栏），同时不低于 760×520 逻辑像素。
    let min_w = (760.0 * scale).round() as u32;
    let min_h = (520.0 * scale).round() as u32;
    let max_w = area.width.saturating_sub(margin_x.max(0) as u32).max(min_w);
    let max_h = area.height.saturating_sub(margin_y.max(0) as u32).max(min_h);
    if let Ok(size) = window.outer_size() {
        let w = size.width.min(max_w);
        let h = size.height.min(max_h);
        if w != size.width || h != size.height {
            let _ = window.set_size(tauri::PhysicalSize::new(w, h));
        }
    }

    // 位置：让整个窗口落在显示器可见范围内（右下角贴边时优先保住标题栏与底栏）。
    if let (Ok(size), Ok(position)) = (window.outer_size(), window.outer_position()) {
        let right = origin.x + area.width as i32 - size.width as i32;
        let bottom = origin.y + area.height as i32 - size.height as i32;
        let x = position.x.clamp(origin.x, right.max(origin.x));
        let y = position.y.clamp(origin.y, bottom.max(origin.y));
        if x != position.x || y != position.y {
            let _ = window.set_position(tauri::PhysicalPosition::new(x, y));
        }
    }
}

fn main() {
    crashlog::install();

    let dirty = Arc::new(AtomicBool::new(false));

    tauri::Builder::default()
        // 单实例必须第一个装：后装的插件会在"第二个实例"还没被拦下时先跑起来。
        .plugin(tauri_plugin_single_instance::init(|app, argv, _cwd| {
            // 第二次启动：把已有窗口拉到前面，并把点名的文件交给界面打开。
            // 注意不能在这里直接读文件——读文件是 files.rs 的事，界面自己也有一份打开逻辑，
            // 这里只发一个"有人点名了这份文件"的意图。
            if let Some(window) = app.get_webview_window("main") {
                let _ = window.unminimize();
                let _ = window.set_focus();
                if let Some(path) = argv.get(1) {
                    if !path.starts_with('-') {
                        let _ = window.emit("shell://open-external", path.clone());
                    }
                }
            }
        }))
        .plugin(tauri_plugin_dialog::init())
        // 证据图要"在资源管理器里看一下"：交给系统默认程序，而不是自己写一个看图窗。
        .plugin(tauri_plugin_opener::init())
        .setup(|app| {
        /*
          窗口必须是普通窗口：迷你模式会临时置顶，但退出迷你时已经还回去了；
          这里再明确设一次 false，免得"上一轮别人的置顶"被操作系统带进来。
        */
        if let Some(window) = app.get_webview_window("main") {
            let _ = window.set_always_on_top(false);
        }
            // 窗口大小在壳里恢复，不交给界面：界面要等 webview 起来才有机会说话，
            // 那时窗口已经在屏幕上以默认大小闪了一下。这里在窗口显示前就改好。
            if let (Some(window), Some(saved)) = (app.get_webview_window("main"), store::window_size()) {
                // 屏幕可能变过（换了显示器、改了缩放、上次记的是更大的窗口或更靠下的位置）：
                // 把上次的大小**和位置**都夹进当前这块屏幕。只夹大小不夹位置的话，窗口会有一截
                // 落到屏幕外面——底部状态行和「输出」按钮永远点不到。
                let mut width = saved.width;
                let mut height = saved.height;
                let monitor = window
                    .current_monitor()
                    .ok()
                    .flatten()
                    // 窗口还没显示时 current_monitor() 常常给 None；这时退回主屏，
                    // 否则"夹进屏幕"这一整段会被跳过，接着就出现比屏幕还大的窗口。
                    .or_else(|| window.primary_monitor().ok().flatten());
                if let Some(monitor) = monitor.as_ref() {
                    let scale = monitor.scale_factor();
                    let max_width = (f64::from(monitor.size().width) / scale) - 40.0;
                    let max_height = (f64::from(monitor.size().height) / scale) - 80.0;
                    width = width.min(max_width).max(760.0);
                    height = height.min(max_height).max(520.0);
                }
                let _ = window.set_size(tauri::LogicalSize::new(width, height));
                // 位置只在"上次确实记过"时用：老账本里没有 x/y，缺省 0/0 会把窗口甩到左上角。
                if (saved.x != 0 || saved.y != 0) && monitor.is_some() {
                    // outer_position() 给的是物理像素，恢复也用 PhysicalPosition——两者必须成对，
                    // 一端按逻辑、一端按物理，窗口每开一次就漂一段。
                    let monitor = monitor.as_ref().expect("上面刚判过");
                    let scale = monitor.scale_factor();
                    let origin = monitor.position();
                    let area = monitor.size();
                    let w = (width * scale).round() as i32;
                    let h = (height * scale).round() as i32;
                    let right = origin.x + area.width as i32 - w;
                    let bottom = origin.y + area.height as i32 - h;
                    let x = saved.x.clamp(origin.x, right.max(origin.x));
                    let y = saved.y.clamp(origin.y, bottom.max(origin.y));
                    let _ = window.set_position(tauri::PhysicalPosition::new(x, y));
                }
            }

            // 窗口显示之后再夹一次：setup 里拿不到显示器时，上面那整段是跳过的。
            // 400ms 足够窗口映射到屏幕上，又短到用户还没来得及手动调整。
            if let Some(window) = app.get_webview_window("main") {
                std::thread::spawn(move || {
                    std::thread::sleep(std::time::Duration::from_millis(400));
                    clamp_to_monitor(&window);
                });
            }

            spawn_pill_spike(app.handle());
            Ok(())
        })
        .manage(ShellState {
            dirty: Arc::clone(&dirty),
        })
        .manage(run::RunState::default())
        .manage(mini::MiniState::default())
        .invoke_handler(tauri::generate_handler![
            layout::runtime_info,
            layout::run_doctor,
            diagnostics::collect_diagnostics,
            crashlog::crash_log_path,
            files::open_flow,
            files::save_flow,
            files::write_scratch_flow,
            files::read_shot,
            files::shot_file,
            run::validate_flow,
            run::run_flow,
            run::run_cancel,
            run::read_run_report,
            run::pause_flow,
            record::record_flow,
            record::record_flow_elevated,
            record::record_stop,
            pill_stop,
            schema::flow_schema,
            pick::pick_region,
            mini::enter_mini,
            mini::leave_mini,
            store::recent_files,
            store::remember_recent,
            store::window_size,
            store::save_window_size,
            store::load_sections,
            store::save_sections,
            store::long_press_ms,
            store::save_long_press_ms,
            set_dirty,
            close_window,
            log_error,
        ])
        .on_window_event(move |window, event| {
            // 只拦"用户想关窗"这一种；销毁走 destroy()，不会回到这里。
            if let WindowEvent::CloseRequested { api, .. } = event {
                if !dirty.load(Ordering::Relaxed) {
                    // 真要走，先把窗口大小记下来：下次开还是这个大小。
                    store::remember_size(window);
                    return;
                }
                api.prevent_close();
                let _ = window.emit("shell://close-requested", ());
            }
        })
        .run(tauri::generate_context!())
        .expect("启动失败：Tauri 外壳起不来多半是 WebView2 或资源缺失，看 docs/dev/editor-ui.md");
}
