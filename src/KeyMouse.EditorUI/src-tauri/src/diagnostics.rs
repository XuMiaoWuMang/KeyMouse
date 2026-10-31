//! 一键打包诊断信息。
//!
//! 用户报"它不动了"的时候，最费时间的不是修，而是问清楚"你那个环境到底什么样"。
//! 这里把外壳自己能看到的东西一次性落到一个文件夹里：环境、体检输出、崩溃日志、
//! 设置账本。全部只读本地文件，不联网、不上传——打包完由用户自己决定发给谁。
//!
//! 三条刻意的取舍：
//! - 写文件失败只降级（那一份标上"读不到"），不让整个打包失败：能拿到几份算几份；
//! - 崩溃日志可能很大，只取末尾若干行——开头那几百行是几个月前的，没人看；
//! - 文件夹名带时间戳，连着打包两次不互相覆盖。

use std::fs;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use crate::{crashlog, layout, store};

/// 崩溃日志最多带走这么多行（从末尾数）。
const CRASH_TAIL_LINES: usize = 400;

/// 打包目录：和设置账本放在一起，用户找得到。
pub(crate) fn bundle_root() -> PathBuf {
    let base = std::env::var("LOCALAPPDATA")
        .map(PathBuf::from)
        .unwrap_or_else(|_| std::env::current_dir().unwrap_or_default());
    base.join("KeyMouse").join("diagnostics")
}

/// 时间戳：本地时间不好拿，就用 UTC 的 `YYYYMMDD-HHMMSS`，只求不重名。
fn stamp() -> String {
    let seconds = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_secs())
        .unwrap_or_default() as i64;
    let days = seconds.div_euclid(86_400);
    let rest = seconds.rem_euclid(86_400);
    let (year, month, day) = civil_from_days(days);
    format!(
        "{year:04}{month:02}{day:02}-{:02}{:02}{:02}",
        rest / 3600,
        (rest % 3600) / 60,
        rest % 60
    )
}

/// Howard Hinnant 的 `civil_from_days`：不引依赖把天数换成年月日。
fn civil_from_days(days: i64) -> (i64, i64, i64) {
    let z = days + 719_468;
    let era = z.div_euclid(146_097);
    let doe = z.rem_euclid(146_097);
    let yoe = (doe - doe / 1460 + doe / 36_524 - doe / 146_096) / 365;
    let y = yoe + era * 400;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let d = doy - (153 * mp + 2) / 5 + 1;
    let m = if mp < 10 { mp + 3 } else { mp - 9 };
    (if m <= 2 { y + 1 } else { y }, m, d)
}

/// 把若干份文本写进目录；返回写了哪些文件名。
///
/// 拆出来是为了能单测：真正收集内容的部分依赖进程环境（是不是提权、命令行在哪），
/// 而"落盘"这一步不依赖任何东西。
pub(crate) fn write_bundle(dir: &Path, files: &[(&str, String)]) -> Result<Vec<String>, String> {
    fs::create_dir_all(dir).map_err(|error| format!("建不了目录 {}：{error}", dir.display()))?;
    let mut written = Vec::new();
    for (name, body) in files {
        let path = dir.join(name);
        match fs::write(&path, body) {
            Ok(()) => written.push((*name).to_string()),
            // 单份失败不算整体失败：其它几份仍然有诊断价值。
            Err(error) => written.push(format!("{name}（写不进去：{error}）")),
        }
    }
    Ok(written)
}

/// 读崩溃日志的末尾若干行；文件不存在返回说明而不是空串。
fn crash_tail(path: &Path) -> String {
    match fs::read_to_string(path) {
        Ok(text) => {
            let lines: Vec<&str> = text.lines().collect();
            let from = lines.len().saturating_sub(CRASH_TAIL_LINES);
            lines[from..].join("\n")
        }
        Err(error) => format!("（读不到 {path}：{error}）", path = path.display()),
    }
}

/// 收一次诊断：写进 `%LOCALAPPDATA%\KeyMouse\diagnostics\<时间戳>\`，返回那个目录。
#[tauri::command]
pub fn collect_diagnostics() -> Result<String, String> {
    let dir = bundle_root().join(stamp());
    let info = layout::runtime_info();
    let doctor = match layout::run_doctor() {
        Ok(text) => text,
        Err(error) => error,
    };
    let settings = match fs::read_to_string(store::store_path()) {
        Ok(text) => text,
        Err(error) => format!("（读不到设置账本：{error}）"),
    };

    let mut files: Vec<(&str, String)> = Vec::new();
    files.push(("环境.txt", format_runtime(&info)));
    files.push(("体检.txt", doctor));
    files.push(("崩溃日志尾部.txt", crash_tail(Path::new(&crashlog::crash_log_path()))));
    files.push(("设置.json", settings));
    files.push(("说明.txt", readme()));

    write_bundle(&dir, &files)?;
    Ok(dir.display().to_string())
}

fn format_runtime(info: &layout::RuntimeInfo) -> String {
    // `cliPath` / `cliVersion` 找不到时是 `None`：这里写成"没找到"，别让诊断文件里出现 `None`。
    let cli = info.cli_path.as_deref().unwrap_or("（没找到命令行）");
    let version = info.cli_version.as_deref().unwrap_or("（没读到版本）");
    let repo = info.repo_root.as_deref().unwrap_or("（不在开发树里）");
    format!(
        "命令行：{cli}\n命令行版本：{version}\n环境：{env}\n仓库：{repo}\n外壳：{editor}\n提权：{elevated}\n",
        env = info.environment,
        editor = info.editor_exe,
        elevated = if info.elevated { "是" } else { "否" }
    )
}

fn readme() -> String {
    "这几份东西是 KeyMouse 编辑器自己写的，里面没有截图、没有输入内容、没有剪贴板。\n\
     \n\
     环境.txt        外壳认到的命令行、版本、是开发树还是安装版、有没有提权\n\
     体检.txt        KeyMouse doctor 的完整输出\n\
     崩溃日志尾部.txt 最近 400 行崩溃记录（没有崩溃过就没有这个文件）\n\
     设置.json       最近打开的流程、窗口位置与大小\n\
     \n\
     要报问题时把整个文件夹发给作者。里面没有能定位到你私人内容的东西，\n\
     但记录过流程路径——不想给就先把那几行删掉。\n"
        .to_string()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn writes_every_file_and_reports_them() {
        let dir = std::env::temp_dir().join(format!("keymouse-diag-test-{}", std::process::id()));
        let _ = fs::remove_dir_all(&dir);
        let files = vec![("a.txt", "第一份".to_string()), ("b.txt", "第二份".to_string())];
        let written = write_bundle(&dir, &files).expect("应当写得进去");
        assert_eq!(written, vec!["a.txt".to_string(), "b.txt".to_string()]);
        let text = fs::read_to_string(dir.join("a.txt")).expect("应当读得回来");
        assert!(text.contains("第一份"));
        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn stamp_is_sixteen_digits() {
        let text = stamp();
        assert_eq!(text.len(), 15, "YYYYMMDD-HHMMSS 应当是 15 个字符：{text}");
        assert!(text.chars().nth(8) == Some('-'), "第九位应当是分隔符：{text}");
    }

    #[test]
    fn crash_tail_says_so_when_missing() {
        let text = crash_tail(Path::new("这个文件肯定不存在-please-ignore.log"));
        assert!(text.contains("读不到"));
    }
}
