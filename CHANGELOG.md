# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Changed — `omy.Utils.Mathematics` (BREAKING)
- **`Matrix<T>.DiagonalizeLU()` now returns a 3-tuple `(L, U, P)` instead of `(L, U)`.** The previous
  two-factor result was mathematically unable to reconstruct the original matrix whenever partial
  pivoting swapped rows (the documented `A = L·U` identity only held for inputs that happened not to
  need a pivot swap; the fix also corrected `L` itself, which previously held the product of the
  elimination operators rather than the actual multiplier matrix). The new contract is `P * A = L * U`.
  Existing call sites using a two-element deconstruction (`var (l, u) = matrix.DiagonalizeLU();`) will
  fail to compile and must be updated to `var (l, u, p) = matrix.DiagonalizeLU();`. No package version
  bump yet — tracked for the coordinated `omy.Utils.*` 2.0.0 batch release rather than an individual
  bump.
- `Solve`/`Invert` singularity checks now use a scale-aware relative pivot tolerance (derived from the
  scalar type's own machine epsilon) instead of comparing to exact zero; both gained an optional
  `relativeSingularityTolerance` parameter to override the default. A previously-accepted matrix whose
  elimination pivot is merely close to zero (relative to the matrix's magnitude) now throws
  `InvalidOperationException` instead of returning a huge/NaN result.

### Changed — `omy.Utils.Reflection` (BREAKING, v1.2.1 → 2.0.0)
- **`LibraryMapper.Emit<TInterface>` now runs in an isolated, sandboxed worker process by default**,
  instead of compiling and loading the generated mapping class directly in the calling process. Same
  method signature, different runtime behavior and requirements:
  - Host applications must call `LibraryMapper.RunWorkerIfRequested(args)` as the very first statement
    of their entry point, before any other startup logic, or `Emit<TInterface>` fails to start the worker.
  - Only interfaces whose members use JSON-representable types (primitives, `string`, enums, and
    arrays/structs made of these) can be mapped this way; `Emit<TInterface>` now throws
    `NotSupportedException` immediately for interfaces using `IntPtr`/pointers/handles or arbitrary
    reference types, which previously worked (in-process, without isolation).
  - Every call now round-trips over a named pipe (JSON serialization both ways) instead of a direct
    in-process delegate call, with a real performance cost per call.
  - The original unsandboxed behavior is preserved as `LibraryMapper.EmitInProcess<TInterface>` (and
    `EmitDllMappableClass.Emit`), gated behind `[Experimental("UTILSREFL001")]` — callers must
    explicitly acknowledge the code-injection risk documented on that method to keep using it.
- `ProcessContainerPermissions.Default` is now a fresh, immutable instance per access instead of a
  shared mutable singleton; all properties are `init`-only.

### Added — `omy.Utils.Reflection`
- `LibraryMapper.Emit<TInterface>` gains optional `loadTimeout`/`callTimeout` parameters; the isolated
  worker's Load/Call/Shutdown requests are now bounded (30s/30s/5s by default) instead of blocking
  indefinitely on a hung native call.
- `EmitWorkerPool`: opt-in sharing of a single isolated worker process across several mapped interfaces,
  trading some isolation between them for a lower per-interface process-spawn cost.
  `LibraryMapper.Emit<TInterface>` itself is unchanged (still one worker per interface by default).
- `ProcessIsolation` hardening: sandboxed child environment allowlisting (`SandboxedProcessEnvironment`,
  now applied to the Windows AppContainer worker as well as Linux/macOS), `AppContainerSandbox` Job
  Object failure handling, `PATHEXT`-aware `CommandAvailability.Exists`.
- `EmitDllMappableClass`/`LibraryMapper.Emit` reject generic interfaces and generic methods upfront with
  a clear error, instead of failing later with a cryptic Roslyn diagnostic.
- `Platform.IsMacOS` alias for `Platform.IsMacOsX`.

### Added — `omy.Utils.NumberToString`
- **Ordinaux EL** : Grec — word rules pour masculin (défaut) + `OrdinalVariants` gender=θηλυκό et gender=ουδέτερο pour 1-12, dizaines et centaines.
- **Ordinaux FI** : Finnois — word rules exhaustives pour toutes les formes (unités 1-9, exceptions 11-19, dizaines 20-90, centaines, туhat).
- **Ordinaux HI** : Hindi — suffixe `वाँ` pour 5-9 et 11+ ; exceptions explicites pour 1-4 et word rule pour 6 (छठा).
- **Ordinaux PL** : Polonais — word rules pour toutes les formes nominatif masculin singulier (unités, 11-19, dizaines, centaines, tysiąc). Pour les ordinaux composés, seul le dernier mot est transformé (limitation XML).
- **Ordinaux AR** : Arabe — exceptions masculines indéfinies pour 1-10.
- **Ordinaux WO** : Wolof — suffixe `ël` + exception 1 (`bu njëkk`).
- **FR ordinal féminin** : `ConvertOrdinal(1, "gender=feminin")` retourne "première" pour les cultures `FR-fr-ca` et `FR-be-ch` via `<OrdinalVariants>`.
- **Ordinaux RU** : nouvelles règles d'ordinaux en russe — suffixe "ый" avec `removeTrailing="ь"`, word rules pour toutes les formes irrégulières (unités, dizaines, centaines, тысяча).
- **`ConvertYear(int year)`** : nouvelle méthode pour lire une année en mots ; pour les langues configurées avec `<YearFormat>` et `<SplitRange>`, l'année est découpée en deux moitiés (ex. EN : 1984 → "nineteen eighty-four", 1900 → "nineteen hundred", 1905 → "nineteen oh five").
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
