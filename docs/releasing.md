# Releasing and publishing

Use this guide to align GitHub releases with NuGet publishing for the `omy.Utils` package family.

## Versioning and tags

1. Update package versions in the respective `.csproj` files when preparing a release.
2. Add release notes to `CHANGELOG.md` under a new version heading.
3. Create a Git tag matching the package version (for example `v1.2.1`).

## GitHub release flow

1. Push the release commit to the `release` branch.
2. Create a GitHub release from the tag (`vX.Y.Z`) and paste the changelog entry as the release notes.
3. Attach any additional binaries or documentation if needed.

## CI publishing pipeline

- The `Publish NuGet` workflow builds the solution on the `release` branch and publishes NuGet packages when their version number changed.
- The workflow checks NuGet to ensure the package version is not already published before uploading.
- Packages are pushed using the `NUGET_API_KEY` secret configured in the repository settings.

## Validating packages

After a release completes:

- Download the generated `.nupkg` artifacts from the workflow run to verify contents (including README files).
- Install a package locally to confirm the version and metadata:

```bash
dotnet new console -n UtilsPackageCheck
cd UtilsPackageCheck
dotnet add package omy.Utils --version <version>
```

- Review the package page on nuget.org to confirm the README and metadata render correctly.
