# Releasing and publishing

Use this guide to align GitHub releases with NuGet publishing for the `omy.Utils` package family.

## Versioning and tags

1. Update `ProductTrainVersion` in `Directory.Build.props` when preparing a release. Do not version individual `.csproj` files independently for any project listed in `eng/product-train-manifest.json`'s `packages` array. A small number of components are deliberately kept out of that array entirely and declare their own literal version instead - see [provisional versioning](releasing/ProvisionalVersioning.md).
2. Add release notes to `CHANGELOG.md` under a new version heading.
3. Create a Git tag matching the package version (for example `v2.0.0-rc.1`).

Every product-train package (every entry in `eng/product-train-manifest.json`'s `packages` array) uses `ProductTrainVersion` from `Directory.Build.props` as its version authority - there is no per-package exception inside that array. The release gate rejects project-local `PackageVersion`, assembly/file version overrides, undeclared hard-coded versions, divergent evaluated MSBuild properties, dependencies, assets, or artifacts.

A small number of components are intentionally kept out of the `packages` array and versioned independently instead: `omy.Utils.Collections` is currently packaged and published separately at its own literal `0.0.1`. See [provisional versioning](releasing/ProvisionalVersioning.md) for the full policy.

## GitHub release flow

1. Push the release commit to the `release` branch.
2. Create a GitHub release from the tag (`vX.Y.Z` or `vX.Y.Z-prerelease`) and paste the changelog entry as the release notes.
3. Attach any additional binaries or documentation if needed.

## CI publishing pipeline

No workflow in this repository pushes a package to NuGet. `NUGET_API_KEY` is not referenced by any
workflow; `dotnet nuget push` only happens inside `eng/publish-product-train.ps1`'s `-Publish` code
path, which nothing here invokes automatically. The pipeline is validate → assemble a candidate →
plan a dry run → **a human runs the actual push manually**, not push-on-merge:

1. **`.github/workflows/nuget-publish.yml`** (`Validate NuGet Release`, runs on pushes to `release`/`releases/**`) builds the solution, packs and runs packaged-consumer acceptance against the manifest-selected packages (`eng/test-packaged-product-train.ps1`), then checks NuGet package-ID *availability only* (`eng/publish-product-train.ps1 -PreflightPackageIdsOnly`) - it does not validate a specific candidate manifest and does not push anything. It also builds and validates the VSIX in a separate job, again without publishing it anywhere.
2. **`.github/workflows/release-quality-gates.yml`** (`Full release quality gates`, runs on pushes to `master`, on version tags, weekly, or via manual dispatch) runs the complete validation chain - canonical packaging, Ubuntu/Windows packaged acceptance, API compatibility, SourceLink, reproducibility, release-warnings, dependency audit - and assembles the immutable `FullRelease` candidate, uploading it as the `full-product-train-<sha>` artifact. This is the run whose ID feeds the next step.
3. **`.github/workflows/publish-validated-product-train.yml`** (`Plan validated product-train publication`, manual `workflow_dispatch` only, taking a `validation-run-id` input) downloads that exact `full-product-train-<sha>` artifact from the specified `release-quality-gates.yml` run and calls `eng/publish-product-train.ps1 -ArtifactsPath artifacts` **without `-Publish`** - it verifies the candidate's hashes and remote NuGet state (zero or all packages already present) and uploads a dry-run `publication-plan.json`. It still does not push anything.
4. **The actual publish is a deliberate, manual, human-run command** against those same validated artifacts. This is the only code path that calls `dotnet nuget push`, and it is intentionally not wired into any workflow trigger. It always queries every manifested NuGet package ID first, and always pushes each package's `.nupkg` (with `--no-symbols`) and then its `.snupkg` separately - never relying on `dotnet nuget push`'s automatic "also push the matching symbol package" behavior, since that would push every `.snupkg` twice (once automatically, once explicitly) and fail or conflict on the second push.

   **Normal publication** requires the remote state to be entirely empty (no manifested package/version already exists):

   ```powershell
   ./eng/publish-product-train.ps1 -ArtifactsPath <path-to-the-downloaded-artifact> -Publish -ApiKey <NuGet API key>
   ```

   A *partial* remote state (some but not all manifested packages already published at this version) is never auto-accepted here - it fails with a diagnostic, whether this is a dry run, `-PreflightPackageIdsOnly`, or this exact `-Publish` command without the flag below. This is deliberate: an unexpected partial state (out-of-band push, corruption) must not be silently treated as a resume.

   **Resuming a known, previously-interrupted publication** of this exact candidate is the only way to proceed past a partial remote state, and it must be requested explicitly:

   ```powershell
   ./eng/publish-product-train.ps1 -ArtifactsPath <path-to-the-downloaded-artifact> -Publish -ResumePartialPublication -ApiKey <NuGet API key>
   ```

   This reuses the same validated candidate and pushes every package's `.nupkg` and `.snupkg` again, but adds `--skip-duplicate` to each push: NuGet guarantees a published id+version's content is immutable, so a re-push of an already-published artifact is turned into a harmless no-op instead of an error, while an artifact that never actually made it (for example, a `.snupkg` whose push failed right after its `.nupkg` succeeded) still gets a real, effective push. `-ResumePartialPublication` requires `-Publish` and does nothing on its own. Do not reach for it to explain away a partial state whose origin you have not actually confirmed to be a known interrupted run of this script - it exists for that one specific, deliberate scenario, not as a general-purpose override.

## Validating packages

After a release completes:

- Download the generated `.nupkg` artifacts from the workflow run to verify contents (including README files).
- Confirm that every produced package uses the synchronized solution version.
- Install a package locally to confirm the version and metadata:

```bash
dotnet new console -n UtilsPackageCheck
cd UtilsPackageCheck
dotnet add package omy.Utils --version 2.0.0-rc.1
```

- Review the package page on nuget.org to confirm the README and metadata render correctly.

## Repository-wide 2.0.0 candidate

The complete process is documented in the [product-train overview](releasing/ProductTrain.md), [quality-gate reference](releasing/ReleaseQualityGates.md), [derived package graph](releasing/PackageGraph.md), and [2.0 migration guide](releasing/MigrationTo2.0.md). The release candidate covers every manifested library and source generator, not only parser packages.

## Visual Studio extension (VSIX)

`Utils.Parser.VisualStudio` is excluded from the NuGet product train above and follows its own
provisional version series until the train reaches a stable `2.0.0`. See the
[VSIX versioning and release guide](releasing/VisualStudioExtension.md) for the policy, and
[`Utils.Parser.VisualStudio/README.md`](../Utils.Parser.VisualStudio/README.md) for the
Marketplace-facing description, features, and manual publication checklist.

## Provisional components

`omy.Utils.Collections` and the `Utils.Parser.VisualStudio` VSIX both ship at independent,
provisional versions rather than the product train's version, for different reasons. See
[provisional versioning](releasing/ProvisionalVersioning.md) for the shared policy and how it is
enforced.

Neither is touched by the CI publishing pipeline above: `eng/product-train-manifest.json`'s
`packages` array (what every step in that pipeline iterates) excludes both. `omy.Utils.Collections`
is packed and published as its own, entirely separate, manual step - there is no dedicated workflow
for it today, so it also skips every train-wide package gate (canonical packaging, inspection,
SourceLink validation, packaged-consumer acceptance, API compatibility, reproducibility). Run
`eng/test-provisional-package.ps1` first as a lighter, package-scoped substitute for those gates -
it packs the real project, inspects the resulting archives, validates SourceLink, and restores/
builds/runs a minimal real consumer against the packed `.nupkg`:

```bash
pwsh -NoProfile -File ./eng/test-provisional-package.ps1
dotnet pack Utils.Collections/Utils.Collections.csproj -c Release -o artifacts/provisional
dotnet nuget push artifacts/provisional/omy.Utils.Collections.0.0.1.nupkg --api-key <NuGet API key> --source https://api.nuget.org/v3/index.json
```

The VSIX's first Marketplace publication is likewise manual - see
[VSIX versioning and release](releasing/VisualStudioExtension.md) for its checklist (and its
separate `VSEXTPREVIEW_TAGGERS` blocker, unrelated to NuGet publishing).

## Solution logo

Every publishable NuGet package and the VSIX use the same source image,
[`res/AllPurposeUtilities_logo.png`](../res/AllPurposeUtilities_logo.png), centralized via
`Directory.Build.props`'s `SolutionLogoPath` property. See
[provisional versioning](releasing/ProvisionalVersioning.md#solution-logo) for how it is wired up;
no project keeps its own copy of the image.
