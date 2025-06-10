# All-purpose Utilities

This repository contains a collection of utility libraries and sample applications targeting **.NET 8** and **.NET 9**. The solution aggregates several projects under the `Utils` family, ranging from low level helpers to Windows Forms samples.

## Requirements

The solution uses the .NET SDK version 9 (preview). Build all projects with:

```bash
 dotnet build
```

## Projects and Namespaces

### `Utils`
A general purpose library exposing many helper namespaces:
- **`Utils.Arrays`** – array comparison utilities, multi dimensional helpers and key/value comparers.
- **`Utils.Collections`** – custom collections such as indexed lists, skip lists, LRU caches and dictionary extensions.
- **`Utils.Expressions`** – expression parser, builders and simplifiers for lambda expressions.
- **`Utils.Files`** – file and path utilities.
- **`Utils.Mathematics`** (base) – mathematical extensions and expression transformers.
- **`Utils.Net`** – helpers for URIs, query strings, mail addresses and IP ranges.
- **`Utils.Objects`** – data conversion, advanced string formatting and miscellaneous object utilities.
- **`Utils.Reflection`** – extra reflection helpers like `PropertyOrFieldInfo`.
- **`Utils.Resources`** – utilities for working with embedded resources.
- **`Utils.Security`** – Google authenticator helpers.
- **`Utils.XML`** – XML processing helpers.

### `Utils.IO`
I/O related helpers including:
- base16/base32/base64 stream encoders and decoders
- binary serialization framework
- stream copying and validation utilities

### `Utils.Net` (System.Net)
Network focused utilities:
- full DNS protocol implementation and packet helpers
- ICMP utilities and basic traceroute support
- gathering system network parameters

### `Utils.Data`
Attributes and helpers to map `IDataRecord`/`IDataReader` data to typed objects.

### `Utils.Imaging`
Bitmap accessors and drawing primitives. Provides ARGB/AHSV color structures and basic vector drawing support.

### `Utils.Fonts`
Font management library able to read and interpret TrueType and PostScript fonts. Also contains utilities for encoding tables and glyph metrics.

### `Utils.Geography`
Models and tools for geographic coordinates, tile representations and various map projections.

### `Utils.Mathematics`
Advanced mathematics library featuring:
- expression derivation and integration
- fast Fourier transform support
- conversion helpers for SI units
- generic linear algebra types

### `Utils.Reflection`
Runtime reflection helpers, notably a dynamic DLL mapping system and platform detection utilities.

### `Utils.VirtualMachine`
Minimal virtual machine framework. Instructions are defined using attributes and executed through a byte‑code processor with configurable endianness.

### `Fractals`
Windows Forms sample application that renders fractals using the imaging library.

### `DrawTest`
Another Windows Forms sample demonstrating the drawing primitives available in `Utils.Imaging`.

### `UtilsTest`
Unit test suite using MSTest and SpecFlow covering the utilities and components from the other projects.

## License

This project is distributed under the Apache 2.0 license (see `LICENSE-apache-2.0.txt`).
