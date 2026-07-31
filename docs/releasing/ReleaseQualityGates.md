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

| Tier | Trigger | Gates | Target | Artifact | Publishable |
|---|---|---|---:|---|---|
| Lightweight pull request | Ordinary documentation outside `docs/releasing` | Path classification and lightweight script checks | Under 2 minutes | None | No |
| Ordinary code pull request | Code or test change | Parallel build, tests, one canonical pack, four representative consumers on Ubuntu and Windows, API, warnings, local SourceLink | 5–10 minutes | Diagnostic reports only | No |
| Package pull request | Package or release-infrastructure change | Same fast gates with canonical package inspection and cross-platform consumers | 10–15 minutes maximum | Diagnostic reports only | No |
| Full release | `master`, weekly, `v*`, or manual | Every consumer and matrix, reproducibility, remote SourceLink, vulnerable/deprecated/outdated audits, complete manifest | Recorded by Actions | `full-product-train-*` | Yes, after separate approval |

The deterministic pull-request set contains `UtilsConsumer` (root package and net8),
`ParserRuntimeConsumer` (complex composition and internal dependencies),
`ParserGeneratorConsumer` (source generation), and
`DependencyInjectionGeneratorConsumer` (analyzer composition and net9). PullRequest restores, builds, and executes only these representative projects; canonical inspection still validates every archive and hash. FullRelease retains every consumer, the generated per-package consumers, and every specialized matrix.

The framework coverage is derived from the selected consumer project files and tested
offline: the PR subset currently covers both `net8.0` and `net9.0`. Selection and gate
flags are defined by `eng/get-packaged-validation-plan.ps1`, preventing documentation,
tests, and packaged acceptance from maintaining separate lists.

The `changes` job uses `eng/get-validation-scope.ps1`, not a third-party path action.
Ordinary non-release documentation skips package production and both platform runners.
Changes below `eng`, `.github/workflows`, and `docs/releasing` run release-script tests.
Manual dispatch has no pull-request base SHA, so it explicitly uses `-ForceProductTrain`
and always runs the complete PR validation rather than attempting an empty diff.

Pull requests do not assemble a publication candidate. Their final JSON is a small,
explicitly non-publishable status summary. Only FullRelease assembles a candidate and
sets `validationTier: full-release` with `reproducibilityValidated: true` after the
two-build Ubuntu reproducibility gate. Publication-capable commands continue to reject
anything else before contacting NuGet.

Pull requests use `.github/workflows/dotnetcore.yml`. Build, unit tests, functional tests, and canonical packaging start in parallel immediately after path classification. Canonical packaging unlocks three parallel branches: Ubuntu packaged acceptance, Windows packaged acceptance, and Ubuntu-only source gates. Both platform jobs download and hash-check the same canonical package artifact; neither packs projects. API compatibility, warnings, and local SourceLink run once on Ubuntu. Reproducibility, remote SourceLink, deprecated/outdated audits, per-consumer vulnerability scans, and exhaustive specialized scenarios remain outside the PR critical path. Each package job uses an isolated `NUGET_PACKAGES` directory, so the ordinary NuGet cache cannot replace the canonical feed. The final `required` job only evaluates job results and emits best-effort timing plus a non-publishable status report; it downloads no validation artifacts. The previous PR run exceeded 30 minutes before failing during candidate assembly. That assembly has been removed; observed replacement timings are recorded per job in `workflow-timings.json` and should be added here after the first completed run.

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

## Versioned API breaking-change manifest

The 2.0 product train points each published-baseline package to `eng/api-breaking-changes/2.0.0.json`. The public API gate compares exact normalized ApiCompat diagnostic IDs and messages with that manifest. Both undeclared diagnostics and stale manifest entries fail the gate; accepted entries require a reason and a checked-in migration anchor. Raw and Markdown comparison reports remain release artifacts. This keeps major-version breaks blocking unless they are explicitly reviewed rather than making APICompat informational.
