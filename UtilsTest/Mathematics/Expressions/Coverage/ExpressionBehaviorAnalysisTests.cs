using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// S5 P18 Architecture A: classification of determinism, mutable-state reads, side effects, exception origins and analysis
/// uncertainty by <see cref="ExpressionBehaviorAnalysis"/>, the trusted numeric functions, the <see cref="DeterministicAttribute"/>
/// declarations, lambdas and captures, hostile inputs, and the absence of any change to the simplifier.
/// </summary>
[TestClass]
[TestCategory("S5-P18A")]
public class ExpressionBehaviorAnalysisTests
{
    /// <summary>A free parameter of type <see cref="double"/>.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>A free parameter of type <see cref="int"/>.</summary>
    private static readonly ParameterExpression I = Expression.Parameter(typeof(int), "i");

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Classifies <paramref name="e"/>.</summary>
    /// <param name="e">The expression.</param>
    /// <returns>Its flags.</returns>
    private static ExpressionBehaviors Flags(Expression e) => ExpressionBehaviorAnalysis.Analyze(e).Flags;

    /// <summary>Gets a static method by declaring type, name and parameter types.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="name">The name.</param>
    /// <param name="parameters">The parameter types.</param>
    /// <returns>The method.</returns>
    private static MethodInfo Static(Type type, string name, params Type[] parameters)
        => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, parameters) ?? throw new MissingMethodException(type.Name, name);

    /// <summary>Builds a call to <see cref="BehaviorMethods"/>.</summary>
    /// <param name="name">The method name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The call.</returns>
    private static Expression Call(string name, params Expression[] arguments) => Expression.Call(BehaviorMethods.Method(name), arguments);

    // ---------------------------------------------------------------- trusted numeric functions

    /// <summary>Total built-in numeric functions are deterministic, free of effects and exceptions; with a constant argument they are reproducible.</summary>
    [TestMethod]
    public void TotalNumericFunctions_AreInert()
    {
        MethodInfo[] total =
        [
            Static(typeof(Math), nameof(Math.Sin), typeof(double)),
            Static(typeof(double), nameof(double.Sin), typeof(double)),
            Static(typeof(MathF), nameof(MathF.Cos), typeof(float)),
            Static(typeof(Math), nameof(Math.Round), typeof(double)),
            Static(typeof(Math), nameof(Math.Max), typeof(int), typeof(int)),
            Static(typeof(Math), nameof(Math.ScaleB), typeof(double), typeof(int)),
            Static(typeof(double), nameof(double.IsNaN), typeof(double)),
            Static(typeof(Half), nameof(Half.Sqrt), typeof(Half)),
            Static(typeof(decimal), nameof(decimal.Floor), typeof(decimal)),
            Static(typeof(BigInteger), nameof(BigInteger.Add), typeof(BigInteger), typeof(BigInteger)),
            Static(typeof(int), nameof(int.PopCount), typeof(int)),
        ];

        foreach (MethodInfo method in total)
        {
            Assert.AreEqual(ExpressionBehaviors.None, ExpressionBehaviorAnalysis.AnalyzeMethod(method).Flags, method.ToString());
        }

        ExpressionBehavior constant = ExpressionBehaviorAnalysis.Analyze(Expression.Call(total[0], Expression.Constant(1.0)));
        Assert.IsTrue(constant.IsReproducible);
        Assert.IsFalse(constant.ReliesOnDeclarations);

        ExpressionBehavior free = ExpressionBehaviorAnalysis.Analyze(Expression.Call(total[0], X));
        Assert.AreEqual(ExpressionBehaviors.ReadsMutableState, free.Flags, "a deterministic call of a free parameter reads mutable state");
        Assert.IsTrue(free.IsDeterministic);
        Assert.IsFalse(free.IsReproducible);
    }

    /// <summary>Partial built-in numeric functions are deterministic and free of effects but may throw an arithmetic exception.</summary>
    [TestMethod]
    public void PartialNumericFunctions_MayThrowArithmetic()
    {
        MethodInfo[] partial =
        [
            Static(typeof(Math), nameof(Math.Sign), typeof(double)),
            Static(typeof(Math), nameof(Math.Clamp), typeof(double), typeof(double), typeof(double)),
            Static(typeof(Math), nameof(Math.Round), typeof(double), typeof(int)),
            Static(typeof(Math), nameof(Math.Round), typeof(double), typeof(MidpointRounding)),
            Static(typeof(Math), nameof(Math.Abs), typeof(int)),
            Static(typeof(decimal), nameof(decimal.Add), typeof(decimal), typeof(decimal)),
            Static(typeof(decimal), "op_Division", typeof(decimal), typeof(decimal)),
            Static(typeof(int), nameof(int.Log2), typeof(int)),
            Static(typeof(BigInteger), nameof(BigInteger.Pow), typeof(BigInteger), typeof(int)),
            Static(typeof(Int128), "op_CheckedAddition", typeof(Int128), typeof(Int128)),
        ];

        foreach (MethodInfo method in partial)
        {
            ExpressionBehavior behavior = ExpressionBehaviorAnalysis.AnalyzeMethod(method);
            Assert.AreEqual(ExpressionBehaviors.MayThrowArithmetic, behavior.Flags, method.ToString());
            Assert.IsTrue(behavior.IsDeterministic && behavior.IsSideEffectFree && behavior.IsFullyAnalyzed);
        }
    }

    /// <summary>
    /// Every trusted function of the running runtime is declared by a trusted type, static and non-generic, and every function
    /// classified as total throws on none of the edge values of its signature (zeros, ones, extremes, NaN, infinities).
    /// </summary>
    [TestMethod]
    public void TrustedFunctions_TotalOnesNeverThrowOnEdgeValues()
    {
        var trusted = ExpressionMethodBehaviors.EnumerateTrusted().ToList();
        Assert.IsTrue(trusted.Count > 300, $"only {trusted.Count} trusted functions");

        var failures = new List<string>();
        foreach ((MethodInfo method, ExpressionBehaviors behaviors) in trusted)
        {
            Assert.IsTrue(method.IsStatic && !method.IsGenericMethod && ExpressionMethodBehaviors.TrustedDeclaringTypes.Contains(method.DeclaringType!), method.ToString());
            Assert.IsTrue(behaviors is ExpressionBehaviors.None or ExpressionBehaviors.MayThrowArithmetic, method.ToString());
            if (behaviors != ExpressionBehaviors.None) continue;

            foreach (object?[] arguments in EdgeArguments(method.GetParameters()))
            {
                try
                {
                    method.Invoke(null, arguments);
                }
                catch (TargetInvocationException exception)
                {
                    failures.Add($"{method.DeclaringType!.Name}.{method}: {exception.InnerException!.GetType().Name} on ({string.Join(", ", arguments)})");
                    break;
                }
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    /// <summary>Enumerates every combination of the edge values of the parameter types, capped per method.</summary>
    /// <param name="parameters">The parameters.</param>
    /// <returns>The argument lists.</returns>
    private static IEnumerable<object?[]> EdgeArguments(ParameterInfo[] parameters)
    {
        object[][] samples = parameters.Select(parameter => EdgeValues(parameter.ParameterType)).ToArray();
        int[] indexes = new int[samples.Length];
        for (int produced = 0; produced < 4096; produced++)
        {
            yield return samples.Select((values, i) => (object?)values[indexes[i]]).ToArray();
            int position = 0;
            while (position < indexes.Length && ++indexes[position] == samples[position].Length)
            {
                indexes[position] = 0;
                position++;
            }

            if (position == indexes.Length) yield break;
        }
    }

    /// <summary>Returns the edge values of a numeric signature type.</summary>
    /// <param name="type">The type.</param>
    /// <returns>Its edge values.</returns>
    private static object[] EdgeValues(Type type)
    {
        if (type == typeof(double)) return [0.0, -0.0, 1.0, -1.0, 0.5, -7.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue, double.MinValue, double.Epsilon];
        if (type == typeof(float)) return [0f, -0f, 1f, -1f, 0.5f, -7f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue, float.MinValue, float.Epsilon];
        if (type == typeof(Half)) return [(Half)0, (Half)1, (Half)(-1), (Half)0.5, Half.NaN, Half.PositiveInfinity, Half.NegativeInfinity, Half.MaxValue, Half.MinValue, Half.Epsilon];
        if (type == typeof(decimal)) return [0m, 1m, -1m, 0.5m, 7m, decimal.MaxValue, decimal.MinValue, 0.0000000000000000000000000001m];
        if (type == typeof(bool)) return [false, true];
        if (type == typeof(MidpointRounding)) return [MidpointRounding.ToEven, MidpointRounding.AwayFromZero, (MidpointRounding)99];
        if (type == typeof(BigInteger)) return [BigInteger.Zero, BigInteger.One, BigInteger.MinusOne, new BigInteger(long.MinValue), BigInteger.Pow(10, 40), -BigInteger.Pow(10, 40)];

        var values = new List<object>();
        foreach (string name in new[] { "MinValue", "MaxValue" })
        {
            object? extreme = type.GetField(name)?.GetValue(null) ?? type.GetProperty(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (extreme is not null) values.Add(extreme);
        }

        MethodInfo fromLong = type.GetMethod("CreateTruncating", BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(typeof(long));
        foreach (long value in new long[] { 0, 1, -1, 2, 7, 63, 64, 200, -200 })
        {
            values.Add(fromLong.Invoke(null, [value])!);
        }

        return [.. values];
    }

    /// <summary>
    /// The name of a trusted function grants no trust on its own: a user method, a user extension method, a user
    /// implementation of static numeric interfaces and an interface's static abstract member are unknown code.
    /// </summary>
    [TestMethod]
    public void NameAlone_GrantsNoTrust()
    {
        MethodInfo[] untrusted =
        [
            BehaviorMethods.Method(nameof(BehaviorMethods.Sin)),
            BehaviorMethods.Method(nameof(BehaviorMethods.Sqrt)),
            typeof(UserNumber).GetMethod(nameof(UserNumber.Abs))!,
            typeof(UserNumber).GetMethod("op_Addition")!,
            typeof(IAdditionOperators<double, double, double>).GetMethod("op_Addition")!,
            typeof(INumberBase<double>).GetMethod("Abs")!,
            Static(typeof(double), nameof(double.ReciprocalEstimate), typeof(double)),
            Static(typeof(double), nameof(double.Parse), typeof(string)),
            Static(typeof(Math), nameof(Math.DivRem), typeof(int), typeof(int)),
        ];

        foreach (MethodInfo method in untrusted)
        {
            Assert.AreEqual(ExpressionBehaviors.UnknownCode, ExpressionBehaviorAnalysis.AnalyzeMethod(method).Flags, method.ToString());
        }

        ParameterExpression a = Expression.Parameter(typeof(UserNumber), "a");
        Assert.AreEqual(ExpressionBehaviors.UnknownCode, Flags(Expression.Add(a, a)), "a custom implementation of a static numeric interface is user code");
        Assert.AreEqual(ExpressionBehaviors.UnknownCode, Flags(Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.Sin)), X)));
    }

    // ---------------------------------------------------------------- declarations

    /// <summary>A declared method is deterministic and side-effect free, may throw user exceptions, and relies on its declaration.</summary>
    [TestMethod]
    public void DeclaredMethod_IsDeterministicAndReliesOnDeclaration()
    {
        ExpressionBehavior method = ExpressionBehaviorAnalysis.AnalyzeMethod(BehaviorMethods.Method(nameof(BehaviorMethods.Scale)));
        Assert.AreEqual(ExpressionBehaviors.ReliesOnDeclaration | ExpressionBehaviors.MayThrowUserCode, method.Flags);
        Assert.IsTrue(method.IsDeterministic && method.IsSideEffectFree && method.IsFullyAnalyzed && method.ReliesOnDeclarations && method.MayThrowUserCode);

        ExpressionBehavior constant = ExpressionBehaviorAnalysis.Analyze(Call(nameof(BehaviorMethods.Scale), Expression.Constant(2.0)));
        Assert.IsTrue(constant.IsReproducible && constant.ReliesOnDeclarations);

        ExpressionBehavior free = ExpressionBehaviorAnalysis.Analyze(Call(nameof(BehaviorMethods.Scale), X));
        Assert.AreEqual(ExpressionBehaviors.ReliesOnDeclaration | ExpressionBehaviors.MayThrowUserCode | ExpressionBehaviors.ReadsMutableState, free.Flags);

        // A closed generic method keeps the declaration of its definition.
        MethodInfo identity = BehaviorMethods.Method(nameof(BehaviorMethods.Identity)).MakeGenericMethod(typeof(double));
        Assert.AreEqual(ExpressionBehaviors.ReliesOnDeclaration | ExpressionBehaviors.MayThrowUserCode, ExpressionBehaviorAnalysis.AnalyzeMethod(identity).Flags);
    }

    /// <summary>
    /// A declaration is honored only when it is bound to the code that runs and speaks for all of it: not on an overridable
    /// virtual method, a method taking a delegate or a reference, nor through <c>[Pure]</c> or an undeclared method.
    /// </summary>
    [TestMethod]
    public void Declaration_IsIgnoredWhenNotBoundToTheCodeThatRuns()
    {
        MethodInfo[] unknown =
        [
            BehaviorMethods.Method(nameof(BehaviorMethods.Unannotated)),
            BehaviorMethods.Method(nameof(BehaviorMethods.PureOnly)),
            BehaviorMethods.Method(nameof(BehaviorMethods.Apply)),
            BehaviorMethods.Method(nameof(BehaviorMethods.ByRef)),
            typeof(BehaviorBase).GetMethod(nameof(BehaviorBase.Virtual))!,
            typeof(Money).GetMethod("op_Subtraction")!,
            typeof(Money).GetMethod("op_Explicit")!,
        ];

        foreach (MethodInfo method in unknown)
        {
            Assert.AreEqual(ExpressionBehaviors.UnknownCode, ExpressionBehaviorAnalysis.AnalyzeMethod(method).Flags, method.ToString());
        }

        // A sealed override and a non-virtual method of a non-sealed class are bound to the code that runs.
        const ExpressionBehaviors instance = ExpressionBehaviors.ReliesOnDeclaration | ExpressionBehaviors.MayThrowUserCode | ExpressionBehaviors.ReadsMutableState;
        Assert.AreEqual(instance, ExpressionBehaviorAnalysis.AnalyzeMethod(typeof(BehaviorDerived).GetMethod(nameof(BehaviorDerived.Virtual))!).Flags);
        Assert.AreEqual(instance, ExpressionBehaviorAnalysis.AnalyzeMethod(typeof(BehaviorBase).GetMethod(nameof(BehaviorBase.NonVirtual))!).Flags);
    }

    /// <summary>
    /// A declared method reads the state of the objects it receives (an array, a reference receiver) and allocates when it
    /// returns a reference; a user value type holding only immutable values references no mutable state.
    /// </summary>
    [TestMethod]
    public void DeclaredMethod_StateAndAllocationFollowTheSignature()
    {
        Assert.AreEqual(
            ExpressionBehaviors.ReliesOnDeclaration | ExpressionBehaviors.MayThrowUserCode | ExpressionBehaviors.ReadsMutableState,
            ExpressionBehaviorAnalysis.AnalyzeMethod(BehaviorMethods.Method(nameof(BehaviorMethods.First))).Flags);
        Assert.AreEqual(
            ExpressionBehaviors.ReliesOnDeclaration | ExpressionBehaviors.MayThrowUserCode | ExpressionBehaviors.Allocates,
            ExpressionBehaviorAnalysis.AnalyzeMethod(BehaviorMethods.Method(nameof(BehaviorMethods.Text))).Flags);

        ParameterExpression money = Expression.Parameter(typeof(Money), "m");
        ExpressionBehavior sum = ExpressionBehaviorAnalysis.Analyze(Expression.Add(money, Expression.Constant(new Money(1m))));
        Assert.AreEqual(ExpressionBehaviors.ReliesOnDeclaration | ExpressionBehaviors.MayThrowUserCode | ExpressionBehaviors.ReadsMutableState, sum.Flags, "a declared custom operator");
        Assert.AreEqual(ExpressionBehaviors.UnknownCode, Flags(Expression.Subtract(money, money)), "an undeclared custom operator");
        Assert.AreEqual(
            ExpressionBehaviors.ReliesOnDeclaration | ExpressionBehaviors.MayThrowUserCode,
            Flags(Expression.Convert(Expression.Constant(1m), typeof(Money))),
            "a declared custom conversion");
    }

    // ---------------------------------------------------------------- members, nondeterminism, unknown code

    /// <summary>
    /// Fields read mutable state unless read-only or constant; a reference receiver may be null; a declared getter is
    /// honored, an undeclared one is unknown code.
    /// </summary>
    [TestMethod]
    public void Members_FieldsAndGetters()
    {
        ParameterExpression cell = Expression.Parameter(typeof(BehaviorCell), "cell");
        const ExpressionBehaviors receiver = ExpressionBehaviors.ReadsMutableState | ExpressionBehaviors.MayThrowRuntime;

        Assert.AreEqual(receiver, Flags(Expression.Field(cell, nameof(BehaviorCell.Field))));
        Assert.AreEqual(receiver, Flags(Expression.Field(cell, nameof(BehaviorCell.Fixed))), "the receiver read and its null check remain");
        Assert.AreEqual(ExpressionBehaviors.None, Flags(Expression.Field(Expression.Constant(new BehaviorCell()), nameof(BehaviorCell.Fixed))));
        Assert.AreEqual(ExpressionBehaviors.ReadsMutableState, Flags(Expression.Field(Expression.Constant(new BehaviorCell()), nameof(BehaviorCell.Field))));
        Assert.AreEqual(ExpressionBehaviors.None, Flags(Expression.Field(null, typeof(BehaviorCell), nameof(BehaviorCell.StaticFixed))));
        Assert.AreEqual(ExpressionBehaviors.ReadsMutableState, Flags(Expression.Field(null, typeof(BehaviorCell), nameof(BehaviorCell.StaticMutable))));
        Assert.AreEqual(ExpressionBehaviors.None, Flags(Expression.Field(null, typeof(double), nameof(double.NaN))));

        Assert.AreEqual(
            ExpressionBehaviors.ReliesOnDeclaration | ExpressionBehaviors.MayThrowUserCode | receiver,
            Flags(Expression.Property(cell, nameof(BehaviorCell.Declared))));
        Assert.AreEqual(ExpressionBehaviors.UnknownCode, Flags(Expression.Property(cell, nameof(BehaviorCell.Undeclared))));
    }

    /// <summary>
    /// Clocks, fresh identifiers and random generators are known nondeterministic members: classified, not unknown; a
    /// random generator also advances its state.
    /// </summary>
    [TestMethod]
    public void KnownNondeterministicMembers()
    {
        ExpressionBehavior now = ExpressionBehaviorAnalysis.Analyze(Expression.Property(null, typeof(DateTime), nameof(DateTime.Now)));
        Assert.AreEqual(ExpressionBehaviors.Nondeterministic | ExpressionBehaviors.ReadsMutableState, now.Flags);
        Assert.IsTrue(now.IsFullyAnalyzed && now.IsSideEffectFree && !now.IsDeterministic);

        Assert.AreEqual(
            ExpressionBehaviors.Nondeterministic | ExpressionBehaviors.ReadsMutableState,
            Flags(Expression.Call(typeof(Guid).GetMethod(nameof(Guid.NewGuid))!)));

        Expression random = Expression.Call(Expression.Constant(new Random(1)), typeof(Random).GetMethod(nameof(Random.NextDouble))!);
        ExpressionBehavior next = ExpressionBehaviorAnalysis.Analyze(random);
        Assert.IsTrue(next.IsFullyAnalyzed, "a known member is classified, not unknown");
        Assert.IsFalse(next.IsDeterministic);
        Assert.IsFalse(next.IsSideEffectFree, "the generator advances its state");

        // Random.Shared is neither listed nor declared: its getter is unknown code.
        Expression shared = Expression.Call(Expression.Property(null, typeof(Random), nameof(Random.Shared)), typeof(Random).GetMethod(nameof(Random.NextDouble))!);
        Assert.AreEqual(ExpressionBehaviors.UnknownCode, Flags(shared));
    }

    /// <summary>The node families the analysis does not model are unknown code, as are constructors and delegate invocations.</summary>
    [TestMethod]
    public void UnsupportedFamilies_AreUnknownCode()
    {
        LabelTarget exit = Expression.Label(typeof(double));
        Expression[] unknown =
        [
            Expression.Loop(Expression.Break(exit, X), exit),
            Expression.TryCatch(X, Expression.Catch(typeof(Exception), Expression.Constant(0.0))),
            Expression.Switch(I, Expression.Constant(0.0), Expression.SwitchCase(Expression.Constant(1.0), Expression.Constant(1))),
            Expression.MemberInit(Expression.New(typeof(BehaviorCell)), Expression.Bind(typeof(BehaviorCell).GetField(nameof(BehaviorCell.Field))!, X)),
            Expression.New(typeof(BehaviorCell)),
            Expression.Invoke(Expression.Parameter(typeof(Func<double>), "f")),
            Expression.Dynamic(new R4CountingBinder("p18a", throwOnBind: true), typeof(object), X),
            new R4HostileExtension(X),
        ];

        foreach (Expression e in unknown)
        {
            Assert.AreEqual(ExpressionBehaviors.UnknownCode, Flags(e), e.GetType().Name);
        }

        Assert.AreEqual(ExpressionBehaviors.None, Flags(Expression.New(typeof(decimal))), "a value type without constructor is its default");
    }

    /// <summary>
    /// An extension node is recognized by its class only: none of its members is called, alone or as the operand of a
    /// conversion, a call argument, a receiver or a lambda body.
    /// </summary>
    [TestMethod]
    public void HostileExtension_IsNeverRead()
    {
        var hostile = new R4HostileExtension(X);
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Expression[] trees =
        [
            hostile,
            Expression.Convert(hostile, typeof(int)),
            Expression.Call(Static(typeof(Math), nameof(Math.Sin), typeof(double)), hostile),
            Expression.Add(hostile, X),
            Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Add(hostile, y), y), X),
            Expression.Block([y], Expression.Assign(y, hostile), Expression.Lambda<Func<double>>(hostile), y),
        ];

        // The framework factories read the node's type while building the trees; only the analysis is under test.
        hostile.ResetCalls();
        hostile.Armed = true;
        foreach (Expression tree in trees)
        {
            Assert.AreEqual(ExpressionBehaviors.UnknownCode, Flags(tree) & ExpressionBehaviors.UnknownCode);
        }

        LambdaExpression lambda = Expression.Lambda<Func<double, double>>(trees[3], X);
        Assert.IsFalse(ExpressionBehaviorAnalysis.AnalyzeInvocation(lambda).IsFullyAnalyzed);
        Assert.AreEqual(0, hostile.TotalCalls, string.Join(", ", hostile.Calls.Keys));
    }

    // ---------------------------------------------------------------- intrinsic operators and exceptions

    /// <summary>Intrinsic arithmetic: integral division, checked arithmetic and conversions may throw; floating-point never does.</summary>
    [TestMethod]
    public void IntrinsicOperators_ExceptionOrigins()
    {
        const ExpressionBehaviors reads = ExpressionBehaviors.ReadsMutableState;
        const ExpressionBehaviors arithmetic = ExpressionBehaviors.MayThrowArithmetic;
        const ExpressionBehaviors runtime = ExpressionBehaviors.MayThrowRuntime;

        Assert.AreEqual(reads | arithmetic, Flags(Expression.Divide(Expression.Constant(1), I)));
        Assert.AreEqual(reads, Flags(Expression.Divide(I, Expression.Constant(2))));
        Assert.AreEqual(reads | arithmetic, Flags(Expression.Divide(I, Expression.Constant(-1))), "MinValue / -1 overflows");
        Assert.AreEqual(reads, Flags(Expression.Divide(X, X)), "IEEE division never throws");
        Assert.AreEqual(reads | arithmetic, Flags(Expression.AddChecked(I, I)));
        Assert.AreEqual(reads, Flags(Expression.AddChecked(X, X)), "checked floating-point arithmetic never throws");
        Assert.AreEqual(reads | arithmetic, Flags(Expression.NegateChecked(I)));
        Assert.AreEqual(reads, Flags(Expression.Negate(I)), "unchecked negation wraps");
        Assert.AreEqual(reads | arithmetic, Flags(Expression.Add(Expression.Convert(I, typeof(decimal)), Expression.Constant(1m))), "decimal addition overflows");

        Assert.AreEqual(reads, Flags(Expression.Convert(I, typeof(double))));
        Assert.AreEqual(reads, Flags(Expression.Convert(X, typeof(int))), "unchecked conversions never throw");
        Assert.AreEqual(reads | arithmetic, Flags(Expression.ConvertChecked(X, typeof(int))));
        Assert.AreEqual(reads, Flags(Expression.ConvertChecked(I, typeof(double))));
        Assert.AreEqual(reads | ExpressionBehaviors.Allocates, Flags(Expression.Convert(X, typeof(object))), "boxing");
        Assert.AreEqual(runtime, Flags(Expression.Convert(Expression.Constant(1.0, typeof(object)), typeof(double))), "unboxing");
        Assert.AreEqual(runtime, Flags(Expression.Convert(Expression.Constant(null, typeof(int?)), typeof(int))), "nullable unwrapping");
        Assert.AreEqual(ExpressionBehaviors.None, Flags(Expression.Convert(Expression.Constant(1), typeof(int?))), "nullable wrapping");
        Assert.AreEqual(runtime, Flags(Expression.Convert(Expression.Constant("a", typeof(object)), typeof(string))), "a reference downcast");
        Assert.AreEqual(arithmetic, Flags(Expression.Convert(Expression.Constant(1m), typeof(int))), "decimal explicit conversion");
        Assert.AreEqual(ExpressionBehaviors.None, Flags(Expression.Convert(Expression.Constant(1), typeof(decimal))), "decimal implicit conversion");

        ParameterExpression array = Expression.Parameter(typeof(double[]), "a");
        Assert.AreEqual(reads | runtime, Flags(Expression.ArrayIndex(array, Expression.Constant(0))), "an element is mutable state");
        Assert.AreEqual(reads | runtime, Flags(Expression.ArrayLength(array)));
        Assert.AreEqual(ExpressionBehaviors.MayThrowUserCode, Flags(Expression.Throw(Expression.Constant(new InvalidOperationException()), typeof(double))));
        Assert.AreEqual(ExpressionBehaviors.Allocates, Flags(Expression.NewArrayInit(typeof(double), Expression.Constant(1.0))));
        Assert.AreEqual(reads | ExpressionBehaviors.Allocates | runtime, Flags(Expression.NewArrayBounds(typeof(double), I)));
    }

    /// <summary>Conditional branches and short-circuit operators contribute the union of what may run.</summary>
    [TestMethod]
    public void Conditionals_UnionOfBranches()
    {
        Expression sign = Expression.Convert(Expression.Call(Static(typeof(Math), nameof(Math.Sign), typeof(double)), X), typeof(double));
        Expression sqrt = Expression.Call(Static(typeof(Math), nameof(Math.Sqrt), typeof(double)), X);
        Expression conditional = Expression.Condition(Expression.GreaterThan(X, Expression.Constant(0.0)), sqrt, sign);
        Assert.AreEqual(ExpressionBehaviors.ReadsMutableState | ExpressionBehaviors.MayThrowArithmetic, Flags(conditional));

        Expression guarded = Expression.AndAlso(Expression.NotEqual(I, Expression.Constant(0)), Expression.Equal(Expression.Divide(Expression.Constant(1), I), Expression.Constant(1)));
        Assert.AreEqual(ExpressionBehaviors.ReadsMutableState | ExpressionBehaviors.MayThrowArithmetic, Flags(guarded), "a may-analysis keeps the guarded branch");
    }

    // ---------------------------------------------------------------- scopes, lambdas, captures

    /// <summary>
    /// Writes to free variables are side effects; block variables and the parameters of the analyzed lambda are local
    /// (reading them reads no outside state, writing them is no side effect), unless passed by reference.
    /// </summary>
    [TestMethod]
    public void Scopes_LocalAndFreeWrites()
    {
        ParameterExpression v = Expression.Variable(typeof(int), "v");
        Assert.AreEqual(ExpressionBehaviors.SideEffects, Flags(Expression.Assign(I, Expression.Constant(1))));
        Assert.AreEqual(ExpressionBehaviors.SideEffects | ExpressionBehaviors.ReadsMutableState, Flags(Expression.PostIncrementAssign(I)));

        Expression block = Expression.Block([v], Expression.Assign(v, Expression.Constant(1)), Expression.AddAssignChecked(v, Expression.Constant(2)), v);
        Assert.AreEqual(ExpressionBehaviors.MayThrowArithmetic, Flags(block), "writes to a block variable are local; the checked addition may overflow");

        LambdaExpression local = Expression.Lambda<Func<int, int>>(Expression.Block(Expression.PreIncrementAssign(I), I), I);
        Assert.AreEqual(ExpressionBehaviors.None, ExpressionBehaviorAnalysis.AnalyzeInvocation(local).Flags, "a parameter of the analyzed lambda is local");
        Assert.AreEqual(ExpressionBehaviors.Allocates, Flags(local), "evaluating a lambda only allocates its delegate");

        ParameterExpression byRef = Expression.Parameter(typeof(int).MakeByRefType(), "r");
        LambdaExpression writesByRef = Expression.Lambda(Expression.Assign(byRef, Expression.Constant(1)), byRef);
        Assert.AreEqual(ExpressionBehaviors.SideEffects, ExpressionBehaviorAnalysis.AnalyzeInvocation(writesByRef).Flags, "a by-reference parameter writes the caller's location");

        // Shadowing: the inner declaration of v ends with its block, the outer one is still local.
        Expression shadowing = Expression.Block([v], Expression.Block([v], Expression.Assign(v, Expression.Constant(1))), Expression.Assign(v, Expression.Constant(2)));
        Assert.AreEqual(ExpressionBehaviors.None, Flags(shadowing));
    }

    /// <summary>
    /// A closure (or quoted tree) that escapes the evaluation captures the locals it references: a write to a captured local
    /// is a side effect, a write to an uncaptured one is not.
    /// </summary>
    [TestMethod]
    public void Captures_EscapingClosures()
    {
        ParameterExpression v = Expression.Variable(typeof(double), "v");
        ParameterExpression f = Expression.Variable(typeof(Func<double>), "f");

        Expression captured = Expression.Block([v, f], Expression.Assign(f, Expression.Lambda<Func<double>>(v)), Expression.Assign(v, Expression.Constant(2.0)), v);
        Assert.AreEqual(ExpressionBehaviors.SideEffects | ExpressionBehaviors.Allocates, Flags(captured));

        Expression notCaptured = Expression.Block([v, f], Expression.Assign(f, Expression.Lambda<Func<double>>(Expression.Constant(1.0))), Expression.Assign(v, Expression.Constant(2.0)), v);
        Assert.AreEqual(ExpressionBehaviors.Allocates, Flags(notCaptured));

        Expression quoted = Expression.Block([v], Expression.Quote(Expression.Lambda<Func<double>>(v)), Expression.Assign(v, Expression.Constant(2.0)), v);
        Assert.AreEqual(ExpressionBehaviors.SideEffects | ExpressionBehaviors.Allocates, Flags(quoted), "a quoted tree captures like a closure");

        // A closure holding code the analysis cannot see may capture any local.
        LabelTarget exit = Expression.Label(typeof(double));
        Expression opaque = Expression.Block([v, f], Expression.Assign(f, Expression.Lambda<Func<double>>(Expression.Loop(Expression.Break(exit, Expression.Constant(1.0)), exit))), Expression.Assign(v, Expression.Constant(2.0)), v);
        Assert.AreEqual(ExpressionBehaviors.SideEffects | ExpressionBehaviors.Allocates, Flags(opaque));

        // Compiler-generated closures read their captured variables through fields of a display object: mutable state.
        double capturedValue = 1.0;
        Expression<Func<double>> compiled = () => capturedValue + 1.0;
        capturedValue = 2.0;
        Assert.AreEqual(ExpressionBehaviors.ReadsMutableState, ExpressionBehaviorAnalysis.AnalyzeInvocation(compiled).Flags);
    }

    /// <summary>
    /// A lambda invoked in place runs its body with its parameters bound; nested invoked lambdas are analyzed recursively;
    /// a lambda passed as a value is not run; a conversion lambda runs its body.
    /// </summary>
    [TestMethod]
    public void Lambdas_InvokedNestedAndPassed()
    {
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        MethodInfo sin = Static(typeof(Math), nameof(Math.Sin), typeof(double));

        Expression nested = Expression.Invoke(
            Expression.Lambda<Func<double, double>>(Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Call(sin, z), z), y), y),
            X);
        Assert.AreEqual(ExpressionBehaviors.ReadsMutableState, Flags(nested), "only the free argument x is outside state");

        Expression nestedUnknown = Expression.Invoke(Expression.Lambda<Func<double, double>>(Call(nameof(BehaviorMethods.Unannotated), y), y), X);
        Assert.AreEqual(ExpressionBehaviors.UnknownCode, Flags(nestedUnknown));

        Expression capturesFree = Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Add(y, X), y), Expression.Constant(1.0));
        Assert.AreEqual(ExpressionBehaviors.ReadsMutableState, Flags(capturesFree), "a free variable captured by an invoked lambda");

        LambdaExpression body = Expression.Lambda<Func<double, double>>(Call(nameof(BehaviorMethods.Unannotated), y), y);
        Assert.AreEqual(ExpressionBehaviors.Allocates, Flags(body), "a lambda passed as a value does not run");
        Assert.AreEqual(ExpressionBehaviors.UnknownCode, ExpressionBehaviorAnalysis.AnalyzeInvocation(body).Flags);

        Expression declared = Expression.Invoke(Expression.Lambda<Func<double, double>>(Call(nameof(BehaviorMethods.Scale), y), y), X);
        ExpressionBehavior declaredBehavior = ExpressionBehaviorAnalysis.Analyze(declared);
        Assert.IsTrue(declaredBehavior.IsDeterministic && declaredBehavior.ReliesOnDeclarations);

        ParameterExpression nullable = Expression.Parameter(typeof(double?), "n");
        ParameterExpression value = Expression.Parameter(typeof(double?), "value");
        LambdaExpression conversion = Expression.Lambda<Func<double?, double>>(Call(nameof(BehaviorMethods.Unannotated), Expression.Convert(value, typeof(double))), value);
        Assert.AreEqual(ExpressionBehaviors.UnknownCode, Flags(Expression.Coalesce(nullable, Expression.Constant(0.0), conversion)), "the conversion lambda runs");
        Assert.AreEqual(ExpressionBehaviors.ReadsMutableState, Flags(Expression.Coalesce(nullable, Expression.Constant(0.0))));
    }

    // ---------------------------------------------------------------- permissions (future policies)

    /// <summary>
    /// The permissions prepared for the future policies: verified behaviors only, or declarations trusted. Nothing in the
    /// simplifier consumes them yet.
    /// </summary>
    [TestMethod]
    public void Permissions_FollowPolicies()
    {
        ExpressionBehavior pure = ExpressionBehaviorAnalysis.Analyze(Expression.Call(Static(typeof(Math), nameof(Math.Sin), typeof(double)), X));
        ExpressionBehavior partial = ExpressionBehaviorAnalysis.Analyze(Expression.Call(Static(typeof(Math), nameof(Math.Sign), typeof(double)), X));
        ExpressionBehavior clock = ExpressionBehaviorAnalysis.Analyze(Expression.Property(null, typeof(DateTime), nameof(DateTime.Now)));
        ExpressionBehavior declared = ExpressionBehaviorAnalysis.Analyze(Call(nameof(BehaviorMethods.Scale), X));
        ExpressionBehavior unknown = ExpressionBehaviorAnalysis.Analyze(Call(nameof(BehaviorMethods.Unannotated), X));

        foreach (ExpressionTrustPolicy policy in new[] { ExpressionTrustPolicy.Verified, ExpressionTrustPolicy.TrustDeclarations })
        {
            Assert.IsTrue(ExpressionBehaviorPermissions.CanDiscard(pure, policy) && ExpressionBehaviorPermissions.CanRepeat(pure, policy) && ExpressionBehaviorPermissions.CanReorder(pure, policy));
            Assert.IsTrue(!ExpressionBehaviorPermissions.CanDiscard(partial, policy) && ExpressionBehaviorPermissions.CanRepeat(partial, policy) && !ExpressionBehaviorPermissions.CanReorder(partial, policy));
            Assert.IsTrue(ExpressionBehaviorPermissions.CanDiscard(clock, policy) && !ExpressionBehaviorPermissions.CanRepeat(clock, policy) && !ExpressionBehaviorPermissions.CanReorder(clock, policy));
            Assert.IsTrue(!ExpressionBehaviorPermissions.CanDiscard(unknown, policy) && !ExpressionBehaviorPermissions.CanRepeat(unknown, policy) && !ExpressionBehaviorPermissions.CanReorder(unknown, policy));
        }

        Assert.IsFalse(ExpressionBehaviorPermissions.CanRepeat(declared, ExpressionTrustPolicy.Verified), "a declaration is not trusted by the verified policy");
        Assert.IsTrue(ExpressionBehaviorPermissions.CanRepeat(declared, ExpressionTrustPolicy.TrustDeclarations));
        Assert.IsFalse(ExpressionBehaviorPermissions.CanDiscard(declared, ExpressionTrustPolicy.TrustDeclarations), "declared code may still throw");

        // The bridge to the existing gate is at least as strict as the gate itself.
        Assert.AreEqual(ExpressionEffects.ReadsMutableState, ExpressionBehaviorPermissions.ToEffects(pure, ExpressionTrustPolicy.Verified));
        ExpressionEffects gate = ExpressionEffectAnalysis.Analyze(Call(nameof(BehaviorMethods.Unannotated), X));
        ExpressionEffects bridged = ExpressionBehaviorPermissions.ToEffects(unknown, ExpressionTrustPolicy.Verified);
        Assert.AreEqual(gate, bridged & gate);
        Assert.AreEqual(ExpressionEffects.ReadsMutableState | ExpressionEffects.MayThrow | ExpressionEffects.Allocates | ExpressionEffects.Observable, bridged);
        Assert.AreEqual(ExpressionEffects.Observable | ExpressionEffects.ReadsMutableState | ExpressionEffects.MayThrow | ExpressionEffects.Allocates,
            ExpressionBehaviorPermissions.ToEffects(declared, ExpressionTrustPolicy.Verified), "an untrusted declaration is unknown code");
    }

    // ---------------------------------------------------------------- no change to the simplifier

    /// <summary>
    /// The simplifier ignores declarations: for the exact and a derived simplifier, a declared call (even one whose declaration
    /// lies) is neither dropped nor merged, the simplified tree runs it exactly as often as the source, and the structural
    /// comparer is unchanged.
    /// </summary>
    [TestMethod]
    public void Simplifiers_IgnoreDeclarations()
    {
        Expression lying = Call(nameof(BehaviorMethods.LyingCounter), X);
        Expression[] sources =
        [
            Expression.Subtract(lying, lying),
            Expression.Multiply(lying, Expression.Constant(0.0)),
            Expression.Add(lying, lying),
            Expression.Add(Call(nameof(BehaviorMethods.Scale), X), Call(nameof(BehaviorMethods.Scale), X)),
        ];

        foreach (ExpressionSimplifier simplifier in new ExpressionSimplifier[] { new(), new DerivedSimplifier() })
        {
            foreach (Expression source in sources)
            {
                Expression simplified = simplifier.Simplify(source);
                Assert.AreEqual(CountCalls(source), CountCalls(simplified), $"{simplifier.GetType().Name}: {source} -> {simplified}");

                Func<double, double> sourceFunction = Expression.Lambda<Func<double, double>>(source, X).Compile();
                Func<double, double> simplifiedFunction = Expression.Lambda<Func<double, double>>(simplified, X).Compile();
                BehaviorMethods.LyingCalls = 0;
                double expected = sourceFunction(3.0);
                int sourceCalls = BehaviorMethods.LyingCalls;
                BehaviorMethods.LyingCalls = 0;
                double actual = simplifiedFunction(3.0);
                Assert.AreEqual(sourceCalls, BehaviorMethods.LyingCalls, $"{simplifier.GetType().Name}: {source} -> {simplified}");
                Assert.AreEqual(expected, actual);
            }
        }

        Assert.IsTrue(ExpressionComparer.Default.Equals(Call(nameof(BehaviorMethods.Scale), X), Call(nameof(BehaviorMethods.Scale), X)));
    }

    /// <summary>Counts the method calls of a tree, a shared call once per occurrence (as it is evaluated).</summary>
    /// <param name="e">The tree.</param>
    /// <returns>The number of <see cref="MethodCallExpression"/> occurrences.</returns>
    private static int CountCalls(Expression e)
    {
        var counter = new CallCounter();
        counter.Visit(e);
        return counter.Count;
    }

    /// <summary>Counts the occurrences of method calls.</summary>
    private sealed class CallCounter : ExpressionVisitor
    {
        /// <summary>Gets the number of calls visited.</summary>
        public int Count { get; private set; }

        /// <inheritdoc/>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Count++;
            return base.VisitMethodCall(node);
        }
    }

    // ---------------------------------------------------------------- concurrency and resources

    /// <summary>Concurrent analyses of shared trees and methods agree with a sequential analysis.</summary>
    [TestMethod]
    public void ConcurrentAnalyses_AgreeWithSequential()
    {
        var trees = new List<Expression>();
        var generator = new EtcAuditGenerator(20261104, 5, AuditFamilies.All & ~AuditFamilies.Extension);
        for (int i = 0; i < 200; i++) trees.Add(generator.Generate());
        trees.Add(Call(nameof(BehaviorMethods.Scale), X));
        trees.Add(Expression.Add(Expression.Parameter(typeof(Money), "m"), Expression.Constant(new Money(1m))));

        ExpressionBehaviors[] expected = trees.Select(Flags).ToArray();
        var results = new ExpressionBehaviors[16][];
        Parallel.For(0, results.Length, worker =>
        {
            var local = new ExpressionBehaviors[trees.Count];
            for (int i = 0; i < trees.Count; i++)
            {
                int index = (i + worker * 7) % trees.Count;
                local[index] = Flags(trees[index]);
            }

            results[worker] = local;
        });

        foreach (ExpressionBehaviors[] result in results)
        {
            CollectionAssert.AreEqual(expected, result);
        }
    }

    /// <summary>Each analysis is a bounded expression operation: the input is validated and the visits are counted.</summary>
    [TestMethod]
    public void Analysis_IsBoundedByResourceLimits()
    {
        Expression chain = X;
        for (int i = 0; i < 40; i++) chain = Expression.Negate(chain);

        Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionBehaviorAnalysis.Analyze(chain, new ExpressionResourceLimits { MaxDepth = 10 }));
        Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionBehaviorAnalysis.Analyze(chain, new ExpressionResourceLimits { MaxStructuralVisits = 10 }));
        Assert.AreEqual(ExpressionBehaviors.ReadsMutableState, Flags(chain));
        Assert.ThrowsExactly<ArgumentNullException>(() => ExpressionBehaviorAnalysis.Analyze(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ExpressionBehaviorAnalysis.AnalyzeMethod(null!));
    }
}
