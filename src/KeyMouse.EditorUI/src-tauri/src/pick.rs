//! 从屏幕上取一点 / 取一块（干活的还是命令行，这里只负责等它、把结果翻译成界面能用的形状）。
//!
//! 为什么不在壳里自己画十字线：取区域的浮层、坐标换算、窗口归属这些判断都在
//! `KeyMouse.Core` 的 `region pick` 里，而且已经有一堆实测结论（客户区 / 窗口 / 屏幕三种空间、
//! 标题栏那种边上情况）。壳再实现一遍，就是第二份会走样的规矩。


use serde::{Deserialize, Serialize};

use crate::run::cli_path_of;

/// 命令行 `region pick --json` 的形状（只声明界面要用的字段）。
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct RawPick {
    ok: bool,
    #[serde(default)]
    space: Option<String>,
    #[serde(default)]
    region: Option<Vec<i64>>,
    #[serde(default)]
    window: Option<RawWindow>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct RawWindow {
    #[serde(default)]
    process: Option<String>,
    #[serde(default)]
    title: Option<String>,
    #[serde(default)]
    class: Option<String>,
    #[serde(default)]
    handle: Option<String>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PickResult {
    /// 'client' | 'window' | 'screen'
    pub space: String,
    pub x: i64,
    pub y: i64,
    pub width: i64,
    pub height: i64,
    /// 选中的窗口（`region pick` 总会有，取点也算）
    pub process: Option<String>,
    pub title: Option<String>,
    pub class: Option<String>,
    pub handle: Option<String>,
}

fn pick_args(point: bool) -> Vec<String> {
    let mut args = vec!["region".to_string(), "pick".to_string()];
    if point {
        args.push("--point".to_string());
    }
    args.push("--json".to_string());
    args
}

fn parse(raw: &str) -> Result<PickResult, String> {
    let picked: RawPick = serde_json::from_str(raw).map_err(|e| format!("读不懂 pick 的输出：{e}"))?;
    if !picked.ok {
        return Err("取区域没成功".to_string());
    }
    let region = picked.region.ok_or_else(|| "pick 没给区域".to_string())?;
    if region.len() < 4 {
        return Err("pick 给的区域不完整".to_string());
    }
    let window = picked.window;
    Ok(PickResult {
        space: picked.space.unwrap_or_else(|| "screen".to_string()),
        x: region[0],
        y: region[1],
        width: region[2],
        height: region[3],
        process: window.as_ref().and_then(|w| w.process.clone()),
        title: window.as_ref().and_then(|w| w.title.clone()),
        class: window.as_ref().and_then(|w| w.class.clone()),
        handle: window.as_ref().and_then(|w| w.handle.clone()),
    })
}

/// 拉起浮层取一点或取一块。`Ok(None)` = 用户按了 Esc（命令行退出码 3，不是错）。
#[tauri::command]
pub async fn pick_region(point: bool) -> Result<Option<PickResult>, String> {
    let args = pick_args(point);
    let picked = tauri::async_runtime::spawn_blocking(move || {
        let cli = cli_path_of()?;
        let output = crate::process::cli_command(&cli)
            .args(&args)
            .env("KEYMOUSE_OUTPUT_ENCODING", "utf-8")
            .output()
            .map_err(|e| format!("调不动 {}：{e}", cli.display()))?;
        Ok::<_, String>((output.status.code(), output))
    })
    .await
    .map_err(|e| format!("取区域的线程挂了：{e}"))??;

    let (code, output) = picked;
    let stdout = String::from_utf8_lossy(&output.stdout).to_string();
    match code {
        Some(0) => parse(&stdout).map(Some),
        // 3 = 用户取消（`region pick` 的约定），不是错误。
        Some(3) => Ok(None),
        other => Err(format!(
            "取区域失败（退出码 {:?}）：{}",
            other,
            String::from_utf8_lossy(&output.stderr).trim()
        )),
    }
}

#[cfg(test)]
mod tests {
    use super::{parse, pick_args};

    const SAMPLE: &str = r#"{
        "ok": true,
        "screen": { "rect": [100, 200, 300, 40] },
        "window": { "handle": "0x20378", "title": "记事本", "pid": 9140, "process": "notepad", "class": "Notepad" },
        "space": "client",
        "region": [100, 200, 300, 40],
        "probe": "KeyMouse probe --hwnd 0x20378 --region 100,200,300,40"
    }"#;

    #[test]
    fn pick_args_ask_for_json_and_point_only_when_asked() {
        assert_eq!(pick_args(false), vec!["region", "pick", "--json"]);
        assert_eq!(pick_args(true), vec!["region", "pick", "--point", "--json"]);
    }

    #[test]
    fn parse_reads_region_window_and_process() {
        let picked = parse(SAMPLE).expect("样例应当能读");
        assert_eq!((picked.x, picked.y, picked.width, picked.height), (100, 200, 300, 40));
        assert_eq!(picked.space, "client");
        assert_eq!(picked.process.as_deref(), Some("notepad"));
        assert_eq!(picked.handle.as_deref(), Some("0x20378"));
    }

    #[test]
    fn parse_reports_a_broken_payload() {
        assert!(parse("{}").is_err());
        assert!(parse("not json").is_err());
    }
}
