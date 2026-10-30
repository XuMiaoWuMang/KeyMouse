//! 起命令行附属进程的唯一入口。
//!
//! 为什么单独一个模块：`KeyMouse.exe` 是控制台程序，用 `Command::new` 直接起会**弹一个黑框**。
//! 界面上每次打开文件、取点、跑流程、读契约都要起它，于是启动和操作时不断有窗口闪一下。
//! 这里统一加 `CREATE_NO_WINDOW`，并且今后只从这里起进程。

use std::process::Command;

/// Windows 的 `CREATE_NO_WINDOW`：给控制台程序开一个不显示的窗口。
#[cfg(windows)]
const CREATE_NO_WINDOW: u32 = 0x0800_0000;

/// 起一个"看不见窗口"的命令行进程。
pub(crate) fn cli_command(cli: impl AsRef<std::ffi::OsStr>) -> Command {
    let mut command = Command::new(cli);
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(CREATE_NO_WINDOW);
    }
    command
}

#[cfg(test)]
mod tests {
    #[test]
    fn create_no_window_flag_is_the_documented_value() {
        // 这个数字写错不会报错，只会把黑框放回来，所以拿文档值钉一下。
        #[cfg(windows)]
        assert_eq!(super::CREATE_NO_WINDOW, 0x0800_0000);
    }
}