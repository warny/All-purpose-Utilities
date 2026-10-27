using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Expressions.VBSyntax.Runtime;

namespace ExpressionResourceProbe;

/// <summary>One sized instance of a scenario: fixed free parameters and a factory building independent trees over them.</summary>
/// <param name="Parameters">The free parameters shared by every tree built by <paramref name="Build"/>.</param>
/// <param name="Build">Builds a fresh tree (for the parser families: compiles the source text again).</param>
/// <param name="Text">The source text for parser scenarios, otherwise <see langword="null"/>.</param>
/// <param name="Dialect">The parser front end of a parser scenario, otherwise <see langword="null"/>.</param>
internal sealed record ScenarioInstance(ParameterExpression[] Parameters, Func<Expression> Build, string? Text = null, ParserDialect? Dialect = null);

/// <summary>A public parser front end (CSyntax or VBSyntax): tokenizer, parser and compiler.</summary>
/// <param name="Name">The dialect name.</param>
/// <param name="Tokenize">Tokenizes a source and returns the token count.</param>
/// <param name="Parse">Parses a source into a parse tree (result discarded).</param>
/// <param name="Compile">Compiles a source with a symbol table into an expression tree.</param>
internal sealed record ParserDialect(
    string Name,
    Func<string, int> Tokenize,
    Action<string> Parse,
    Func<string, IReadOnlyDictionary<string, Expression>, Expression> Compile)
{
    /// <summary>The <c>Utils.Expressions.CSyntax</c> front end.</summary>
    public static readonly ParserDialect CSyntax = new(
        "CSyntax",
        text => new CSyntaxTokenParser().Tokenize(text).Count,
        text => new CSyntaxTokenParser().Parse(text),
        (text, symbols) => new CSyntaxExpressionCompiler().CompileExpression(text, symbols));

    /// <summary>The <c>Utils.Expressions.VBSyntax</c> front end.</summary>
    public static readonly ParserDialect VBSyntax = new(
        "VBSyntax",
        text => new VBSyntaxTokenParser().Tokenize(text).Count,
        text => new VBSyntaxTokenParser().Parse(text),
        (text, symbols) => new VBSyntaxExpressionCompiler().CompileExpression(text, symbols));

    /// <summary>Creates a parser scenario instance whose build compiles <paramref name="text"/> over <paramref name="parameters"/>.</summary>
    /// <param name="parameters">The free parameters, resolved by name.</param>
    /// <param name="text">The source text.</param>
    /// <returns>The instance.</returns>
    public ScenarioInstance Instance(ParameterExpression[] parameters, string text)
    {
        IReadOnlyDictionary<string, Expression> symbols = parameters.ToDictionary(parameter => parameter.Name!, parameter => (Expression)parameter);
        return new ScenarioInstance(parameters, () => Compile(text, symbols), text, this);
    }
}

/// <summary>A named family of expression trees parameterized by a size.</summary>
/// <param name="Name">The stable scenario name used by the campaign script and the roadmap.</param>
/// <param name="Family">The roadmap family letter (A to F, or G for the original R0 chains).</param>
/// <param name="Description">What the size controls.</param>
/// <param name="Create">Creates the instance of a given size.</param>
internal sealed record Scenario(string Name, string Family, string Description, Func<int, ScenarioInstance> Create);

/// <summary>The scenario catalog of the ETC R0 resource campaign (roadmap §7).</summary>
internal static class Scenarios
{
    /// <summary>The <see cref="Math.Abs(double)"/> method.</summary>
    private static readonly MethodInfo AbsMethod = typeof(Math).GetMethod(nameof(Math.Abs), [typeof(double)])!;

    /// <summary>The custom multiplication operator of family F-custom (S5 family <c>F</c>).</summary>
    private static readonly MethodInfo CustomMultiplyMethod = typeof(Scenarios).GetMethod(nameof(CustomMultiply), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>The identity call of family F-opaque (S5 family <c>z</c>).</summary>
    private static readonly MethodInfo IdentityMethod = typeof(Scenarios).GetMethod(nameof(Identity), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Gets every scenario.</summary>
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("A-left-sum", "A", "x0 + x1 + ... (left-associated, n distinct parameters)", n => Sum(n, Fold.Left)),
        new("B-right-sum", "B", "x0 + (x1 + (...)) (right-associated, n distinct parameters)", n => Sum(n, Fold.Right)),
        new("C-balanced-sum", "C", "balanced binary sum of n distinct parameters (depth log2 n)", n => Sum(n, Fold.Balanced)),
        new("D-poly-pow", "D", "sum of (k+2) * Pow(x, k) for k < n (expanded, powers)", PolynomialWithPowers),
        new("D-poly-mul", "D", "sum of (k+2) * x*x*...*x (k unshared factors) for k < n (expanded, O(n^2) nodes)", n => PolynomialWithProducts(n, shared: false)),
        new("D-poly-mul-shared", "D", "as D-poly-mul, each x^k reusing the x^(k-1) subtree (DAG: O(n) distinct, O(n^2) logical nodes)", n => PolynomialWithProducts(n, shared: true)),
        new("D-horner", "D", "2 + x*(3 + x*(4 + ...)) (Horner form, depth 2n)", Horner),
        new("D-repeated", "D", "x + x + ... + x (n occurrences of one parameter, left-associated)", RepeatedTerm),
        new("E-csyntax-sum", "E", "CSyntax source 'x0 + x1 + ... + x(n-1)'", n => ParsedSum(ParserDialect.CSyntax, n, nested: false)),
        new("E-csyntax-nested", "E", "CSyntax source 'x0 + (x1 + (... + x(n-1)))' (parenthesized nesting)", n => ParsedSum(ParserDialect.CSyntax, n, nested: true)),
        new("E-csyntax-parens", "E", "CSyntax source '((...(x0)...))' (n pairs of parentheses, one parameter)", n => Parentheses(ParserDialect.CSyntax, n)),
        new("E-vbsyntax-sum", "E", "VBSyntax source 'x0 + x1 + ... + x(n-1)'", n => ParsedSum(ParserDialect.VBSyntax, n, nested: false)),
        new("E-vbsyntax-nested", "E", "VBSyntax source 'x0 + (x1 + (... + x(n-1)))' (parenthesized nesting)", n => ParsedSum(ParserDialect.VBSyntax, n, nested: true)),
        new("E-vbsyntax-parens", "E", "VBSyntax source '((...(x0)...))' (n pairs of parentheses, one parameter)", n => Parentheses(ParserDialect.VBSyntax, n)),
        new("F-p", "F", "S5 family p: 100*x0 - x1*3 - 4*x2 - ... (n terms)", n => CoefficientChain(n, equalCoefficients: false, custom: false, opaqueHead: false)),
        new("F-g", "F", "S5 family g: equal coefficients 3*x0 - x1*3 - 3*x2 - ... (n terms)", n => CoefficientChain(n, equalCoefficients: true, custom: false, opaqueHead: false)),
        new("F-custom", "F", "S5 family F: family p with a custom multiplication operator (n terms)", n => CoefficientChain(n, equalCoefficients: false, custom: true, opaqueHead: false)),
        new("F-opaque", "F", "S5 family z: 100*Identity(opaque) - x1*3 - 4*x2 - ... (n terms)", n => CoefficientChain(n, equalCoefficients: false, custom: false, opaqueHead: true)),
        new("G-negate", "G", "-(-(...(p))) (n unary nodes, R0 measurement)", n => Chain(n, e => Expression.Negate(e))),
        new("G-conditional", "G", "nested conditionals in the true branch (R0 measurement)", ConditionalChain),
        new("G-call", "G", "Math.Abs(Math.Abs(...)) (R0 measurement)", n => Chain(n, e => Expression.Call(AbsMethod, e))),
        new("G-invoke", "G", "Invoke of nested parameterless lambdas (R0 measurement)", n => Chain(n, e => Expression.Invoke(Expression.Lambda<Func<double>>(e)))),
    ];

    /// <summary>Gets a scenario by name.</summary>
    /// <param name="name">The scenario name.</param>
    /// <returns>The scenario.</returns>
    public static Scenario Get(string name) => All.FirstOrDefault(scenario => scenario.Name == name)
        ?? throw new ArgumentException($"Unknown scenario '{name}'.", nameof(name));

    /// <summary>The association of a sum.</summary>
    private enum Fold
    {
        /// <summary>((x0 + x1) + x2) + ...</summary>
        Left,

        /// <summary>x0 + (x1 + (x2 + ...)).</summary>
        Right,

        /// <summary>Recursive halves.</summary>
        Balanced,
    }

    /// <summary>Creates <paramref name="count"/> distinct double parameters.</summary>
    /// <param name="count">The number of parameters.</param>
    /// <returns>The parameters x0, x1, ...</returns>
    private static ParameterExpression[] Parameters(int count)
        => Enumerable.Range(0, count).Select(i => Expression.Parameter(typeof(double), "x" + i)).ToArray();

    /// <summary>Creates the sum families A, B and C.</summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="fold">The association.</param>
    /// <returns>The instance.</returns>
    private static ScenarioInstance Sum(int n, Fold fold)
    {
        ParameterExpression[] x = Parameters(n);
        return new ScenarioInstance(x, () => fold switch
        {
            Fold.Left => x.Skip(1).Aggregate((Expression)x[0], Expression.Add),
            Fold.Right => x.Take(n - 1).Reverse().Aggregate((Expression)x[n - 1], (acc, term) => Expression.Add(term, acc)),
            _ => Balanced(x, 0, n),
        }, null);
    }

    /// <summary>Builds a balanced sum of <c>x[start..start+count)</c> iteratively (no recursion over the size).</summary>
    /// <param name="x">The parameters.</param>
    /// <param name="start">The first index.</param>
    /// <param name="count">The number of terms.</param>
    /// <returns>The balanced sum.</returns>
    private static Expression Balanced(ParameterExpression[] x, int start, int count)
    {
        // Pairwise reduction level by level: same shape as recursive halving, with a depth of ceil(log2 count).
        List<Expression> level = x.Skip(start).Take(count).Cast<Expression>().ToList();
        while (level.Count > 1)
        {
            var next = new List<Expression>((level.Count + 1) / 2);
            for (int i = 0; i + 1 < level.Count; i += 2) next.Add(Expression.Add(level[i], level[i + 1]));
            if (level.Count % 2 == 1) next.Add(level[^1]);
            level = next;
        }

        return level[0];
    }

    /// <summary>Creates family D-poly-pow.</summary>
    /// <param name="n">The number of monomials.</param>
    /// <returns>The instance.</returns>
    private static ScenarioInstance PolynomialWithPowers(int n)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        return new ScenarioInstance([x], () =>
        {
            Expression acc = Expression.Constant(2.0);
            for (int k = 1; k < n; k++)
            {
                acc = Expression.Add(acc, Expression.Multiply(Expression.Constant(k + 2.0), Expression.Power(x, Expression.Constant((double)k))));
            }

            return acc;
        }, null);
    }

    /// <summary>Creates families D-poly-mul and D-poly-mul-shared.</summary>
    /// <param name="n">The number of monomials.</param>
    /// <param name="shared">Whether x^k reuses the x^(k-1) subtree.</param>
    /// <returns>The instance.</returns>
    private static ScenarioInstance PolynomialWithProducts(int n, bool shared)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        return new ScenarioInstance([x], () =>
        {
            Expression acc = Expression.Constant(2.0);
            Expression power = x;
            for (int k = 1; k < n; k++)
            {
                if (!shared && k > 1)
                {
                    power = x;
                    for (int i = 1; i < k; i++) power = Expression.Multiply(power, x);
                }
                else if (k > 1)
                {
                    power = Expression.Multiply(power, x);
                }

                acc = Expression.Add(acc, Expression.Multiply(Expression.Constant(k + 2.0), power));
            }

            return acc;
        }, null);
    }

    /// <summary>Creates family D-horner.</summary>
    /// <param name="n">The number of coefficients.</param>
    /// <returns>The instance.</returns>
    private static ScenarioInstance Horner(int n)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        return new ScenarioInstance([x], () =>
        {
            Expression acc = Expression.Constant(n + 1.0);
            for (int k = n - 2; k >= 0; k--) acc = Expression.Add(Expression.Constant(k + 2.0), Expression.Multiply(x, acc));
            return acc;
        }, null);
    }

    /// <summary>Creates family D-repeated.</summary>
    /// <param name="n">The number of occurrences.</param>
    /// <returns>The instance.</returns>
    private static ScenarioInstance RepeatedTerm(int n)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        return new ScenarioInstance([x], () =>
        {
            Expression acc = x;
            for (int i = 1; i < n; i++) acc = Expression.Add(acc, x);
            return acc;
        }, null);
    }

    /// <summary>Creates the parser sum families: the build compiles the source text through the dialect's public compiler.</summary>
    /// <param name="dialect">The public parser front end.</param>
    /// <param name="n">The number of terms.</param>
    /// <param name="nested">Whether the source nests parentheses to the right.</param>
    /// <returns>The instance.</returns>
    private static ScenarioInstance ParsedSum(ParserDialect dialect, int n, bool nested)
    {
        ParameterExpression[] x = Parameters(n);
        var builder = new StringBuilder();
        for (int i = 0; i < n; i++)
        {
            if (i > 0) builder.Append(nested ? " + (" : " + ");
            builder.Append('x').Append(i);
        }

        if (nested) builder.Append(')', Math.Max(0, n - 1));
        return dialect.Instance(x, builder.ToString());
    }

    /// <summary>Creates the parenthesis-nesting families.</summary>
    /// <param name="dialect">The public parser front end.</param>
    /// <param name="n">The number of parenthesis pairs.</param>
    /// <returns>The instance.</returns>
    private static ScenarioInstance Parentheses(ParserDialect dialect, int n)
        => dialect.Instance([Expression.Parameter(typeof(double), "x0")], new string('(', n) + "x0" + new string(')', n));

    /// <summary>Creates the S5 coefficient chains (families p, g, F and z).</summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="equalCoefficients">Use the coefficient 3 everywhere (family g).</param>
    /// <param name="custom">Use the custom multiplication operator (family F).</param>
    /// <param name="opaqueHead">Use <c>100 * Identity(opaque)</c> as the head term (family z).</param>
    /// <returns>The instance.</returns>
    private static ScenarioInstance CoefficientChain(int n, bool equalCoefficients, bool custom, bool opaqueHead)
    {
        ParameterExpression[] x = Parameters(n);
        object opaque = new OpaqueValue();
        Expression Multiply(Expression left, Expression right) => custom ? Expression.Multiply(left, right, CustomMultiplyMethod) : Expression.Multiply(left, right);
        Expression Coefficient(int i) => Expression.Constant(equalCoefficients ? 3.0 : i + 2.0);

        return new ScenarioInstance(x, () =>
        {
            Expression head = opaqueHead
                ? Expression.Call(IdentityMethod, Expression.Constant(opaque, typeof(object)))
                : x[0];
            Expression acc = Multiply(Expression.Constant(equalCoefficients ? 3.0 : 100.0), head);
            for (int i = 1; i < n; i++)
            {
                acc = Expression.Subtract(acc, i % 2 == 0 ? Multiply(Coefficient(i), x[i]) : Multiply(x[i], Coefficient(i)));
            }

            return acc;
        }, null);
    }

    /// <summary>Creates a unary-like chain of <paramref name="n"/> wrappers around one parameter.</summary>
    /// <param name="n">The number of wrappers.</param>
    /// <param name="wrap">The wrapper factory.</param>
    /// <returns>The instance.</returns>
    private static ScenarioInstance Chain(int n, Func<Expression, Expression> wrap)
    {
        ParameterExpression p = Expression.Parameter(typeof(double), "p");
        return new ScenarioInstance([p], () =>
        {
            Expression e = p;
            for (int i = 0; i < n; i++) e = wrap(e);
            return e;
        }, null);
    }

    /// <summary>Creates the nested-conditional chain.</summary>
    /// <param name="n">The nesting depth.</param>
    /// <returns>The instance.</returns>
    private static ScenarioInstance ConditionalChain(int n)
    {
        ParameterExpression p = Expression.Parameter(typeof(double), "p");
        return new ScenarioInstance([p], () =>
        {
            Expression e = p;
            for (int i = 0; i < n; i++) e = Expression.Condition(Expression.GreaterThan(p, Expression.Constant(0.5)), e, Expression.Constant(2.5));
            return e;
        }, null);
    }

    /// <summary>A custom multiplication operator (keeps S5 family F outside the ordinary arithmetic class).</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The product.</returns>
    private static double CustomMultiply(double left, double right) => left * right;

    /// <summary>Returns 1 for any value (the opaque head of S5 family z).</summary>
    /// <param name="value">The opaque value.</param>
    /// <returns>Always 1.</returns>
    private static double Identity(object value) => 1.0;

    /// <summary>An opaque constant value whose equality is user code.</summary>
    private sealed class OpaqueValue
    {
        /// <inheritdoc/>
        public override bool Equals(object? obj) => ReferenceEquals(this, obj);

        /// <inheritdoc/>
        public override int GetHashCode() => 17;
    }
}
