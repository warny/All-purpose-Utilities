using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Stage S3 regression coverage: <see cref="ExpressionSimplifier"/> is documented as a symbolic algebra
/// simplifier, not a strict CLR/IEEE-754 execution-preserving optimizer, but that contract does not
/// authorize treating a custom/user-defined operator as ordinary arithmetic, collapsing lifted nullable
/// semantics, or turning truncating integer division into field division. Every test goes through the
/// public <see cref="ExpressionSimplifier.Simplify(Expression)"/> entry point (never a protected rule
/// directly) and, where practical, compiles and executes both the source and the simplified delegate to
/// prove behavioral equivalence, not merely structural shape. Several tests here are documented as
/// "regression": they fail on the pre-S3 baseline (the rewrite fires when it must not) and only pass once
/// the centralized operator-safety guards land. See
/// <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>, stage S3, for the audit this hardening
/// addresses.
/// </summary>
[TestClass]
public class ExpressionSimplifierSymbolicContractTests
{
    #region Custom operator fixtures

    /// <summary>Observably different from ordinary <c>+</c>: any rule that folds this away as if it were plain addition changes the result.</summary>
    private static double CustomAdd(double a, double b) => a + b + 1000.0;

    /// <summary>Observably different from ordinary <c>-</c>.</summary>
    private static double CustomSubtract(double a, double b) => a - b - 1000.0;

    /// <summary>Observably different from ordinary <c>*</c>.</summary>
    private static double CustomMultiply(double a, double b) => a * b * 2.0 + 1.0;

    /// <summary>Observably different from ordinary <c>/</c>.</summary>
    private static double CustomDivide(double a, double b) => a / b + 7.0;

    /// <summary>Observably different from ordinary <c>^</c> (<see cref="Math.Pow(double, double)"/>).</summary>
    private static double CustomPower(double a, double b) => Math.Pow(a, b) + 1.0;

    /// <summary>Observably different from ordinary unary negation.</summary>
    private static double CustomNegate(double a) => -a - 1000.0;

    /// <summary>Reflected <see cref="MethodInfo"/> for <see cref="CustomAdd"/>, used as the explicit custom <c>Method</c> on a hand-built <see cref="BinaryExpression"/>.</summary>
    private static readonly MethodInfo CustomAddMethod = Method(nameof(CustomAdd));

    /// <summary>Reflected <see cref="MethodInfo"/> for <see cref="CustomSubtract"/>, used as the explicit custom <c>Method</c> on a hand-built <see cref="BinaryExpression"/>.</summary>
    private static readonly MethodInfo CustomSubtractMethod = Method(nameof(CustomSubtract));

    /// <summary>Reflected <see cref="MethodInfo"/> for <see cref="CustomMultiply"/>, used as the explicit custom <c>Method</c> on a hand-built <see cref="BinaryExpression"/>.</summary>
    private static readonly MethodInfo CustomMultiplyMethod = Method(nameof(CustomMultiply));

    /// <summary>Reflected <see cref="MethodInfo"/> for <see cref="CustomDivide"/>, used as the explicit custom <c>Method</c> on a hand-built <see cref="BinaryExpression"/>.</summary>
    private static readonly MethodInfo CustomDivideMethod = Method(nameof(CustomDivide));

    /// <summary>Reflected <see cref="MethodInfo"/> for <see cref="CustomPower"/>, used as the explicit custom <c>Method</c> on a hand-built <see cref="BinaryExpression"/>.</summary>
    private static readonly MethodInfo CustomPowerMethod = Method(nameof(CustomPower));

    /// <summary>Reflected <see cref="MethodInfo"/> for <see cref="CustomNegate"/>, used as the explicit custom <c>Method</c> on a hand-built <see cref="UnaryExpression"/>.</summary>
    private static readonly MethodInfo CustomNegateMethod = Method(nameof(CustomNegate));

    /// <summary>Resolves the private static <see cref="MethodInfo"/> for one of this class's own custom-operator fixture methods, by name.</summary>
    /// <param name="name">The fixture method's name, typically supplied via <see langword="nameof"/>.</param>
    /// <returns>The resolved <see cref="MethodInfo"/>.</returns>
    private static MethodInfo Method(string name) =>
        typeof(ExpressionSimplifierSymbolicContractTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Creates a fresh <see cref="double"/> parameter expression, defaulting to the name <c>x</c>.</summary>
    /// <param name="name">The parameter's display name.</param>
    /// <returns>A new <see cref="double"/> <see cref="ParameterExpression"/>.</returns>
    private static ParameterExpression X(string name = "x") => Expression.Parameter(typeof(double), name);

    /// <summary>Builds a call to a public static <see cref="double"/> method (for example <see cref="double.Log(double)"/>, <see cref="double.Sin(double)"/>) with the given arguments, resolving the overload by an all-<see cref="double"/> parameter list.</summary>
    /// <param name="methodName">The name of the <see cref="double"/> static method to call.</param>
    /// <param name="args">The call's argument expressions, one per <see cref="double"/> parameter.</param>
    /// <returns>The resulting <see cref="MethodCallExpression"/>.</returns>
    private static MethodCallExpression CallDouble(string methodName, params Expression[] args)
    {
        var parameterTypes = new Type[args.Length];
        System.Array.Fill(parameterTypes, typeof(double));
        MethodInfo method = typeof(double).GetMethod(methodName, parameterTypes)!;
        return Expression.Call(null, method, args);
    }

    /// <summary>Recursively records every non-null <see cref="UnaryExpression.Method"/>/<see cref="BinaryExpression.Method"/> found in a tree, using the standard <see cref="ExpressionVisitor"/> walk rather than a hand-rolled traversal.</summary>
    private sealed class MethodCollectingVisitor : ExpressionVisitor
    {
        /// <summary>The distinct non-null operator <see cref="MethodInfo"/> instances encountered so far by this visitor.</summary>
        public HashSet<MethodInfo> Methods { get; } = new();

        /// <summary>Records <paramref name="node"/>'s <see cref="UnaryExpression.Method"/>, if any, into <see cref="Methods"/> before visiting its operand.</summary>
        /// <param name="node">The unary node being visited.</param>
        /// <returns>The (possibly rewritten) node, per the base <see cref="ExpressionVisitor"/> contract.</returns>
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.Method is not null) Methods.Add(node.Method);
            return base.VisitUnary(node);
        }

        /// <summary>Records <paramref name="node"/>'s <see cref="BinaryExpression.Method"/>, if any, into <see cref="Methods"/> before visiting its operands.</summary>
        /// <param name="node">The binary node being visited.</param>
        /// <returns>The (possibly rewritten) node, per the base <see cref="ExpressionVisitor"/> contract.</returns>
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.Method is not null) Methods.Add(node.Method);
            return base.VisitBinary(node);
        }
    }

    /// <summary>Determines whether <paramref name="method"/> appears as the explicit operator <c>Method</c> of any <see cref="UnaryExpression"/>/<see cref="BinaryExpression"/> anywhere in <paramref name="tree"/>, proving the operator survived simplification atomically instead of being flattened or folded away.</summary>
    /// <param name="tree">The expression tree to search.</param>
    /// <param name="method">The operator method to look for.</param>
    /// <returns><see langword="true"/> when <paramref name="method"/> is found; otherwise <see langword="false"/>.</returns>
    private static bool ContainsMethod(Expression tree, MethodInfo method)
    {
        var visitor = new MethodCollectingVisitor();
        visitor.Visit(tree);
        return visitor.Methods.Contains(method);
    }

    /// <summary>
    /// Compares source vs. simplified delegate results with a small floating-point tolerance: the S3
    /// contract explicitly does not promise bit-for-bit IEEE-754 equality across an algebraic rewrite (for
    /// example combining two <see cref="double.Log(double)"/> calls into one via a different libm code
    /// path), only symbolic/behavioral equivalence.
    /// </summary>
    private static void AssertSameBehavior(LambdaExpression source, LambdaExpression simplified, params double[] args)
    {
        Delegate compiledSource = source.Compile();
        Delegate compiledSimplified = simplified.Compile();

        object?[] boxedArgs = System.Array.ConvertAll(args, static a => (object?)a);
        object? expected = compiledSource.DynamicInvoke(boxedArgs);
        object? actual = compiledSimplified.DynamicInvoke(boxedArgs);

        if (expected is double expectedDouble && actual is double actualDouble)
        {
            Assert.AreEqual(expectedDouble, actualDouble, 1e-9);
        }
        else
        {
            Assert.AreEqual(expected, actual);
        }
    }

    #endregion

    // ================================================================================================
    // 1. Root custom operator identities must not consume the custom Method.
    // ================================================================================================

    /// <summary>Regression: a custom-<c>Method</c> <c>Add(x, 0)</c> must not be collapsed by <c>AdditionWithZero</c> into plain <c>x</c>, which would silently discard the custom operator's actual behavior.</summary>
    [TestMethod]
    public void CustomAdd_WithZero_KeepsCustomMethod_RootIdentityNotApplied()
    {
        ParameterExpression x = X();
        var source = Expression.Lambda<Func<double, double>>(Expression.Add(x, Expression.Constant(0.0), CustomAddMethod), x);

        var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified.Body, CustomAddMethod), "Custom Add(x, 0) must not collapse to x.");
        AssertSameBehavior(source, simplified, 3.0);
    }

    /// <summary>Regression: a custom-<c>Method</c> <c>Subtract(x, 0)</c> must not be collapsed by <c>SubstractionWithZero</c> into plain <c>x</c>.</summary>
    [TestMethod]
    public void CustomSubtract_WithZero_KeepsCustomMethod_RootIdentityNotApplied()
    {
        ParameterExpression x = X();
        var source = Expression.Lambda<Func<double, double>>(Expression.Subtract(x, Expression.Constant(0.0), CustomSubtractMethod), x);

        var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified.Body, CustomSubtractMethod), "Custom Subtract(x, 0) must not collapse to x.");
        AssertSameBehavior(source, simplified, 3.0);
    }

    /// <summary>Regression: a custom-<c>Method</c> <c>Multiply(x, 1)</c> must not be collapsed by <c>MultiplicationWithZeroOrOne</c> into plain <c>x</c>.</summary>
    [TestMethod]
    public void CustomMultiply_ByOne_KeepsCustomMethod_RootIdentityNotApplied()
    {
        ParameterExpression x = X();
        var source = Expression.Lambda<Func<double, double>>(Expression.Multiply(x, Expression.Constant(1.0), CustomMultiplyMethod), x);

        var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified.Body, CustomMultiplyMethod), "Custom Multiply(x, 1) must not collapse to x.");
        AssertSameBehavior(source, simplified, 3.0);
    }

    /// <summary>Regression: a custom-<c>Method</c> <c>Divide(x, 1)</c> must not be collapsed by <c>DivideWithZeroOrOne</c> into plain <c>x</c>.</summary>
    [TestMethod]
    public void CustomDivide_ByOne_KeepsCustomMethod_RootIdentityNotApplied()
    {
        ParameterExpression x = X();
        var source = Expression.Lambda<Func<double, double>>(Expression.Divide(x, Expression.Constant(1.0), CustomDivideMethod), x);

        var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified.Body, CustomDivideMethod), "Custom Divide(x, 1) must not collapse to x.");
        AssertSameBehavior(source, simplified, 3.0);
    }

    /// <summary>Regression: a custom-<c>Method</c> <c>Power(x, 1)</c> must not be collapsed by <c>PowerByZeroOrOne</c> into plain <c>x</c>.</summary>
    [TestMethod]
    public void CustomPower_ByOne_KeepsCustomMethod_RootIdentityNotApplied()
    {
        ParameterExpression x = X();
        var source = Expression.Lambda<Func<double, double>>(Expression.Power(x, Expression.Constant(1.0), CustomPowerMethod), x);

        var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified.Body, CustomPowerMethod), "Custom Power(x, 1) must not collapse to x.");
        AssertSameBehavior(source, simplified, 3.0);
    }

    /// <summary>Regression: <c>x + (-customY)</c>, where the <see cref="ExpressionType.Negate"/> operand carries a custom <c>Method</c>, must not be rewritten by <c>AdditionWithNegate</c> as if it were ordinary sign flip (which would drop the custom negation's actual behavior).</summary>
    [TestMethod]
    public void CustomNegate_InsideAdditionWithNegateShape_KeepsCustomMethod()
    {
        ParameterExpression x = X("x");
        ParameterExpression y = X("y");
        UnaryExpression customNegateY = Expression.Negate(y, CustomNegateMethod);
        var source = Expression.Lambda<Func<double, double, double>>(Expression.Add(x, customNegateY), x, y);

        var simplified = (Expression<Func<double, double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified.Body, CustomNegateMethod), "AdditionWithNegate must not treat a custom Negate as ordinary sign flip.");
        AssertSameBehavior(source, simplified, 5.0, 5.0);
        AssertSameBehavior(source, simplified, 2.0, 7.0);
    }

    /// <summary>Regression: <c>-(x - y)</c> built with a custom-<c>Method</c> outer <see cref="ExpressionType.Negate"/> must not be rewritten by <c>NegateWithSubstraction</c> into <c>-y + x</c>, which would replace the custom negation with an ordinary sign flip.</summary>
    [TestMethod]
    public void CustomNegate_OuterNegateOfSubtraction_KeepsCustomMethod()
    {
        ParameterExpression x = X("x");
        ParameterExpression y = X("y");
        BinaryExpression subtract = Expression.Subtract(x, y);
        UnaryExpression body = Expression.Negate(subtract, CustomNegateMethod);
        var source = Expression.Lambda<Func<double, double, double>>(body, x, y);

        var simplified = (Expression<Func<double, double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified.Body, CustomNegateMethod), "NegateWithSubstraction must not rewrite a custom outer Negate.");
        AssertSameBehavior(source, simplified, 9.0, 4.0);
    }

    // ================================================================================================
    // 2. Nested custom Add/Subtract/Multiply/Negate must survive canonicalization atomically.
    // ================================================================================================

    /// <summary>Regression: ordinary <c>Add(customAdd(x, y), z)</c> must keep the inner custom-<c>Method</c> <c>Add</c> as one atomic term through additive canonicalization (<c>CollectAdditiveTerms</c>), instead of flattening it into ordinary <c>Add</c> nodes and losing its <c>Method</c>.</summary>
    [TestMethod]
    public void OrdinaryAddition_WithNestedCustomAdd_KeepsInnerNodeAtomic()
    {
        ParameterExpression x = X("x");
        ParameterExpression y = X("y");
        ParameterExpression z = X("z");
        BinaryExpression innerCustomAdd = Expression.Add(x, y, CustomAddMethod);
        var source = Expression.Lambda<Func<double, double, double, double>>(Expression.Add(innerCustomAdd, z), x, y, z);

        var simplified = (Expression<Func<double, double, double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified.Body, CustomAddMethod), "Additive canonicalization must not flatten a custom Add into ordinary Add.");
        AssertSameBehavior(source, simplified, 1.0, 2.0, 3.0);
    }

    /// <summary>Regression: ordinary <c>Add(customSubtract(x, y), z)</c> must keep the inner custom-<c>Method</c> <c>Subtract</c> as one atomic term through additive canonicalization.</summary>
    [TestMethod]
    public void OrdinaryAddition_WithNestedCustomSubtract_KeepsInnerNodeAtomic()
    {
        ParameterExpression x = X("x");
        ParameterExpression y = X("y");
        ParameterExpression z = X("z");
        BinaryExpression innerCustomSubtract = Expression.Subtract(x, y, CustomSubtractMethod);
        var source = Expression.Lambda<Func<double, double, double, double>>(Expression.Add(innerCustomSubtract, z), x, y, z);

        var simplified = (Expression<Func<double, double, double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified.Body, CustomSubtractMethod), "Additive canonicalization must not flatten a custom Subtract into ordinary Subtract.");
        AssertSameBehavior(source, simplified, 4.0, 1.0, 6.0);
    }

    /// <summary>Regression: ordinary <c>Multiply(customMultiply(x, y), z)</c> must keep the inner custom-<c>Method</c> <c>Multiply</c> as one atomic factor through multiplicative canonicalization (<c>CollectMultiplicativeFactors</c>).</summary>
    [TestMethod]
    public void OrdinaryMultiplication_WithNestedCustomMultiply_KeepsInnerNodeAtomic()
    {
        ParameterExpression x = X("x");
        ParameterExpression y = X("y");
        ParameterExpression z = X("z");
        BinaryExpression innerCustomMultiply = Expression.Multiply(x, y, CustomMultiplyMethod);
        var source = Expression.Lambda<Func<double, double, double, double>>(Expression.Multiply(innerCustomMultiply, z), x, y, z);

        var simplified = (Expression<Func<double, double, double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified.Body, CustomMultiplyMethod), "Multiplicative canonicalization must not flatten a custom Multiply into ordinary Multiply.");
        AssertSameBehavior(source, simplified, 2.0, 3.0, 4.0);
    }

    /// <summary>Regression: a custom-<c>Method</c> <see cref="ExpressionType.Negate"/> nested inside additive canonicalization must remain atomic instead of being reinterpreted as an ordinary sign flip by <c>CollectAdditiveTerms</c>.</summary>
    [TestMethod]
    public void AdditiveCanonicalization_WithNestedCustomNegate_DoesNotFlipSign()
    {
        ParameterExpression x = X("x");
        ParameterExpression y = X("y");
        ParameterExpression z = X("z");
        UnaryExpression customNegateY = Expression.Negate(y, CustomNegateMethod);
        BinaryExpression inner = Expression.Add(x, customNegateY);
        var source = Expression.Lambda<Func<double, double, double, double>>(Expression.Add(z, inner), x, y, z);

        var simplified = (Expression<Func<double, double, double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified.Body, CustomNegateMethod), "CollectAdditiveTerms must not reinterpret a custom Negate as ordinary sign flip.");
        AssertSameBehavior(source, simplified, 1.0, 2.0, 3.0);
    }

    // ================================================================================================
    // 3. Custom outer operator around log/trig operands must not trigger the built-in identities.
    // ================================================================================================

    /// <summary>Regression: <c>Log(x) + Log(y)</c> built with a custom-<c>Method</c> outer <see cref="ExpressionType.Add"/> must not be combined by <c>LogarithmSimplificationAddNumber</c> into <c>Log(x*y)</c>, which checks only the inner calls, not the outer operator.</summary>
    [TestMethod]
    public void CustomAdd_OfTwoLogCalls_DoesNotCombineIntoLogOfProduct()
    {
        ParameterExpression x = X("x");
        ParameterExpression y = X("y");
        MethodCallExpression logX = CallDouble(nameof(double.Log), x);
        MethodCallExpression logY = CallDouble(nameof(double.Log), y);
        var source = Expression.Lambda<Func<double, double, double>>(Expression.Add(logX, logY, CustomAddMethod), x, y);

        var simplified = (Expression<Func<double, double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified.Body, CustomAddMethod), "A custom Add of two Log calls must not be rewritten into Log(x*y).");
        AssertSameBehavior(source, simplified, 2.0, 3.0);
    }

    /// <summary>Regression: <c>Sin(x) / Cos(x)</c> built with a custom-<c>Method</c> outer <see cref="ExpressionType.Divide"/> must not be rewritten by <c>DivisionOfCosAndSinNumber</c> into <c>Tan(x)</c>.</summary>
    [TestMethod]
    public void CustomDivide_OfSinAndCos_DoesNotBecomeTan()
    {
        ParameterExpression x = X("x");
        MethodCallExpression sinX = CallDouble(nameof(double.Sin), x);
        MethodCallExpression cosX = CallDouble(nameof(double.Cos), x);
        var source = Expression.Lambda<Func<double, double>>(Expression.Divide(sinX, cosX, CustomDivideMethod), x);

        var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified.Body, CustomDivideMethod), "A custom Divide of Sin(x)/Cos(x) must not be rewritten into Tan(x).");
        AssertSameBehavior(source, simplified, 0.7);
    }

    /// <summary>Regression: <c>sin(x)^2 + cos(x)^2</c>, where the <c>sin(x)^2</c> factor is a custom-<c>Method</c> <see cref="ExpressionType.Power"/>, must not be collapsed by <c>AdditionOfCos2andSin2Number</c> into the constant <c>1</c>; that identity checked the outer <see cref="ExpressionType.Add"/>'s <c>Method</c> but, before this fix, never the two <see cref="ExpressionType.Power"/> operands' <c>Method</c>.</summary>
    [TestMethod]
    public void CustomPower_InsideSinSquaredPlusCosSquared_DoesNotCollapseToOne()
    {
        // AdditionOfCos2andSin2Number checks the outer Add's Method but, before this fix, never checked
        // the two Power operands' Method: a custom Power(sin(x), 2) could still be consumed by the
        // sin^2(x)+cos^2(x) -> 1 identity even though its actual value need not be an ordinary square.
        ParameterExpression x = X("x");
        MethodCallExpression sinX = CallDouble(nameof(double.Sin), x);
        MethodCallExpression cosX = CallDouble(nameof(double.Cos), x);
        BinaryExpression customSinSquared = Expression.Power(sinX, Expression.Constant(2.0), CustomPowerMethod);
        BinaryExpression cosSquared = Expression.Power(cosX, Expression.Constant(2.0));
        var source = Expression.Lambda<Func<double, double>>(Expression.Add(customSinSquared, cosSquared), x);

        var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified.Body, CustomPowerMethod), "A custom Power(sin(x), 2) must not be consumed by the sin^2+cos^2 -> 1 identity.");
        AssertSameBehavior(source, simplified, 0.7);
        AssertSameBehavior(source, simplified, 1.3);
    }

    // ================================================================================================
    // 4. ExpressionComparer must not regain false equivalence through pre-comparison simplification.
    // ================================================================================================

    /// <summary>Regression: <see cref="ExpressionComparer"/> simplifies both operands before comparing them structurally, so a bad root-identity rewrite could still make it report a false equivalence even though its structural core correctly compares <c>Method</c> metadata. A custom-<c>Method</c> <c>Add(x, 0)</c> must therefore not compare equal to plain <c>y</c>.</summary>
    [TestMethod]
    public void Comparer_CustomAdditionWithZero_IsNotEqualToPlainOperand()
    {
        ParameterExpression x = X("x");
        ParameterExpression y = X("y");
        var customExpression = Expression.Lambda<Func<double, double>>(Expression.Add(x, Expression.Constant(0.0), CustomAddMethod), x);
        var plainExpression = Expression.Lambda<Func<double, double>>(y, y);

        Assert.IsFalse(ExpressionComparer.Default.Equals(customExpression, plainExpression));
    }

    // ================================================================================================
    // 5. Integer nested-division counterexamples: field-style reassociation is invalid under truncation.
    // ================================================================================================

    /// <summary>Regression: <c>x / (y / z)</c> over <see cref="int"/> operands must not be reassociated by <c>DivisionOfDivision</c> into the field-style <c>(x*z) / y</c>, which is invalid under truncating integer division. Discriminating counterexample: <c>8 / (3 / 2)</c> is <c>8</c> under truncation but <c>(8*2) / 3</c> is <c>5</c>.</summary>
    [TestMethod]
    public void IntegerDivisionOfDivision_XOverYOverZ_MatchesTruncatingSourceSemantics()
    {
        ParameterExpression x = Expression.Parameter(typeof(int), "x");
        ParameterExpression y = Expression.Parameter(typeof(int), "y");
        ParameterExpression z = Expression.Parameter(typeof(int), "z");
        var source = Expression.Lambda<Func<int, int, int, int>>(Expression.Divide(x, Expression.Divide(y, z)), x, y, z);

        var simplified = (Expression<Func<int, int, int, int>>)new ExpressionSimplifier().Simplify(source);

        Func<int, int, int, int> compiledSource = source.Compile();
        Func<int, int, int, int> compiledSimplified = simplified.Compile();

        // 8 / (3 / 2) = 8 / 1 = 8 under truncating integer division; the invalid field rewrite (x*z)/y = 16/3 = 5.
        Assert.AreEqual(8, compiledSource(8, 3, 2));
        Assert.AreEqual(compiledSource(8, 3, 2), compiledSimplified(8, 3, 2));
    }

    /// <summary>Regression: <c>(x / y) / (z / w)</c> over <see cref="int"/> operands must not be reassociated by <c>DivisionOfDivision</c> into the field-style <c>(x*w) / (y*z)</c>. Discriminating counterexample: <c>(9/2)/(4/3)</c> is <c>4</c> under truncation but <c>(9*3) / (2*4)</c> is <c>3</c>.</summary>
    [TestMethod]
    public void IntegerDivisionOfDivision_XOverYAllOverZOverW_MatchesTruncatingSourceSemantics()
    {
        ParameterExpression x = Expression.Parameter(typeof(int), "x");
        ParameterExpression y = Expression.Parameter(typeof(int), "y");
        ParameterExpression z = Expression.Parameter(typeof(int), "z");
        ParameterExpression w = Expression.Parameter(typeof(int), "w");
        var source = Expression.Lambda<Func<int, int, int, int, int>>(
            Expression.Divide(Expression.Divide(x, y), Expression.Divide(z, w)), x, y, z, w);

        var simplified = (Expression<Func<int, int, int, int, int>>)new ExpressionSimplifier().Simplify(source);

        Func<int, int, int, int, int> compiledSource = source.Compile();
        Func<int, int, int, int, int> compiledSimplified = simplified.Compile();

        // (9/2)/(4/3) = 4/1 = 4 under truncating integer division; the invalid field rewrite (x*w)/(y*z) = 27/8 = 3.
        Assert.AreEqual(4, compiledSource(9, 2, 4, 3));
        Assert.AreEqual(compiledSource(9, 2, 4, 3), compiledSimplified(9, 2, 4, 3));
    }

    /// <summary>Positive control: <see cref="Utils.Objects.Types.FloatingPointNumber"/> types are not truncating rings, so <c>DivisionOfDivision</c>'s field-style reassociation of <c>x / (y / z)</c> must remain available (and behavior-preserving) for <see cref="double"/> operands.</summary>
    [TestMethod]
    public void FloatingPointDivisionOfDivision_StillReassociates()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        var source = Expression.Lambda<Func<double, double, double, double>>(Expression.Divide(x, Expression.Divide(y, z)), x, y, z);

        var simplified = (Expression<Func<double, double, double, double>>)new ExpressionSimplifier().Simplify(source);

        AssertSameBehavior(source, simplified, 8.0, 3.0, 2.0);
        AssertSameBehavior(source, simplified, 1.0, 7.0, 4.0);
    }

    // ================================================================================================
    // 6. Lifted nullable arithmetic must preserve null propagation.
    // ================================================================================================

    /// <summary>Regression: lifted <c>int? x * 0</c> must preserve <see langword="null"/> propagation (<c>x == null</c> implies a <see langword="null"/> result) rather than being collapsed by <c>MultiplicationWithZeroOrOne</c> into the non-null constant <c>0</c>.</summary>
    [TestMethod]
    public void LiftedNullableMultiplicationByZero_PreservesNullPropagation()
    {
        ParameterExpression x = Expression.Parameter(typeof(int?), "x");
        var source = Expression.Lambda<Func<int?, int?>>(Expression.Multiply(x, Expression.Constant(0, typeof(int?))), x);

        var simplified = (Expression<Func<int?, int?>>)new ExpressionSimplifier().Simplify(source);
        Func<int?, int?> compiledSource = source.Compile();
        Func<int?, int?> compiledSimplified = simplified.Compile();

        Assert.IsNull(compiledSource(null));
        Assert.AreEqual(compiledSource(null), compiledSimplified(null));
        Assert.AreEqual(compiledSource(5), compiledSimplified(5));
    }

    /// <summary>Regression: lifted <c>0 / int? x</c> must preserve <see langword="null"/> propagation rather than being collapsed by <c>DivideWithZero</c> into the non-null constant <c>0</c>.</summary>
    [TestMethod]
    public void LiftedNullableDivisionOfZero_PreservesNullPropagation()
    {
        ParameterExpression x = Expression.Parameter(typeof(int?), "x");
        var source = Expression.Lambda<Func<int?, int?>>(Expression.Divide(Expression.Constant(0, typeof(int?)), x), x);

        var simplified = (Expression<Func<int?, int?>>)new ExpressionSimplifier().Simplify(source);
        Func<int?, int?> compiledSource = source.Compile();
        Func<int?, int?> compiledSimplified = simplified.Compile();

        Assert.IsNull(compiledSource(null));
        Assert.AreEqual(compiledSource(null), compiledSimplified(null));
        Assert.AreEqual(compiledSource(5), compiledSimplified(5));
    }

    /// <summary>Positive control: an ordinary, non-lifted <see cref="int"/> <c>x * 0</c> must still simplify to the constant <c>0</c>; the new lifted-arithmetic guard must not over-reject non-nullable operands.</summary>
    [TestMethod]
    public void NonLiftedIntMultiplicationByZero_StillSimplifies()
    {
        ParameterExpression x = Expression.Parameter(typeof(int), "x");
        var source = Expression.Lambda<Func<int, int>>(Expression.Multiply(x, Expression.Constant(0)), x);

        var simplified = (Expression<Func<int, int>>)new ExpressionSimplifier().Simplify(source);

        Assert.AreEqual(ExpressionType.Constant, simplified.Body.NodeType);
        Assert.AreEqual(0, simplified.Compile()(41));
    }

    // ================================================================================================
    // 7. Positive controls: ordinary built-in simplification keeps working.
    // ================================================================================================

    /// <summary>Positive control: ordinary <see cref="double"/> <c>x + 0</c> must still simplify to plain <c>x</c>; the new operator-safety guards must not disable this built-in identity.</summary>
    [TestMethod]
    public void OrdinaryDouble_AdditionWithZero_StillSimplifies()
    {
        Expression<Func<double, double>> source = x => x + 0;

        var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.AreEqual(ExpressionType.Parameter, simplified.Body.NodeType);
        Assert.AreEqual(4.0, simplified.Compile()(4.0));
    }

    /// <summary>Positive control: ordinary <see cref="double"/> <c>x * 1</c> must still simplify to plain <c>x</c>.</summary>
    [TestMethod]
    public void OrdinaryDouble_MultiplicationByOne_StillSimplifies()
    {
        Expression<Func<double, double>> source = x => x * 1;

        var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.AreEqual(ExpressionType.Parameter, simplified.Body.NodeType);
        Assert.AreEqual(6.0, simplified.Compile()(6.0));
    }

    /// <summary>Positive control: ordinary, non-lifted, non-custom commutative <see cref="double"/> addition must still canonicalize identically regardless of operand order, so <see cref="ExpressionComparer"/> still reports the two orderings as equal.</summary>
    [TestMethod]
    public void OrdinaryDouble_CanonicalAdditiveOrdering_StillReordersDeterministically()
    {
        ParameterExpression x = X("x");
        ParameterExpression y = X("y");
        // Both lambdas declare parameters in the SAME (x, y) position order; only the body's operand
        // order differs (param0+param1 vs param1+param0), so equality genuinely requires commutative
        // canonicalization rather than falling out of positional alpha-equivalence for free.
        var left = Expression.Lambda<Func<double, double, double>>(Expression.Add(x, y), x, y);
        var right = Expression.Lambda<Func<double, double, double>>(Expression.Add(y, x), x, y);

        Assert.IsTrue(ExpressionComparer.Default.Equals(left, right), "Ordinary commutative addition must still canonicalize for comparison.");
    }

    /// <summary>Positive control: a standard, non-custom <c>Expression.Power(double, double)</c> node (whose default <c>Method</c> resolves to <see cref="Math.Pow(double, double)"/>) must still be recognized as ordinary, so <c>x^1</c> still simplifies to plain <c>x</c>.</summary>
    [TestMethod]
    public void OrdinaryDouble_StandardPower_StillSimplifies()
    {
        ParameterExpression x = X();
        var source = Expression.Lambda<Func<double, double>>(Expression.Power(x, Expression.Constant(1.0)), x);

        var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.AreEqual(ExpressionType.Parameter, simplified.Body.NodeType);
        Assert.AreEqual(7.0, simplified.Compile()(7.0));
    }

    /// <summary>Positive control: ordinary <c>Log(x) + Log(y)</c> on the supported positive finite domain must still combine into <c>Log(x*y)</c>; the new outer-operator guard must not accidentally disable this S2-restored identity for the ordinary (non-custom) case.</summary>
    [TestMethod]
    public void OrdinaryDouble_LogarithmCombination_StillSimplifiesOnPositiveFiniteDomain()
    {
        Expression<Func<double, double, double>> source = (x, y) => double.Log(x) + double.Log(y);

        var simplified = (Expression<Func<double, double, double>>)new ExpressionSimplifier().Simplify(source);

        Assert.AreEqual(ExpressionType.Call, simplified.Body.NodeType, "Log(x) + Log(y) must still combine into Log(x*y).");
        AssertSameBehavior(source, simplified, 2.0, 3.0);
        AssertSameBehavior(source, simplified, 0.5, 8.0);
    }

    /// <summary>Positive control: ordinary <c>Sin(x) / Cos(x)</c> must still become <c>Tan(x)</c>; the new outer-operator guard must not disable this identity for the ordinary (non-custom) case.</summary>
    [TestMethod]
    public void OrdinaryDouble_SinOverCos_StillBecomesTan()
    {
        Expression<Func<double, double>> source = x => double.Sin(x) / double.Cos(x);

        var simplified = (Expression<Func<double, double>>)new ExpressionSimplifier().Simplify(source);

        AssertSameBehavior(source, simplified, 0.7);
        AssertSameBehavior(source, simplified, 1.3);
    }

    /// <summary>Positive control: unlike an arbitrary user-defined operator, <see cref="decimal"/>'s <c>op_Addition</c> is a genuine predefined CLR operator method (not a hard-coded <c>null</c>-<c>Method</c> assumption), so the structurally-probed operator-safety table classifies it as ordinary and <c>x + 0m</c> must still simplify to plain <c>x</c>.</summary>
    [TestMethod]
    public void OrdinaryDecimal_AdditionWithZero_PositiveControl()
    {
        ParameterExpression x = Expression.Parameter(typeof(decimal), "x");
        var source = Expression.Lambda<Func<decimal, decimal>>(Expression.Add(x, Expression.Constant(0m)), x);

        var simplified = (Expression<Func<decimal, decimal>>)new ExpressionSimplifier().Simplify(source);

        Assert.AreEqual(ExpressionType.Parameter, simplified.Body.NodeType, "decimal's op_Addition is a genuine predefined operator, not a user-defined one; the identity must still fire.");
        Assert.AreEqual(4m, simplified.Compile()(4m));
    }
}
