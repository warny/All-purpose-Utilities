# Parser product-train packaging audit

## Project and package graph

| Project reference | Distribution decision | Public-type exposure | TFM and consumer status |
|---|---|---|---|
| Diagnostics -> Source | NuGet dependency at the synchronized parser version | Diagnostics use shared source locations | `netstandard2.0`; usable by compatible compiler/runtime consumers |
| Parser -> Source, Diagnostics, Antlr4.Common | Three explicit NuGet dependencies | Parser diagnostics and source contracts cross public boundaries; common composition types are primarily internal | Parser `net8.0`; dependencies `netstandard2.0` |
| Expressions -> Parser, Utils | Two explicit NuGet dependencies | Expression policies implement parser interfaces and use Utils expression contracts | `net8.0` |
| Generators -> Diagnostics, Antlr4.Common (Diagnostics -> Source) | Embedded analyzer-host DLLs, suppressed from runtime dependency groups | Not application runtime API | Generator and support DLLs `netstandard2.0` |

Topological publication order is `Utils` / `Source`, then `Diagnostics` / `Antlr4.Common`, then `Parser`, then `Expressions` / `Generators`. `omy.Utils` 1.2.2 follows the currently published 1.2.1 patch because the runtime resource delivery fix is compatible. Parser packages were not found on NuGet during the audit and use the documented `2.0.0-rc.1` train version.

## Generator findings

The generator package deliberately embeds four analyzer-directory DLLs and one `buildTransitive` targets file. Its support projects are built before `pack --no-build`; hard-coded `bin/$(Configuration)/netstandard2.0` inputs remain a maintenance risk, but clean candidate staging and archive inspection prevent stale packages from being silently reused. Roslyn load failures, generated-file emission, and attach-file settings are exercised by a real consumer. Different SDK minor-version behavior remains a CI/platform observation rather than a compatibility promise.

## Runtime resources and platform findings

`DateFormulaConfiguration.json` was the only product-train runtime file loaded from `AppContext.BaseDirectory`; it is now an assembly resource. Parser filesystem APIs accept caller-selected grammar paths and are not missing package payloads. The train targets stable `net8.0` and `netstandard2.0`; no native dependency was found. Unsafe code remains confined to `omy.Utils`' documented implementation. The runtime and expressions projects compile without `EnablePreviewFeatures`; package-only consumers enforce the same setting. Single-file, trimming, and AOT support are not claimed.

## Existing publication workflow risks

The release workflow discovers every project recursively, packs and pushes inside one loop, and can therefore select unintended packages or leave a partial release when a later push fails. Existence-check network failures are treated like an absent package. It does not validate an immutable candidate set, internal dependency coherence, SourceLink, symbol contents, consumer restoration, or a pre-push vulnerability report. A duplicate version is skipped independently. Package publication cannot be transactional. The acceptance scripts intentionally stop before any push and provide manifest-selected artifacts for a future validate-then-publish workflow.
