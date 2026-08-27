# Releasing and publishing

Use this guide to align GitHub releases with NuGet publishing for the `omy.Utils` package family.

## Versioning and tags

1. Update the synchronized solution version in `Directory.Build.targets` when preparing a release. Do not version individual `.csproj` files independently.
2. Add release notes to `CHANGELOG.md` under a new version heading.
3. Create a Git tag matching the package version (for example `v2.0.0-rc.1`).

Most of the 24 manifested projects use `ProductTrainVersion` from `Directory.Build.props` as their version authority. A small number are explicitly marked `"versionPolicy": "provisional"` in `eng/product-train-manifest.json` and instead declare their own literal version - see [provisional versioning](releasing/ProvisionalVersioning.md). Either way, the release gate rejects project-local `PackageVersion`, assembly/file version overrides, undeclared hard-coded versions, divergent evaluated MSBuild properties, dependencies, assets, or artifacts.

## GitHub release flow

1. Push the release commit to the `release` branch.
2. Create a GitHub release from the tag (`vX.Y.Z` or `vX.Y.Z-prerelease`) and paste the changelog entry as the release notes.
3. Attach any additional binaries or documentation if needed.

## CI publishing pipeline

- The `Publish NuGet` workflow (`.github/workflows/nuget-publish.yml`) runs on pushes to the `release` branch.
- It validates and packs only the 24 manifest-selected packages, then checks package metadata, assemblies, internal dependencies, isolated consumer assets, API compatibility, and reproducibility.
- Before the first push, it queries all 24 NuGet package IDs. Exactly zero or all 24 may exist. A partial remote train fails with a diagnostic and no automatic push.
- A fully absent train is pushed in manifest topological order as the single logical `omy 2.0.0-rc.1` release; the dry-run command omits `-Publish`.
- Packages are pushed using the `NUGET_API_KEY` secret configured in the repository settings.

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

## Solution logo

Every publishable NuGet package and the VSIX use the same source image,
[`res/AllPurposeUtilities_logo.png`](../res/AllPurposeUtilities_logo.png), centralized via
`Directory.Build.props`'s `SolutionLogoPath` property. See
[provisional versioning](releasing/ProvisionalVersioning.md#solution-logo) for how it is wired up;
no project keeps its own copy of the image.
