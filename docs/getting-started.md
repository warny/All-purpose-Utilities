# Getting started with omy.Utils

The **omy.Utils** family is a set of focused utility packages you can consume directly from NuGet. Use this guide to pick the right package, install it, and understand platform support.

## Which package should I choose?

- **omy.Utils** – foundational helpers (arrays, collections, expressions, strings, streams, security) that other packages build upon.
- **omy.Utils.Net** – networking helpers, DNS/ICMP tools, and Wake-on-LAN utilities.
- **omy.Utils.IO** – stream handling, base16/base32/base64 converters, and binary serialization.
- **omy.Utils.Data** – map `IDataRecord`/`IDataReader` to strongly typed objects with attributes.
- **omy.Utils.Imaging** – bitmap accessors, vector drawing, and color helpers.
- **omy.Utils.Fonts** – TrueType/PostScript parsing and glyph metrics utilities.
- **omy.Utils.Geography** – coordinate models, projections, and tile helpers.
- **omy.Utils.Mathematics** – FFT, derivation/integration, SI conversions, and linear algebra types.
- **omy.Utils.Reflection** – reflection extensions (`PropertyOrFieldInfo`, delegate invocation helpers).
- **omy.Utils.Xml** – attribute-driven XML processing with `XmlDataProcessor` helpers.
- **omy.Utils.VirtualMachine** – minimal VM framework with attribute-defined instructions.
- **omy.Utils.OData** – OData client helpers and source generators.
- **omy.Utils.DependencyInjection** – DI helpers plus source generators.

## Installation

Install the package that matches your scenario via the .NET CLI:

```bash
dotnet add package omy.Utils
# or
dotnet add package omy.Utils.Net
```

Packages are published on NuGet under the `omy.` prefix.

## Supported target frameworks

The libraries target stable target frameworks for consumers:

- **net8.0** for most foundational packages
- **net9.0** for networking and selected libraries

Building the repository may require the latest .NET SDK, but consuming the NuGet packages only requires the listed TFMs.

## Versioning and compatibility

Releases follow semantic versioning across the package family. Avoid breaking changes when updating between patch releases. Check the [`CHANGELOG.md`](../CHANGELOG.md) for notable documentation and metadata updates.

## Feedback and issues

Report bugs or request enhancements via GitHub issues on the [project repository](https://github.com/warny/All-purpose-Utilities). Contributions are welcome through pull requests.
