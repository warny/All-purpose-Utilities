using System;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Characterization coverage for <see cref="ExpressionCallSignatureAttribute.Match(Expression)"/> and
/// <see cref="ConstantNumericAttribute.Match(Expression)"/>, the two hot-path signature matchers used by
/// <c>ExpressionTransformer</c> rule dispatch. #587 replaces the <c>Enumerable.Any</c> traversals inside
/// both overrides with indexed loops, purely as allocation/scaffolding cleanup — no dispatch-order,
/// exception, or short-circuit behavior changes. These tests lock the exact observable baseline
/// (type-before-name evaluation order, public <see cref="ExpressionCallSignatureAttribute.Types"/> array
/// mutability, null/invalid-input exception identity, and per-element <c>Convert.ToDouble</c> evaluation)
/// so the traversal rewrite can be verified not to alter any of it.
/// </summary>
[TestClass]
public class ExpressionSignatureAttributeMatchTests
{
    private static readonly MethodInfo SqrtMethod =
        typeof(double).GetMethod(nameof(double.Sqrt), BindingFlags.Public | BindingFlags.Static, [typeof(double)])!;

    private static readonly MethodInfo SinMethod =
        typeof(double).GetMethod(nameof(double.Sin), BindingFlags.Public | BindingFlags.Static, [typeof(double)])!;

    private static MethodCallExpression SqrtCall() =>
        (MethodCallExpression)Expression.Call(SqrtMethod, Expression.Parameter(typeof(double), "x"));

    private static MethodCallExpression SinCall() =>
        (MethodCallExpression)Expression.Call(SinMethod, Expression.Parameter(typeof(double), "x"));

    // ------------------------------------------------------------------------------------------
    // ExpressionCallSignatureAttribute
    // ------------------------------------------------------------------------------------------

    /// <summary>Item 1: a non-<see cref="MethodCallExpression"/> node never matches.</summary>
    [TestMethod]
    public void CallSignature_NonMethodCallExpression_ReturnsFalse()
    {
        var attribute = new ExpressionCallSignatureAttribute(typeof(double), nameof(double.Sqrt));
        Assert.IsFalse(attribute.Match(Expression.Constant(1.0)));
    }

    /// <summary>Item 2: exact declaring type + exact name matches.</summary>
    [TestMethod]
    public void CallSignature_ExactTypeAndName_ReturnsTrue()
    {
        var attribute = new ExpressionCallSignatureAttribute(typeof(double), nameof(double.Sqrt));
        Assert.IsTrue(attribute.Match(SqrtCall()));
    }

    /// <summary>Item 3: exact declaring type but wrong name does not match — the type check alone is insufficient.</summary>
    [TestMethod]
    public void CallSignature_ExactTypeWrongName_ReturnsFalse()
    {
        var attribute = new ExpressionCallSignatureAttribute(typeof(double), nameof(double.Log));
        Assert.IsFalse(attribute.Match(SqrtCall()));
    }

    /// <summary>Item 4: with multiple <c>Types</c> entries, a later entry can still match; declaration order is preserved.</summary>
    [TestMethod]
    public void CallSignature_MultipleTypes_LaterEntryMatches()
    {
        var attribute = new ExpressionCallSignatureAttribute([typeof(string), typeof(double)], nameof(double.Sqrt));
        Assert.IsTrue(attribute.Match(SqrtCall()));
    }

    /// <summary>Item 5: an empty <c>Types</c> array never matches, regardless of the call.</summary>
    [TestMethod]
    public void CallSignature_EmptyTypes_ReturnsFalse()
    {
        var attribute = new ExpressionCallSignatureAttribute([], nameof(double.Sqrt));
        Assert.IsFalse(attribute.Match(SqrtCall()));
    }

    /// <summary>
    /// Item 6: mutating the public <see cref="ExpressionCallSignatureAttribute.Types"/> array reference after
    /// construction affects subsequent <c>Match</c> calls — the array is read live, never snapshotted.
    /// </summary>
    [TestMethod]
    public void CallSignature_MutatingTypesArray_AffectsSubsequentMatch()
    {
        var attribute = new ExpressionCallSignatureAttribute([typeof(string)], nameof(double.Sqrt));
        var call = SqrtCall();

        Assert.IsFalse(attribute.Match(call));

        attribute.Types[0] = typeof(double);

        Assert.IsTrue(attribute.Match(call));
    }

    /// <summary>Item 7: a generic interface definition (<c>ITrigonometricFunctions&lt;&gt;</c>) still matches through <c>TypeEx.IsDefinedBy</c>.</summary>
    [TestMethod]
    public void CallSignature_GenericInterfaceDefinition_MatchesThroughIsDefinedBy()
    {
        var attribute = new ExpressionCallSignatureAttribute(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Sin));
        Assert.IsTrue(attribute.Match(SinCall()));
    }

    /// <summary>Item 8: <c>Types == null</c> preserves the baseline exception type and <c>ParamName</c> (from <c>Enumerable.Any</c>'s own null-source guard).</summary>
    [TestMethod]
    public void CallSignature_NullTypes_ThrowsArgumentNullExceptionForSource()
    {
        var attribute = new ExpressionCallSignatureAttribute((Type[])null!, nameof(double.Sqrt));

        var ex = Assert.ThrowsExactly<ArgumentNullException>(() => attribute.Match(SqrtCall()));
        Assert.AreEqual("source", ex.ParamName);
    }

    /// <summary>Item 9: a null element inside <c>Types</c> preserves the baseline exception raised by <c>TypeEx.IsDefinedBy</c>'s own null-guard on <c>baseType</c>.</summary>
    [TestMethod]
    public void CallSignature_NullTypeElement_ThrowsArgumentNullExceptionForBaseType()
    {
        var attribute = new ExpressionCallSignatureAttribute([null!], nameof(double.Sqrt));

        var ex = Assert.ThrowsExactly<ArgumentNullException>(() => attribute.Match(SqrtCall()));
        Assert.AreEqual("baseType", ex.ParamName);
    }

    /// <summary>
    /// Item 10: a null <c>Types</c> element is inspected — and throws — before the method-name comparison
    /// even runs, even when the call's method name would not have matched anyway. This locks the
    /// type-before-name evaluation order: a naive "reject wrong names first" rewrite would instead return
    /// <see langword="false"/> here.
    /// </summary>
    [TestMethod]
    public void CallSignature_NullTypeElement_ThrowsEvenWhenNameWouldNotHaveMatched()
    {
        var attribute = new ExpressionCallSignatureAttribute([null!], "ThisNameWouldNeverMatch");

        Assert.ThrowsExactly<ArgumentNullException>(() => attribute.Match(SqrtCall()));
    }

    /// <summary>Item 11: <c>FunctionName == null</c> preserves baseline behavior — matching type, then a harmless <c>false</c> string comparison, no exception.</summary>
    [TestMethod]
    public void CallSignature_NullFunctionName_ReturnsFalseWithoutThrowing()
    {
        var attribute = new ExpressionCallSignatureAttribute(typeof(double), null!);
        Assert.IsFalse(attribute.Match(SqrtCall()));
    }

    // ------------------------------------------------------------------------------------------
    // ConstantNumericAttribute
    // ------------------------------------------------------------------------------------------

    /// <summary>Item 1: a non-<see cref="ConstantExpression"/> node never matches.</summary>
    [TestMethod]
    public void ConstantNumeric_NonConstantExpression_ReturnsFalse()
    {
        var attribute = new ConstantNumericAttribute();
        Assert.IsFalse(attribute.Match(Expression.Parameter(typeof(double), "x")));
    }

    /// <summary>Item 2: a constant with a nonnumeric or null value never matches.</summary>
    [TestMethod]
    public void ConstantNumeric_NonNumericOrNullValue_ReturnsFalse()
    {
        var attribute = new ConstantNumericAttribute();
        Assert.IsFalse(attribute.Match(Expression.Constant("hello")));
        Assert.IsFalse(attribute.Match(Expression.Constant(null, typeof(object))));
    }

    /// <summary>Item 3: the default constructor (<c>Values == null</c>) accepts any numeric constant.</summary>
    [TestMethod]
    public void ConstantNumeric_DefaultConstructor_AcceptsAnyNumericConstant()
    {
        var attribute = new ConstantNumericAttribute();
        Assert.IsTrue(attribute.Match(Expression.Constant(5.0)));
        Assert.IsTrue(attribute.Match(Expression.Constant(-123.456)));
    }

    /// <summary>Item 4: an explicit one-value set matches only that value.</summary>
    [TestMethod]
    public void ConstantNumeric_SingleAllowedValue_MatchesOnlyThatValue()
    {
        var attribute = new ConstantNumericAttribute(0);
        Assert.IsTrue(attribute.Match(Expression.Constant(0.0)));
        Assert.IsFalse(attribute.Match(Expression.Constant(1.0)));
    }

    /// <summary>Item 5: with multiple allowed values, the first entry, the last entry, and a miss all resolve correctly.</summary>
    [TestMethod]
    public void ConstantNumeric_MultipleAllowedValues_FirstLastAndMiss()
    {
        var attribute = new ConstantNumericAttribute(0, 1, -1);
        Assert.IsTrue(attribute.Match(Expression.Constant(0.0)));
        Assert.IsTrue(attribute.Match(Expression.Constant(-1.0)));
        Assert.IsFalse(attribute.Match(Expression.Constant(2.0)));
    }

    /// <summary>Item 6: an explicit but empty allowed-values set never matches, unlike the default (null) constructor.</summary>
    [TestMethod]
    public void ConstantNumeric_EmptyAllowedValues_ReturnsFalse()
    {
        var attribute = new ConstantNumericAttribute(System.Array.Empty<double>());
        Assert.IsFalse(attribute.Match(Expression.Constant(0.0)));
    }

    /// <summary>Item 7: <see cref="double.NaN"/> never equality-matches, including against itself as an allowed value — no special-casing.</summary>
    [TestMethod]
    public void ConstantNumeric_NaN_NeverEqualityMatches()
    {
        var unrestricted = new ConstantNumericAttribute();
        Assert.IsTrue(unrestricted.Match(Expression.Constant(double.NaN)));

        var restrictedToNaN = new ConstantNumericAttribute(double.NaN);
        Assert.IsFalse(restrictedToNaN.Match(Expression.Constant(double.NaN)));
    }

    /// <summary>Item 8: a few common numeric CLR types retain their current matching result under the default (unrestricted) constructor.</summary>
    [TestMethod]
    public void ConstantNumeric_VariousNumericClrTypes_AllMatchUnderDefaultConstructor()
    {
        var attribute = new ConstantNumericAttribute();
        Assert.IsTrue(attribute.Match(Expression.Constant(5)));
        Assert.IsTrue(attribute.Match(Expression.Constant(5f)));
        Assert.IsTrue(attribute.Match(Expression.Constant(5.0)));
        Assert.IsTrue(attribute.Match(Expression.Constant(5m)));
    }
}
