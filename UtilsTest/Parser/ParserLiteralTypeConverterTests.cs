using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies the closed, culture-invariant conversion model used by explicitly typed literal rule-call policies.
/// </summary>
[TestClass]
public class ParserLiteralTypeConverterTests
{
    /// <summary>
    /// Verifies exact scalar matches and object binding preserve their values and runtime types.
    /// </summary>
    [TestMethod]
    public void Convert_ExactMatchesAndObject_Succeed()
    {
        AssertSuccess(true, "bool", true);
        AssertSuccess(42, "int", 42);
        AssertSuccess("text", "string", "text");
        AssertSuccess('x', "char", 'x');
        AssertSuccess(42, "object", 42);
    }

    /// <summary>
    /// Verifies aliases, canonical framework names, and surrounding type whitespace are equivalent.
    /// </summary>
    [TestMethod]
    public void Convert_AliasesFrameworkNamesAndWhitespace_AreSupported()
    {
        AssertSuccess(42, "long", 42L);
        AssertSuccess(42, "System.Int64", 42L);
        AssertSuccess(42, " System.Int64 ", 42L);
        AssertSuccess(42, "System.Int32?", 42);
    }

    /// <summary>
    /// Verifies checked integral widening and narrowing and rejects overflow and negative unsigned values.
    /// </summary>
    [TestMethod]
    public void Convert_IntegralConversions_AreChecked()
    {
        AssertSuccess(42, "long", 42L);
        AssertSuccess(42L, "int", 42);
        AssertSuccess(42, "byte", (byte)42);
        AssertFailure(2147483648L, "int");
        AssertFailure(300, "byte");
        AssertFailure(-1, "uint");
    }

    /// <summary>
    /// Verifies integral-to-floating conversions require exact preservation and floating-to-integral conversion is rejected.
    /// </summary>
    [TestMethod]
    public void Convert_FloatingPointConversions_UseExactPolicy()
    {
        AssertSuccess(42, "double", 42d);
        AssertFailure(9007199254740993L, "double");
        AssertFailure(42d, "int");
        AssertSuccess(1.5d, "float", 1.5f);
        AssertFailure(0.1d, "float");
    }

    /// <summary>
    /// Verifies decimal accepts exact integral values but rejects double literals.
    /// </summary>
    [TestMethod]
    public void Convert_Decimal_AcceptsOnlyIntegralLiteralSources()
    {
        AssertSuccess(42, "decimal", 42m);
        AssertSuccess(42L, "System.Decimal", 42m);
        AssertFailure(42d, "decimal");
    }

    /// <summary>
    /// Verifies the deliberately small string and character conversion set.
    /// </summary>
    [TestMethod]
    public void Convert_StringAndCharacter_UseLimitedConversions()
    {
        AssertSuccess('x', "string", "x");
        AssertSuccess("x", "char", 'x');
        AssertFailure("hello", "char");
        AssertFailure("42", "int");
        AssertFailure("true", "bool");
    }

    /// <summary>
    /// Verifies Boolean values cannot bind to numeric targets.
    /// </summary>
    [TestMethod]
    public void Convert_BooleanToNumeric_IsRejected()
    {
        AssertFailure(true, "int");
        AssertFailure(false, "double");
    }

    /// <summary>
    /// Verifies nullability for value and reference targets.
    /// </summary>
    [TestMethod]
    public void Convert_Nullability_IsEnforcedForValueTypes()
    {
        AssertSuccess(null, "int?", null);
        AssertSuccess(null, "System.Int32?", null);
        AssertFailure(null, "int");
        AssertSuccess(null, "string", null);
        AssertSuccess(null, "string?", null);
        AssertSuccess(null, "object", null);
        AssertSuccess(null, "System.Object?", null);
    }

    /// <summary>
    /// Verifies unsupported and malformed target declarations fail without general type resolution.
    /// </summary>
    [TestMethod]
    public void Convert_UnsupportedOrMalformedTypes_AreRejected()
    {
        AssertFailure(1, "MyType");
        AssertFailure(1, "List<int>");
        AssertFailure(1, "int[]");
        AssertFailure(1, "System . Int32");
        AssertFailure(1, "int??");
        AssertFailure(1, string.Empty);
    }

    /// <summary>
    /// Asserts one successful conversion and its exact runtime value.
    /// </summary>
    /// <param name="value">Source literal value.</param>
    /// <param name="rawType">Declared target type.</param>
    /// <param name="expected">Expected converted value.</param>
    private static void AssertSuccess(object? value, string rawType, object? expected)
    {
        ParserLiteralConversionResult result = ParserLiteralTypeConverter.Convert(value, rawType);

        Assert.IsTrue(result.Success, result.Error);
        Assert.AreEqual(expected, result.Value);
        Assert.AreEqual(expected?.GetType(), result.Value?.GetType());
        Assert.IsNull(result.Error);
    }

    /// <summary>
    /// Asserts one conservative conversion failure.
    /// </summary>
    /// <param name="value">Source literal value.</param>
    /// <param name="rawType">Declared target type.</param>
    private static void AssertFailure(object? value, string rawType)
    {
        ParserLiteralConversionResult result = ParserLiteralTypeConverter.Convert(value, rawType);

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.Value);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Error));
    }
}
