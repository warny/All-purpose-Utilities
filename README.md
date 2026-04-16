# All-purpose Utilities

`All-purpose-Utilities` is a family of focused .NET libraries published on NuGet under the `omy.Utils` prefix.

It is designed for consumers who want small, task-oriented packages (networking, I/O, data mapping, parser tooling, source generators, and more) without adopting a single monolithic framework.

## NuGet packages

### Core and runtime libraries

- `omy.Utils` — shared foundation with arrays, collections, expressions, streams, security, and utility helpers.
- `omy.Utils.Collections` — skip list and collection-specific helpers.
- `omy.Utils.Data` — attribute-based mapping from `IDataRecord` / `IDataReader` to objects.
- `omy.Utils.DependencyInjection` — attribute-driven registration helpers for `Microsoft.Extensions.DependencyInjection`.
- `omy.Utils.Fonts` — TrueType/PostScript parsing and font utilities.
- `omy.Utils.Geography` — coordinates, projections, and map tile helpers.
- `omy.Utils.IO` — stream utilities, binary serialization, base16/base32/base64.
- `omy.Utils.Imaging` — bitmap accessors, color conversion, and drawing primitives.
- `omy.Utils.Mathematics` — symbolic helpers, FFT, SI units, and algebra primitives.
- `omy.Utils.Net` — DNS, ICMP, Wake-on-LAN, ARP, and URI tooling.
- `omy.Utils.NumberToString` — number-to-string conversion package extracted from the base library.
- `omy.Utils.OData` — OData client and metadata helpers.
- `omy.Utils.Parser` — runtime ANTLR4 grammar loading, tokenization, and parsing utilities.
- `omy.Utils.Reflection` — reflection helpers and dynamic access wrappers.
- `omy.Utils.VirtualMachine` — attribute-driven byte-code interpreter primitives.
- `omy.Utils.XML` — XML processing helpers (`XmlDataProcessor`, mapping attributes).

### Source generator packages

- `omy.Utils.DependencyInjection.Generators` — generates DI registrations.
- `omy.Utils.IO.Serialization.Generators` — generates serialization code for stream contracts.
- `omy.Utils.OData.Generators` — generates OData models/helpers from EDMX metadata.
- `omy.Utils.Parser.Generators` — grammar-related generation helpers.

## Quick install

Install only the package you need:

```bash
dotnet add package omy.Utils
# or
dotnet add package omy.Utils.Net
```

## Usage example

```csharp
using Utils.Net;

UriBuilderEx builder = new UriBuilderEx("https://example.com");
builder.QueryString["key"].Add("value");

Console.WriteLine(builder.ToString());
```

## Documentation

- [Getting started](docs/getting-started.md)
- [Release process](docs/releasing.md)
- [GitHub About proposal](docs/github-about.md)
- [Changelog](CHANGELOG.md)
- [Base package README (`omy.Utils`)](Utils/README.md)

## Consumer vs contributor requirements

### Consuming packages

Consumers only need a .NET runtime/toolchain compatible with the package target framework (for example `net8.0`, `net9.0`, or `netstandard2.0` depending on package).

### Building this repository

Contributors building everything from source should use the SDK required by the solution (`Utils.sln` currently targets .NET 9 for development/testing projects).

```bash
dotnet build Utils.sln
dotnet test Utils.sln
```

## License

Apache 2.0 (`LICENSE-apache-2.0.txt`).
