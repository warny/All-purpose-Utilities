# Provisional versioning

Most packages in `eng/product-train-manifest.json` follow the synchronized `omy.Utils` product
train: they declare `<Version>$(ProductTrainVersion)</Version>` and are validated, packed, and
published at exactly `ProductTrainVersion` (`Directory.Build.props`, currently `2.0.0-rc.1`).

A small number of components are not yet mature enough to make that commitment, but still need to
be packed and published as real artifacts (NuGet packages, or - for the Visual Studio extension -
a VSIX). For those, the manifest models an explicit, independent version instead of silently
inheriting the train's version or being excluded from the train altogether.

## The `versionPolicy` field

Every entry in `eng/product-train-manifest.json`'s `packages` array has a version maturity policy,
resolved by `eng/Release.Common.ps1`'s `Get-PackageVersionPolicy`/`Get-PackageVersion`:

- **`"product-train"` (default when the field is absent):** the package's `<Version>` must be the
  literal MSBuild property reference `$(ProductTrainVersion)`, and every release gate validates it
  against the manifest's `version` field.
- **`"provisional"`:** the package declares its own literal `<Version>` and a matching
  `"provisionalVersion"` field in the manifest entry. Every release gate validates the package
  against that literal instead of the train version.

This is a deliberate, explicit field - it is never inferred from `publicApiPolicy` or any other
attribute, because packages that are `publicApiPolicy: "first-candidate"` (no published NuGet
baseline yet) are not automatically "provisional": most of them (for example
`omy.Utils.NumberToString`, or the `omy.Utils.Parser.*` family) are still expected to ship at the
train's version. Only a package explicitly marked `"versionPolicy": "provisional"` gets its own
version.

## Current provisional components

| Component | Provisional version | Reason |
|---|---|---|
| `omy.Utils.Collections` (NuGet package) | `0.0.1` | Only contains `SkipList`/`SkipListDictionary` today; not yet considered mature enough to join the 2.0 product train as a stable component. |
| `Utils.Parser.VisualStudio` (VSIX, not a NuGet package - see below) | `0.0.1` | The VSIX version format has no representation for a SemVer prerelease suffix like `2.0.0-rc.1`; see [VSIX versioning and release](VisualStudioExtension.md). |

Both will jump directly to the product train's stable version (e.g. `2.0.0`) once they are
considered mature/stable enough - there is no obligation to publish an intermediate `1.x` or to
keep incrementing `0.0.x` once the train itself is stable.

## The VSIX is not a NuGet package

The VSIX is deliberately **not** modeled as a `versionPolicy`-carrying entry in the manifest's
`packages` array: that array (and the release gates that iterate it - packing, public API
comparison, SourceLink validation, packaged-consumer acceptance) is specifically about the NuGet
product train. The VSIX already has its own entry in the manifest's `exclusions` array
(`"classification": "vsix"`), and its own independent version policy is documented and enforced
separately (`docs/releasing/VisualStudioExtension.md`, `eng/test-vsix-package.ps1`). Forcing it
into the NuGet-shaped `packages` array purely to reuse the same field name would blur what that
array actually represents.

## Solution logo

All publishable NuGet packages and the VSIX reuse a single source file,
[`res/AllPurposeUtilities_logo.png`](../../res/AllPurposeUtilities_logo.png), referenced by the
`SolutionLogoPath` MSBuild property in `Directory.Build.props`. No project keeps its own copy:

- **NuGet packages:** `Directory.Build.props` sets `PackageIcon` and packs `SolutionLogoPath` at
  the archive root for every project where `IsPackable != 'false'`, with no per-project
  configuration needed.
- **VSIX:** `Utils.Parser.VisualStudio.csproj` links `SolutionLogoPath` into the VSIX at
  `Resources\AllPurposeUtilities_logo.png`, matching the `<Icon>` element declared in
  `source.extension.vsixmanifest`.

Only the packaged copies inside `.nupkg`/`.vsix` archives are packaging artifacts; the repository
itself keeps exactly one physical copy of the image.
