//! Windows 上让“运行日志 HUD”真的像一层桌面覆盖物。
//!
//! 这些都是 Tauri 不会替你设、但桌面挂件类程序必须设的位：
//! * `WS_EX_TOOLWINDOW`：不出现在 Alt+Tab / 任务栏切换列表里。
//! * `WS_EX_NOACTIVATE`：鼠标点上去不夺走用户正在操作的窗口的键盘焦点。
//! * `WS_EX_TRANSPARENT`：命中测试直接穿过，HUD 不接收任何点击。
//! * `WDA_EXCLUDEFROMCAPTURE`：HUD 仍显示给用户，但扫屏 / 截图 API 看不到它。
//!   OCR 读的是屏幕像素，如果日志窗被一起抓进去，步骤文字就可能被当成目标内容。
//!
//! 旧浮条曾经需要点击，所以撤掉过 `WS_EX_NOACTIVATE`；现在的 HUD 是只读的，
//! 既没有按钮也没有拖拽，正应该把鼠标事件全部让给底下的程序。
//!
//! * `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)`：每次显示都重新插到置顶带最上。
//!   只设一次 `always_on_top` 的话，窗口只是“身在置顶带”，被别的置顶窗盖住就再也回不来。
//!
//! 做法参考了桌宠项目 `https://github.com/MerZlin/dsh-pet-indesktop`
//! （`pet/platform_win.py` 里的 `_set_windows_no_activate` / `_set_windows_topmost`），
//! 它把这几处踩过的问题写得很清楚（issue #98 就是“点桌宠导致复制粘贴失效”）。
//!
//! 只在 Windows 上编译；其它平台是空实现，调用处不用 cfg。

#[cfg(windows)]
mod imp {
    use std::ffi::c_void;

    use windows_sys::Win32::Foundation::HWND;
    use windows_sys::Win32::UI::WindowsAndMessaging::{
        GetWindowLongW, SetWindowDisplayAffinity, SetWindowLongW, SetWindowPos, GWL_EXSTYLE,
        HWND_TOPMOST, SWP_NOACTIVATE, SWP_NOMOVE, SWP_NOSIZE, WDA_EXCLUDEFROMCAPTURE,
        WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW, WS_EX_TRANSPARENT,
    };

    /// 把窗口样式改成“不抢焦点、鼠标穿透、不进 Alt+Tab”，并把 z 序重新插到最上。
    ///
    /// 返回 `true` 表示这次真的动了样式位（方便调用处打日志/写测试断言）。
    pub fn make_overlay(window: &tauri::WebviewWindow) -> bool {
        let handle = match window.hwnd() {
            Ok(handle) => handle,
            Err(error) => {
                crate::crashlog::write(&format!("hud overlay：hwnd 拿不到（{error}）"));
                return false;
            }
        };
        // tauri 用的 `windows` crate 与本项目的 `windows-sys` 对 HWND 的封装不同，
        // 一律转成裸指针再转回来，免得绑死某一版的类型定义。
        let hwnd = handle.0 as *mut c_void as HWND;

        // 先改样式位（不重建原生窗口），再重排 z 序。
        let style = unsafe { GetWindowLongW(hwnd, GWL_EXSTYLE) } as u32;
        let wanted = style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT;
        let mut changed = false;
        if wanted != style {
            unsafe { SetWindowLongW(hwnd, GWL_EXSTYLE, wanted as i32) };
            changed = true;
        }
        // 让 OCR / 截图 API 看不到 HUD：用户需要看见进度，但读屏不能把日志字当成目标内容。
        // Win10 2004+ 才有 WDA_EXCLUDEFROMCAPTURE；旧系统返回 false，HUD 仍会出现在截图里。
        let excluded = unsafe { SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE) };
        // SWP_NOACTIVATE：只改 z 序，不夺走用户正在用的那个窗口的键盘焦点。
        unsafe {
            SetWindowPos(
                hwnd,
                HWND_TOPMOST,
                0,
                0,
                0,
                0,
                SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE,
            )
        };
        crate::crashlog::write(&format!(
            "hud overlay：hwnd=0x{:X} ex=0x{style:08X} -> 0x{wanted:08X} changed={changed} excluded={excluded}",
            hwnd as usize
        ));
        changed
    }
}

#[cfg(not(windows))]
mod imp {
    pub fn make_overlay(_window: &tauri::WebviewWindow) -> bool {
        false
    }
}

pub use imp::make_overlay;
