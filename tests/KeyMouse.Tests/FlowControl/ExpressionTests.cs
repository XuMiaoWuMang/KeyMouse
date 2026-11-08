using KeyMouse.Flow;

namespace KeyMouse.Tests;

/// <summary>
/// 表达式求值器：优先级、短路、类型规则、错误位置。
///
/// 这些测试不碰桌面：谓词通过 <see cref="IExpressionRuntime"/> 打桩，所以 <c>text()</c> 的
/// 行为可以在这里独立断言，不必先有一个记事本窗口。
/// </summary>
internal static class ExpressionTests
{
    public static void Run()
    {
        Harness.Group("expr: 字面量与算术");

        Harness.Equal("乘除先于加减", 7d, Number("1 + 2 * 3"));
        Harness.Equal("括号改变顺序", 9d, Number("(1 + 2) * 3"));
        Harness.Equal("一元负号", -2d, Number("-3 + 1"));
        Harness.Equal("除法保留小数", 2.5d, Number("10 / 4"));
        Harness.Equal("取余", 3d, Number("7 % 4"));
        Harness.Equal("整数值不带 .0", "3", Text("1 + 2"));
        Harness.Equal("小数按需要显示", "2.5", Text("5 / 2"));
        Throws("1 / 0", "不能除以 0");
        Throws("1 % 0", "不能对 0 取余");
        Throws("1 + true", "不能相加");

        Harness.Group("expr: 文本、数字与加号");

        Harness.Equal("两个字符串拼接", "ab", Text("\"a\" + \"b\""));
        Harness.Equal("数字接在文字后面", "a1", Text("\"a\" + 1"));
        Harness.Equal("像数字的文本参与算术", 4d, Number("i + 1", ("i", FlowValue.Text("3"))));
        Harness.Equal("不像数字的文本只能拼接", "x-y", Text("s + \"-y\"", ("s", FlowValue.Text("x"))));
        Harness.Equal("两个文本即使像数字也拼接", "33", Text("a + b", ("a", FlowValue.Text("3")), ("b", FlowValue.Text("3"))));
        Harness.Equal("单引号也是字符串", "副本", Text("'副本'"));

        Harness.Group("expr: 比较与布尔");

        Check("小于", Bool("1 < 2"));
        Check("大于等于", Bool("2 >= 2"));
        Check("相等", Bool("1 == 1"));
        Check("不等", Bool("1 != 2"));
        Check("文本按序号比较", Bool("\"a\" < \"b\""));
        Check("两个文本不猜数字：01 与 1 不相等", !Bool("\"01\" == \"1\""));
        Check("数字与文本可以按数字比较", Bool("3 == \"3\""));
        Check("两个都像数字的文本按数字比大小", Bool("\"10\" > \"9\""));
        Check("not 取反", Bool("!(1 > 2)"));
        Check("与运算", Bool("1 < 2 && 2 < 3"));
        Check("或运算", Bool("1 > 2 || 2 < 3"));
        Throws("\"abc\" == 3", "需要一个数字");
        Throws("true == 1", "只能和 true/false 比较");
        Throws("true && 1", "需要一个 true/false");
        Throws("true < false", "不能比大小");

        Harness.Group("expr: 短路是必须的，右边可能是读屏");

        var stub = new Stub();
        Check("&& 左侧为假时不求值右边", !Bool("false && window(\"x\")", stub) && stub.Calls.Count == 0);
        Check("|| 左侧为真时不求值右边", Bool("true || window(\"x\")", stub) && stub.Calls.Count == 0);
        Check("需要时才真的调用谓词", Bool("true && window(\"x\")", stub) && stub.Calls.Count == 1);

        Harness.Group("expr: 变量与谓词");

        Harness.Equal("变量参与拼接", "你好 记事本", Text("greeting + \" \" + name",
            ("greeting", FlowValue.Text("你好")), ("name", FlowValue.Text("记事本"))));
        var found = stub.CallOf("found(\"副本\")");
        Harness.Equal("谓词参数原样交给运行时", "副本", found[0].AsText());
        var five = stub.CallOf("text(\"保存\", 0, 0, 400, 32)");
        Harness.Equal("text() 的五参数形态", 5, five.Count);
        Harness.Equal("elapsed() 无参", 0, stub.CallOf("elapsed()").Count);
        Harness.Equal("found() 可以不带文字", 0, stub.CallOf("found()").Count);
        Harness.Check("变量表按出现顺序去重",
            Expression.Parse("a + b + a", "测试").Variables.SequenceEqual(["a", "b"]),
            string.Join(",", Expression.Parse("a + b + a", "测试").Variables));
        Harness.Check("函数表按出现顺序去重",
            Expression.Parse("window(\"a\") && found(\"x\")", "测试").Functions.SequenceEqual(["window", "found"]),
            string.Join(",", Expression.Parse("window(\"a\") && found(\"x\")", "测试").Functions));

        Throws("nope + 1", "还没有值");
        Throws("text(\"a\", 1, 2, 3)", "1 个参数");
        Throws("window()", "1 个参数");
        Throws("elapsed(1)", "不接受参数");
        Throws("find(\"a\")", "不认识的函数");

        Harness.Group("expr: 错误带位置，且在加载时就能报");

        Column("1 +", 4);
        Column("(1 + 2", 7);
        Column("1 2", 3);
        Column("a = 1", 3);
        Column("a & b", 3);
        Column("1 ? 2 : 3", 3);
        Column("and", 1);
        Throws("", "表达式是空的");
        Throws("\"abc", "字符串没有闭合");
        Throws("\"a\\q\"", "不认识的转义");
        Throws("text(\"a\",)", "这里需要");
        Throws("text(\"a\"", "这里需要 ）");
        Throws(new string('(', 70) + "1" + new string(')', 70), "嵌套太深");
        Throws(string.Join("+", Enumerable.Repeat("1", 2100)), "表达式太长");
        Throws("{i}", "不认识的字符");
    }

    private static FlowValue Eval(string source, params (string Name, FlowValue Value)[] variables) =>
        Expression.Parse(source, "测试").Evaluate(Frame(variables), new Stub());

    private static FlowValue Eval(string source, Stub runtime, params (string Name, FlowValue Value)[] variables) =>
        Expression.Parse(source, "测试").Evaluate(Frame(variables), runtime);

    private static VariableFrame Frame((string Name, FlowValue Value)[] variables) =>
        new(null, variables.Select(v => new KeyValuePair<string, FlowValue>(v.Name, v.Value)));

    private static double Number(string source, params (string, FlowValue)[] variables) =>
        Eval(source, variables).AsNumber("测试");

    private static string Text(string source, params (string, FlowValue)[] variables) =>
        Eval(source, variables).AsText();

    private static bool Bool(string source, params (string, FlowValue)[] variables) =>
        Eval(source, variables).AsBool("测试");

    private static bool Bool(string source, Stub runtime) => Eval(source, runtime).AsBool("测试");

    private static bool Bool(string source, Stub runtime, params (string, FlowValue)[] variables) =>
        Eval(source, runtime, variables).AsBool("测试");

    private static void Check(string what, bool ok) => Harness.Check(what, ok);

    private static void Column(string source, int column)
    {
        try
        {
            Eval(source);
            Harness.Check($"「{source}」被拒绝", false, "没有抛异常");
        }
        catch (CommandFailure ex)
        {
            Harness.Check($"「{source}」报第 {column} 列", ex.Message.Contains($"第 {column} 列"), ex.Message);
        }
    }

    private static void Throws(string source, string contains)
    {
        try
        {
            Eval(source);
            Harness.Check($"「{Short(source)}」被拒绝", false, "没有抛异常");
        }
        catch (CommandFailure ex)
        {
            Harness.Check($"「{Short(source)}」：{contains}", ex.Message.Contains(contains), ex.Message);
        }
    }

    private static string Short(string source) => source.Length <= 24 ? source : source[..21] + "…";

    /// <summary>Stands in for the runner's real predicates: records calls, returns fixed answers.</summary>
    private sealed class Stub : IExpressionRuntime
    {
        internal List<(string Function, FlowValue[] Args)> Calls { get; } = new();

        public FlowValue Call(string function, IReadOnlyList<FlowValue> args)
        {
            Calls.Add((function, args.ToArray()));
            return function switch
            {
                "window" or "front" => FlowValue.Bool(true),
                "found" => FlowValue.Bool(false),
                "elapsed" => FlowValue.Number(1234),
                "text" => FlowValue.Bool(true),
                _ => throw new CommandFailure(2, $"stub 不认识 {function}"),
            };
        }

        internal IReadOnlyList<FlowValue> CallOf(string source)
        {
            Expression.Parse(source, "测试").Evaluate(new VariableFrame(null), this);
            return Calls[^1].Args;
        }
    }
}
