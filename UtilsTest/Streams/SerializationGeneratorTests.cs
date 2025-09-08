using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.IO.Serialization;

namespace UtilsTest.Streams;

/// <summary>
/// Simple message used to validate the serialization generator.
/// </summary>
[GenerateReaderWriter]
public class SampleMessage
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    [Field(0)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    [Field(1)]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Represents a child object with manual serialization logic.
/// </summary>
public class CustomChild
{
    /// <summary>
    /// Gets or sets a custom numeric value.
    /// </summary>
    public int Value { get; set; }
}

/// <summary>
/// Provides reader and writer methods for <see cref="CustomChild"/>.
/// </summary>
public static class CustomChildSerialization
{
    /// <summary>
    /// Reads a <see cref="CustomChild"/> instance using the reader.
    /// </summary>
    /// <param name="reader">Source reader.</param>
    /// <returns>Deserialized child.</returns>
    public static CustomChild ReadCustomChild(this IReader reader)
    {
        return new CustomChild { Value = reader.Read<int>() };
    }

    /// <summary>
    /// Writes a <see cref="CustomChild"/> instance using the writer.
    /// </summary>
    /// <param name="writer">Target writer.</param>
    /// <param name="value">Instance to serialize.</param>
    public static void WriteCustomChild(this IWriter writer, CustomChild value)
    {
        writer.Write<int>(value.Value);
    }
}

/// <summary>
/// Message containing a child object with its own serialization methods.
/// </summary>
[GenerateReaderWriter]
public class ParentMessage
{
    /// <summary>Gets or sets the child.</summary>
    [Field(0)]
    public CustomChild Child { get; set; } = new();

    /// <summary>Gets or sets an additional integer value.</summary>
    [Field(1)]
    public int Extra { get; set; }
}

/// <summary>
/// Tests verifying compile-time serialization.
/// </summary>
[TestClass]
public class SerializationGeneratorTests
{
    /// <summary>
    /// Ensures that a roundtrip using generated methods preserves data.
    /// </summary>
    [TestMethod]
    public void GeneratedSerializerRoundtrip()
    {
        var original = new SampleMessage { Id = 42, Name = "Alice" };
        using var ms = new MemoryStream();
        var writer = new Writer(ms);
        writer.WriteSampleMessage(original);
        ms.Position = 0;
        var reader = new Reader(ms);
        var copy = reader.ReadSampleMessage();
        Assert.AreEqual(original.Id, copy.Id);
        Assert.AreEqual(original.Name, copy.Name);
    }

    /// <summary>
    /// Ensures that nested objects with custom serializers are respected.
    /// </summary>
    [TestMethod]
    public void GeneratedSerializerWithCustomChild()
    {
        var original = new ParentMessage { Child = new CustomChild { Value = 7 }, Extra = 3 };
        using var ms = new MemoryStream();
        var writer = new Writer(ms);
        writer.WriteParentMessage(original);
        ms.Position = 0;
        var reader = new Reader(ms);
        var copy = reader.ReadParentMessage();
        Assert.AreEqual(original.Child.Value, copy.Child.Value);
        Assert.AreEqual(original.Extra, copy.Extra);
    }
}

