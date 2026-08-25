using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Utils.IO.Serialization;

namespace UtilsTest.Serialization;

/// <summary>Reflection contract used to verify strict boolean decoding through a serialized member.</summary>
public sealed class BoolContainer
{
    /// <summary>Gets or sets the boolean payload.</summary>
    [Field(1)] public bool Value { get; set; }
}

/// <summary>Generated contract used to verify strict boolean decoding through source-generated members.</summary>
[GenerateReaderWriter]
public sealed class GeneratedBoolContainer
{
    /// <summary>Gets or sets the boolean payload.</summary>
    [Field(1)] public bool Value { get; set; }
}

/// <summary>Test-only codec deliberately using non-canonical semantics to prove explicit overrides still win.</summary>
public sealed class PermissiveBoolCodec : IWireCodec<bool>
{
    /// <inheritdoc />
    public bool Read(IReader reader) => reader.ReadByte() != 0;
    /// <inheritdoc />
    public void Write(IWriter writer, bool value) => writer.WriteByte(value ? (byte)1 : (byte)0);
}

/// <summary>Verifies IO-11: canonical, strict one-byte boolean wire decoding.</summary>
[TestClass]
public sealed class BooleanWireFormatTests
{
    /// <summary>Verifies the writer emits exactly the canonical byte for each value.</summary>
    [TestMethod]
    public void Writer_EmitsCanonicalByte()
    {
        CollectionAssert.AreEqual(new byte[] { 0x00 }, Serialize(false));
        CollectionAssert.AreEqual(new byte[] { 0x01 }, Serialize(true));
    }

    /// <summary>Verifies the raw reader accepts only the two canonical bytes.</summary>
    [TestMethod]
    public void RawReader_AcceptsOnlyCanonicalBytes()
    {
        Assert.IsFalse(new RawReader().ReadBool(new Reader(new MemoryStream([0x00]))));
        Assert.IsTrue(new RawReader().ReadBool(new Reader(new MemoryStream([0x01]))));
    }

    /// <summary>Verifies every malformed byte from 2 through 255 is rejected as malformed wire data.</summary>
    [TestMethod]
    public void RawReader_RejectsEveryMalformedByte()
    {
        for (int value = 2; value <= byte.MaxValue; value++)
        {
            InvalidDataException exception = Assert.ThrowsExactly<InvalidDataException>(
                () => new RawReader().ReadBool(new Reader(new MemoryStream([(byte)value]))),
                $"Byte 0x{value:X2} must be rejected.");
            StringAssert.Contains(exception.Message, "0 or 1");
        }
    }

    /// <summary>Verifies the public generic reader path uses the strict built-in decoder.</summary>
    [TestMethod]
    public void GenericReader_UsesStrictDecoding()
    {
        Assert.IsFalse(new Reader(new MemoryStream([0x00])).Read<bool>());
        Assert.IsTrue(new Reader(new MemoryStream([0x01])).Read<bool>());
        Assert.ThrowsExactly<InvalidDataException>(() => new Reader(new MemoryStream([0x02])).Read<bool>());
        Assert.ThrowsExactly<InvalidDataException>(() => new Reader(new MemoryStream([0xFF])).Read<bool>());
    }

    /// <summary>Verifies an empty stream reports end-of-stream rather than malformed data.</summary>
    [TestMethod]
    public void GenericReader_EmptyStream_ThrowsEndOfStream()
    {
        Assert.ThrowsExactly<EndOfStreamException>(() => new Reader(new MemoryStream([])).Read<bool>());
    }

    /// <summary>Verifies boolean encoding is one byte and independent of the numeric byte-order option.</summary>
    [TestMethod]
    public void RawReaderWriter_IgnoreBigEndianOption()
    {
        foreach (bool bigEndian in new[] { false, true })
        {
            using MemoryStream stream = new();
            new RawWriter { BigEndian = bigEndian }.WriteBool(new Writer(stream), true);
            CollectionAssert.AreEqual(new byte[] { 0x01 }, stream.ToArray());
            stream.Position = 0;
            Assert.IsTrue(new RawReader { BigEndian = bigEndian }.ReadBool(new Reader(stream)));
        }
    }

    /// <summary>Verifies reflection-based members enforce the same strict decoding as the built-in reader.</summary>
    [TestMethod]
    public void ReflectionMember_UsesStrictDecoding()
    {
        Assert.IsFalse(Deserialize<BoolContainer>([0x00]).Value);
        Assert.IsTrue(Deserialize<BoolContainer>([0x01]).Value);
        Assert.ThrowsExactly<InvalidDataException>(() => Deserialize<BoolContainer>([0x02]));
    }

    /// <summary>Verifies generated members enforce the same strict decoding as the built-in reader.</summary>
    [TestMethod]
    public void GeneratedMember_UsesStrictDecoding()
    {
        Assert.IsFalse(ReadGeneratedBoolContainer([0x00]).Value);
        Assert.IsTrue(ReadGeneratedBoolContainer([0x01]).Value);
        Assert.ThrowsExactly<InvalidDataException>(() => ReadGeneratedBoolContainer([0x02]));
    }

    /// <summary>Verifies an explicitly registered custom codec still overrides the strict built-in reader.</summary>
    [TestMethod]
    public void ExplicitCodec_OverridesBuiltInStrictReader()
    {
        SerializationOptions options = new();
        options.Codecs.Set(new PermissiveBoolCodec());
        Reader reader = new(new MemoryStream([0x02]), options);
        Assert.IsTrue(reader.Read<bool>());
    }

    /// <summary>Verifies an explicitly supplied legacy converter still overrides the strict built-in reader.</summary>
    [TestMethod]
    public void ExplicitLegacyConverter_OverridesBuiltInStrictReader()
    {
        Func<IReader, bool> permissive = source => source.ReadByte() != 0;
        Reader reader = new(new MemoryStream([0x02]), [permissive]);
        Assert.IsTrue(reader.Read<bool>());
    }

    /// <summary>Verifies a boolean consumes exactly one physical byte against the IO-04 aggregate read budget.</summary>
    [TestMethod]
    public void AggregateReadBudget_ChargesExactlyOneByte()
    {
        Reader reader = new(new MemoryStream([0x01]), new ReaderOptions { MaximumReadBytes = 1 });
        Assert.IsTrue(reader.Read<bool>());

        Reader exhausted = new(new MemoryStream([0x01]), new ReaderOptions { MaximumReadBytes = 0 });
        Assert.ThrowsExactly<InvalidDataException>(() => exhausted.Read<bool>());
    }

    /// <summary>Serializes a boolean using the default little-endian raw writer.</summary>
    private static byte[] Serialize(bool value)
    {
        using MemoryStream stream = new();
        new Writer(stream).Write(value);
        return stream.ToArray();
    }

    /// <summary>Deserializes a reflection contract from literal wire bytes.</summary>
    private static T Deserialize<T>(byte[] bytes) where T : new() =>
        new Reader(new MemoryStream(bytes)).Read<T>();

    /// <summary>Deserializes the generated boolean contract from literal wire bytes.</summary>
    private static GeneratedBoolContainer ReadGeneratedBoolContainer(byte[] bytes) =>
        new Reader(new MemoryStream(bytes)).ReadUtilsTest_Serialization_GeneratedBoolContainer_9074e7e6();
}
