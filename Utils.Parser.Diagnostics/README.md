# omy.Utils.Parser.Diagnostics

`omy.Utils.Parser.Diagnostics` provides the shared diagnostics model used by parser runtime and generator components in the `omy.Utils` ecosystem.

## Purpose

Use this package when you need a common set of diagnostic primitives across parser-related tools, including:

- diagnostic descriptors,
- diagnostic severities,
- diagnostic aggregation utilities.

## Typical usage

The package is typically consumed transitively by parser packages. Reference it directly when building custom tooling that needs to exchange diagnostics with `omy.Utils.Parser` or `omy.Utils.Parser.Generators`.

```bash
dotnet add package omy.Utils.Parser.Diagnostics
```

## Related packages

- `omy.Utils.Parser`
- `omy.Utils.Parser.Generators`

See the repository root README for installation and package selection guidance.
