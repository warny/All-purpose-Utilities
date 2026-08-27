# All-purpose Utilities

`All-purpose-Utilities` is a family of focused .NET libraries published on NuGet under the `omy.Utils` prefix.

It is designed for consumers who want small, task-oriented packages (networking, I/O, data mapping, parser tooling, source generators, and more) without adopting a single monolithic framework.

## Projects and project READMEs

### Core libraries (NuGet)

| Project | Package (if published) | Purpose | README |
|---|---|---|---|
| `Utils` | `omy.Utils` | Shared foundational helpers. | [Utils/README.md](Utils/README.md) |
| `Utils.Collections` | `omy.Utils.Collections` | Collection and indexing primitives. Manifested in the product train, but at an independent **provisional `0.0.x` version** rather than the shared product-train version — see [provisional versioning](docs/releasing/ProvisionalVersioning.md). | [Utils.Collections/README.md](Utils.Collections/README.md) |
| `Utils.Data` | `omy.Utils.Data` | Data-record to object mapping and SQL helpers. | [Utils.Data/README.md](Utils.Data/README.md) |
| `Utils.DependencyInjection` | `omy.Utils.DependencyInjection` | DI registration helpers. | [Utils.DependencyInjection/README.md](Utils.DependencyInjection/README.md) |
| `Utils.Expressions.CSyntax` | `omy.Utils.Expressions.CSyntax` | C-like expression compiler. | [Utils.Expressions.CSyntax/README.md](Utils.Expressions.CSyntax/README.md) |
| `Utils.Expressions.VBSyntax` | `omy.Utils.Expressions.VBSyntax` | VB-like expression compiler. | [Utils.Expressions.VBSyntax/README.md](Utils.Expressions.VBSyntax/README.md) |
| `Utils.Fonts` | `omy.Utils.Fonts` | Font parsing and typography helpers. | [Utils.Fonts/README.md](Utils.Fonts/README.md) |
| `Utils.Geography` | `omy.Utils.Geography` | Coordinate/projection utilities. | [Utils.Geography/README.md](Utils.Geography/README.md) |
| `Utils.IO` | `omy.Utils.IO` | Stream, serialization, and encoding helpers. | [Utils.IO/README.md](Utils.IO/README.md) |
| `Utils.Imaging` | `omy.Utils.Imaging` | Imaging and drawing primitives. **Windows only** (`System.Drawing.Common`). | [Utils.Imaging/README.md](Utils.Imaging/README.md) |
| `Utils.Mathematics` | `omy.Utils.Mathematics` | Math, algebra, and symbolic tooling. | [Utils.Mathematics/README.md](Utils.Mathematics/README.md) |
| `Utils.Net` | `omy.Utils.Net` | Networking protocols and helpers. | [Utils.Net/README.md](Utils.Net/README.md) |
| `Utils.NumberToString` | `omy.Utils.NumberToString` | Number-to-string conversion helpers. | [Utils.NumberToString/README.md](Utils.NumberToString/README.md) |
| `Utils.OData` | `omy.Utils.OData` | OData helpers/runtime pieces. | [Utils.OData/README.md](Utils.OData/README.md) |
| `Utils.Parser` | `omy.Utils.Parser` | Parser runtime and tokenizer with **partial ANTLR4 `.g4` support** (see support status). | [Utils.Parser/README.md](Utils.Parser/README.md) |
| `Utils.Parser.Diagnostics` | `omy.Utils.Parser.Diagnostics` | Shared parser diagnostics contracts for runtime and generators. | [Utils.Parser.Diagnostics/README.md](Utils.Parser.Diagnostics/README.md) |
| `Utils.Parser.Source` | `omy.Utils.Parser.Source` | Shared parser source-location contracts without diagnostics coupling. | [Utils.Parser.Source/README.md](Utils.Parser.Source/README.md) |
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

## Configurable binary wire formats

`omy.Utils.IO` supports exact-type wire codecs and per-member overrides. For example, the following keeps the default .NET Binary representation everywhere except for one Unix-millisecond timestamp:

```csharp
using System;
using System.IO;
using Utils.IO.Serialization;

public sealed class AuditEntry
{
    [Field(1)]
    public DateTime Created { get; set; }

    [Field(2)]
    [WireCodec(typeof(UnixMillisecondsDateTimeCodec))]
    public DateTime ExternalTimestamp { get; set; }
}

using MemoryStream stream = new();
Writer writer = new(stream);
writer.Write(new AuditEntry
{
    Created = DateTime.UtcNow,
    ExternalTimestamp = DateTime.UnixEpoch
});
```

See the [Utils.IO wire-codec guide](Utils.IO/README.md#wire-codecs-and-framing) for global registration, custom codecs, directional readers/writers, and framing/buffering examples.

## Parser look-ahead note

The look-ahead probe layer can now conservatively classify structurally epsilon-capable alternatives such as optional or zero-or-more quantifiers. This classification remains informational and does not bypass normal parsing.

Still intentionally out of scope: adaptive prediction, recursive FIRST-set analysis, shared look-ahead graphs, continuation queues, and parallel parsing.

## Recent expression updates

- `ExpressionCompilerContext` is now the shared runtime context for expression compilers.
- `Utils.Expressions.CSyntax` supports forward-referenced function declarations with shared context usage.
- Generic symbolic math APIs are available through `ExpressionDerivation<T>`, `ExpressionIntegration<T>`, and `MathExpressionExtensions` (`Derivate<T>` / `Integrate<T>`).

## Documentation

- [Getting started](docs/getting-started.md)
- [Release process](docs/releasing.md)
- [GitHub About proposal](docs/github-about.md)
- [Changelog](CHANGELOG.md)
- [Base package README (`omy.Utils`)](Utils/README.md)
- [`.g4` support status (`Utils.Parser`)](Utils.Parser/README.md#g4-file-support-status)

## `.g4` file support status

ANTLR4 support in this repository is **still evolving** and is **not yet 100% ANTLR4-compatible**.

- ✅ Recommended usage: `.g4` grammars already validated by the `Utils.Parser` test suite.
- ⚠️ Some advanced ANTLR4 constructs may be partially supported or unsupported, depending on version and scenario.
- ✅ For stable production usage, prefer:
  - patterns already covered by automated tests,
  - or build-time generation/validation through `omy.Utils.Parser.Generators`.
- ℹ️ Known missing/limited capabilities are documented in `Utils.Parser/README.md` under **Known missing or limited features**.
- ℹ️ A high-level support matrix is also available in `Utils.Parser/README.md` under **Current support matrix (high-level)**.

See the `Utils.Parser` README for details about current support scope.

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

## Synchronized 2.0 release candidate

Most publishable `omy.Utils` libraries and source generators are prepared as one `2.0.0-rc.1` product train. A small number of components are explicitly `provisional` and ship at their own independent version instead (`omy.Utils.Collections` at `0.0.1` today; see [provisional versioning](docs/releasing/ProvisionalVersioning.md), which also covers the `Utils.Parser.VisualStudio` Visual Studio extension's own `0.0.x` series). Contributors can review the [product-train inventory and release boundary](docs/releasing/ProductTrain.md); consumers upgrading from published packages should start with the [2.0 migration guide](docs/releasing/MigrationTo2.0.md).
