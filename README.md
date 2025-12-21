# All-purpose Utilities

A collection of production-ready .NET utility libraries published as the **omy.Utils** package family. Each package focuses on a specific area (networking, I/O, data, imaging, etc.) and is built to be consumed directly from NuGet without preview SDKs.

## Packages at a glance

- **omy.Utils** – foundational helpers used across the other packages (arrays, collections, expressions, strings, streams, security).
- **omy.Utils.Net** – DNS, ICMP, network discovery, Wake-on-LAN, and URI builder helpers.
- **omy.Utils.IO** – stream utilities, base16/base32/base64 converters, and binary serialization helpers.
- **omy.Utils.Data** – attributes and mappers for `IDataRecord`/`IDataReader` to typed objects.
- **omy.Utils.Imaging** – bitmap accessors, vector drawing primitives, and color helpers.
- **omy.Utils.Fonts** – TrueType/PostScript parsing, encoding tables, and glyph metrics utilities.
- **omy.Utils.Geography** – coordinate models, map projections, and tile helpers.
- **omy.Utils.Mathematics** – advanced math helpers (FFT, derivation/integration, SI conversions, linear algebra).
- **omy.Utils.Reflection** – reflection extensions such as `PropertyOrFieldInfo` and dynamic delegate invocation.
- **omy.Utils.Xml** – attribute-driven XML processing and `XmlDataProcessor` helpers.
- **omy.Utils.VirtualMachine** – minimal VM framework with attribute-defined instructions and configurable endianness.
- **omy.Utils.OData** – OData client helpers and related generators.
- **omy.Utils.DependencyInjection** – dependency injection helpers plus source generators.

> Additional project-level READMEs provide deeper details for specialized packages like serialization or generators.

## Install from NuGet

Use the package that matches your scenario. Examples:

```bash
dotnet add package omy.Utils
# or
dotnet add package omy.Utils.Net
```

> All packages target stable TFMs (primarily `net8.0` and `net9.0`). Only building the repository may require the latest SDK.

## Quick usage

```csharp
using Utils.Net;

var builder = new UriBuilderEx("http://example.com");
builder.QueryString["key"].Add("value");
Console.WriteLine(builder.ToString());
```

More examples are available inside each package README (see `Utils/README.md` for the base library).

## Documentation

- [Getting started](docs/getting-started.md)
- [GitHub about summary](docs/github-about.md)
- [Releasing guide](docs/releasing.md)
- [Changelog](CHANGELOG.md)

## Build from source

The solution targets **.NET 9** for development. To build locally:

```bash
dotnet build
```

Unit tests live in the `UtilsTest` project and can be run with `dotnet test`.

## License

This project is distributed under the Apache 2.0 license (see `LICENSE-apache-2.0.txt`).
