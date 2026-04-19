# Getting started with `omy.Utils`

Use this guide to select the right package, install it quickly, and confirm supported target frameworks.

> Target framework values in this document are derived from the corresponding project (`*.csproj`) files in this repository.

## 1) Choose a package

Pick the smallest package that matches your use case.

| Package | Purpose | TFM |
|---|---|---|
| `omy.Utils` | Shared foundation helpers used across the ecosystem. | `net8.0` |
| `omy.Utils.Collections` | Collection structures and helpers (including skip list). | `net8.0` |
| `omy.Utils.Data` | Map `IDataRecord` / `IDataReader` to typed objects. | `net8.0` |
| `omy.Utils.DependencyInjection` | Attribute-driven DI registration helpers. | `net9.0` |
| `omy.Utils.Fonts` | Font parsing and glyph/encoding helpers. | `net8.0` |
| `omy.Utils.Geography` | Coordinates, projections, and map/tile helpers. | `net8.0` |
| `omy.Utils.IO` | Streams, conversion helpers, and serialization primitives. | `net8.0` |
| `omy.Utils.Imaging` | Imaging and drawing utilities. | `net8.0` |
| `omy.Utils.Mathematics` | Math helpers, FFT, and algebra-related types. | `net8.0` |
| `omy.Utils.Net` | Networking helpers (DNS, ICMP, WOL, URI tooling). | `net9.0` |
| `omy.Utils.NumberToString` | Number-to-string conversion helpers. | `net8.0` |
| `omy.Utils.OData` | OData client and metadata utilities. | `net9.0` |
| `omy.Utils.Parser` | Runtime parsing infrastructure for ANTLR4 grammars. | `net8.0` |
| `omy.Utils.Reflection` | Reflection wrappers and invocation helpers. | `net8.0` |
| `omy.Utils.VirtualMachine` | Attribute-based byte-code interpreter support. | `net8.0` |
| `omy.Utils.XML` | XML mapping and processing helpers. | `net8.0` |
| `omy.Utils.DependencyInjection.Generators` | Source generators for DI. | `netstandard2.0` |
| `omy.Utils.IO.Serialization.Generators` | Source generators for stream serializers. | `netstandard2.0` |
| `omy.Utils.OData.Generators` | Source generators for OData metadata. | `netstandard2.0` |
| `omy.Utils.Parser.Generators` | Parser/grammar source generator utilities. | `netstandard2.0` |

## 2) Install

```bash
dotnet add package omy.Utils
# or
dotnet add package omy.Utils.IO
```

For source-generator packages:

```bash
dotnet add package omy.Utils.DependencyInjection.Generators
```

## 3) Minimal usage snippet

```csharp
using Utils.Arrays;

int[] values = [0, 1, 2, 0];
int[] trimmed = values.Trim(0);
```

## Versioning policy

The repository uses semantic versioning per package. Patch and minor updates should remain non-breaking for existing public APIs.

Track release notes in [`CHANGELOG.md`](../CHANGELOG.md).

## Feedback and issues

Open issues and feature requests in the GitHub repository:

- https://github.com/warny/All-purpose-Utilities/issues

## Note for contributors

Consuming packages and building the repository are different concerns:

- **Consumers**: use the package TFM listed above.
- **Contributors**: build/test via `Utils.sln`, which includes .NET 9 development/test projects.
