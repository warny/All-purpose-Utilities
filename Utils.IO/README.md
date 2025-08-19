# Utils.IO Library

**Utils.IO** provides input/output helpers used across the other utility packages.
It targets **.NET 9** and focuses on working with streams and binary data while keeping processing logic separate from data structures.

## Features

- Stream extension methods for reading, copying and validating data
- Base16, base32 and base64 encoders/decoders that operate on streams
- A lightweight binary serialization framework built around interfaces
- Helpers to chain multiple output streams and to validate data while copying
- `PartialStream` for exposing a subsection of another stream

## Usage examples

```csharp
using var fs = File.OpenRead("data.bin");
byte[] header = fs.ReadBytes(16);
using var slice = new Utils.IO.PartialStream(fs, 16, 32);
byte[] chunk = slice.ReadBytes((int)slice.Length);

using var a = new MemoryStream();
using var b = new MemoryStream();
using var copier = new Utils.IO.StreamCopier(a, b);
copier.Write(chunk, 0, chunk.Length);

using var target = new MemoryStream();
using var validator = new Utils.IO.StreamValidator(target);
validator.Write(chunk, 0, chunk.Length);
validator.Validate();
```
