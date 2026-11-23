//! 用户主动导出输出面板快照；不读报告或真实日志，不自动持久化。
//! IPC 契约：docs/reference/editor-ipc.md 的 save_run_log。

use std::fs::{self, OpenOptions};
use std::io::Write;
use std::path::Path;
use std::sync::atomic::{AtomicU64, Ordering};

use crate::files::ShellError;

static NEXT_FILE: AtomicU64 = AtomicU64::new(0);

/// 在用户选择的目录内完成写入后再替换，避免失败时截断旧日志。
#[tauri::command]
pub fn save_run_log(path: String, text: String) -> Result<(), ShellError> {
    write_snapshot(Path::new(&path), text.as_bytes())
        .map_err(|error| ShellError::new("shell.io", format!("保存日志失败 {path}：{error}")))
}

fn write_snapshot(target: &Path, bytes: &[u8]) -> std::io::Result<()> {
    let parent = target.parent().filter(|dir| !dir.as_os_str().is_empty()).unwrap_or_else(|| Path::new("."));
    // 固定长度名字避免目标文件名很长时临时路径超限；create_new 防止碰撞和覆盖。
    let (temporary, mut file) = loop {
        let id = NEXT_FILE.fetch_add(1, Ordering::Relaxed);
        let path = parent.join(format!(".keymouse-log-{}-{id}.tmp", std::process::id()));
        match OpenOptions::new().write(true).create_new(true).open(&path) {
            Ok(file) => break (path, file),
            Err(error) if error.kind() == std::io::ErrorKind::AlreadyExists => continue,
            Err(error) => return Err(error),
        }
    };
    let result = file.write_all(bytes).and_then(|_| file.sync_all());
    drop(file); // Windows 重命名前必须关闭本进程句柄。
    let result = result.and_then(|_| fs::rename(&temporary, target));
    if result.is_err() {
        let _ = fs::remove_file(&temporary);
    }
    result
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;
    use std::time::{SystemTime, UNIX_EPOCH};

    struct TestDirectory(PathBuf);

    impl TestDirectory {
        fn new() -> Self {
            let stamp = SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_nanos();
            let path = std::env::temp_dir().join(format!("keymouse-run-log-{}-{stamp}-{}", std::process::id(), NEXT_FILE.fetch_add(1, Ordering::Relaxed)));
            fs::create_dir(&path).unwrap();
            Self(path)
        }

        fn assert_only(&self, name: &str) {
            let names: Vec<_> = fs::read_dir(&self.0).unwrap().map(|entry| entry.unwrap().file_name()).collect();
            assert_eq!(names, vec![std::ffi::OsString::from(name)]);
        }
    }

    impl Drop for TestDirectory {
        fn drop(&mut self) { let _ = fs::remove_dir_all(&self.0); }
    }

    #[test]
    fn exports_utf8_without_bom_and_replaces_exact_snapshot() {
        let dir = TestDirectory::new();
        let target = dir.0.join("未命名流程.log");
        let text = "# 提示\n  合成测试输出 ✓\n错误：示例\n";
        save_run_log(target.display().to_string(), text.into()).unwrap();
        assert_eq!(fs::read(&target).unwrap(), text.as_bytes());
        save_run_log(target.display().to_string(), "新快照\n".into()).unwrap();
        assert_eq!(fs::read(&target).unwrap(), "新快照\n".as_bytes());
        dir.assert_only("未命名流程.log");
    }

    #[test]
    fn failed_replacement_cleans_temporary_file_and_preserves_target() {
        let dir = TestDirectory::new();
        let target = dir.0.join("directory.log");
        fs::create_dir(&target).unwrap();
        fs::write(target.join("marker"), "original").unwrap();
        let error = save_run_log(target.display().to_string(), "snapshot".into()).unwrap_err();
        assert_eq!(error.code, "shell.io");
        assert!(error.message.contains("保存日志失败"));
        assert_eq!(fs::read_to_string(target.join("marker")).unwrap(), "original");
        dir.assert_only("directory.log");
    }

    #[test]
    fn missing_directory_reports_io_without_creating_it() {
        let dir = TestDirectory::new();
        let error = save_run_log(dir.0.join("missing/output.txt").display().to_string(), "snapshot".into()).unwrap_err();
        assert_eq!(error.code, "shell.io");
        assert_eq!(fs::read_dir(&dir.0).unwrap().count(), 0);
    }

    #[cfg(windows)]
    #[test]
    fn locked_destination_keeps_previous_log_intact() {
        use std::os::windows::fs::OpenOptionsExt;
        let dir = TestDirectory::new();
        let target = dir.0.join("locked.log");
        fs::write(&target, "previous snapshot").unwrap();
        let lock = OpenOptions::new().read(true).share_mode(1).open(&target).unwrap();
        assert_eq!(save_run_log(target.display().to_string(), "replacement".into()).unwrap_err().code, "shell.io");
        assert_eq!(fs::read_to_string(&target).unwrap(), "previous snapshot");
        dir.assert_only("locked.log");
        drop(lock);
    }
}
