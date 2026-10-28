using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// One reusable expression-tree sample of the ETC coverage catalog (see
/// <c>Utils/TODO-2026-10-27-etc-expression-tree-coverage-roadmap.md</c>).
/// </summary>
/// <param name="Name">The stable sample identifier used by the baseline tables and the roadmap matrix.</param>
/// <param name="RootNodeType">The <see cref="ExpressionType"/> of the sample's root node.</param>
/// <param name="Build">
/// Builds a fresh, independent tree. Every call must allocate new nodes (except the shared free parameters of
/// <see cref="ExpressionNodeCatalog"/>) so that two calls produce structurally equal but reference-distinct trees.
/// </param>
/// <param name="IsPrimary">Whether this is the primary sample of <paramref name="RootNodeType"/> (exactly one per node type).</param>
internal sealed record ExpressionNodeSample(string Name, ExpressionType RootNodeType, Func<Expression> Build, bool IsPrimary)
{
    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>
/// Catalog of expression-tree samples used by the ETC (expression tree coverage) characterization tests: one
/// primary sample per <see cref="ExpressionType"/> value plus variants exercising optional metadata (custom
/// operator methods, lifting, conversion lambdas, catch variables and filters, labels, member bindings...).
/// </summary>
/// <remarks>
/// Every sample places the marker leaf <see cref="Leaf"/> (<c>p * 1.0</c>) in as many valid child positions as its
/// node family allows, so a single tree shows, per contract, whether every child position is visited:
/// <list type="bullet">
/// <item><description>the generic recursive transformer rewrites the marker's <c>1.0</c> constant into <c>2.0</c>;</description></item>
/// <item><description>the simplifier rewrites <c>p * 1.0</c> into <c>p</c>;</description></item>
/// <item><description>parameter substitution replaces the free parameter <see cref="Free"/> by a constant.</description></item>
/// </list>
/// The catalog contains no other <see cref="double"/> constant equal to <c>1.0</c>, so the markers are unambiguous.
/// </remarks>
internal static class ExpressionNodeCatalog
{
    /// <summary>The free <see cref="double"/> parameter every marker leaf reads.</summary>
    public static readonly ParameterExpression Free = Expression.Parameter(typeof(double), "p");

    /// <summary>A free <see cref="double"/> variable used as the writable target of assignment samples.</summary>
    public static readonly ParameterExpression Variable = Expression.Parameter(typeof(double), "v");

    /// <summary>A free <see cref="int"/> variable used as the writable target of integral assignment samples.</summary>
    public static readonly ParameterExpression IntVariable = Expression.Parameter(typeof(int), "i");

    /// <summary>The <see cref="Math.Sin(double)"/> method.</summary>
    private static readonly MethodInfo SinMethod = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;

    /// <summary>The <see cref="TimeSpan.FromSeconds(double)"/> method.</summary>
    private static readonly MethodInfo FromSecondsMethod = typeof(TimeSpan).GetMethod(nameof(TimeSpan.FromSeconds), [typeof(double)])!;

    /// <summary>The <see cref="StrongBox{T}"/> constructor taking the initial value.</summary>
    private static readonly ConstructorInfo StrongBoxConstructor = typeof(StrongBox<double>).GetConstructor([typeof(double)])!;

    /// <summary>The <see cref="StrongBox{T}.Value"/> field.</summary>
    private static readonly FieldInfo StrongBoxValueField = typeof(StrongBox<double>).GetField(nameof(StrongBox<double>.Value))!;

    /// <summary>The <see cref="List{T}.Add(T)"/> method.</summary>
    private static readonly MethodInfo ListAddMethod = typeof(List<double>).GetMethod(nameof(List<double>.Add))!;

    /// <summary>A shared symbol document used by the debug-information sample.</summary>
    private static readonly SymbolDocumentInfo Document = Expression.SymbolDocument("etc-sample.cs");

    /// <summary>Builds a fresh marker leaf <c>p * 1.0</c>.</summary>
    /// <returns>A new <see cref="double"/> marker expression.</returns>
    public static Expression Leaf() => Expression.Multiply(Free, Expression.Constant(1.0));

    /// <summary>Builds a fresh marker leaf converted to <see cref="int"/>.</summary>
    /// <returns>A new <see cref="int"/> marker expression.</returns>
    private static Expression IntLeaf() => Expression.Convert(Leaf(), typeof(int));

    /// <summary>Builds a fresh marker leaf boxed to <see cref="object"/>.</summary>
    /// <returns>A new <see cref="object"/> marker expression.</returns>
    private static Expression ObjectLeaf() => Expression.Convert(Leaf(), typeof(object));

    /// <summary>Builds a fresh <see cref="bool"/> marker test <c>p * 1.0 &gt; 0.5</c>.</summary>
    /// <returns>A new <see cref="bool"/> marker expression.</returns>
    private static Expression BoolLeaf() => Expression.GreaterThan(Leaf(), Expression.Constant(0.5));

    /// <summary>Builds a fresh marker leaf lifted to <see cref="Nullable{T}"/>.</summary>
    /// <returns>A new <see cref="Nullable{T}"/> marker expression.</returns>
    private static Expression NullableLeaf() => Expression.Convert(Leaf(), typeof(double?));

    /// <summary>Gets every sample of the catalog, primary samples first in <see cref="ExpressionType"/> order.</summary>
    public static IReadOnlyList<ExpressionNodeSample> All { get; } = CreateSamples();

    /// <summary>Gets a sample by its stable name.</summary>
    /// <param name="name">The sample name.</param>
    /// <returns>The matching sample.</returns>
    public static ExpressionNodeSample Get(string name) => All.Single(sample => sample.Name == name);

    /// <summary>Creates the full sample list.</summary>
    /// <returns>Every primary sample followed by every variant.</returns>
    private static List<ExpressionNodeSample> CreateSamples()
    {
        var samples = new List<ExpressionNodeSample>();

        void Primary(ExpressionType type, Func<Expression> build) => samples.Add(new ExpressionNodeSample(type.ToString(), type, build, true));
        void Variant(string name, ExpressionType type, Func<Expression> build) => samples.Add(new ExpressionNodeSample(name, type, build, false));

        // Arithmetic and bitwise binary operators.
        Primary(ExpressionType.Add, () => Expression.Add(Leaf(), Leaf()));
        Primary(ExpressionType.AddChecked, () => Expression.AddChecked(Leaf(), Leaf()));
        Primary(ExpressionType.Subtract, () => Expression.Subtract(Leaf(), Expression.Add(Leaf(), Expression.Constant(0.5))));
        Primary(ExpressionType.SubtractChecked, () => Expression.SubtractChecked(Leaf(), Expression.Add(Leaf(), Expression.Constant(0.5))));
        Primary(ExpressionType.Multiply, () => Expression.Multiply(Expression.Add(Leaf(), Expression.Constant(0.5)), Leaf()));
        Primary(ExpressionType.MultiplyChecked, () => Expression.MultiplyChecked(Expression.Add(Leaf(), Expression.Constant(0.5)), Leaf()));
        Primary(ExpressionType.Divide, () => Expression.Divide(Leaf(), Expression.Add(Leaf(), Expression.Constant(0.5))));
        Primary(ExpressionType.Modulo, () => Expression.Modulo(Leaf(), Expression.Add(Leaf(), Expression.Constant(0.5))));
        Primary(ExpressionType.Power, () => Expression.Power(Leaf(), Expression.Add(Leaf(), Expression.Constant(0.5))));
        Primary(ExpressionType.And, () => Expression.And(IntLeaf(), IntLeaf()));
        Primary(ExpressionType.Or, () => Expression.Or(IntLeaf(), IntLeaf()));
        Primary(ExpressionType.ExclusiveOr, () => Expression.ExclusiveOr(IntLeaf(), IntLeaf()));
        Primary(ExpressionType.LeftShift, () => Expression.LeftShift(IntLeaf(), IntLeaf()));
        Primary(ExpressionType.RightShift, () => Expression.RightShift(IntLeaf(), IntLeaf()));

        // Comparison and logical binary operators.
        Primary(ExpressionType.Equal, () => Expression.Equal(Leaf(), Leaf()));
        Primary(ExpressionType.NotEqual, () => Expression.NotEqual(Leaf(), Leaf()));
        Primary(ExpressionType.GreaterThan, () => Expression.GreaterThan(Leaf(), Expression.Add(Leaf(), Expression.Constant(0.5))));
        Primary(ExpressionType.GreaterThanOrEqual, () => Expression.GreaterThanOrEqual(Leaf(), Leaf()));
        Primary(ExpressionType.LessThan, () => Expression.LessThan(Leaf(), Expression.Add(Leaf(), Expression.Constant(0.5))));
        Primary(ExpressionType.LessThanOrEqual, () => Expression.LessThanOrEqual(Leaf(), Leaf()));
        Primary(ExpressionType.AndAlso, () => Expression.AndAlso(BoolLeaf(), BoolLeaf()));
        Primary(ExpressionType.OrElse, () => Expression.OrElse(BoolLeaf(), BoolLeaf()));
        Primary(ExpressionType.Coalesce, () => Expression.Coalesce(NullableLeaf(), Leaf()));
        Primary(ExpressionType.ArrayIndex, () => Expression.ArrayIndex(Expression.Call(CatalogHelpers.MakeArrayMethod, Leaf()), Expression.Constant(0)));

        // Assignments (the target is the free variable v or i; the runner declares it).
        Primary(ExpressionType.Assign, () => Expression.Assign(Variable, Leaf()));
        Primary(ExpressionType.AddAssign, () => Expression.AddAssign(Variable, Leaf()));
        Primary(ExpressionType.AddAssignChecked, () => Expression.AddAssignChecked(Variable, Leaf()));
        Primary(ExpressionType.SubtractAssign, () => Expression.SubtractAssign(Variable, Leaf()));
        Primary(ExpressionType.SubtractAssignChecked, () => Expression.SubtractAssignChecked(Variable, Leaf()));
        Primary(ExpressionType.MultiplyAssign, () => Expression.MultiplyAssign(Variable, Leaf()));
        Primary(ExpressionType.MultiplyAssignChecked, () => Expression.MultiplyAssignChecked(Variable, Leaf()));
        Primary(ExpressionType.DivideAssign, () => Expression.DivideAssign(Variable, Leaf()));
        Primary(ExpressionType.ModuloAssign, () => Expression.ModuloAssign(Variable, Leaf()));
        Primary(ExpressionType.PowerAssign, () => Expression.PowerAssign(Variable, Leaf()));
        Primary(ExpressionType.AndAssign, () => Expression.AndAssign(IntVariable, IntLeaf()));
        Primary(ExpressionType.OrAssign, () => Expression.OrAssign(IntVariable, IntLeaf()));
        Primary(ExpressionType.ExclusiveOrAssign, () => Expression.ExclusiveOrAssign(IntVariable, IntLeaf()));
        Primary(ExpressionType.LeftShiftAssign, () => Expression.LeftShiftAssign(IntVariable, IntLeaf()));
        Primary(ExpressionType.RightShiftAssign, () => Expression.RightShiftAssign(IntVariable, IntLeaf()));
        Primary(ExpressionType.PreIncrementAssign, () => Expression.PreIncrementAssign(Variable));
        Primary(ExpressionType.PreDecrementAssign, () => Expression.PreDecrementAssign(Variable));
        Primary(ExpressionType.PostIncrementAssign, () => Expression.PostIncrementAssign(Variable));
        Primary(ExpressionType.PostDecrementAssign, () => Expression.PostDecrementAssign(Variable));

        // Unary operators.
        Primary(ExpressionType.Negate, () => Expression.Negate(Leaf()));
        Primary(ExpressionType.NegateChecked, () => Expression.NegateChecked(Leaf()));
        Primary(ExpressionType.UnaryPlus, () => Expression.UnaryPlus(Leaf()));
        Primary(ExpressionType.Not, () => Expression.Not(BoolLeaf()));
        Primary(ExpressionType.OnesComplement, () => Expression.OnesComplement(IntLeaf()));
        Primary(ExpressionType.Increment, () => Expression.Increment(Leaf()));
        Primary(ExpressionType.Decrement, () => Expression.Decrement(Leaf()));
        Primary(ExpressionType.IsTrue, () => Expression.IsTrue(BoolLeaf()));
        Primary(ExpressionType.IsFalse, () => Expression.IsFalse(BoolLeaf()));
        Primary(ExpressionType.Convert, () => Expression.Convert(Leaf(), typeof(int)));
        Primary(ExpressionType.ConvertChecked, () => Expression.ConvertChecked(Leaf(), typeof(int)));
        Primary(ExpressionType.TypeAs, () => Expression.TypeAs(ObjectLeaf(), typeof(double?)));
        Primary(ExpressionType.Unbox, () => Expression.Unbox(ObjectLeaf(), typeof(double)));
        Primary(ExpressionType.ArrayLength, () => Expression.ArrayLength(Expression.Call(CatalogHelpers.MakeArrayMethod, Leaf())));
        Primary(ExpressionType.Quote, () => Expression.Quote(Expression.Lambda<Func<double>>(Leaf())));
        Primary(ExpressionType.Throw, () => Expression.Throw(Expression.Call(CatalogHelpers.MakeExceptionMethod, Leaf()), typeof(double)));

        // Leaves, calls, members, invocation, lambda and conditional.
        Primary(ExpressionType.Constant, () => Expression.Constant(2.5));
        Primary(ExpressionType.Parameter, () => Free);
        Primary(ExpressionType.Default, () => Expression.Default(typeof(double)));
        Primary(ExpressionType.Call, () => Expression.Call(SinMethod, Leaf()));
        Primary(ExpressionType.MemberAccess, () => Expression.Property(Expression.Call(FromSecondsMethod, Leaf()), nameof(TimeSpan.TotalSeconds)));
        Primary(ExpressionType.Invoke, () =>
        {
            ParameterExpression q = Expression.Parameter(typeof(double), "q");
            return Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Add(q, Leaf()), q), Leaf());
        });
        Primary(ExpressionType.Lambda, () => Expression.Lambda<Func<double>>(Leaf()));
        Primary(ExpressionType.Conditional, () => Expression.Condition(BoolLeaf(), Leaf(), Expression.Add(Leaf(), Expression.Constant(0.5))));
        Primary(ExpressionType.TypeIs, () => Expression.TypeIs(ObjectLeaf(), typeof(double)));
        Primary(ExpressionType.TypeEqual, () => Expression.TypeEqual(ObjectLeaf(), typeof(double)));

        // Object and array construction, initializers and indexing.
        Primary(ExpressionType.New, () => Expression.New(StrongBoxConstructor, Leaf()));
        Primary(ExpressionType.NewArrayInit, () => Expression.NewArrayInit(typeof(double), Leaf(), Leaf()));
        Primary(ExpressionType.NewArrayBounds, () => Expression.NewArrayBounds(typeof(double), IntLeaf()));
        Primary(ExpressionType.MemberInit, () => Expression.MemberInit(
            Expression.New(typeof(CatalogHolder).GetConstructor([typeof(double)])!, Leaf()),
            Expression.Bind(typeof(CatalogHolder).GetField(nameof(CatalogHolder.Value))!, Leaf())));
        Primary(ExpressionType.ListInit, () => Expression.ListInit(Expression.New(typeof(List<double>)), Leaf(), Leaf()));
        Primary(ExpressionType.Index, () => Expression.ArrayAccess(Expression.Call(CatalogHelpers.MakeArrayMethod, Leaf()), Expression.Subtract(IntLeaf(), IntLeaf())));

        // Control flow and scopes.
        Primary(ExpressionType.Block, () =>
        {
            ParameterExpression local = Expression.Variable(typeof(double), "local");
            return Expression.Block([local], Expression.Assign(local, Leaf()), Expression.Add(local, Leaf()));
        });
        Primary(ExpressionType.Try, () => Expression.TryCatch(Leaf(), Expression.Catch(typeof(InvalidOperationException), Leaf())));
        Primary(ExpressionType.Switch, () => Expression.Switch(
            IntLeaf(),
            Leaf(),
            Expression.SwitchCase(Leaf(), Expression.Constant(3), IntLeaf())));
        Primary(ExpressionType.Loop, () =>
        {
            LabelTarget breakLabel = Expression.Label(typeof(double), "break");
            LabelTarget continueLabel = Expression.Label("continue");
            return Expression.Loop(
                Expression.Condition(BoolLeaf(), Expression.Break(breakLabel, Leaf()), Expression.Continue(continueLabel), typeof(void)),
                breakLabel,
                continueLabel);
        });
        Primary(ExpressionType.Goto, () => Expression.Return(Expression.Label(typeof(double), "return"), Leaf()));
        Primary(ExpressionType.Label, () => Expression.Label(Expression.Label(typeof(double), "label"), Leaf()));
        Primary(ExpressionType.RuntimeVariables, () => Expression.RuntimeVariables(Free, Variable));
        Primary(ExpressionType.DebugInfo, () => Expression.DebugInfo(Document, 1, 1, 1, 5));

        // Special and extensible nodes.
        Primary(ExpressionType.Dynamic, () => Expression.Dynamic(new CatalogBinder(), typeof(object), ObjectLeaf()));
        Primary(ExpressionType.Extension, () => new ReducibleCatalogExtension(Leaf()));

        // Metadata variants (R1).
        Variant("Add.CustomMethod", ExpressionType.Add, () => Expression.Add(Leaf(), Leaf(), CatalogHelpers.PlusMethod));
        Variant("Add.Lifted", ExpressionType.Add, () => Expression.Add(NullableLeaf(), NullableLeaf()));
        Variant("Equal.LiftedToNull", ExpressionType.Equal, () => Expression.Equal(NullableLeaf(), NullableLeaf(), true, null));
        Variant("Coalesce.Conversion", ExpressionType.Coalesce, () =>
        {
            ParameterExpression n = Expression.Parameter(typeof(double?), "n");
            return Expression.Coalesce(NullableLeaf(), Leaf(), Expression.Lambda<Func<double?, double>>(Expression.Multiply(Expression.Convert(n, typeof(double)), Expression.Constant(2.5)), n));
        });
        Variant("AddAssign.CustomMethod", ExpressionType.AddAssign, () => Expression.AddAssign(Variable, Leaf(), CatalogHelpers.PlusMethod));
        Variant("Negate.CustomMethod", ExpressionType.Negate, () => Expression.Negate(Leaf(), CatalogHelpers.FortyTwoMethod));
        Variant("Convert.CustomMethod", ExpressionType.Convert, () => Expression.Convert(Leaf(), typeof(int), CatalogHelpers.TruncateMethod));
        Variant("PreIncrementAssign.CustomMethod", ExpressionType.PreIncrementAssign, () => Expression.PreIncrementAssign(Variable, CatalogHelpers.FortyTwoMethod));
        Variant("Throw.Rethrow", ExpressionType.Throw, () => Expression.Rethrow(typeof(double)));
        Variant("Call.InstanceReceiver", ExpressionType.Call, () => Expression.Call(
            Expression.Call(FromSecondsMethod, Leaf()),
            typeof(TimeSpan).GetMethod(nameof(TimeSpan.Add), [typeof(TimeSpan)])!,
            Expression.Call(FromSecondsMethod, Leaf())));
        Variant("MemberAccess.Static", ExpressionType.MemberAccess, () => Expression.Field(null, typeof(string).GetField(nameof(string.Empty))!));
        Variant("Invoke.Delegate", ExpressionType.Invoke, () => Expression.Invoke(Expression.Constant(CatalogHelpers.Twice), Leaf()));
        Variant("Lambda.Metadata", ExpressionType.Lambda, () =>
        {
            ParameterExpression x = Expression.Parameter(typeof(double), "x");
            return Expression.Lambda<CatalogDelegate>(Expression.Add(x, Leaf()), "named", true, [x]);
        });
        Variant("New.Members", ExpressionType.New, () => Expression.New(
            typeof(Tuple<double>).GetConstructor([typeof(double)])!,
            [Leaf()],
            [typeof(Tuple<double>).GetProperty(nameof(Tuple<double>.Item1))!]));
        Variant("New.ValueTypeDefault", ExpressionType.New, () => Expression.New(typeof(DateTime)));

        // Binding and control-flow variants (R2).
        Variant("Block.TypedResult", ExpressionType.Block, () => Expression.Block(typeof(object), ObjectLeaf()));
        Variant("Block.Shadowing", ExpressionType.Block, () =>
        {
            ParameterExpression local = Expression.Variable(typeof(double), "local");
            return Expression.Block([local], Expression.Assign(local, Leaf()), Expression.Block([local], Expression.Assign(local, Leaf()), local));
        });
        Variant("Block.DebugInfo", ExpressionType.Block, () => Expression.Block(Expression.DebugInfo(Document, 2, 1, 2, 9), Leaf()));
        Variant("Block.GotoLabel", ExpressionType.Block, () =>
        {
            LabelTarget target = Expression.Label(typeof(double), "exit");
            return Expression.Block(Expression.Return(target, Leaf()), Expression.Label(target, Leaf()));
        });
        Variant("Block.RuntimeVariables", ExpressionType.Block, () =>
        {
            ParameterExpression local = Expression.Variable(typeof(double), "local");
            return Expression.Block([local], Expression.Assign(local, Leaf()), Expression.RuntimeVariables(local));
        });
        Variant("Try.CatchVariableFilter", ExpressionType.Try, () =>
        {
            ParameterExpression exception = Expression.Variable(typeof(InvalidOperationException), "ex");
            return Expression.TryCatch(Leaf(), Expression.Catch(exception, Leaf(), Expression.AndAlso(Expression.NotEqual(exception, Expression.Constant(null, typeof(InvalidOperationException))), BoolLeaf())));
        });
        Variant("Try.Finally", ExpressionType.Try, () => Expression.TryFinally(Leaf(), ObjectLeaf()));
        Variant("Try.Fault", ExpressionType.Try, () => Expression.TryFault(Leaf(), ObjectLeaf()));
        Variant("Try.CatchFinally", ExpressionType.Try, () => Expression.TryCatchFinally(Leaf(), ObjectLeaf(), Expression.Catch(typeof(Exception), Leaf())));
        Variant("Switch.ComparisonMethod", ExpressionType.Switch, () => Expression.Switch(
            Leaf(),
            Leaf(),
            CatalogHelpers.NearlyEqualMethod,
            Expression.SwitchCase(Leaf(), Expression.Add(Leaf(), Expression.Constant(0.5)))));
        Variant("Goto.Goto", ExpressionType.Goto, () => Expression.Goto(Expression.Label("goto")));

        // Initializer variants (R3).
        Variant("MemberInit.MemberMemberBinding", ExpressionType.MemberInit, () => Expression.MemberInit(
            Expression.New(typeof(CatalogHolder)),
            Expression.MemberBind(typeof(CatalogHolder).GetField(nameof(CatalogHolder.Box))!, Expression.Bind(StrongBoxValueField, Leaf()))));
        Variant("MemberInit.MemberListBinding", ExpressionType.MemberInit, () => Expression.MemberInit(
            Expression.New(typeof(CatalogHolder)),
            Expression.ListBind(typeof(CatalogHolder).GetField(nameof(CatalogHolder.Items))!, Expression.ElementInit(ListAddMethod, Leaf()))));
        Variant("ListInit.ExplicitAddMethod", ExpressionType.ListInit, () => Expression.ListInit(
            Expression.New(typeof(List<double>).GetConstructor([typeof(int)])!, IntLeaf()),
            ListAddMethod,
            Leaf()));
        Variant("Index.Indexer", ExpressionType.Index, () => Expression.MakeIndex(
            Expression.ListInit(Expression.New(typeof(List<double>)), Leaf(), Leaf()),
            typeof(List<double>).GetProperty("Item")!,
            [Expression.Subtract(IntLeaf(), IntLeaf())]));

        // Special-node variants (R4).
        Variant("Extension.Irreducible", ExpressionType.Extension, () => new IrreducibleCatalogExtension(Leaf()));
        Variant("Quote.CapturedOuterParameter", ExpressionType.Quote, () =>
        {
            ParameterExpression x = Expression.Parameter(typeof(double), "x");
            return Expression.Quote(Expression.Lambda<Func<double, double>>(Expression.Add(x, Leaf()), x));
        });

        return samples;
    }
}

/// <summary>A delegate type distinct from <see cref="Func{T, TResult}"/>, used to detect delegate-type loss.</summary>
/// <param name="value">The input value.</param>
/// <returns>The computed value.</returns>
internal delegate double CatalogDelegate(double value);

/// <summary>Static helpers referenced by catalog samples (custom operators, factories, comparison methods).</summary>
internal static class CatalogHelpers
{
    /// <summary>The <see cref="Plus"/> method, used as an explicit binary operator method.</summary>
    public static readonly MethodInfo PlusMethod = typeof(CatalogHelpers).GetMethod(nameof(Plus))!;

    /// <summary>The <see cref="FortyTwo"/> method, used as an explicit unary operator method.</summary>
    public static readonly MethodInfo FortyTwoMethod = typeof(CatalogHelpers).GetMethod(nameof(FortyTwo))!;

    /// <summary>The <see cref="Truncate"/> method, used as an explicit conversion method.</summary>
    public static readonly MethodInfo TruncateMethod = typeof(CatalogHelpers).GetMethod(nameof(Truncate))!;

    /// <summary>The <see cref="MakeArray"/> method.</summary>
    public static readonly MethodInfo MakeArrayMethod = typeof(CatalogHelpers).GetMethod(nameof(MakeArray))!;

    /// <summary>The <see cref="MakeException"/> method.</summary>
    public static readonly MethodInfo MakeExceptionMethod = typeof(CatalogHelpers).GetMethod(nameof(MakeException))!;

    /// <summary>The <see cref="NearlyEqual"/> method, used as an explicit switch comparison method.</summary>
    public static readonly MethodInfo NearlyEqualMethod = typeof(CatalogHelpers).GetMethod(nameof(NearlyEqual))!;

    /// <summary>A delegate invoked by the delegate-invocation sample.</summary>
    public static readonly Func<double, double> Twice = value => value * 2;

    /// <summary>A custom addition operator whose result differs from the predefined one.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><c>left + right + 100</c>.</returns>
    public static double Plus(double left, double right) => left + right + 100;

    /// <summary>A custom negation operator whose result differs from the predefined one.</summary>
    /// <param name="value">The operand (ignored).</param>
    /// <returns>Always 42.</returns>
    public static double FortyTwo(double value) => 42;

    /// <summary>A custom conversion that truncates and adds 1000, so it differs from the predefined conversion.</summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted value.</returns>
    public static int Truncate(double value) => (int)value + 1000;

    /// <summary>Creates a two-element array whose elements both equal <paramref name="value"/>.</summary>
    /// <param name="value">The element value.</param>
    /// <returns>The new array.</returns>
    public static double[] MakeArray(double value) => [value, value];

    /// <summary>Creates an exception whose message carries <paramref name="value"/>.</summary>
    /// <param name="value">The value recorded in the message.</param>
    /// <returns>The new exception.</returns>
    public static Exception MakeException(double value) => new InvalidOperationException(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>A custom switch comparison that ignores differences smaller than 0.25.</summary>
    /// <param name="switchValue">The switch value.</param>
    /// <param name="caseValue">The case test value.</param>
    /// <returns><see langword="true"/> when the values differ by less than 0.25.</returns>
    public static bool NearlyEqual(double switchValue, double caseValue) => Math.Abs(switchValue - caseValue) < 0.25;
}

/// <summary>A mutable holder used by the member-initialization samples.</summary>
internal sealed class CatalogHolder
{
    /// <summary>A plain value field.</summary>
    public double Value;

    /// <summary>A nested object field, initialized by member-member bindings.</summary>
    public StrongBox<double> Box = new();

    /// <summary>A nested list field, initialized by member-list bindings.</summary>
    public List<double> Items = [];

    /// <summary>Initializes an empty holder.</summary>
    public CatalogHolder() { }

    /// <summary>Initializes a holder whose <see cref="Value"/> starts at <paramref name="seed"/>.</summary>
    /// <param name="seed">The initial value.</param>
    public CatalogHolder(double seed) => Value = seed;

    /// <inheritdoc/>
    public override string ToString() => $"Holder({Value},{Box.Value},[{string.Join(",", Items)}])";
}

/// <summary>A call-site binder that never binds; dynamic samples are only built, compared and transformed.</summary>
internal sealed class CatalogBinder : CallSiteBinder
{
    /// <inheritdoc/>
    public override Expression Bind(object[] args, System.Collections.ObjectModel.ReadOnlyCollection<ParameterExpression> parameters, LabelTarget returnLabel)
        => throw new NotSupportedException("The ETC catalog binder never binds.");
}

/// <summary>A reducible extension node with one child; reduces to that child.</summary>
internal sealed class ReducibleCatalogExtension : Expression
{
    /// <summary>Initializes the node.</summary>
    /// <param name="child">The single child, also the reduction result.</param>
    public ReducibleCatalogExtension(Expression child) => Child = child;

    /// <summary>Gets the single child.</summary>
    public Expression Child { get; }

    /// <inheritdoc/>
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc/>
    public override Type Type => typeof(double);

    /// <inheritdoc/>
    public override bool CanReduce => true;

    /// <inheritdoc/>
    public override Expression Reduce() => Child;

    /// <inheritdoc/>
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        Expression child = visitor.Visit(Child);
        return ReferenceEquals(child, Child) ? this : new ReducibleCatalogExtension(child);
    }
}

/// <summary>An irreducible extension node with one child; it cannot be compiled.</summary>
internal sealed class IrreducibleCatalogExtension : Expression
{
    /// <summary>Initializes the node.</summary>
    /// <param name="child">The single child.</param>
    public IrreducibleCatalogExtension(Expression child) => Child = child;

    /// <summary>Gets the single child.</summary>
    public Expression Child { get; }

    /// <inheritdoc/>
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc/>
    public override Type Type => typeof(double);

    /// <inheritdoc/>
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        Expression child = visitor.Visit(Child);
        return ReferenceEquals(child, Child) ? this : new IrreducibleCatalogExtension(child);
    }
}
