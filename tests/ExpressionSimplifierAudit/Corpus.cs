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
        ("neg", "-(-(...(-x0))) (n ordinary negations, S5A-10)"),
        ("negcall", "Sin(-(-Sin(-(-(... x0))))) (n double negations between calls, S5A-10)"),
        ("negdag", "s = -(-t); t' = s + s (shared double negations, 2^n logical nodes, S5A-10)"),
        ("negadd", "((x0 + -(-x1)) + -(-x2)) + ... (double negations inside a left-associated sum, S5A-10)"),
        ("negprod", "(-x(n-1)) * (... * ((-x1) * (-x0))) (each product of two negations is a double negation, S5A-10)"),
        ("altsign", "x0 - (-2*x1) + (-3*x2) - (-4*x3) + ... (alternating signs over negative coefficients, sign normalization)"),         ("negcoef", "(-2*x0) + (-3*x1) + (-4*x2) + ... (added terms with negative coefficients)"),         ("negprodsum", "-(2*x0) - (3*x1) + -(4*x2) - ... (negated products with positive coefficients)"),         ("sharedneg", "t - t + t - ... with t = -2 * x0 shared (one negative-coefficient product, n occurrences)"),
        ("zerominus", "0 - ((0 - (x0 * x1)) * x2) ... (SubstractionWithZero(0, t) re-entries)"),
        ("plus0", "((x0 * x1) + 0) * x2 + 0 ... (AdditionWithZero(t, 0) re-transforms its prepared operand)"),
        ("zeroplus", "0 + ((0 + (x0 * x1)) * x2) ... (AdditionWithZero(0, t) returns its operand: control of plus0)"),
        ("pow1", "((x0 * x1) ^ 1 * x2) ^ 1 ... (PowerByZeroOrOne exponent 1 re-transforms its prepared operand)"),
        ("horner", "((x0 * x1 + 1) * x1 + 1) * x1 + 1 ... (Horner chain: the power rule compares (1 + ...) with x1 at every level, comparer depth capped at 7)"),
        ("constabs", "2 + Abs(2 + Abs(... Sin(x0))) (AdditionOfEqualsElements probes a constant other than 1 against a call: 2^n comparer simplifications)"),
        ("nanabs", "NaN + Abs(NaN + Abs(... Sin(x0))) (constabs with NaN; a non-default policy folds every level, S5 P18 C2b)"),
        ("nanpow", "NaN + (NaN + (... x0) ^ 2) ^ 2 (inert operand at every level: a non-default policy classifies the whole operand at every level, S5 P18 C2b)"),
        ("subdiv", "1 - x1/x0 - x2/x0 - ... - xn/x0 (left-associated subtraction of quotients over one denominator, n quotients)"),
        ("subdivR", "1 - (x1/x0 - (x2/x0 - (... - xn/x0))) (right-associated)"),
        ("subdivB", "1 - ((x1/x0 + x2/x0) + (x3/x0 + x4/x0)) ... (balanced sum of the quotients, subtracted once)"),
        ("subdivM", "1 - x1/x0 + x2/x0 - x3/x0 + ... (left-associated, alternating signs)"),
        ("subdivD", "1 - x1/y1 - x2/y2 - ... - xn/yn (left-associated, distinct denominators)"),
        ("subdivL", "1 - x1/x0 - x2/x0 - ... - xn/x0 over long (integral division: not a field)"),
        ("subdivRD", "1 - (x1/y1 - (x2/y2 - (... - xn/yn))) (right-associated, distinct denominators)"),
        ("subdivRF", "1 - (x1/x0 - (x2/x0 - (... - xn/x0))) over float"),
        ("subdivBM", "1 - ((x1/x0 - x2/x0) + (x3/x0 - x4/x0)) ... (balanced, every right operand of an odd level subtracted)"),
        ("subdivBF", "1 - ((x1/x0 + x2/x0) + (x3/x0 + x4/x0)) ... over float"),
        ("refusedfac", "(2 - 1) + x0*x1 + x0*x2 + ... (refused by its constant pair, factorable once the levels fold it: C3c consistency)"),
        ("refusednone", "(2 - 1) + x1*y1 + x2*y2 + ... (refused by its constant pair, no common factor once folded)"),
        ("tailquot", "x0*x1 + x0*x2 + ... + x1/x0 (refused by its last quotient; its factorable sub-chain is factored on its own)"),
        ("dagfactor", "s*x1 + s*x2 + ... with s = (u + v) shared (a sum factor: binary factorings level by level, C3d dag)"),
        ("mixsign", "2*x1 - 2*x2 + (-2)*x3 - (-2)*x4 + ... (one coefficient magnitude with both signs)"),
        ("m4suffix", "((2*x1 + x0/q) + 2*x2) + 2*x3 + ... (M4-1: refused by the quotient at every level, the products form the canonical suffix)"),
        ("m4middle", "2*x1 + ... + 2*xk + x0/q + 2*x(k+1) + ... (M4-2: factorable terms before and after the quotient)"),
        ("m4segments", "2*x1 + x0/q1 + 2*x2 + x0/q2 + ... (M4-3: one quotient every second term)"),
        ("m4none", "((Sin(x1)*y1 + x0/q) + Sin(x2)*y2) + ... (M4-4: a factorable suffix without any common factor)"),
        ("m4calls", "((2*x1 + x0/q) + Next()) + 2*x2 + Next() + ... (M4-5: observable calls, pinned last)"),
        ("m4dag", "((2*x1 + s) + 2*x2) + s + ... with s = x0/q shared (M4-6: a shared quotient)"),
        ("bfquot", "((x0/q + y1*(z/w)) + y2*(z/w)) + ... (mission B: identical quotients as the factor of refused products)"),
        ("bfquotcoef", "((x0/q + 2*(x1/w)) + 2*(x2/w)) + ... (mission B: different quotients, one common coefficient)"),
        ("bfsum", "((x0/q + y1*(1 + z)) + y2*(1 + z)) + ... (mission B: identical sums as the factor)"),
        ("bfsumcoef", "((x0/q + 2*(x1 + z)) + 2*(x2 + z)) + ... (mission B: different sums, one common coefficient)"),
        ("bfalt", "x1/q + 2*x1 + x2/q + 2*x2 + ... (mission B: quotients and products alternating)"),
        ("bfnone", "((x0/q + y1*(x1/w1)) + y2*(x2/w2)) + ... (mission B: no common factor)"),
        ("bfdag", "((x0/q + y1*s) + y2*s) + ... with s = (1 + z) shared (mission B: a shared sum factor)"),
        ("bfcalls", "x0/q + y1*(z/w) + Next() + y2*(z/w) + Next() + ... (mission B: observable calls, pinned last)"),
        ("bfgroup", "((y + x/z) + x/z) + ... + x/z (mission B: a group of identical quotients)"),
        ("mdag", "s = x*y; s = s*s, n times (multiplicative DAG: n + 3 distinct nodes, 2^(n+2) - 1 occurrences; variant A)"),
        ("mdagtree", "the tree copy of mdag (no shared node; control of mdag, small n only)"),
        ("mdagq", "x0/q + mdag (a refused sum holding the DAG; variant B)"),
        ("mdagm4", "x0/q + (mdag + 2*x1) (the DAG inside an M4 suffix; variant C)"),
        ("mdag2", "mdag*x1 + mdag*y1 (two products sharing the DAG; variant D)"),
        ("mdagmix", "s = x*y; s = (s + s) * s, n times (additive and multiplicative sharing; variant E)"),
        ("mdagnone", "x0/q + mdag + z*w (no common factor; variant F)"),
        ("mdagopaque", "s = carrier(o)*x; s = s*s, n times (an opaque factor; variant G)"),
        ("mdagieee", "s = (x*1e300)*(y*-0.0); s = s*s, n times (extreme and signed-zero constants; variant H)"),
        ("mdagnan", "x0/q + s, s = (x*NaN)*y; s = s*s, n times (NaN constant; variant H)"),
        ("mdaglong", "mdag over long (integral wrapping product)"),
        ("mdaglongtree", "the tree copy of mdaglong (control, small n only)"),
        ("Lmullong", "x * y * x * y * ... over long, n factors, left-associated (control of mdaglong without sharing)"),
        ("mdagcustom", "s = x*y; s = s*s with a custom operator method, n times (variant G: custom operator)"),
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
            "neg" => Enumerable.Range(0, n).Aggregate((Expression)x, (acc, _) => Expression.Negate(acc)),
            "negcall" => Enumerable.Range(0, n).Aggregate((Expression)x, (acc, _) => Expression.Call(Sin, Expression.Negate(Expression.Negate(acc)))),
            "negdag" => Enumerable.Range(0, n).Aggregate((Expression)x, (acc, _) => Expression.Negate(Expression.Negate(acc)) is var shared ? Expression.Add(shared, shared) : acc),
            "negadd" => LeftChain(xs.Select((v, i) => i == 0 ? (Expression)v : Expression.Negate(Expression.Negate(v))), Expression.Add),
            "negprod" => xs.Skip(1).Aggregate((Expression)Expression.Negate(x), (acc, v) => Expression.Multiply(Expression.Negate(v), acc)),
            "altsign" => xs.Skip(1).Select((v, i) => (v, i)).Aggregate((Expression)x, (acc, p) => p.i % 2 == 0 ? Expression.Subtract(acc, Expression.Multiply(Expression.Constant(-(p.i + 2.0)), p.v)) : Expression.Add(acc, Expression.Multiply(Expression.Constant(-(p.i + 2.0)), p.v))),             "negcoef" => LeftChain(xs.Select((v, i) => (Expression)Expression.Multiply(Expression.Constant(-(i + 2.0)), v)), Expression.Add),             "negprodsum" => xs.Skip(1).Select((v, i) => (v, i)).Aggregate((Expression)Expression.Negate(Expression.Multiply(Expression.Constant(2.0), x)), (acc, p) => p.i % 2 == 0 ? Expression.Subtract(acc, Expression.Multiply(Expression.Constant(p.i + 3.0), p.v)) : Expression.Add(acc, Expression.Negate(Expression.Multiply(Expression.Constant(p.i + 3.0), p.v)))),             "sharedneg" => Expression.Multiply(Expression.Constant(-2.0), x) is var shared ? Enumerable.Range(1, Math.Max(1, n - 1)).Aggregate((Expression)shared, (acc, i) => i % 2 == 1 ? Expression.Subtract(acc, shared) : Expression.Add(acc, shared)) : x,
            "refusedfac" => LeftChain(xs.Skip(1).Select(v => (Expression)Expression.Multiply(x, v)).Prepend(Expression.Subtract(Expression.Constant(2.0), Expression.Constant(1.0))), Expression.Add),
            "refusednone" => LeftChain(xs.Skip(1).Select((v, i) => (Expression)Expression.Multiply(v, Expression.Parameter(typeof(double), "y" + (i + 1)))).Prepend(Expression.Subtract(Expression.Constant(2.0), Expression.Constant(1.0))), Expression.Add),
            "tailquot" => Expression.Add(LeftChain(xs.Skip(1).Select(v => (Expression)Expression.Multiply(x, v)).DefaultIfEmpty(x), Expression.Add), Expression.Divide(xs[Math.Min(1, n - 1)], x)),
            "dagfactor" => Expression.Add(Expression.Parameter(typeof(double), "u"), Expression.Parameter(typeof(double), "v")) is var sum
                ? LeftChain(xs.Select(v => (Expression)Expression.Multiply(sum, v)), Expression.Add) : x,
            "mixsign" => xs.Select((v, i) => (Expression)Expression.Multiply(Expression.Constant(i % 4 < 2 ? 2.0 : -2.0), v)).Select((term, i) => (term, i))
                .Aggregate((Expression?)null, (acc, p) => acc is null ? p.term : p.i % 2 == 1 ? Expression.Subtract(acc, p.term) : Expression.Add(acc, p.term))!,
            "m4suffix" => LeftChain(Products(xs, 2.0).Take(1).Append(Quotient(x, "q")).Concat(Products(xs, 2.0).Skip(1)), Expression.Add),
            "m4middle" => LeftChain(Products(xs, 2.0).Take(Math.Max(1, (n - 1) / 2)).Append(Quotient(x, "q")).Concat(Products(xs, 2.0).Skip(Math.Max(1, (n - 1) / 2))), Expression.Add),
            "m4segments" => LeftChain(Products(xs, 2.0).SelectMany((p, i) => new[] { p, Quotient(x, "q" + i) }).Take(Math.Max(2, n)), Expression.Add),
            "m4none" => LeftChain(xs.Skip(1).Select((v, i) => (Expression)Expression.Multiply(Expression.Call(Sin, v), Expression.Parameter(typeof(double), "y" + (i + 1))))
                .Take(1).Append(Quotient(x, "q")).Concat(xs.Skip(2).Select((v, i) => (Expression)Expression.Multiply(Expression.Call(Sin, v), Expression.Parameter(typeof(double), "y" + (i + 2))))), Expression.Add),
            "m4calls" => LeftChain(Products(xs, 2.0).Take(1).Append(Quotient(x, "q")).Concat(Products(xs, 2.0).Skip(1).SelectMany(p => new[] { Expression.Call(typeof(EffectLeaves).GetMethod(nameof(EffectLeaves.Next))!), p })), Expression.Add),
            "bfquot" => LeftChain(Ys(n).Select(y => (Expression)Expression.Multiply(y, Expression.Divide(Z, W))).Prepend(Quotient(x, "q")), Expression.Add),
            "bfquotcoef" => LeftChain(xs.Skip(1).DefaultIfEmpty(x).Select(v => (Expression)Expression.Multiply(Expression.Constant(2.0), Expression.Divide(v, W))).Prepend(Quotient(x, "q")), Expression.Add),
            "bfsum" => LeftChain(Ys(n).Select(y => (Expression)Expression.Multiply(y, Expression.Add(Expression.Constant(1.0), Z))).Prepend(Quotient(x, "q")), Expression.Add),
            "bfsumcoef" => LeftChain(xs.Skip(1).DefaultIfEmpty(x).Select(v => (Expression)Expression.Multiply(Expression.Constant(2.0), Expression.Add(v, Z))).Prepend(Quotient(x, "q")), Expression.Add),
            "bfalt" => LeftChain(xs.SelectMany(v => new Expression[] { Expression.Divide(v, Expression.Parameter(typeof(double), "q")), Expression.Multiply(Expression.Constant(2.0), v) }).Take(Math.Max(2, n)), Expression.Add),
            "bfnone" => LeftChain(Ys(n).Select((y, i) => (Expression)Expression.Multiply(y, Expression.Divide(xs[Math.Min(i + 1, xs.Length - 1)], Expression.Parameter(typeof(double), "w" + (i + 1))))).Prepend(Quotient(x, "q")), Expression.Add),
            "bfdag" => Expression.Add(Expression.Constant(1.0), Z) is var shared ? LeftChain(Ys(n).Select(y => (Expression)Expression.Multiply(y, shared)).Prepend(Quotient(x, "q")), Expression.Add) : x,
            "bfcalls" => LeftChain(Ys(n).SelectMany(y => new Expression[] { Expression.Multiply(y, Expression.Divide(Z, W)), Expression.Call(typeof(EffectLeaves).GetMethod(nameof(EffectLeaves.Next))!) }).Prepend(Quotient(x, "q")), Expression.Add),
            "bfgroup" => LeftChain(Enumerable.Range(0, Math.Max(2, n)).Select(_ => (Expression)Expression.Divide(x, Z)).Prepend(Expression.Parameter(typeof(double), "y")), Expression.Add),
            "mdag" => MulDag(n, typeof(double), shared: true),
            "mdagtree" => MulDag(n, typeof(double), shared: false),
            "mdagq" => Expression.Add(Quotient(x, "q"), MulDag(n, typeof(double), shared: true)),
            "mdagm4" => Expression.Add(Quotient(x, "q"), Expression.Add(MulDag(n, typeof(double), shared: true), Expression.Multiply(Expression.Constant(2.0), xs[Math.Min(1, xs.Length - 1)]))),
            "mdag2" => MulDag(n, typeof(double), shared: true) is var dag
                ? Expression.Add(Expression.Multiply(dag, xs[Math.Min(1, xs.Length - 1)]), Expression.Multiply(dag, Ys(1).First())) : x,
            "mdagmix" => Enumerable.Range(0, n).Aggregate((Expression)Expression.Multiply(Dx, Dy), (acc, _) => Expression.Multiply(Expression.Add(acc, acc), acc)),
            "mdagnone" => Expression.Add(Expression.Add(Quotient(x, "q"), MulDag(n, typeof(double), shared: true)), Expression.Multiply(Z, W)),
            "mdagopaque" => Enumerable.Range(0, n).Aggregate(Expression.Multiply(Carrier(new CountingOpaque(0)), Dx), (acc, _) => Expression.Multiply(acc, acc)),
            "mdagieee" => Enumerable.Range(0, n).Aggregate(Expression.Multiply(Expression.Multiply(Dx, Expression.Constant(1e300)), Expression.Multiply(Dy, Expression.Constant(-0.0))), (acc, _) => Expression.Multiply(acc, acc)),
            "mdagnan" => Expression.Add(Quotient(x, "q"), Enumerable.Range(0, n).Aggregate(Expression.Multiply(Expression.Multiply(Dx, Expression.Constant(double.NaN)), Dy), (acc, _) => Expression.Multiply(acc, acc))),
            "mdaglong" => MulDag(n, typeof(long), shared: true),
            "mdaglongtree" => MulDag(n, typeof(long), shared: false),
            "Lmullong" => new[] { Expression.Parameter(typeof(long), "x"), Expression.Parameter(typeof(long), "y") } is var pair
                ? LeftChain(Enumerable.Range(0, Math.Max(n, 1)).Select(i => (Expression)pair[i % 2]), Expression.Multiply) : x,
            "mdagcustom" => Enumerable.Range(0, n).Aggregate(Expression.Multiply(Dx, Dy, CustomMultiplyMethod), (acc, _) => Expression.Multiply(acc, acc, CustomMultiplyMethod)),
            "m4dag" => Quotient(x, "q") is var s ?LeftChain(Products(xs, 2.0).Take(1).Append(s).Concat(Products(xs, 2.0).Skip(1).SelectMany(p => new[] { p, s })), Expression.Add) : x,
            "zerominus" => Nest(xs, (acc, v) => Expression.Subtract(Expression.Constant(0.0), Expression.Multiply(acc, v))),
            "plus0" => Nest(xs, (acc, v) => Expression.Add(Expression.Multiply(acc, v), Expression.Constant(0.0))),
            "zeroplus" => Nest(xs, (acc, v) => Expression.Add(Expression.Constant(0.0), Expression.Multiply(acc, v))),
            "pow1" => Nest(xs, (acc, v) => Expression.Power(Expression.Multiply(acc, v), Expression.Constant(1.0))),
            "nanabs" => Enumerable.Range(0, n).Aggregate((Expression)Expression.Call(Sin, x), (acc, _) => Expression.Add(Expression.Constant(double.NaN), Expression.Call(typeof(Math).GetMethod(nameof(Math.Abs), [typeof(double)])!, acc))),
            "nanpow" => Enumerable.Range(0, n).Aggregate((Expression)x, (acc, _) => Expression.Add(Expression.Constant(double.NaN), Expression.Power(acc, Expression.Constant(2.0)))),
            "constabs" =>Enumerable.Range(0, n).Aggregate((Expression)Expression.Call(Sin, x), (acc, _) => Expression.Add(Expression.Constant(2.0), Expression.Call(typeof(Math).GetMethod(nameof(Math.Abs), [typeof(double)])!, acc))),
            "horner" => Enumerable.Range(0, n).Aggregate((Expression)xs[0], (acc, _) => Expression.Add(Expression.Multiply(acc, xs[Math.Min(1, n - 1)]), Expression.Constant(1.0))),
            "subdiv" => LeftChain(Quotients(n, false).Prepend(Expression.Constant(1.0)), Expression.Subtract),
            "subdivR" => Quotients(n, false).Reverse().Aggregate((Expression?)null, (acc, q) => acc is null ? q : Expression.Subtract(q, acc)) is { } tail
                ? Expression.Subtract(Expression.Constant(1.0), tail) : Expression.Constant(1.0),
            "subdivB" => Expression.Subtract(Expression.Constant(1.0), Balanced(Quotients(n, false).ToList())),
            "subdivM" => Quotients(n, false).Select((q, i) => (q, i)).Aggregate((Expression)Expression.Constant(1.0),
                (acc, p) => p.i % 2 == 0 ? Expression.Subtract(acc, p.q) : Expression.Add(acc, p.q)),
            "subdivD" => LeftChain(Quotients(n, true).Prepend(Expression.Constant(1.0)), Expression.Subtract),
            "subdivL" => LeftChain(Quotients(n, false, typeof(long)).Prepend(Expression.Constant(1L)), Expression.Subtract),
            "subdivRD" => RightSubtraction(Quotients(n, true), Expression.Constant(1.0)),
            "subdivRF" => RightSubtraction(Quotients(n, false, typeof(float)), Expression.Constant(1f)),
            "subdivBM" => Expression.Subtract(Expression.Constant(1.0), Balanced(Quotients(n, false).ToList(), 0)),
            "subdivBF" => Expression.Subtract(Expression.Constant(1f), Balanced(Quotients(n, false, typeof(float)).ToList())),
            "bal" => Balanced(Enumerable.Range(0, Math.Max(1, n / 2)).Select(i => (Expression)Expression.Subtract(xs[2 * i % n], xs[(2 * i + 1) % n])).ToList()),
            _ => throw new ArgumentException("Unknown family " + name),
        };
    }

    /// <summary>The products <c>c*x1, c*x2, ...</c> of the parameters but the first (the M4 families).</summary>
    /// <param name="xs">The parameters.</param>
    /// <param name="coefficient">The common coefficient.</param>
    /// <returns>The products.</returns>
    private static IEnumerable<Expression> Products(ParameterExpression[] xs, double coefficient)
        => xs.Skip(1).DefaultIfEmpty(xs[0]).Select(v => (Expression)Expression.Multiply(Expression.Constant(coefficient), v));

    /// <summary>The parameter <c>x</c> of the multiplicative DAG families.</summary>
    private static readonly ParameterExpression Dx = Expression.Parameter(typeof(double), "x");

    /// <summary>The parameter <c>y</c> of the multiplicative DAG families.</summary>
    private static readonly ParameterExpression Dy = Expression.Parameter(typeof(double), "y");

    /// <summary>
    /// The multiplicative DAG of the mission "multiplicative DAG resource bounds": <c>s = x * y</c>, then <c>s = s * s</c>
    /// <paramref name="n"/> times, with <c>s</c> shared (<paramref name="shared"/>) or rebuilt as a tree.
    /// </summary>
    /// <param name="n">The number of squarings.</param>
    /// <param name="type">The type of the parameters.</param>
    /// <param name="shared">Whether both operands of each squaring are the same instance.</param>
    /// <returns>The DAG or its tree copy.</returns>
    private static Expression MulDag(int n, Type type, bool shared)
    {
        ParameterExpression px = type == typeof(double) ? Dx : Expression.Parameter(type, "x");
        ParameterExpression py = type == typeof(double) ? Dy : Expression.Parameter(type, "y");
        Expression Build(int level) => level == 0 ? Expression.Multiply(px, py) : Expression.Multiply(Build(level - 1), Build(level - 1));
        if (!shared) return Build(n);
        Expression result = Expression.Multiply(px, py);
        for (int level = 0; level < n; level++) result = Expression.Multiply(result, result);
        return result;
    }

    /// <summary>The parameter <c>z</c> of the mission B families.</summary>
    private static readonly ParameterExpression Z = Expression.Parameter(typeof(double), "z");

    /// <summary>The parameter <c>w</c> of the mission B families.</summary>
    private static readonly ParameterExpression W = Expression.Parameter(typeof(double), "w");

    /// <summary>The parameters <c>y1</c> to <c>yn</c> of the mission B families (at least one).</summary>
    /// <param name="n">The number of parameters.</param>
    /// <returns>The parameters.</returns>
    private static IEnumerable<ParameterExpression> Ys(int n) => Enumerable.Range(1, Math.Max(1, n)).Select(i => Expression.Parameter(typeof(double), "y" + i));

    /// <summary>The quotient <c>x / name</c>, by a parameter of its own (the M4 families).</summary>
    /// <param name="x">The numerator.</param>
    /// <param name="name">The name of the denominator.</param>
    /// <returns>The quotient.</returns>
    private static Expression Quotient(ParameterExpression x, string name) => Expression.Divide(x, Expression.Parameter(typeof(double), name));

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

    /// <summary>The quotients <c>x1/x0, ..., xn/x0</c>, or <c>x1/y1, ..., xn/yn</c> over distinct denominators.</summary>
    /// <param name="n">The number of quotients.</param>
    /// <param name="distinct">Whether every quotient has its own denominator.</param>
    /// <param name="type">The type of the parameters (<see cref="double"/> by default).</param>
    /// <returns>The quotients.</returns>
    private static IEnumerable<Expression> Quotients(int n, bool distinct, Type? type = null)
    {
        type ??= typeof(double);
        ParameterExpression x0 = Expression.Parameter(type, "x0");
        for (int i = 1; i <= n; i++)
        {
            Expression denominator = distinct ? Expression.Parameter(type, "y" + i) : x0;
            yield return Expression.Divide(Expression.Parameter(type, "x" + i), denominator);
        }
    }

    /// <summary>Folds items into a balanced sum.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The sum.</returns>
    private static Expression Balanced(IReadOnlyList<Expression> items)
        => items.Count == 1 ? items[0] : Expression.Add(Balanced(items.Take(items.Count / 2).ToList()), Balanced(items.Skip(items.Count / 2).ToList()));

    /// <summary>Folds items into a balanced tree whose nodes at an odd level subtract their right operand.</summary>
    /// <param name="items">The items.</param>
    /// <param name="level">The level of the node built (the root is at level 0).</param>
    /// <returns>The tree.</returns>
    private static Expression Balanced(IReadOnlyList<Expression> items, int level)
    {
        if (items.Count == 1) return items[0];
        Expression left = Balanced(items.Take(items.Count / 2).ToList(), level + 1);
        Expression right = Balanced(items.Skip(items.Count / 2).ToList(), level + 1);
        return level % 2 == 1 ? Expression.Subtract(left, right) : Expression.Add(left, right);
    }

    /// <summary>Builds <c>head - (q1 - (q2 - (... - qn)))</c>.</summary>
    /// <param name="items">The terms <c>q1 ... qn</c>.</param>
    /// <param name="head">The first term.</param>
    /// <returns>The tree.</returns>
    private static Expression RightSubtraction(IEnumerable<Expression> items, Expression head)
        => items.Reverse().Aggregate((Expression?)null, (acc, q) => acc is null ? q : Expression.Subtract(q, acc)) is { } tail
            ? Expression.Subtract(head, tail) : head;
}
