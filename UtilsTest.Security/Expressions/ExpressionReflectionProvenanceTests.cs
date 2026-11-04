using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// S5 A-10: reflection objects that are not the runtime's own (user subclasses of <see cref="MethodInfo"/>,
/// <see cref="FieldInfo"/>, <see cref="PropertyInfo"/>) can never obtain the privileges of an intrinsic or trusted member by
/// lying about their metadata, and the engine's decisions never run their members. Before the fix, a <see cref="MethodInfo"/>
/// impersonating <c>Math.Sin</c> made <c>Simplify(FakeSin(x) * 0)</c> return <c>0</c>, one impersonating <c>double.Sin</c> was
/// replaced by the real function, and an impersonated <c>decimal.op_Addition</c> made <c>d + 0m</c> lose its user call.
/// </summary>
[TestClass]
public class ExpressionReflectionProvenanceTests
{
    /// <summary>Upper bound of the worker threads.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The small worker stack of the stack-guard tests.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>A free parameter of type <see cref="double"/>.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>A free parameter of type <see cref="decimal"/>.</summary>
    private static readonly ParameterExpression D = Expression.Parameter(typeof(decimal), "d");

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>A reference cell with a mutable field and property, read through impersonating members.</summary>
    public sealed class Cell
    {
        /// <summary>A mutable field.</summary>
        public double Value = 2.0;

        /// <summary>Gets <see cref="Value"/>.</summary>
        public double Property => Value;
    }

    /// <summary>The exact and a derived simplifier.</summary>
    private static readonly ExpressionSimplifier[] Simplifiers = [new ExpressionSimplifier(), new DerivedSimplifier()];

    /// <summary>Gets a static method by declaring type, name and parameter types.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="name">The name.</param>
    /// <param name="parameters">The parameter types.</param>
    /// <returns>The method.</returns>
    private static MethodInfo Static(Type type, string name, params Type[] parameters)
        => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, parameters) ?? throw new MissingMethodException(type.Name, name);

    /// <summary>Counts the occurrences of <paramref name="member"/> held by the nodes of <paramref name="e"/>, by reference.</summary>
    /// <param name="e">The tree.</param>
    /// <param name="member">The reflection object.</param>
    /// <returns>The number of nodes holding it.</returns>
    private static int Occurrences(Expression e, MemberInfo member)
    {
        var counter = new MemberCounter(member);
        counter.Visit(e);
        return counter.Count;
    }

    /// <summary>Counts the nodes holding a given reflection object, without calling any of its members.</summary>
    private sealed class MemberCounter : ExpressionVisitor
    {
        /// <summary>The counted object.</summary>
        private readonly MemberInfo _member;

        /// <summary>Initializes a counter.</summary>
        /// <param name="member">The counted object.</param>
        public MemberCounter(MemberInfo member) => _member = member;

        /// <summary>Gets the number of occurrences.</summary>
        public int Count { get; private set; }

        /// <inheritdoc/>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (ReferenceEquals(node.Method, _member)) Count++;
            return base.VisitMethodCall(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (ReferenceEquals(node.Method, _member)) Count++;
            return base.VisitBinary(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (ReferenceEquals(node.Method, _member)) Count++;
            return base.VisitUnary(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitMember(MemberExpression node)
        {
            if (ReferenceEquals(node.Member, _member)) Count++;
            return base.VisitMember(node);
        }
    }

    /// <summary>
    /// Asserts that simplifying <paramref name="source"/> keeps every occurrence of the impersonating member and that the
    /// interpreted simplified tree evaluates it exactly as often as the interpreted source, with the same result, for the exact
    /// and a derived simplifier.
    /// </summary>
    /// <typeparam name="T">The parameter type.</typeparam>
    /// <param name="source">The source tree over <paramref name="parameter"/>.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="member">The impersonating member.</param>
    /// <param name="trap">Its trap.</param>
    /// <param name="argument">The argument of the evaluation.</param>
    /// <param name="countsEvaluations">Whether the interpreter evaluates <paramref name="member"/> through the impersonation (a call, an operator, a field; not a property, whose runtime getter it calls directly), so that evaluations can be counted.</param>
    private static void AssertUserEvaluationKept<T>(Expression source, ParameterExpression parameter, MemberInfo member, ReflectionTrap trap, T argument, bool countsEvaluations = true)
    {
        foreach (ExpressionSimplifier simplifier in Simplifiers)
        {
            string label = $"{simplifier.GetType().Name}: {source}";
            Expression simplified = simplifier.Simplify(source);
            Assert.AreEqual(Occurrences(source, member), Occurrences(simplified, member), $"{label} -> {simplified}");

            Delegate sourceFunction = Expression.Lambda(source, parameter).Compile(preferInterpretation: true);
            Delegate simplifiedFunction = Expression.Lambda(simplified, parameter).Compile(preferInterpretation: true);
            trap.Reset();
            object? expected = sourceFunction.DynamicInvoke(argument);
            int sourceEvaluations = trap.Evaluations;
            trap.Reset();
            object? actual = simplifiedFunction.DynamicInvoke(argument);
            Assert.AreEqual(sourceEvaluations, trap.Evaluations, $"{label} -> {simplified}");
            Assert.AreEqual(expected, actual, label);
            Assert.AreEqual(countsEvaluations, sourceEvaluations > 0, label);
        }
    }

    // ---------------------------------------------------------------- S-01 / S-08

    /// <summary>
    /// S-01, S-08: an impersonation of <c>Math.Sin</c>, <c>double.Sin</c> or <c>Math.Pow</c> keeps every evaluation through the
    /// algebraic identities (product by zero, difference and sum of equal terms, the trigonometric identity) and is never
    /// replaced by the function it imitates, for the exact and a derived simplifier.
    /// </summary>
    [TestMethod]
    public void ImpersonatedFunctions_KeepTheirEvaluations()
    {
        var mathSin = new ImpersonatingMethod(Static(typeof(Math), nameof(Math.Sin), typeof(double)));
        var doubleSin = new ImpersonatingMethod(Static(typeof(double), nameof(double.Sin), typeof(double)));
        var doubleCos = Static(typeof(double), nameof(double.Cos), typeof(double));
        var mathPow = new ImpersonatingMethod(Static(typeof(Math), nameof(Math.Pow), typeof(double), typeof(double)));

        Expression fakeSin = Expression.Call(mathSin, X);
        AssertUserEvaluationKept(Expression.Multiply(fakeSin, Expression.Constant(0.0)), X, mathSin, mathSin.Trap, 0.5);
        AssertUserEvaluationKept(Expression.Subtract(fakeSin, fakeSin), X, mathSin, mathSin.Trap, 0.5);
        AssertUserEvaluationKept(Expression.Add(fakeSin, Expression.Call(mathSin, X)), X, mathSin, mathSin.Trap, 0.5);

        Expression fakeDoubleSin = Expression.Call(doubleSin, X);
        AssertUserEvaluationKept(fakeDoubleSin, X, doubleSin, doubleSin.Trap, 0.5);
        AssertUserEvaluationKept(
            Expression.Add(Expression.Power(fakeDoubleSin, Expression.Constant(2.0)), Expression.Power(Expression.Call(doubleCos, X), Expression.Constant(2.0))),
            X, doubleSin, doubleSin.Trap, 0.5);

        AssertUserEvaluationKept(Expression.Call(mathPow, X, Expression.Constant(2.0)), X, mathPow, mathPow.Trap, 3.0);
    }

    // ---------------------------------------------------------------- S-02 / S-06 / S-10

    /// <summary>
    /// S-02, S-10: with every member of the impersonating objects armed to throw, none of the engine's decision points reads
    /// them: the provenance check, the effect gate (expression, operands, operator, member), the ordinary-arithmetic checks,
    /// the call-rule signatures, the inlining analysis and the public behavior analysis, which classifies them as unknown code.
    /// </summary>
    [TestMethod]
    public void DecisionPoints_NeverReadImpersonatingObjects()
    {
        var sin = new ImpersonatingMethod(Static(typeof(Math), nameof(Math.Sin), typeof(double)));
        var add = new ImpersonatingMethod(Static(typeof(decimal), "op_Addition", typeof(decimal), typeof(decimal)));
        var negate = new ImpersonatingMethod(Static(typeof(decimal), "op_UnaryNegation", typeof(decimal)));
        var implicitConversion = new ImpersonatingMethod(Static(typeof(decimal), "op_Implicit", typeof(int)));
        var field = new ImpersonatingField(typeof(Cell).GetField(nameof(Cell.Value))!);
        var property = new ImpersonatingProperty(typeof(Cell).GetProperty(nameof(Cell.Property))!);
        ConstantExpression cell = Expression.Constant(new Cell());
        ParameterExpression p = Expression.Parameter(typeof(double), "p");

        MethodCallExpression call = Expression.Call(sin, X);
        BinaryExpression sum = Expression.Add(D, Expression.Constant(1m), add);
        UnaryExpression negation = Expression.Negate(D, negate);
        UnaryExpression conversion = Expression.Convert(Expression.Parameter(typeof(int), "i"), typeof(decimal), implicitConversion);
        MemberExpression fieldRead = Expression.Field(cell, field);
        MemberExpression propertyRead = Expression.Property(cell, property);
        LambdaExpression lambda = Expression.Lambda<Func<double, double>>(Expression.Call(sin, p), p);
        InvocationExpression invocation = Expression.Invoke(lambda, X);
        Expression[] trees = [call, sum, negation, conversion, fieldRead, propertyRead, Expression.Multiply(call, Expression.Constant(0.0))];
        string[] labels = ["call", "decimal sum", "decimal negation", "decimal conversion", "field read", "property read", "product by zero"];
        ReflectionTrap[] traps = [sin.Trap, add.Trap, negate.Trap, implicitConversion.Trap, field.Trap, property.Trap];
        foreach (ReflectionTrap trap in traps)
        {
            trap.Reset();
            trap.Armed = true;
        }

        try
        {
            foreach (MemberInfo member in new MemberInfo[] { sin, add, negate, implicitConversion, field, property })
            {
                Assert.IsFalse(ExpressionReflectionProvenance.IsRuntime(member));
                Assert.IsFalse(ExpressionReflectionProvenance.IsSameMember(member, typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])));
            }

            // The labels are computed beforehand: the framework's ToString of a call reads its method's attributes.
            for (int i = 0; i < trees.Length; i++)
            {
                Assert.AreEqual(ExpressionEffects.Observable, ExpressionEffectAnalysis.Analyze(trees[i]) & ExpressionEffects.Observable, labels[i]);
                Assert.IsFalse(ExpressionEffectAnalysis.IsInert(ExpressionEffectAnalysis.Analyze(trees[i])), labels[i]);
                Assert.IsFalse(ExpressionBehaviorAnalysis.Analyze(trees[i]).IsFullyAnalyzed, labels[i]);
            }

            Assert.IsFalse(ExpressionEffectAnalysis.HasInertOperands(Expression.Multiply(call, Expression.Constant(0.0))));
            Assert.AreEqual(ExpressionEffects.Observable, ExpressionEffectAnalysis.MethodEffects(sin));
            Assert.IsFalse(ExpressionSimplifier.IsOrdinaryBinaryArithmetic(sum));
            Assert.IsFalse(ExpressionSimplifier.IsOrdinaryUnaryNegate(negation));
            Assert.IsFalse(new ExpressionCallSignatureAttribute(typeof(Math), nameof(Math.Sin)).Match(call));
            Assert.IsFalse(new ExpressionCallSignatureAttribute(typeof(System.Numerics.ITrigonometricFunctions<>), nameof(Math.Sin)).Match(call));
            Assert.AreEqual(InvocationInliningKind.Preserve, InvocationInliningAnalysis.Analyze(lambda, invocation));
            Assert.AreEqual(ExpressionBehaviors.UnknownCode, ExpressionBehaviorAnalysis.AnalyzeMethod(sin).Flags);
            Assert.AreEqual(ExpressionBehaviors.UnknownCode, ExpressionBehaviorAnalysis.AnalyzeInvocation(lambda).Flags);
            StringAssert.Contains(ExpressionTraceFormatter.Format(call), "<custom member>");
            StringAssert.Contains(ExpressionTraceFormatter.Format(fieldRead), "<custom member>");
        }
        finally
        {
            foreach (ReflectionTrap trap in traps) trap.Armed = false;
        }

        foreach (ReflectionTrap trap in traps)
        {
            Assert.AreEqual(0, trap.ArmedCalls, string.Join(", ", trap.Calls));
        }
    }

    /// <summary>
    /// S-06: the method caches never store, hash or compare an impersonating object, even when it shares the hash code of the
    /// method it imitates and claims to be equal to it, sequentially or concurrently; the real method keeps its classification.
    /// </summary>
    [TestMethod]
    public void MethodCaches_NeverTouchImpersonatingObjects()
    {
        MethodInfo realSin = Static(typeof(Math), nameof(Math.Sin), typeof(double));
        Assert.AreEqual(ExpressionEffects.None, ExpressionEffectAnalysis.MethodEffects(realSin), "the real method is cached first");
        ImpersonatingMethod[] fakes = [.. Enumerable.Range(0, 8).Select(_ => new ImpersonatingMethod(realSin))];
        foreach (ImpersonatingMethod fake in fakes) fake.Trap.Armed = true;

        try
        {
            for (int i = 0; i < 1000; i++)
            {
                ImpersonatingMethod fake = fakes[i % fakes.Length];
                Assert.AreEqual(ExpressionEffects.Observable, ExpressionEffectAnalysis.MethodEffects(fake));
                Assert.AreEqual(ExpressionBehaviors.UnknownCode, ExpressionBehaviorAnalysis.AnalyzeMethod(fake).Flags);
            }

            Parallel.For(0, 4000, i =>
            {
                ImpersonatingMethod fake = fakes[i % fakes.Length];
                if (ExpressionEffectAnalysis.MethodEffects(fake) != ExpressionEffects.Observable) throw new AssertFailedException("cached");
                if (ExpressionBehaviorAnalysis.AnalyzeMethod(fake).Flags != ExpressionBehaviors.UnknownCode) throw new AssertFailedException("cached");
                if (ExpressionEffectAnalysis.MethodEffects(realSin) != ExpressionEffects.None) throw new AssertFailedException("real method polluted");
            });
        }
        finally
        {
            foreach (ImpersonatingMethod fake in fakes) fake.Trap.Armed = false;
        }

        Assert.AreEqual(0, fakes.Sum(fake => fake.Trap.ArmedCalls));
        Assert.AreEqual(ExpressionEffects.None, ExpressionEffectAnalysis.MethodEffects(realSin));
        Assert.AreEqual(ExpressionBehaviors.None, ExpressionBehaviorAnalysis.AnalyzeMethod(realSin).Flags);
    }

    // ---------------------------------------------------------------- S-03 / S-04 / S-05

    /// <summary>
    /// S-03: an impersonation of a <c>decimal</c> operator is never ordinary arithmetic: <c>d + 0m</c>, <c>d * 0m</c> and
    /// <c>-(-d)</c> keep every user evaluation, while the real operators are still simplified.
    /// </summary>
    [TestMethod]
    public void ImpersonatedOperators_AreUserCode()
    {
        var add = new ImpersonatingMethod(Static(typeof(decimal), "op_Addition", typeof(decimal), typeof(decimal)));
        var multiply = new ImpersonatingMethod(Static(typeof(decimal), "op_Multiply", typeof(decimal), typeof(decimal)));
        var negate = new ImpersonatingMethod(Static(typeof(decimal), "op_UnaryNegation", typeof(decimal)));

        AssertUserEvaluationKept(Expression.Add(D, Expression.Constant(0m), add), D, add, add.Trap, 3m);
        AssertUserEvaluationKept(Expression.Multiply(D, Expression.Constant(0m), multiply), D, multiply, multiply.Trap, 3m);
        AssertUserEvaluationKept(Expression.Negate(Expression.Negate(D, negate), negate), D, negate, negate.Trap, 3m);

        // Positive control: the real operator is ordinary arithmetic.
        BinaryExpression real = Expression.Add(D, Expression.Constant(0m));
        Assert.IsTrue(ExpressionSimplifier.IsOrdinaryBinaryArithmetic(real));
        Assert.AreSame(D, new ExpressionSimplifier().Simplify(real));
    }

    /// <summary>S-04: an impersonation of a <c>decimal</c> conversion is user code; the real conversions keep their classification.</summary>
    [TestMethod]
    public void ImpersonatedConversions_AreUserCode()
    {
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        var implicitConversion = new ImpersonatingMethod(Static(typeof(decimal), "op_Implicit", typeof(int)));
        MethodInfo realExplicit = typeof(decimal).GetMethods().Single(m => m.Name == "op_Explicit" && m.ReturnType == typeof(double));
        var explicitConversion = new ImpersonatingMethod(realExplicit);

        Assert.AreEqual(ExpressionEffects.Observable | ExpressionEffects.ReadsMutableState, ExpressionEffectAnalysis.Analyze(Expression.Convert(i, typeof(decimal), implicitConversion)));
        Assert.AreEqual(ExpressionEffects.Observable | ExpressionEffects.ReadsMutableState, ExpressionEffectAnalysis.Analyze(Expression.Convert(D, typeof(double), explicitConversion)));
        AssertUserEvaluationKept(Expression.Multiply(Expression.Convert(D, typeof(double), explicitConversion), Expression.Constant(0.0)), D, explicitConversion, explicitConversion.Trap, 3m);

        // Positive controls.
        Assert.AreEqual(ExpressionEffects.ReadsMutableState, ExpressionEffectAnalysis.Analyze(Expression.Convert(i, typeof(decimal))));
        Assert.AreEqual(ExpressionEffects.ReadsMutableState | ExpressionEffects.MayThrow, ExpressionEffectAnalysis.Analyze(Expression.Convert(D, typeof(int))));
    }

    /// <summary>
    /// S-05: a field impersonation cannot claim to be read-only, and a property impersonation stays user code, for the gate and
    /// the public analysis; reading them through a product by zero keeps the user evaluation. Real fields keep their classification.
    /// </summary>
    [TestMethod]
    public void ImpersonatedFieldsAndProperties_AreUserCode()
    {
        ConstantExpression cell = Expression.Constant(new Cell());
        var field = new ImpersonatingField(typeof(Cell).GetField(nameof(Cell.Value))!);
        var property = new ImpersonatingProperty(typeof(Cell).GetProperty(nameof(Cell.Property))!);
        ParameterExpression unused = Expression.Parameter(typeof(double), "unused");

        Assert.AreEqual(ExpressionEffects.Observable, ExpressionEffectAnalysis.Analyze(Expression.Field(cell, field)));
        Assert.AreEqual(ExpressionEffects.Observable, ExpressionEffectAnalysis.Analyze(Expression.Property(cell, property)));
        Assert.AreEqual(ExpressionBehaviors.UnknownCode, ExpressionBehaviorAnalysis.Analyze(Expression.Field(cell, field)).Flags);
        Assert.AreEqual(ExpressionBehaviors.UnknownCode, ExpressionBehaviorAnalysis.Analyze(Expression.Property(cell, property)).Flags);
        AssertUserEvaluationKept(Expression.Multiply(Expression.Field(cell, field), Expression.Constant(0.0)), unused, field, field.Trap, 1.0);
        AssertUserEvaluationKept(Expression.Multiply(Expression.Property(cell, property), Expression.Constant(0.0)), unused, property, property.Trap, 1.0, countsEvaluations: false);

        // Positive controls: a real mutable field reads mutable state, a real read-only one does not.
        Assert.AreEqual(ExpressionEffects.ReadsMutableState, ExpressionEffectAnalysis.Analyze(Expression.Field(cell, nameof(Cell.Value))));
        Assert.AreEqual(ExpressionEffects.None, ExpressionEffectAnalysis.Analyze(Expression.Field(null, typeof(string), nameof(string.Empty))));
    }

    // ---------------------------------------------------------------- provenance classes

    /// <summary>
    /// The provenance check accepts the runtime's reflection objects whatever their source (ordinary, generic definitions and
    /// instantiations, array methods, created dynamic and collectible types, <see cref="DynamicMethod"/>, constructors, ordinary
    /// and literal fields, properties) and refuses the others (emit builders, user subclasses, <see langword="null"/>) without
    /// reading them. A real user method stays user code.
    /// </summary>
    [TestMethod]
    public void Provenance_AcceptsRuntimeObjectsOnly()
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("A10Provenance"), AssemblyBuilderAccess.RunAndCollect);
        TypeBuilder typeBuilder = assembly.DefineDynamicModule("m").DefineType("T", TypeAttributes.Public);
        MethodBuilder builder = typeBuilder.DefineMethod("F", MethodAttributes.Public | MethodAttributes.Static, typeof(double), [typeof(double)]);
        ILGenerator il = builder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ret);
        Type created = typeBuilder.CreateType();
        var dynamicMethod = new DynamicMethod("D", typeof(double), [typeof(double)]);
        ILGenerator dynamicIl = dynamicMethod.GetILGenerator();
        dynamicIl.Emit(OpCodes.Ldarg_0);
        dynamicIl.Emit(OpCodes.Ret);
        var context = new AssemblyLoadContext("A10Provenance", isCollectible: true);
        MethodInfo collectible = context.LoadFromAssemblyPath(typeof(ExpressionSimplifier).Assembly.Location)
            .GetType(typeof(ExpressionBehaviorAnalysis).FullName!)!.GetMethod(nameof(ExpressionBehaviorAnalysis.AnalyzeMethod))!;

        MemberInfo[] runtime =
        [
            Static(typeof(Math), nameof(Math.Sin), typeof(double)),
            typeof(Enumerable).GetMethod(nameof(Enumerable.Empty))!,
            typeof(Enumerable).GetMethod(nameof(Enumerable.Empty))!.MakeGenericMethod(typeof(int)),
            typeof(int[]).GetMethod("Get")!,
            created.GetMethod("F")!,
            collectible,
            dynamicMethod,
            typeof(Cell).GetConstructor(Type.EmptyTypes)!,
            typeof(Cell).GetField(nameof(Cell.Value))!,
            typeof(Math).GetField(nameof(Math.PI))!,
            typeof(Cell).GetProperty(nameof(Cell.Property))!,
        ];
        foreach (MemberInfo member in runtime)
        {
            Assert.IsTrue(ExpressionReflectionProvenance.IsRuntime(member), member.ToString());
        }

        var impersonation = new ImpersonatingMethod(Static(typeof(Math), nameof(Math.Sin), typeof(double)));
        impersonation.Trap.Armed = true;
        Assert.IsFalse(ExpressionReflectionProvenance.IsRuntime(builder), "an emit builder can forward to user objects");
        Assert.IsFalse(ExpressionReflectionProvenance.IsRuntime(impersonation));
        Assert.IsFalse(ExpressionReflectionProvenance.IsRuntime(null));
        Assert.AreEqual(0, impersonation.Trap.ArmedCalls);

        // A runtime user method is accepted by provenance but stays unknown user code.
        Assert.AreEqual(ExpressionEffects.Observable, ExpressionEffectAnalysis.MethodEffects((MethodInfo)created.GetMethod("F")!));
        Assert.AreEqual(ExpressionBehaviors.UnknownCode, ExpressionBehaviorAnalysis.AnalyzeMethod(dynamicMethod).Flags);
        context.Unload();
    }

    // ---------------------------------------------------------------- S-09

    /// <summary>
    /// S-09: a conservative classification does not evade the resource limits: a hostile nesting of impersonated calls ends with a
    /// controlled refusal on a small stack for the gate, the public analysis and the simplifier, the default limits refuse it
    /// before any recursion, a shared DAG is refused, and no impersonating member is read meanwhile.
    /// </summary>
    [TestMethod]
    public void ImpersonatedNestings_AreResourceBounded()
    {
        var sin = new ImpersonatingMethod(Static(typeof(Math), nameof(Math.Sin), typeof(double)));
        Expression deep = Chain(100_000, e => Expression.Call(sin, e));
        Expression dag = P;
        for (int i = 0; i < 40; i++) dag = Expression.Add(dag, Expression.Call(sin, dag));
        sin.Trap.Reset();
        sin.Trap.Armed = true;
        try
        {
            RunOnThread(() =>
            {
                Assert.AreEqual(ExpressionResourceLimitKind.Stack, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, null, null, null, deep, static d => ExpressionEffectAnalysis.Analyze(d))).Kind);
                Assert.AreEqual(ExpressionResourceLimitKind.Stack, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionBehaviorAnalysis.Analyze(deep, ExpressionResourceLimits.Unlimited)).Kind);
                Assert.AreEqual(ExpressionResourceLimitKind.Stack, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(deep)).Kind);
                Assert.AreEqual(ExpressionResourceLimitKind.Depth, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier().Simplify(deep)).Kind);
                Assert.AreEqual(ExpressionResourceLimitKind.Depth, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionBehaviorAnalysis.Analyze(deep)).Kind);
                Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
            }, SmallStack, Timeout);

            Assert.AreEqual(ExpressionResourceLimitKind.Nodes, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier().Simplify(dag)).Kind);
            Assert.AreEqual(ExpressionResourceLimitKind.Nodes, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => ExpressionBehaviorAnalysis.Analyze(dag)).Kind);
        }
        finally
        {
            sin.Trap.Armed = false;
        }

        Assert.AreEqual(0, sin.Trap.ArmedCalls, string.Join(", ", sin.Trap.Calls.Distinct()));
    }
}
