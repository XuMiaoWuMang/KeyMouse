//! 外壳与界面之间的全部接口。
//!
//! 契约写在 `docs/reference/editor-ipc.md`：参数、返回值、错误码、界面怎么用。
//! 改这里的任何一条，先去改那份文档。

use std::fs;
use std::path::Path;

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
}
