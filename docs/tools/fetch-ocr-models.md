# 获取并校验内置 OCR 模型

[模型脚本](../../tools/build/fetch-ocr-models.ps1)获取指定 wheel 中的检测与识别文件，并校验内容哈希。开发树的位置为 `artifacts/models/ocr/det.onnx` 和 `rec.onnx`；不应将模型二进制加入 Git。

```powershell
pwsh tools/build/fetch-ocr-models.ps1          # 缺失时下载
pwsh tools/build/fetch-ocr-models.ps1 -Verify  # 只校验已有文件
```

需要重新获取才使用 `-Force`。更换来源时审查许可、哈希和两份模型的配对关系；不要仅改下载地址就发布。查找顺序、外部引擎与排障步骤见 [OCR](../dev/ocr.md)。
