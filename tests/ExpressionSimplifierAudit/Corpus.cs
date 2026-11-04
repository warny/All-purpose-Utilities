using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ExpressionSimplifierAudit;

/// <summary>
/// Parses the small arithmetic template language of the audit (<c>a*x + (1-a)*x</c>, <c>(x^a)^b</c>, <c>sin(x)^2 + cos(x)^2</c>):
/// <c>+ - * / ^</c>, unary minus, parentheses, numbers, the functions <c>sin</c>, <c>cos</c>, <c>tan</c>, <c>log</c>, <c>log10</c>,
/// <c>sqrt</c> (the <see cref="double"/> methods the simplifier recognizes) and one-letter placeholders. Every occurrence of
/// a placeholder is bound to the same <see cref="Expression"/> instance, so a template repeating a letter builds a shared node.
/// </summary>
internal sealed class Template
{
    /// <summary>The template text.</summary>
    private readonly string _text;

    /// <summary>The current position of the parser.</summary>
    private int _position;

    /// <summary>The placeholder bindings of the current instantiation.</summary>
    private IReadOnlyDictionary<char, Expression> _bindings = new Dictionary<char, Expression>();

    /// <summary>Creates a template.</summary>
    /// <param name="text">The template text.</param>
    public Template(string text)
    {
        _text = text;
        Placeholders = ScanPlaceholders(text);
    }

    /// <summary>Gets the template text.</summary>
    public string Text => _text;

    /// <summary>Gets the distinct placeholders, in alphabetical order.</summary>
    public char[] Placeholders { get; }

    /// <summary>Builds the tree of the template with the given bindings.</summary>
    /// <param name="bindings">The expression bound to each placeholder.</param>
    /// <returns>The tree.</returns>
    public Expression Build(IReadOnlyDictionary<char, Expression> bindings)
    {
        _bindings = bindings;
        _position = 0;
        Expression result = ParseSum();
        SkipSpaces();
        if (_position != _text.Length) throw new FormatException("Unexpected character at " + _position + " in " + _text);
        return result;
    }

    /// <summary>The <see cref="double"/> functions of the template language.</summary>
    private static readonly Dictionary<string, MethodInfo> Functions = new()
    {
        ["sin"] = typeof(double).GetMethod(nameof(double.Sin), [typeof(double)])!,
        ["cos"] = typeof(double).GetMethod(nameof(double.Cos), [typeof(double)])!,
        ["tan"] = typeof(double).GetMethod(nameof(double.Tan), [typeof(double)])!,
        ["log"] = typeof(double).GetMethod(nameof(double.Log), [typeof(double)])!,
        ["log10"] = typeof(double).GetMethod(nameof(double.Log10), [typeof(double)])!,
        ["sqrt"] = typeof(double).GetMethod(nameof(double.Sqrt), [typeof(double)])!,
    };

    /// <summary>Finds the placeholders of a template (letters that are not part of a function name).</summary>
    /// <param name="text">The template text.</param>
    /// <returns>The distinct placeholders.</returns>
    private static char[] ScanPlaceholders(string text)
    {
        var result = new SortedSet<char>();
        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsLetter(text[i])) continue;
            int start = i;
            while (i < text.Length && char.IsLetterOrDigit(text[i])) i++;
            string word = text[start..i];
            if (!Functions.ContainsKey(word))
            {
                foreach (char c in word) if (char.IsLetter(c)) result.Add(c);
            }

            i--;
        }

        return result.ToArray();
    }

    /// <summary>Parses a sum or difference (left associative).</summary>
    /// <returns>The tree.</returns>
    private Expression ParseSum()
    {
        Expression left = ParseProduct();
        while (true)
        {
            SkipSpaces();
            if (Accept('+')) left = Expression.Add(left, ParseProduct());
            else if (Accept('-')) left = Expression.Subtract(left, ParseProduct());
            else return left;
        }
    }

    /// <summary>Parses a product or quotient (left associative).</summary>
    /// <returns>The tree.</returns>
    private Expression ParseProduct()
    {
        Expression left = ParseUnary();
        while (true)
        {
            SkipSpaces();
            if (Accept('*')) left = Expression.Multiply(left, ParseUnary());
            else if (Accept('/')) left = Expression.Divide(left, ParseUnary());
            else return left;
        }
    }

    /// <summary>Parses a unary minus (applied to a power, as in mathematics: <c>-x^2</c> is <c>-(x^2)</c>).</summary>
    /// <returns>The tree.</returns>
    private Expression ParseUnary()
    {
        SkipSpaces();
        if (Accept('-'))
        {
            SkipSpaces();
            if (_position < _text.Length && char.IsDigit(_text[_position])) return ParsePower(negativeNumber: true);
            return Expression.Negate(ParseUnary());
        }

        return ParsePower(negativeNumber: false);
    }

    /// <summary>Parses a power (right associative).</summary>
    /// <param name="negativeNumber">Whether a minus sign directly precedes a number literal (folded into it).</param>
    /// <returns>The tree.</returns>
    private Expression ParsePower(bool negativeNumber)
    {
        Expression basis = ParseAtom(negativeNumber);
        SkipSpaces();
        if (Accept('^')) return Expression.Power(basis, ParseUnary());
        return basis;
    }

    /// <summary>Parses a number, a placeholder, a function call or a parenthesized sum.</summary>
    /// <param name="negativeNumber">Whether the number literal is negative.</param>
    /// <returns>The tree.</returns>
    private Expression ParseAtom(bool negativeNumber)
    {
        SkipSpaces();
        if (Accept('('))
        {
            Expression inner = ParseSum();
            SkipSpaces();
            if (!Accept(')')) throw new FormatException("Missing ) in " + _text);
            return inner;
        }

        char c = _text[_position];
        if (char.IsDigit(c))
        {
            int start = _position;
            while (_position < _text.Length && (char.IsDigit(_text[_position]) || _text[_position] is '.' or 'e' or 'E')) _position++;
            double value = double.Parse(_text[start.._position], CultureInfo.InvariantCulture);
            return Expression.Constant(negativeNumber ? -value : value);
        }

        int wordStart = _position;
        while (_position < _text.Length && char.IsLetterOrDigit(_text[_position])) _position++;
        string word = _text[wordStart.._position];
        if (Functions.TryGetValue(word, out MethodInfo? function))
        {
            SkipSpaces();
            if (!Accept('(')) throw new FormatException("Missing ( after " + word);
            Expression argument = ParseSum();
            SkipSpaces();
            if (!Accept(')')) throw new FormatException("Missing ) in " + _text);
            return Expression.Call(function, argument);
        }

        if (word.Length != 1) throw new FormatException("Unknown word " + word + " in " + _text);
        return _bindings[word[0]];
    }

    /// <summary>Consumes <paramref name="c"/> when it is the next character.</summary>
    /// <param name="c">The expected character.</param>
    /// <returns><see langword="true"/> when consumed.</returns>
    private bool Accept(char c)
    {
        if (_position < _text.Length && _text[_position] == c)
        {
            _position++;
            return true;
        }

        return false;
    }

    /// <summary>Skips spaces.</summary>
    private void SkipSpaces()
    {
        while (_position < _text.Length && _text[_position] == ' ') _position++;
    }
}

/// <summary>The structural corpus of the termination and convergence campaigns (brief §4 and §5).</summary>
internal static class Corpus
{
    /// <summary>The <see cref="double"/> parameters of the corpus.</summary>
    public static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>The second parameter.</summary>
    public static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");

    /// <summary>The third parameter.</summary>
    public static readonly ParameterExpression Z = Expression.Parameter(typeof(double), "z");

    /// <summary>The structures of the brief (§4 corpus, §5 priority families, §10 divisions and powers, §8 identities).</summary>
    public static readonly string[] Templates =
    [
        "a + b", "a - b", "a * b", "a / b",
        "a - (b - c)", "(a - b) - c", "a + (b - c)", "(a + b) - c",
        "a * (b + c)", "a * (b - c)", "(a * b) * c", "a * (b * c)",
        "a / (b / c)", "(a / b) / c", "a * (b / c)", "(a / b) * c",
        "a*x + b*x", "a*x - b*x", "a*x + (1-a)*x",
        "x - y - z", "x - (y + z)", "x - (y - z)",
        "x^2 * x^3", "(x^a)^b", "x / x", "x - x",
        "(a / b) / (c / d)", "a / (1 / b)", "(a / 1) / b", "a / (b / 1)", "(a * b) / c",
        "a ^ -1", "a ^ -2", "(a ^ b) ^ c",
        "y - 1", "y - 2", "10*y - 10", "x / (1/x)", "(1-y) / (1/y)",
        "c*(a - b)", "((a - b) - c) - d", "a - (b - (c - d))",
        "x * 0", "0 / x", "x / -1", "x ^ 0", "0 ^ x",
        "log(x) + log(y)", "log(x) - log(y)", "sin(x)^2 + cos(x)^2", "sin(x) / cos(x)",
        "a^b + c^d", "x^y + x^2",
    ];

    /// <summary>The leaf pool of the corpus: the three parameters and the constants of the brief.</summary>
    public static Expression[] Leaves() =>
    [
        X, Y, Z,
        Expression.Constant(0.0), Expression.Constant(1.0), Expression.Constant(-1.0), Expression.Constant(2.0),
        Expression.Constant(-2.0), Expression.Constant(0.5), Expression.Constant(1e300),
    ];

    /// <summary>
    /// Enumerates every instantiation of every template over the leaf pool (each placeholder bound to every leaf), then a
    /// seeded sample of second-level compositions (a placeholder bound to an instantiated template).
    /// </summary>
    /// <param name="nestedSamples">The number of second-level compositions.</param>
    /// <param name="seed">The seed of the sample.</param>
    /// <returns>The labelled cases.</returns>
    public static IEnumerable<(string Label, Expression Tree)> Instantiate(int nestedSamples, int seed)
    {
        Expression[] leaves = Leaves();
        var level1 = new List<(string, Expression)>();
        foreach (string text in Templates)
        {
            var template = new Template(text);
            foreach (int[] assignment in Assignments(template.Placeholders.Length, leaves.Length))
            {
                var bindings = new Dictionary<char, Expression>();
                for (int i = 0; i < assignment.Length; i++) bindings[template.Placeholders[i]] = leaves[assignment[i]];
                Expression tree = template.Build(bindings);
                string label = text + " | " + string.Join(",", template.Placeholders.Select((p, i) => p + "=" + LeafName(leaves[assignment[i]])));
                level1.Add((label, tree));
                yield return (label, tree);
            }
        }

        var random = new Random(seed);
        for (int s = 0; s < nestedSamples; s++)
        {
            var template = new Template(Templates[random.Next(Templates.Length)]);
            var bindings = new Dictionary<char, Expression>();
            var names = new List<string>();
            foreach (char p in template.Placeholders)
            {
                if (random.Next(3) == 0)
                {
                    Expression leaf = leaves[random.Next(leaves.Length)];
                    bindings[p] = leaf;
                    names.Add(p + "=" + LeafName(leaf));
                }
                else
                {
                    (string label, Expression tree) = level1[random.Next(level1.Count)];
                    bindings[p] = tree;
                    names.Add(p + "=[" + label + "]");
                }
            }

            yield return ("nested#" + s + " " + template.Text + " | " + string.Join(",", names), template.Build(bindings));
        }
    }

    /// <summary>Returns the short name of a leaf.</summary>
    /// <param name="leaf">The leaf.</param>
    /// <returns>The name.</returns>
    public static string LeafName(Expression leaf) => leaf switch
    {
        ParameterExpression p => p.Name!,
        ConstantExpression c => Convert.ToString(c.Value, CultureInfo.InvariantCulture)!,
        _ => AuditText.Format(leaf),
    };

    /// <summary>Enumerates every assignment of <paramref name="slots"/> slots to <paramref name="choices"/> values.</summary>
    /// <param name="slots">The number of slots.</param>
    /// <param name="choices">The number of values.</param>
    /// <returns>The assignments.</returns>
    private static IEnumerable<int[]> Assignments(int slots, int choices)
    {
        var current = new int[slots];
        while (true)
        {
            yield return (int[])current.Clone();
            int i = slots - 1;
            while (i >= 0 && ++current[i] == choices)
            {
                current[i] = 0;
                i--;
            }

            if (i < 0) yield break;
        }
    }
}

/// <summary>An opaque constant value whose equality is reference equality and whose calls are counted.</summary>
public sealed class CountingOpaque
{
    /// <summary>The number of <see cref="Equals(object?)"/> and <see cref="GetHashCode"/> calls on every instance.</summary>
    public static long Calls;

    /// <summary>The identifier of the instance.</summary>
    public int Id { get; }

    /// <summary>Creates an instance.</summary>
    /// <param name="id">The identifier.</param>
    public CountingOpaque(int id) => Id = id;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        Calls++;
        return ReferenceEquals(this, obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        Calls++;
        return 17;
    }
}

/// <summary>The complexity families of the S5 history (P10, P14, P15, P16), rebuilt from the characterization tests.</summary>
internal static class Families
{
    /// <summary>The inert carrier of an opaque value: <c>Math.Truncate((value as double?) ?? 1.0)</c> (same as <c>SafeOpaqueTerm</c>).</summary>
    private static readonly MethodInfo Truncate = typeof(Math).GetMethod(nameof(Math.Truncate), [typeof(double)])!;

    /// <summary>The custom multiplication operator of family <c>F</c>.</summary>
    private static readonly MethodInfo CustomMultiplyMethod = typeof(Families).GetMethod(nameof(CustomMultiply))!;

    /// <summary>The <see cref="double.Sin(double)"/> method of family <c>n</c>.</summary>
    private static readonly MethodInfo Sin = typeof(double).GetMethod(nameof(double.Sin), [typeof(double)])!;

    /// <summary>A custom multiplication (ordinary product, but a user-defined operator method).</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The product.</returns>
    public static double CustomMultiply(double left, double right) => left * right;

    /// <summary>The names, provenance and shapes of the families.</summary>
    public static readonly (string Name, string Shape)[] Catalog =
    [
        ("p", "100*x0 - x1*3 - 4*x2 - x3*5 - ... (P16 TailSortedChain: first term sorts last)"),
        ("B", "2*x0 - x1*3 - 4*x2 - ... (P10 family B: first term sorts first)"),
        ("g", "3*x0 - 3*x1 - 3*x2 - ... (P16 'equalCoefficients')"),
        ("F", "100*x0 - 3*x1 - ... with a custom multiplication operator on every term (P16 CustomOperatorChain)"),
        ("z", "100*carrier(opaque) - x1*3 - 4*x2 - ... (P15 MixedChain, one opaque value never compared)"),
        ("e", "100*carrier(o1) - 3*carrier(o2) - 4*x2 - ... (P15 MixedChain, two opaque values)"),
        ("k", "100*carrier(\"s\") - x1*3 - ... (P15 MixedChain, known-safe string constant)"),
        ("n", "100*Sin(x0) - x1*3 - ... (P15 call term)"),
        ("o", "100*carrier(o0) - 3*carrier(o1) - 4*carrier(o2) - ... (P15 OpaqueChain, firstSortsLast, subtraction)"),
        ("r", "100*carrier(o0) + 3*carrier(o1) + ... (P15 OpaqueChain, firstSortsLast, addition)"),
        ("H", "2*x + 3*x + 4*x + ... (P10/P14 family H, one shared x)"),
        ("Lsub", "x0 - x1 - x2 - ... (left-associated, distinct parameters)"),
        ("Rsub", "x0 - (x1 - (x2 - ...)) (right-associated)"),
        ("Lmul", "x0 * x1 * x2 * ... (left-associated product)"),
        ("Gm", "(2*x0) * (3*x1) * (4*x2) * ... (products with constant factors)"),
        ("div", "x0 / (x1 / (x2 / ...)) (nested field division)"),
        ("pow", "x^2 * x^3 * x^4 * ... (power combination, one shared x)"),
        ("bal", "balanced sum of differences ((x0 - x1) + (x2 - x3)) + ..."),
        ("negmul", "(-x(n-1)) * (... * ((-x2) * ((-x1) * x0))) (MultiplicationWithNegate re-entries)"),
        ("negdiv", "(-(... (-((-x0) / x1)) ...)) / x(n-1) (DivisionWithNegate re-entries)"),
        ("subneg", "(-(... (-((-x0) - x1)) ...)) - x(n-1) (SubstractionWithNegate re-entries)"),
        ("negsum", "-(-(-(x0 - x1) - x2) ... - x(n-1)) (NegateWithSubstraction / canonical re-entries)"),
        ("zerominus", "0 - ((0 - (x0 * x1)) * x2) ... (SubstractionWithZero(0, t) re-entries)"),
        ("plus0", "((x0 * x1) + 0) * x2 + 0 ... (AdditionWithZero(t, 0) re-transforms its prepared operand)"),
        ("zeroplus", "0 + ((0 + (x0 * x1)) * x2) ... (AdditionWithZero(0, t) returns its operand: control of plus0)"),
        ("pow1", "((x0 * x1) ^ 1 * x2) ^ 1 ... (PowerByZeroOrOne exponent 1 re-transforms its prepared operand)"),
    ];

    /// <summary>Builds family <paramref name="name"/> at size <paramref name="n"/>.</summary>
    /// <param name="name">The family name (see <see cref="Catalog"/>).</param>
    /// <param name="n">The number of terms.</param>
    /// <returns>The tree.</returns>
    public static Expression Build(string name, int n)
    {
        ParameterExpression[] xs = Enumerable.Range(0, Math.Max(n, 1)).Select(i => Expression.Parameter(typeof(double), "x" + i)).ToArray();
        ParameterExpression x = xs[0];
        return name switch
        {
            "p" => LeftChain(Terms(xs, 100), Expression.Subtract),
            "B" => LeftChain(Terms(xs, 2), Expression.Subtract),
            "g" => LeftChain(xs.Select(v => (Expression)Expression.Multiply(Expression.Constant(3.0), v)), Expression.Subtract),
            "F" => LeftChain(xs.Select((v, i) => (Expression)Expression.Multiply(Expression.Constant(i == 0 ? 100.0 : i + 2.0), v, CustomMultiplyMethod)), Expression.Subtract),
            "z" => Mixed(xs, Expression.Multiply(Expression.Constant(100.0), Carrier(new CountingOpaque(0))), null),
            "e" => Mixed(xs, Expression.Multiply(Expression.Constant(100.0), Carrier(new CountingOpaque(0))), Expression.Multiply(Expression.Constant(3.0), Carrier(new CountingOpaque(1)))),
            "k" => Mixed(xs, Expression.Multiply(Expression.Constant(100.0), Carrier("s")), null),
            "n" => Mixed(xs, Expression.Multiply(Expression.Constant(100.0), Expression.Call(Sin, x)), null),
            "o" => LeftChain(Enumerable.Range(0, n).Select(i => (Expression)Expression.Multiply(Expression.Constant(i == 0 ? 100.0 : i + 2.0), Carrier(new CountingOpaque(i)))), Expression.Subtract),
            "r" => LeftChain(Enumerable.Range(0, n).Select(i => (Expression)Expression.Multiply(Expression.Constant(i == 0 ? 100.0 : i + 2.0), Carrier(new CountingOpaque(i)))), Expression.Add),
            "H" => LeftChain(Enumerable.Range(0, n).Select(i => (Expression)Expression.Multiply(Expression.Constant(i + 2.0), x)), Expression.Add),
            "Lsub" => LeftChain(xs, Expression.Subtract),
            "Rsub" => Enumerable.Reverse(xs).Aggregate((Expression?)null, (acc, v) => acc is null ? v : Expression.Subtract(v, acc))!,
            "Lmul" => LeftChain(xs, Expression.Multiply),
            "Gm" => LeftChain(xs.Select((v, i) => (Expression)Expression.Multiply(Expression.Constant(i + 2.0), v)), Expression.Multiply),
            "div" => Enumerable.Reverse(xs).Aggregate((Expression?)null, (acc, v) => acc is null ? v : Expression.Divide(v, acc))!,
            "pow" => LeftChain(Enumerable.Range(0, n).Select(i => (Expression)Expression.Power(x, Expression.Constant(i + 2.0))), Expression.Multiply),
            "negmul" => Nest(xs, (acc, v) => Expression.Multiply(Expression.Negate(v), acc)),
            "negdiv" => Nest(xs, (acc, v) => Expression.Divide(Expression.Negate(acc), v)),
            "subneg" => Nest(xs, (acc, v) => Expression.Subtract(Expression.Negate(acc), v)),
            "negsum" => Nest(xs, (acc, v) => Expression.Negate(Expression.Subtract(acc, v))),
            "zerominus" => Nest(xs, (acc, v) => Expression.Subtract(Expression.Constant(0.0), Expression.Multiply(acc, v))),
            "plus0" => Nest(xs, (acc, v) => Expression.Add(Expression.Multiply(acc, v), Expression.Constant(0.0))),
            "zeroplus" => Nest(xs, (acc, v) => Expression.Add(Expression.Constant(0.0), Expression.Multiply(acc, v))),
            "pow1" => Nest(xs, (acc, v) => Expression.Power(Expression.Multiply(acc, v), Expression.Constant(1.0))),
            "bal" => Balanced(Enumerable.Range(0, Math.Max(1, n / 2)).Select(i => (Expression)Expression.Subtract(xs[2 * i % n], xs[(2 * i + 1) % n])).ToList()),
            _ => throw new ArgumentException("Unknown family " + name),
        };
    }

    /// <summary>The inert carrier of an opaque value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The carrier.</returns>
    public static Expression Carrier(object value)
        => Expression.Call(Truncate, Expression.Coalesce(Expression.TypeAs(Expression.Constant(value, typeof(object)), typeof(double?)), Expression.Constant(1.0)));

    /// <summary>The terms <c>c0*x0, x1*3, 4*x2, x3*5, ...</c> (factor order alternating).</summary>
    /// <param name="xs">The parameters.</param>
    /// <param name="first">The coefficient of the first term.</param>
    /// <returns>The terms.</returns>
    private static IEnumerable<Expression> Terms(ParameterExpression[] xs, double first)
        => xs.Select((v, i) => i == 0
            ? (Expression)Expression.Multiply(Expression.Constant(first), v)
            : i % 2 == 0 ? Expression.Multiply(Expression.Constant(i + 2.0), v) : Expression.Multiply(v, Expression.Constant(i + 2.0)));

    /// <summary>The P15 mixed chain: the given head term (and optional second term) followed by the P16 terms.</summary>
    /// <param name="xs">The parameters.</param>
    /// <param name="head">The first term.</param>
    /// <param name="second">The second term, or <see langword="null"/> for <c>x1*3</c>.</param>
    /// <returns>The chain.</returns>
    private static Expression Mixed(ParameterExpression[] xs, Expression head, Expression? second)
    {
        Expression[] terms = Terms(xs, 100).ToArray();
        terms[0] = head;
        if (second is not null && terms.Length > 1) terms[1] = second;
        return LeftChain(terms, Expression.Subtract);
    }

    /// <summary>Folds items into a left-associated chain.</summary>
    /// <param name="items">The items.</param>
    /// <param name="combine">The operator.</param>
    /// <returns>The chain.</returns>
    private static Expression LeftChain(IEnumerable<Expression> items, Func<Expression, Expression, BinaryExpression> combine)
        => items.Aggregate((acc, item) => combine(acc, item));

    /// <summary>Wraps the accumulator with each further parameter: <c>acc = wrap(acc, x_i)</c>, starting from <c>x0</c>.</summary>
    /// <param name="xs">The parameters.</param>
    /// <param name="wrap">The wrapping shape.</param>
    /// <returns>The nested tree.</returns>
    private static Expression Nest(ParameterExpression[] xs, Func<Expression, Expression, Expression> wrap)
    {
        Expression acc = xs[0];
        for (int i = 1; i < xs.Length; i++) acc = wrap(acc, xs[i]);
        return acc;
    }

    /// <summary>Folds items into a balanced sum.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The sum.</returns>
    private static Expression Balanced(IReadOnlyList<Expression> items)
        => items.Count == 1 ? items[0] : Expression.Add(Balanced(items.Take(items.Count / 2).ToList()), Balanced(items.Skip(items.Count / 2).ToList()));
}
