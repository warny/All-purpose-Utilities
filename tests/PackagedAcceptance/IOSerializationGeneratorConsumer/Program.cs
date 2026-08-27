using Utils.IO.Serialization;

// The generator names methods from the type's stable identity + a deterministic FNV-1a hash (see
// Utils.IO.Serialization.Generators/README.md), not the plain type name: WritePayload_a0639745 /
// ReadPayload_a0639745 for this exact "Payload" type declared in the global namespace. The
// generated source itself (obj/**/generated/.../Payload_a0639745.Serialization.g.cs) is the
// authoritative source for these names; they are not guessed or hand-derived here.
using var stream = new MemoryStream();
var writer = new Writer(stream);
writer.WritePayload_a0639745(new Payload { Value = 42 });
stream.Position = 0;
var copy = new Reader(stream).ReadPayload_a0639745();
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

