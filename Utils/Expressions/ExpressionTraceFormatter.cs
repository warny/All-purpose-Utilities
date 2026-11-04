using System.Globalization;
using System.Linq.Expressions;
using System.Text;

namespace Utils.Expressions;

/// <summary>
/// Renders expression trees for diagnostics (trace exports) without running user code: bounded in depth and length, and blind
/// to the content of extension nodes.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="Expression.ToString"/>, the rendering never calls <see cref="object.ToString"/> on a constant of a
/// non-primitive type (shown as <c>&lt;TypeName&gt;</c>), and never reads a member of an extension node (shown as
/// <c>&lt;extension ClassName&gt;</c>): only type tests and the non-virtual <see cref="object.GetType"/> are used on it (ETC R4).
/// Framework node families are rendered in a compact C#-like syntax; the control-flow families (blocks, loops, try, switch,
/// goto, labels) are summarized.
/// </para>
/// <para>
/// The rendering is a diagnostic aid, not a parser input: parameters are shown by name (unnamed ones as <c>$0</c>, <c>$1</c>,
/// … in order of appearance), so two distinct parameters with the same name look alike.
/// </para>
/// </remarks>
public static class ExpressionTraceFormatter
{
    /// <summary>The default maximum depth rendered before a subtree is elided.</summary>
    public const int DefaultMaxDepth = 32;

    /// <summary>The default maximum length of a rendering.</summary>
    public const int DefaultMaxLength = 512;

    /// <summary>The marker of an elided subtree or a truncated rendering.</summary>
    private const string Ellipsis = "…";

    /// <summary>The name rendered for a method or member that is not a runtime reflection object (S5 A-10).</summary>
    private const string CustomMember = "<custom member>";

    /// <summary>Renders <paramref name="expression"/>.</summary>
    /// <param name="expression">The tree, or <see langword="null"/>.</param>
    /// <param name="maxDepth">The maximum depth rendered; deeper subtrees are shown as <c>…</c>.</param>
    /// <param name="maxLength">The maximum length of the result; a longer rendering is cut and ends with <c>…</c>.</param>
    /// <returns>The rendering.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDepth"/> or <paramref name="maxLength"/> is not positive.</exception>
    public static string Format(Expression? expression, int maxDepth = DefaultMaxDepth, int maxLength = DefaultMaxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        var writer = new Writer(maxDepth, maxLength);
        writer.Write(expression, 0);
        return writer.ToString();
    }

    /// <summary>The state of one rendering.</summary>
    private sealed class Writer
    {
        /// <summary>The rendered text.</summary>
        private readonly StringBuilder _text = new();

        /// <summary>The maximum depth rendered.</summary>
        private readonly int _maxDepth;

        /// <summary>The maximum length of the text.</summary>
        private readonly int _maxLength;

        /// <summary>The names given to unnamed parameters, by reference.</summary>
        private Dictionary<ParameterExpression, int>? _unnamed;

        /// <summary>Initializes a rendering.</summary>
        /// <param name="maxDepth">The maximum depth rendered.</param>
        /// <param name="maxLength">The maximum length of the text.</param>
        public Writer(int maxDepth, int maxLength)
        {
            _maxDepth = maxDepth;
            _maxLength = maxLength;
        }

        /// <summary>Gets whether the length limit is reached.</summary>
        private bool Full => _text.Length >= _maxLength;

        /// <summary>Returns the text, cut to the length limit.</summary>
        /// <returns>The rendering.</returns>
        public override string ToString()
            => _text.Length <= _maxLength ? _text.ToString() : _text.ToString(0, _maxLength) + Ellipsis;

        /// <summary>Appends text unless the length limit is reached.</summary>
        /// <param name="text">The text.</param>
        private void Append(string text)
        {
            if (!Full) _text.Append(text);
        }

        /// <summary>Renders one node (recursive, depth-bounded).</summary>
        /// <param name="node">The node.</param>
        /// <param name="depth">Its depth.</param>
        public void Write(Expression? node, int depth)
        {
            if (Full) return;
            if (node is null)
            {
                Append("null");
                return;
            }

            if (ExpressionOpacity.IsOpaque(node))
            {
                Append("<extension " + node.GetType().Name + ">");
                return;
            }

            if (depth >= _maxDepth)
            {
                Append(Ellipsis);
                return;
            }

            // The rendering is bounded by maxDepth, but a caller may pass a large one: keep the stack guarded.
            ExpressionResourceBudget.EnsureStack();
            int next = depth + 1;

            // Node-family dispatch (Utils/AGENTS.md local exception): every branch delegates to one writer.
            switch (node)
            {
                case ConstantExpression constant: Append(FormatConstant(constant)); break;
                case ParameterExpression parameter: Append(NameOf(parameter)); break;
                case BinaryExpression binary: WriteBinary(binary, next); break;
                case UnaryExpression unary: WriteUnary(unary, next); break;
                // S5 A-10: the name of a reflection object that is not a runtime one is user code and is never read.
                case MethodCallExpression call when !ExpressionReflectionProvenance.IsRuntime(call.Method): WriteAccess(call.Object, null, CustomMember, call.Arguments, next); break;
                case MethodCallExpression call: WriteAccess(call.Object, call.Method.DeclaringType, call.Method.Name, call.Arguments, next); break;
                case MemberExpression member when !ExpressionReflectionProvenance.IsRuntime(member.Member): WriteAccess(member.Expression, null, CustomMember, null, next); break;
                case MemberExpression member: WriteAccess(member.Expression, member.Member.DeclaringType, member.Member.Name, null, next); break;
                case LambdaExpression lambda: WriteLambda(lambda, next); break;
                case ConditionalExpression conditional: WriteConditional(conditional, next); break;
                case InvocationExpression invocation: WriteApplication(invocation.Expression, "(", invocation.Arguments, ")", next); break;
                case NewExpression creation: WriteList("new " + creation.Type.Name + "(", creation.Arguments, ")", next); break;
                case NewArrayExpression array: WriteNewArray(array, next); break;
                case TypeBinaryExpression typeBinary: WriteTypeTest(typeBinary, next); break;
                case DefaultExpression @default: Append("default(" + @default.Type.Name + ")"); break;
                case IndexExpression index: WriteApplication(index.Object, "[", index.Arguments, "]", next); break;
                case BlockExpression block: WriteList("{ ", block.Expressions, " }", next, "; "); break;
                case DynamicExpression dynamic: WriteList("dynamic(", dynamic.Arguments, ")", next); break;
                case MemberInitExpression memberInit: Append("new " + memberInit.Type.Name + " { " + Ellipsis + " }"); break;
                case ListInitExpression listInit: Append("new " + listInit.Type.Name + " { " + Ellipsis + " }"); break;

                // Loop, Try, Switch, Goto, Label, RuntimeVariables, DebugInfo: summarized by their node type.
                default: Append("<" + node.NodeType + ">"); break;
            }
        }

        /// <summary>Renders a binary node in infix form when it has a symbol, otherwise as a call.</summary>
        /// <param name="binary">The node.</param>
        /// <param name="depth">The depth of its operands.</param>
        private void WriteBinary(BinaryExpression binary, int depth)
        {
            if (binary.NodeType == ExpressionType.ArrayIndex)
            {
                WriteApplication(binary.Left, "[", [binary.Right], "]", depth);
                return;
            }

            string? symbol = SymbolOf(binary.NodeType);
            if (symbol is null)
            {
                WriteList<Expression>(binary.NodeType + "(", [binary.Left, binary.Right], ")", depth);
                return;
            }

            WriteOperand(binary.Left, depth);
            Append(" " + symbol + " ");
            WriteOperand(binary.Right, depth);
        }

        /// <summary>Renders a unary node as a prefix, a suffix, a cast or a call.</summary>
        /// <param name="unary">The node.</param>
        /// <param name="depth">The depth of its operand.</param>
        private void WriteUnary(UnaryExpression unary, int depth)
        {
            (string prefix, string suffix, bool operand) = unary.NodeType switch
            {
                ExpressionType.Negate or ExpressionType.NegateChecked => ("-", "", true),
                ExpressionType.UnaryPlus => ("+", "", true),
                ExpressionType.Convert or ExpressionType.ConvertChecked => ("(" + unary.Type.Name + ")", "", true),
                ExpressionType.ArrayLength => ("", ".Length", true),
                ExpressionType.TypeAs => ("(", " as " + unary.Type.Name + ")", false),
                ExpressionType.Quote => ("quote(", ")", false),
                ExpressionType.Throw => ("throw ", "", false),
                _ => (unary.NodeType + "(", ")", false),
            };

            Append(prefix);
            if (operand) WriteOperand(unary.Operand, depth);
            else Write(unary.Operand, depth);
            Append(suffix);
        }

        /// <summary>Renders a member access or a method call: <c>receiver.Name</c> or <c>Type.Name</c>, with its arguments.</summary>
        /// <param name="receiver">The instance, or <see langword="null"/> for a static member.</param>
        /// <param name="declaringType">The declaring type, shown for a static member.</param>
        /// <param name="name">The member name.</param>
        /// <param name="arguments">The call arguments, or <see langword="null"/> for a member access.</param>
        /// <param name="depth">The depth of the children.</param>
        private void WriteAccess(Expression? receiver, Type? declaringType, string name, IReadOnlyList<Expression>? arguments, int depth)
        {
            if (receiver is null) Append(declaringType?.Name ?? "?");
            else WriteOperand(receiver, depth);
            Append("." + name);
            if (arguments is not null) WriteList("(", arguments, ")", depth);
        }

        /// <summary>Renders a lambda: <c>(parameters) =&gt; body</c>.</summary>
        /// <param name="lambda">The lambda.</param>
        /// <param name="depth">The depth of its children.</param>
        private void WriteLambda(LambdaExpression lambda, int depth)
        {
            WriteList("(", lambda.Parameters, ") => ", depth);
            Write(lambda.Body, depth);
        }

        /// <summary>Renders a conditional: <c>(test ? ifTrue : ifFalse)</c>.</summary>
        /// <param name="conditional">The conditional.</param>
        /// <param name="depth">The depth of its children.</param>
        private void WriteConditional(ConditionalExpression conditional, int depth)
        {
            Append("(");
            Write(conditional.Test, depth);
            Append(" ? ");
            Write(conditional.IfTrue, depth);
            Append(" : ");
            Write(conditional.IfFalse, depth);
            Append(")");
        }

        /// <summary>Renders an invocation or an indexing: the target followed by its arguments.</summary>
        /// <param name="target">The invoked or indexed node.</param>
        /// <param name="open">The opening delimiter.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="close">The closing delimiter.</param>
        /// <param name="depth">The depth of the children.</param>
        private void WriteApplication(Expression? target, string open, IReadOnlyList<Expression> arguments, string close, int depth)
        {
            WriteOperand(target, depth);
            WriteList(open, arguments, close, depth);
        }

        /// <summary>Renders an array creation, with its bounds or its elements.</summary>
        /// <param name="array">The creation.</param>
        /// <param name="depth">The depth of its children.</param>
        private void WriteNewArray(NewArrayExpression array, int depth)
        {
            string element = "new " + (array.Type.GetElementType()?.Name ?? "?");
            if (array.NodeType == ExpressionType.NewArrayBounds) WriteList(element + "[", array.Expressions, "]", depth);
            else WriteList(element + "[] { ", array.Expressions, " }", depth);
        }

        /// <summary>Renders a type test: <c>(operand is Type)</c>.</summary>
        /// <param name="typeBinary">The type test.</param>
        /// <param name="depth">The depth of its operand.</param>
        private void WriteTypeTest(TypeBinaryExpression typeBinary, int depth)
        {
            Append("(");
            Write(typeBinary.Expression, depth);
            Append((typeBinary.NodeType == ExpressionType.TypeEqual ? " is exactly " : " is ") + typeBinary.TypeOperand.Name + ")");
        }
        /// <summary>Renders an operand, in parentheses when it is itself an infix or prefix node.</summary>
        /// <param name="operand">The operand.</param>
        /// <param name="depth">Its depth.</param>
        private void WriteOperand(Expression? operand, int depth)
        {
            bool parenthesize = operand is BinaryExpression { NodeType: not ExpressionType.ArrayIndex } binary && SymbolOf(binary.NodeType) is not null
                || operand is UnaryExpression { NodeType: ExpressionType.Negate or ExpressionType.NegateChecked or ExpressionType.UnaryPlus or ExpressionType.Convert or ExpressionType.ConvertChecked or ExpressionType.Throw };
            if (parenthesize) Append("(");
            Write(operand, depth);
            if (parenthesize) Append(")");
        }

        /// <summary>Renders a list of nodes between delimiters.</summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="open">The opening delimiter.</param>
        /// <param name="items">The nodes.</param>
        /// <param name="close">The closing delimiter.</param>
        /// <param name="depth">Their depth.</param>
        /// <param name="separator">The separator.</param>
        private void WriteList<T>(string open, IReadOnlyList<T> items, string close, int depth, string separator = ", ")
            where T : Expression
        {
            Append(open);
            for (int i = 0; i < items.Count && !Full; i++)
            {
                if (i > 0) Append(separator);
                Write(items[i], depth);
            }

            Append(close);
        }

        /// <summary>Gets the displayed name of a parameter.</summary>
        /// <param name="parameter">The parameter.</param>
        /// <returns>Its name, or <c>$n</c> for an unnamed parameter.</returns>
        private string NameOf(ParameterExpression parameter)
        {
            if (!string.IsNullOrEmpty(parameter.Name)) return parameter.Name;

            _unnamed ??= new Dictionary<ParameterExpression, int>(ReferenceEqualityComparer.Instance);
            if (!_unnamed.TryGetValue(parameter, out int number))
            {
                number = _unnamed.Count;
                _unnamed.Add(parameter, number);
            }

            return "$" + number.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Renders a constant without running user code: the value of a primitive, decimal, string, char, Boolean or enumeration
    /// constant (framework formatting, invariant culture), otherwise only its type.
    /// </summary>
    /// <param name="constant">The constant.</param>
    /// <returns>The rendering.</returns>
    private static string FormatConstant(ConstantExpression constant) => constant.Value switch
    {
        null => "null",
        bool value => value ? "true" : "false",
        string value => "\"" + (value.Length <= 40 ? value : value[..40] + Ellipsis) + "\"",
        char value => "'" + value + "'",
        double value => value.ToString("R", CultureInfo.InvariantCulture),
        float value => value.ToString("R", CultureInfo.InvariantCulture) + "f",
        decimal value => value.ToString(CultureInfo.InvariantCulture) + "m",
        Enum value when value.GetType().IsEnum => value.GetType().Name + "." + value,
        sbyte or byte or short or ushort or int or uint or long or ulong or nint or nuint
            => ((IFormattable)constant.Value).ToString(null, CultureInfo.InvariantCulture),
        _ => "<" + constant.Value.GetType().Name + ">",
    };

    /// <summary>Gets the infix symbol of a binary node type, or <see langword="null"/> when it is rendered as a call.</summary>
    /// <param name="nodeType">The node type.</param>
    /// <returns>The symbol, or <see langword="null"/>.</returns>
    private static string? SymbolOf(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Add or ExpressionType.AddChecked => "+",
        ExpressionType.Subtract or ExpressionType.SubtractChecked => "-",
        ExpressionType.Multiply or ExpressionType.MultiplyChecked => "*",
        ExpressionType.Divide => "/",
        ExpressionType.Modulo => "%",
        ExpressionType.Power => "**",
        ExpressionType.And => "&",
        ExpressionType.Or => "|",
        ExpressionType.ExclusiveOr => "^",
        ExpressionType.AndAlso => "&&",
        ExpressionType.OrElse => "||",
        ExpressionType.Equal => "==",
        ExpressionType.NotEqual => "!=",
        ExpressionType.LessThan => "<",
        ExpressionType.LessThanOrEqual => "<=",
        ExpressionType.GreaterThan => ">",
        ExpressionType.GreaterThanOrEqual => ">=",
        ExpressionType.Coalesce => "??",
        ExpressionType.LeftShift => "<<",
        ExpressionType.RightShift => ">>",
        ExpressionType.Assign => "=",
        _ => null,
    };
}
