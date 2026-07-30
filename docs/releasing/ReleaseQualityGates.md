# Release quality gates

Run the complete local chain with:

```powershell
./eng/run-release-quality-gates.ps1 -Configuration Release
```

The orchestrator uses an isolated NuGet cache and restores its environment. It restores, builds, and tests the solution; discovers projects; validates the package graph and versions; builds and packs the full train; inspects archives; restores package-only consumers; checks public APIs and warnings; validates SourceLink source retrieval and checksums; performs two clean reproducibility builds; audits dependencies; and creates a self-verifying candidate manifest.

Reports are written below `artifacts/reports`, immutable-candidate metadata below `artifacts/manifests`, and packages below `artifacts/packages`. Failed packaged-acceptance workspaces and logs are retained. Successful temporary consumers are removed after shutting down MSBuild servers to accommodate Windows locks.

Packaged acceptance uses one isolated NuGet global-packages cache for the job. Each
generated consumer has a distinct top-level package graph and therefore receives one
explicit restore to produce and inspect its own `project.assets.json`; these restores
reuse the shared cache rather than forcing network downloads. Every subsequent build,
run, generator matrix, and publish for that consumer uses `--no-restore`. This preserves
independent proof that each `omy.*` reference resolves from the candidate feed instead
of replacing candidates with project references or claiming that all distinct graphs
can be restored by one MSBuild invocation.

## Failure policy

Undeclared or ambiguous projects, duplicate IDs, graph cycles, divergent versions, permissive internal dependency versions, unexpected archive contents, missing symbols/readmes/licenses, API breaks without a manifest acceptance, expired warning/dependency exceptions, SourceLink retrieval failures, content-level reproducibility differences, vulnerable product dependencies, and manifest hash differences are blocking.

Warning exceptions are project-and-code specific in `eng/release-warning-exceptions.json`; dependency exceptions require scope, reason, tracking issue, and expiry in `eng/dependency-exceptions.json`. No wildcard exception is allowed.

## CI validation tiers

Pull requests use `.github/workflows/dotnetcore.yml`. Build, blocking vulnerability
checks, unit and functional tests run first. A dedicated Ubuntu job then discovers the
packable projects and produces the canonical package train exactly once. Package
acceptance, API compatibility, release-warning checks, and local SourceLink mapping,
PDB, and checksum checks run as individually visible steps on Ubuntu and Windows after
both jobs download that same immutable package artifact. Neither validation job packs
the projects. Reproducibility remains a separate two-build check on Ubuntu; it measures
repeatability in one controlled environment rather than comparing independent builds
from different operating systems. Job limits are 25 minutes for build/tests, 35 minutes
for canonical packaging, 55 minutes for packaged acceptance, and 35 minutes for
reproducibility. Every job retains available reports and logs after failure or cancellation.

The full workflow, `.github/workflows/release-quality-gates.yml`, runs weekly, for
`v*` tags, and on manual dispatch. It retains the remote SourceLink download, deprecated
and outdated dependency reports, complete build/test pass, and all package consumers
on Ubuntu and Windows. Its two-build reproducibility check runs once on Ubuntu. The
120-minute full-validation limit and per-command limits bound external tools while
leaving diagnostic logs that name the command, exit code, and elapsed time.
Native commands create their log and print the command before process startup, then
stream stdout and stderr to both the Actions console and the log. Each log ends with a
SUCCESS, FAILED, or TIMEOUT marker. After a timeout, process-tree termination and
redirected-stream draining have their own bounded grace period, so cleanup cannot turn
an expired command into an unlimited wait.

For a fast local check after build and test jobs have already passed, run:

```powershell
./eng/run-release-quality-gates.ps1 -Mode PullRequest -Configuration Release
```

To inspect which gates a mode would execute without invoking external tools, add
`-PlanOnly`. Before publishing, run the complete non-publishing validation:

```powershell
./eng/run-release-quality-gates.ps1 -Mode FullRelease -Configuration Release
```

The full workflow validates candidates but never publishes them. Publication remains
a separate, explicitly authorized workflow. A release pull request should remain a
draft until the pull-request workflow has succeeded on Ubuntu and Windows.

### Run 2397 diagnosis

Run 2397 placed restore, build, both test suites, packaging and every package consumer,
generator matrices, API checks, warning checks, network SourceLink retrieval, two
isolated reproducibility builds, dependency audits, and manifest generation inside one
opaque step on both operating systems. Windows reached the job timeout during that
step, so the active gate and command were not visible and dependent Linux/functional
work was cancelled. The tiered workflows remove repeated PR build/test work from the
orchestrator, expose each mandatory PR gate, and reserve remote/advisory release work
for the full workflow rather than merely increasing the old timeout.

## Publication-compatible validation artifact

The Ubuntu canonical-packages job records the SHA-256 of every `.nupkg` and `.snupkg`
in `canonical-packages.json`. Both platform validation jobs recalculate those hashes
before running consumers and copy them into their acceptance reports. Assembly verifies
that Ubuntu and Windows tested every canonical file with exactly the recorded hash; it
does not compare independently rebuilt platform packages. It copies only the canonical
package directory, combines the platform validation reports and the distinct Ubuntu
reproducibility report, then generates and self-validates
`artifacts/manifests/release-candidate-manifest.json`. The resulting
`full-product-train-<sha>` artifact preserves the contract consumed by
`publish-validated-product-train.yml`; pull requests still never publish packages.
The artifact contract can be checked locally without contacting NuGet by running
`./eng/publish-product-train.ps1 -ArtifactsPath <path> -ValidateCandidateOnly`.
