# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added — `omy.Utils.NumberToString`
- **Ordinal variants**: `ConvertOrdinal(int, params string[])` — ordinals can now be inflected for gender and other dimensions using the same `"dimension=value"` syntax as `Convert`. Languages with variant ordinals: ES, IT, PT, CA, GL, HE.
- **Prefix ordinals**: `<Ordinals prefix="…">` in the XML configuration produces ordinals by prepending a fixed string to the cardinal. Used by ZH (第), JA (第), KO (제), EE (etsõ).
- **`IOrdinalLanguageSpecifics`**: new interface that can be implemented alongside `INumberToStringLanguageSpecifics` to override ordinal formation with custom logic (highest priority, falls back to XML pipeline when `TryConvertOrdinal` returns `false`).
- **`SupportsOrdinals` property** on `INumberToStringConverter`: returns `true` when the converter has any ordinal configuration (exceptions, word rules, suffix, or prefix).
- **Ordinal support** extended to: DE (German), ES (Spanish), IT (Italian), PT (Portuguese), CA (Catalan), GL (Galician), HE (Hebrew), EE (Ewe), ZH (Chinese), JA (Japanese), KO (Korean).
- **Swiss/Liechtenstein German config** (`de-CH`, `de-LI`): separate configuration without the `"ein tausend" → "tausend"` contraction used in standard German. Ordinals follow the same rules as DE with an explicit exception for 1000 → "tausendste".

### Changed — `omy.Utils.NumberToString`
- Updated `Utils.NumberToString/README.md`: corrected ordinal support matrix, added ordinal examples for DE, ES, IT, PT, CA, GL, HE, EE/ZH/JA/KO, documented `IOrdinalLanguageSpecifics`, `SupportsOrdinals`, prefix ordinals, and `<OrdinalVariants>` XML syntax.

### Changed
- Added generated C# opt-in allocation of declared parser rule locals as missing-only untyped `null` invocation-frame entries before `@init`, while preserving conservative `Parse(...)` behavior.
- Clarified parser source-coordinate documentation across `omy.Utils.Parser.Source` and the parser roadmap, including the split between runtime offsets and human-readable diagnostic/tooling locations.
- Updated the `omy.Utils` NuGet description to a concise consumer-facing summary aligned with the package README and discoverability goals.

### Added
- Added `omy.Utils.Parser.Source` as a shared source-location contracts package for `SourceCodeLocation` and `SourceCodeRange` without requiring a diagnostics dependency.
- Clarified parser runtime documentation for policy-controlled semantic predicates/actions, conservative defaults, memoization assumptions, and related diagnostics semantics.
- Corrected package casing reference from `omy.Utils.Xml` to `omy.Utils.XML` in the base package README to match the published NuGet package identifier.
- Refined consumer documentation: updated root README and getting-started guide with complete package inventory, install-first flow, and explicit consumer vs contributor requirements.
- Clarified getting-started and release documentation with csproj-derived TFM guidance, source-generator install example, and explicit CI workflow mapping.
- Added `omy.Utils.Parser` (v0.1.0): self-describing universal parser framework. Tokenizes and parses any ANTLR4 grammar at runtime without code generation. Includes `LexerEngine`, `ParserEngine`, `Antlr4GrammarConverter`, and `RuleResolver`.
- Added XML documentation (English) to all `Utils.Parser` public and private members.
- Added `PackageTags`, `PackageReadmeFile`, `RepositoryUrl`, `RepositoryType`, and `PackageProjectUrl` to `omy.Utils.Parser.csproj`.
- Added `RepositoryUrl`, `RepositoryType`, and `PackageProjectUrl` to all other packable project files.
- Added consumer-focused documentation, getting started guide, GitHub About proposal, and release process notes.
- Marked internal projects (`Utils.Expressions.CSyntax`, `Utils.Parser.VisualStudio.Worker`) as non-packable to keep NuGet metadata scope limited to published packages.
- Documented package family overview and usage in the root README and base package README.
