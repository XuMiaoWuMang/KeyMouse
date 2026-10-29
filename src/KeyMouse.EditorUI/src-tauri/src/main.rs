//! 新编辑器外壳（Tauri）的入口。
//!
//! 它只做界面做不到的事：开窗口、文件对话框、读写文件、起 `KeyMouse.exe`。
//! 业务判断一律问命令行，不在这里重写一遍——那份逻辑在
//! `src\KeyMouse.Core`，两边各写一份迟早会分叉。
//!
//! 契约见 `docs/reference/editor-ipc.md`。

mod crashlog;
mod files;
mod layout;
mod record;
mod run;

use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;

use tauri::{Emitter, Manager, WindowEvent};

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
    window.destroy().map_err(|e| e.to_string())
}

/// 界面里没接住的错误推到这里，和 Rust 侧的 panic 落在同一个文件。
#[tauri::command]
fn log_error(message: String) {
    crashlog::write(&format!("ui {message}"));
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
        .manage(ShellState {
            dirty: Arc::clone(&dirty),
        })
        .manage(run::RunState::default())
        .invoke_handler(tauri::generate_handler![
            layout::runtime_info,
            files::open_flow,
            files::save_flow,
            files::read_shot,
            files::shot_file,
            run::validate_flow,
            run::run_flow,
            run::run_cancel,
            record::record_flow,
            record::record_stop,
            set_dirty,
            close_window,
            log_error,
        ])
        .on_window_event(move |window, event| {
            // 只拦"用户想关窗"这一种；销毁走 destroy()，不会回到这里。
            if let WindowEvent::CloseRequested { api, .. } = event {
                if !dirty.load(Ordering::Relaxed) {
                    return;
                }
                api.prevent_close();
                let _ = window.emit("shell://close-requested", ());
            }
        })
        .run(tauri::generate_context!())
        .expect("启动失败：Tauri 外壳起不来多半是 WebView2 或资源缺失，看 docs/dev/editor-ui.md");
}
