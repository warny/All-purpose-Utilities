using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// Security invariants of the flatten rule of <see cref="ExpressionSimplifier"/> (PR B,
/// <c>docs/expressions/SimplifierFlatten.md</c>): the work of an accepted chain is counted and refused just above a limit,
/// the shape limits still apply first, an accepted chain of any depth is simplified on a 256 KiB stack while a declined one
/// keeps the controlled stack refusal, the rule never reads an extension node nor a custom reflection object, and it never
/// accepts a chain whose evaluation is observable.
/// </summary>
[TestClass]
public class ExpressionSimplifierFlattenResourceTests
{
    /// <summary>The stack size of the worker threads.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>The maximum duration of a worker thread.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    /// <summary>Limits without any bound.</summary>
    private static readonly ExpressionResourceLimits NoLimits = ExpressionResourceLimits.Unlimited;

    /// <summary>A derived simplifier without any member: it keeps the level by level treatment.</summary>
    private sealed class LevelByLevelSimplifier : ExpressionSimplifier
    {
        /// <summary>Initializes the simplifier.</summary>
        /// <param name="limits">The resource limits.</param>
        public LevelByLevelSimplifier(ExpressionResourceLimits limits)
            : base(limits)
        {
        }
    }

    /// <summary>Builds a chain of <paramref name="terms"/> distinct parameters, iteratively.</summary>
    /// <param name="terms">The number of terms.</param>
    /// <param name="shape">0: left-associated sum; 1: right-associated sum; 2: left-associated difference.</param>
    /// <returns>The chain.</returns>
    private static Expression LinearChain(int terms, int shape)
    {
        Expression e = Expression.Parameter(typeof(double), "x0");
        for (int i = 1; i < terms; i++)
        {
            ParameterExpression x = Expression.Parameter(typeof(double), "x" + i);
            e = shape switch
            {
                0 => Expression.Add(e, x),
                1 => Expression.Add(x, e),
                _ => Expression.Subtract(e, x),
            };
        }

        return e;
    }

    /// <summary>Counts the terms of a right-associated canonical chain, iteratively.</summary>
    /// <param name="chain">The result of the simplification.</param>
    /// <returns>The number of groups of the chain.</returns>
    private static int CountGroups(Expression chain)
    {
        int count = 1;
        while (chain is BinaryExpression { NodeType: ExpressionType.Add } add)
        {
            count++;
            chain = add.Right;
        }

        return count;
    }

    /// <summary>Simplifies <paramref name="e"/> inside an outer operation and returns the visit counters of the whole call.</summary>
    /// <param name="simplifier">The simplifier.</param>
    /// <param name="e">The expression.</param>
    /// <returns>The transformation and structural visits.</returns>
    private static (long Transform, long Structural) Count(ExpressionSimplifier simplifier, Expression e)
        => ExpressionResourceBudget.Run(NoLimits, null, null, null, (simplifier, e), static state =>
        {
            state.simplifier.Simplify(state.e);
            return (ExpressionResourceBudget.TransformVisits, ExpressionResourceBudget.StructuralVisits);
        });

    /// <summary>
    /// The work of an accepted chain is counted: one transformation visit for the chain and one per term, and structural
    /// visits that grow linearly with the chain (extraction, grouping walk, keys, sort). Each budget refuses the chain just
    /// above its limit with an exception of the right kind, leaves no operation open, and the same instance works again.
    /// </summary>
    [TestMethod]
    public void AcceptedChain_IsCounted_AndRefusedJustAboveEachLimit()
    {
        Expression chain = LinearChain(64, 2);
        (long transform, long structural) = Count(new ExpressionSimplifier(NoLimits), chain);
        Assert.AreEqual(65, transform);
        Assert.IsTrue(structural >= 2 * 64 - 1, $"{structural} structural visits");

        (_, long structural128) = Count(new ExpressionSimplifier(NoLimits), LinearChain(128, 2));
        (_, long structural256) = Count(new ExpressionSimplifier(NoLimits), LinearChain(256, 2));
        Assert.IsTrue(structural256 - structural128 < 4 * (structural128 - structural), $"structural visits: {structural}, {structural128}, {structural256}");

        Assert.IsNotNull(new ExpressionSimplifier(NoLimits with { MaxTransformVisits = transform }).Simplify(chain));
        var workLimited = new ExpressionSimplifier(NoLimits with { MaxTransformVisits = transform - 1 });
        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => workLimited.Simplify(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.Work, exception.Kind);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.AreEqual(8, CountGroups(workLimited.Simplify(LinearChain(8, 2))), "The refused instance works again.");

        Assert.IsNotNull(new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = structural }).Simplify(chain));
        var structureLimited = new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = structural - 1 });
        exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => structureLimited.Simplify(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, exception.Kind);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.AreEqual(8, CountGroups(structureLimited.Simplify(LinearChain(8, 2))), "The refused instance works again.");
    }

    /// <summary>
    /// The shape limits apply before the flatten rule: a linear chain deeper than <see cref="ExpressionResourceLimits.MaxDepth"/>
    /// or larger than <see cref="ExpressionResourceLimits.MaxNodes"/> is refused by the validation, as before, although the
    /// rule would simplify it without recursion.
    /// </summary>
    [TestMethod]
    public void ShapeLimits_StillRefuseALinearChain()
    {
        Expression chain = LinearChain(600, 0);

        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier().Simplify(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.Depth, exception.Kind);

        exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier(Shape(maxNodes: 1_000)).Simplify(chain));
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, exception.Kind);

        Assert.AreEqual(500, CountGroups(new ExpressionSimplifier().Simplify(LinearChain(500, 0))), "A chain within the limits is simplified.");
    }

    /// <summary>
    /// On a 256 KiB thread and without limits, an accepted chain of 100,000 terms of every grouping is simplified: no part
    /// of the treatment of an accepted chain recurses on the chain (admission, extraction, grouping walk, canonicalization,
    /// reconstruction).
    /// </summary>
    [TestMethod]
    public void AcceptedDeepChain_IsSimplified_OnASmallStack()
    {
        const int Terms = 100_000;
        foreach (int shape in new[] { 0, 1, 2 })
        {
            Expression chain = LinearChain(Terms, shape);
            RunOnThread(() =>
            {
                Expression result = new ExpressionSimplifier(NoLimits).Simplify(chain);
                Assert.AreEqual(Terms, CountGroups(result), $"shape {shape}");
                Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
            }, SmallStack, Timeout);
        }
    }

    /// <summary>
    /// A declined deep chain keeps the level by level treatment and its controlled stack refusal on a 256 KiB thread (never a
    /// crash), like the same chain given to a derived simplifier; no operation stays open and the instance works again.
    /// </summary>
    [TestMethod]
    public void DeclinedDeepChain_IsAControlledStackRefusal()
    {
        // The last term repeats the first parameter: the chain is not linear.
        Expression linear = LinearChain(20_000, 0);
        ParameterExpression first = (ParameterExpression)Leftmost(linear);
        Expression declined = Expression.Add(linear, first);

        RunOnThread(() =>
        {
            var simplifier = new ExpressionSimplifier(NoLimits);
            ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => simplifier.Simplify(declined));
            Assert.AreEqual(ExpressionResourceLimitKind.Stack, exception.Kind);
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
            Assert.AreEqual(20_000, CountGroups(simplifier.Simplify(linear)), "The instance works again, on the accepted part.");

            exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new LevelByLevelSimplifier(NoLimits).Simplify(linear));
            Assert.AreEqual(ExpressionResourceLimitKind.Stack, exception.Kind);
        }, SmallStack, Timeout);
    }

    /// <summary>Returns the leftmost leaf of a left-associated chain, iteratively.</summary>
    /// <param name="chain">The chain.</param>
    /// <returns>The leftmost operand.</returns>
    private static Expression Leftmost(Expression chain)
    {
        while (chain is BinaryExpression binary) chain = binary.Left;
        return chain;
    }

    /// <summary>An extension node whose every member throws once armed.</summary>
    private sealed class ArmedExtension : Expression
    {
        /// <summary>Gets or sets whether the members throw.</summary>
        public bool Armed { get; set; }

        /// <summary>Gets the number of calls made while armed.</summary>
        public int Calls { get; private set; }

        /// <summary>Counts a call and throws when armed.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value returned while not armed.</param>
        /// <returns><paramref name="value"/>.</returns>
        private T Hit<T>(T value)
        {
            if (!Armed) return value;
            Calls++;
            throw new InvalidOperationException("A member of an extension node was called.");
        }

        /// <inheritdoc/>
        public override ExpressionType NodeType => Hit(ExpressionType.Extension);

        /// <inheritdoc/>
        public override Type Type => Hit(typeof(double));

        /// <inheritdoc/>
        public override bool CanReduce => Hit(false);

        /// <inheritdoc/>
        public override string ToString() => Hit("extension");
    }

    /// <summary>A plain addition, impersonated as a user-defined operator method.</summary>
    /// <param name="x">The left operand.</param>
    /// <param name="y">The right operand.</param>
    /// <returns>The sum.</returns>
    public static double Plus(double x, double y) => x + y;

    /// <summary>
    /// The built-in simplifier admits its chains without the effect gate, so the admission must refuse by itself, with type
    /// tests only: an armed extension node among linear terms is never read, and an operator carrying an impersonating method
    /// (the root of a term, or a factor product) is never crossed nor inspected. Each of these chains is refused, and the
    /// same chain without the hostile node is admitted.
    /// </summary>
    [TestMethod]
    public void Admission_NeverReadsExtensionNodes_NorImpersonatingReflectionObjects()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a"), b = Expression.Parameter(typeof(double), "b"), c = Expression.Parameter(typeof(double), "c");
        ParameterExpression d = Expression.Parameter(typeof(double), "d");
        var extension = new ArmedExtension();
        var operatorMethod = new ImpersonatingMethod(typeof(ExpressionSimplifierFlattenResourceTests).GetMethod(nameof(Plus))!);
        Expression linear = Expression.Subtract(Expression.Add(a, d), Expression.Multiply(Expression.Constant(3.0), b));
        Expression[] hostile =
        [
            Expression.Add(Expression.Subtract(Expression.Add(a, extension), Expression.Multiply(Expression.Constant(3.0), b)), c),
            Expression.Add(Expression.Add(extension, a), b),
            Expression.Add(linear, Expression.Add(c, c, operatorMethod)),
            Expression.Add(linear, Expression.Multiply(Expression.Constant(5.0), c, operatorMethod)),
        ];
        var simplifier = new ExpressionSimplifier();
        Assert.AreEqual(ExpressionRuleRejection.None, simplifier.FlattenAdmission(linear));

        extension.Armed = true;
        operatorMethod.Trap.Reset();
        operatorMethod.Trap.Armed = true;
        try
        {
            foreach (Expression chain in hostile)
            {
                Assert.AreEqual(ExpressionRuleRejection.ChainNotAdmitted, simplifier.FlattenAdmission(chain));
            }
        }
        finally
        {
            // See ExpressionFlattenResourceTests: an armed impersonating method must not stay in the framework's caches.
            operatorMethod.Trap.Armed = false;
            extension.Armed = false;
        }

        Assert.AreEqual(0, extension.Calls);
        Assert.AreEqual(0, operatorMethod.Trap.ArmedCalls + operatorMethod.Trap.TypeReads);
    }

    /// <summary>
    /// A chain over a shared DAG (every level adds a node to itself: <c>2^n</c> logical terms) is refused by the admission in
    /// one visit per distinct node, at the second occurrence of its parameter, and is never extracted: the simplifier keeps
    /// its level by level treatment and its transformation budget stops it, as before the rule existed.
    /// </summary>
    [TestMethod]
    public void SharedDag_IsRefusedByTheAdmission_WithoutExtraction()
    {
        Expression dag = SharedDag(50);
        var simplifier = new ExpressionSimplifier(NoLimits with { MaxTransformVisits = 10_000 });

        long structural = ExpressionResourceBudget.Run(NoLimits, null, null, null, (simplifier, dag), static state =>
        {
            Assert.AreEqual(ExpressionRuleRejection.ChainNotAdmitted, state.simplifier.FlattenAdmission(state.dag));
            return ExpressionResourceBudget.StructuralVisits;
        });
        Assert.AreEqual(50, structural, "One visit per distinct node of the DAG.");

        ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => simplifier.Simplify(dag));
        Assert.AreEqual(ExpressionResourceLimitKind.Work, exception.Kind);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
    }


    /// <summary>
    /// Builds <c>-x0 + x1 + ... + x(n-1)</c>, left-associated, iteratively: every level holds the negation, a term that is not
    /// linear.
    /// </summary>
    /// <param name="terms">The number of terms.</param>
    /// <returns>The chain.</returns>
    private static BinaryExpression NegationFirstChain(int terms)
    {
        Expression e = Expression.Negate(Expression.Parameter(typeof(double), "x0"));
        for (int i = 1; i < terms; i++) e = Expression.Add(e, Expression.Parameter(typeof(double), "x" + i));
        return (BinaryExpression)e;
    }

    /// <summary>Returns the left spine of a left-associated chain, root first, iteratively.</summary>
    /// <param name="chain">The chain.</param>
    /// <returns>Its crossed nodes.</returns>
    private static List<BinaryExpression> LeftSpine(Expression chain)
    {
        var spine = new List<BinaryExpression>();
        while (chain is BinaryExpression { NodeType: ExpressionType.Add or ExpressionType.Subtract } binary)
        {
            spine.Add(binary);
            chain = binary.Left;
        }

        return spine;
    }

    /// <summary>
    /// The known refusals of the admission (Flatten mission C1) are counted work: the root walk, one visit per recorded node,
    /// one per later lookup. A structural budget just below that count refuses inside the recording pass, the operation
    /// closes, the nodes recorded before the refusal are proven refusals, and the next operation starts without any table.
    /// </summary>
    [TestMethod]
    public void KnownRefusals_AreCounted_AndARefusalLeavesACorrectTable()
    {
        const int Terms = 64;
        BinaryExpression chain = NegationFirstChain(Terms);
        List<BinaryExpression> spine = LeftSpine(chain);
        var simplifier = new ExpressionSimplifier(NoLimits);

        long structural = ExpressionResourceBudget.Run(NoLimits, null, null, null, (simplifier, spine), static state =>
        {
            foreach (BinaryExpression node in state.spine) Assert.AreEqual(ExpressionRuleRejection.ChainNotAdmitted, state.simplifier.FlattenAdmission(node));
            return ExpressionResourceBudget.StructuralVisits;
        });
        Assert.AreEqual((Terms - 1) + (Terms - 2) + (Terms - 2), structural, "Root walk, recording pass, one visit per lookup.");

        // A nested operation whose budget runs out in the middle of the recording pass: the outer operation keeps the table.
        ExpressionResourceBudget.Run(NoLimits, null, null, null, (simplifier, spine), static state =>
        {
            ExpressionResourceLimits tight = NoLimits with { MaxStructuralVisits = (Terms - 1) + 10 };
            ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(
                () => ExpressionResourceBudget.Run(tight, null, null, null, state, static inner => inner.simplifier.FlattenAdmission(inner.spine[0])));
            Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, exception.Kind);

            ExpressionNodeSet table = ExpressionResourceBudget.ActiveSession!.FlattenRefusals!;
            Assert.IsTrue(table.Count is > 0 and <= 10, $"{table.Count} recorded nodes");
            for (int i = 1; i < state.spine.Count; i++)
            {
                Assert.AreEqual(i <= table.Count, table.Contains(state.spine[i]), $"spine node {i}");
            }

            return 0;
        });

        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        ExpressionResourceBudget.Run(NoLimits, null, null, null, 0, static _ =>
        {
            Assert.IsNull(ExpressionResourceBudget.ActiveSession!.FlattenRefusals);
            return 0;
        });

        // The whole simplification is still refused just above its structural budget, and the instance works again.
        (_, long simplification) = Count(new ExpressionSimplifier(NoLimits), chain);
        Assert.IsNotNull(new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = simplification }).Simplify(chain));
        var limited = new ExpressionSimplifier(NoLimits with { MaxStructuralVisits = simplification - 1 });
        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => limited.Simplify(chain)).Kind);
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.AreEqual(8, CountGroups(limited.Simplify(LinearChain(8, 2))));
    }

    /// <summary>
    /// The walk and the recording pass are iterative: on a 256 KiB thread, the admission of a 100,000-term chain refused by
    /// its deepest term records the whole spine, and every node below the root is then refused by one lookup.
    /// </summary>
    [TestMethod]
    public void DeepRefusedChain_IsRecorded_OnASmallStack()
    {
        const int Terms = 100_000;
        BinaryExpression chain = NegationFirstChain(Terms);
        List<BinaryExpression> spine = LeftSpine(chain);
        RunOnThread(() =>
        {
            var simplifier = new ExpressionSimplifier(NoLimits);
            long structural = ExpressionResourceBudget.Run(NoLimits, null, null, null, (simplifier, spine), static state =>
            {
                foreach (BinaryExpression node in state.spine) Assert.AreEqual(ExpressionRuleRejection.ChainNotAdmitted, state.simplifier.FlattenAdmission(node));
                return ExpressionResourceBudget.StructuralVisits;
            });
            Assert.AreEqual((Terms - 1) + 2L * (Terms - 2), structural);
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        }, SmallStack, Timeout);
    }

    /// <summary>A value whose equality members count their calls: user code the admission must never run.</summary>
    private sealed class CountingValue
    {
        /// <summary>Gets the number of calls of <see cref="Equals(object?)"/> and <see cref="GetHashCode"/>.</summary>
        public int Calls { get; private set; }

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
            return 1;
        }
    }

    /// <summary>Turns a <see cref="CountingValue"/> into a number.</summary>
    /// <param name="value">The value.</param>
    /// <returns>Always two.</returns>
    public static double Carrier(object value) => 2;

    /// <summary>
    /// The walk and the recording pass of a refusal never read a hostile node: an armed extension node at the bottom of a
    /// long chain, an operator carrying an impersonating method in its middle, and a constant whose equality members count
    /// their calls, each refuse the chain and the levels above them, without any member call.
    /// </summary>
    [TestMethod]
    public void Recording_NeverReadsHostileNodes()
    {
        var extension = new ArmedExtension();
        var operatorMethod = new ImpersonatingMethod(typeof(ExpressionSimplifierFlattenResourceTests).GetMethod(nameof(Plus))!);
        var value = new CountingValue();
        ParameterExpression[] x = [.. Enumerable.Range(0, 40).Select(static i => Expression.Parameter(typeof(double), "x" + i))];

        Expression withExtension = extension;
        for (int i = 0; i < 30; i++) withExtension = Expression.Add(withExtension, x[i]);
        Expression withOperator = x[0];
        for (int i = 1; i < 30; i++) withOperator = Expression.Add(withOperator, i == 15 ? Expression.Add(x[i], x[i + 10], operatorMethod) : x[i]);
        Expression withConstant = x[0];
        for (int i = 1; i < 30; i++)
        {
            withConstant = Expression.Add(withConstant, i == 10
                ? Expression.Negate(Expression.Call(typeof(ExpressionSimplifierFlattenResourceTests).GetMethod(nameof(Carrier))!, Expression.Constant(value, typeof(object))))
                : x[i]);
        }

        var simplifier = new ExpressionSimplifier();
        extension.Armed = true;
        operatorMethod.Trap.Reset();
        operatorMethod.Trap.Armed = true;
        try
        {
            foreach (Expression chain in new[] { withExtension, withOperator, withConstant })
            {
                List<BinaryExpression> spine = LeftSpine(chain);
                int recorded = ExpressionResourceBudget.Run(NoLimits, null, null, null, (simplifier, spine), static state =>
                {
                    Assert.AreEqual(ExpressionRuleRejection.ChainNotAdmitted, state.simplifier.FlattenAdmission(state.spine[0]));
                    return ExpressionResourceBudget.ActiveSession!.FlattenRefusals?.Count ?? 0;
                });
                Assert.IsTrue(recorded > 0);
            }
        }
        finally
        {
            // See ExpressionFlattenResourceTests: an armed impersonating method must not stay in the framework's caches.
            operatorMethod.Trap.Armed = false;
            extension.Armed = false;
        }

        Assert.AreEqual(0, extension.Calls);
        Assert.AreEqual(0, operatorMethod.Trap.ArmedCalls + operatorMethod.Trap.TypeReads);
        Assert.AreEqual(0, value.Calls);
    }

    /// <summary>
    /// The tables belong to the session of each thread: one simplifier instance used by several threads at once on chains
    /// refused deep inside gives, on every thread, the result of a sequential simplification.
    /// </summary>
    [TestMethod]
    public void KnownRefusals_AreIsolatedBetweenThreads()
    {
        BinaryExpression[] chains = [NegationFirstChain(48), NegationFirstChain(64), (BinaryExpression)Expression.Add(LinearChain(40, 0), Expression.Parameter(typeof(double), "y"))];
        var simplifier = new ExpressionSimplifier();
        string[] expected = [.. chains.Select(chain => simplifier.Simplify(chain).ToString())];
        var failures = new System.Collections.Concurrent.ConcurrentQueue<string>();
        System.Threading.Tasks.Parallel.For(0, 64, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, iteration =>
        {
            int index = iteration % chains.Length;
            string actual = simplifier.Simplify(chains[index]).ToString();
            if (actual != expected[index]) failures.Enqueue($"{iteration}: {actual}");
        });

        Assert.IsTrue(failures.IsEmpty, string.Join(Environment.NewLine, failures));
    }

    /// <summary>A function unknown to the effect analysis.</summary>
    /// <returns>The number of calls so far.</returns>
    public static double Unknown() => ++s_unknownCalls;

    /// <summary>The number of calls of <see cref="Unknown"/>.</summary>
    private static int s_unknownCalls;

    /// <summary>
    /// The rule never accepts a chain whose evaluation is observable, under any policy: the compiled result of a chain mixing
    /// linear terms, unknown calls, a zero and a <c>NaN</c> evaluates every call of the source, the same number of times.
    /// </summary>
    [TestMethod]
    public void ObservableEvaluations_AreKept_UnderEveryPolicy()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a"), b = Expression.Parameter(typeof(double), "b");
        Expression Call() => Expression.Call(typeof(ExpressionSimplifierFlattenResourceTests).GetMethod(nameof(Unknown))!);
        Expression[] sources =
        [
            Expression.Subtract(Expression.Add(Expression.Add(a, Call()), b), Call()),
            Expression.Add(Expression.Add(Call(), Expression.Constant(0.0)), Expression.Multiply(Expression.Constant(3.0), a)),
            Expression.Add(Expression.Add(Expression.Constant(double.NaN), Call()), Expression.Add(a, b)),
            Expression.Add(Expression.Add(a, b), Expression.Negate(Call())),
        ];
        ExpressionSimplifierOptions[] policies =
        [
            ExpressionSimplifierOptions.Default,
            new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
            new() { AggressiveSimplification = true },
            new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations, AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
        ];

        foreach (Expression source in sources)
        {
            s_unknownCalls = 0;
            Expression.Lambda<Func<double, double, double>>(source, a, b).Compile()(1, 2);
            int expected = s_unknownCalls;
            Assert.IsTrue(expected > 0);

            foreach (ExpressionSimplifierOptions policy in policies)
            {
                var simplified = (Expression<Func<double, double, double>>)new ExpressionSimplifier { Options = policy }.Simplify(Expression.Lambda<Func<double, double, double>>(source, a, b));
                s_unknownCalls = 0;
                simplified.Compile()(1, 2);
                Assert.AreEqual(expected, s_unknownCalls, $"{source} under {policy}");
            }
        }
    }
}
