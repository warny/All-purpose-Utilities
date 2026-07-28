using Utils.IO.Serialization;

using var stream = new MemoryStream();
var writer = new Writer(stream);
writer.WritePayload(new Payload { Value = 42 });
stream.Position = 0;
var copy = new Reader(stream).ReadPayload();
if (copy.Value != 42) throw new InvalidOperationException("Generated serializer did not round-trip the payload.");
Console.WriteLine("io-serialization-generator-executed");

/// <summary>Represents a generated-serialization acceptance payload.</summary>
[GenerateReaderWriter]
public partial class Payload
{
    /// <summary>Gets or sets the value that must survive a round trip.</summary>
    [Field(0)]
    public int Value { get; set; }
}

