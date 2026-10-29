using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R1b, finding ETC-15 (compatibility entry C-06): the symbolic rules of the simplifier never drop, repeat, merge or
/// move an evaluation whose effect is observable or not proven absent (assignments, increments, user methods, property
/// getters, constructors, allocations, checked arithmetic, array access, decimal and integer division). Every scenario
/// compares the result AND the execution trace of the source and of the simplified tree, for the exact and a derived
/// simplifier; innocent expressions are still simplified.
/// </summary>
[TestClass]
[TestCategory("ETC-R1b")]
public class ExpressionTreeR1bEffectTests
{
    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>The trace shared by the instrumented members of the scenarios.</summary>
    [ThreadStatic]
    private static List<string>? _trace;

    /// <summary>Gets the trace of the current thread.</summary>
    private static List<string> Trace => _trace ??= [];

    /// <summary>Records <paramref name="name"/> and returns <paramref name="value"/>.</summary>
    /// <param name="name">The call name.</param>
    /// <param name="value">The returned value.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static double Log(string name, double value)
    {
        Trace.Add(name);
        return value;
    }

    /// <summary>A user method returning one for an opaque value, recording the call.</summary>
    /// <param name="value">The opaque value.</param>
    /// <returns>One.</returns>
    public static double IdentityFromObject(object value)
    {
        Trace.Add("id:" + value);
        return 1.0;
    }

    /// <summary>A reference type whose constructor, property getter and mutable field are observable.</summary>
    public sealed class Cell
    {
        /// <summary>A mutable field.</summary>
        public double Field;

        /// <summary>A read-only field.</summary>
        public readonly double Fixed = 4.0;

        /// <summary>Initializes a cell, recording the construction.</summary>
        /// <param name="field">The initial field value.</param>
        public Cell(double field)
        {
            Trace.Add("new");
            Field = field;
        }

        /// <summary>Gets <see cref="Field"/>, recording the call.</summary>
        public double Property
        {
            get
            {
                Trace.Add("get");
                return Field;
            }
        }
    }

    /// <summary>The <see cref="Log"/> method.</summary>
    private static readonly MethodInfo LogMethod = typeof(ExpressionTreeR1bEffectTests).GetMethod(nameof(Log))!;

    /// <summary>The <see cref="IdentityFromObject"/> method.</summary>
    private static readonly MethodInfo IdentityMethod = typeof(ExpressionTreeR1bEffectTests).GetMethod(nameof(IdentityFromObject))!;

    /// <summary>Builds <c>Log(name, value)</c>.</summary>
    /// <param name="name">The call name.</param>
    /// <param name="value">The returned value.</param>
    /// <returns>The call.</returns>
    private static Expression LogCall(string name, double value) => Expression.Call(LogMethod, Expression.Constant(name), Expression.Constant(value));

    /// <summary>The outcome of one evaluation: its value or exception type, then its trace.</summary>
    /// <param name="body">A parameterless body.</param>
    /// <param name="variables">Block variables initialized to zero before the body runs.</param>
    /// <returns>A printable outcome.</returns>
    private static string Run(Expression body, params ParameterExpression[] variables)
    {
        Expression block = variables.Length == 0
            ? body
            : Expression.Block(variables, [.. variables.Select(v => (Expression)Expression.Assign(v, Expression.Default(v.Type))), body]);
        Trace.Clear();
        string value;
        try
        {
            value = Convert.ToString(Expression.Lambda(Expression.Convert(block, typeof(object))).Compile().DynamicInvoke(), System.Globalization.CultureInfo.InvariantCulture)!;
        }
        catch (TargetInvocationException exception)
        {
            value = "throws " + exception.InnerException!.GetType().Name;
        }

        return value + " | " + string.Join(",", Trace);
    }

    /// <summary>Asserts that the exact and a derived simplifier preserve the value, exception and trace of <paramref name="source"/>.</summary>
    /// <param name="source">The source body.</param>
    /// <param name="variables">Block variables initialized to zero.</param>
    /// <returns>The exact simplifier's result.</returns>
    private static Expression AssertPreserved(Expression source, params ParameterExpression[] variables)
    {
        string expected = Run(source, variables);
        Expression exact = new ExpressionSimplifier().Simplify(source);
        Assert.AreEqual(expected, Run(exact, variables), $"exact: {source} => {exact}");
        Expression derived = new DerivedSimplifier().Simplify(source);
        Assert.AreEqual(expected, Run(derived, variables), $"derived: {source} => {derived}");
        return exact;
    }

    /// <summary><c>++v + ++v</c> evaluates both increments (3, not 2).</summary>
    [TestMethod]
    public void TwoIncrements_AreBothEvaluated()
    {
        ParameterExpression v = Expression.Variable(typeof(double), "v");
        AssertPreserved(Expression.Add(Expression.PreIncrementAssign(v), Expression.PreIncrementAssign(v)), v);
        Assert.AreEqual("3 | ", Run(new ExpressionSimplifier().Simplify(Expression.Add(Expression.PreIncrementAssign(v), Expression.PreIncrementAssign(v))), v));
    }

    /// <summary><c>(v = 5) * 0 + v</c> keeps the assignment and reads <c>v</c> after it (5, not 0).</summary>
    [TestMethod]
    public void AssignmentTimesZero_IsNotDropped()
    {
        ParameterExpression v = Expression.Variable(typeof(double), "v");
        Expression source = Expression.Add(Expression.Multiply(Expression.Assign(v, Expression.Constant(5.0)), Expression.Constant(0.0)), v);
        AssertPreserved(source, v);
        Assert.AreEqual("5 | ", Run(new ExpressionSimplifier().Simplify(source), v));
    }

    /// <summary>Two calls of a side-effecting method are neither merged nor reordered, nor cancelled.</summary>
    [TestMethod]
    public void TwoSideEffectingCalls_AreKeptInOrder()
    {
        AssertPreserved(Expression.Add(LogCall("a", 1.0), LogCall("a", 1.0)));
        AssertPreserved(Expression.Subtract(LogCall("a", 1.0), LogCall("a", 1.0)));
        AssertPreserved(Expression.Add(LogCall("b", 2.0), Expression.Add(Expression.Constant(1.0), LogCall("a", 1.0))));
        AssertPreserved(Expression.Multiply(LogCall("a", 3.0), Expression.Constant(0.0)));
    }

    /// <summary>Two identical allocations are two objects: both constructions run, and the identity test still fails.</summary>
    [TestMethod]
    public void TwoIdenticalAllocations_AreNotMerged()
    {
        ConstructorInfo constructor = typeof(Cell).GetConstructor([typeof(double)])!;
        Expression Make() => Expression.New(constructor, Expression.Constant(2.0));

        AssertPreserved(Expression.Add(Expression.Field(Make(), nameof(Cell.Field)), Expression.Field(Make(), nameof(Cell.Field))));
        AssertPreserved(Expression.Subtract(Expression.Field(Make(), nameof(Cell.Field)), Expression.Field(Make(), nameof(Cell.Field))));
        Expression identity = Expression.ReferenceEqual(Make(), Make());
        Assert.AreEqual("False | new,new", Run(new ExpressionSimplifier().Simplify(identity)));
    }

    /// <summary>A property read with a counting getter keeps every read, even when the reads would cancel or merge.</summary>
    [TestMethod]
    public void PropertyReads_KeepEveryGetterCall()
    {
        var cell = new Cell(3.0);
        Expression Read() => Expression.Property(Expression.Constant(cell), nameof(Cell.Property));

        AssertPreserved(Expression.Add(Read(), Read()));
        AssertPreserved(Expression.Subtract(Read(), Read()));
        AssertPreserved(Expression.Multiply(Read(), Expression.Constant(0.0)));
    }

    /// <summary>A mutable field read between two writes reads each written value: no read moves across a write.</summary>
    [TestMethod]
    public void MutableReadsBetweenWrites_AreNotMoved()
    {
        var cell = new Cell(0.0);
        Expression field = Expression.Field(Expression.Constant(cell), nameof(Cell.Field));
        Expression source = Expression.Add(
            Expression.Add(Expression.Assign(field, Expression.Constant(1.0)), field),
            Expression.Add(Expression.Assign(field, Expression.Constant(2.0)), Expression.Multiply(field, Expression.Constant(10.0))));

        cell.Field = 0.0;
        string expected = Run(source);
        cell.Field = 0.0;
        Assert.AreEqual(expected, Run(new ExpressionSimplifier().Simplify(source)));
        Assert.AreEqual("24 | ", expected);
    }

    /// <summary>An exception raised by an operand is not suppressed by a multiplication by zero or a cancellation.</summary>
    [TestMethod]
    public void ExceptionsAreNotSuppressedByAlgebra()
    {
        ParameterExpression i = Expression.Variable(typeof(int), "i");
        Expression overflow = Expression.AddChecked(Expression.Constant(int.MaxValue), Expression.Add(i, Expression.Constant(1)));
        Expression outOfRange = Expression.ArrayIndex(Expression.Constant(new double[1]), Expression.Add(i, Expression.Constant(3)));
        Expression divideByZero = Expression.Divide(Expression.Constant(7), i);
        Expression decimalOverflow = Expression.Add(Expression.Constant(decimal.MaxValue), Expression.Convert(Expression.Add(i, Expression.Constant(1)), typeof(decimal)));

        Assert.AreEqual("throws OverflowException | ", Run(new ExpressionSimplifier().Simplify(Expression.Multiply(overflow, Expression.Constant(0))), i));
        Assert.AreEqual("throws IndexOutOfRangeException | ", Run(new ExpressionSimplifier().Simplify(Expression.Multiply(outOfRange, Expression.Constant(0.0))), i));
        Assert.AreEqual("throws DivideByZeroException | ", Run(new ExpressionSimplifier().Simplify(Expression.Multiply(divideByZero, Expression.Constant(0))), i));
        Assert.AreEqual("throws DivideByZeroException | ", Run(new ExpressionSimplifier().Simplify(Expression.Subtract(divideByZero, divideByZero)), i));
        Assert.AreEqual("throws OverflowException | ", Run(new ExpressionSimplifier().Simplify(Expression.Subtract(decimalOverflow, decimalOverflow)), i));
        AssertPreserved(Expression.Multiply(overflow, Expression.Constant(0)), i);
        AssertPreserved(Expression.Multiply(outOfRange, Expression.Constant(0.0)), i);
    }

    /// <summary>
    /// A division's own exception is not removed by an identity on the node itself: <c>0 / i</c> (integral or decimal) keeps
    /// the division by zero, <c>i / -1</c> and <c>i / -j</c> keep the overflow of <c>MinValue / -1</c>; the floating-point
    /// identities (<c>0 / x -&gt; 0</c>, <c>x / -1 -&gt; -x</c>), which cannot throw, still apply.
    /// </summary>
    [TestMethod]
    public void DivisionIdentities_KeepTheirOwnExceptions()
    {
        ParameterExpression i = Expression.Variable(typeof(int), "i");
        ParameterExpression m = Expression.Variable(typeof(decimal), "m");
        Expression minValue = Expression.Add(Expression.Constant(int.MinValue), i);

        Assert.AreEqual("throws DivideByZeroException | ", Run(new ExpressionSimplifier().Simplify(Expression.Divide(Expression.Constant(0), i)), i));
        Assert.AreEqual("throws DivideByZeroException | ", Run(new ExpressionSimplifier().Simplify(Expression.Divide(Expression.Constant(0m), m)), m));
        Assert.AreEqual("throws OverflowException | ", Run(new ExpressionSimplifier().Simplify(Expression.Divide(minValue, Expression.Constant(-1))), i));
        Assert.AreEqual("throws OverflowException | ", Run(new ExpressionSimplifier().Simplify(Expression.Divide(minValue, Expression.Negate(Expression.Add(i, Expression.Constant(1))))), i));

        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Assert.AreEqual(ExpressionType.Constant, new ExpressionSimplifier().Simplify(Expression.Divide(Expression.Constant(0.0), x)).NodeType);
        Assert.AreEqual(ExpressionType.Negate, new ExpressionSimplifier().Simplify(Expression.Divide(x, Expression.Constant(-1.0))).NodeType);
    }

    /// <summary>Innocent expressions are still simplified: neutral elements, cancellation, merging and constant folding.</summary>
    [TestMethod]
    public void InnocentExpressions_AreStillSimplified()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        var cell = new Cell(3.0);
        Expression fixedField = Expression.Field(Expression.Constant(cell), nameof(Cell.Fixed));
        var simplifier = new ExpressionSimplifier();

        Assert.AreSame(x, simplifier.Simplify(Expression.Add(Expression.Multiply(x, Expression.Constant(1.0)), Expression.Constant(0.0))));
        Assert.AreSame(x, simplifier.Simplify(Expression.Subtract(Expression.Add(x, x), x)));
        Assert.AreEqual("(2 * Sin(x))", simplifier.Simplify(Expression.Add(Expression.Call(sin, x), Expression.Call(sin, x))).ToString());
        Assert.AreEqual(ExpressionType.Constant, simplifier.Simplify(Expression.Subtract(fixedField, fixedField)).NodeType);
        Assert.AreEqual(ExpressionType.Constant, simplifier.Simplify(Expression.Multiply(Expression.Constant(2.0), Expression.Constant(3.5))).NodeType);
    }

    /// <summary>
    /// The scenarios of the S5 memo tests with their former user-method carrier: a chain of <c>c * IdentityFromObject(k)</c>
    /// terms is simplified without running user code, and every call is kept once, in source order (each term is pinned).
    /// </summary>
    [TestMethod]
    public void UserMethodCarrierChains_KeepEveryCallInOrder()
    {
        Expression Term(double coefficient, string id) => Expression.Multiply(Expression.Constant(coefficient), Expression.Call(IdentityMethod, Expression.Constant(id, typeof(object))));
        Expression[] chains =
        [
            Expression.Subtract(Expression.Subtract(Expression.Subtract(Term(2, "d0"), Term(3, "d1")), Term(4, "d0")), Term(5, "d2")),
            Expression.Add(Expression.Add(Term(100, "d0"), Term(3, "d1")), Term(100, "d0")),
            Expression.Subtract(Term(2, "d0"), Term(2, "d0")),
        ];

        foreach (Expression chain in chains)
        {
            Trace.Clear();
            Expression simplified = AssertPreserved(chain);
            Assert.AreEqual(chain.ToString().Split("IdentityFromObject(").Length, simplified.ToString().Split("IdentityFromObject(").Length, simplified.ToString());
        }
    }

    /// <summary>
    /// Structural equality is not algebraic interchangeability (R1c boundary): two calls of the same user method compare
    /// equal and hash equally, yet their difference is not cancelled.
    /// </summary>
    [TestMethod]
    public void StructurallyEqualCalls_AreNotInterchangeable()
    {
        Expression first = LogCall("a", 1.0);
        Expression second = LogCall("a", 1.0);

        Assert.IsTrue(ExpressionComparer.Default.Equals(first, second));
        Assert.AreEqual(ExpressionComparer.Default.GetHashCode(first), ExpressionComparer.Default.GetHashCode(second));
        Assert.AreEqual("0 | a,a", Run(new ExpressionSimplifier().Simplify(Expression.Subtract(first, second))));
    }
}
