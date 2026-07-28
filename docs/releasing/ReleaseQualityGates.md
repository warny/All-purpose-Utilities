# Release quality gates

Run the complete local chain with:

```powershell
./eng/run-release-quality-gates.ps1 -Configuration Release
```

The orchestrator uses an isolated NuGet cache and restores its environment. It restores, builds, and tests the solution; discovers projects; validates the package graph and versions; builds and packs the full train; inspects archives; restores package-only consumers; checks public APIs and warnings; validates SourceLink source retrieval and checksums; performs two clean reproducibility builds; audits dependencies; and creates a self-verifying candidate manifest.

Reports are written below `artifacts/reports`, immutable-candidate metadata below `artifacts/manifests`, and packages below `artifacts/packages`. Failed packaged-acceptance workspaces and logs are retained. Successful temporary consumers are removed after shutting down MSBuild servers to accommodate Windows locks.

## Failure policy

Undeclared or ambiguous projects, duplicate IDs, graph cycles, divergent versions, permissive internal dependency versions, unexpected archive contents, missing symbols/readmes/licenses, API breaks without a manifest acceptance, expired warning/dependency exceptions, SourceLink retrieval failures, content-level reproducibility differences, vulnerable product dependencies, and manifest hash differences are blocking.

Warning exceptions are project-and-code specific in `eng/release-warning-exceptions.json`; dependency exceptions require scope, reason, tracking issue, and expiry in `eng/dependency-exceptions.json`. No wildcard exception is allowed.
