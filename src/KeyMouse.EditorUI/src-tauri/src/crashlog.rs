//! 崩溃与错误日志。
//!
//! 装在用户目录里（`%LOCALAPPDATA%\KeyMouse\editor-crash.log`），和老的 WinUI 外壳同一个位置——
//! 用户习惯了一句话"看那个日志"，换外壳不该让这句话失效。
//!
//! 记三件事：Rust 侧的 panic、界面里没接住的异常（由前端调 `log_error` 推来）、启动那一行。
//! 只追加、不轮转：这个文件正常情况下一辈子是空的。

use std::io::Write;
use std::path::PathBuf;

/// 日志文件路径。拿不到 `LOCALAPPDATA` 时退到程序自己所在目录。
fn log_path() -> PathBuf {
    let directory = match std::env::var_os("LOCALAPPDATA") {
        Some(root) => PathBuf::from(root).join("KeyMouse"),
        None => std::env::current_exe()
            .ok()
            .and_then(|exe| exe.parent().map(|p| p.to_path_buf()))
            .unwrap_or_else(|| PathBuf::from(".")),
    };

    let _ = std::fs::create_dir_all(&directory);
    directory.join("editor-crash.log")
}

/// 日志文件的绝对路径（界面要拿它去"在文件夹里看"或直接打开）。
/// 每次调用都会确保目录存在，所以拿到路径之后直接打开不会因为文件还没建而失败。
#[tauri::command]
pub fn crash_log_path() -> String {
    log_path().to_string_lossy().to_string()
}

/// 追加一行（带本地时间）。
pub fn write(line: &str) {
    let path = log_path();
    if let Ok(mut file) = std::fs::OpenOptions::new().create(true).append(true).open(&path) {
        let _ = writeln!(file, "{}  {}", stamp(), line);
    }
}

/// 时间戳。不引第三方库：这里只要"能看出先后"，不做日志框架的事。
fn stamp() -> String {
    // `SystemTime` 没有本地时区的概念；用 `chrono` 只为了一个时间戳不值当，
    // 所以记 UTC 并在末尾标明——排查时够用。
    let now = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_secs())
        .unwrap_or(0);

    let (days, seconds) = (now / 86_400, now % 86_400);
    let (hours, minutes, secs) = (seconds / 3600, (seconds % 3600) / 60, seconds % 60);

    // days → 公历年月日（Howard Hinnant 的 civil_from_days）。
    let z = days as i64 + 719_468;
    let era = if z >= 0 { z } else { z - 146_096 } / 146_097;
    let doe = z - era * 146_097;
    let yoe = (doe - doe / 1460 + doe / 36_524 - doe / 146_096) / 365;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let day = doy - (153 * mp + 2) / 5 + 1;
    let month = if mp < 10 { mp + 3 } else { mp - 9 };
    let year = yoe + era * 400 + if month <= 2 { 1 } else { 0 };

    format!("{year:04}-{month:02}-{day:02} {hours:02}:{minutes:02}:{secs:02}Z")
}

/// 接管 panic：先记下来，再交给默认处理器（它会把信息打到 stderr）。
pub fn install() {
    write(&format!(
        "启动 {}",
        std::env::args().nth(1).unwrap_or_else(|| "（没有点名文件）".into())
    ));

    let previous = std::panic::take_hook();
    std::panic::set_hook(Box::new(move |info| {
        let location = info
            .location()
            .map(|l| format!("{}:{}", l.file(), l.line()))
            .unwrap_or_else(|| "未知位置".into());
        write(&format!("panic {location} {info}"));
        previous(info);
    }));
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn stamp_is_sortable_and_ends_with_z() {
        let value = stamp();
        // 固定宽度：按字典序排就是按时间排。
        assert_eq!(value.len(), 20, "{value}");
        assert!(value.ends_with('Z'), "{value}");
        assert!(value.starts_with("20"), "{value}");
    }
}
