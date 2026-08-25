# Migrating the package family to 2.0.0-rc.1

This is a coordinated major-version release candidate. Update all direct `omy.Utils*` references together and remove mixed 1.x, 2.0.0, `0.0.1`, and preview references. Internal dependencies are exact at `2.0.0-rc.1`.

## Families

- **Core:** review the detailed [`omy.Utils` 1.2.1 audit](../api/omy.Utils-1.2.1-to-2.0.0-rc.1.md), including removed expression/number formatting APIs, enumerable overloads, nullability, and embedded DateFormula resources.
- **IO and serialization, Networking, Data, Imaging and fonts, Geography, Mathematics and collections, OData, Dependency injection, Virtual machine, and Number formatting:** recompile against the candidate and review the generated package-specific ApiCompat report. The manifest records the verified latest stable baseline: mostly 1.2.1, OData and OData.Generators 0.0.1, VirtualMachine 0.1.0, and first-candidate baselines for unpublished packages.
- **Reflection:** compare against the latest published 1.2.1 package and account for the isolated worker behavior already documented in the changelog.
- **Parser:** these packages establish their first public candidate baseline and remain governed by the parser production support contract.
- **Source generators:** reference generators as analyzers with `ReferenceOutputAssembly="false"`; do not reference their implementation assemblies directly.

ApiCompat findings are accepted only for this coordinated major candidate and remain visible in `artifacts/reports/public-api-comparison.*`. Validate application-specific behavior before production deployment.

## Reviewed API compatibility changes

The repository-wide ApiCompat run against verified latest stable packages reports the following accepted major-version incompatibility counts: Core 126; IO 6; XML 1; Net 24; Data 3; Fonts 49; Imaging 7; Geography 23; Reflection 3; Mathematics 19; OData 10; VirtualMachine 7; OData generators 3; IO serialization generators 4; and dependency-injection generators 3. DependencyInjection runtime is binary compatible in the automated comparison. Collections, NumberToString, and parser packages establish first candidate baselines.

Each accepted diagnostic is pinned by diagnostic ID and exact message in `eng/api-breaking-changes/2.0.0.json`. The human-review inventory in [Accepted API breaks](AcceptedApiBreaks.md) groups the exact removed or incompatible surface by package and links every acceptance back to its package section. New diagnostics, stale acceptances, and missing migration anchors all fail the gate. The counts include removed types/members and changed signatures or constraints; they are not behavioral guarantees or rename inference. The package-specific raw reports under `artifacts/api-compat` and structured `public-api-comparison.json` are the authoritative review inputs; consumers must recompile and exercise their own usage.

## Core read-only collections

The numeric classification tables under `Utils.Objects.Types` changed from mutable `Type[]` values
to immutable `IReadOnlyList<Type>` values. Use collection operations directly; for example,
`type.In(Types.Number)` may become `Types.Number.Contains(type)`. If an API specifically requires an
array, materialize an explicit copy with `Types.Number.ToArray()`.

`ConstantNumericAttribute.Values` changed from `double[]` to `IReadOnlyList<double>?`. Use `Count`
instead of `Length`, retain the existing null handling, and call `ToArray()` only when a mutable copy
is genuinely required.

<a id="utils-io-serialization-2"></a>
## Utils.IO serialization and stream changes

Version 2.0 intentionally removes the legacy `StreamCopier`, `StreamValidator`, `BaseDecoderStream`, `ReadArray`, and `WriteVariableLengthString` signatures listed in the versioned API-breaking-change manifest. Consumers should migrate to the bounded/configurable overloads described by the current API documentation rather than restore 1.x shims.

`StreamCopier` now validates targets when they are registered, through any constructor, `Add`, `Insert`, or the indexer setter. Null, non-writable, self, and duplicate target references are rejected; duplicate identity is reference-based (`ReferenceEquals`), not `Equals`. After disposal the target list remains inspectable but is frozen: `CanWrite` becomes `false`, `IsReadOnly` becomes `true`, and every mutating member throws `ObjectDisposedException`. Consumers relying on the previous permissive registration or on the post-dispose list being cleared or freely mutable must update accordingly.

`BaseEncoderStream` now validates `maxDataWidth` (must be -1 or positive) and `indent` (must be non-negative) at construction, throwing `ArgumentOutOfRangeException` instead of accepting invalid values or failing later. Wrapping no longer emits a trailing separator/indent after the last line, and padding/filler characters now participate in the configured line width like any other encoded character; consumers matching wrapped output against a fixed string should re-check it. Unsupported `BaseEncoderStream` operations (`Position` setter, `Read`, `Seek`, `SetLength`) now throw `NotSupportedException` instead of `InvalidOperationException`, consistent with the stream's advertised `CanRead`/`CanSeek` capabilities.

`ReaderWriterGenerator` is now an `IIncrementalGenerator`. Generated extension methods no longer use a type's simple name: their deterministic name contains the namespace, containing types, metadata arity, and a stable FNV-1a suffix. Call sites must use the newly generated method name visible in IntelliSense.

Runtime reader converters require an exact result type. A converter returning an interface or base class is not used for a concrete `Read<T>` because it cannot guarantee `T`. Writers may use a base/interface converter; the most specific applicable registration wins and equal-specificity candidates are rejected.

`PartialStream` serializes reads, writes, position changes, seeking, and length changes through one sync/async-compatible gate. Bounds are evaluated while holding that gate, async wait and I/O honor cancellation, failed writes do not advance the logical position, and each operation restores the underlying stream position.

## omy.Utils.Net protocol clients

Use `SmtpPath` and `SmtpMailOptions` for SMTP envelopes, handle `ProtocolResponseException` for negative responses, and recreate a client after `ProtocolSessionPoisonedException`. POP3 STAT sizes are `long`; NNTP `NewNewsAsync` returns message-id strings; and `NextAsync` returns null only for response 421. Prefer the new `TextReader`/`TextWriter` streaming overloads for large payloads.

## NumberToString rule precedence

Version 2.0 removes declaration order as an implicit tie-breaker. Add `priority="100"` (or another intentional signed `xs:int` value) when compatible rules have equal canonical specificity. Ordinal variants and trigger forms select the greatest specificity and then greatest priority. Cumulative variants apply the least specific and lowest-priority rules first.

Programmatic trigger forms must migrate from `(Constraints, To)` tuples to `NumberToStringConverter.TriggerReplacementForm`. `VariantRule` and `OrdinalVariantRule` constructors accept an optional final `priority` argument. Configurations inherited through `baseOn` retain parent priorities; parent and child candidates are validated together and no implicit override occurs.

### ForcedVariants (NTS-04) — additive, with one French output correction

A configured lexical constituent (a time unit, a currency unit/subunit, a fraction
denominator term) can now force grammatical variant dimensions (e.g. gender) on the
numeric fragment it governs, without the caller supplying them — see the
"ForcedVariants" section of `Utils.NumberToString/README.md` for the full precedence
and locality contract. The new public surface is purely additive: the existing
`NumberToStringConverterOptions.TimeUnits` tuple shape and `CurrencyDefinition`'s
existing properties are unchanged; `TimeUnitForcedVariants`, `FractionForcedVariants`,
`CurrencyDefinition.UnitForcedVariants`/`SubunitForcedVariants`, and the new
`ForcedVariantSet` type default to an empty set that is behaviorally identical to
pre-2.0 output. No `AcceptedApiBreaks.md` entry is required.

The one intentional **output change** is a grammatical correction to the built-in
French time-unit configuration: `hour`/`minute`/`second` (all feminine nouns) now
declare `forceVariants="gender=feminin"`. `fr.Convert(new TimeSpan(1, 0, 0))` changes
from `"un heure"` to the grammatically correct `"une heure"`, and `fr.Convert(TimeSpan.FromHours(21))`
changes from `"vingt et un heures"` to `"vingt et une heures"`, without the caller
passing `gender=feminin` explicitly. Callers that previously worked around the defect
by passing `gender=feminin` themselves are unaffected — the explicit variant still
produces the same, now-correct, result. `fr.Convert(1)` and `fr.Convert(21)` (ordinary
cardinals, no time unit involved) remain masculine by default.

`ForcedVariantSet.Create` — introduced in this same rc, not previously published —
takes `params IEnumerable<(string Dimension, string Value)>` rather than an array-typed
`params (string, string)[]`; existing tuple-literal call sites (`Create(("gender", "feminin"))`)
are source-compatible. `ForcedVariantSet` dimension aliases (a language's declared
`localName`, e.g. French `genre`) are now canonicalized to the dimension's canonical
name before use; a forced set that mixed a canonical name and its alias for the same
dimension (previously silently inert) is now rejected deterministically.

Portuguese, Galician, Catalan, and Spanish (`PT`, `GL`, `CA`, `ES`) gain built-in
`Convert(TimeSpan)`/`Convert(TimeOnly)` support (`SupportsTimeConversion` becomes
`true`). PT/GL/CA use the same `forceVariants="gender=..."` pattern on their feminine
`hour` unit; `minute`/`second` remain masculine by default. Spanish additionally
declares a new `form` variant dimension (`standalone`/`attributive`) so its masculine
attributive numeral apocope ("uno"→"un", "veintiuno"→"veintiún") applies correctly to
both count 1 and compound counts (21, 31, …) — see the "Lexical form selection /
ForcedVariants (NTS-05)" note below. These are new capabilities, not behavior changes
to existing output.

### Lexical form selection (NTS-05) — additive

New public extension point: `ILexicalFormSelector`, `LexicalFormContext`,
`LexicalFormSet`, `DefaultLexicalFormSelector`, `LexicalFormSelectorConfiguration`,
and `NumberToStringConverter.RegisterLexicalFormSelector`/
`NumberToStringConverterOptions.TimeUnitForms`/`TimeUnitFormSelectors` — lets a
configured time unit choose among more than two named word forms (e.g. a future
Russian "час"/"часа"/"часов") via a selector resolved at most once per distinct
selector type, during configuration loading, never during `Convert(...)`. A unit may
optionally pass selector-specific settings via a structured
`<LexicalFormSelector type="..."><Configuration>...</Configuration></LexicalFormSelector>`
XML element (the core library never interprets `<Configuration>`'s content); units
needing no configuration keep using the concise `formSelector="..."` attribute.
`NumberToStringConverter.TimeUnitForms`/`TimeUnitFormSelectors` report the *effective*
state for every configured time unit (synthesized singular/plural and
`DefaultLexicalFormSelector` for units with no override). Every unit that configures no
selector uses the built-in default (today's exact singular/plural behavior), so no
existing configuration or output changes. See the "Lexical form selection" section of
`Utils.NumberToString/README.md` and `Utils.NumberToString/DONE-2026-08-25(1).md`/
`DONE-2026-08-25(2).md`. Spanish does not use this mechanism — its time-unit fix is a
`ForcedVariants` addition (see above), not a lexical-form-selector one.

<a id="omy-utils-fonts-2"></a>
## Utils.Fonts hostile-font parsing hardening

`TrueTypeFont.ParseFont`/`ParseFontAsync` now accept an optional `TrueTypeFontParsingOptions`
governing a `FontValidationMode` (`Strict`, the default, or `Permissive`), explicit resource limits
(font/table size, table count, `cmap` subtable count, composite-glyph depth/component/point
budgets), and stream-ownership (`LeaveOpen`). Strict mode rejects any structural anomaly by throwing
`FontParseException`; permissive mode records `FontDiagnostic`s on `TrueTypeFont.Diagnostics` and
continues whenever doing so remains memory-safe -- resource-limit violations always throw in both
modes. Fonts that previously loaded silently despite duplicate table tags, checksum mismatches,
malformed `cmap` subtables, or out-of-range composite glyph references now fail fast by default;
pass `FontValidationMode.Permissive` to restore a best-effort parse. `TrueTypeFontParsingOptions.MaximumFontBytes`
defaults to 64 MiB and cannot be configured above `uint.MaxValue` (4 GiB) -- SFNT table
offsets/lengths are themselves unsigned 32-bit fields, so this library never supports a larger font
regardless of this setting; a value above that ceiling throws `ArgumentOutOfRangeException`
immediately when constructing the options.

Several `short`-typed members were widened to their correct unsigned/wider wire type and are 2.0
breaks requiring recompilation: see the "Second audit pass" entry under
[`omy.Utils.Fonts`](AcceptedApiBreaks.md#omy-utils-fonts) for the full list, including the
`GlyphCompound.getGlyphIndex` → `GetGlyphIndex` rename and the `CmapTable.CMaps`/
`GlyphCompound.Instructions` immutability changes. This also includes `CmapSubtable.PlatformID` and
`PlatformSpecificID`, which now use `ushort` to match their unsigned 16-bit wire fields.

The static `FontSupport` name, charset, and encoding tables now expose `IReadOnlyList<string>` or
`IReadOnlyList<int>` rather than mutable arrays. Continue to use indexing and enumeration, replace
`Length` with `Count`, and make any required mutable copy explicit, for example:

```csharp
var copy = FontSupport.StdNames.ToArray();
```
