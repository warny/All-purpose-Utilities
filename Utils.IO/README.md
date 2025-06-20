# Utils.IO Library

**Utils.IO** provides input/output helpers used across the other utility packages.
It focuses on working with streams and binary data while keeping processing logic separate from data structures.

## Features

- Stream extension methods for reading, copying and validating data
- Base16, base32 and base64 encoders/decoders that operate on streams
- A lightweight binary serialization framework built around interfaces
- Helpers to chain multiple output streams and to validate data while copying
- `PartialStream` for exposing a subsection of another stream
