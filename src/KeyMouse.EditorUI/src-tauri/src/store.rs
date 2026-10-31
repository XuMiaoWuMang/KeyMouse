// 界面自己的小账本：最近打开过的流程、上次的窗口大小。
//
// 这份数据只影响"下次打开时顺手一点"，坏了也不该让编辑器起不来——所以读写都吞错、
// 读不到就当没有。它跟流程文件没关系，落在用户目录里，不进仓库。

use std::fs;
use std::path::{Path, PathBuf};

use serde::{Deserialize, Serialize};

/// 最近文件最多记这么多。老外壳也是 8 条：再多就没人往下看了。
const RECENT_LIMIT: usize = 8;

#[derive(Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Store {
    /// 最近的流程文件，越靠前越新。
    #[serde(default)]
    pub recent: Vec<String>,
    /// 上次关闭时的窗口位置与尺寸（尺寸是逻辑像素，位置是物理像素——Windows 的
    /// `outer_position` 就是这么给的，换算成逻辑反而要赌它落在哪块屏）。
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub window: Option<WindowState>,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub struct WindowState {
    pub width: f64,
    pub height: f64,
    /// 老账本里没有这两个字段：缺了就当没有位置记忆，别让整份设置读不出来。
    #[serde(default)]
    /// 物理像素，与 outer_position() / set_position(PhysicalPosition) 成对使用。
    pub x: i32,
    #[serde(default)]
    pub y: i32,
}

/// 账本的位置：`%LOCALAPPDATA%\KeyMouse\editor-settings.json`。
///
/// 拿不到 LOCALAPPDATA（极少见）就退到程序目录——读不到变量时宁可写在自己旁边，
/// 也不要让"记不住最近文件"变成"打不开界面"。
pub fn store_path() -> PathBuf {
    let base = std::env::var_os("LOCALAPPDATA")
        .map(PathBuf::from)
        .unwrap_or_else(|| std::env::current_exe().unwrap_or_default().parent().map(Path::to_path_buf).unwrap_or_default());
    base.join("KeyMouse").join("editor-settings.json")
}

/// 读账本。文件不在、内容坏了、读不动，一律给空账本。
pub fn load() -> Store {
    let path = store_path();
    let Ok(text) = fs::read_to_string(&path) else {
        return Store::default();
    };
    serde_json::from_str(&text).unwrap_or_default()
}

/// 写账本。**故意不返回错误**：记不住最近文件不是用户的问题。
pub fn save(store: &Store) {
    let path = store_path();
    if let Some(parent) = path.parent() {
        let _ = fs::create_dir_all(parent);
    }
    if let Ok(text) = serde_json::to_string_pretty(store) {
        let _ = fs::write(&path, text + "\n");
    }
}

/// 把一个路径提到最近列表最前面。
///
/// 规则：同一个路径只留一条（比较时先规范化，避免 `D:\a\..\a\b.json` 这种形状重复）；
/// 超过 [`RECENT_LIMIT`] 丢最旧的。返回新的列表，方便界面直接用。
pub fn remember(store: &mut Store, path: &str) -> Vec<String> {
    let normalized = normalize(path);
    store.recent.retain(|item| normalize(item) != normalized);
    store.recent.insert(0, path.to_string());
    store.recent.truncate(RECENT_LIMIT);
    store.recent.clone()
}

/// 只把还存在的文件给界面：最近列表里常有已经删掉/挪走的条目。
pub fn existing(items: &[String]) -> Vec<String> {
    items.iter().filter(|item| Path::new(item).is_file()).cloned().collect()
}

fn normalize(path: &str) -> String {
    fs::canonicalize(path)
        .map(|p| p.to_string_lossy().to_lowercase())
        .unwrap_or_else(|_| path.replace('/', "\\").to_lowercase())
}

/// 关窗时把内尺寸记进账本。在 Rust 侧做而不是让界面调用：界面可能已经没了。
pub fn remember_size(window: &tauri::Window) {
    let Ok(size) = window.inner_size() else {
        return;
    };
    let scale = window.scale_factor().unwrap_or(1.0);
    let logical = size.to_logical::<f64>(scale);
    let mut state = load();
    if logical.width < 200.0 || logical.height < 120.0 {
        return;
    }
    let position = window.outer_position().unwrap_or(tauri::PhysicalPosition { x: 0, y: 0 });
    state.window = Some(WindowState {
        width: logical.width,
        height: logical.height,
        x: position.x,
        y: position.y,
    });
    save(&state);
}
/// 最近打开过的流程（只给还存在的），新的在前。
#[tauri::command]
pub fn recent_files() -> Vec<String> {
    existing(&load().recent)
}

/// 记一条最近文件，返回新的列表（界面直接拿去重画）。
#[tauri::command]
pub fn remember_recent(path: String) -> Vec<String> {
    let mut store = load();
    let list = remember(&mut store, &path);
    save(&store);
    existing(&list)
}

/// 上次关闭时的窗口位置与尺寸；没记过就给 `null`。
#[tauri::command]
pub fn window_size() -> Option<WindowState> {
    load().window
}

/// 记下这次的窗口位置与尺寸（尺寸逻辑像素、位置物理像素）。关窗前调用。
#[tauri::command]
pub fn save_window_size(width: f64, height: f64, x: Option<i32>, y: Option<i32>) {
    if width < 200.0 || height < 120.0 {
        // 迷你模式那种一条细带的尺寸记下来毫无意义，下次开就是一条缝。
        return;
    }
    let mut store = load();
    let (x, y) = match (x, y) {
        (Some(x), Some(y)) => (x, y),
        _ => store.window.map(|w| (w.x, w.y)).unwrap_or((0, 0)),
    };
    store.window = Some(WindowState { width, height, x, y });
    save(&store);
}
#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn remember_puts_the_newest_first_and_drops_duplicates() {
        let mut store = Store::default();
        remember(&mut store, "D:/flows/a.json");
        remember(&mut store, "D:/flows/b.json");
        remember(&mut store, "D:/flows/a.json");
        assert_eq!(store.recent, vec!["D:/flows/a.json", "D:/flows/b.json"]);
    }

    #[test]
    fn remember_keeps_at_most_eight() {
        let mut store = Store::default();
        for i in 0..12 {
            remember(&mut store, &format!("D:/flows/{i}.json"));
        }
        assert_eq!(store.recent.len(), RECENT_LIMIT);
        assert_eq!(store.recent[0], "D:/flows/11.json");
    }

    #[test]
    fn store_survives_a_round_trip() {
        let original = Store {
            recent: vec!["D:/flows/a.json".into()],
            window: Some(WindowState { width: 892.0, height: 632.0, x: 120, y: 80 }),
        };
        let text = serde_json::to_string(&original).expect("serialize");
        let back: Store = serde_json::from_str(&text).expect("deserialize");
        assert_eq!(back.recent, original.recent);
        assert_eq!(back.window.map(|s| s.width), Some(892.0));
    }

    #[test]
    fn a_broken_store_reads_as_empty() {
        let store: Store = serde_json::from_str("{ not json").unwrap_or_default();
        assert!(store.recent.is_empty());
        assert!(store.window.is_none());
    }
}
