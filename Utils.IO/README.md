# omy.Utils.IO (stream helpers)

`omy.Utils.IO` delivers stream utilities, binary validators, and base16/base32/base64 encoders for day-to-day I/O scenarios.

## Install
```bash
dotnet add package omy.Utils.IO
```

## Supported frameworks
- net8.0

## Features
- Stream extensions for reading, copying, slicing, and validating data.
- Base16, base32, and base64 encoders/decoders that operate on streams.
- Lightweight binary serialization framework built around interfaces.
- Helpers to chain multiple output streams and expose partial segments.

## Quick usage
```csharp
using var fs = File.OpenRead("data.bin");
byte[] header = fs.ReadBytes(16);
using var slice = new Utils.IO.PartialStream(fs, 16, 32);
byte[] chunk = slice.ReadBytes((int)slice.Length);

using var target = new MemoryStream();
using var validator = new Utils.IO.StreamValidator(target);
validator.Write(chunk, 0, chunk.Length);
validator.Validate();
```

## Related packages
- `omy.Utils` – foundational helpers referenced by the I/O utilities.
- `omy.Utils.VirtualMachine` – used for some binary parsing scenarios.
