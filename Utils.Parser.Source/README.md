# omy.Utils.Parser.Source

`omy.Utils.Parser.Source` contains shared source-location contracts for the `Utils.Parser` ecosystem.

The package is intentionally small and does not carry runtime parsing logic, diagnostic emission logic, generators, or tooling behavior. It only provides reusable contracts that other parser packages can reference without depending on diagnostics.

## Contracts

- `SourceCodeLocation` represents a required source file path with a 1-based line and 1-based column for human-readable display.
- `SourceCodeRange` extends `SourceCodeLocation` with a length component for human-readable source ranges.
- `SourceLocation` represents a point in source text with an absolute offset plus optional file/display coordinates.
- `SourceSpan` represents a runtime text span with an absolute offset and length, plus optional file/display coordinates for diagnostics formatting.

## Intended consumers

This package is designed to be shared by diagnostics, parser runtime components, source generators, and future tooling surfaces that need source-location data without introducing an artificial dependency on `omy.Utils.Parser.Diagnostics`.
