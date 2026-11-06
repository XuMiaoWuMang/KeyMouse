# OCR：读屏用的内置引擎

> 结论先说：KeyMouse 现在自带上 PaddleOCR 的 PP-OCRv4 检测/识别模型，跑在 ONNX Runtime 上，
> 不再需要先装 Tesseract。外部引擎仍然保留（`--engine tesseract`），但那是退路，不是默认。

## 为什么换

旧路径是"调用本机 Tesseract 命令行，解析 tsv"。三个问题在真实桌面上反复出现：

1. **要先装东西**。新机器、只装了运行库的机器上，`read-text` 第一步就是"没找到引擎"。
2. **中文差**。Tesseract 的 `chi_sim` 对界面里 12–14 px 的中文，逐字错读很常见，而
   `find` 匹配的是连续子串，错一个字就找不到。
3. **只有词级置信度**，没有"这一行整体有多可信"的概念；`DropScore` 那种策略没法表达。

BetterGI 的做法是把 PaddleOCR 的 det/rec 导成 ONNX、用 ONNX Runtime 跑，模型随程序走。
这套在游戏画面上被验证过千万次，我们要读的界面文字只会更容易——所以照它做。

## 结构

```
src\KeyMouse.Core\Recognition\Ocr\
  OcrModels.cs       模型在哪：覆盖变量 → <exe>\models\ocr → artifacts\models\ocr → %LOCALAPPDATA%
  OcrImage.cs        像素层：双线性缩放、裁剪、归一化成 CHW，无 OpenCV
  DbPostProcess.cs   检测后处理：概率图 → 连通域 → 框 + 分数 + unclip
  CtcDecoder.cs      CTC 贪心解码：跳空白、合并重复、置信度取出字帧均值
  PaddleOcr.cs       引擎本体：det → 逐框裁剪 → rec → 过滤/排序
```

调用方只有一处：`src\KeyMouse.Core\Capture\Probe.Engine.cs` 的 `RunOcr`。它按 `--engine` 决定
走内置还是外部，然后把结果装成 probe 一直在用的 `(text, confidence, lines)`——所以
`TextLocator`、`--find`、`read-text` 步骤、`click-text` 全都没改。

## 参数（照 PaddleOCR PP-OCRv4 的配置）

| 阶段 | 参数 | 值 | 出处 |
| --- | --- | --- | --- |
| Det | 输入长边 | 960（`max` 规则，再对齐 32） | BetterGI `Det` 的 `LimitSideLen` |
| Det | 归一化 | mean `0.485/0.456/0.406`，std `0.229/0.224/0.225` | PaddleOCR 通用预处理 |
| Det | 二值化 / 框分数 | 0.3 / 0.6 | `BoxThreshold` / `BoxScoreThreshold` |
| Det | unclip 比 / 最小尺寸 / 最多框 | 1.5 / 3 px / 1000 | 同上 |
| Rec | 输入高 | 48，宽度按比例 | PP-OCR rec 模型固定 48 |
| Rec | 归一化 | mean/std 都是 `0.5` | 同上 |
| Rec | 丢分 | 0.5（`DropScore`） | BetterGI `PaddleOcrService` |
| 字表 | 来源 | `rec.onnx` 的 metadata `character` | 本模型族自带，不用另外下 `ppocr_keys_v1.txt` |

## 模型从哪来

模型不进仓库（15 MB 二进制，换模型才变，历史里留下一串读不了的 blob）。由
[`tools\build\fetch-ocr-models.ps1`](../tools/fetch-ocr-models.md) 取一次：

```powershell
pwsh tools\build\fetch-ocr-models.ps1      # 落 artifacts\models\ocr\{det,rec}.onnx，带哈希校验
```

来源是 PyPI 上的 `rapidocr-onnxruntime`（Apache-2.0）：wheel 里直接打包了 PaddleOCR 官方导出的
`ch_PP-OCRv4_det_infer.onnx` / `ch_PP-OCRv4_rec_infer.onnx`，走清华镜像下载。
`tools\build\build.ps1` 会把它们复制到 `artifacts\cli\models\ocr\`，没有模型时构建会黄色提示
"包里的内置引擎会回落到 Tesseract"。

安装版靠 `tools\build\pack-release.ps1`：`cli.zip` 里除了 `KeyMouse.exe`，还带两条
`models/ocr/det.onnx`、`models/ocr/rec.onnx` 条目；安装程序按条目里的相对路径解到
`<安装目录>\cli\models\ocr\`，于是装完就能用内置引擎。少了模型只警告不失败，
但装出来的机器会退回外部 Tesseract——所以 `pack-release` 打包时要盯着那条警告。

查找顺序（`OcrModels.SearchPath`）：`KEYMOUSE_OCR_MODELS` 环境变量 → `<exe>\models\ocr` →
开发树的 `artifacts\models\ocr` → `%LOCALAPPDATA%\KeyMouse\ocr`。**两个文件必须同在一处**，
不混用不同来源的 det/rec。

## `--engine` 的三种取值

| 值 | 行为 |
| --- | --- |
| `auto`（默认） | 有模型就用内置，没有就回落到 Tesseract，并在报告里写明回落原因 |
| `builtin` | 只用内置；模型缺失直接报错（不静默降级，装坏了要看得见） |
| `tesseract` 或任意路径 | 走旧的外部引擎路径，参数兼容 Tesseract 命令行 |

## 怎么验

```powershell
# 引擎身份、模型、耗时、每行框
.\artifacts\cli\KeyMouse.exe probe --process KeyMouse.Editor --pick 1 --region 0,0,900,120 --engine builtin

# 中英混排能不能定位（这条同时验 TextLocator）
.\artifacts\cli\KeyMouse.exe probe --process KeyMouse.Editor --pick 1 --region 0,0,900,120 --engine builtin --find 流程编辑器

# 单元：张量布局 / DB 后处理 / CTC / 模型加载（有模型时才会真的加载）
dotnet run -c Release --project tests\KeyMouse.Tests -- --only ocr
```

2026-11-06 的一轮实测（900×120 的界面区域，两次读取逐字一致）：置信度 96.4，耗时 525 ms，
读出 `KEYMOUSE / 流程编辑器 / 还没有打开流程 / 切换窗口 / 点击 / 输入文字 / …`，
`--find 流程编辑器` 定位到客户点 213,43。证据留在 `artifacts\shots\p40-ocr\`。

## 已知边界（写清楚，免得下次当成 bug）

- **只出轴对齐的框**。DB 轮廓在这里用连通域近似，没有 OpenCV 的多边形 unclip；竖排或大角度
  倾斜的文字会读出、但框是外接矩形。要旋转框再引入多边形裁剪。
- **没有方向分类器**（PaddleOCR 的 cls 模型），180° 倒置的文字读不出来。界面文字不会倒置。
- **图标会被读成符号**：`⊡输入文字` 这类前面带图标的按钮，识别结果里图标变成一个标点，
  例如 `:I输入文字`。`find` 用子串匹配，所以不影响定位；但如果你要的是"干净的行文本"，
  得在展示层裁掉行首的非文字字符。
- **每读一次约 250–350 ms**（det+rec，单线程 CPU）。`reads` 默认 2 次，所以一次 probe 约
  0.5 s；循环里高频读屏时要自己算预算。

## 与 BetterGI 的差别（有意为之）

| 方面 | BetterGI | 这里 | 为什么 |
| --- | --- | --- | --- |
| 图像库 | OpenCvSharp | 自己写的 4 个像素操作 | 我们只用得到缩放/裁剪/归一化，引 OpenCV 是几十 MB 换 200 行 |
| 模型档位 | V4/V4En/V5/V5Latin/V5Eslav/V5Korean/V6 按语言选 | 只有 V4 中文+英文 | 我们的界面就这两类文字，多档位是维护成本 |
| 字符表 | 模型同目录 `inference.yml` | 模型 metadata 的 `character` | 这是这一族 ONNX 模型自带的信息，少一个文件少一处出错 |
| 批处理 | rec 按宽度排序、`min(8, CPU)` 分批 | 逐框单张 | 界面一次读 5–15 行，批起来省几十毫秒，复杂度不值 |
| 预热 | 构造时跑一张图 | 首次读的时候顺带 | 我们不是常驻进程，构造完立刻就读 |
| 多线程 | 全程加锁（DML 张量内存 bug） | 单线程 + 锁 | 同样是 CPU，但没有 DML 路径 |

## 相关

- 命令与退出码：[命令行参考](../reference/cli.md)
- 取模型脚本：[`fetch-ocr-models.ps1`](../tools/fetch-ocr-models.md)
- 编辑器外壳一侧的读屏（取点/取区域）：[编辑器 IPC 契约](../reference/editor-ipc.md)
