# `fetch-ocr-models.ps1`：取内置 OCR 的模型

内置 OCR（PaddleOCR PP-OCRv4 的 det / rec，ONNX）靠两个模型文件工作，它们**不在仓库里**：
15 MB 的二进制只在换模型时才变，塞进 git 之后每次 clone 都得拖一遍。这个脚本负责取一次。

- 脚本：`tools\build\fetch-ocr-models.ps1`
- 产物：`artifacts\models\ocr\det.onnx`、`artifacts\models\ocr\rec.onnx`（已 gitignore）
- 来源：PyPI 上的 `rapidocr-onnxruntime`（Apache-2.0），wheel 里直接打包了 PaddleOCR 官方导出的
  `ch_PP-OCRv4_det_infer.onnx` / `ch_PP-OCRv4_rec_infer.onnx`；下载走清华镜像
  `pypi.tuna.tsinghua.edu.cn`（`files.pythonhosted.org` 在部分网络下不可达）

## 用法

```powershell
pwsh tools\build\fetch-ocr-models.ps1              # 有就跳过，没有就取
pwsh tools\build\fetch-ocr-models.ps1 -Force       # 重下
pwsh tools\build\fetch-ocr-models.ps1 -Verify      # 只校验（CI/排障用，不联网）
```

`-Verify` 校验两个文件的 SHA-256，不符就用非 0 退出码说清楚是哪一份不对。

## 什么时候会自动用到它

- `tools\build\build.ps1` 打 CLI 产物时，会把 `artifacts\models\ocr\*.onnx` 复制到
  `artifacts\cli\models\ocr\`，安装包复制 cli 目录时一起带上。
- 命令行运行时，内置引擎按这个顺序找模型：`KEYMOUSE_OCR_MODELS` → `<exe>\models\ocr` →
  开发树的 `artifacts\models\ocr` → `%LOCALAPPDATA%\KeyMouse\ocr`。
- 找不到模型时：`--engine auto`（默认）回落到 Tesseract，报告里写明原因；`--engine builtin`
  直接报错——装坏了要看得见，而不是悄悄换引擎。

## 换模型时注意

1. 两个文件的哈希钉在脚本顶部（`$expected`）；换模型就换哈希。
2. 文件名必须还是 `det.onnx` / `rec.onnx`——`OcrModels` 只认这两个名字。
3. 字符表从 `rec.onnx` 的 metadata（`character` 键）读；换成一个没有这个元数据的模型，
   内置引擎会明确报"模型里没有字符表"，而不是读出一堆空白。
4. 换完跑一次 `dotnet run -c Release --project tests\KeyMouse.Tests -- --only ocr`，
   里面有"字符表规模 > 6000"这条兜底检查。

## 相关

- OCR 的设计、参数与边界：[OCR：读屏用的内置引擎](../dev/ocr.md)
- 构建脚本：[`build.ps1` 与 `pack-release.ps1`](build.md)
