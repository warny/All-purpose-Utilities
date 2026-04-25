using System.Linq.Expressions;
using Utils.Parser.Runtime;

namespace Utils.Expressions.VBSyntax.Runtime;

public sealed partial class VBSyntaxExpressionCompiler
{
    // ── OrElse / Or / Xor ────────────────────────────────────────────────────

    /// <summary>
    /// Compiles <c>Or</c>, <c>OrElse</c>, and <c>Xor</c> binary operators.
    /// <c>OrElse</c> short-circuits; <c>Or</c> is bitwise/non-short-circuit.
    /// </summary>
    private static Expression? CompileLogicalOr(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        var operands = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (operands.Count == 0) return null;
        if (operands.Count == 1) return operands[0];

        string source = GetNodeSourceText(context, nav);
        Expression result = operands[0];
        int pos = 0;
        for (int i = 1; i < operands.Count; i++)
        {
            // Find the operator between operand i-1 and operand i in the source.
            int opStart = IndexAfterOperand(source, pos, operands[i - 1]);
            string op = ExtractOperatorToken(source, opStart, ["OrElse", "Or", "Xor"]);
            result = op switch
            {
                "OrElse" => Expression.OrElse(result, operands[i]),
                "Xor"    => Expression.ExclusiveOr(result, operands[i]),
                _        => Expression.Or(result, operands[i]),
            };
            pos = opStart;
        }

        return result;
    }

    // ── AndAlso / And ────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles <c>And</c> and <c>AndAlso</c> binary operators.
    /// </summary>
    private static Expression? CompileLogicalAnd(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        var operands = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (operands.Count == 0) return null;
        if (operands.Count == 1) return operands[0];

        string source = GetNodeSourceText(context, nav);
        Expression result = operands[0];
        int pos = 0;
        for (int i = 1; i < operands.Count; i++)
        {
            int opStart = IndexAfterOperand(source, pos, operands[i - 1]);
            string op = ExtractOperatorToken(source, opStart, ["AndAlso", "And"]);
            result = op == "AndAlso"
                ? Expression.AndAlso(result, operands[i])
                : Expression.And(result, operands[i]);
            pos = opStart;
        }

        return result;
    }

    // ── Not ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles the unary <c>Not</c> operator.
    /// </summary>
    private static Expression? CompileLogicalNot(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        Expression? inner = children.FirstOrDefault(static c => c is not null);
        if (inner is null) return null;

        string source = GetNodeSourceText(context, nav).TrimStart();
        if (!source.StartsWith("Not", StringComparison.OrdinalIgnoreCase))
            return inner;

        return inner.Type == typeof(bool)
            ? Expression.Not(inner)
            : Expression.Not(inner);
    }

    // ── Equality: = and <> ────────────────────────────────────────────────────

    /// <summary>
    /// Compiles <c>=</c> (equality) and <c>&lt;&gt;</c> (inequality) comparisons.
    /// </summary>
    private static Expression? CompileEquality(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
        => CompileBinaryChain(nav, context, children,
            op => op == "<>"
                ? (Expression left, Expression right) => Expression.NotEqual(left, right)
                : (Expression left, Expression right) => Expression.Equal(left, right),
            ["<>", "="]);

    // ── Relational: < <= > >= ─────────────────────────────────────────────────

    /// <summary>
    /// Compiles relational comparison operators.
    /// </summary>
    private static Expression? CompileRelational(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
        => CompileBinaryChain(nav, context, children,
            op => op switch
            {
                "<=" => (Expression l, Expression r) => Expression.LessThanOrEqual(l, r),
                ">=" => (Expression l, Expression r) => Expression.GreaterThanOrEqual(l, r),
                ">"  => (Expression l, Expression r) => Expression.GreaterThan(l, r),
                _    => (Expression l, Expression r) => Expression.LessThan(l, r),
            },
            ["<=", ">=", "<>", "<", ">"]);

    // ── String concatenation: & ───────────────────────────────────────────────

    /// <summary>
    /// Compiles the <c>&amp;</c> string concatenation operator.
    /// </summary>
    private static Expression? CompileConcat(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        var operands = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (operands.Count == 0) return null;
        if (operands.Count == 1) return operands[0];

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
        => CompileBinaryChain(nav, context, children,
            op => op == "<<"
                ? (Expression l, Expression r) => Expression.LeftShift(l, r)
                : (Expression l, Expression r) => Expression.RightShift(l, r),
            ["<<", ">>"]);

    // ── Addition / Subtraction ────────────────────────────────────────────────

    /// <summary>
    /// Compiles <c>+</c> and <c>-</c> additive operators.
    /// </summary>
    private static Expression? CompileAddSub(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
        => CompileBinaryChain(nav, context, children,
            op => op == "-"
                ? (Expression l, Expression r) => Expression.Subtract(l, ConvertNumeric(r, l.Type))
                : (Expression l, Expression r) => Expression.Add(l, ConvertNumeric(r, l.Type)),
            ["-", "+"]);

    // ── Multiplication / Division / Mod ───────────────────────────────────────

    /// <summary>
    /// Compiles <c>*</c>, <c>/</c>, and <c>Mod</c> operators.
    /// </summary>
    private static Expression? CompileMulDivMod(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
        => CompileBinaryChain(nav, context, children,
            op => op switch
            {
                "/"   => (Expression l, Expression r) => Expression.Divide(l, ConvertNumeric(r, l.Type)),
                "Mod" => (Expression l, Expression r) => Expression.Modulo(l, ConvertNumeric(r, l.Type)),
                _     => (Expression l, Expression r) => Expression.Multiply(l, ConvertNumeric(r, l.Type)),
            },
            ["Mod", "/", "*"]);

    // ── Unary negate / plus ───────────────────────────────────────────────────

    /// <summary>
    /// Compiles unary <c>-</c> and <c>+</c> operators.
    /// </summary>
    private static Expression? CompileNegate(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        Expression? inner = children.FirstOrDefault(static c => c is not null);
        if (inner is null) return null;
        string source = GetNodeSourceText(context, nav).TrimStart();
        if (source.StartsWith('-'))
            return Expression.Negate(inner);
        return inner;
    }

    // ── Power: ^ ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles the <c>^</c> exponentiation operator using <see cref="Math.Pow"/>.
    /// </summary>
    private static Expression? CompilePower(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children)
    {
        var operands = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (operands.Count == 0) return null;
        if (operands.Count == 1) return operands[0];

        // Right-associative: build from right
        Expression result = operands[^1];
        for (int i = operands.Count - 2; i >= 0; i--)
            result = Expression.Power(
                ConvertNumeric(operands[i], typeof(double)),
                ConvertNumeric(result, typeof(double)));
        return result;
    }

    // ── Generic binary chain helper ───────────────────────────────────────────

    /// <summary>
    /// Folds a left-associative binary operator chain using a factory selected by operator token.
    /// </summary>
    private static Expression? CompileBinaryChain(
        ParseTreeNavigator nav,
        CompilationContext context,
        IReadOnlyList<Expression?> children,
        Func<string, Func<Expression, Expression, Expression>> factory,
        IReadOnlyList<string> operators)
    {
        var operands = children.Where(static c => c is not null).Cast<Expression>().ToList();
        if (operands.Count == 0) return null;
        if (operands.Count == 1) return operands[0];

        string source = GetNodeSourceText(context, nav);
        Expression result = operands[0];
        int searchFrom = 0;
        for (int i = 1; i < operands.Count; i++)
        {
            string op = ExtractOperatorToken(source, searchFrom, operators);
            int opIdx = source.IndexOf(op, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (opIdx >= 0) searchFrom = opIdx + op.Length;
            result = factory(op)(result, operands[i]);
        }

        return result;
    }

    // ── Operator scanning utilities ───────────────────────────────────────────

    /// <summary>
    /// Returns the index in <paramref name="source"/> immediately after the first token
    /// that is a keyword or symbol from <paramref name="operators"/>.
    /// </summary>
    private static int IndexAfterOperand(string source, int from, Expression _) => from;

    /// <summary>
    /// Scans <paramref name="source"/> starting at <paramref name="from"/> and returns
    /// the first operator token found from <paramref name="candidates"/>.
    /// </summary>
    private static string ExtractOperatorToken(string source, int from, IReadOnlyList<string> candidates)
    {
        int best = int.MaxValue;
        string found = candidates[^1];
        foreach (string candidate in candidates)
        {
            int idx = source.IndexOf(candidate, from, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && idx < best)
            {
                best = idx;
                found = candidate;
            }
        }

        return found;
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
