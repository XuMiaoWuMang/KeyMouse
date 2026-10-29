# make-icon.py —— 生成编辑器图标源图

`tools\build\make-icon.py`

画一张 1024×1024 的 PNG，作为 [`tauri icon`](editor-ui.md) 的输入。仓库里原本没有产品图标
（安装程序用的是默认图标），所以这张图是画出来的，不是找来的。

## 图形

朱砂底（`#C2410C`）圆角方块，纸上（`#FAF7F2`）三条左对齐、从长到短的横条。
和界面左上角那枚 `▞` 标记是同一套语言：三条横条读起来就是"这条流程有三步"。
只用两个颜色，缩到 16px 仍然能认出是"一列东西"，不会变成一团。

## 用法

```powershell
python tools\build\make-icon.py
# 写 src\KeyMouse.EditorUI\src-tauri\icons\source.png
pwsh tools\dev\editor-ui.ps1 icons      # 再从源图铺开 ico / png / Square*Logo 全套
```

需要 Pillow。系统 Python 里没装的话，用任何带 Pillow 的解释器跑这个文件即可——脚本只用到
`PIL.Image` 与 `PIL.ImageDraw`，与解释器版本无关。

## 改图形

只改文件顶部三个常量：`SIGNAL`（底色）、`PAPER`（横条色）、`BARS`（三条横条的左边距、顶边距、宽、高）。
**改完要重新跑一次 `editor-ui.ps1 icons`**，否则打包进去的还是旧图。

## 产物去哪

- 源图：`src\KeyMouse.EditorUI\src-tauri\icons\source.png`（进仓库，保证可重现）
- 生成物：同目录下的 `icon.ico`、`icon.png`、`128x128.png`、`Square*.png` 等（进仓库，打包要用）
- Tauri 会顺手生成 `android/` 与 `ios/`，这个产品不发移动端，`editor-ui.ps1 icons` 会删掉它们

## 相关

- 图标怎么被用掉：[editor-ui.ps1](editor-ui.md)
- 界面配色：[开发指南 · 主题](../dev/development.md)
