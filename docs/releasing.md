# Releasing and publishing

Use this guide to align GitHub releases with NuGet publishing for the `omy.Utils` package family.

## Versioning and tags

1. Update the synchronized solution version in `Directory.Build.targets` when preparing a release. Do not version individual `.csproj` files independently.
2. Add release notes to `CHANGELOG.md` under a new version heading.
3. Create a Git tag matching the package version (for example `v2.0.0-rc.1`).

All projects inherit the same `Version` and `PackageVersion`. `Directory.Build.targets` is evaluated after project files and therefore overrides legacy project-local `<Version>` values until those declarations are removed.

## GitHub release flow

1. Push the release commit to the `release` branch.
2. Create a GitHub release from the tag (`vX.Y.Z` or `vX.Y.Z-prerelease`) and paste the changelog entry as the release notes.
3. Attach any additional binaries or documentation if needed.

## CI publishing pipeline

- The `Publish NuGet` workflow (`.github/workflows/nuget-publish.yml`) runs on pushes to the `release` branch.
- It restores, builds, packs, and publishes only packages whose effective `<PackageVersion>` is not already present on nuget.org.
- The workflow checks NuGet to ensure the package version is not already published before uploading.
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
