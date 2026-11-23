# ADR 0002：直接派发类型化流程步骤

**决定：** JSON 流程校验、编译后直接派发类型化步骤，不再先生成一条命令行再让解析器读回来。用户提供的文本脚本继续使用文本解析器。

**原因：** 文本参数可能恰好长得像命令行选项。将用户文字与参数边界保留在模型中，可避免二次解释；编辑器日志需要可读命令，但显示文本不应参与执行。

**边界：** [模型](../../../src/KeyMouse.Core.Flow.Model)不依赖桌面；[校验层](../../../src/KeyMouse.Core.Flow.Validation)产生计划；[执行层](../../../src/KeyMouse.Core.Flow.Execution)通过委托与桌面抽象工作；顶层 [Core](../../../src/KeyMouse.Core)组合具体实现。跨入口兼容通过 `typed-dispatch` 与 CLI E2E 验证。
