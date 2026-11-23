//! 播放与普通录制共用的全局停止键 `Ctrl+Alt+Q`。
//!
//! 独立消息线程注册系统热键，主窗口隐藏、失焦时仍能叫停。
//! 播放调用 `run_cancel`；普通录制调用 `record_stop`，先通知命令行写盘。
//! 提权录制由命令行自己的钩子处理，普通外壳不接管它的停止。

use std::sync::mpsc;
use std::thread::{self, JoinHandle};

use tauri::AppHandle;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Action {
    Playback,
    Recording,
}

#[cfg(windows)]
mod imp {
    use super::*;
    use windows_sys::Win32::Foundation::WPARAM;
    use windows_sys::Win32::System::Threading::GetCurrentThreadId;
    use windows_sys::Win32::UI::Input::KeyboardAndMouse::{
        RegisterHotKey, UnregisterHotKey, MOD_ALT, MOD_CONTROL, MOD_NOREPEAT, VK_Q,
    };
    use windows_sys::Win32::UI::WindowsAndMessaging::{
        GetMessageW, PeekMessageW, PostThreadMessageW, MSG, PM_NOREMOVE, WM_HOTKEY, WM_QUIT,
    };

    const HOTKEY_ID: i32 = 0x4B4D;

    pub struct Handle {
        thread_id: u32,
        join: Option<JoinHandle<()>>,
    }

    impl Drop for Handle {
        fn drop(&mut self) {
            unsafe {
                PostThreadMessageW(self.thread_id, WM_QUIT, 0, 0);
            }
            if let Some(join) = self.join.take() {
                let _ = join.join();
            }
        }
    }

    pub fn start(app: AppHandle, action: Action) -> Result<Handle, String> {
        start_handler(VK_Q as u32, move || match action {
            Action::Playback => {
                let _ = crate::run::run_cancel(app);
            }
            Action::Recording => {
                if let Err(error) = crate::record::record_stop(app) {
                    crate::crashlog::write(&format!("录制停止键处理失败：{error}"));
                }
            }
        })
    }

    fn start_handler(key: u32, stop: impl FnOnce() + Send + 'static) -> Result<Handle, String> {
        let (ready_tx, ready_rx) = mpsc::channel::<Result<u32, String>>();
        let join = thread::spawn(move || {
            let mut message: MSG = unsafe { std::mem::zeroed() };
            // 在发 ready 前建好消息队列，立即停止也能投递 WM_QUIT。
            unsafe { PeekMessageW(&mut message, std::ptr::null_mut(), 0, 0, PM_NOREMOVE); }
            let registered = unsafe {
                RegisterHotKey(
                    std::ptr::null_mut(), HOTKEY_ID,
                    MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, key,
                )
            };
            if registered == 0 {
                let _ = ready_tx.send(Err("全局停止键 Ctrl+Alt+Q 被别的程序占用了".to_string()));
                return;
            }
            let thread_id = unsafe { GetCurrentThreadId() };
            if ready_tx.send(Ok(thread_id)).is_err() {
                // 调用方已超时离开，不能泄漏占用全局组合键的线程。
                unsafe { UnregisterHotKey(std::ptr::null_mut(), HOTKEY_ID); }
                return;
            }

            let mut stop = Some(stop);
            loop {
                let result = unsafe { GetMessageW(&mut message, std::ptr::null_mut(), 0, 0) };
                if result <= 0 { break; }
                if message.message == WM_HOTKEY && message.wParam == HOTKEY_ID as WPARAM {
                    if let Some(stop) = stop.take() {
                        // 收尾线程注销热键后才释放 child 槽位；record_stop 会等该槽位。
                        // 必须离开消息线程执行，避免两边互等；每轮只派发一次停止。
                        thread::spawn(stop);
                    }
                }
            }
            unsafe { UnregisterHotKey(std::ptr::null_mut(), HOTKEY_ID); }
        });

        match ready_rx.recv_timeout(std::time::Duration::from_secs(2)) {
            Ok(Ok(thread_id)) => Ok(Handle { thread_id, join: Some(join) }),
            Ok(Err(error)) => {
                let _ = join.join();
                Err(error)
            }
            Err(_) => Err("全局停止键注册超时，没有启动".to_string()),
        }
    }

    #[cfg(test)]
    mod tests {
        use super::*;
        use std::time::Duration;
        use windows_sys::Win32::UI::Input::KeyboardAndMouse::VK_F24;

        #[test]
        fn cleanup_does_not_wait_for_recording_stop_handler() {
            let (entered_tx, entered_rx) = mpsc::channel();
            let (release_tx, release_rx) = mpsc::channel();
            let handle = start_handler(VK_F24 as u32, move || {
                entered_tx.send(()).unwrap();
                // 模拟 record_stop 等待 run-exit 清理槽位。
                let _ = release_rx.recv_timeout(Duration::from_secs(3));
            }).unwrap();
            unsafe { PostThreadMessageW(handle.thread_id, WM_HOTKEY, HOTKEY_ID as WPARAM, 0); }
            entered_rx.recv_timeout(Duration::from_secs(1)).unwrap();
            let (cleaned_tx, cleaned_rx) = mpsc::channel();
            let cleanup = thread::spawn(move || {
                drop(handle);
                cleaned_tx.send(()).unwrap();
            });
            let cleaned = cleaned_rx.recv_timeout(Duration::from_millis(500)).is_ok();
            let _ = release_tx.send(());
            cleanup.join().unwrap();
            assert!(cleaned, "注销热键不应等录制停止处理结束，否则与子进程收尾互相等待");

            // 同一组合键可以用于下一轮，确认上一轮确实注销。
            drop(start_handler(VK_F24 as u32, || {}).unwrap());
        }
    }
}

#[cfg(not(windows))]
mod imp {
    use super::*;
    pub struct Handle;
    pub fn start(_app: AppHandle, _action: Action) -> Result<Handle, String> {
        Err("当前平台没有全局停止键；播放需要手动停止".to_string())
    }
}

pub use imp::{start, Handle};
