using System.Globalization;
using System.Text;

namespace KeyMouse.Flow;

/// <summary>
/// What an expression is allowed to ask the outside world. The runner implements it with real window
/// lookups and OCR reads; tests implement it with a stub, which is why the evaluator itself never
/// touches a desktop.
///
/// Only pure predicates live here: no actions, no waits, no writes. An expression decides a branch;
/// it must not click anything, move focus, or sleep.
/// </summary>
internal interface IExpressionRuntime
{
    FlowValue Call(string function, IReadOnlyList<FlowValue> args);
}

/// <summary>
/// A small C-flavoured expression language for flow conditions, written by hand.
///
/// Deliberately not a general scripting language:
/// - literals: numbers, "text" / 'text', true / false;
/// - variables: bare names (document variables, --set, loop variables, read-text results);
/// - arithmetic: + - * / %, concatenation with + when either side is text and the other is not a
///   number that can meet it as a number;
/// - comparison: == != &lt; &lt;= &gt; &gt;=, &amp;&amp; || ! with C precedence and short-circuit;
/// - parentheses; five builtin predicates: text(), window(), front(), found(), elapsed().
///
/// No eval, no reflection, no arrays, no function definitions, no assignment inside an expression.
/// Errors carry a 1-based column, and are reported when the file loads wherever the name/value is
/// statically knowable - a typo in a condition must cost a second, not the five minutes a `while`
/// might spin.
/// </summary>
internal sealed class Expression
{
    /// <summary>An expression longer than this is a mistake, not a clever condition.</summary>
    internal const int MaxLength = 4096;

    /// <summary>Nesting limit: keeps a pathological string from becoming a stack overflow.</summary>
    internal const int MaxDepth = 64;

    private readonly Node _root;

    private Expression(string source, IReadOnlyList<string> variables, IReadOnlyList<string> functions, Node root)
    {
        Source = source;
        Variables = variables;
        Functions = functions;
        _root = root;
    }

    internal string Source { get; }

    /// <summary>Names this expression reads. The loader uses it to refuse a typo before anything runs.</summary>
    internal IReadOnlyList<string> Variables { get; }

    /// <summary>Predicates this expression calls. A `text()` call requires the step to have a probe.</summary>
    internal IReadOnlyList<string> Functions { get; }

    internal FlowValue Evaluate(VariableFrame frame, IExpressionRuntime runtime) =>
        _root.Evaluate(new EvalContext(this, frame, runtime));

    /// <summary>
    /// Parses one expression. <paramref name="where"/> is the location the error should name
    /// ("第 3 步的 cond"), and is the only context the parser needs.
    /// </summary>
    internal static Expression Parse(string source, string where)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new CommandFailure(2, $"{where}：表达式是空的");
        if (source.Length > MaxLength)
            throw new CommandFailure(2, $"{where}：表达式太长（{source.Length} 个字符，最多 {MaxLength}）");

        var parser = new Parser(source, where);
        Node root = parser.ParseAll();
        var variables = new List<string>();
        var functions = new List<string>();
        Node.CollectAll(root, variables, functions);
        return new Expression(source, variables, functions, root);
    }

    internal static CommandFailure Error(string where, string source, int column, string detail) =>
        new(2, $"{where}：表达式「{Shorten(source)}」第 {column} 列：{detail}");

    internal static string Shorten(string source) => source.Length <= 80 ? source : source[..77] + "…";

    // ------------------------------------------------------------------ evaluation

    private sealed class EvalContext
    {
        internal EvalContext(Expression owner, VariableFrame frame, IExpressionRuntime runtime)
        {
            Owner = owner;
            Frame = frame;
            Runtime = runtime;
        }

        internal Expression Owner { get; }
        internal VariableFrame Frame { get; }
        internal IExpressionRuntime Runtime { get; }

        /// <summary>The location string handed to value conversions, e.g. for "expected a number".</summary>
        internal string At(int column) => $"表达式「{Shorten(Owner.Source)}」第 {column} 列";

        internal CommandFailure Error(int column, string detail) =>
            new(2, $"表达式「{Shorten(Owner.Source)}」第 {column} 列：{detail}");
    }

    private abstract class Node
    {
        internal abstract FlowValue Evaluate(EvalContext context);

        internal abstract void Collect(List<string> variables, List<string> functions);

        internal static void CollectAll(Node node, List<string> variables, List<string> functions) =>
            node.Collect(variables, functions);
    }

    private sealed class LiteralNode : Node
    {
        private readonly FlowValue _value;

        internal LiteralNode(FlowValue value) => _value = value;

        internal override FlowValue Evaluate(EvalContext context) => _value;

        internal override void Collect(List<string> variables, List<string> functions) { }
    }

    private sealed class VariableNode : Node
    {
        private readonly string _name;
        private readonly int _column;

        internal VariableNode(string name, int column)
        {
            _name = name;
            _column = column;
        }

        internal override FlowValue Evaluate(EvalContext context)
        {
            if (context.Frame.LookupValue(_name) is { } value) return value;
            throw context.Error(_column,
                $"变量 '{_name}' 还没有值（文档 variables、set、read-text 或循环变量才能给它值）");
        }

        internal override void Collect(List<string> variables, List<string> functions)
        {
            if (!variables.Contains(_name)) variables.Add(_name);
        }
    }

    private sealed class UnaryNode : Node
    {
        private readonly string _op;
        private readonly Node _operand;
        private readonly int _column;

        internal UnaryNode(string op, Node operand, int column)
        {
            _op = op;
            _operand = operand;
            _column = column;
        }

        internal override FlowValue Evaluate(EvalContext context)
        {
            FlowValue value = _operand.Evaluate(context);
            if (_op == "!") return FlowValue.Bool(!value.AsBool(context.At(_column)));
            double number = value.AsNumber(context.At(_column));
            return FlowValue.Number(-number);
        }

        internal override void Collect(List<string> variables, List<string> functions) =>
            _operand.Collect(variables, functions);
    }

    private sealed class BinaryNode : Node
    {
        private readonly string _op;
        private readonly Node _left;
        private readonly Node _right;
        private readonly int _column;

        internal BinaryNode(string op, Node left, Node right, int column)
        {
            _op = op;
            _left = left;
            _right = right;
            _column = column;
        }

        internal override FlowValue Evaluate(EvalContext context)
        {
            // Short-circuit first: the right side may be an OCR read, and "false && read" must not read.
            if (_op is "&&" or "||")
            {
                bool left = _left.Evaluate(context).AsBool(context.At(_column));
                if (_op == "&&" && !left) return FlowValue.Bool(false);
                if (_op == "||" && left) return FlowValue.Bool(true);
                return FlowValue.Bool(_right.Evaluate(context).AsBool(context.At(_column)));
            }

            FlowValue a = _left.Evaluate(context);
            FlowValue b = _right.Evaluate(context);

            switch (_op)
            {
                case "+":
                    if (a.Kind == FlowValueKind.Bool || b.Kind == FlowValueKind.Bool)
                        throw context.Error(_column, "true/false 不能相加");
                    // Both numbers add. Number + text adds when the text reads as a number (that is
                    // what makes `--set i=3` usable as `i + 1`), otherwise it concatenates. Two texts
                    // always concatenate: a screen read of "3" must not silently become arithmetic.
                    if (a.Kind == FlowValueKind.Number && b.Kind == FlowValueKind.Number)
                        return Number(a.AsNumber(context.At(_column)) + b.AsNumber(context.At(_column)), context);
                    if (a.Kind == FlowValueKind.Number && b.TryNumber(out double rightNumber))
                        return Number(a.AsNumber(context.At(_column)) + rightNumber, context);
                    if (b.Kind == FlowValueKind.Number && a.TryNumber(out double leftNumber))
                        return Number(leftNumber + b.AsNumber(context.At(_column)), context);
                    return FlowValue.Text(a.AsText() + b.AsText());

                case "-":
                    return Number(Numeric(a, context) - Numeric(b, context), context);
                case "*":
                    return Number(Numeric(a, context) * Numeric(b, context), context);
                case "/":
                {
                    double divisor = Numeric(b, context);
                    if (divisor == 0) throw context.Error(_column, "不能除以 0");
                    return Number(Numeric(a, context) / divisor, context);
                }
                case "%":
                {
                    double divisor = Numeric(b, context);
                    if (divisor == 0) throw context.Error(_column, "不能对 0 取余");
                    return Number(Numeric(a, context) % divisor, context);
                }
                case "==":
                    return FlowValue.Bool(Equal(a, b, context));
                case "!=":
                    return FlowValue.Bool(!Equal(a, b, context));
                case "<":
                    return FlowValue.Bool(Compare(a, b, context) < 0);
                case "<=":
                    return FlowValue.Bool(Compare(a, b, context) <= 0);
                case ">":
                    return FlowValue.Bool(Compare(a, b, context) > 0);
                case ">=":
                    return FlowValue.Bool(Compare(a, b, context) >= 0);
                default:
                    throw context.Error(_column, $"不认识的运算符 '{_op}'");
            }
        }

        private double Numeric(FlowValue value, EvalContext context) => value.AsNumber(context.At(_column));

        /// <summary>Arithmetic overflow is a type-level failure, not an infinity the flow can carry on with.</summary>
        private FlowValue Number(double value, EvalContext context)
        {
            if (!double.IsFinite(value)) throw context.Error(_column, "数字超出了能表示的范围");
            return FlowValue.Number(value);
        }

        /// <summary>
        /// Equality is kind-aware: two texts stay texts (so "01" != "1"), but a number and a text
        /// compare by coercing the text - that is what makes `--set i=3` usable as `i == 3`.
        /// </summary>
        private bool Equal(FlowValue a, FlowValue b, EvalContext context)
        {
            if (a.Kind == FlowValueKind.Bool || b.Kind == FlowValueKind.Bool)
            {
                if (a.Kind != FlowValueKind.Bool || b.Kind != FlowValueKind.Bool)
                    throw context.Error(_column, "true/false 只能和 true/false 比较");
                return a.AsBool(context.At(_column)) == b.AsBool(context.At(_column));
            }
            if (a.Kind == FlowValueKind.Text && b.Kind == FlowValueKind.Text)
                return string.Equals(a.AsText(), b.AsText(), StringComparison.Ordinal);

            return a.AsNumber(context.At(_column)).Equals(b.AsNumber(context.At(_column)));
        }

        /// <summary>
        /// Numbers compare numerically. Two texts compare numerically only when both read as numbers,
        /// otherwise ordinally: a screen read of "10" vs "9" should not silently become 10 &lt; 9 by
        /// accident of locale, but a flow that stores counters as `--set` values still needs numbers.
        /// </summary>
        private int Compare(FlowValue a, FlowValue b, EvalContext context)
        {
            if (a.Kind == FlowValueKind.Bool || b.Kind == FlowValueKind.Bool)
                throw context.Error(_column, "true/false 不能比大小");

            if (a.Kind == FlowValueKind.Number || b.Kind == FlowValueKind.Number)
                return a.AsNumber(context.At(_column)).CompareTo(b.AsNumber(context.At(_column)));

            if (a.TryNumber(out double numberA) && b.TryNumber(out double numberB))
                return numberA.CompareTo(numberB);
            return string.CompareOrdinal(a.AsText(), b.AsText());
        }

        internal override void Collect(List<string> variables, List<string> functions)
        {
            _left.Collect(variables, functions);
            _right.Collect(variables, functions);
        }
    }

    private sealed class CallNode : Node
    {
        private readonly string _function;
        private readonly IReadOnlyList<Node> _arguments;
        private readonly int _column;

        internal CallNode(string function, IReadOnlyList<Node> arguments, int column)
        {
            _function = function;
            _arguments = arguments;
            _column = column;
        }

        internal override FlowValue Evaluate(EvalContext context)
        {
            var values = new FlowValue[_arguments.Count];
            for (int i = 0; i < _arguments.Count; i++) values[i] = _arguments[i].Evaluate(context);
            try
            {
                return context.Runtime.Call(_function, values);
            }
            catch (CommandFailure)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw context.Error(_column, $"{_function}() 求值失败：{ex.Message}");
            }
        }

        internal override void Collect(List<string> variables, List<string> functions)
        {
            if (!functions.Contains(_function)) functions.Add(_function);
            foreach (Node argument in _arguments) argument.Collect(variables, functions);
        }
    }

    // ------------------------------------------------------------------ parsing

    private enum TokenKind
    {
        End,
        Number,
        Text,
        Identifier,
        Operator,
        LeftParen,
        RightParen,
        Comma,
    }

    private readonly record struct Token(TokenKind Kind, string Text, double Number, int Column);

    private sealed class Parser
    {
        private readonly string _source;
        private readonly string _where;
        private readonly List<Token> _tokens = new();
        private int _index;
        private int _depth;

        internal Parser(string source, string where)
        {
            _source = source;
            _where = where;
            Lex();
        }

        private Token Current => _tokens[_index];

        internal Node ParseAll()
        {
            Node root = ParseOr();
            if (Current.Kind != TokenKind.End)
                throw Error(Current.Column, $"多余的内容「{Current.Text}」");
            return root;
        }

        private Node ParseOr()
        {
            Node left = ParseAnd();
            while (Match("||", out int column)) left = new BinaryNode("||", left, ParseAnd(), column);
            return left;
        }

        private Node ParseAnd()
        {
            Node left = ParseEquality();
            while (Match("&&", out int column)) left = new BinaryNode("&&", left, ParseEquality(), column);
            return left;
        }

        private Node ParseEquality()
        {
            Node left = ParseRelational();
            while (Match("==", out int column) || Match("!=", out column))
            {
                string op = _tokens[_index - 1].Text;
                left = new BinaryNode(op, left, ParseRelational(), column);
            }
            return left;
        }

        private Node ParseRelational()
        {
            Node left = ParseAdditive();
            while (Match("<", out int column) || Match("<=", out column) ||
                   Match(">", out column) || Match(">=", out column))
            {
                string op = _tokens[_index - 1].Text;
                left = new BinaryNode(op, left, ParseAdditive(), column);
            }
            return left;
        }

        private Node ParseAdditive()
        {
            Node left = ParseMultiplicative();
            while (Match("+", out int column) || Match("-", out column))
            {
                string op = _tokens[_index - 1].Text;
                left = new BinaryNode(op, left, ParseMultiplicative(), column);
            }
            return left;
        }

        private Node ParseMultiplicative()
        {
            Node left = ParseUnary();
            while (Match("*", out int column) || Match("/", out column) || Match("%", out column))
            {
                string op = _tokens[_index - 1].Text;
                left = new BinaryNode(op, left, ParseUnary(), column);
            }
            return left;
        }

        private Node ParseUnary()
        {
            if (Match("!", out int column) || Match("-", out column))
            {
                string op = _tokens[_index - 1].Text;
                Enter(column);
                try
                {
                    return new UnaryNode(op, ParseUnary(), column);
                }
                finally
                {
                    _depth--;
                }
            }
            return ParsePrimary();
        }

        private Node ParsePrimary()
        {
            Token token = Current;
            switch (token.Kind)
            {
                case TokenKind.Number:
                    _index++;
                    return new LiteralNode(FlowValue.Number(token.Number));

                case TokenKind.Text:
                    _index++;
                    return new LiteralNode(FlowValue.Text(token.Text));

                case TokenKind.Identifier:
                    _index++;
                    if (Current.Kind == TokenKind.LeftParen) return ParseCall(token);
                    if (token.Text == "true") return new LiteralNode(FlowValue.Bool(true));
                    if (token.Text == "false") return new LiteralNode(FlowValue.Bool(false));
                    if (token.Text is "and" or "or" or "not")
                        throw Error(token.Column, $"这里用 C 风格：{token.Text} 应写成 &&、|| 或 !");
                    return new VariableNode(token.Text, token.Column);

                case TokenKind.LeftParen:
                {
                    _index++;
                    Enter(token.Column);
                    try
                    {
                        Node inner = ParseOr();
                        Expect(TokenKind.RightParen, "）");
                        return inner;
                    }
                    finally
                    {
                        _depth--;
                    }
                }

                case TokenKind.End:
                    throw Error(token.Column, "表达式还没写完");

                default:
                    throw Error(token.Column, $"这里需要一个表达式，而不是「{token.Text}」");
            }
        }

        private Node ParseCall(Token name)
        {
            _index++; // (
            var arguments = new List<Node>();
            if (Current.Kind != TokenKind.RightParen)
            {
                while (true)
                {
                    Enter(name.Column);
                    try
                    {
                        arguments.Add(ParseOr());
                    }
                    finally
                    {
                        _depth--;
                    }
                    if (!Match(",", out _)) break;
                }
            }
            Expect(TokenKind.RightParen, "）");
            ValidateFunction(name, arguments.Count);
            return new CallNode(name.Text, arguments, name.Column);
        }

        private void ValidateFunction(Token name, int count)
        {
            switch (name.Text)
            {
                case "text":
                    if (count is not (1 or 5))
                        throw Error(name.Column,
                            "text() 需要 1 个参数（要读的文字）或 5 个参数（文字, x, y, 宽, 高）");
                    break;
                case "window":
                case "front":
                    if (count != 1) throw Error(name.Column, $"{name.Text}() 需要 1 个参数（进程名）");
                    break;
                case "found":
                    if (count > 1) throw Error(name.Column, "found() 最多 1 个参数（想找的文字）");
                    break;
                case "elapsed":
                    if (count != 0) throw Error(name.Column, "elapsed() 不接受参数");
                    break;
                default:
                    throw Error(name.Column,
                        $"不认识的函数 '{name.Text}'（可用：text | window | front | found | elapsed）");
            }
        }

        private void Expect(TokenKind kind, string what)
        {
            if (Current.Kind == kind)
            {
                _index++;
                return;
            }
            throw Error(Current.Column, $"这里需要 {what}");
        }

        private bool Match(string op, out int column)
        {
            if ((Current.Kind == TokenKind.Operator || Current.Kind == TokenKind.Comma) && Current.Text == op)
            {
                column = Current.Column;
                _index++;
                return true;
            }
            column = 0;
            return false;
        }

        private void Enter(int column)
        {
            if (++_depth > MaxDepth)
            {
                throw Error(column, $"嵌套太深（最多 {MaxDepth} 层）——写简单点，或者拆成几个 set");
            }
        }

        // -------------------------------------------------------------- lexer

        private void Lex()
        {
            int i = 0;
            while (i < _source.Length)
            {
                char c = _source[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                int column = i + 1;
                if (char.IsDigit(c) || (c == '.' && i + 1 < _source.Length && char.IsDigit(_source[i + 1])))
                {
                    i = LexNumber(i, column);
                    continue;
                }
                if (c is '"' or '\'')
                {
                    i = LexText(i, column, c);
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    i++;
                    while (i < _source.Length && (char.IsLetterOrDigit(_source[i]) || _source[i] is '_' or '.')) i++;
                    _tokens.Add(new Token(TokenKind.Identifier, _source[start..i], 0, column));
                    continue;
                }

                if (i + 1 < _source.Length)
                {
                    string pair = _source.Substring(i, 2);
                    if (pair is "&&" or "||" or "==" or "!=" or "<=" or ">=")
                    {
                        _tokens.Add(new Token(TokenKind.Operator, pair, 0, column));
                        i += 2;
                        continue;
                    }
                }

                switch (c)
                {
                    case '+':
                    case '-':
                    case '*':
                    case '/':
                    case '%':
                    case '!':
                    case '<':
                    case '>':
                        _tokens.Add(new Token(TokenKind.Operator, c.ToString(), 0, column));
                        i++;
                        continue;
                    case '(':
                        _tokens.Add(new Token(TokenKind.LeftParen, "(", 0, column));
                        i++;
                        continue;
                    case ')':
                        _tokens.Add(new Token(TokenKind.RightParen, ")", 0, column));
                        i++;
                        continue;
                    case ',':
                        _tokens.Add(new Token(TokenKind.Comma, ",", 0, column));
                        i++;
                        continue;
                    case '=':
                        throw Error(column, "比较用 ==；赋值请用 set 步骤或 for 的 init/step");
                    case '&':
                    case '|':
                        throw Error(column, $"逻辑运算用 && 或 ||（单个 {c} 不认识）");
                    case '?':
                    case ':':
                        throw Error(column, "不支持三目运算：把条件写进 if / else 块");
                    default:
                        throw Error(column, $"不认识的字符 '{c}'");
                }
            }

            _tokens.Add(new Token(TokenKind.End, "", 0, _source.Length + 1));
        }

        private int LexNumber(int start, int column)
        {
            int i = start;
            while (i < _source.Length && char.IsDigit(_source[i])) i++;
            if (i < _source.Length && _source[i] == '.')
            {
                i++;
                while (i < _source.Length && char.IsDigit(_source[i])) i++;
            }
            string raw = _source[start..i];
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                !double.IsFinite(value))
            {
                throw Error(column, $"数字 '{raw}' 读不出来");
            }
            _tokens.Add(new Token(TokenKind.Number, raw, value, column));
            return i;
        }

        private int LexText(int start, int column, char quote)
        {
            var text = new StringBuilder();
            int i = start + 1;
            while (i < _source.Length)
            {
                char c = _source[i];
                if (c == quote)
                {
                    _tokens.Add(new Token(TokenKind.Text, text.ToString(), 0, column));
                    return i + 1;
                }
                if (c != '\\')
                {
                    text.Append(c);
                    i++;
                    continue;
                }

                if (i + 1 >= _source.Length) throw Error(i + 1, "字符串结尾的反斜杠后面什么都没有");
                char escaped = _source[i + 1];
                text.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '\\' => '\\',
                    '"' => '"',
                    '\'' => '\'',
                    _ => throw Error(i + 1, $"不认识的转义 \\{escaped}"),
                });
                i += 2;
            }
            throw Error(column, "字符串没有闭合");
        }

        private CommandFailure Error(int column, string detail) =>
            Expression.Error(_where, _source, column, detail);
    }
}
