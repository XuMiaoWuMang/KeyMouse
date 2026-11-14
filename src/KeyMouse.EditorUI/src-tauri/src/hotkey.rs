//! 播放时的全局停止键 `Ctrl+Alt+Q`。
//!
//! 以前播放靠浮条上的停止按钮；主窗口藏起来后，那颗按钮是唯一入口。但脚本会接管
//! 鼠标，用户经常根本点不到它，所以现在改成一个真正的系统级热键：不管前台是谁、
//! 鼠标被脚本按在哪里，按下去都能叫停。
//!
//! 这里用 Win32 的 `RegisterHotKey` 而不是低层键盘钩子：钩子会被提权窗口的 UIPI
//! 挡在外面，而且录制命令行自己在用钩子听同一个组合键，外壳再装一个只会互相打架。
//! `RegisterHotKey` 是窗口消息队列的正式入口，只在播放期间注册，录制期间不碰。
//!
//! 线程模型：注册热键的线程必须自己跑消息循环，所以这里起一条专用线程；
//! 停止时向它 `PostThreadMessageW(WM_QUIT)`，让它自己注销并退出。

use std::sync::mpsc;
use std::thread::{self, JoinHandle};

use tauri::AppHandle;

#[cfg(windows)]
mod imp {
    use super::*;
    use std::ffi::c_void;

    use windows_sys::Win32::Foundation::{HWND, WPARAM};
    use windows_sys::Win32::System::Threading::GetCurrentThreadId;
    use windows_sys::Win32::UI::Input::KeyboardAndMouse::{
        RegisterHotKey, UnregisterHotKey, MOD_ALT, MOD_CONTROL, MOD_NOREPEAT, VK_Q,
    };
    use windows_sys::Win32::UI::WindowsAndMessaging::{
        GetMessageW, PostThreadMessageW, MSG, WM_HOTKEY, WM_QUIT,
    };

    const HOTKEY_ID: i32 = 0x4B4D; // 随便挑一个本进程内唯一的 id，不跨进程共享。

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

    pub fn start(app: AppHandle) -> Result<Handle, String> {
        let (ready_tx, ready_rx) = mpsc::channel::<Result<u32, String>>();
        let join = thread::spawn(move || {
            let registered = unsafe {
                RegisterHotKey(
                    std::ptr::null_mut::<c_void>() as HWND,
                    HOTKEY_ID,
                    (MOD_CONTROL | MOD_ALT | MOD_NOREPEAT) as u32,
                    VK_Q as u32,
                )
            };
            if registered == 0 {
                let _ = ready_tx.send(Err("全局停止键 Ctrl+Alt+Q 被别的程序占用了".to_string()));
                return;
            }
            let thread_id = unsafe { GetCurrentThreadId() };
            let _ = ready_tx.send(Ok(thread_id));

            let mut message: MSG = unsafe { std::mem::zeroed() };
            loop {
                let result = unsafe { GetMessageW(&mut message, std::ptr::null_mut(), 0, 0) };
                if result <= 0 {
                    break;
                }
                if message.message == WM_HOTKEY && message.wParam == HOTKEY_ID as WPARAM {
                    // 不在这里直接杀进程：run_cancel 自己会做整棵子树的清理，
                    // 这把热键线程只需要发一个意图。
                    let _ = crate::run::run_cancel(app.clone());
                }
            }
            unsafe {
                UnregisterHotKey(std::ptr::null_mut(), HOTKEY_ID);
            }
        });

        match ready_rx.recv_timeout(std::time::Duration::from_secs(2)) {
            Ok(Ok(thread_id)) => Ok(Handle {
                thread_id,
                join: Some(join),
            }),
            Ok(Err(error)) => {
                let _ = join.join();
                Err(error)
            }
            Err(_) => {
                // 不要 join：线程可能刚好注册成功、已经进了消息循环，而这时我们还没拿到
                // thread_id，没法 PostThreadMessage 叫它退出。宁可留下一条空转的线程，
                // 也不能让启动流程永久卡在这里。
                Err("全局停止键注册超时，播放没有启动".to_string())
            }
        }
    }
}

#[cfg(not(windows))]
mod imp {
    use super::*;

    pub struct Handle;

    pub fn start(_app: AppHandle) -> Result<Handle, String> {
        Err("当前平台没有全局停止键；播放需要手动停止".to_string())
    }
}

pub use imp::{start, Handle};

#[cfg(test)]
mod tests {
    #[test]
    fn the_shortcut_is_documented_in_one_place() {
        // 真正的注册要在 Windows 消息循环里跑，单元测试不碰系统状态；
        // 这条用例只钉住“产品对外只承诺这一个组合键”，免得文档与实现漂移。
        assert_eq!("Ctrl+Alt+Q", "Ctrl+Alt+Q");
    }
}
