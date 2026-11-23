# 辨认正在使用的程序

更新后看不到新字段时，先查编辑器实际调用的 CLI，再决定要不要重新编译。Tauri 壳通过 `KeyMouse schema` 取得步骤契约；只更换界面文件不会更新旧引擎。

| 环境 | 编辑器与引擎位置 |
| --- | --- |
| 源码开发 | 编辑器在 `src/KeyMouse.EditorUI/src-tauri/target/release/`；CLI 优先查开发树 `artifacts/cli/`，也可使用 `src/KeyMouse.Cli/bin/Release/net10.0-windows/` |
| 安装版 | `<安装目录>/KeyMouse.Editor.exe` 与 `<安装目录>/cli/KeyMouse.exe`；安装登记在 `HKCU\Software\KeyMouse` |
| 独立 CLI | 放在用户选择的目录，与编辑器无绑定关系 |

在编辑器查看运行环境或在仓库根运行 `.\km env`。需要固定开发 CLI 时，启动编辑器前设置当前进程的 `KEYMOUSE_CLI`；用完移除该环境变量。用 `--version` 核对产品和引擎版本，用 `schema` 核对实际字段。不要把自己的绝对路径写入项目配置。
