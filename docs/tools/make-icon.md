# 更新项目图标

图标是打包输入，不是运行时截图。源图和 Tauri 所需尺寸可以提交；生成时使用项目自绘图形，不要从桌面截图裁切。

```powershell
python tools/build/make-icon.py
pwsh tools/dev/editor-ui.ps1 icons
```

[生成脚本](../../tools/build/make-icon.py)写入 `src/KeyMouse.EditorUI/src-tauri/icons/source.png`，第二步生成平台图标。提交前检查小尺寸轮廓，以及变更是否仅限预期的资源文件。
