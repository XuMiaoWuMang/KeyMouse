//! 流程格式的契约：界面照它渲染，不再各写一份字段表。
//!
//! 老外壳（WinUI）是直接读 `KeyMouse.Core` 里的 `FlowSchema`；新外壳是独立进程，
//! 所以走命令行要一份 `KeyMouse schema` 的 JSON。多一次进程启动，换来的是
//! **契约只有一份**：命令行加了字段，界面下次打开就有，不需要两边同步改代码。

use std::process::Command;

use serde::{Deserialize, Serialize};

use crate::run::cli_path_of;

/// 一次进程不够快？那就记住它。契约在进程生命周期里不会变。
static CACHED: std::sync::OnceLock<String> = std::sync::OnceLock::new();

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SchemaPayload {
    /// 原始 JSON 文本，界面自己解析（这里不复制一份契约的结构）。
    pub json: String,
    /// 读它的那个命令行，出错时能说清"问的是谁"。
    pub cli_path: String,
}

#[tauri::command]
pub fn flow_schema() -> Result<SchemaPayload, String> {
    let cli = cli_path_of()?;
    if let Some(cached) = CACHED.get() {
        return Ok(SchemaPayload {
            json: cached.clone(),
            cli_path: cli.display().to_string(),
        });
    }

    let output = Command::new(&cli)
        .arg("schema")
        // 与 run.rs 同一个理由：让命令行吐 UTF-8，而不是跟着控制台码页走。
        .env("KEYMOUSE_OUTPUT_ENCODING", "utf-8")
        .output()
        .map_err(|e| format!("调不动 {}：{e}", cli.display()))?;

    if !output.status.success() {
        return Err(format!(
            "{} schema 失败（退出码 {:?}）：{}",
            cli.display(),
            output.status.code(),
            String::from_utf8_lossy(&output.stderr).trim()
        ));
    }

    let json = String::from_utf8(output.stdout)
        .map_err(|_| "schema 的输出不是 UTF-8".to_string())?;
    if !json.trim_start().starts_with('{') {
        return Err(format!("schema 的输出不像 JSON：{}", json.chars().take(80).collect::<String>()));
    }

    let _ = CACHED.set(json.clone());
    Ok(SchemaPayload {
        json,
        cli_path: cli.display().to_string(),
    })
}
