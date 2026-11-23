# 为 OCR 问题制作可重复证据

[evidence.ps1](../../tools/diagnostics/evidence.ps1) 使用测试靶窗展示已知文字，比较不同取图和识别设置。适合定位「看到了正确像素，但读错了文字」的问题。

```powershell
pwsh tools/diagnostics/evidence.ps1 -Samples 3 -Attempts 1
```

在解锁的专用测试桌面运行，需要可用 CLI、靶窗和 OCR 引擎；`-Exe` 与 `-TargetExe` 可固定版本。结果写入 `artifacts/tests/evidence-*/`。对比原始图像、处理后的图像与识别结果，不要只比较退出码。归档可能包含截图、文本和本机路径；只分享已经检查过的最小样本。
