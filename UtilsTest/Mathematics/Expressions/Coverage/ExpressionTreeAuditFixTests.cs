using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>A mutable structure: a write through a variable of this type changes that variable only.</summary>
public struct AuditFixMutable
{
    /// <summary>A mutable field.</summary>
    public double F;

    /// <summary>A nested mutable structure.</summary>
    public AuditFixInner Inner;

    /// <summary>Gets or sets <see cref="F"/> through an indexer.</summary>
    /// <param name="index">Ignored.</param>
    /// <returns><see cref="F"/>.</returns>
    public double this[int index]
    {
        readonly get => F;
        set => F = value;
    }

    /// <summary>Gets <see cref="F"/> after incrementing it (a mutating getter).</summary>
    public double Next => ++F;

    /// <summary>Gets <see cref="F"/> (a <see langword="readonly"/> getter).</summary>
    public readonly double Current => F;

    /// <summary>Increments <see cref="F"/> (a mutating instance method).</summary>
    public void Bump() => F += 1;

    /// <summary>Returns <see cref="F"/> (a <see langword="readonly"/> instance method).</summary>
    /// <returns><see cref="F"/>.</returns>
    public readonly double Read() => F;

    /// <summary>Writes through a by-reference parameter.</summary>
    /// <param name="target">The written location.</param>
    /// <param name="value">The value.</param>
    public static void SetRef(ref double target, double value) => target = value;
}

/// <summary>A nested mutable structure.</summary>
public struct AuditFixInner
{
    /// <summary>A mutable field.</summary>
    public double G;
}

/// <summary>A mutable class: a write through a variable of this type changes the shared object.</summary>
public sealed class AuditFixBox
{
    /// <summary>A mutable field.</summary>
    public double F;
}

/// <summary>
/// ETC closure audit, corrections. AUD-01: inlining an invocation whose body writes through a value-type parameter (a field,
/// a nested field or an indexer element assigned or incremented, a field passed by reference, a mutating method or getter)
/// redirected the write from the parameter's copy to the argument (a caller variable, or a constant's temporary), so
/// <c>Simplify</c> changed the result. AUD-02: the free-variable walk treated a dynamic operation as an extension node, so
/// <c>CopyExpression</c> refused to alpha-rename a lambda whose body holds one.
/// </summary>
[TestClass]
[TestCategory("ETC-Audit")]
public class ExpressionTreeAuditFixTests
{
    /// <summary>The field <see cref="AuditFixMutable.F"/>.</summary>
    private static readonly FieldInfo FieldF = typeof(AuditFixMutable).GetField(nameof(AuditFixMutable.F))!;

    /// <summary>The value-type parameter of the invoked lambdas.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(AuditFixMutable), "p");

    /// <summary>A caller variable passed as the argument.</summary>
    private static readonly ParameterExpression S = Expression.Variable(typeof(AuditFixMutable), "s");

    /// <summary>Exposes the protected <c>CopyExpression</c> and <c>ReplaceArguments</c>.</summary>
    private sealed class ExposedTransformer : ExpressionTransformer
    {
        /// <summary>Calls <c>CopyExpression</c>.</summary>
        /// <param name="e">The node.</param>
        /// <param name="parameters">The children.</param>
        /// <returns>The copy.</returns>
        public static Expression Copy(Expression e, params Expression[] parameters) => CopyExpression(e, parameters);

        /// <summary>Calls <c>ReplaceArguments</c>.</summary>
        /// <param name="e">The tree.</param>
        /// <param name="parameter">The replaced parameter.</param>
        /// <param name="replacement">The replacement.</param>
        /// <returns>The substituted tree.</returns>
        public Expression Replace(Expression e, ParameterExpression parameter, Expression replacement) => ReplaceArguments(e, [parameter], [replacement]);

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);
    }

    /// <summary>Finds invocations.</summary>
    private sealed class InvocationFinder : ExpressionVisitor
    {
        /// <summary>Gets whether an invocation was found.</summary>
        public bool Found { get; private set; }

        /// <inheritdoc/>
        protected override Expression VisitInvocation(InvocationExpression node)
        {
            Found = true;
            return node;
        }
    }

    /// <summary>Tests whether a tree still holds an invocation.</summary>
    /// <param name="e">The tree.</param>
    /// <returns><see langword="true"/> if an invocation remains.</returns>
    private static bool HasInvocation(Expression e)
    {
        var finder = new InvocationFinder();
        finder.Visit(e);
        return finder.Found;
    }

    /// <summary>The bodies writing through the value-type parameter <see cref="P"/>, each followed by a read of <c>p.F</c>.</summary>
    /// <returns>The named write shapes.</returns>
    private static IEnumerable<(string Name, Expression Write)> Writes()
    {
        FieldInfo inner = typeof(AuditFixMutable).GetField(nameof(AuditFixMutable.Inner))!;
        FieldInfo g = typeof(AuditFixInner).GetField(nameof(AuditFixInner.G))!;
        yield return ("field assignment", Expression.Assign(Expression.Field(P, FieldF), Expression.Constant(5.0)));
        yield return ("field compound assignment", Expression.AddAssign(Expression.Field(P, FieldF), Expression.Constant(5.0)));
        yield return ("field increment", Expression.PreIncrementAssign(Expression.Field(P, FieldF)));
        yield return ("nested field assignment", Expression.Assign(Expression.Field(Expression.Field(P, inner), g), Expression.Constant(5.0)));
        yield return ("indexer assignment", Expression.Assign(Expression.MakeIndex(P, typeof(AuditFixMutable).GetProperty("Item")!, [Expression.Constant(0)]), Expression.Constant(5.0)));
        yield return ("field by reference", Expression.Call(typeof(AuditFixMutable).GetMethod(nameof(AuditFixMutable.SetRef))!, Expression.Field(P, FieldF), Expression.Constant(5.0)));
        yield return ("mutating method", Expression.Call(P, typeof(AuditFixMutable).GetMethod(nameof(AuditFixMutable.Bump))!));
        yield return ("mutating getter", Expression.Property(P, nameof(AuditFixMutable.Next)));
    }

    /// <summary>The arguments: a caller variable, a constant and a default value.</summary>
    /// <returns>The named arguments.</returns>
    private static IEnumerable<(string Name, Expression Argument)> Arguments()
    {
        yield return ("variable", S);
        yield return ("constant", Expression.Constant(new AuditFixMutable()));
        yield return ("default", Expression.Default(typeof(AuditFixMutable)));
    }

    /// <summary>
    /// Builds <c>{ var s; r1 = ((p) =&gt; { write; p.F + p.Inner.G })(argument); r1 * 1000 + s.F + s.Inner.G }</c>: the
    /// result shows both the value read inside the lambda and the state of the caller variable afterwards.
    /// </summary>
    /// <param name="write">The write through <see cref="P"/>.</param>
    /// <param name="argument">The argument.</param>
    /// <returns>The lambda to simplify.</returns>
    private static Expression<Func<double>> Program(Expression write, Expression argument)
    {
        FieldInfo inner = typeof(AuditFixMutable).GetField(nameof(AuditFixMutable.Inner))!;
        FieldInfo g = typeof(AuditFixInner).GetField(nameof(AuditFixInner.G))!;
        Expression ReadAll(Expression target) => Expression.Add(Expression.Field(target, FieldF), Expression.Field(Expression.Field(target, inner), g));
        var invoked = Expression.Lambda<Func<AuditFixMutable, double>>(Expression.Block(write, ReadAll(P)), P);
        return Expression.Lambda<Func<double>>(Expression.Block(
            [S],
            Expression.Add(Expression.Multiply(Expression.Invoke(invoked, argument), Expression.Constant(1000.0)), ReadAll(S))));
    }

    /// <summary>
    /// AUD-01: every write through a value-type parameter keeps the invocation, for a variable, a constant and a default
    /// argument, and the simplified tree has the meaning of the original (the caller variable is never written).
    /// </summary>
    [TestMethod]
    public void AUD01_WriteThroughValueTypeParameter_KeepsInvocationAndMeaning()
    {
        var simplifier = new ExpressionSimplifier();
        var failures = new List<string>();
        foreach ((string writeName, Expression write) in Writes())
        {
            foreach ((string argumentName, Expression argument) in Arguments())
            {
                Expression<Func<double>> original = Program(write, argument);
                var simplified = (Expression<Func<double>>)simplifier.Simplify(original);
                double expected = original.Compile()(), actual = simplified.Compile()();
                if (!expected.Equals(actual) || !HasInvocation(simplified))
                {
                    failures.Add($"{writeName} / {argumentName}: expected {expected}, actual {actual}, invocation kept {HasInvocation(simplified)}");
                }
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    /// <summary>
    /// AUD-01 with a write-only body: <c>{ var s; ((p) =&gt; { write; 0 })(s); s.F }</c>. No read of <c>p</c> follows the
    /// write, so the variable argument was inlined and the write reached the caller variable <c>s</c>.
    /// </summary>
    [TestMethod]
    public void AUD01_WriteOnlyBody_DoesNotWriteTheCallerVariable()
    {
        var simplifier = new ExpressionSimplifier();
        var failures = new List<string>();
        foreach ((string writeName, Expression write) in Writes())
        {
            var invoked = Expression.Lambda<Func<AuditFixMutable, double>>(Expression.Block(write, Expression.Constant(0.0)), P);
            Expression<Func<double>> original = Expression.Lambda<Func<double>>(Expression.Block([S], Expression.Invoke(invoked, S), Expression.Field(S, FieldF)));
            var simplified = (Expression<Func<double>>)simplifier.Simplify(original);
            double expected = original.Compile()(), actual = simplified.Compile()();
            if (!expected.Equals(actual) || !HasInvocation(simplified)) failures.Add($"{writeName}: expected {expected}, actual {actual}, invocation kept {HasInvocation(simplified)}");
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    /// <summary>
    /// AUD-01, scope of the guard: reads through a value-type parameter (a field, a <see langword="readonly"/> getter or method,
    /// a method of a <see langword="readonly"/> structure such as <see cref="double"/>) and writes through a reference-type
    /// parameter (the object is shared, so inlining keeps the meaning) are still inlined.
    /// </summary>
    [TestMethod]
    public void AUD01_ReadsAndReferenceTypeWrites_AreStillInlined()
    {
        var simplifier = new ExpressionSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression box = Expression.Parameter(typeof(AuditFixBox), "box");
        FieldInfo boxF = typeof(AuditFixBox).GetField(nameof(AuditFixBox.F))!;
        var cases = new (string Name, LambdaExpression Invoked, Expression Argument)[]
        {
            ("field read", Expression.Lambda(Expression.Field(P, FieldF), P), Expression.Default(typeof(AuditFixMutable))),
            ("readonly getter", Expression.Lambda(Expression.Property(P, nameof(AuditFixMutable.Current)), P), Expression.Default(typeof(AuditFixMutable))),
            ("readonly method", Expression.Lambda(Expression.Call(P, typeof(AuditFixMutable).GetMethod(nameof(AuditFixMutable.Read))!), P), Expression.Default(typeof(AuditFixMutable))),
            ("readonly structure method", Expression.Lambda(Expression.Convert(Expression.Call(x, typeof(double).GetMethod(nameof(double.CompareTo), [typeof(double)])!, Expression.Constant(1.0)), typeof(double)), x), Expression.Constant(2.0)),
            ("reference-type field write", Expression.Lambda(Expression.Assign(Expression.Field(box, boxF), Expression.Constant(5.0)), box), Expression.Constant(new AuditFixBox())),
        };

        foreach ((string name, LambdaExpression invoked, Expression argument) in cases)
        {
            LambdaExpression original = Expression.Lambda(Expression.Invoke(invoked, argument));
            Expression simplified = simplifier.Simplify(original);
            Assert.IsFalse(HasInvocation(simplified), $"{name}: the invocation should still be inlined.");
        }
    }

    /// <summary>
    /// AUD-01, closures and by-reference invocation arguments: a write through the parameter inside a nested lambda keeps the
    /// invocation; a field of the parameter passed to a by-reference delegate parameter keeps it too.
    /// </summary>
    [TestMethod]
    public void AUD01_NestedAndByReferenceWrites_KeepInvocation()
    {
        var simplifier = new ExpressionSimplifier();
        Expression write = Expression.Assign(Expression.Field(P, FieldF), Expression.Constant(5.0));
        var nested = Expression.Lambda<Func<AuditFixMutable, double>>(
            Expression.Block(Expression.Invoke(Expression.Lambda<Func<double>>(write)), Expression.Field(P, FieldF)), P);
        Expression<Func<double>> original = Expression.Lambda<Func<double>>(Expression.Block([S], Expression.Add(Expression.Invoke(nested, S), Expression.Field(S, FieldF))));
        var simplified = (Expression<Func<double>>)simplifier.Simplify(original);
        Assert.AreEqual(original.Compile()(), simplified.Compile()());

        ParameterExpression target = Expression.Parameter(typeof(double).MakeByRefType(), "target");
        var setter = Expression.Lambda<RefSetter>(Expression.Assign(target, Expression.Constant(7.0)), target);
        var byRef = Expression.Lambda<Func<AuditFixMutable, double>>(
            Expression.Block(Expression.Invoke(setter, Expression.Field(P, FieldF)), Expression.Field(P, FieldF)), P);
        original = Expression.Lambda<Func<double>>(Expression.Block([S], Expression.Add(Expression.Invoke(byRef, S), Expression.Multiply(Expression.Field(S, FieldF), Expression.Constant(1000.0)))));
        simplified = (Expression<Func<double>>)simplifier.Simplify(original);
        Assert.AreEqual(original.Compile()(), simplified.Compile()());
        Assert.IsTrue(HasInvocation(simplified));
    }

    /// <summary>A delegate writing through a by-reference parameter.</summary>
    /// <param name="target">The written location.</param>
    /// <returns>The written value.</returns>
    public delegate double RefSetter(ref double target);

    /// <summary>
    /// AUD-02: a lambda whose body holds a dynamic operation is alpha-renamed by <c>CopyExpression</c> like any other lambda:
    /// the dynamic arguments follow the renaming, the binder is the same instance and is never called.
    /// </summary>
    [TestMethod]
    public void AUD02_CopyLambdaWithDynamicBody_IsAlphaRenamed()
    {
        var binder = new R4CountingBinder("b");
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression renamed = Expression.Parameter(typeof(double), "a2");
        var lambda = Expression.Lambda<Func<double, double>>(
            Expression.Add(Expression.Convert(Expression.Dynamic(binder, typeof(object), a, Expression.Constant(1.0)), typeof(double)), a), a);

        var copy = (Expression<Func<double, double>>)ExposedTransformer.Copy(lambda, renamed);

        Assert.AreSame(renamed, copy.Parameters[0]);
        var body = (BinaryExpression)copy.Body;
        var dynamic = (DynamicExpression)((UnaryExpression)body.Left).Operand;
        Assert.AreSame(renamed, dynamic.Arguments[0]);
        Assert.AreSame(renamed, body.Right);
        Assert.AreSame(binder, dynamic.Binder);
        Assert.AreEqual(0, binder.TotalCalls);
        Assert.AreEqual(lambda.Compile()(2.0), copy.Compile()(2.0));
    }

    /// <summary>
    /// AUD-02: a lambda copy is still refused when the new declaration occurs free inside a dynamic argument (it would be
    /// captured), so walking the arguments does not weaken the capture check.
    /// </summary>
    [TestMethod]
    public void AUD02_CopyLambdaCapturingThroughDynamicArgument_IsRefused()
    {
        var binder = new R4CountingBinder("b");
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression free = Expression.Parameter(typeof(double), "free");
        var lambda = Expression.Lambda<Func<double, double>>(
            Expression.Convert(Expression.Dynamic(binder, typeof(object), a, free), typeof(double)), a);

        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(lambda, free));
        Assert.AreEqual(0, binder.TotalCalls);
    }

    /// <summary>
    /// AUD-02: substituting a replacement that holds a dynamic operation under a binder no longer renames a declaration the
    /// replacement does not mention.
    /// </summary>
    [TestMethod]
    public void AUD02_SubstitutionWithDynamicReplacement_RenamesOnlyCapturingDeclarations()
    {
        var binder = new R4CountingBinder("b");
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        var inner = Expression.Lambda<Func<double, double>>(Expression.Add(a, x), a);
        Expression replacement = Expression.Convert(Expression.Dynamic(binder, typeof(object), z, z), typeof(double));

        var result = (LambdaExpression)new ExposedTransformer().Replace(inner, x, replacement);

        Assert.AreSame(a, result.Parameters[0]);
        Assert.AreSame(replacement, ((BinaryExpression)result.Body).Right);
        Assert.AreEqual(0, binder.TotalCalls);
    }
}
