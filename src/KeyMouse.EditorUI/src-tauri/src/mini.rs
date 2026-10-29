//! 录制时的迷你模式。
//!
//! 录制期间用户要操作**别的**窗口，编辑器自己那两栏就成了障碍：窗口一被盖住，
//! 工具栏上的「停止」就够不着了。所以录制一开始就把主窗口缩成一条细带
//! （置顶、可拖、上面一个「停止」），录完再还原。
//!
//! 为什么不是第二块悬浮窗口：Tauri 的第二块 webview 在本机实测**不合成**
//! （data: URL 也是空白），而老外壳的 `NativeIsland` 是自绘的 Win32 窗口。
//! 与其在这个壳里再造一个自绘窗口，不如先把"够得着"这件事用已经验证过的
//! webview 做出来；ADR 0001 的未决问题 6 记着这条取舍。

use std::sync::Mutex;

use tauri::{LogicalSize, PhysicalPosition, Window};

/// 迷你带的高度（逻辑像素）。宽度跟着主窗口缩到同一条。
const MINI_HEIGHT: f64 = 52.0;

/// 还原用的原样。
#[derive(Clone, Copy)]
struct Saved {
    width: f64,
    height: f64,
    x: i32,
    y: i32,
    on_top: bool,
}

/// 迷你模式的状态。只可能有一份，所以直接放在托管状态里。
#[derive(Default)]
pub struct MiniState {
    saved: Mutex<Option<Saved>>,
}

/// 进入迷你模式：记住原样，再缩成一条细带。
///
/// 位置**不动**：窗口就地变矮，比"缩完跳到屏幕角落"更不晃眼。
#[tauri::command]
pub fn enter_mini(window: Window, state: tauri::State<'_, MiniState>) -> Result<(), String> {
    let mut saved = state.saved.lock().map_err(|e| e.to_string())?;
    if saved.is_some() {
        return Ok(());
    }

    let scale = window.scale_factor().map_err(|e| e.to_string())?;
    let size = window.inner_size().map_err(|e| e.to_string())?;
    let position = window.outer_position().map_err(|e| e.to_string())?;
    let on_top = window.is_always_on_top().unwrap_or(false);

    *saved = Some(Saved {
        width: size.width as f64 / scale,
        height: size.height as f64 / scale,
        x: position.x,
        y: position.y,
        on_top,
    });

    window
        .set_always_on_top(true)
        .map_err(|e| e.to_string())?;
    window
        .set_size(LogicalSize::new(420.0, MINI_HEIGHT))
        .map_err(|e| e.to_string())?;
    Ok(())
}

/// 退出迷你模式：把窗口还原成进迷你之前的样子。
#[tauri::command]
pub fn leave_mini(window: Window, state: tauri::State<'_, MiniState>) -> Result<(), String> {
    let mut saved = state.saved.lock().map_err(|e| e.to_string())?;
    let Some(original) = saved.take() else {
        return Ok(());
    };

    window
        .set_size(LogicalSize::new(original.width, original.height))
        .map_err(|e| e.to_string())?;
    window
        .set_position(PhysicalPosition::new(original.x, original.y))
        .map_err(|e| e.to_string())?;
    window
        .set_always_on_top(original.on_top)
        .map_err(|e| e.to_string())?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn mini_height_leaves_room_for_a_button() {
        // 52 是"一行文字 + 一个 28px 按钮"的下限；比这矮就得砍掉文字
        assert!(MINI_HEIGHT >= 48.0, "再矮按钮就放不下了");
        assert!(MINI_HEIGHT <= 64.0, "再高就不像一条细带了");
    }
}
