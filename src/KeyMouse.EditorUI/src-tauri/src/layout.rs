//! 环境判定：这是开发树还是安装版、命令行在哪、自己有没有提权。
//!
//! 搜索顺序必须与 `src\KeyMouse.Core\Product\ProductLayout.cs` 的 `CliCandidates()` 一致，
//! 否则会出现"编辑器说找到了、`doctor` 说没有"的错位。

use std::env;
use std::path::{Path, PathBuf};

use serde::Serialize;

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RuntimeInfo {
    /// 找到的命令行；`None` 表示没找到（界面显示"没找到命令行"，不是错误弹窗）。
    pub cli_path: Option<String>,
    /// `KeyMouse.exe --version` 的原样输出，例如 `KeyMouse 0.2.1（引擎 2.1.0）`。
    pub cli_version: Option<String>,
    /// `development` / `installed` / `unknown`。
    pub environment: String,
    /// 开发树的仓库根；安装版为 `None`。
    pub repo_root: Option<String>,
    pub editor_exe: String,
    pub elevated: bool,
    /// 启动时带进来的流程文件（keymouse-editor.exe 流程.json）；没有就是 None。
    /// 与老编辑器一致：双击 .json、命令行直接点名，都应该打开它而不是干等着。
    pub initial_path: Option<String>,
}

#[tauri::command]
pub fn runtime_info() -> RuntimeInfo {
    let exe = env::current_exe().unwrap_or_default();
    let here = exe.parent().map(Path::to_path_buf).unwrap_or_default();
    let repo = find_repo_root(&here);
    let cli = resolve_cli(&here, repo.as_deref());

    let version = cli
        .as_ref()
        .and_then(|path| {
            crate::process::cli_command(path)
                .arg("--version")
                // 我们要解析这段文字，不是在终端里看它：让命令行别去猜控制台码页。
                .env("KEYMOUSE_OUTPUT_ENCODING", "utf-8")
                .output()
                .ok()
        })
        .map(|out| String::from_utf8_lossy(&out.stdout).trim().to_string())
        .filter(|text| !text.is_empty());

    let environment = match &repo {
        Some(_) => "development",
        None => "installed",
    };

    RuntimeInfo {
        cli_path: cli.map(|p| p.display().to_string()),
        cli_version: version,
        environment: environment.to_string(),
        repo_root: repo.map(|p| p.display().to_string()),
        editor_exe: exe.display().to_string(),
        elevated: is_elevated(),
        initial_path: initial_path(),
    }
}

/// 从 `start` 往上找同时有 `KeyMouse.sln` 与 `Directory.Build.props` 的目录。
pub fn find_repo_root(start: &Path) -> Option<PathBuf> {
    let mut dir = Some(start);
    for _ in 0..12 {
        let current = dir?;
        if current.join("KeyMouse.sln").is_file() && current.join("Directory.Build.props").is_file() {
            return Some(current.to_path_buf());
        }
        dir = current.parent();
    }
    None
}

/// 与 `ProductLayout.CliCandidates()` 同序：环境变量 → artifacts\cli → bin\Release → 自身目录 → 自身\cli → PATH。
fn resolve_cli(here: &Path, repo: Option<&Path>) -> Option<PathBuf> {
    const NAME: &str = "KeyMouse.exe";
    let mut candidates: Vec<PathBuf> = Vec::new();

    if let Ok(over) = env::var("KEYMOUSE_CLI") {
        if !over.trim().is_empty() {
            candidates.push(PathBuf::from(over));
        }
    }
    if let Some(root) = repo {
        candidates.push(root.join("artifacts").join("cli").join(NAME));
        candidates.push(
            root.join("src")
                .join("KeyMouse.Cli")
                .join("bin")
                .join("Release")
                .join("net10.0-windows")
                .join(NAME),
        );
    }
    candidates.push(here.join(NAME));
    candidates.push(here.join("cli").join(NAME));

    if let Some(found) = candidates.into_iter().find(|p| p.is_file()) {
        return Some(found);
    }

    // 最后才看 PATH：那里常常是上一次安装留下的旧版本。
    env::split_paths(&env::var_os("PATH").unwrap_or_default())
        .map(|dir| dir.join(NAME))
        .find(|p| p.is_file())
}

/// 第一个命令行参数：存在的文件才算，别的（比如 --foo）一律忽略。
fn initial_path() -> Option<String> {
    let arg = env::args().nth(1)?;
    if arg.starts_with('-') {
        return None;
    }
    let path = PathBuf::from(&arg);
    let absolute = if path.is_absolute() { path } else { env::current_dir().ok()?.join(path) };
    absolute.is_file().then(|| absolute.display().to_string())
}

/// 提权判定用"能不能在系统目录里建个探针文件"：比起读令牌权限，这条在开发和安装两
/// 种形态下都只看结果，不依赖清单怎么写的。
pub fn is_elevated() -> bool {
    let probe = Path::new(r"C:\Windows\System32").join("keymouse-elevation-probe.tmp");
    fs_write_probe(&probe)
}

fn fs_write_probe(path: &Path) -> bool {
    use std::io::Write;
    match std::fs::File::create(path) {
        Ok(mut file) => {
            let ok = file.write_all(b"probe").is_ok();
            let _ = std::fs::remove_file(path);
            ok
        }
        Err(_) => false,
    }
}
