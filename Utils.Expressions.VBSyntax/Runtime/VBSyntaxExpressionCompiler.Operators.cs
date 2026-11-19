using System.Linq.Expressions;
using Utils.Parser.Runtime;

namespace Utils.Expressions.VBSyntax.Runtime;

public sealed partial class VBSyntaxExpressionCompiler
{
    // ── Operator token tables (token rule name → operator) ──────────────────

    /// <summary>Operators of <c>operation_or</c>.</summary>
    private static readonly IReadOnlyDictionary<string, string> OrOperators =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["ORELSE"] = "OrElse", ["OR"] = "Or", ["XOR"] = "Xor" };

    /// <summary>Operators of <c>operation_and</c>.</summary>
    private static readonly IReadOnlyDictionary<string, string> AndOperators =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["ANDALSO"] = "AndAlso", ["AND"] = "And" };

    /// <summary>Operators of <c>operation_equality</c>.</summary>
    private static readonly IReadOnlyDictionary<string, string> EqualityOperators =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["OP_EQUAL"] = "=", ["OP_NOT_EQUAL"] = "<>" };

    /// <summary>Operators of <c>operation_relational</c>.</summary>
    private static readonly IReadOnlyDictionary<string, string> RelationalOperators =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OP_LESS"] = "<", ["OP_LESS_EQUAL"] = "<=", ["OP_GREATER"] = ">", ["OP_GREATER_EQUAL"] = ">=",
        };

    /// <summary>Operators of <c>operation_concat</c>.</summary>
    private static readonly IReadOnlyDictionary<string, string> ConcatOperators =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["OP_CONCAT"] = "&" };

    /// <summary>Operators of <c>operation_shift</c>.</summary>
    private static readonly IReadOnlyDictionary<string, string> ShiftOperators =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["OP_SHIFT_LEFT"] = "<<", ["OP_SHIFT_RIGHT"] = ">>" };

    /// <summary>Operators of <c>operation_plus</c>.</summary>
    private static readonly IReadOnlyDictionary<string, string> AdditiveOperators =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["OP_PLUS"] = "+", ["OP_MINUS"] = "-" };

    /// <summary>Operators of <c>operation_mul</c>.</summary>
    private static readonly IReadOnlyDictionary<string, string> MultiplicativeOperators =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["OP_MULTIPLY"] = "*", ["OP_DIVIDE"] = "/", ["MOD"] = "Mod" };

    /// <summary>Operators of <c>operation_power</c>.</summary>
    private static readonly IReadOnlyDictionary<string, string> PowerOperators =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["OP_POWER"] = "^" };

    // ── OrElse / Or / Xor ────────────────────────────────────────────────────

    /// <summary>
    /// Compiles <c>Or</c>, <c>OrElse</c>, and <c>Xor</c> binary operators.
    /// <c>OrElse</c> short-circuits; <c>Or</c> is bitwise/non-short-circuit.
    /// </summary>
    private static Expression? CompileLogicalOr(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
        => CompileOperatorChain(nav, children, OrOperators,
            static (op, left, right) => op switch
            {
                "OrElse" => Expression.OrElse(left, right),
                "Xor" => Expression.ExclusiveOr(left, right),
                _ => Expression.Or(left, right),
            });

    // ── AndAlso / And ────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles <c>And</c> and <c>AndAlso</c> binary operators.
    /// </summary>
    private static Expression? CompileLogicalAnd(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
        => CompileOperatorChain(nav, children, AndOperators,
            static (op, left, right) => op == "AndAlso"
                ? Expression.AndAlso(left, right)
                : Expression.And(left, right));

    // ── Not ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles the unary <c>Not</c> operator. The operator is read from the parse tree (a leading <c>NOT</c> token),
    /// never from the source text, so an operand such as <c>Nothing</c> or an identifier starting with <c>Not</c> is
    /// not negated.
    /// </summary>
    private static Expression? CompileLogicalNot(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        Expression? inner = children.FirstOrDefault(static c => c is not null);
        if (inner is null) return null;
        return StartsWithToken(nav, "NOT") ? Expression.Not(inner) : inner;
    }

    // ── Equality: = and <> ────────────────────────────────────────────────────

    /// <summary>
    /// Compiles <c>=</c> (equality) and <c>&lt;&gt;</c> (inequality) comparisons.
    /// </summary>
    private static Expression? CompileEquality(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
        => CompileOperatorChain(nav, children, EqualityOperators,
            static (op, left, right) => op == "<>"
                ? Expression.NotEqual(left, right)
                : Expression.Equal(left, right));

    // ── Relational: < <= > >= ─────────────────────────────────────────────────

    /// <summary>
    /// Compiles relational comparison operators.
    /// </summary>
    private static Expression? CompileRelational(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
        => CompileOperatorChain(nav, children, RelationalOperators,
            static (op, l, r) => op switch
            {
                "<=" => Expression.LessThanOrEqual(l, r),
                ">=" => Expression.GreaterThanOrEqual(l, r),
                ">"  => Expression.GreaterThan(l, r),
                _    => Expression.LessThan(l, r),
            });

    // ── String concatenation: & ───────────────────────────────────────────────

    /// <summary>
    /// Compiles the <c>&amp;</c> string concatenation operator into one <see cref="string.Concat(string[])"/> call over
    /// every operand of the chain.
    /// </summary>
    private static Expression? CompileConcat(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        List<Expression> operands = FlattenChainOperands(children);
        if (IsChainSegment(nav.Node)) return operands.Count == 0 ? null : new ChainOperands(operands);
        if (operands.Count == 0) return null;
        if (operands.Count == 1) return operands[0];
        RequireOperatorCount(nav, ConcatOperators, operands.Count);

        var toStringMethod = typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes)!;
        var concatMethod = typeof(string).GetMethod(nameof(string.Concat), [typeof(string[])])!;

        Expression[] parts = operands
            .Select(e => e.Type == typeof(string)
                ? e
                : Expression.Call(e, toStringMethod))
            .ToArray();

        return Expression.Call(concatMethod, Expression.NewArrayInit(typeof(string), parts));
    }

    // ── Shift: << >> ─────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles <c>&lt;&lt;</c> and <c>&gt;&gt;</c> shift operators.
    /// </summary>
    private static Expression? CompileShift(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
        => CompileOperatorChain(nav, children, ShiftOperators,
            static (op, l, r) => op == "<<"
                ? Expression.LeftShift(l, r)
                : Expression.RightShift(l, r));

    // ── Addition / Subtraction ────────────────────────────────────────────────

    /// <summary>
    /// Compiles <c>+</c> and <c>-</c> additive operators.
    /// </summary>
    private static Expression? CompileAddSub(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
        => CompileOperatorChain(nav, children, AdditiveOperators,
            static (op, l, r) => op == "-"
                ? Expression.Subtract(l, ConvertNumeric(r, l.Type))
                : Expression.Add(l, ConvertNumeric(r, l.Type)));

    // ── Multiplication / Division / Mod ───────────────────────────────────────

    /// <summary>
    /// Compiles <c>*</c>, <c>/</c>, and <c>Mod</c> operators.
    /// </summary>
    private static Expression? CompileMulDivMod(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
        => CompileOperatorChain(nav, children, MultiplicativeOperators,
            static (op, l, r) => op switch
            {
                "/"   => Expression.Divide(l, ConvertNumeric(r, l.Type)),
                "Mod" => Expression.Modulo(l, ConvertNumeric(r, l.Type)),
                _     => Expression.Multiply(l, ConvertNumeric(r, l.Type)),
            });

    // ── Unary negate / plus ───────────────────────────────────────────────────

    /// <summary>
    /// Compiles unary <c>-</c> and <c>+</c> operators, read from the leading token of the node.
    /// </summary>
    private static Expression? CompileNegate(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        Expression? inner = children.FirstOrDefault(static c => c is not null);
        if (inner is null) return null;
        return StartsWithToken(nav, "OP_MINUS") ? Expression.Negate(inner) : inner;
    }

    // ── Power: ^ ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles the <c>^</c> exponentiation operator using <see cref="Math.Pow"/>. Chains are right-associative:
    /// <c>a ^ b ^ c</c> is <c>a ^ (b ^ c)</c>.
    /// </summary>
    private static Expression? CompilePower(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
        => CompileOperatorChain(nav, children, PowerOperators,
            static (_, l, r) => Expression.Power(
                ConvertNumeric(l, typeof(double)),
                ConvertNumeric(r, typeof(double))),
            rightAssociative: true);

    // ── Operator chains ───────────────────────────────────────────────────────

    /// <summary>
    /// Compiles one node of a binary operator chain rule <c>R : O (op O)*</c>.
    /// <para>
    /// The parse tree of such a rule contains synthetic nodes that carry the rule's name: the quantifier wrapper, one
    /// node per repetition (<c>op O</c>) and one node per operator group. The compiler visits them like the rule node
    /// itself, so they must not fold anything: they only return their operands (<see cref="ChainOperands"/>). The rule
    /// node then folds every operand of the chain with the operators read from the tokens of those synthetic nodes, in
    /// source order, without looking inside the operands (a parenthesized operand keeps its own operators).
    /// </para>
    /// </summary>
    /// <param name="nav">Current node.</param>
    /// <param name="children">Compiled children.</param>
    /// <param name="operatorTokens">Operator token rule names of the chain, mapped to the operator passed to <paramref name="combine"/>.</param>
    /// <param name="combine">Builds the expression of one operator applied to its two operands.</param>
    /// <param name="rightAssociative">Whether the chain folds from the right instead of the left.</param>
    /// <returns>The folded expression, the operand carrier of a synthetic node, or <see langword="null"/>.</returns>
    /// <exception cref="InvalidOperationException">The operators found do not match the operands of the chain.</exception>
    private static Expression? CompileOperatorChain(
        ParseTreeNavigator nav,
        IReadOnlyList<Expression?> children,
        IReadOnlyDictionary<string, string> operatorTokens,
        Func<string, Expression, Expression, Expression> combine,
        bool rightAssociative = false)
    {
        List<Expression> operands = FlattenChainOperands(children);
        if (IsChainSegment(nav.Node)) return operands.Count == 0 ? null : new ChainOperands(operands);
        if (operands.Count == 0) return null;
        if (operands.Count == 1) return operands[0];

        List<string> operators = RequireOperatorCount(nav, operatorTokens, operands.Count);
        if (rightAssociative)
        {
            Expression right = operands[^1];
            for (int i = operands.Count - 2; i >= 0; i--)
                right = combine(operators[i], operands[i], right);
            return right;
        }

        Expression left = operands[0];
        for (int i = 1; i < operands.Count; i++)
            left = combine(operators[i - 1], left, operands[i]);
        return left;
    }

    /// <summary>
    /// Reads the operators of the chain rooted at <paramref name="nav"/> and checks that there is exactly one between
    /// each pair of consecutive operands.
    /// </summary>
    /// <param name="nav">The chain rule node.</param>
    /// <param name="operatorTokens">Operator token rule names of the chain.</param>
    /// <param name="operandCount">The number of operands.</param>
    /// <returns>The operators in source order.</returns>
    /// <exception cref="InvalidOperationException">The operator count differs from <paramref name="operandCount"/> - 1.</exception>
    private static List<string> RequireOperatorCount(ParseTreeNavigator nav, IReadOnlyDictionary<string, string> operatorTokens, int operandCount)
    {
        var operators = new List<string>();
        CollectChainOperators(nav.Node, nav.RuleName, operatorTokens, operators);
        if (operators.Count != operandCount - 1)
        {
            throw new InvalidOperationException(
                $"Operator chain '{nav.RuleName}' at position {nav.Node.Span.Position} has {operandCount} operands but {operators.Count} operators.");
        }

        return operators;
    }

    /// <summary>
    /// Collects, in source order, the operator tokens of a chain: tokens that are direct children of the chain node or
    /// of its synthetic nodes (quantifier, repetitions, operator groups). Operand nodes are not entered.
    /// </summary>
    /// <param name="node">The chain node or one of its synthetic nodes.</param>
    /// <param name="ruleName">The chain rule name.</param>
    /// <param name="operatorTokens">Operator token rule names of the chain.</param>
    /// <param name="operators">Receives the operators.</param>
    private static void CollectChainOperators(ParseNode node, string ruleName, IReadOnlyDictionary<string, string> operatorTokens, List<string> operators)
    {
        if (node is not ParserNode parent) return;
        foreach (ParseNode child in parent.Children)
        {
            switch (child)
            {
                case LexerNode lexer when operatorTokens.TryGetValue(lexer.Token.RuleName, out string? op):
                    operators.Add(op);
                    break;
                case QuantifierNode:
                case ParserNode when child.Rule?.Name == ruleName:
                    // Synthetic node of the same chain (a real rule invocation is always below an operand rule).
                    CollectChainOperators(child, ruleName, operatorTokens, operators);
                    break;
            }
        }
    }

    /// <summary>
    /// Determines whether a node of a chain rule is one of its synthetic nodes rather than the rule invocation itself:
    /// the quantifier wrapper, a repetition (starting with its operator) or an operator group. The rule invocation
    /// always starts with an operand node of another rule.
    /// </summary>
    /// <param name="node">A node carrying the chain rule's name.</param>
    /// <returns><see langword="true"/> for a synthetic node.</returns>
    private static bool IsChainSegment(ParseNode node)
    {
        if (node is QuantifierNode) return true;
        if (node is not ParserNode { Children.Count: > 0 } parserNode) return false;
        ParseNode first = parserNode.Children[0];
        return first is LexerNode || (first is ParserNode && first.Rule?.Name == node.Rule?.Name);
    }

    /// <summary>Flattens compiled children into operands, expanding the carriers returned by synthetic chain nodes.</summary>
    /// <param name="children">Compiled children.</param>
    /// <returns>The operands in source order.</returns>
    private static List<Expression> FlattenChainOperands(IReadOnlyList<Expression?> children)
    {
        var operands = new List<Expression>(children.Count);
        foreach (Expression? child in children)
        {
            if (child is ChainOperands segment) operands.AddRange(segment.Operands);
            else if (child is not null) operands.Add(child);
        }

        return operands;
    }

    /// <summary>Determines whether the first raw child of a node is a token of the given rule.</summary>
    /// <param name="nav">The node.</param>
    /// <param name="tokenRuleName">The token rule name.</param>
    /// <returns><see langword="true"/> when the node starts with that token.</returns>
    private static bool StartsWithToken(ParseTreeNavigator nav, string tokenRuleName)
        => nav.RawChildren is [LexerNode { Token.RuleName: var name }, ..] && name == tokenRuleName;

    /// <summary>
    /// Operands of a synthetic chain node, handed to the enclosing chain node. It never reaches a compiled expression:
    /// the chain node always flattens it.
    /// </summary>
    private sealed class ChainOperands(IReadOnlyList<Expression> operands) : Expression
    {
        /// <summary>Gets the operands in source order.</summary>
        public IReadOnlyList<Expression> Operands { get; } = operands;

        /// <inheritdoc />
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc />
        public override Type Type => typeof(void);
    }
    // ── Numeric type conversion ───────────────────────────────────────────────

    /// <summary>
    /// Converts <paramref name="expr"/> to <paramref name="targetType"/> when both are numeric.
    /// </summary>
    private static Expression ConvertNumeric(Expression expr, Type targetType)
    {
        if (expr.Type == targetType) return expr;
        if (IsNumericType(expr.Type) && IsNumericType(targetType))
            return Expression.Convert(expr, targetType);
        return expr;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> is a primitive numeric type.
    /// </summary>
    private static bool IsNumericType(Type type)
    {
        Type t = Nullable.GetUnderlyingType(type) ?? type;
        return t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort)
            || t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong)
            || t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    }
}
