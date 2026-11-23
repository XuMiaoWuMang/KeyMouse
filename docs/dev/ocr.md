# OCR：区分取图、识别和匹配

`probe` 先确定窗口和区域，再捕获像素、识别文字，最后才匹配查询。遇到「没看清」时，先确认原图是否包含完整的一行。放大一张空图或换匹配算法都不会生成缺失的内容。

| 参数 | 选择 |
| --- | --- |
| `--capture screen` | 读桌面可见像素；遮挡会改变结果 |
| `--capture print` | 请求目标窗口绘制内容。窗口可能返回空白；不能推论所有最小化应用都支持 |
| `--engine auto` | 有配套模型时使用内置引擎，否则尝试外部 Tesseract |
| `--engine builtin` | 要求内置检测和识别模型；适合验证随包模型是否完整 |

模型由[获取脚本](../tools/fetch-ocr-models.md)校验。`det.onnx` 与 `rec.onnx` 必须在同一目录。查找顺序和文件名见 [OcrModels.cs](../../src/KeyMouse.Core.Capture/Recognition/Ocr/OcrModels.cs)；可用 `KEYMOUSE_OCR_MODELS` 临时指定目录，开发默认查 `artifacts/models/ocr/`。

```powershell
pwsh tools/build/fetch-ocr-models.ps1 -Verify
dotnet run -c Release --project tests/KeyMouse.Tests -- --only ocr
```

`--keep-image` 留下原始像素，`--keep-prepared` 留下送入识别器的图像。请在合成靶窗中使用，避免把屏幕截图和识别文字作为公开错误日志。
