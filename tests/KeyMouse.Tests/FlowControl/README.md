# 流程控制与表达式：单元测试

这一目录测的是 **KeyMouse 流程里的 C 风格控制流**（`if/else`、`while`、`do`、`for`、`break/continue`、
`set`、`assert`）与它背后的**表达式求值器**。全部不碰桌面：普通步骤交给假 dispatcher，
谓词通过 `IExpressionRuntime` 打桩，所以一条命令就能跑完。

## 怎么单跑

```pwsh
.\km t expr            # 表达式求值器
.\km t flow-control    # if/while/for 与护栏
.\km t expr flow-control

# 不重新编译：
.\km t -n expr
# 直接跑已编译的 exe：
tests\KeyMouse.Tests\bin\Release\net10.0-windows\KeyMouse.Tests.exe --only flow-control
```

## 文件与覆盖

| 文件 | 覆盖 |
| --- | --- |
| `ExpressionTests.cs` | 优先级、括号、一元运算、文本/数字/布尔的类型规则、短路、列号错误、未定义变量、未知函数、`text()` 两种形态、长度/嵌套上限 |
| `IfElseTests.cs` | 真假分支、`else if`（嵌套）、无 else、块作用域、加载期错误（缺 cond/steps、空 else、块带 when、`text()` 缺 probe）、条件不是布尔、`assert` 失败 |
| `WhileTests.cs` | 条件重读、零轮、`break`/`continue`、`do-while` 至少一次、`maxRounds`、`--max-steps`、`--dry-run` 只走一遍、循环外 break/continue 被拒 |
| `ForSugarTests.cs` | `init/cond/step` 顺序、零轮、`continue` 之后仍执行 step、`break`、循环变量作用域、step 新名字不可见于 body、`maxRounds` 与 dry-run |
| `FlowRun.cs` | 测试用运行器：把 JSON 写进临时目录，跑 `FlowRunner`，从 `--report` 读回退出码/步骤/origin；不是测试入口 |
| `FlowControlTests.cs` | `flow-control` 模块入口，依次调用上面三个文件 |

## 已知边界

- `text()` / `window()` / `front()` 的真实实现会查窗口、读屏，属于桌面行为；这里只用桩断言"表达式怎么调用它们"。
  真实读屏路径与 `wait-text` / `when` 共用同一套 `Probe` / `TextPredicate`，由桌面冒烟与手工验证覆盖。
- `--dry-run` 下 `text()` **按成立算**（与 `when` 的 dry-run 一致），不是真读；这是为了让 dry-run 验证结构而不是空转。
- 报告里的 `total` 对含条件/循环的流程是**静态规划数**（`if` 两条分支都算、循环体只算一遍），
  不能当"实际执行步数"；实际步数看 `commands` / `succeeded + failed`。
- `break`/`continue` 只能出现在 `while`/`do`/`for`/`repeat`/`foreach` 体内；子流程里的 `break` 不能跨 `call`。
