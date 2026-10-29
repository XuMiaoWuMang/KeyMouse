# editor-ui.ps1 —— 新编辑器外壳的日常命令

`tools\dev\editor-ui.ps1`

新编辑器外壳（`src\KeyMouse.EditorUI`，Tauri + Svelte）的入口。它**不管老编辑器**——WinUI 那套仍然走
[`km e`](km.md)。

## 什么时候用它

- 改了 `src\KeyMouse.EditorUI` 下的界面或 Rust 外壳，要看效果。
- 第一次在这台机器上跑新外壳（装依赖、编 Rust）。
- 要重新生成图标。
- 想清掉 `node_modules` 与 `target`（它们加起来比整个仓库还大）。

## 参数

| 参数 | 说明 |
| --- | --- |
| `-Verb`（位置参数） | `setup` / `dev` / `build` / `check` / `icons` / `clean`，默认 `dev` |
| `-Registry` | 装依赖用的 npm 源，默认 `https://registry.npmmirror.com` |

## 动词

| 动词 | 做什么 | 产物 |
| --- | --- | --- |
| `setup` | `pnpm install`（走镜像，重试 5 次） | `node_modules/` |
| `dev` | 起 vite + `cargo run`，打开窗口 | 日志写 `artifacts\tests\editor-ui\dev-<时间戳>.log` |
| `build` | 只打前端 | `src\KeyMouse.EditorUI\dist\`，打印总体积 |
| `check` | `svelte-check`：类型与模板 | 只读，0 error 才算过 |
| `icons` | 从 `src-tauri\icons\source.png` 铺开整套尺寸 | `src-tauri\icons\`（顺手删掉 android/ios 两套） |
| `clean` | 删 `node_modules`、`src-tauri\target`、`dist` | — |

## 它替你处理的三个环境坑

这三条在上面脚本的注释里也写着，因为每一条都真实拦住过人：

1. **npm 源**。直连 `registry.npmjs.org` 会 `ETIMEDOUT`/`ECONNRESET`，所以默认走 `registry.npmmirror.com`。
   要直连就传 `-Registry https://registry.npmjs.org`。
2. **cargo 的证书吊销检查**。本机 `~\.cargo\config.toml` 通常配了镜像源；Windows 上 Schannel 在连不上吊销服务器时
   会直接失败（`CRYPT_E_REVOCATION_OFFLINE`）。`dev` 动词会把 `CARGO_HTTP_CHECK_REVOKE` 置 `false` 再编。
3. **pnpm 的 build 脚本**。pnpm 11 默认不跑依赖的安装脚本，esbuild 因此不可用，报 `ERR_PNPM_IGNORED_BUILDS`。
   `src\KeyMouse.EditorUI\pnpm-workspace.yaml` 里已经写了 `allowBuilds: esbuild: true`。

另有一个已知坑不在这三条里：`pnpm tauri dev` 会同时起 vite 和 cargo，**vite 崩了窗口还在**（页面是内存里的旧版），
表现是"改了代码界面不变"。看到这种情况先看 `artifacts\tests\editor-ui\dev-*.log`，必要时重开 `dev`。

## 退出码

`dev` 以外的动词失败时抛异常并以非 0 退出（`Invoke-Step` 统一判断 `$LASTEXITCODE`）。
`dev` 是长跑命令，关掉窗口或 `Ctrl+C` 结束。

## 图标

源图由 [`tools\build\make-icon.py`](make-icon.md) 生成（需要 Pillow）。图形是"朱砂底、纸上三条从长到短的横条"：
它和界面左上角那枚标记同一套语言，也在说"一列步骤"。

```powershell
python tools\build\make-icon.py            # 写 src-tauri\icons\source.png
pwsh tools\dev\editor-ui.ps1 icons         # 从源图铺开 ico/png 全套
```

## 相关

- 新外壳的目录、IPC 契约与阶段计划：[新编辑器外壳](../dev/editor-ui.md)
- 为什么换前端：[ADR 0001](../dev/adr/0001-editor-shell-tauri.md)
