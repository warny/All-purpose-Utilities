# Migrating the package family to 2.0.0-rc.1

This is a coordinated major-version release candidate. Update all direct `omy.Utils*` references together and remove mixed 1.x, 2.0.0, `0.0.1`, and preview references. Internal dependencies are exact at `2.0.0-rc.1`.

## Families

- **Core:** review the detailed [`omy.Utils` 1.2.1 audit](../api/omy.Utils-1.2.1-to-2.0.0-rc.1.md), including removed expression/number formatting APIs, enumerable overloads, nullability, and embedded DateFormula resources.
- **IO and serialization, Networking, Data, Imaging and fonts, Geography, Mathematics and collections, OData, Dependency injection, Virtual machine, and Number formatting:** recompile against the candidate and review the generated package-specific ApiCompat report. The manifest records the verified latest stable baseline: mostly 1.2.1, OData and OData.Generators 0.0.1, VirtualMachine 0.1.0, and first-candidate baselines for unpublished packages.
- **Reflection:** compare against the latest published 1.2.1 package and account for the isolated worker behavior already documented in the changelog.
- **Parser:** these packages establish their first public candidate baseline and remain governed by the parser production support contract.
- **Source generators:** reference generators as analyzers with `ReferenceOutputAssembly="false"`; do not reference their implementation assemblies directly.

ApiCompat findings are accepted only for this coordinated major candidate and remain visible in `artifacts/reports/public-api-comparison.*`. Validate application-specific behavior before production deployment.

## Reviewed binary-compatibility findings

The repository-wide ApiCompat run against verified latest stable packages reports the following accepted major-version incompatibility counts: Core 114; IO 7; XML 1; Net 14; Data 3; Fonts 22; Imaging 7; Geography 23; Reflection 3; Mathematics 19; OData 10; VirtualMachine 7; OData generator 3; IO serialization generator 3; and dependency-injection generator 3. DependencyInjection runtime is binary compatible in the automated comparison. Collections, NumberToString, and parser packages establish first candidate baselines.

These counts include removed types/members, changed signatures and constraints, assembly identity changes, and analyzer assembly-shape differences. They are not behavioral guarantees or rename inference. The package-specific raw reports under `artifacts/api-compat` and structured `public-api-comparison.json` are the authoritative review inputs; consumers must recompile and exercise their own usage.
