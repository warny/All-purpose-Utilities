# omy.Utils.Parser.Source

`omy.Utils.Parser.Source` contains shared source-location contracts for the `Utils.Parser` ecosystem.

The package is intentionally small. It does not contain runtime parsing logic, parser logic, diagnostic emission logic, generator logic, or tooling behavior. It only provides reusable source-coordinate contracts that parser runtime components, diagnostics, generators, and tooling can reference without introducing artificial package coupling.

## Runtime source coordinates

Runtime source coordinates describe technical positions in the source text consumed by lexer, parser, and runtime components.

- `SourceLocation` represents a point in source text.
- `SourceSpan` represents a range in source text.

These contracts are intended for tokens, parse nodes, lexer/parser runtime observation, runtime analyses, and source-text slicing.

For runtime coordinates:

- `Position` is a zero-based absolute offset in the source string.
- `Length` is a length in characters/runtime text units according to the source representation used by the runtime.
- `Line` and `Column` are 1-based display coordinates associated with the same point or span start.
- `FilePath` is optional because source text can be anonymous, generated, or held only in memory.

## Human-readable source coordinates

Human-readable source coordinates describe locations intended for diagnostics, display, import/export, and tooling surfaces.

- `SourceCodeLocation` represents a required file path plus 1-based line and column.
- `SourceCodeRange` extends `SourceCodeLocation` with a display/diagnostic length.

These contracts are useful when a readable source location is known but no canonical runtime absolute position is available.

For human-readable coordinates:

- no absolute source offset is stored;
- `FilePath` is required;
- `Line` and `Column` are 1-based;
- `Length`, when present, is intended for display or diagnostic ranges rather than runtime slicing authority.

## Why both models exist

An absolute position in a source string and a file/line/column location are not interchangeable without the source text and the counting conventions used to interpret it.

Line endings, tab expansion, Unicode representation, and column-counting rules can all make conversion context-dependent. A runtime offset can be the correct contract for token and parse-node operations while still being insufficient to reconstruct a human-readable location without additional source context. Conversely, a human-readable file/line/column location can be enough for diagnostics or tooling while not identifying a canonical absolute offset in a particular runtime source buffer.

For that reason, `SourceSpan` does not replace `SourceCodeRange`, and `SourceCodeLocation` does not replace `SourceLocation`. They model different contracts and should only be converted by code that explicitly owns the relevant source text and counting conventions.

## Intended consumers

This package is designed to be shared by diagnostics, parser runtime components, source generators, and future tooling surfaces that need source-location data without depending on `omy.Utils.Parser.Diagnostics`.
