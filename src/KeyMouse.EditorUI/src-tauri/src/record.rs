//! 录制：把一段操作录成流程文件。
//!
//! 录制的权限问题绕不开：钩子只能看见与它同级或更低权限窗口的输入。所以这里默认**不**
//! 在外壳里自己提权，而是如实按当前权限录——代价是提权窗口上的点击录不到，命令行会把
//! 这件事写在输出里（"这些窗口的权限比 KeyMouse 高"）。
//!
//! 要录那些窗口，用户得显式选「以管理员权限录制」（见 [`record_flow_elevated`]）：
//! 那一刻会弹一次 UAC，之后录制在一个提权进程里跑。代价说清楚：外壳**够不着**那个
//! 提权进程——既杀不掉它，也送不进停止键（UIPI 不许低权限进程向高权限窗口注入输入），
//! 所以停止键只能由用户自己按（`Ctrl+Alt+Q`，或连按两次 `Esc`），外壳这边靠"文件出现
//! 了没有"判断这一轮结束了没有。
//!
//! 停止有两个办法，都用产品自己的能力，不另开后门：
//! 1. 命令行自己的停止键 `Ctrl+Alt+Q`：外壳再发一次这个组合键（就是 `KeyMouse key combo`）。
//! 2. 发键失败/没反应时兜底 `run_cancel`，杀进程——代价是这一次录制不落盘。

use std::process::Stdio;
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

/// 提权录制：命令行自己在 `--elevate` 里用 runas 起一个管理员进程并等它结束，
/// 所以外壳这边仍然只是"起一个子进程、等它退出"，退出码就是那次录制的退出码。
fn elevated_args(out_path: &str, duration_ms: u64, ignore_window: Option<isize>) -> Vec<String> {
    let mut args = vec!["--elevate".to_string()];
    args.extend(record_args(out_path, duration_ms, ignore_window));
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
    spawn_streaming(&app, record_args(&out_path, duration_ms, ignore))?;
    // 迷你模式由界面自己进出（它更清楚"这一轮录制结束了没有"），
    // 这里不再碰窗口形态。
    Ok(())
}

/// 以管理员权限录一轮：UAC 里点"是"之后，录制在提权进程里进行。
///
/// 停止键只能用户自己按——提权进程不接受我们这个非提权外壳的注入（UIPI 拦着）。
/// 所以界面那边要如实告诉用户怎么停，并且**不要**调 [`record_stop`]。
#[tauri::command]
pub fn record_flow_elevated(
    app: AppHandle,
    window: tauri::Window,
    out_path: String,
    duration_ms: u64,
) -> Result<(), String> {
    let ignore = window.hwnd().ok().map(|handle| handle.0 as isize);
    spawn_streaming(&app, elevated_args(&out_path, duration_ms, ignore))?;
    Ok(())
}

/// 按 `Ctrl+Alt+Q` 让命令行自己收尾（它会写文件、打印摘要）。
///
/// 按键走的是同一个命令行、同一套注入：录制本身靠的就是这套注入，能录就能停。
/// 3 秒还没退出就杀进程——宁可丢这一次录制，也不要让界面卡在"录制中"。
///
/// 这 3 秒是留给它写文件的：收到停止键之后，它要把录到的东西落盘、打印摘要、退出。
/// 太短会在最不该打断的时刻打断（写了一半的 json）。
#[tauri::command]
pub fn record_stop(app: AppHandle) -> Result<&'static str, String> {
    let cli = crate::run::cli_path_of()?;

    crate::process::cli_command(&cli)
        .args(["key", "combo", "ctrl+alt+q"])
        .env("KEYMOUSE_OUTPUT_ENCODING", "utf-8")
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .status()
        .map_err(|e| format!("发停止键失败：{e}"))?;

    let started = Instant::now();
    while started.elapsed() < Duration::from_millis(3000) {
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
    use super::{elevated_args, record_args};

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

    #[test]
    fn elevated_args_put_the_flag_first() {
        // --elevate 必须是第一个参数：命令行看到它就不再往下解析，直接 runas 重跑。
        assert_eq!(
            elevated_args("r.json", 2000, Some(0xBEEF)),
            vec!["--elevate", "record", "--out", "r.json", "--duration", "2000", "--ignore-window", "0xBEEF"]
        );
    }
}
