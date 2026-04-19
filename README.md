# All-purpose Utilities

`All-purpose-Utilities` is a family of focused .NET libraries published on NuGet under the `omy.Utils` prefix.

It is designed for consumers who want small, task-oriented packages (networking, I/O, data mapping, parser tooling, source generators, and more) without adopting a single monolithic framework.

## Projects and project READMEs

### Core libraries (NuGet)

| Project | Package (if published) | Purpose | README |
|---|---|---|---|
| `Utils` | `omy.Utils` | Shared foundational helpers. | [Utils/README.md](Utils/README.md) |
| `Utils.Collections` | `omy.Utils.Collections` | Collection and indexing primitives. | [Utils.Collections/README.md](Utils.Collections/README.md) |
| `Utils.Data` | `omy.Utils.Data` | Data-record to object mapping and SQL helpers. | [Utils.Data/README.md](Utils.Data/README.md) |
| `Utils.DependencyInjection` | `omy.Utils.DependencyInjection` | DI registration helpers. | [Utils.DependencyInjection/README.md](Utils.DependencyInjection/README.md) |
| `Utils.Expressions.CSyntax` | `omy.Utils.Expressions.CSyntax` | C-like expression compiler. | [Utils.Expressions.CSyntax/readme.md](Utils.Expressions.CSyntax/readme.md) |
| `Utils.Fonts` | `omy.Utils.Fonts` | Font parsing and typography helpers. | [Utils.Fonts/README.md](Utils.Fonts/README.md) |
| `Utils.Geography` | `omy.Utils.Geography` | Coordinate/projection utilities. | [Utils.Geography/README.md](Utils.Geography/README.md) |
| `Utils.IO` | `omy.Utils.IO` | Stream, serialization, and encoding helpers. | [Utils.IO/README.md](Utils.IO/README.md) |
| `Utils.Imaging` | `omy.Utils.Imaging` | Imaging and drawing primitives. | [Utils.Imaging/README.md](Utils.Imaging/README.md) |
| `Utils.Mathematics` | `omy.Utils.Mathematics` | Math, algebra, and symbolic tooling. | [Utils.Mathematics/README.md](Utils.Mathematics/README.md) |
| `Utils.Net` | `omy.Utils.Net` | Networking protocols and helpers. | [Utils.Net/README.md](Utils.Net/README.md) |
| `Utils.NumberToString` | `omy.Utils.NumberToString` | Number-to-string conversion helpers. | [Utils.NumberToString/README.md](Utils.NumberToString/README.md) |
| `Utils.OData` | `omy.Utils.OData` | OData helpers/runtime pieces. | [Utils.OData/README.md](Utils.OData/README.md) |
| `Utils.Parser` | `omy.Utils.Parser` | Parser runtime helpers and tokenization. | [Utils.Parser/README.md](Utils.Parser/README.md) |
| `Utils.Reflection` | `omy.Utils.Reflection` | Reflection/process-isolation helpers. | [Utils.Reflection/README.md](Utils.Reflection/README.md) |
| `Utils.VirtualMachine` | `omy.Utils.VirtualMachine` | VM and opcode helper abstractions. | [Utils.VirtualMachine/README.md](Utils.VirtualMachine/README.md) |
| `Utils.Xml` | `omy.Utils.XML` | XML-related helpers. | [Utils.Xml/README.md](Utils.Xml/README.md) |

### Source generator packages

| Project | Package | Purpose | README |
|---|---|---|---|
| `Utils.DependencyInjection.Generators` | `omy.Utils.DependencyInjection.Generators` | Generates DI registrations. | [Utils.DependencyInjection.Generators/README.md](Utils.DependencyInjection.Generators/README.md) |
| `Utils.IO.Serialization.Generators` | `omy.Utils.IO.Serialization.Generators` | Generates stream serialization code. | [Utils.IO.Serialization.Generators/README.md](Utils.IO.Serialization.Generators/README.md) |
| `Utils.OData.Generators` | `omy.Utils.OData.Generators` | Generates OData helpers/models. | [Utils.OData.Generators/README.md](Utils.OData.Generators/README.md) |
| `Utils.Parser.Generators` | `omy.Utils.Parser.Generators` | Grammar generation helpers. | [Utils.Parser.Generators/README.md](Utils.Parser.Generators/README.md) |

### Tooling, samples, and tests

| Project | Purpose | README |
|---|---|---|
| `Utils.Parser.VisualStudio` | Visual Studio integration layer for parser tooling. | [Utils.Parser.VisualStudio/README.md](Utils.Parser.VisualStudio/README.md) |
| `Utils.Parser.VisualStudio.Worker` | Out-of-process worker used by VS integration. | [Utils.Parser.VisualStudio.Worker/README.md](Utils.Parser.VisualStudio.Worker/README.md) |
| `DrawTest` | Windows Forms drawing sample app. | [DrawTest/README.md](DrawTest/README.md) |
| `Fractals` | Windows Forms fractal sample app. | [Fractals/README.md](Fractals/README.md) |
| `UtilsTest` | MSTest/SpecFlow integration and unit tests. | [UtilsTest/README.md](UtilsTest/README.md) |

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
