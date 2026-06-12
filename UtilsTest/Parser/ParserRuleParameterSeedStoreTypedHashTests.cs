using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies deterministic seed hashing for scalar runtime types introduced by typed literal conversion.
/// </summary>
[TestClass]
public class ParserRuleParameterSeedStoreTypedHashTests
{
    /// <summary>
    /// Verifies equal converted values have equal hashes and different converted values have different hashes.
    /// </summary>
    [TestMethod]
    public void ConvertedValues_HashDeterministicallyByEffectiveValue()
    {
        Assert.AreEqual(Hash((byte)1), Hash((byte)1));
        Assert.AreNotEqual(Hash((byte)1), Hash((byte)2));
        Assert.AreEqual(Hash(1m), Hash(1m));
        Assert.AreNotEqual(Hash(1m), Hash(2m));
    }

    /// <summary>
    /// Verifies different literal source runtime types share a state hash after conversion to the same effective target value.
    /// </summary>
    [TestMethod]
    public void EquivalentConvertedTargetValues_HaveEqualHashes()
    {
        ParserLiteralConversionResult integerSource = ParserLiteralTypeConverter.Convert(1, "double");
        ParserLiteralConversionResult doubleSource = ParserLiteralTypeConverter.Convert(1d, "double");

        Assert.IsTrue(integerSource.Success);
        Assert.IsTrue(doubleSource.Success);
        Assert.AreEqual(Hash(integerSource.Value!), Hash(doubleSource.Value!));
    }

    /// <summary>
    /// Verifies runtime numeric type metadata remains part of the deterministic seed hash.
    /// </summary>
    [TestMethod]
    public void NumericallyEqualDifferentRuntimeTypes_HaveDifferentHashes()
    {
        Assert.AreNotEqual(Hash((byte)1), Hash(1));
        Assert.AreNotEqual(Hash(1), Hash(1L));
        Assert.AreNotEqual(Hash(1f), Hash(1d));
    }

    /// <summary>
    /// Verifies default-derived and explicit values hash by their final converted value, including present null.
    /// </summary>
    [TestMethod]
    public void DefaultAndExplicitEffectiveValues_HashByConvertedState()
    {
        ParserLiteralConversionResult defaultInt = ParserLiteralTypeConverter.Convert(42, "int");
        ParserLiteralConversionResult explicitInt = ParserLiteralTypeConverter.Convert(42, "int");
        ParserLiteralConversionResult differentInt = ParserLiteralTypeConverter.Convert(43, "int");
        ParserLiteralConversionResult defaultByte = ParserLiteralTypeConverter.Convert(42, "byte");
        ParserLiteralConversionResult explicitByte = ParserLiteralTypeConverter.Convert(42, "byte");

        Assert.AreEqual(Hash(defaultInt.Value), Hash(explicitInt.Value));
        Assert.AreEqual(Hash(defaultByte.Value), Hash(explicitByte.Value));
        Assert.AreNotEqual(Hash(defaultInt.Value), Hash(differentInt.Value));
        Assert.AreNotEqual(Hash(defaultInt.Value), Hash(defaultByte.Value));
        Assert.AreEqual(Hash(null), Hash(null));
    }

    /// <summary>
    /// Computes a seed-store state hash containing one value.
    /// </summary>
    /// <param name="value">Seed value.</param>
    /// <returns>The deterministic managed state hash.</returns>
    private static ulong Hash(object? value)
        => new ParserRuleParameterSeedStore().With("child", "value", value).GetParserExecutionStateHash();
}
