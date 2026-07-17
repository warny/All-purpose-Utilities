using System;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection;

namespace UtilsTest.Reflection;

/// <summary>
/// Tests for <see cref="TypeEx"/> extension methods.
/// </summary>
[TestClass]
public class TypeExTests
{
    // ─── Types used in tests ─────────────────────────────────────────────────────

    private class WithFields
    {
        public int Value;
        public string Name;
    }

    private class WithProperty
    {
        public int Count { get; set; }
    }

    private class WithNoMatchingMember { }

    // ─── GetPropertyOrField — item 63 ───────────────────────────────────────────

    [TestMethod]
    public void GetPropertyOrField_FindsExistingField()
    {
        PropertyOrFieldInfo? result = typeof(WithFields).GetPropertyOrField("Value");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsField);
        Assert.AreEqual("Value", result.Name);
    }

    [TestMethod]
    public void GetPropertyOrField_FindsExistingProperty()
    {
        PropertyOrFieldInfo? result = typeof(WithProperty).GetPropertyOrField("Count");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsProperty);
        Assert.AreEqual("Count", result.Name);
    }

    [TestMethod]
    public void GetPropertyOrField_ReturnsNull_WhenNotFound()
    {
        PropertyOrFieldInfo? result = typeof(WithNoMatchingMember).GetPropertyOrField("NonExistent");
        Assert.IsNull(result);
    }

    // ─── GetStaticMethods / GetStaticMethod — item 64 ───────────────────────────

    [TestMethod]
    public void GetStaticMethods_ThrowsForClosedGenericType()
    {
        // List<int> is a closed generic type — MakeGenericType on it would fail.
        Assert.ThrowsException<ArgumentException>(
            () => typeof(System.Collections.Generic.List<int>)
                .GetStaticMethods([typeof(int)], "ConvertAll"));
    }

    [TestMethod]
    public void GetStaticMethods_ThrowsForNonGenericType()
    {
        Assert.ThrowsException<ArgumentException>(
            () => typeof(string).GetStaticMethods([typeof(int)], "Concat"));
    }

    [TestMethod]
    public void GetStaticMethods_ThrowsForWrongArgCount()
    {
        // List<> has one generic parameter; passing two arguments should throw.
        Assert.ThrowsException<ArgumentException>(
            () => typeof(System.Collections.Generic.List<>)
                .GetStaticMethods([typeof(int), typeof(string)], "ConvertAll"));
    }

    [TestMethod]
    public void GetStaticMethod_ThrowsForClosedGenericType()
    {
        Assert.ThrowsException<ArgumentException>(
            () => typeof(System.Collections.Generic.List<int>)
                .GetStaticMethod([typeof(int)], "ConvertAll", [typeof(int)]));
    }

    // ─── IsAssignableFromEx — item 60 ───────────────────────────────────────────

    [TestMethod]
    public void IsAssignableFromEx_AllowsCSharpImplicitConversion_SbyteToInt()
    {
        Assert.IsTrue(typeof(int).IsAssignableFromEx(typeof(sbyte)));
    }

    [TestMethod]
    public void IsAssignableFromEx_AllowsCSharpImplicitConversion_IntToLong()
    {
        Assert.IsTrue(typeof(long).IsAssignableFromEx(typeof(int)));
    }

    [TestMethod]
    public void IsAssignableFromEx_AllowsCSharpImplicitConversion_IntToDouble()
    {
        Assert.IsTrue(typeof(double).IsAssignableFromEx(typeof(int)));
    }

    [TestMethod]
    public void IsAssignableFromEx_AllowsCSharpImplicitConversion_FloatToDouble()
    {
        Assert.IsTrue(typeof(double).IsAssignableFromEx(typeof(float)));
    }

    [TestMethod]
    public void IsAssignableFromEx_RejectsNarrowingConversion_LongToInt()
    {
        Assert.IsFalse(typeof(int).IsAssignableFromEx(typeof(long)));
    }

    [TestMethod]
    public void IsAssignableFromEx_RejectsSignedMismatch_UintToInt()
    {
        Assert.IsFalse(typeof(int).IsAssignableFromEx(typeof(uint)));
    }

    [TestMethod]
    public void IsAssignableFromEx_RejectsSignedMismatch_IntToUint()
    {
        Assert.IsFalse(typeof(uint).IsAssignableFromEx(typeof(int)));
    }

    [TestMethod]
    public void IsAssignableFromEx_AllowsSameType()
    {
        Assert.IsTrue(typeof(int).IsAssignableFromEx(typeof(int)));
    }

    [TestMethod]
    public void IsAssignableFromEx_AllowsReferenceTypeAssignability()
    {
        Assert.IsTrue(typeof(object).IsAssignableFromEx(typeof(string)));
    }
}
