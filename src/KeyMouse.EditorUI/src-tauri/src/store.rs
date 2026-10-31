// 界面自己的小账本：最近打开过的流程、上次的窗口大小。
//
// 这份数据只影响"下次打开时顺手一点"，坏了也不该让编辑器起不来——所以读写都吞错、
// 读不到就当没有。它跟流程文件没关系，落在用户目录里，不进仓库。

use std::fs;
use std::path::{Path, PathBuf};

use serde::{Deserialize, Serialize};

/// 最近文件最多记这么多。老外壳也是 8 条：再多就没人往下看了。
const RECENT_LIMIT: usize = 8;

/// 长按识别的缺省阈值。400 ms 太容易误触、800 ms 又像卡住，取中间。
pub const DEFAULT_LONG_PRESS_MS: u32 = 500;

/// 阈值给个上下限：太小等于单击触发两次，太大等于永远按不出来。
pub const MIN_LONG_PRESS_MS: u32 = 150;
pub const MAX_LONG_PRESS_MS: u32 = 3000;

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
    /// 每个流程文件的纯界面分组：键是文件绝对路径，值是按 `at` 排好的边界。
    ///
    /// 它**不进流程 JSON**：换台机器、换一版命令行，那份文件照样能跑——分组只是编辑器
    /// 自己为了看得顺而加的抽屉。文件改名/移动会丢，这个代价写在这里，不打算兜。
    #[serde(default, skip_serializing_if = "std::collections::BTreeMap::is_empty")]
    pub sections: std::collections::BTreeMap<String, Vec<Section>>,
    /// 长按识别阈值（毫秒）。界面可以改；缺省 500。
    #[serde(default = "default_long_press_ms")]
    pub long_press_ms: u32,
}

/// 一个分组头：在第 `at` 个步骤之前开一组，管到下一个边界之前。
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct Section {
    pub at: usize,
    #[serde(default)]
    pub title: String,
}

fn default_long_press_ms() -> u32 {
    DEFAULT_LONG_PRESS_MS
}

impl Store {
    /// 补上 `#[derive(Default)]` 给不了的那两个默认值。
    fn normalize_defaults(&mut self) {
        if self.long_press_ms == 0 {
            self.long_press_ms = DEFAULT_LONG_PRESS_MS;
        }
    }
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
    let mut store: Store = serde_json::from_str(&text).unwrap_or_default();
    store.normalize_defaults();
    store
}

/// 写账本。**故意不返回错误**：记不住最近文件不是用户的问题。
pub fn save(store: &Store) {
    let path = store_path();
    if let Some(parent) = path.parent() {
        let _ = fs::create_dir_all(parent);
    }
    let mut copy = Store {
        recent: store.recent.clone(),
        window: store.window,
        sections: store.sections.clone(),
        long_press_ms: store.long_press_ms,
    };
    copy.normalize_defaults();
    if let Ok(text) = serde_json::to_string_pretty(&copy) {
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

/// 一份流程文件对应账本里的哪个键。
///
/// 用规范化后的**绝对路径**：界面上 `samples\demo-flow.json` 与
/// `D:\Data\KeyMouse\samples\demo-flow.json` 说的是同一份文件，键也该是一个。
pub fn sections_key(path: &str) -> String {
    normalize(path)
}

/// 把界面递过来的分组边界捋顺：丢掉空的标题、同一个 `at` 只留第一个、按位置排序。
///
/// 越界的边界在这里不管（只有界面知道现在有几行）——界面会先拿 `steps.length` 截一道，
/// 这里再兜也是同一件事。
pub fn tidy_sections(sections: &[Section]) -> Vec<Section> {
    let mut out: Vec<Section> = Vec::with_capacity(sections.len());
    for section in sections {
        if out.iter().any(|kept| kept.at == section.at) {
            continue;
        }
        let title = section.title.trim();
        out.push(Section {
            at: section.at,
            title: if title.is_empty() { format!("分组 {}", out.len() + 1) } else { title.to_string() },
        });
    }
    out.sort_by_key(|section| section.at);
    out
}

/// 某个流程文件的分组边界。没记过就是空数组（不是错误）。
#[tauri::command]
pub fn load_sections(path: String) -> Vec<Section> {
    let store = load();
    store.sections.get(&sections_key(&path)).cloned().unwrap_or_default()
}

/// 记下某个流程文件的分组边界，返回捋顺之后的列表（界面拿去重画）。
#[tauri::command]
pub fn save_sections(path: String, sections: Vec<Section>) -> Vec<Section> {
    let tidied = tidy_sections(&sections);
    let mut store = load();
    let key = sections_key(&path);
    if tidied.is_empty() {
        store.sections.remove(&key);
    } else {
        store.sections.insert(key, tidied.clone());
    }
    save(&store);
    tidied
}

/// 长按阈值（毫秒）。
#[tauri::command]
pub fn long_press_ms() -> u32 {
    load().long_press_ms
}

/// 改长按阈值。超出 [`MIN_LONG_PRESS_MS`, `MAX_LONG_PRESS_MS`] 的夹回边界，返回生效值。
#[tauri::command]
pub fn save_long_press_ms(ms: u32) -> u32 {
    let value = ms.clamp(MIN_LONG_PRESS_MS, MAX_LONG_PRESS_MS);
    let mut store = load();
    store.long_press_ms = value;
    save(&store);
    value
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
            sections: std::collections::BTreeMap::new(),
            long_press_ms: DEFAULT_LONG_PRESS_MS,
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

    #[test]
    fn sections_are_sorted_without_duplicates() {
        let tidied = tidy_sections(&[
            Section { at: 4, title: "尾巴".into() },
            Section { at: 0, title: "开头".into() },
            Section { at: 4, title: "重复".into() },
        ]);
        assert_eq!(
            tidied,
            vec![Section { at: 0, title: "开头".into() }, Section { at: 4, title: "尾巴".into() }]
        );
    }

    #[test]
    fn an_empty_title_becomes_a_numbered_one() {
        let tidied = tidy_sections(&[Section { at: 2, title: "   ".into() }]);
        assert_eq!(tidied[0].title, "分组 1");
    }

    #[test]
    fn an_old_store_without_the_new_fields_still_loads() {
        // 老账本里没有 sections / longPressMs：读出来要有一份能用的默认值。
        let store: Store =
            serde_json::from_str(r#"{"recent":["D:/a.json"],"window":{"width":892,"height":632,"x":1,"y":2}}"#)
                .expect("old store parses");
        assert!(store.sections.is_empty());
        assert_eq!(store.long_press_ms, DEFAULT_LONG_PRESS_MS);
    }

    #[test]
    fn the_long_press_threshold_is_clamped() {
        assert_eq!(1u32.clamp(MIN_LONG_PRESS_MS, MAX_LONG_PRESS_MS), MIN_LONG_PRESS_MS);
        assert_eq!(99999u32.clamp(MIN_LONG_PRESS_MS, MAX_LONG_PRESS_MS), MAX_LONG_PRESS_MS);
    }
}
