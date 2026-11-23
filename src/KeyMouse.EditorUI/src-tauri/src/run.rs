//! 跑流程：试运行、播放、停止。
//!
//! 外壳**不判断**流程对不对、窗口在不在——那些全在 `src\KeyMouse.Core.*` 能力项目。这里只负责
//! 起一条 `KeyMouse.exe run <流程> [--dry-run]`、把它的输出按行转成事件、并留住句柄
//! 以便"停止"能真的把它掐掉。
//!
//! 界面保持普通权限。实际播放检测到受保护目录的 UIAccess 组件后，由普通 CLI
//! 使用认证管道桥接输入；试运行及未安装组件的环境仍使用普通 CLI。
//! UIAccess 启动失败会明确报错，不静默退回权限不足的执行路径。

use std::io::{BufRead, BufReader, Write};
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
    /// 播放或普通录制期间注册的全局停止键。提权录制不装它：外壳够不着那个高权限进程。
    pub(crate) hotkey: Mutex<Option<crate::hotkey::Handle>>,
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

/// 这一次运行的报告落哪。
///
/// 报告里有每一步的 `origin`（"3"、`"6.1"`），界面靠它把结果写回对应的行——分组步会展开成
/// 多次执行，光看 `[ 3/8 ]` 的执行序号对不上行号。放在临时目录里，和 `editor-run.json`
/// 同一个地方，用户想翻也翻得到。
fn report_path() -> std::path::PathBuf {
    let base = match std::env::var_os("LOCALAPPDATA") {
        Some(dir) => std::path::PathBuf::from(dir).join("KeyMouse").join("tmp"),
        None => std::env::current_exe()
            .ok()
            .and_then(|exe| exe.parent().map(|dir| dir.to_path_buf()))
            .unwrap_or_else(|| std::path::PathBuf::from(".")),
    };
    let _ = std::fs::create_dir_all(&base);
    base.join("editor-run-report.json")
}

/// 与命令行 `ScriptRunner.FallbackReportPath` 约定一致：主目录写不进去时报告会落到这里。
fn fallback_report_path() -> std::path::PathBuf {
    std::env::temp_dir()
        .join("KeyMouse")
        .join("editor-run-report.json")
}

/// 把上一次运行留下的报告原样读给界面。读不到就返回空串：报告是锦上添花，不该让一次
/// 跑成功的流程因为“报告没了”变成错误。
///
/// 两个位置都可能有文件：主路径可能还是上一轮的旧报告，而这一次已经写到了备用路径。
/// 因此按修改时间取新的，不能盲目先读主路径。
#[tauri::command]
pub fn read_run_report() -> String {
    read_latest_report(&[report_path(), fallback_report_path()])
}

fn read_latest_report(paths: &[std::path::PathBuf]) -> String {
    let mut candidates: Vec<_> = paths
        .iter()
        .filter_map(|path| {
            let metadata = std::fs::metadata(path).ok()?;
            if !metadata.is_file() {
                return None;
            }
            Some((metadata.modified().ok()?, path))
        })
        .collect();
    candidates.sort_by_key(|(modified, _)| std::cmp::Reverse(*modified));
    candidates
        .into_iter()
        .find_map(|(_, path)| {
            let body = std::fs::read_to_string(path).ok()?;
            // 写入中断或旧产物留下的半份 JSON 不能遮住另一处完整报告。
            let value = serde_json::from_str::<serde_json::Value>(&body).ok()?;
            value.is_object().then_some(body)
        })
        .unwrap_or_default()
}

/// 每轮开始前清掉旧报告，免得子进程 100ms 失败、界面却读到上一轮的成功报告。
fn clear_run_reports() {
    for path in [report_path(), fallback_report_path()] {
        let _ = std::fs::remove_file(path);
    }
}

/// 一条命令的参数。抽出来是为了能单测：`--dry-run` 必须在路径后面，不能丢。
fn run_args(path: &str, dry_run: bool) -> Vec<String> {
    let mut args = vec![
        "run".to_string(),
        path.to_string(),
        "--report".to_string(),
        report_path().to_string_lossy().to_string(),
    ];
    if dry_run {
        args.push("--dry-run".to_string());
    }
    args
}

#[tauri::command]
pub fn validate_flow(app: AppHandle, path: String) -> Result<RunExit, String> {
    let cli = cli_path_of()?;
    clear_run_reports();
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
    clear_run_reports();
    // 先装全局停止键再起进程：注册失败就不该开始一段没人能叫停的播放。
    let hotkey = crate::hotkey::start(app.clone(), crate::hotkey::Action::Playback)?;
    *app.state::<RunState>().hotkey.lock().unwrap() = Some(hotkey);
    if let Err(error) = spawn_streaming(&app, run_args(&path, false)) {
        // 子进程没起来：把热键线程收掉，别让它占着组合键。
        let _ = app.state::<RunState>().hotkey.lock().unwrap().take();
        return Err(error.to_string());
    }
    Ok(())
}

/// 起一条命令行的子进程，把它的输出按行转成事件，并把句柄留在槽位里等"停止"。
///
/// 播放与录制共用这一段：两者的差别只在参数，收尾方式一模一样
/// （界面按下停止 → `run_cancel` → 杀整棵子树）。
pub(crate) fn spawn_streaming(app: &AppHandle, args: Vec<String>) -> Result<(), String> {
    let cli = cli_path_of()?;
    let helper = installed_input_helper();
    let client = requires_client(&args);
    if client {
        let json = crate::schema::read_schema(&cli)?;
        let schema: serde_json::Value = serde_json::from_str(&json).map_err(|e| e.to_string())?;
        if schema.pointer("/capabilities/backgroundClient").and_then(|v| v.as_bool()) != Some(true) {
            return Err(format!("{} 不支持客户区后台录制与回放。请更新配套 CLI；没有启动录制或发送输入。", cli.display()));
        }
    }
    let args = input_execution_args(args, helper.as_deref(), client);
    let bridged = args.first().is_some_and(|arg| arg == "--uiaccess-run");
    let state = app.state::<RunState>();
    let (out_thread, err_thread) = {
        let mut slot = state.child.lock().unwrap();
        if slot.is_some() {
            return Err("已经在跑了；先停止再开始下一次".to_string());
        }
        // 播放与两种录制共用启动边界；确认槽位空闲后再清上一轮 HUD。
        // 否则先播放再录制时，旧结构化日志会遮住新的录制状态行。
        app.state::<crate::hud::HudLog>().clear();
        let mut child = crate::process::cli_command(&cli)
            .args(args)
            .env("KEYMOUSE_OUTPUT_ENCODING", "utf-8")
            // 让流程输出多一条“执行中”的行，HUD 才能在长步骤（例如等待）里显示当前操作；
            // 主输出面板会过滤掉这类行，命令行直接运行时则完全不会产生它们。
            .env("KEYMOUSE_HUD", "1")
            .stdin(if bridged { Stdio::piped() } else { Stdio::null() })
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
        let out_thread = std::thread::spawn(move || {
            if let Some(stream) = stdout {
                pump(stream, "out", out_handle);
            }
        });
        let err_handle = app.clone();
        let err_thread = std::thread::spawn(move || {
            if let Some(stream) = stderr {
                pump(stream, "err", err_handle);
            }
        });
        (out_thread, err_thread)
    };

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
                    Ok(Some(status)) => break status.code(),
                    Ok(None) => {}
                    Err(_) => break None,
                },
                // 槽位空了：只有"停止"会把句柄拿走，此时没有退出码可报。
                None => break None,
            }
            drop(slot);
            std::thread::sleep(std::time::Duration::from_millis(80));
        };

        // 进程退出时管道仍可能有缓冲；末行必须先于 run-exit，槽位在收尾完成前保持占用。
        drain_output(out_thread, err_thread);
        // 子进程一退就注销播放或普通录制的停止热键。
        let _ = state.hotkey.lock().unwrap().take();
        // 不管这一趟是录制还是播放，子进程一退，日志窗就没有理由留在屏幕上。
        // 用户按 Ctrl+Alt+Q / 到时长自动停时不会有人来关窗，只能在这儿撤。
        crate::hud::hide(&wait_handle);
        let cancelled = {
            // 与停止入口共用 child 锁：释放槽位时才消费取消标记，避免泄漏到下一轮。
            let mut slot = state.child.lock().unwrap();
            *state.pid.lock().unwrap() = None;
            *slot = None;
            state.cancelled.swap(false, std::sync::atomic::Ordering::Relaxed)
        };
        let _ = wait_handle.emit("shell://run-exit", RunExit { code, cancelled });
    });

    Ok(())
}

/// 暂停/继续正在跑的那一条：置上/清掉 `Local\KeyMouse-pause-<pid>` 这个命名事件。
///
/// 命令行在建进程时就把事件建好了（见 `src\KeyMouse.Core.Foundation\Runtime\PauseControl.cs`），
/// 它每一步之前问一次——所以暂停点永远落在两步之间，不会把半个组合键留在键盘上。
/// 返回 true 表示"确实有在跑的东西被按了"，界面据此切换按钮文案。
#[tauri::command]
pub fn pause_flow(app: AppHandle, paused: bool) -> Result<bool, String> {
    use windows_sys::Win32::Foundation::{CloseHandle, HANDLE};
    use windows_sys::Win32::System::Threading::{
        OpenEventW, ResetEvent, SetEvent, EVENT_MODIFY_STATE,
    };

    let state = app.state::<RunState>();
    {
        let mut slot = state.child.lock().unwrap();
        if let Some(stdin) = slot.as_mut().and_then(|child| child.stdin.as_mut()) {
            writeln!(stdin, "{}", serde_json::json!({"type":"pause", "paused":paused}))
                .and_then(|_| stdin.flush()).map_err(|e| format!("发送暂停指令失败：{e}"))?;
            return Ok(true);
        }
    }
    let pid = *state.pid.lock().unwrap();
    let Some(pid) = pid else {
        return Err("现在没有在跑的流程".to_string());
    };

    let mut name: Vec<u16> = format!("Local\\KeyMouse-pause-{pid}")
        .encode_utf16()
        .collect();
    name.push(0);
    // SAFETY: name 以 0 结尾、只有本进程在写这块内存；句柄用完立刻关。
    unsafe {
        let handle: HANDLE = OpenEventW(EVENT_MODIFY_STATE, 0, name.as_ptr());
        if handle.is_null() {
            return Err(format!("找不到 pid {pid} 的暂停开关（它可能已经跑完了）"));
        }
        let ok = if paused {
            SetEvent(handle)
        } else {
            ResetEvent(handle)
        };
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
            state
                .cancelled
                .store(true, std::sync::atomic::Ordering::Relaxed);
            // UIAccess helper 不一定是可由普通壳结束的子进程；由桥接的认证管道转交停止。
            if let Some(mut stdin) = child.stdin.take() {
                if writeln!(stdin, "{{\"type\":\"cancel\"}}\n").and_then(|_| stdin.flush()).is_ok() {
                    return true;
                }
            }
            // 普通 CLI：整棵子树一起杀，run 可能拉起外部 OCR 引擎。
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

fn drain_output(stdout: std::thread::JoinHandle<()>, stderr: std::thread::JoinHandle<()>) {
    for reader in [stdout, stderr] {
        if reader.join().is_err() {
            crate::crashlog::write("运行输出读取线程异常退出");
        }
    }
}

fn pump<R: std::io::Read + Send + 'static>(stream: R, kind: &'static str, app: AppHandle) {
    for line in BufReader::new(stream).lines().map_while(Result::ok) {
        emit_line(&app, kind, &line);
    }
}

fn emit_line(app: &AppHandle, stream: &'static str, line: &str) {
    // HUD 页面可能比这条输出晚挂载；留一份给页面挂载时补拉，别让第一步永远空着。
    app.state::<crate::hud::HudLog>().push(line);
    let _ = app.emit(
        "shell://run-output",
        RunLine {
            stream,
            line: line.to_string(),
        },
    );
}

/// UIAccess 组件由独立的受信任安装步骤放入机器级目录；开发树不猜测 unsigned 产物。
fn installed_input_helper() -> Option<std::path::PathBuf> {
    let base = std::env::var_os("ProgramW6432").or_else(|| std::env::var_os("ProgramFiles"))?;
    let path = std::path::PathBuf::from(base).join("KeyMouse").join("Input").join("KeyMouse.Input.exe");
    path.is_file().then_some(path)
}

fn needs_client_runtime(value: &serde_json::Value) -> bool {
    match value {
        serde_json::Value::Object(v) => v.get("type").and_then(|x| x.as_str()) == Some("call") ||
            ["at", "from", "to", "region"].iter().any(|key| v.get(*key).filter(|p| p.is_object()).is_some_and(|p| p.get("space").and_then(|s| s.as_str()).unwrap_or("client") == "client")) ||
            v.values().any(needs_client_runtime),
        serde_json::Value::Array(v) => v.iter().any(needs_client_runtime),
        _ => false,
    }
}

fn requires_client(args: &[String]) -> bool {
    if args.iter().any(|a| a == "--client-points") { return true; }
    if args.first().is_none_or(|arg| arg != "run") { return false; }
    let doc = args.get(1).and_then(|path| std::fs::read(path).ok()).and_then(|bytes|
        serde_json::from_slice::<serde_json::Value>(bytes.strip_prefix(&[0xef, 0xbb, 0xbf]).unwrap_or(&bytes)).ok());
    doc.as_ref().is_none_or(needs_client_runtime)
}

fn input_execution_args(args: Vec<String>, helper: Option<&std::path::Path>, client: bool) -> Vec<String> {
    let Some(helper) = helper else { return args; };
    if args.first().is_none_or(|arg| arg != "run") || args.iter().any(|arg| arg == "--dry-run") {
        return args;
    }
    // A background flow needs the peer's ordinary token, not UIAccess. Mixed and nested flows
    // stay on the same normal CLI too; an older installed helper must not replay them globally.
    if client { return args; }
    let mut routed = vec!["--uiaccess-run".to_string(), helper.to_string_lossy().into_owned()];
    routed.extend(args);
    routed
}

/// 命令行位置由 `layout::runtime_info` 已经算过一遍；这里再问一次而不是缓存，
/// 是为了让"换了 KEYMOUSE_CLI 重开窗口"这种操作立刻生效。
pub(crate) fn cli_path_of() -> Result<std::path::PathBuf, String> {
    let info = crate::layout::runtime_info();
    info.cli_path.map(std::path::PathBuf::from).ok_or_else(|| {
        "没找到 KeyMouse.exe：先跑 pwsh tools\\dev\\km.ps1 d，或用 KEYMOUSE_CLI 指定".to_string()
    })
}

#[cfg(test)]
mod tests {
    use super::{drain_output, read_latest_report, run_args};
    fn input_execution_args(args: Vec<String>, helper: Option<&std::path::Path>) -> Vec<String> {
        let client = super::requires_client(&args);
        super::input_execution_args(args, helper, client)
    }

    #[test]
    fn input_helper_routes_playback_but_not_validation_or_recording() {
        let helper = std::path::Path::new("C:/Program Files/KeyMouse/Input/KeyMouse.Input.exe");
        let file = std::env::temp_dir().join(format!("km-route-{}.json", std::process::id()));
        std::fs::write(&file, r#"{"steps":[{"type":"click","at":{"space":"screen"}}]}"#).unwrap();
        let play = vec!["run".into(), file.to_string_lossy().into_owned()];
        assert_eq!(input_execution_args(play.clone(), None), play);
        let routed = input_execution_args(play.clone(), Some(helper));
        assert_eq!(&routed[..2], &["--uiaccess-run", helper.to_str().unwrap()]);
        assert_eq!(&routed[2..], &play);
        for data in [r#"{"steps":[{"type":"click","at":{"x":3,"y":4}}]}"#, r#"{"steps":[{"type":"call"}]}"#] {
            std::fs::write(&file, data).unwrap();
            assert_eq!(input_execution_args(play.clone(), Some(helper)), play);
        }
        std::fs::remove_file(file).unwrap();
        let dry = vec!["run".into(), "new flow.json".into(), "--dry-run".into()];
        assert_eq!(input_execution_args(dry.clone(), Some(helper)), dry);
        let record = vec!["record".into(), "flow.json".into()];
        assert_eq!(input_execution_args(record.clone(), Some(helper)), record);
    }

    #[test]
    fn output_is_drained_before_exit_notification() {
        let events = std::sync::Arc::new(std::sync::Mutex::new(Vec::new()));
        let (release, barrier) = std::sync::mpsc::channel();
        let out_events = events.clone();
        let stdout = std::thread::spawn(move || {
            barrier.recv().unwrap();
            out_events.lock().unwrap().push("last stdout");
        });
        let err_events = events.clone();
        let stderr = std::thread::spawn(move || {
            err_events.lock().unwrap().push("last stderr");
        });
        release.send(()).unwrap();
        drain_output(stdout, stderr);
        events.lock().unwrap().push("exit");
        let events = events.lock().unwrap();
        assert_eq!(events.len(), 3);
        assert_eq!(events.last(), Some(&"exit"));
    }

    struct ReportFixture(std::path::PathBuf);

    impl ReportFixture {
        fn new() -> Self {
            static NEXT: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(0);
            let id = NEXT.fetch_add(1, std::sync::atomic::Ordering::Relaxed);
            let root = std::env::temp_dir().join(format!(
                "keymouse-report-tests-{}-{}-{id}",
                std::process::id(),
                std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)
                    .unwrap()
                    .as_nanos()
            ));
            std::fs::create_dir_all(&root).unwrap();
            Self(root)
        }

        fn file(&self, name: &str, body: &str, age: u64) -> std::path::PathBuf {
            let path = self.0.join(name);
            std::fs::write(&path, body).unwrap();
            std::fs::File::options()
                .write(true)
                .open(&path)
                .unwrap()
                .set_times(
                    std::fs::FileTimes::new()
                        .set_modified(std::time::UNIX_EPOCH + std::time::Duration::from_secs(age)),
                )
                .unwrap();
            path
        }
    }

    impl Drop for ReportFixture {
        fn drop(&mut self) {
            let _ = std::fs::remove_dir_all(&self.0);
        }
    }

    #[test]
    fn unreadable_newer_candidate_does_not_hide_valid_report() {
        let fixture = ReportFixture::new();
        let good = fixture.file("fallback.json", "{\"succeeded\":2}", 100);
        let directory = fixture.0.join("primary.json");
        std::fs::create_dir(&directory).unwrap();
        assert_eq!(read_latest_report(&[good, directory]), "{\"succeeded\":2}");
    }

    #[test]
    fn truncated_newer_candidate_does_not_hide_valid_report() {
        let fixture = ReportFixture::new();
        let good = fixture.file("fallback.json", "{\"succeeded\":2}", 100);
        let broken = fixture.file("primary.json", "{\"succeeded\":", 200);
        assert_eq!(read_latest_report(&[good, broken]), "{\"succeeded\":2}");
    }

    #[test]
    fn newest_valid_report_wins_and_missing_reports_are_empty() {
        let fixture = ReportFixture::new();
        let old = fixture.file("primary.json", "{\"succeeded\":1}", 100);
        let new = fixture.file("fallback.json", "{\"succeeded\":2}", 200);
        assert_eq!(read_latest_report(&[new, old]), "{\"succeeded\":2}");
        assert_eq!(read_latest_report(&[fixture.0.join("missing.json")]), "");
    }

    #[test]
    fn dry_run_arg_comes_after_the_path() {
        assert_eq!(
            run_args("a.json", true)[0..3],
            [
                "run".to_string(),
                "a.json".to_string(),
                "--report".to_string()
            ]
        );
        assert_eq!(
            *run_args("a.json", true).last().unwrap(),
            "--dry-run".to_string()
        );
    }

    #[test]
    fn play_has_no_extra_flags() {
        let plain = run_args("a b.json", false);
        assert_eq!(plain[0..2], ["run".to_string(), "a b.json".to_string()]);
        assert!(!plain.contains(&"--dry-run".to_string()));
    }
}
