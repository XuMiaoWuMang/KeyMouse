# 脚本索引

所有命令从仓库根目录运行。下表按副作用区分工具：检查文档不需要桌面，录制与冒烟会产生真实输入，发布脚本还会改变远端仓库。

| 用途 | 入口 |
| --- | --- |
| 编译、启动或测试 | [km 快捷入口](km.md) · [编辑器开发](editor-ui.md) · [构建与打包](build.md) |
| 检查工作树 | [文档](check-docs.md) · [版本](check-version.md) · [公开材料](../guide/publication.md) · [综合验证](verify.md) |
| 准备资源 | [OCR 模型](fetch-ocr-models.md) · [产品图标](make-icon.md) |
| 交互桌面验收 | [冒烟](smoke.md) · [输入可达性](check-input.md) · [录制起点](record-focus.md) |
| 本机诊断 | [窗口权限](window-levels.md) · [截图](screenshot.md) · [OCR 取证](evidence.md) |
| 远端操作 | [推送](push.md) · [Release](release.md) |

查构建结果的位置请看[源码地图](../dev/project-map.md)。可运行的发布附件与供测试暂存的文件不是同一类；不要把整个 `artifacts/` 传到 GitHub。
