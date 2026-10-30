//! 外壳与界面之间的全部接口。
//!
//! 契约写在 `docs/reference/editor-ipc.md`：参数、返回值、错误码、界面怎么用。
//! 改这里的任何一条，先去改那份文档。

use std::fs;
use std::path::{Path, PathBuf};

use serde::Serialize;

/// 失败一律带一个稳定的 `code`，界面按它分支；`message` 是给人看的中文原话。
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ShellError {
    pub code: &'static str,
    pub message: String,
}

impl ShellError {
    pub fn new(code: &'static str, message: impl Into<String>) -> Self {
        Self { code, message: message.into() }
    }
}

/// 一个流程文件：原样文本 + 解析后的对象。界面拿 `json` 渲染，拿 `text` 兜底比对。
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FlowFile {
    pub path: String,
    pub text: String,
    pub json: serde_json::Value,
}

/// 读一个流程文件。只做"能读、是 JSON"，不做字段校验——那是 `validate_flow` 的事。
#[tauri::command]
pub fn open_flow(path: String) -> Result<FlowFile, ShellError> {
    let text = fs::read_to_string(&path)
        .map_err(|e| ShellError::new("shell.io", format!("读不了 {path}：{e}")))?;
    let json: serde_json::Value = serde_json::from_str(&text).map_err(|e| {
        ShellError::new(
            "flow.invalid-json",
            format!("{} 不是合法 JSON：{e}", short(&path)),
        )
    })?;
    Ok(FlowFile { path, text, json })
}

/// 把界面手里的文本原样写回。不校验、不格式化——界面已经在内存里改过了。
#[tauri::command]
pub fn save_flow(path: String, text: String) -> Result<(), ShellError> {
    if let Some(parent) = Path::new(&path).parent() {
        if !parent.as_os_str().is_empty() {
            fs::create_dir_all(parent)
                .map_err(|e| ShellError::new("shell.io", format!("建不了目录 {}：{e}", parent.display())))?;
        }
    }
    fs::write(&path, text.as_bytes())
        .map_err(|e| ShellError::new("shell.io", format!("写不了 {path}：{e}")))
}

/// 外壳自己的工作目录：`%LOCALAPPDATA%\KeyMouse\tmp`，拿不到变量就退到程序目录。
///
/// 与命令行那边的 `Scratch`（TEMP → LOCALAPPDATA → 程序目录）不是同一份实现，但**同一套
/// 意思**：临时文件总得有个写得进去的地方。这里不先试 `%TEMP%`，是因为外壳只需要一个
/// 稳定位置（"那一份临时副本"每次覆盖同一个文件，用户想找也找得到）。
fn scratch_dir() -> Result<PathBuf, ShellError> {
    let base = std::env::var_os("LOCALAPPDATA")
        .map(PathBuf::from)
        .unwrap_or_else(|| {
            std::env::current_exe()
                .ok()
                .and_then(|exe| exe.parent().map(Path::to_path_buf))
                .unwrap_or_else(|| PathBuf::from("."))
        });
    let dir = base.join("KeyMouse").join("tmp");
    fs::create_dir_all(&dir)
        .map_err(|e| ShellError::new("shell.io", format!("建不了临时目录 {}：{e}", dir.display())))?;
    Ok(dir)
}

/// 把当前编辑内容写到临时副本，返回那个路径——**给"还没保存就想试运行/播放"用**。
///
/// 为什么要有它：命令行只认文件。老外壳遇到未保存的改动就是这么做的（写一份临时文件去跑），
/// 新外壳此前直接拒绝（"先保存再跑"），于是"加两步试试"这条最自然的用法走不通。
/// 绝不写回用户正在编辑的那个文件——那是 `save_flow` 的事。
#[tauri::command]
pub fn write_scratch_flow(text: String) -> Result<String, ShellError> {
    let path = scratch_dir()?.join("editor-run.json");
    fs::write(&path, text.as_bytes())
        .map_err(|e| ShellError::new("shell.io", format!("写不了临时文件 {}：{e}", path.display())))?;
    Ok(path.to_string_lossy().into_owned())
}

/// 读一张证据图。`shot` 是流程里记录的那个相对路径（`<流程>.shots/0001-click.png`）。
///
/// 三道检查缺一不可：只认 `.png`、解析后必须真的在**流程文件所在目录**里（`..` 与软链接都拦）、
/// 并且文件确实存在。界面拿字节自己造 Blob URL，**不走 asset 协议**——这样 WebView 永远不需要
/// 读懂文件系统路径，能被读到的范围就等于这里放行过的那些。
#[tauri::command]
pub fn read_shot(flow_path: String, shot: String) -> Result<tauri::ipc::Response, ShellError> {
    let real = resolve_shot(&flow_path, &shot)?;
    let bytes = fs::read(&real)
        .map_err(|e| ShellError::new("shell.io", format!("读不了证据图 {shot}：{e}")))?;
    Ok(tauri::ipc::Response::new(bytes))
}

/// 证据图的绝对路径。界面要"用系统看图程序打开"或"在资源管理器里看"时需要它——
/// 界面自己拼不出 PATH 分隔符与绝对路径，也不该拼。
#[tauri::command]
pub fn shot_file(flow_path: String, shot: String) -> Result<String, ShellError> {
    Ok(resolve_shot(&flow_path, &shot)?.display().to_string())
}

/// 三道检查缺一不可：只认 `.png`、解析后必须真的在**流程文件所在目录**里
/// （`..` 与软链接都拦）、并且文件确实存在。
fn resolve_shot(flow_path: &str, shot: &str) -> Result<PathBuf, ShellError> {
    let root = Path::new(flow_path)
        .parent()
        .map(Path::to_path_buf)
        .unwrap_or_else(|| PathBuf::from("."));
    if !Path::new(shot)
        .extension()
        .is_some_and(|ext| ext.eq_ignore_ascii_case("png"))
    {
        return Err(ShellError::new("shot.not-png", format!("证据只认 PNG：{shot}")));
    }

    // 先拿真实路径再比前缀：只比字面路径的话，`sub/../x.png` 和软链接都能绕出去
    let real_root = fs::canonicalize(&root)
        .map_err(|e| ShellError::new("shell.io", format!("定位不了流程目录 {}：{e}", root.display())))?;
    let real = fs::canonicalize(root.join(shot))
        .map_err(|e| ShellError::new("shot.missing", format!("读不到证据图 {shot}：{e}")))?;
    if !real.starts_with(&real_root) {
        return Err(ShellError::new("shot.outside", format!("证据图不在流程文件旁边：{shot}")));
    }
    Ok(real)
}

fn short(path: &str) -> String {
    Path::new(path)
        .file_name()
        .map(|name| name.to_string_lossy().to_string())
        .unwrap_or_else(|| path.to_string())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;

    /// 每个测试一个自己的目录，跑完删掉——别把临时文件留在别人的 %TEMP% 里。
    fn scratch(name: &str) -> PathBuf {
        let dir = std::env::temp_dir().join(format!("keymouse-editor-test-{name}-{}", std::process::id()));
        let _ = fs::remove_dir_all(&dir);
        fs::create_dir_all(&dir).expect("建测试目录");
        dir
    }

    #[test]
    fn open_flow_returns_text_and_json() {
        let dir = scratch("open");
        let path = dir.join("flow.json");
        fs::write(&path, r#"{"steps":[{"type":"sleep","ms":10}]}"#).unwrap();

        let file = open_flow(path.display().to_string()).expect("应当读得动");

        assert_eq!(file.json["steps"][0]["type"], "sleep");
        assert!(file.text.contains("sleep"));
        assert_eq!(file.path, path.display().to_string());
        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn open_flow_reports_invalid_json_with_a_code() {
        let dir = scratch("bad-json");
        let path = dir.join("flow.json");
        fs::write(&path, "这不是 JSON").unwrap();

        let error = open_flow(path.display().to_string()).expect_err("应当失败");

        assert_eq!(error.code, "flow.invalid-json");
        // 报错要说清楚是哪个文件，只说"不是 JSON"没法排查
        assert!(error.message.contains("flow.json"), "消息里要带文件名：{}", error.message);
        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn save_flow_writes_exactly_what_it_was_given() {
        let dir = scratch("save");
        let path = dir.join("sub").join("flow.json");

        save_flow(path.display().to_string(), "{\n  \"steps\": []\n}\n".to_string()).expect("应当写得动");

        // 少一层目录也要自己建出来：用户另存为时常常敲一个还不存在的路径
        let written = fs::read_to_string(&path).unwrap();
        assert_eq!(written, "{\n  \"steps\": []\n}\n");
        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn open_flow_reports_missing_file_as_io() {
        let dir = scratch("missing");
        let path = dir.join("nope.json");

        let error = open_flow(path.display().to_string()).expect_err("应当失败");

        assert_eq!(error.code, "shell.io");
        let _ = fs::remove_dir_all(&dir);
    }

    /// 一张最小的 PNG（1×1，透明）：只要字节对，界面那边怎么解都是它自己的事。
    const TINY_PNG: &[u8] = &[
        0x89, b'P', b'N', b'G', 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x00, 0x00, 0x0d, b'I', b'H', b'D', b'R',
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1f, 0x15, 0xc4,
        0x89, 0x00, 0x00, 0x00, 0x0a, b'I', b'D', b'A', b'T', 0x78, 0x9c, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0d, 0x0a, 0x2d, 0xb4, 0x00, 0x00, 0x00, 0x00, b'I', b'E', b'N', b'D', 0xae,
        0x42, 0x60, 0x82,
    ];

    #[test]
    fn read_shot_serves_a_png_next_to_the_flow() {
        let dir = scratch("shot-ok");
        let flow = dir.join("flow.json");
        fs::write(&flow, "{}").unwrap();
        let shots = dir.join("flow.shots");
        fs::create_dir_all(&shots).unwrap();
        fs::write(shots.join("0001-click.png"), TINY_PNG).unwrap();

        let served = read_shot(flow.display().to_string(), "flow.shots/0001-click.png".to_string());

        assert!(served.is_ok(), "同一目录下的证据应当读得到：{:?}", served.err().map(|e| e.code));
        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn read_shot_refuses_anything_but_png() {
        let dir = scratch("shot-txt");
        let flow = dir.join("flow.json");
        fs::write(&flow, "{}").unwrap();
        fs::write(dir.join("notes.txt"), "秘密").unwrap();

        let error = read_shot(flow.display().to_string(), "notes.txt".to_string()).err().expect("应当拒绝");

        assert_eq!(error.code, "shot.not-png");
        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn read_shot_refuses_paths_outside_the_flow_directory() {
        let dir = scratch("shot-escape");
        let inner = dir.join("inner");
        fs::create_dir_all(&inner).unwrap();
        let flow = inner.join("flow.json");
        fs::write(&flow, "{}").unwrap();
        // 真实存在、但不在流程旁边：只比字面路径的实现会把它放出去
        fs::write(dir.join("outside.png"), TINY_PNG).unwrap();

        let error = read_shot(flow.display().to_string(), "../outside.png".to_string()).err().expect("应当拒绝");

        assert_eq!(error.code, "shot.outside");
        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn read_shot_reports_a_missing_file() {
        let dir = scratch("shot-missing");
        let flow = dir.join("flow.json");
        fs::write(&flow, "{}").unwrap();

        let error = read_shot(flow.display().to_string(), "flow.shots/0001-click.png".to_string())
            .err()
            .expect("应当失败");

        assert_eq!(error.code, "shot.missing");
        let _ = fs::remove_dir_all(&dir);
    }
}
