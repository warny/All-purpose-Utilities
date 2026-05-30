# omy.Utils.Parser.Source

`omy.Utils.Parser.Source` contains shared source-location contracts for the `Utils.Parser` ecosystem.

The package is intentionally small and does not carry runtime parsing logic, diagnostic emission logic, generators, or tooling behavior. It only provides reusable contracts that other parser packages can reference without depending on diagnostics.

## Contracts

- `SourceCodeLocation` represents a source file path with a 1-based line and 1-based column.
- `SourceCodeRange` extends `SourceCodeLocation` with a length component for source ranges.

## Intended consumers

This package is designed to be shared by diagnostics, parser runtime components, source generators, and future tooling surfaces that need source-location data without introducing an artificial dependency on `omy.Utils.Parser.Diagnostics`.
