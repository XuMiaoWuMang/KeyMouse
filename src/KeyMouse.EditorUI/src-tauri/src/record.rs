//! 录制：把一段操作录成流程文件。
//!
//! 录制的权限问题绕不开：钩子只能看见与它同级或更低权限窗口的输入。所以这里**不**在
//! 外壳里自己提权，而是如实按当前权限录——代价是提权窗口上的点击录不到，命令行会把
//! 这件事写在输出里（"这些窗口的权限比 KeyMouse 高"）。要录那些窗口，用命令行
//! `KeyMouse --elevate record --out …`，那句话会原样出现在输出面板里。
//!
//! 停止有两个办法，都用产品自己的能力，不另开后门：
//! 1. 命令行自己的停止键 `Ctrl+Alt+Q`：外壳再发一次这个组合键（就是 `KeyMouse key combo`）。
//! 2. 发键失败/没反应时兜底 `run_cancel`，杀进程——代价是这一次录制不落盘。

use std::process::{Command, Stdio};
use std::time::{Duration, Instant};

use tauri::{AppHandle, Manager};

use crate::run::{spawn_streaming, RunState};

/// 录制时把自己这个窗口排除掉：否则"点一下编辑器"也会被录成一步。
fn record_args(out_path: &str, duration_ms: u64, ignore_window: Option<isize>) -> Vec<String> {
    let mut args = vec![
        "record".to_string(),
        "--out".to_string(),
        out_path.to_string(),
    ];

    if duration_ms > 0 {
        args.push("--duration".to_string());
        args.push(duration_ms.to_string());
    }

    if let Some(handle) = ignore_window {
        args.push("--ignore-window".to_string());
        args.push(format!("0x{handle:X}"));
    }

    args
}

#[tauri::command]
pub fn record_flow(
    app: AppHandle,
    window: tauri::Window,
    out_path: String,
    duration_ms: u64,
) -> Result<(), String> {
    let ignore = window.hwnd().ok().map(|handle| handle.0 as isize);
    spawn_streaming(&app, record_args(&out_path, duration_ms, ignore))
}

/// 按 `Ctrl+Alt+Q` 让命令行自己收尾（它会写文件、打印摘要）。
///
/// 按键走的是同一个命令行、同一套注入：录制本身靠的就是这套注入，能录就能停。
/// 1.5 秒还没退出就杀进程——宁可丢这一次录制，也不要让界面卡在"录制中"。
#[tauri::command]
pub fn record_stop(app: AppHandle) -> Result<&'static str, String> {
    let cli = crate::run::cli_path_of(&app)?;

    Command::new(&cli)
        .args(["key", "combo", "ctrl+alt+q"])
        .env("KEYMOUSE_OUTPUT_ENCODING", "utf-8")
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .status()
        .map_err(|e| format!("发停止键失败：{e}"))?;

    let started = Instant::now();
    while started.elapsed() < Duration::from_millis(1500) {
        if !is_running(&app) {
            return Ok("stopped");
        }
        std::thread::sleep(Duration::from_millis(100));
    }

    if crate::run::run_cancel(app) {
        return Ok("killed");
    }
    Ok("idle")
}

fn is_running(app: &AppHandle) -> bool {
    let state = app.state::<RunState>();
    let slot = state.child.lock().unwrap();
    slot.is_some()
}

#[cfg(test)]
mod tests {
    use super::record_args;

    #[test]
    fn record_args_include_output_and_ignore_window() {
        assert_eq!(
            record_args("C:\\a b\\rec.json", 0, Some(0x12AB)),
            vec!["record", "--out", "C:\\a b\\rec.json", "--ignore-window", "0x12AB"]
        );
    }

    #[test]
    fn duration_is_only_passed_when_positive() {
        assert_eq!(record_args("r.json", 3000, None), vec!["record", "--out", "r.json", "--duration", "3000"]);
    }
}
