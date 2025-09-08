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
}

