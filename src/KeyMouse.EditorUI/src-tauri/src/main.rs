//! 新编辑器外壳（Tauri）的入口。
//!
//! 它只做界面做不到的事：开窗口、文件对话框、读写文件、起 `KeyMouse.exe`。
//! 业务判断一律问命令行，不在这里重写一遍——那份逻辑在
//! `src\KeyMouse.Core`，两边各写一份迟早会分叉。
//!
//! 契约见 `docs/reference/editor-ipc.md`。

mod files;
mod layout;

fn main() {
    tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .invoke_handler(tauri::generate_handler![
            layout::runtime_info,
            files::open_flow,
            files::save_flow,
        ])
        .run(tauri::generate_context!())
        .expect("启动失败：Tauri 外壳起不来多半是 WebView2 或资源缺失，看 docs/dev/editor-ui.md");
}
