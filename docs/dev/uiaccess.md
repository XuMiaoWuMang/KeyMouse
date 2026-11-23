# 可选的 UIAccess 输入组件

标准安装器不携带这个组件，日常编辑流程也不需要它。需要部署的组织应自行提供受信任签名，并在专用 Windows 环境验收；仓库不能包含签名私钥、证书密码或本机信任配置。

先用只读计划核对附件与安装目标。`$helper` 应指向组织提供的已签名输入组件；显式安装与构建普通编辑器是两件事。

```powershell
pwsh tools/build/install-uiaccess.ps1 -HelperPath $helper -Mode Plan
pwsh tools/build/pack-release.ps1 -UIAccessHelper $helper -Plan
```

## 安装安全边界

1. 仅接受绝对本地 publish 目录路径；拒绝 UNC、目录内未知文件、子目录、各文件及任一已存在祖先的重解析点。每个允许文件的签名必须由 `Get-AuthenticodeSignature` 判定为 `Valid`。
2. 通过 Windows 数据资源 API 读取内嵌 `RT_MANIFEST #1`，不执行 helper；禁止 XML DTD，要求恰好一个 `requestedExecutionLevel`，属性严格为 `level="asInvoker" uiAccess="true"`。不使用外部同名 manifest。
3. 目标取 Windows `SpecialFolder.ProgramFiles`，不采信可覆盖的 `ProgramFiles` 环境变量，不允许自定义用户目录。目标及祖先拒绝重解析点。
4. 已存在 Program Files、KeyMouse、Input 与目标 EXE 的所有者只能为 SYSTEM、Administrators 或 TrustedInstaller；任何其他身份的写入、修改、删除、更改 ACL 或取得所有权授权均拒绝（保守拒绝，即使另有 deny）。卷根等上层目录额外拒绝非受信身份的删除子项、改 ACL 和取所有权权限。脚本不会修复可疑既存目录；应由管理员独立调查。
5. 新建目录使用受保护 ACL：SYSTEM 与 Administrators 完全控制，Users 读取执行，所有者为 Administrators。已有安全目录保留 ACL。所有源文件持只读共享句柄防止复制期间写入或替换，先复制到受保护父目录下唯一暂存目录，设置管理员所有者，重新逐文件验签及比对 SHA-256，再用同父目录 rename 切换整个 Input 目录，最后重新验签、检查全目录 ACL 与 hash；不会修改已签名内容。更新前旧目录也必须通过完整签名/允许列表检查；目录被运行中程序占用会报错并保留旧目录。切换失败时尽可能恢复已验证的旧目录，已切换后的异常会保留受保护备份供管理员检查。
6. 假定本机管理员/SYSTEM 可信；不抵御其并发篡改目录或禁用系统安全策略。helper 在运行时只信任受保护代码，业务数据/流程文件作为输入而非可加载代码处理；CLI 与 helper 的参数、进程创建与错误传播由 CLI 实现负责。

不需要 helper 时不要传 `-UIAccessHelper`；普通发布不会携带 helper，并清除同版本旧的可选 ZIP，避免误发布历史附件。其他版本的历史附件不由脚本删除。打包的临时目录使用唯一名字保留在 artifacts 下供检查，不进入版本库。部署更新需重新签名、重新显式安装；普通安装器不修改或卸载受保护 helper。卸载此可选能力时由管理员核对计划中的固定路径后单独移除文件，避免误删其他安装内容。

## 验证与限制

无证书、无提权环境可验证 PowerShell AST 语法、普通发布 `-Plan`、未签名 helper 拒绝、manifest 资源解析以及不安全目录/重解析点拒绝。完整成功路径需要组织认可且目标机器已信任的已签名 UIAccess 单文件 helper，须在专用 Windows 测试环境执行管理员安装，再以管理员组交互用户验收跨完整性窗口输入；标准用户单列验证并记录 medium+ 限制。不能通过临时安装根证书来绕过验收条件。

参考：[Microsoft UIAccess 安全策略](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/user-account-control/security-policy-settings)、[Windows 完整性级别与 UIAccess](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-securityoverview)。
