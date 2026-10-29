//! KeyMouse 流程编辑器的外壳。
//!
//! 存在的理由只有一个：**界面进程不应该拿着管理员权限**。web 层（WebView2）里没有
//! 文件系统、没有进程、没有输入注入，只有界面；凡是"要和系统说话"的事都从这里出去，
//! 并且走的是与命令行同一份 `KeyMouse.exe`（README 里那条"路径只在 ProductLayout 判定
//! 一次"的约定，这里也不能破坏）。
//!
//! 这个文件保持薄：找命令行、问版本、报告环境。逻辑一旦变多，就说明该下沉进 Core 了。

#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use serde::Serialize;
use std::path::{Path, PathBuf};
use std::process::Command;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct RuntimeInfo {
    /// 编辑器的这一份命令行在哪（找不到就是空字符串，界面会显示"未找到"）。
    cli_path: String,
    /// `KeyMouse.exe --version` 的原样输出。
    cli_version: String,
    /// `development`（仓库里跑的）或 `installed`（装好的）。
    environment: String,
    /// 判定依据：离自己最近的那个 KeyMouse.sln 目录，安装版没有。
    repo_root: String,
    /// 外壳自己的 exe 路径，排查时最有用的一行。
    editor_exe: String,
    /// 外壳是不是提权的。这个产品里它**应该永远为 false**。
    elevated: bool,
}

/// 从当前 exe 往上找仓库根：同时有 `KeyMouse.sln` 与 `Directory.Build.props`。
fn find_repo_root(start: &Path) -> Option<PathBuf> {
    let mut dir = Some(start);
    for _ in 0..12 {
        let here = dir?;
        if here.join("KeyMouse.sln").exists() && here.join("Directory.Build.props").exists() {
            return Some(here.to_path_buf());
        }
        dir = here.parent();
    }
    None
}

/// 命令行候选顺序与 `ProductLayout.CliCandidates()` 一致：环境变量 → 仓库产物 → 自己身边 → PATH。
fn resolve_cli() -> Option<PathBuf> {
    if let Ok(raw) = std::env::var("KEYMOUSE_CLI") {
        let p = PathBuf::from(raw);
        if p.is_file() {
            return Some(p);
        }
    }

    let exe = std::env::current_exe().ok()?;
    let here = exe.parent()?.to_path_buf();

    if let Some(root) = find_repo_root(&here) {
        let candidate = root.join("artifacts").join("cli").join("KeyMouse.exe");
        if candidate.is_file() {
            return Some(candidate);
        }
        let built = root
            .join("src")
            .join("KeyMouse.Cli")
            .join("bin")
            .join("Release")
            .join("net10.0-windows")
            .join("KeyMouse.exe");
        if built.is_file() {
            return Some(built);
        }
    }

    for candidate in [here.join("KeyMouse.exe"), here.join("cli").join("KeyMouse.exe")] {
        if candidate.is_file() {
            return Some(candidate);
        }
    }

    // 最后的退路：PATH 里那份。写死名字和 Core 里的常量一致。
    if let Ok(path) = std::env::var("PATH") {
        for dir in path.split(';') {
            let candidate = Path::new(dir).join("KeyMouse.exe");
            if candidate.is_file() {
                return Some(candidate);
            }
        }
    }

    None
}

fn is_elevated() -> bool {
    // 只问一个问题：能不能写系统目录。能写就是提权的——外壳不该能写。
    let probe = Path::new("C:\\Windows\\System32");
    std::fs::OpenOptions::new()
        .write(true)
        .open(probe.join("keymouse-elevation-probe.tmp"))
        .is_ok()
}

#[tauri::command]
fn runtime_info() -> RuntimeInfo {
    let exe = std::env::current_exe().unwrap_or_default();
    let here = exe.parent().map(Path::to_path_buf).unwrap_or_default();
    let repo = find_repo_root(&here);
    let cli = resolve_cli();

    let version = cli
        .as_ref()
        .and_then(|path| Command::new(path).arg("--version").output().ok())
        .map(|out| String::from_utf8_lossy(&out.stdout).trim().to_string())
        .unwrap_or_default();

    RuntimeInfo {
        cli_path: cli.map(|p| p.display().to_string()).unwrap_or_default(),
        cli_version: version,
        environment: if repo.is_some() { "development" } else { "installed" }.to_string(),
        repo_root: repo.map(|p| p.display().to_string()).unwrap_or_default(),
        editor_exe: exe.display().to_string(),
        elevated: is_elevated(),
    }
}

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![runtime_info])
        .run(tauri::generate_context!())
        .expect("启动失败：Tauri 外壳起不来多半是 WebView2 或资源缺失，看 docs/dev/development.md");
}
