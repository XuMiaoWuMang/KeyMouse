//! Check the process token before WebView2 or user storage is initialized.

use std::{mem, ptr};
use windows_sys::Win32::{
    Foundation::CloseHandle,
    Security::{
        GetSidSubAuthority, GetSidSubAuthorityCount, GetTokenInformation, TokenIntegrityLevel,
        SECURITY_MAX_SID_SIZE, TOKEN_MANDATORY_LABEL, TOKEN_QUERY,
    },
    System::Threading::{GetCurrentProcess, OpenProcessToken},
    UI::WindowsAndMessaging::{MessageBoxW, MB_ICONWARNING, MB_OK},
};

fn integrity_rid() -> Option<u32> {
    // The returned label points into the same buffer; keep it aligned and in place.
    #[repr(C)]
    struct LabelBuffer(TOKEN_MANDATORY_LABEL, [u8; SECURITY_MAX_SID_SIZE as usize]);
    unsafe {
        let mut token = ptr::null_mut();
        if OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &mut token) == 0 {
            return None;
        }
        let mut buf = mem::MaybeUninit::<LabelBuffer>::uninit();
        let mut size = 0;
        let ok = GetTokenInformation(
            token,
            TokenIntegrityLevel,
            buf.as_mut_ptr().cast(),
            mem::size_of::<LabelBuffer>() as u32,
            &mut size,
        );
        CloseHandle(token);
        if ok == 0 {
            return None;
        }
        let sid = (*buf.as_ptr()).0.Label.Sid;
        let last = (*GetSidSubAuthorityCount(sid)).checked_sub(1)?;
        Some(*GetSidSubAuthority(sid, u32::from(last)))
    }
}

pub fn reject_low_integrity() -> bool {
    // This is a startup diagnostic, not an authorization check. On query failure try normal startup.
    if !integrity_rid().is_some_and(|rid| rid < 0x2000) {
        return false;
    }
    // A low-integrity process may not even be able to resolve its executable path.
    let message = concat!(
        "编辑器正以低完整性运行，无法正常使用 WebView2 用户数据。无需以管理员身份运行。\n\n",
        "安装版：从开始菜单打开 KeyMouse 编辑器。\n",
        "开发版：从普通资源管理器打开仓库中的\n",
        r"src\KeyMouse.EditorUI\src-tauri\target\release\KeyMouse.Editor.exe"
    );
    let wide = |text: &str| text.encode_utf16().chain(Some(0)).collect::<Vec<_>>();
    let title = wide("KeyMouse 编辑器：启动权限受限");
    let body = wide(message);
    eprintln!("{message}");
    unsafe {
        MessageBoxW(
            ptr::null_mut(),
            body.as_ptr(),
            title.as_ptr(),
            MB_OK | MB_ICONWARNING,
        )
    };
    true
}

#[cfg(test)]
mod tests {
    #[test]
    fn can_read_integrity_rid_from_the_running_process() {
        assert!(super::integrity_rid().is_some());
    }
}
