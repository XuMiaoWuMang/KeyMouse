//! 跑流程：试运行、播放、停止。
//!
//! 外壳**不判断**流程对不对、窗口在不在——那些全在 `src\KeyMouse.Core`。这里只负责
//! 起一条 `KeyMouse.exe run <流程> [--dry-run]`、把它的输出按行转成事件、并留住句柄
//! 以便"停止"能真的把它掐掉。
//!
//! 为什么不用常驻的 `KeyMouse serve`：那个进程要提权（录制要能看见被提权的窗口），
//! 而界面进程刻意不提权。一次性子进程的权限跟着外壳走，行为与在终端里手敲一致，
//! 出问题也能拿同一条命令复现。

use std::io::{BufRead, BufReader};
use std::process::{Child, Command, Stdio};
use std::sync::Mutex;

use serde::Serialize;
use tauri::{AppHandle, Emitter, Manager};

/// 正在跑的那条子进程。`None` 表示当前没在跑。
///
/// 它被 `Mutex` 包着是因为：读输出在后台线程，而"停止"来自界面线程。
#[derive(Default)]
pub struct RunState {
    /// 录制那边要问「还在跑吗」，所以对本 crate 可见。
    pub(crate) child: Mutex<Option<Child>>,
    /// 这一次结束是不是被"停止"掐掉的：界面据此说"已停止"而不是"失败（退出码 1）"。
    cancelled: std::sync::atomic::AtomicBool,
    /// 子进程 pid：暂停/继续要按它去开那个命名事件（名字里带着它）。
    pub(crate) pid: Mutex<Option<u32>>,
}

#[derive(Debug, Serialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct RunLine {
    pub stream: &'static str,
    pub line: String,
}

#[derive(Debug, Serialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct RunExit {
    pub code: Option<i32>,
    /// 是不是被"停止"掐掉的——界面据此决定是报"已停止"还是"失败"。
    pub cancelled: bool,
}

/// 一条命令的参数。抽出来是为了能单测：`--dry-run` 必须在路径后面，不能丢。
fn run_args(path: &str, dry_run: bool) -> Vec<String> {
    let mut args = vec!["run".to_string(), path.to_string()];
    if dry_run {
        args.push("--dry-run".to_string());
    }
    args
}

#[tauri::command]
pub fn validate_flow(app: AppHandle, path: String) -> Result<RunExit, String> {
    let cli = cli_path_of()?;
    let output = crate::process::cli_command(&cli)
        .args(run_args(&path, true))
        .env("KEYMOUSE_OUTPUT_ENCODING", "utf-8")
        .stdin(Stdio::null())
        .output()
        .map_err(|e| format!("起不来 {}：{e}", cli.display()))?;

    // 试运行也把输出发给界面：失败时用户需要看到"第 2 步失败：没有窗口匹配"。
    for line in String::from_utf8_lossy(&output.stdout).lines() {
        emit_line(&app, "out", line);
    }
    for line in String::from_utf8_lossy(&output.stderr).lines() {
        emit_line(&app, "err", line);
    }

    Ok(RunExit {
        code: output.status.code(),
        cancelled: false,
    })
}

#[tauri::command]
pub fn run_flow(app: AppHandle, path: String) -> Result<(), String> {
    spawn_streaming(&app, run_args(&path, false)).map_err(|e| e.to_string())
}

/// 起一条命令行的子进程，把它的输出按行转成事件，并把句柄留在槽位里等"停止"。
///
/// 播放与录制共用这一段：两者的差别只在参数，收尾方式一模一样
/// （界面按下停止 → `run_cancel` → 杀整棵子树）。
pub(crate) fn spawn_streaming(app: &AppHandle, args: Vec<String>) -> Result<(), String> {
    let cli = cli_path_of()?;
    let state = app.state::<RunState>();
    {
        let mut slot = state.child.lock().unwrap();
        if slot.is_some() {
            return Err("已经在跑了；先停止再开始下一次".to_string());
        }
        let mut child = crate::process::cli_command(&cli)
            .args(args)
            .env("KEYMOUSE_OUTPUT_ENCODING", "utf-8")
            .stdin(Stdio::null())
            .stdout(Stdio::piped())
            .stderr(Stdio::piped())
            .spawn()
            .map_err(|e| format!("起不来 {}：{e}", cli.display()))?;

        // 先把管子取走，再把句柄放进槽位：读线程不持有句柄，"停止"才能立刻 kill。
        let stdout = child.stdout.take();
        let stderr = child.stderr.take();
        *state.pid.lock().unwrap() = Some(child.id());
        *slot = Some(child);

        let out_handle = app.clone();
        std::thread::spawn(move || {
            if let Some(stream) = stdout {
                pump(stream, "out", out_handle);
            }
        });
        let err_handle = app.clone();
        std::thread::spawn(move || {
            if let Some(stream) = stderr {
                pump(stream, "err", err_handle);
            }
        });
    }

    // 等结束的线程：**不去抢句柄**，只轮询它自己退出了没有。
    //
    // 第一版是"把句柄 take 出来再 wait"，看着更省事，实际把"停止"废掉了：
    // 句柄被 wait 线程搬走之后，run_cancel 在槽位里什么也找不到，按停止毫无反应。
    // 轮询的开销是每 80ms 一次 try_wait，比起一条流程动辄几秒，这钱花得值。
    let wait_handle = app.clone();
    std::thread::spawn(move || {
        let state = wait_handle.state::<RunState>();
        let code = loop {
            let mut slot = state.child.lock().unwrap();
            match slot.as_mut() {
                Some(child) => match child.try_wait() {
                    Ok(Some(status)) => {
                        *slot = None;
                        break status.code();
                    }
                    Ok(None) => {}
                    Err(_) => {
                        *slot = None;
                        break None;
                    }
                },
                // 槽位空了：只有"停止"会把句柄拿走，此时没有退出码可报。
                None => break None,
            }
            drop(slot);
            std::thread::sleep(std::time::Duration::from_millis(80));
        };

        let cancelled = state.cancelled.swap(false, std::sync::atomic::Ordering::Relaxed);
        // 不管这一趟是录制还是播放，子进程一退，悬浮停止球就没有理由留在屏幕上。
        let _ = wait_handle.emit("shell://run-exit", RunExit { code, cancelled });
    });

    Ok(())
}

/// 暂停/继续正在跑的那一条：置上/清掉 `Local\KeyMouse-pause-<pid>` 这个命名事件。
///
/// 命令行在建进程时就把事件建好了（见 `src\KeyMouse.Core\Runtime\PauseControl.cs`），
/// 它每一步之前问一次——所以暂停点永远落在两步之间，不会把半个组合键留在键盘上。
/// 返回 true 表示"确实有在跑的东西被按了"，界面据此切换按钮文案。
#[tauri::command]
pub fn pause_flow(app: AppHandle, paused: bool) -> Result<bool, String> {
    use windows_sys::Win32::Foundation::{CloseHandle, HANDLE};
    use windows_sys::Win32::System::Threading::{OpenEventW, ResetEvent, SetEvent, EVENT_MODIFY_STATE};

    let state = app.state::<RunState>();
    let pid = *state.pid.lock().unwrap();
    let Some(pid) = pid else {
        return Err("现在没有在跑的流程".to_string());
    };

    let mut name: Vec<u16> = format!("Local\\KeyMouse-pause-{pid}").encode_utf16().collect();
    name.push(0);
    // SAFETY: name 以 0 结尾、只有本进程在写这块内存；句柄用完立刻关。
    unsafe {
        let handle: HANDLE = OpenEventW(EVENT_MODIFY_STATE, 0, name.as_ptr());
        if handle.is_null() {
            return Err(format!("找不到 pid {pid} 的暂停开关（它可能已经跑完了）"));
        }
        let ok = if paused { SetEvent(handle) } else { ResetEvent(handle) };
        CloseHandle(handle);
        if ok == 0 {
            return Err("改不了暂停开关".to_string());
        }
    }
    Ok(true)
}

#[tauri::command]
pub fn run_cancel(app: AppHandle) -> bool {
    let state = app.state::<RunState>();
    let mut slot = state.child.lock().unwrap();
    match slot.as_mut() {
        Some(child) => {
            state.cancelled.store(true, std::sync::atomic::Ordering::Relaxed);
            // 整棵子树一起杀：run 可能已经拉起 runner 或外部 OCR 引擎。
            let pid = child.id();
            let killed = child.kill().is_ok();
            let _ = Command::new("taskkill")
                .args(["/PID", &pid.to_string(), "/T", "/F"])
                .stdout(Stdio::null())
                .stderr(Stdio::null())
                .status();
            killed
        }
        None => false,
    }
}

fn pump<R: std::io::Read + Send + 'static>(stream: R, kind: &'static str, app: AppHandle) {
    for line in BufReader::new(stream).lines().map_while(Result::ok) {
        emit_line(&app, kind, &line);
    }
}

fn emit_line(app: &AppHandle, stream: &'static str, line: &str) {
    let _ = app.emit(
        "shell://run-output",
        RunLine {
            stream,
            line: line.to_string(),
        },
    );
}

/// 命令行位置由 `layout::runtime_info` 已经算过一遍；这里再问一次而不是缓存，
/// 是为了让"换了 KEYMOUSE_CLI 重开窗口"这种操作立刻生效。
pub(crate) fn cli_path_of() -> Result<std::path::PathBuf, String> {
    let info = crate::layout::runtime_info();
    info.cli_path
        .map(std::path::PathBuf::from)
        .ok_or_else(|| "没找到 KeyMouse.exe：先跑 pwsh tools\\dev\\km.ps1 d，或用 KEYMOUSE_CLI 指定".to_string())
}

#[cfg(test)]
mod tests {
    use super::run_args;

    #[test]
    fn dry_run_arg_comes_after_the_path() {
        assert_eq!(run_args("a.json", true), vec!["run", "a.json", "--dry-run"]);
    }

    #[test]
    fn play_has_no_extra_flags() {
        assert_eq!(run_args("a b.json", false), vec!["run", "a b.json"]);
    }
}
