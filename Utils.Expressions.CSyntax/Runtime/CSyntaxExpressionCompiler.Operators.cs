using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Parser.Runtime;

namespace Utils.Expressions.CSyntax.Runtime;

/// <summary>
/// Compiles C-like parse trees into LINQ expression trees by using
/// <see cref="ParseTreeCompiler{TContext, TResult}"/>.
/// </summary>
public sealed partial class CSyntaxExpressionCompiler
{
    /// <summary>
    /// Compiles a logical OR chain.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled logical OR expression.</returns>
    private static Expression? CompileLogicalOr(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
        => FoldBinary(nav, children, ["||"], static (op, left, right) => Expression.OrElse(EnsureBoolean(left), EnsureBoolean(right)));

    /// <summary>
    /// Compiles a logical AND chain.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled logical AND expression.</returns>
    private static Expression? CompileLogicalAnd(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
        => FoldBinary(nav, children, ["&&"], static (op, left, right) => Expression.AndAlso(EnsureBoolean(left), EnsureBoolean(right)));

    /// <summary>
    /// Compiles equality/inequality operators.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled equality expression.</returns>
    private static Expression? CompileEquality(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        return FoldBinary(nav, children, (op, left, right) =>
        {
            (Expression normalizedLeft, Expression normalizedRight) = NormalizeForComparison(left, right);
            return op switch
            {
                "==" => Expression.Equal(normalizedLeft, normalizedRight),
                "!=" => Expression.NotEqual(normalizedLeft, normalizedRight),
                _ => throw new NotSupportedException($"Unsupported equality operator '{op}'."),
            };
        }, ["==", "!="]);
    }

    /// <summary>
    /// Compiles relational operators.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled relational expression.</returns>
    private static Expression? CompileRelational(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        return FoldBinary(nav, children, (op, left, right) =>
        {
            (Expression l, Expression r) = NormalizeNumericPair(left, right);
            return op switch
            {
                "<" => Expression.LessThan(l, r),
                "<=" => Expression.LessThanOrEqual(l, r),
                ">" => Expression.GreaterThan(l, r),
                ">=" => Expression.GreaterThanOrEqual(l, r),
                _ => throw new NotSupportedException($"Unsupported relational operator '{op}'."),
            };
        }, ["<", "<=", ">", ">="]);
    }

    /// <summary>
    /// Compiles bit-shift operators.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled shift expression.</returns>
    private static Expression? CompileShift(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        return FoldBinary(nav, children, (op, left, right) =>
        {
            Expression l = EnsureInteger(left);
            Expression r = EnsureInteger(right);
            return op switch
            {
                "<<" => Expression.LeftShift(l, r),
                ">>" => Expression.RightShift(l, r),
                _ => throw new NotSupportedException($"Unsupported shift operator '{op}'."),
            };
        }, ["<<", ">>"]);
    }

    /// <summary>
    /// Compiles addition/subtraction operators.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled additive expression.</returns>
    private static Expression? CompileAddSub(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        return FoldBinary(nav, children, (op, left, right) =>
        {
            if (op == "+" && (left.Type == typeof(string) || right.Type == typeof(string)))
            {
                Expression l = left.Type == typeof(string) ? left
                    : Expression.Call(left, left.Type.GetMethod(nameof(ToString), Type.EmptyTypes)!);
                Expression r = right.Type == typeof(string) ? right
                    : Expression.Call(right, right.Type.GetMethod(nameof(ToString), Type.EmptyTypes)!);
                return Expression.Call(
                    typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string)])!,
                    l, r);
            }

            (Expression ln, Expression rn) = NormalizeNumericPair(left, right);
            return op switch
            {
                "+" => Expression.Add(ln, rn),
                "-" => Expression.Subtract(ln, rn),
                _ => throw new NotSupportedException($"Unsupported additive operator '{op}'."),
            };
        }, ["+", "-"]);
    }

    /// <summary>
    /// Compiles multiplication/division/modulo operators.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled multiplicative expression.</returns>
    private static Expression? CompileMulDivMod(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        return FoldBinary(nav, children, (op, left, right) =>
        {
            (Expression l, Expression r) = NormalizeNumericPair(left, right);
            return op switch
            {
                "*" => Expression.Multiply(l, r),
                "/" => Expression.Divide(l, r),
                "%" => Expression.Modulo(l, r),
                _ => throw new NotSupportedException($"Unsupported multiplicative operator '{op}'."),
            };
        }, ["*", "/", "%"]);
    }

    /// <summary>
    /// Compiles exponentiation operators.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled power expression.</returns>
    private static Expression? CompilePower(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        return FoldBinary(nav, children, (op, left, right) =>
        {
            if (op != "**")
            {
                throw new NotSupportedException($"Unsupported power operator '{op}'.");
            }

            return Expression.Call(
                typeof(Math).GetMethod(nameof(Math.Pow), [typeof(double), typeof(double)])!,
                EnsureNumeric(left),
                EnsureNumeric(right));
        }, ["**"]);
    }

    /// <summary>
    /// Compiles unary operators.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled unary expression.</returns>
    private static Expression? CompileUnary(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        if (nav.RawChildren is null || nav.RawChildren.Count == 0)
        {
            return null;
        }

        HashSet<string> unaryOperators = new HashSet<string>(StringComparer.Ordinal) { "+", "-", "!", "~" };
        string? op = null;
        int operatorChildIndex = -1;
        for (int i = 0; i < nav.RawChildren.Count; i++)
        {
            if (nav.RawChildren[i] is LexerNode lexerNode && unaryOperators.Contains(lexerNode.Token.Text))
            {
                op = lexerNode.Token.Text;
                operatorChildIndex = i;
                break;
            }

            if (i == 0 && nav.RawChildren.Count > 1 && nav.RawChildren[i] is ParserNode parserNode)
            {
                string? nestedOperator = CollectOperatorTokens(parserNode.Children, unaryOperators).FirstOrDefault();
                if (nestedOperator is not null)
                {
                    op = nestedOperator;
                    operatorChildIndex = i;
                    break;
                }
            }
        }

        Expression? operandCandidate = op is null
            ? children.FirstOrDefault(static child => child is not null)
            : children.Skip(operatorChildIndex + 1).FirstOrDefault(static child => child is not null)
                ?? children.FirstOrDefault(static child => child is not null);
        if (operandCandidate is null)
        {
            return null;
        }

        if (op is null)
        {
            return operandCandidate;
        }

        Expression operand = RequireExpression(operandCandidate, "unary operand");
        return op switch
        {
            "+" => IsNumericType(operand.Type) ? operand : throw new NotSupportedException($"Expression '{operand}' is not numeric."),
            "-" => Expression.Negate(IsNumericType(operand.Type) ? operand : throw new NotSupportedException($"Expression '{operand}' is not numeric.")),
            "!" => Expression.Not(EnsureBoolean(operand)),
            "~" => Expression.OnesComplement(EnsureInteger(operand)),
            _ => throw new NotSupportedException($"Unsupported unary operator '{op}'."),
        };
    }

    /// <summary>
    /// Compiles assignment instructions.
    /// </summary>
    /// <param name="nav">Current parser navigator.</param>
    /// <param name="context">Current compilation context.</param>
    /// <param name="children">Compiled child expressions.</param>
    /// <returns>Compiled assignment expression.</returns>
    private static Expression? CompileAssignment(ParseTreeNavigator nav, CompilationContext context, IReadOnlyList<Expression?> children)
    {
        if (nav.RawChildren is null || nav.RawChildren.Count < 3)
        {
            return children.FirstOrDefault(static child => child is not null);
        }

        int assignmentIndex = nav.RawChildren
            .Select((node, index) => (node, index))
            .FirstOrDefault(tuple => tuple.node is LexerNode lexer && lexer.Token.Text == "=")
            .index;
        if (assignmentIndex <= 0)
        {
            return children.FirstOrDefault(static child => child is not null);
        }

        Expression left = RequireExpression(
            FindCompiledChildNear(nav.RawChildren, children, assignmentIndex - 1, -1),
            "assignment target");
        Expression right = RequireExpression(
            FindCompiledChildNear(nav.RawChildren, children, assignmentIndex + 1, +1),
            "assignment value");
        Expression convertedRight = ConvertIfNeeded(right, left.Type);
        return Expression.Assign(left, convertedRight);
    }

}
