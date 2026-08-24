# omy.Utils.IO (stream helpers)

`omy.Utils.IO` delivers stream utilities, binary validators, base16/base32/base64 encoders, and a reflection-driven binary serialization framework.

## Install
```bash
dotnet add package omy.Utils.IO
```

## Supported frameworks
- net8.0

## Features
- `StreamUtils` extension methods for reading, writing, copying, and slicing streams.
- `PartialStream` — a seekable slice/window over an underlying stream.
- `StreamCopier` — fan-out writer that broadcasts to multiple streams simultaneously.
- `StreamValidator` — buffered writer with explicit commit (`Validate`) or discard semantics.
- Base16/Base32/Base64 encoding and decoding via streaming classes or convenience converters.
- `Reader` / `Writer` — reflection-driven binary serialization using `[Field]` attributes.

## Quick usage
```csharp
using Utils.IO;
using Utils.IO.BaseEncoding;

byte[] data = [0xDE, 0xAD, 0xBE, 0xEF];
string hex = Bases.Base16.ToString(data);   // "DEADBEEF"
string b64 = Bases.Base64.ToString(data);   // "3q2+7w=="
byte[] back = Bases.Base64.FromString(b64); // [0xDE, 0xAD, 0xBE, 0xEF]
```

## StreamUtils examples

Extension methods on `Stream` for common low-level I/O operations.

### Read helpers

```csharp
using System.IO;
using Utils.IO;

using var fs = File.OpenRead("data.bin");

// Read exactly N bytes (throws EndOfStreamException if stream ends early when raiseException=true)
byte[] header = fs.ReadBytes(16);
byte[] strict = fs.ReadBytes(16, raiseException: true);

// Read all remaining bytes
byte[] rest = fs.ReadToEnd();

// Read as text
string text = fs.ReadAllText();                        // UTF-8
string latin = fs.ReadAllText(System.Text.Encoding.Latin1);

// Lazy line enumeration
foreach (string line in fs.ReadLines())
    Console.WriteLine(line);

// Read up to the next CRLF delimiter
byte[] block = fs.ReadBlock(new byte[] { 0x0D, 0x0A });
```

### Write helpers

```csharp
using var ms = new MemoryStream();
ms.WriteBytes(new byte[] { 1, 2, 3 });
ms.WriteAllText("hello");
```

### Copy and buffer

```csharp
using var src = File.OpenRead("input.bin");
using var dst = File.Create("output.bin");
src.CopyToStream(dst, bufferSize: 65536);

// Load into a rewound MemoryStream
MemoryStream buffer = src.ReadToMemoryStream();
```

## PartialStream examples

`PartialStream` exposes a bounded slice of a seekable stream without copying data. Views over the same underlying stream share synchronization so their temporary seeks cannot interfere. Disposing a view does not close the underlying stream or invalidate the synchronization used by another view.

```csharp
using Utils.IO;

using var fs = File.OpenRead("archive.bin");

// Slice starting at offset 64, length 128 bytes
using var slice = new PartialStream(fs, position: 64, length: 128);
byte[] chunk = slice.ReadBytes((int)slice.Length);

// Slice from the stream's current position
fs.Position = 200;
using var tail = new PartialStream(fs, length: 50);
```

Length-prefixed strings and `BigInteger` values have no additional payload limit by default, preserving historical round trips. Set an explicit limit when reading untrusted input:

```csharp
using Utils.IO.Serialization;

var options = new ReaderOptions { MaximumPayloadLength = 4 * 1024 * 1024 };
var reader = new Reader(stream, options);
string value = reader.Read<string>();
```

The limit is measured in payload bytes. `null` means unlimited, zero accepts only empty payloads, and negative limits are rejected when the reader is constructed.

## StreamCopier examples

`StreamCopier` is a write-only stream that mirrors every `Write` call to all registered target streams. It implements `IList<Stream>` so targets can be added or removed at runtime.

```csharp
using System.IO;
using Utils.IO;

using var file   = File.Create("log.bin");
using var memory = new MemoryStream();

// Write to both targets at once
using var copier = new StreamCopier(file, memory);
copier.WriteBytes(new byte[] { 0x01, 0x02, 0x03 });

// Optionally dispose targets when the copier is disposed
using var autoClose = new StreamCopier(closeAllTargetsOnDispose: true,
    File.Create("a.bin"), File.Create("b.bin"));
autoClose.WriteBytes(new byte[] { 0xFF });
// a.bin and b.bin are closed here

// Dynamic target management (IList<Stream>)
var dynamic = new StreamCopier();
dynamic.Add(new MemoryStream());
dynamic.Add(new MemoryStream());
dynamic.RemoveAt(0);
```

## StreamValidator examples

`StreamValidator` buffers all writes and only flushes them to the underlying stream when `Validate()` is called. Call `Discard()` to abandon the buffer.

```csharp
using System.IO;
using Utils.IO;

using var output = new MemoryStream();
using var validator = new StreamValidator(output);

validator.WriteBytes(new byte[] { 1, 2, 3 });

// Check whether the data looks right before committing
if (/* validation passes */ true)
    validator.Validate();  // writes buffer → output
else
    validator.Discard();   // drops the buffer
```

## Base encoding examples

`Bases` exposes ready-made `Base16`, `Base32`, and `Base64` converters that implement both `IBaseConverter` (convenience string methods) and `IBaseDescriptor` (used by the streaming classes).

### Convenience converters

```csharp
using Utils.IO.BaseEncoding;

byte[] data = [0x48, 0x65, 0x6C, 0x6C, 0x6F]; // "Hello"

string hex = Bases.Base16.ToString(data);           // "48656C6C6F"
string b32 = Bases.Base32.ToString(data);           // "JBSWY3DP"
string b64 = Bases.Base64.ToString(data);           // "SGVsbG8="

// Line-wrapped output (e.g. PEM-style at 64 chars)
string pem = Bases.Base64.ToString(data, maxDataWidth: 64, indent: 0);

// Decode
byte[] fromHex = Bases.Base16.FromString("48656C6C6F");
byte[] fromB64 = Bases.Base64.FromString("SGVsbG8=");
```

### Custom descriptor

```csharp
// Build a custom base-8 (octal) descriptor
var octal = new CustomDescriptor("01234567", separator: "\n");
// Then use BaseEncoderStream / BaseDecoderStream directly
```

### Streaming encode/decode

```csharp
using System.IO;
using Utils.IO.BaseEncoding;

// Encode a stream to a string
using var sw = new System.IO.StringWriter();
using var encoder = new BaseEncoderStream(sw, Bases.Base64);
encoder.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);
encoder.Close();
string encoded = sw.ToString();  // "AQIDBA=="

// Decode a string to a stream
using var ms = new MemoryStream();
using var decoder = new BaseDecoderStream(ms, Bases.Base64);
decoder.Write("AQIDBA==");
decoder.Close();
byte[] decoded = ms.ToArray();  // [1, 2, 3, 4]
```

## Binary serialization examples

`Reader` and `Writer` serialize objects by reflecting members decorated with `[Field(order)]`. Primitive types (`byte`, `short`, `int`, `long`, `float`, `double`, etc.) are handled automatically by built-in delegates.

Raw extended integers have explicit portable wire contracts: `Int128` and `UInt128` occupy 16 bytes in the selected numeric byte order, while `BigInteger` uses an endian-aware `Int32` byte-length prefix and a minimal signed two's-complement payload in the same byte order. GUIDs use the canonical 16-byte RFC/network layout regardless of the numeric byte-order option.

Boolean values use a canonical one-byte wire representation: `00` is false and `01` is true. Other byte values are malformed and rejected.

### Define a serializable struct

```csharp
using Utils.IO.Serialization;

public class FileHeader
{
    [Field(0)] public uint Magic;
    [Field(1)] public ushort Version;
    [Field(2)] public uint DataOffset;
}
```

### Write

```csharp
using System.IO;
using Utils.IO.Serialization;

using var fs = File.Create("output.bin");
var writer = new Writer(fs);

var header = new FileHeader { Magic = 0xDEADBEEF, Version = 1, DataOffset = 64 };
writer.Write(header);              // serializes fields in [Field] order
writer.Write<int>(42);            // write a primitive directly
writer.WriteByte(0xFF);
```

### Read

```csharp
using var fs = File.OpenRead("output.bin");
var reader = new Reader(fs);

var header = reader.Read<FileHeader>();
Console.WriteLine(header.Magic);    // 0xDEADBEEF
Console.WriteLine(header.Version);  // 1

int value = reader.Read<int>();     // 42

// Slice — create a sub-reader for a region
Reader sub = reader.Slice(position: 16, length: 32);
```

### Push/pop position bookmarks

```csharp
reader.Push();                             // save current position
reader.Push(offset: 100, SeekOrigin.Begin); // save and seek
reader.Pop();                              // restore
```

## Related packages
- `omy.Utils` – foundational helpers referenced by the I/O utilities.
- `omy.Utils.Reflection` – `PropertyOrFieldInfo` used by the serialization framework.


[Versioned API documentation](https://warny.github.io/All-purpose-Utilities/v2.0.0-rc.1/)

## Serialization contract rules

`[Field(order)]` defines the binary format. Orders must be unique within a type; negative orders are supported and sort before non-negative orders. Reflection and generated serializers sort numerically and never use reflection enumeration order. Deserialization requires a concrete closed type with an accessible parameterless constructor and public writable instance members; serialization requires public readable instance members.

Runtime contract discovery, validation, converter resolution, delegate generation, caching, and execution are separate stages. Invalid reflection contracts raise an aggregated `SerializationContractException` (`UIORT001`–`UIORT007`) before expression compilation. Recursive value contracts are rejected because the package does not implicitly implement object identities or graph references.

Custom runtime converters have the signatures `T ReadX(IReader)` and `void WriteX(IWriter, T)`. Readers require an exact return-type registration. Writers accept base/interface registrations, select the most specific applicable type, and reject equal-specificity ambiguity. The source generator recognizes accessible static methods with the same exact signatures, retains the selected Roslyn symbol, and emits a fully qualified call. Convention names (`Read{Type}`/`Write{Type}`) remain supported but ambiguous exact matches are errors.

A `Reader` or `Writer` coordinates concurrent first-time contract construction with explicit shared states and a cross-thread dependency graph, sharing both successful delegates and structured failures without mutual-wait deadlocks. Stream position stacks and object execution remain instance state and should not be manipulated concurrently by callers.

`Reader.TryGetBytesLeft(out long)` reports a value only for a coherent seekable stream. `Writer.TryGetBytesUntilCurrentLength(out long)` deliberately describes distance to the current length—not writable capacity; the old `Writer.BytesLeft` member is obsolete. Failed `Push(offset, origin)` calls do not add stack entries and attempt to restore position without hiding the original seek error.

`PartialStream` implements Span and Memory-based async operations with identical slice limits. Async operations delegate to the underlying stream, honor cancellation, restore the underlying position, and do not hold a synchronous monitor across an `await`. `FlushAsync` flushes without finalizing an encoding quantum, and disposing a partial view never disposes its underlying stream.

## Wire codecs and framing

`Reader` and `Writer` use exact-type wire-codec registrations from a snapshot of `SerializationOptions`. Codecs only transform values to and from payloads; framing independently describes whether the payload is fixed-width, length-prefixed, or self-delimited (`CodecOwned`). A member-level `[WireCodec]` or `[WireFraming]` override wins over an explicitly registered type codec. An explicit type codec wins over legacy converters supplied to the `Reader` or `Writer`; those converters in turn win over built-in fallbacks such as the default DateTime representation.

### Select a DateTime format globally

The default DateTime representation is .NET Binary. Register another exact-type codec when an entire wire contract uses a different representation:

```csharp
using Utils.IO.Serialization;

SerializationOptions options = new();
options.Codecs.Set(new UnixSecondsDateTimeCodec());

using MemoryStream stream = new();
Writer writer = new(stream, options);
writer.Write(new DateTime(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc));

stream.Position = 0;
Reader reader = new(stream, options);
DateTime value = reader.Read<DateTime>(); // DateTimeKind.Utc
```

Available codecs are `DotNetBinaryDateTimeCodec`, `TicksDateTimeCodec`, `UnixSecondsDateTimeCodec`, `UnixMillisecondsDateTimeCodec`, `OleAutomationDateTimeCodec`, and `FileTimeDateTimeCodec`.

| Codec | Wire primitive | Kind on read |
|---|---|---|
| .NET Binary | `Int64` | Preserved by framework binary semantics |
| Ticks | `Int64` | `Unspecified` |
| Unix seconds | `Int64` | `Utc` |
| Unix milliseconds | `Int64` | `Utc` |
| OLE Automation | `Double` | Framework `FromOADate` semantics (`Unspecified`) |
| FILETIME | `Int64` | `Utc` |

### Override one serialized member

Use `[WireCodec]` without changing the codec selected for other members of the same type:

```csharp
using Utils.IO.Serialization;

public sealed class EventRecord
{
    [Field(1)]
    public DateTime Created { get; set; } // default .NET Binary

    [Field(2)]
    [WireCodec(typeof(UnixMillisecondsDateTimeCodec))]
    public DateTime ImportedTimestamp { get; set; } // signed Unix milliseconds
}
```

The reflection serializer and serializers produced by `[GenerateReaderWriter]` honor the same member override and runtime registration precedence.

### Register a codec for an application type

A fixed codec declares its exact payload size and can use the existing primitive operations, including their configured byte order:

```csharp
using Utils.IO.Serialization;

public readonly record struct CustomerId(int Value);

public sealed class CustomerIdCodec : IFixedWireCodec<CustomerId>
{
    public int Size => sizeof(int);

    public CustomerId Read(IReader reader) => new(reader.Read<int>());

    public void Write(IWriter writer, CustomerId value) =>
        writer.Write(value.Value);
}

SerializationOptions options = new();
options.Codecs.Set(new CustomerIdCodec());

using MemoryStream stream = new();
new Writer(stream, options).Write(new CustomerId(42));
stream.Position = 0;
CustomerId copy = new Reader(stream, options).Read<CustomerId>();
```

`Reader` and `Writer` snapshot the registry when they are constructed. Changing `options.Codecs` afterward does not change those instances or slices created from them.

### Register only one direction

Reader and writer registrations are independent. This is useful while migrating a legacy format whose reader must remain available but whose writer is no longer used:

```csharp
SerializationOptions options = new();
options.Codecs.SetReader<CustomerId>(new CustomerIdCodec());

Reader reader = new(stream, options);
CustomerId id = reader.Read<CustomerId>();
```

If the opposite direction has no codec, normal legacy-converter or generated/reflection fallback resolution still applies.

### Choose framing and bounded buffering

Framing stays separate from codecs:

```csharp
using System.Collections.Generic;
using System.Text;
using Utils.IO.Serialization;

public readonly record struct Utf8Text(string Value);

public sealed class Utf8TextCodec : IWireCodec<Utf8Text>
{
    public Utf8Text Read(IReader reader)
    {
        List<byte> bytes = [];
        for (int value = reader.ReadByte(); value >= 0; value = reader.ReadByte())
        {
            bytes.Add((byte)value);
        }

        return new Utf8Text(Encoding.UTF8.GetString([.. bytes]));
    }

    public void Write(IWriter writer, Utf8Text value) =>
        writer.WriteBytes(Encoding.UTF8.GetBytes(value.Value));
}

SerializationOptions options = new()
{
    VariablePayloadWritePolicy = VariablePayloadWritePolicy.AllowBuffering,
    MaximumBufferedPayloadLength = 64 * 1024
};

options.Codecs.Set(
    new Utf8TextCodec(),
    new Int32LengthWireFraming());
```

`Utf8TextCodec` does not know its payload size before encoding. A seekable writer can therefore backpatch the fixed-width `Int32` prefix; a forward-only writer uses the explicitly enabled bounded staging buffer. If a codec implements `IWireSizeProvider<T>`, the writer can instead emit the known length before the payload without seeking or buffering.

Fixed framing and sizes reported by `IWireSizeProvider<T>` are enforced with a non-buffering, bounded counting writer, including on forward-only streams. It rejects an overflow before bytes beyond the declaration reach the target and reports an underwrite after the codec returns.

A known payload length is written before the payload and works on forward-only streams. Because its prefix is written first, a later codec underwrite is reported but does not roll back that prefix or already-written payload bytes. For an unknown size, a seekable writer may backpatch only a fixed-width prefix. Other cases fail before target output unless `AllowBuffering` and a finite `MaximumBufferedPayloadLength` are explicitly configured; the staging stream enforces that limit while the codec writes, before the target is modified. Variable-width prefixes are never treated as fixed reservations. `CodecOwned` codecs remain responsible for recognizing their own termination.

### Bound reads from untrusted data

The three reader limits are deliberately independent and opt in. `null` retains the unlimited historical behavior:

```csharp
ReaderOptions limits = new()
{
    MaximumPayloadLength = 1024 * 1024, // each string, BigInteger, or framed codec payload
    MaximumReadBytes = 4L * 1024 * 1024, // prefixes plus payload bytes across the entire operation tree
    MaximumCollectionLength = 100_000    // elements accepted before collection allocation
};

Reader reader = new(untrustedStream, limits, serializationOptions);
Customer value = reader.Read<Customer>();
```

`MaximumPayloadLength` remains a per-payload limit and is not a decreasing global budget.
`MaximumReadBytes` counts bytes actually returned from the wire, including length prefixes. Slices,
mapped readers, reflection and generated contracts, nested contracts, and codec-owned reads share the
same budget. Seeking does not refund bytes, so rereading consumes budget again. Length-prefixed and
fixed framed codec bytes are charged while staging from the physical source and are not charged again
when the bounded child reader decodes that already-accounted buffer.

`MaximumCollectionLength` separately protects allocations whose element count came from untrusted
data; a byte budget cannot infer arbitrary managed element sizes. These controls bound deterministic
wire parsing and do not estimate CLR object overhead or total process memory.
