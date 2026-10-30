using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R3, checkpoint B: capture-avoiding substitution through the initializer and index families (R2 scope model) and the
/// conservative inlining of invocations whose body holds them. Equality, hashing and canonical keys are in
/// <see cref="ExpressionTreeR3EqualityTests"/>.
/// </summary>
[TestClass]
[TestCategory("ETC-R3")]
public class ExpressionTreeR3SubstitutionTests
{
    /// <summary>A free parameter shared by the samples.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary>A second free parameter.</summary>
    private static readonly ParameterExpression Q = Expression.Parameter(typeof(double), "q");

    /// <summary>A free trace parameter, so the samples hold no constant (constants are compared by value, which is also fine).</summary>
    private static readonly ParameterExpression Log = Expression.Parameter(typeof(R3TraceLog), "log");

    /// <summary>Exposes the protected <c>ReplaceArguments</c>.</summary>
    private sealed class ExposedTransformer : ExpressionTransformer
    {
        /// <summary>Calls <c>ReplaceArguments</c>.</summary>
        /// <param name="e">The tree.</param>
        /// <param name="parameter">The parameter to replace.</param>
        /// <param name="replacement">The replacement.</param>
        /// <returns>The substituted tree.</returns>
        public Expression Replace(Expression e, ParameterExpression parameter, Expression replacement) => ReplaceArguments(e, [parameter], [replacement]);

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);
    }

    /// <summary>Replaces <paramref name="parameter"/> by <paramref name="replacement"/> in <paramref name="e"/>.</summary>
    /// <param name="e">The tree.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="replacement">The replacement.</param>
    /// <returns>The substituted tree.</returns>
    private static Expression Replace(Expression e, ParameterExpression parameter, Expression replacement) => new ExposedTransformer().Replace(e, parameter, replacement);

    /// <summary>Gets a public member of <see cref="R3TracedHolder"/>.</summary>
    /// <param name="name">The member name.</param>
    /// <returns>The member.</returns>
    private static System.Reflection.MemberInfo M(string name) => R3TracedHolder.Member(name);

    /// <summary>Builds <c>new R3TracedHolder(log)</c> over <see cref="Log"/>.</summary>
    /// <returns>The creation.</returns>
    private static NewExpression NewHolder() => Expression.New(typeof(R3TracedHolder).GetConstructor([typeof(R3TraceLog)])!, Log);

    /// <summary>Builds <c>new R3TracedList(log)</c> over <see cref="Log"/>.</summary>
    /// <returns>The creation.</returns>
    private static NewExpression NewList() => Expression.New(typeof(R3TracedList).GetConstructor([typeof(R3TraceLog)])!, Log);

    /// <summary>Builds an <c>Add</c> initializer of <see cref="R3TracedList"/>, selecting the overload by the argument types.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The initializer.</returns>
    private static ElementInit Add(params Expression[] arguments)
        => Expression.ElementInit(R3TracedList.AddMethod(arguments.Select(argument => argument.Type).ToArray()), arguments);

    /// <summary>Runs <paramref name="body"/> over <see cref="Log"/> and the given value of <see cref="P"/>, and describes its result.</summary>
    /// <param name="body">The body (it may also use <see cref="Q"/>, bound to <c>10</c>).</param>
    /// <param name="p">The value of <see cref="P"/>.</param>
    /// <returns>The result description and the trace.</returns>
    private static (string Result, string[] Trace) Run(Expression body, double p = 1.0)
    {
        var log = new R3TraceLog();
        Func<R3TraceLog, double, double, object> compiled = Expression.Lambda<Func<R3TraceLog, double, double, object>>(Expression.Convert(body, typeof(object)), Log, P, Q).Compile();
        object result = compiled(log, p, 10.0);
        string description = result switch
        {
            R3TracedHolder holder => holder.Describe(),
            R3TracedList list => "[" + list.Describe() + "]",
            double number => R3TraceLog.Text(number),
            _ => result?.ToString() ?? "null",
        };
        return (description, log.Entries.ToArray());
    }

    /// <summary>Asserts that two runs produced the same result and the same trace.</summary>
    /// <param name="expected">The expected run.</param>
    /// <param name="actual">The actual run.</param>
    /// <param name="message">The case name.</param>
    private static void AssertSameRun((string Result, string[] Trace) expected, (string Result, string[] Trace) actual, string message)
    {
        Assert.AreEqual(expected.Result, actual.Result, message);
        CollectionAssert.AreEqual(expected.Trace, actual.Trace, message);
    }

    // ------------------------------------------------------------------------------------------
    // Substitution (ETC-03, R2 scope model).
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Substitution reaches the value of a member assignment, nested member bindings, the arguments of list bindings and list
    /// initializers, and the receiver and arguments of an index access; members, binding kinds, <c>Add</c> methods and the
    /// indexer are kept, and the result runs like the original with the value substituted.
    /// </summary>
    [TestMethod]
    public void Substitution_ReachesEveryBindingAndInitializer()
    {
        Expression tree = Expression.MemberInit(
            NewHolder(),
            Expression.Bind(M(nameof(R3TracedHolder.Value)), P),
            Expression.MemberBind(M(nameof(R3TracedHolder.Child)), Expression.MemberBind(M(nameof(R3TracedHolder.Child)), Expression.Bind(M(nameof(R3TracedHolder.Other)), Expression.Add(P, Q)))),
            Expression.ListBind(M(nameof(R3TracedHolder.Items)), Add(P), Add(P, Q)));

        var replaced = (MemberInitExpression)Replace(tree, P, Expression.Constant(5.0));

        AssertSameRun(Run(tree, 5.0), Run(replaced, 99.0), "Substituting 5 for p must be the same as running with p = 5.");
        Assert.AreSame(((MemberInitExpression)tree).NewExpression, replaced.NewExpression, "An unchanged creation is reused.");
        CollectionAssert.AreEqual(
            ((MemberInitExpression)tree).Bindings.Select(binding => (binding.BindingType, binding.Member)).ToArray(),
            replaced.Bindings.Select(binding => (binding.BindingType, binding.Member)).ToArray());
        Assert.AreSame(R3TracedList.AddMethod(typeof(double), typeof(double)), ((MemberListBinding)replaced.Bindings[2]).Initializers[1].AddMethod);

        Expression list = Expression.ListInit(NewList(), Add(P), Add(Q, P), Add(Expression.Convert(P, typeof(int))));
        AssertSameRun(Run(list, 2.0), Run(Replace(list, P, Expression.Constant(2.0)), 99.0), "list initialization");

        // Without any unary or binary node (which the substitution has always rebuilt), nothing to replace means the same instance.
        Expression plain = Expression.ListInit(NewList(), Add(P), Add(Q, P));
        Assert.AreSame(plain, Replace(plain, Expression.Parameter(typeof(double), "unrelated"), Expression.Constant(2.0)), "Nothing to replace: same instance.");
    }

    /// <summary>
    /// Substitution reaches the receiver and the arguments of an index access, read or written: the access stays writable as
    /// an assignment target, with the same indexer.
    /// </summary>
    [TestMethod]
    public void Substitution_ReachesIndexReceiversAndArguments_AndKeepsTargetsWritable()
    {
        ParameterExpression grid = Expression.Parameter(typeof(R3TracedGrid), "grid");
        ParameterExpression row = Expression.Parameter(typeof(int), "row");
        IndexExpression cell = Expression.MakeIndex(grid, R3TracedGrid.CellIndexer, [row, Expression.Constant(2)]);
        Expression write = Expression.AddAssign(cell, P);

        var log = new R3TraceLog();
        var instance = new R3TracedGrid(log);
        var replaced = (BinaryExpression)Replace(Replace(write, grid, Expression.Constant(instance)), row, Expression.Constant(1));
        var target = (IndexExpression)replaced.Left;
        Assert.AreSame(R3TracedGrid.CellIndexer, target.Indexer);
        Assert.AreEqual(instance, ((ConstantExpression)target.Object!).Value);
        Assert.AreEqual(1, ((ConstantExpression)target.Arguments[0]).Value);

        Func<double, double> compiled = Expression.Lambda<Func<double, double>>(replaced, P).Compile();
        Assert.AreEqual(4.0, compiled(4.0));
        CollectionAssert.AreEqual(new[] { "get[1,2]", "set[1,2]=4" }, log.Entries);

        // A replacement that is itself an index access is a valid assignment target.
        ParameterExpression variable = Expression.Variable(typeof(double), "v");
        var assignment = (BinaryExpression)Replace(Expression.Assign(variable, P), variable, Expression.ArrayAccess(Expression.Constant(new double[2]), Expression.Constant(1)));
        Assert.IsInstanceOfType<IndexExpression>(assignment.Left);

        // A replacement that is not writable is refused instead of producing an invalid tree.
        Assert.ThrowsExactly<ArgumentException>(() => Replace(Expression.Assign(variable, P), variable, Expression.Constant(1.0)));
    }

    /// <summary>
    /// Only free occurrences are replaced: a block redeclaring the parameter hides it inside the initialization, and a lambda
    /// holding an initialization sees the free occurrence replaced.
    /// </summary>
    [TestMethod]
    public void Substitution_ReplacesFreeOccurrencesOnly()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression underLambda = Expression.Lambda<Func<double, R3TracedHolder>>(
            Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Value)), Expression.Add(P, x))), x);
        var replaced = (Expression<Func<double, R3TracedHolder>>)Replace(underLambda, P, Expression.Constant(5.0));
        Func<R3TraceLog, Func<double, R3TracedHolder>> outer = Expression.Lambda<Func<R3TraceLog, Func<double, R3TracedHolder>>>(replaced, Log).Compile();
        Assert.AreEqual(7.0, GetValue(outer(new R3TraceLog())(2.0)));

        Expression shadowed = Expression.Block(
            [P],
            Expression.Assign(P, Expression.Constant(3.0)),
            Expression.ListInit(NewList(), Add(P)));
        var replacedBlock = (BlockExpression)Replace(shadowed, P, Expression.Constant(5.0));
        CollectionAssert.AreEqual(new[] { P }, replacedBlock.Variables.ToArray(), "The block keeps its declaration.");
        Assert.AreSame(P, ((ListInitExpression)replacedBlock.Expressions[1]).Initializers[0].Arguments[0], "The block redeclares p: the occurrence is bound, not free.");
        Assert.AreEqual("[3]", Run(replacedBlock).Result);
    }

    /// <summary>Reads <see cref="R3TracedHolder.Value"/> without tracing into the asserted trace.</summary>
    /// <param name="holder">The holder.</param>
    /// <returns>The value.</returns>
    private static double GetValue(R3TracedHolder holder) => holder.Value;

    /// <summary>
    /// Capture is avoided: replacing a free parameter by an expression mentioning a variable that a nested lambda or a catch
    /// handler redeclares, around an initialization, alpha-renames that declaration instead of capturing the replacement.
    /// </summary>
    [TestMethod]
    public void Substitution_AvoidsCaptureAroundInitializations()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression lambda = Expression.Lambda<Func<double, R3TracedHolder>>(
            Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Value)), Expression.Add(P, Expression.Multiply(x, Expression.Constant(10.0))))), x);

        // p := x (the outer x, free): the nested lambda's own x must be renamed, otherwise it would capture the replacement.
        var replaced = (LambdaExpression)Replace(lambda, P, x);
        Assert.AreNotSame(x, replaced.Parameters[0], "The capturing declaration is renamed.");
        Func<double, R3TraceLog, Func<double, R3TracedHolder>> compiled =
            Expression.Lambda<Func<double, R3TraceLog, Func<double, R3TracedHolder>>>(replaced, x, Log).Compile();
        Assert.AreEqual(1.0 + 20.0, GetValue(compiled(1.0, new R3TraceLog())(2.0)));

        ParameterExpression error = Expression.Variable(typeof(Exception), "error");
        ParameterExpression message = Expression.Parameter(typeof(Exception), "message");
        Expression guarded = Expression.TryCatch(
            Expression.Throw(Expression.Constant(new InvalidOperationException("inner")), typeof(R3TracedList)),
            Expression.Catch(error, Expression.ListInit(NewList(), Add(Expression.Property(message, nameof(Exception.Message))), Add(Expression.Property(error, nameof(Exception.Message))))));
        var replacedTry = (TryExpression)Replace(guarded, message, error);
        Assert.AreNotSame(error, replacedTry.Handlers[0].Variable, "The catch variable is renamed.");
        Func<Exception, R3TraceLog, R3TracedList> run = Expression.Lambda<Func<Exception, R3TraceLog, R3TracedList>>(replacedTry, error, Log).Compile();
        Assert.AreEqual("\"outer\",\"inner\"", run(new Exception("outer"), new R3TraceLog()).Describe());
    }

    // ------------------------------------------------------------------------------------------
    // Conservative inlining.
    // ------------------------------------------------------------------------------------------

    /// <summary>Simplifies <paramref name="body"/> and reports whether an invocation remains.</summary>
    /// <param name="body">The body.</param>
    /// <param name="simplified">The simplified body.</param>
    /// <returns><see langword="true"/> if the simplified body still holds an invocation.</returns>
    private static bool KeepsInvocation(Expression body, out Expression simplified)
    {
        simplified = new ExpressionSimplifier().Simplify(body);
        return new InvocationFinder().Find(simplified);
    }

    /// <summary>Finds an invocation node.</summary>
    private sealed class InvocationFinder : ExpressionVisitor
    {
        /// <summary>Whether an invocation was found.</summary>
        private bool _found;

        /// <summary>Searches a tree.</summary>
        /// <param name="e">The tree.</param>
        /// <returns><see langword="true"/> if it holds an invocation.</returns>
        public bool Find(Expression e)
        {
            Visit(e);
            return _found;
        }

        /// <inheritdoc/>
        protected override Expression VisitInvocation(InvocationExpression node)
        {
            _found = true;
            return node;
        }
    }

    /// <summary>
    /// Inlining into initializations stays conservative: an argument with effects, a variable argument read after a setter,
    /// an <c>Add</c> call or a write, and a variable argument read twice across user code keep the invocation; constant
    /// arguments, and a variable read before any user code, are inlined. Every result runs exactly like the original.
    /// </summary>
    [TestMethod]
    public void Inlining_IntoInitializations_IsConservative()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression v = Expression.Variable(typeof(double), "v");
        Expression Invoke(Expression body, Expression argument) => Expression.Invoke(Expression.Lambda(body, a), argument);
        Expression InBlock(Expression body) => Expression.Block([v], Expression.Assign(v, P), body);

        var cases = new (string Name, Expression Tree, bool Inlined)[]
        {
            ("effect argument", Invoke(Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Value)), a)), Expression.Call(Log, R3TraceLog.TagMethod(typeof(double)), Expression.Constant("arg"), P)), false),
            ("unused effect argument", Invoke(Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Value)), Expression.Constant(1.0))), Expression.Call(Log, R3TraceLog.TagMethod(typeof(double)), Expression.Constant("arg"), P)), false),
            ("constant used in two bindings", Invoke(Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Value)), a), Expression.Bind(M(nameof(R3TracedHolder.Other)), a)), Expression.Constant(3.0)), true),
            ("variable used in two bindings", InBlock(Invoke(Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Value)), a), Expression.Bind(M(nameof(R3TracedHolder.Other)), a)), v)), false),
            ("variable used in two Add calls", InBlock(Invoke(Expression.ListInit(NewList(), Add(a), Add(a)), v)), false),
            ("variable read after a write", InBlock(Invoke(Expression.MemberInit(NewHolder(), Expression.Bind(M(nameof(R3TracedHolder.Value)), Expression.Assign(v, Expression.Constant(5.0))), Expression.Bind(M(nameof(R3TracedHolder.Other)), a)), v)), false),
            ("variable read after a throwing Add", InBlock(Invoke(Expression.ListInit(NewList(), Add(Expression.Constant(-1)), Add(a)), v)), false),
            ("variable read first, in the creation", InBlock(Invoke(Expression.MemberInit(Expression.New(typeof(R3TracedHolder).GetConstructor([typeof(R3TraceLog), typeof(double)])!, Log, a), Expression.Bind(M(nameof(R3TracedHolder.Other)), Expression.Constant(2.0))), v)), true),
            ("variable as the index of an assignment target", InBlock(Invoke(Expression.Assign(Expression.ArrayAccess(Expression.Constant(new double[4]), Expression.Convert(a, typeof(int))), Expression.Constant(7.0)), v)), true),
            ("variable read after an indexer write", InBlock(Invoke(Expression.Add(Expression.Assign(Expression.MakeIndex(Expression.Constant(new R3TracedGrid(new R3TraceLog())), R3TracedGrid.NameIndexer, [Expression.Constant("k")]), Expression.Constant(7.0)), a), v)), false),
        };

        foreach ((string name, Expression tree, bool inlined) in cases)
        {
            Assert.AreEqual(!inlined, KeepsInvocation(tree, out Expression simplified), name);
            string? expectedException = null;
            string? actualException = null;
            (string Result, string[] Trace) expected = default;
            (string Result, string[] Trace) actual = default;
            try { expected = Run(tree); } catch (Exception exception) { expectedException = exception.GetType().FullName; }
            try { actual = Run(simplified); } catch (Exception exception) { actualException = exception.GetType().FullName; }
            Assert.AreEqual(expectedException, actualException, name);
            Assert.AreEqual(expected.Result, actual.Result, name);
            CollectionAssert.AreEqual(expected.Trace, actual.Trace, name);
        }
    }
}
