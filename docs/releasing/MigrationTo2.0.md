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

The repository-wide ApiCompat run against verified latest stable packages reports the following accepted major-version incompatibility counts: Core 114; IO 7; XML 1; Net 14; Data 3; Fonts 22; Imaging 7; Geography 23; Reflection 3; Mathematics 19; OData 10; VirtualMachine 7; and three each for the OData, IO serialization, and dependency-injection generators. DependencyInjection runtime is binary compatible in the automated comparison. Collections, NumberToString, and parser packages establish first candidate baselines.

Each accepted diagnostic is pinned by diagnostic ID and exact message in `eng/api-breaking-changes/2.0.0.json`. The human-review inventory in [Accepted API breaks](AcceptedApiBreaks.md) groups the exact removed or incompatible surface by package and links every acceptance back to its package section. New diagnostics, stale acceptances, and missing migration anchors all fail the gate. The counts include removed types/members and changed signatures or constraints; they are not behavioral guarantees or rename inference. The package-specific raw reports under `artifacts/api-compat` and structured `public-api-comparison.json` are the authoritative review inputs; consumers must recompile and exercise their own usage.

<a id="utils-io-serialization-2"></a>
## Utils.IO serialization and stream changes

Version 2.0 intentionally removes the legacy `StreamCopier`, `StreamValidator`, `BaseDecoderStream`, `ReadToEnd`, `ReadArray`, and `WriteVariableLengthString` signatures listed in the versioned API-breaking-change manifest. Consumers should migrate to the bounded/configurable overloads described by the current API documentation rather than restore 1.x shims.

`ReaderWriterGenerator` is now an `IIncrementalGenerator`. Generated extension methods no longer use a type's simple name: their deterministic name contains the namespace, containing types, metadata arity, and a stable FNV-1a suffix. Call sites must use the newly generated method name visible in IntelliSense.

Runtime reader converters require an exact result type. A converter returning an interface or base class is not used for a concrete `Read<T>` because it cannot guarantee `T`. Writers may use a base/interface converter; the most specific applicable registration wins and equal-specificity candidates are rejected.

`PartialStream` serializes reads, writes, position changes, seeking, and length changes through one sync/async-compatible gate. Bounds are evaluated while holding that gate, async wait and I/O honor cancellation, failed writes do not advance the logical position, and each operation restores the underlying stream position.

## NumberToString rule precedence

Version 2.0 removes declaration order as an implicit tie-breaker. Add `priority="100"` (or another intentional signed `xs:int` value) when compatible rules have equal canonical specificity. Ordinal variants and trigger forms select the greatest specificity and then greatest priority. Cumulative variants apply the least specific and lowest-priority rules first.

Programmatic trigger forms must migrate from `(Constraints, To)` tuples to `NumberToStringConverter.TriggerReplacementForm`. `VariantRule` and `OrdinalVariantRule` constructors accept an optional final `priority` argument. Configurations inherited through `baseOn` retain parent priorities; parent and child candidates are validated together and no implicit override occurs.

## omy.Utils.Net protocol clients

Use `SmtpPath` and `SmtpMailOptions` for SMTP envelopes, handle `ProtocolResponseException` for negative responses, and recreate a client after `ProtocolSessionPoisonedException`. POP3 STAT sizes are `long`; NNTP `NewNewsAsync` returns message-id strings; and `NextAsync` returns null only for response 421. Prefer the new `TextReader`/`TextWriter` streaming overloads for large payloads.
